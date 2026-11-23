using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using Velopack;

namespace PinShot
{
    public partial class App : Application
    {
        private static string AppVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "3.4.0";
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [STAThread]
        public static void Main(string[] args)
        {
            VelopackApp.Build().Run();
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private HwndSource? _hwndSource;
        private TrayIconController? _tray;
        private Mutex? _mutex;
        private bool _ownsMutex;

        private bool _isAppEnabled = true;
        private bool _ignoreTextCopies = true;
        private readonly DispatcherTimer _updateCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromHours(6)
        };
        private readonly DispatcherTimer _updateApplyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        private UpdateService? _updateService;
        private VelopackAsset? _pendingUpdate;
        private bool _checkingForUpdates;

        // Espera a que el portapapeles "se calme" y reintenta si está ocupado
        private readonly DispatcherTimer _clipTimer = new DispatcherTimer();
        private int _retries;
        private const int MaxRetries = 12;

        private static DateTime _suppressUntil = DateTime.MinValue;

        // Anti-duplicados: la captura de Windows suele avisar 2 veces
        private DateTime _lastShownAt = DateTime.MinValue;
        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private string _lastHash = "";

        /// <summary>Evita que PinShot reaccione a copias hechas por el propio PinShot.</summary>
        public static void SuppressClipboard()
        {
            _suppressUntil = DateTime.Now.AddMilliseconds(1500);
        }

        // ───────────── Ventanas de notas ─────────────
        private static LibraryWindow? _library;

        public static void OpenLibrary()
        {
            if (_library == null)
            {
                _library = new LibraryWindow();
                _library.Closed += (s, e) => _library = null;
                _library.Show();
            }

            if (_library.WindowState == WindowState.Minimized)
                _library.WindowState = WindowState.Normal;
            _library.Activate();
        }

        public static void OpenNewNote()
        {
            NoteEditorWindow.Open(NoteStore.CreateNew());
        }

        public static void OpenSettings()
        {
            var win = new SettingsWindow();
            win.ShowDialog();
        }

