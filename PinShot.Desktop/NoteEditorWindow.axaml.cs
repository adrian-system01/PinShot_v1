using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using PinShot;

namespace PinShot.Desktop;

public partial class NoteEditorWindow : Window
{
    private static readonly Dictionary<string, NoteEditorWindow> OpenEditors = new();
    private readonly NoteItem _note;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly List<Bitmap> _displayedImages = new();
    private bool _loading = true;
    private bool _deleted;

    public NoteEditorWindow(NoteItem note)
    {
        InitializeComponent();
        _note = note;
        NoteText.Text = note.Text;
        PinButton.Content = note.IsPinned ? "Quitar fijado" : "Fijar";
        DateText.Text = "Editado " + note.Modified.ToString("g");
        _loading = false;

        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Persist();
        };
        NoteStore.Changed += OnStoreChanged;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _saveTimer.Stop();
            NoteStore.Changed -= OnStoreChanged;
            DisposeImages();
            OpenEditors.Remove(_note.Id);
        };

        RebuildTags();
        RebuildImages();
        RebuildPalette();
        ApplyColor();
        SaveStatus.Text = File.Exists(Path.Combine(NoteStore.NoteDir(note.Id), "nota.json"))
            ? "Guardado"
            : "Nueva nota";
    }

    public NoteEditorWindow() : this(NoteStore.CreateNew())
    {
    }

    public static void Open(NoteItem note, Window? owner = null)
    {
        if (OpenEditors.TryGetValue(note.Id, out NoteEditorWindow? existing))
        {
            existing.Activate();
            return;
        }

        var editor = new NoteEditorWindow(note);
        OpenEditors[note.Id] = editor;
        if (owner != null) editor.Show(owner);
        else editor.Show();
        editor.Activate();
    }

    public static void CloseIfOpen(string id)
    {
        if (OpenEditors.TryGetValue(id, out NoteEditorWindow? editor))
        {
            editor._deleted = true;
            editor.Close();
        }
    }

    public static void UpdatePinnedState(string id, bool isPinned)
    {
        if (!OpenEditors.TryGetValue(id, out NoteEditorWindow? editor)) return;
        editor._note.IsPinned = isPinned;
        editor.PinButton.Content = isPinned ? "Quitar fijado" : "Fijar";
    }

    private void OnStoreChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnStoreChanged);
            return;
        }

        RebuildPalette();
        ApplyColor();
    }

    private void RebuildPalette()
    {
        PalettePanel.Children.Clear();
        string[] colors = NoteStore.Settings.ColorBlindMode
            ? NoteStore.AccessiblePalette
            : NoteStore.Palette;

        foreach (string color in colors)
        {
            var button = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(0, 0, 7, 7),
                Background = new SolidColorBrush(Color.Parse(color)),
                BorderBrush = string.Equals(_note.Color, color, StringComparison.OrdinalIgnoreCase)
                    ? Brushes.White
                    : new SolidColorBrush(Color.Parse("#263449")),
                BorderThickness = new Thickness(2)
            };
            ToolTip.SetTip(button, color);
            button.Click += (_, _) =>
            {
                _note.Color = color;
                Persist();
                ApplyColor();
                RebuildPalette();
            };
            PalettePanel.Children.Add(button);
        }
    }

    private void ApplyColor()
    {
        HeaderAccent.Background = new SolidColorBrush(Color.Parse(_note.Color));
    }

    private void NoteText_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loading) return;

        SaveStatus.Text = "Cambios pendientes…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private bool Persist()
    {
        _note.Text = NoteText.Text ?? "";
        if (NoteStore.IsEmpty(_note))
        {
            SaveStatus.Text = "Sin cambios";
            return true;
        }

        bool saved = NoteStore.Save(_note);
        SaveStatus.Text = saved
            ? "Guardado " + DateTime.Now.ToString("HH:mm")
            : "No se pudo guardar";
        SaveStatus.Foreground = Brush(saved ? "#91A0B4" : "#EF4444");
        if (saved) DateText.Text = "Editado " + _note.Modified.ToString("g");
        return saved;
    }

    private void RebuildTags()
    {
        TagsPanel.Children.Clear();
        foreach (string tag in _note.Tags.ToList())
        {
            var remove = new Button
            {
                Content = "×",
                Padding = new Thickness(5, 0),
                Background = Brushes.Transparent
            };
            remove.Click += (_, _) =>
            {
                _note.Tags.Remove(tag);
                RebuildTags();
                Persist();
            };

            var content = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "#" + tag, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    remove
                }
            };
            TagsPanel.Children.Add(new Border
            {
                Background = Brush("#1D2A3B"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 3),
                Margin = new Thickness(0, 0, 6, 4),
                Child = content
            });
        }
    }

    private void AddTag_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => AddTag();

    private void TagInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddTag();
    }

    private async void AddTag()
    {
        string tag = (TagInput.Text ?? "").Trim().TrimStart('#').Trim();
        if (tag.Length == 0) return;
        if (tag.Length > 32)
        {
            SaveStatus.Text = "Las etiquetas permiten hasta 32 caracteres.";
            return;
        }
        if (_note.Tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
        {
            TagInput.Clear();
            return;
        }

        _note.Tags.Add(tag);
        TagInput.Clear();
        RebuildTags();
        if (!Persist())
            await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo guardar la etiqueta.");
    }

    private void RebuildImages()
    {
        DisposeImages();
        ImagesPanel.Children.Clear();
        foreach (string file in _note.Images.ToList())
        {
            string path = NoteStore.ImagePath(_note, file);
            byte[]? data = NoteStore.LoadImage(path);
            if (data == null) continue;

            Bitmap bitmap;
            try
            {
                bitmap = ImageCodec.Decode(data, 360);
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo mostrar una imagen de la nota: " + ex);
                continue;
            }

            _displayedImages.Add(bitmap);
            var remove = new Button
            {
                Content = "Quitar",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Margin = new Thickness(4)
            };
            remove.Click += async (_, _) =>
            {
                if (!await DialogService.ConfirmAsync(this, "Quitar imagen",
                        "¿Eliminar esta imagen de la nota?")) return;
                if (!NoteStore.RemoveImage(_note, file))
                {
                    await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo quitar la imagen.");
                    return;
                }
                Persist();
                RebuildImages();
            };

            var cell = new Grid { Width = 180, Height = 130, Margin = new Thickness(0, 0, 8, 8) };
            cell.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform });
            cell.Children.Add(remove);
            ImagesPanel.Children.Add(cell);
        }
    }

    private void DisposeImages()
    {
        foreach (Bitmap bitmap in _displayedImages) bitmap.Dispose();
        _displayedImages.Clear();
    }

    private async void PasteImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            Bitmap? bitmap = clipboard == null ? null : await clipboard.TryGetBitmapAsync();
            if (bitmap == null)
            {
                await DialogService.ShowInfoAsync(this, "PinShot", "No hay una imagen en el portapapeles.");
                return;
            }

            using (bitmap)
                NoteStore.AddImage(_note, ImageCodec.EncodePng(bitmap));
            Persist();
            RebuildImages();
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo pegar una imagen en la nota: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo pegar", ex.Message);
        }
    }

    private async void AddImage_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Añadir imagen",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Imágenes")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                    }
                }
            });

        int failed = 0;
        foreach (IStorageFile file in files)
        {
            try
            {
                await using Stream input = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer);
                using Bitmap bitmap = ImageCodec.Decode(buffer.ToArray());
                NoteStore.AddImage(_note, ImageCodec.EncodePng(bitmap));
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo añadir imagen a la nota: " + ex);
                failed++;
            }
        }

        Persist();
        RebuildImages();
        if (failed > 0)
            await DialogService.ShowErrorAsync(this, "PinShot",
                $"No se pudieron añadir {failed} imagen(es).");
    }

    private async void Pin_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _note.IsPinned = !_note.IsPinned;
        if (!Persist())
        {
            _note.IsPinned = !_note.IsPinned;
            await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo guardar el cambio.");
            return;
        }
        PinButton.Content = _note.IsPinned ? "Quitar fijado" : "Fijar";
    }

    private async void Delete_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await DialogService.ConfirmAsync(this, "Eliminar nota",
                "La nota y sus imágenes se eliminarán permanentemente.")) return;

        _deleted = true;
        _saveTimer.Stop();
        if (!NoteStore.Delete(_note))
        {
            _deleted = false;
            await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo eliminar la nota.");
            return;
        }
        Close();
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_deleted) return;

        _saveTimer.Stop();
        _note.Text = NoteText.Text ?? "";
        if (NoteStore.IsEmpty(_note))
        {
            if (File.Exists(Path.Combine(NoteStore.NoteDir(_note.Id), "nota.json")))
            {
                e.Cancel = true;
                if (!await DialogService.ConfirmAsync(this, "Nota vacía",
                        "Esta nota no tiene contenido. ¿Eliminarla?")) return;
                _deleted = true;
                if (!NoteStore.Delete(_note))
                {
                    _deleted = false;
                    await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo eliminar la nota vacía.");
                    return;
                }
                Close();
            }
            else
            {
                _deleted = true;
            }
        }
        else if (!Persist())
        {
            e.Cancel = true;
            await DialogService.ShowErrorAsync(this, "PinShot",
                "No se pudieron guardar los cambios; la nota seguirá abierta.");
        }
    }

    private async void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.S && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            _saveTimer.Stop();
            if (!Persist())
                await DialogService.ShowErrorAsync(this, "PinShot", "No se pudieron guardar los cambios.");
            e.Handled = true;
        }
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
