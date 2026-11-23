using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PinShot;
using System.Security.Cryptography;

namespace PinShot.Desktop;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _clipboardTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(850)
    };
    private readonly List<Bitmap> _previewBitmaps = new();
    private string _filter = "";
    private bool _pinnedOnly;
    private bool _pollingClipboard;
    private string? _lastClipboardError;
    private string _lastImageHash = "";
    private CaptureWindow? _captureWindow;

    public MainWindow()
    {
        InitializeComponent();
        NoteStore.Changed += OnStoreChanged;
        _clipboardTimer.Tick += PollClipboard;
        Opened += (_, _) => _clipboardTimer.Start();
        Closed += (_, _) =>
        {
            _clipboardTimer.Stop();
            NoteStore.Changed -= OnStoreChanged;
            DisposePreviews();
            _captureWindow?.Close();
        };
        RefreshCards();
    }

    private void OnStoreChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshCards);
            return;
        }

        RefreshCards();
    }

    private void RefreshCards()
    {
        DisposePreviews();
        CardsPanel.Children.Clear();
        CardsPanel.Children.Add(BuildQuickCard());

        string filter = _filter.Trim();
        int shown = 0;
        foreach (NoteItem note in NoteStore.LoadAll())
        {
            bool matches = filter.Length == 0 ||
                note.Text.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                note.Tags.Any(tag => tag.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
            if (!matches || (_pinnedOnly && !note.IsPinned)) continue;

            CardsPanel.Children.Add(BuildCard(note));
            shown++;
        }

        if (shown == 0)
        {
            string message = _pinnedOnly ? "No hay notas fijadas que coincidan."
                : filter.Length > 0 ? "No hay notas con ese texto o etiqueta."
                : "Aún no hay notas guardadas.\nGuarda una captura o crea una nota nueva.";
            CardsPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = Brush("#91A0B4"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(4, 10)
            });
        }
    }

    private Control BuildQuickCard()
    {
        var content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "＋", Foreground = Brush("#60A5FA"), FontSize = 17 },
                new TextBlock { Text = "Crear una nota nueva", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }
            }
        };
        var button = new Button
        {
            Content = content,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Background = Brush("#111B2B"),
            Foreground = Brush("#D7E2F0"),
            BorderBrush = Brush("#2A3A50"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12)
        };
        button.Click += (_, _) => NoteEditorWindow.Open(NoteStore.CreateNew(), this);
        return button;
    }

    private Control BuildCard(NoteItem note)
    {
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(10, 8, 10, 10) };
        var metadata = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 6
        };
        if (note.IsPinned)
            metadata.Children.Add(new TextBlock { Text = "◆", Foreground = Brush(DisplayColor(note.Color)) });
        metadata.Children.Add(new TextBlock
        {
            Text = note.Modified.ToString("g"),
            Foreground = Brush("#8392A8"),
            FontSize = 10
        });
        stack.Children.Add(metadata);

        if (note.Tags.Count > 0)
        {
            var tags = new WrapPanel();
            foreach (string tag in note.Tags)
            {
                tags.Children.Add(new Border
                {
                    Background = Brush("#1D2A3B"),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 2),
                    Margin = new Thickness(0, 0, 4, 4),
                    Child = new TextBlock { Text = "#" + tag, Foreground = Brush("#AFC2D9"), FontSize = 10 }
                });
            }
            stack.Children.Add(tags);
        }

        if (note.Images.Count > 0)
        {
            var thumbnails = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            foreach (string imageName in note.Images.Take(4))
            {
                byte[]? data = NoteStore.LoadImage(NoteStore.ImagePath(note, imageName));
                if (data == null) continue;

                try
                {
                    Bitmap bitmap = ImageCodec.Decode(data, 240);
                    _previewBitmaps.Add(bitmap);
                    var preview = new Button
                    {
                        Width = 92,
                        Height = 76,
                        Margin = new Thickness(0, 0, 5, 0),
                        Padding = new Thickness(0),
                        Content = new Image { Source = bitmap, Stretch = Stretch.UniformToFill }
                    };
                    ToolTip.SetTip(preview, "Abrir para ampliar y mover la imagen");
                    preview.Click += (_, _) => new CaptureWindow(data).Show(this);
                    thumbnails.Children.Add(preview);
                }
                catch (Exception ex)
                {
                    AppLog.Write("No se pudo generar una miniatura: " + ex.Message);
                }
            }
            stack.Children.Add(thumbnails);
        }

        if (!string.IsNullOrWhiteSpace(note.Text))
        {
            stack.Children.Add(new TextBlock
            {
                Text = note.Text.Trim(),
                Foreground = Brush("#DCE6F2"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxHeight = 72,
                Margin = new Thickness(2, 2)
            });
        }
        else if (note.Images.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = "(nota vacía)", Foreground = Brush("#718198") });
        }

        var accent = new Border
        {
            BorderBrush = Brush(DisplayColor(note.Color)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Child = stack
        };
        var card = new Button
        {
            Content = accent,
            Padding = new Thickness(0),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Background = Brush("#111B2B"),
            BorderBrush = Brush("#2A3A50"),
            BorderThickness = new Thickness(1)
        };
        card.Click += (_, _) => NoteEditorWindow.Open(note, this);
        card.ContextMenu = BuildMenu(note);
        return card;
    }

    private ContextMenu BuildMenu(NoteItem note)
    {
        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Abrir nota" };
        open.Click += (_, _) => NoteEditorWindow.Open(note, this);
        var pin = new MenuItem { Header = note.IsPinned ? "Quitar de fijadas" : "Fijar nota" };
        pin.Click += async (_, _) =>
        {
            note.IsPinned = !note.IsPinned;
            if (!NoteStore.Save(note))
            {
                note.IsPinned = !note.IsPinned;
                await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo actualizar la nota.");
            }
            else
            {
                NoteEditorWindow.UpdatePinnedState(note.Id, note.IsPinned);
            }
        };
        var delete = new MenuItem { Header = "Eliminar nota" };
        delete.Click += async (_, _) =>
        {
            if (!await DialogService.ConfirmAsync(this, "Eliminar nota",
                    "La nota y sus imágenes se eliminarán permanentemente.")) return;
            if (!NoteStore.Delete(note))
            {
                await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo eliminar la nota.");
                return;
            }
            NoteEditorWindow.CloseIfOpen(note.Id);
        };
        menu.Items.Add(open);
        menu.Items.Add(pin);
        menu.Items.Add(delete);
        return menu;
    }

    private async void PollClipboard(object? sender, EventArgs e)
    {
        if (_pollingClipboard || App.IsClipboardSuppressed) return;
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;

        _pollingClipboard = true;
        try
        {
            using Bitmap? bitmap = await clipboard.TryGetBitmapAsync();
            if (bitmap == null) return;

            byte[] png = ImageCodec.EncodePng(bitmap);
            string hash = Convert.ToHexString(SHA256.HashData(png));
            if (string.Equals(hash, _lastImageHash, StringComparison.Ordinal)) return;

            _lastImageHash = hash;
            _lastClipboardError = null;
            StatusText.Text = $"Captura detectada: {bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}.";
            _captureWindow?.Close();
            _captureWindow = new CaptureWindow(png);
            _captureWindow.Closed += (_, _) => _captureWindow = null;
            _captureWindow.Show(this);
            _captureWindow.Activate();
        }
        catch (Exception ex)
        {
            string error = ex.GetType().Name + ": " + ex.Message;
            if (!string.Equals(error, _lastClipboardError, StringComparison.Ordinal))
            {
                AppLog.Write("No se pudo leer una imagen del portapapeles: " + ex);
                StatusText.Text = "No se pudo leer una imagen del portapapeles.";
                _lastClipboardError = error;
            }
        }
        finally
        {
            _pollingClipboard = false;
        }
    }

    private void DisposePreviews()
    {
        foreach (Bitmap bitmap in _previewBitmaps) bitmap.Dispose();
        _previewBitmaps.Clear();
    }

    private string DisplayColor(string color)
    {
        if (!NoteStore.Settings.ColorBlindMode) return color;
        int index = Array.IndexOf(NoteStore.Palette, color);
        return index >= 0 ? NoteStore.AccessiblePalette[index] : color;
    }

    private void SearchBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        _filter = SearchBox.Text ?? "";
        RefreshCards();
    }

    private void PinnedOnly_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _pinnedOnly = PinnedOnlyCheck.IsChecked == true;
        RefreshCards();
    }

    private void Settings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        new SettingsWindow().Show(this);
    }

    private void NewNote_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => OpenNewNote();

    public void OpenNewNote()
    {
        NoteEditorWindow.Open(NoteStore.CreateNew(), this);
    }

    public void ShowAndActivate()
    {
        if (!IsVisible) Show();
        Activate();
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}