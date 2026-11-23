using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace PinShot
{
    /// <summary>Una nota: texto + imagenes + color.</summary>
    public class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Modified { get; set; } = DateTime.Now;
        public string Text { get; set; } = "";
        public string Color { get; set; } = "#22C55E";
        public bool IsPinned { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Images { get; set; } = new List<string>();
    }

    public class AppSettings
    {
        public string DefaultColor { get; set; } = "#22C55E";
    }

    /// <summary>
    /// Sistema de guardado. Cada nota es una carpeta en
    /// %LOCALAPPDATA%\PinShot\Notas\{id}\ con nota.json + sus imagenes PNG.
    /// </summary>
    public static class NoteStore
    {
        public static readonly string[] Palette =
        {
            "#22C55E", // verde
            "#3B82F6", // azul
            "#F59E0B", // ambar
            "#EF4444", // rojo
            "#A855F7", // morado
            "#EC4899", // rosado
            "#14B8A6", // turquesa
            "#9CA3AF"  // gris
        };

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions { WriteIndented = true };
        private static AppSettings? _settings;
        internal static string? RootDirectoryOverride { get; set; }

        /// <summary>Se dispara cuando se guarda/borra algo (para refrescar la lista).</summary>
        public static event Action? Changed;

        public static void NotifyChanged() => Changed?.Invoke();

        public static string RootDir =>
            RootDirectoryOverride ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinShot");

        public static string NotesDir => Path.Combine(RootDir, "Notas");
        private static string SettingsPath => Path.Combine(RootDir, "config.json");

        public static string NoteDir(string id) => Path.Combine(NotesDir, id);
        public static string ImagePath(NoteItem n, string file) => Path.Combine(NoteDir(n.Id), file);

        // ───────────── Configuracion ─────────────
        public static AppSettings Settings
        {
            get
            {
                if (_settings == null) _settings = LoadSettings();
                return _settings;
            }
        }

        private static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var s = ReadJson<AppSettings>(SettingsPath);
                    if (s != null) return s;
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo leer config.json: " + ex.Message);
            }
            return new AppSettings();
        }

        public static bool SaveSettings()
        {
            return WriteJsonAtomic(SettingsPath, Settings);
        }

        // ───────────── Notas ─────────────
        public static NoteItem CreateNew()
        {
            return new NoteItem { Color = Settings.DefaultColor };
        }

        public static bool IsEmpty(NoteItem n)
        {
            return string.IsNullOrWhiteSpace(n.Text) &&
                n.Images.Count == 0 &&
                n.Tags.Count == 0 &&
                !n.IsPinned;
        }

        public static List<NoteItem> LoadAll()
        {
            var list = new List<NoteItem>();
            try
            {
                if (!Directory.Exists(NotesDir)) return list;

                foreach (string dir in Directory.GetDirectories(NotesDir))
                {
                    try
                    {
                        string json = Path.Combine(dir, "nota.json");
                        if (!File.Exists(json)) continue;

                        var n = ReadJson<NoteItem>(json);
                        if (n == null) continue;

                        n.Id = Path.GetFileName(dir);
                        n.Text ??= "";
                        n.Tags = n.Tags ?? new List<string>();
                        n.Images = (n.Images ?? new List<string>())
                            .Where(f => Path.GetFileName(f) == f && File.Exists(Path.Combine(dir, f)))
                            .ToList();
                        list.Add(n);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write("Nota ilegible (" + dir + "): " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Error al leer notas: " + ex.Message);
            }

            return list.OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.Modified)
                .ToList();
        }

        public static bool Save(NoteItem n, bool notify = true, bool touch = true)
        {
            if (touch) n.Modified = DateTime.Now;
            string dir = NoteDir(n.Id);
            string jsonPath = Path.Combine(dir, "nota.json");

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo crear la carpeta de la nota " + n.Id + ": " + ex.Message);
                return false;
            }

            bool saved = WriteJsonAtomic(jsonPath, n);
            if (saved && notify) Changed?.Invoke();
            return saved;
        }

        public static bool Delete(NoteItem n)
        {
            try
            {
                string dir = NoteDir(n.Id);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo borrar la nota: " + ex.Message);
                Changed?.Invoke();
                return false;
            }
            Changed?.Invoke();
            return true;
        }

        // ───────────── Imagenes ─────────────
        public static string AddImage(NoteItem n, BitmapSource bmp)
        {
            string dir = NoteDir(n.Id);
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo crear la carpeta para la imagen: " + ex.Message);
                throw;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string name = "img_" + stamp + ".png";
            int i = 1;
            while (File.Exists(Path.Combine(dir, name)))
            {
                name = "img_" + stamp + "_" + i + ".png";
                i++;
            }

            string finalPath = Path.Combine(dir, name);
            string tempPath = finalPath + ".tmp";

            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = File.Create(tempPath))
                {
                    encoder.Save(fs);
                }

                File.Move(tempPath, finalPath);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                AppLog.Write("No se pudo guardar la imagen " + name + ": " + ex.Message);
                throw;
            }

            n.Images.Add(name);
            return name;
        }

        public static bool RemoveImage(NoteItem n, string file)
        {
            try
            {
                string path = ImagePath(n, file);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo quitar la imagen " + file + ": " + ex.Message);
                return false;
            }

            n.Images.Remove(file);
            return true;
        }

        public static BitmapImage? LoadImage(string path, int decodeWidth = 0)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                if (decodeWidth > 0) bi.DecodePixelWidth = decodeWidth;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo cargar imagen " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Guarda una captura (con su texto) como una nota nueva.</summary>
        public static NoteItem SaveCapture(BitmapSource img, string text)
        {
            var n = CreateNew();
            n.Text = (text ?? "").Trim();
            try
            {
                AddImage(n, img);
                if (!Save(n))
                    throw new IOException("No se pudo guardar la nota de la captura.");
                return n;
            }
            catch
            {
                try { if (Directory.Exists(NoteDir(n.Id))) Directory.Delete(NoteDir(n.Id), true); } catch { }
                throw;
            }
        }

        public static void ExportNotes(string archivePath)
        {
            string tempPath = archivePath + ".tmp";
            try
            {
                string destination = Path.GetFullPath(archivePath);
                string notesPrefix = Path.GetFullPath(NotesDir) + Path.DirectorySeparatorChar;
                if (destination.StartsWith(notesPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("El respaldo no se puede guardar dentro de la carpeta de notas.");

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Directory.CreateDirectory(NotesDir);
                if (File.Exists(tempPath)) File.Delete(tempPath);
                ZipFile.CreateFromDirectory(NotesDir, tempPath, CompressionLevel.Optimal, false);
                File.Move(tempPath, archivePath, true);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                AppLog.Write("No se pudieron exportar las notas: " + ex.Message);
                throw;
            }
        }

        public static int ImportNotes(string archivePath)
        {
            string stagingDir = Path.Combine(RootDir, "import-" + Guid.NewGuid().ToString("N"));
            var importedDirs = new List<string>();

            try
            {
                Directory.CreateDirectory(stagingDir);
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    const long maxArchiveSize = 512L * 1024 * 1024;
                    if (archive.Entries.Count > 10000)
                    {
                        throw new InvalidDataException("El archivo de respaldo es demasiado grande.");
                    }

                    long totalSize = 0;
                    var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Length > maxArchiveSize || totalSize > maxArchiveSize - entry.Length)
                            throw new InvalidDataException("El archivo de respaldo es demasiado grande.");
                        totalSize += entry.Length;

                        string[] segments = entry.FullName.Replace('\\', '/')
                            .Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length == 0) continue;
                        if (segments.Length > 2 || segments.Any(segment =>
                            segment == "." || segment == ".." || segment.Contains(':')))
                        {
                            throw new InvalidDataException("El archivo contiene rutas no válidas.");
                        }
                        if (!seenEntries.Add(string.Join("/", segments)))
                            throw new InvalidDataException("El archivo contiene entradas duplicadas.");

                        string targetPath = Path.GetFullPath(Path.Combine(stagingDir,
                            entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                        string stagingPrefix = Path.GetFullPath(stagingDir) + Path.DirectorySeparatorChar;
                        if (!targetPath.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("El archivo contiene una ruta fuera del respaldo.");

                        if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                        {
                            Directory.CreateDirectory(targetPath);
                            continue;
                        }

                        if (segments.Length != 2)
                            throw new InvalidDataException("El respaldo tiene una estructura no válida.");

                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        using var input = entry.Open();
                        using var output = File.Create(targetPath);
                        input.CopyTo(output);
                    }
                }

                Directory.CreateDirectory(NotesDir);
                int imported = 0;
                foreach (string sourceDir in Directory.GetDirectories(stagingDir))
                {
                    string jsonPath = Path.Combine(sourceDir, "nota.json");
                    if (!File.Exists(jsonPath)) continue;

                    var note = JsonSerializer.Deserialize<NoteItem>(File.ReadAllText(jsonPath));
                    if (note == null) continue;

                    note.Id = Guid.NewGuid().ToString("N");
                    note.Text ??= "";
                    note.Tags = note.Tags ?? new List<string>();
                    note.Images = (note.Images ?? new List<string>())
                        .Where(file => Path.GetFileName(file) == file &&
                                       File.Exists(Path.Combine(sourceDir, file)))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    string targetDir = NoteDir(note.Id);
                    Directory.CreateDirectory(targetDir);
                    importedDirs.Add(targetDir);
                    foreach (string image in note.Images)
                        File.Copy(Path.Combine(sourceDir, image), Path.Combine(targetDir, image));

                    if (!Save(note, notify: false, touch: false))
                        throw new IOException("No se pudo guardar una de las notas importadas.");
                    imported++;
                }

                if (imported > 0) Changed?.Invoke();
                return imported;
            }
            catch (Exception ex)
            {
                foreach (string dir in importedDirs)
                {
                    try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
                }
                AppLog.Write("No se pudieron importar las notas: " + ex.Message);
                throw;
            }
            finally
            {
                try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
            }
        }

        private static bool WriteJsonAtomic<T>(string path, T value)
        {
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(tempPath, JsonSerializer.Serialize(value, JsonOpts));

                if (File.Exists(path))
                {
                    File.Copy(path, backupPath, true);
                }

                File.Move(tempPath, path, true);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                AppLog.Write("No se pudo guardar " + path + ": " + ex.Message);
                return false;
            }
        }

        private static T? ReadJson<T>(string path)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo leer " + path + ": " + ex.Message);
            }

            string backupPath = path + ".bak";
            if (!File.Exists(backupPath)) return default;

            try
            {
                var value = JsonSerializer.Deserialize<T>(File.ReadAllText(backupPath));
                if (value != null)
                    AppLog.Write("Se recupero " + path + " desde la copia de seguridad.");
                return value;
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo recuperar " + backupPath + ": " + ex.Message);
                return default;
            }
        }
    }
}