        // ───────────── Inicio ─────────────
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += (s, args) =>
            {
                AppLog.Write("Excepcion no controlada: " + args.Exception);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                AppLog.Write("Excepcion fatal: " + args.ExceptionObject);

            try
            {
                _mutex = new Mutex(true, "PinShot_SingleInstance_v3", out bool created);
                _ownsMutex = created;
                if (!created)
                {
                    System.Windows.MessageBox.Show(
                        "PinShot ya esta en ejecucion.\nBusca su icono en el area de notificaciones (flecha ^ junto al reloj).",
                        "PinShot", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                _clipTimer.Tick += ClipTimer_Tick;
                _updateCheckTimer.Tick += async (s, args) => await CheckForUpdatesAsync();
                _updateApplyTimer.Tick += ApplyUpdateWhenIdle;

                CreateTrayIcon();
                CreateClipboardListenerWindow();
                _updateCheckTimer.Start();
                _ = CheckForUpdatesAsync();

                ShowBalloon("PinShot activo",
                    "Toma una captura (Win+Shift+S) y aparecera flotando. Icono en la flecha ^ si no lo ves.");
                AppLog.Write("PinShot iniciado.");
            }
            catch (Exception ex)
            {
                AppLog.Write("Error al iniciar: " + ex);
                System.Windows.MessageBox.Show(
                    "PinShot no pudo iniciar:\n\n" + ex.Message + "\n\nLog: " + AppLog.Path,
                    "PinShot", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void CreateClipboardListenerWindow()
        {
            var parameters = new HwndSourceParameters("PinShotClipboardListener")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);

            if (!AddClipboardFormatListener(_hwndSource.Handle))
            {
                int err = Marshal.GetLastWin32Error();
                AppLog.Write("AddClipboardFormatListener fallo. Error Win32: " + err);
                ShowBalloon("PinShot", "No se pudo escuchar el portapapeles (error " + err + ").", ToolTipIcon.Error);
            }
        }

        // ───────────── Bandeja ─────────────
        private void CreateTrayIcon()
        {
            _tray = new TrayIconController(
                AppVersion,
                OpenNewNote,
                OpenLibrary,
                OpenSettings,
                CloseAllCaptures,
                Shutdown,
                () => _isAppEnabled,
                SetEnabled,
                () => _ignoreTextCopies,
                value => _ignoreTextCopies = value);
        }

        private void SetEnabled(bool enabled)
        {
            _isAppEnabled = enabled;
            _tray?.SetEnabled(enabled);
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try { _tray?.ShowBalloon(title, text, icon); } catch (Exception ex) { AppLog.Write("No se pudo mostrar aviso: " + ex.Message); }
        }

        private void CloseAllCaptures()
        {
            foreach (var w in Windows.OfType<FloatingCaptureWindow>().ToList())
            {
                try { w.Close(); } catch { }
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_checkingForUpdates || _pendingUpdate != null) return;
            _checkingForUpdates = true;

            try
            {
                _updateService ??= new UpdateService();
                if (!_updateService.IsInstalled) return;

                var update = await _updateService.CheckAndDownloadAsync();
                if (update == null) return;

                _pendingUpdate = update;
                AppLog.Write("Actualización " + update.Version + " descargada; se instalará cuando se cierren las ventanas.");
                _updateApplyTimer.Start();
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo comprobar o descargar actualizaciones: " + ex);
            }
            finally
            {
                _checkingForUpdates = false;
            }
        }

        private void ApplyUpdateWhenIdle(object? sender, EventArgs e)
        {
            if (_pendingUpdate == null || _updateService == null) return;
            if (Windows.Cast<Window>().Any(window => window.IsVisible)) return;

            _updateApplyTimer.Stop();
            AppLog.Write("Aplicando actualización " + _pendingUpdate.Version + " y reiniciando.");
            _updateService.ApplyAndRestart(_pendingUpdate);
        }

        // ───────────── Portapapeles ─────────────
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                handled = true;

                if (_isAppEnabled && DateTime.Now > _suppressUntil)
                {
                    // Cada aviso reinicia la espera: se procesa solo cuando el portapapeles ya se calmo
                    _retries = 0;
                    _clipTimer.Stop();
                    _clipTimer.Interval = TimeSpan.FromMilliseconds(200);
                    _clipTimer.Start();
                }
            }
            return IntPtr.Zero;
        }

        private void ClipTimer_Tick(object? sender, EventArgs e)
        {
            _clipTimer.Stop();

            bool finished;
            try
            {
                finished = TryShowClipboardImage();
            }
            catch (Exception ex)
            {
                AppLog.Write("Portapapeles (intento " + (_retries + 1) + "): " + ex.Message);
                finished = false;
            }

            if (!finished)
            {
                _retries++;
                if (_retries < MaxRetries)
                {
                    _clipTimer.Interval = TimeSpan.FromMilliseconds(150);
                    _clipTimer.Start();
                }
                else
                {
                    AppLog.Write("No se pudo leer la imagen del portapapeles tras varios intentos.");
                    ShowBalloon("PinShot", "No pude leer la captura del portapapeles.", ToolTipIcon.Warning);
                }
            }
        }

        /// <returns>true si termino (mostro la imagen o no habia nada); false si hay que reintentar.</returns>
        private bool TryShowClipboardImage()
        {
            if (!Clipboard.ContainsImage()) return true;

            if (_ignoreTextCopies && (Clipboard.ContainsText() || Clipboard.ContainsFileDropList()))
                return true;

            BitmapSource? image = ReadClipboardImage();
            if (image == null) return false;

            if (IsDuplicate(image)) return true;

            var win = new FloatingCaptureWindow(image);
            win.Show();
            win.Activate();
            return true;
        }

        private bool IsDuplicate(BitmapSource image)
        {
            double elapsed = (DateTime.Now - _lastShownAt).TotalSeconds;
            string hash = ComputeHash(image);

            bool sameSize = image.PixelWidth == _lastWidth && image.PixelHeight == _lastHeight;
            bool duplicate =
                (sameSize && elapsed < 3.0) ||                       // segundo aviso de la misma captura
                (hash.Length > 0 && hash == _lastHash && elapsed < 10.0);

            if (duplicate)
            {
                AppLog.Write("Captura duplicada ignorada.");
                return true;
            }

            _lastShownAt = DateTime.Now;
            _lastWidth = image.PixelWidth;
            _lastHeight = image.PixelHeight;
            _lastHash = hash;
            return false;
        }

        private static string ComputeHash(BitmapSource image)
        {
            try
            {
                var converted = new FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgr32, null, 0);
                int stride = converted.PixelWidth * 4;
                var pixels = new byte[stride * converted.PixelHeight];
                converted.CopyPixels(pixels, stride, 0);
                return Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(pixels));
            }
            catch
            {
                return "";
            }
        }

        internal static BitmapSource? ReadClipboardImage()
        {
            // 1) Formato PNG (conserva bien la transparencia y colores)
            try
            {
                if (Clipboard.ContainsData("PNG") && Clipboard.GetData("PNG") is Stream stream)
                {
                    using (stream)
                    {
                        var decoder = new PngBitmapDecoder(
                            stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var frame = decoder.Frames[0];
                        frame.Freeze();
                        return frame;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write("Lectura PNG fallo, uso formato normal: " + ex.Message);
            }

            // 2) Formato estandar
            BitmapSource? img = Clipboard.GetImage();
            if (img == null) return null;

            var cached = new CachedBitmap(img, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            cached.Freeze();
            return cached;
        }

        // ───────────── Salida ─────────────
        protected override void OnExit(ExitEventArgs e)
        {
            try { _clipTimer.Stop(); } catch { }
            _updateCheckTimer.Stop();
            _updateApplyTimer.Stop();

            if (_hwndSource != null)
            {
                try { RemoveClipboardFormatListener(_hwndSource.Handle); } catch { }
                _hwndSource.Dispose();
            }

            _tray?.Dispose();

            if (_mutex != null)
            {
                try { if (_ownsMutex) _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
            }

            base.OnExit(e);
        }
    }
}
