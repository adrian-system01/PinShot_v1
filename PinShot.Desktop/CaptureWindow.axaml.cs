using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PinShot;

namespace PinShot.Desktop;

public partial class CaptureWindow : Window
{
    private const double MinZoom = 1;
    private const double MaxZoom = 8;
    private const double ZoomFactor = 1.15;
    private static readonly object CascadeLock = new();
    private static readonly HashSet<int> OpenCascadeSlots = new();
    private static int _nextCascadeSlot;

    private readonly byte[] _pngData;
    private readonly Bitmap _image;
    private readonly ScaleTransform _imageScale = new();
    private readonly TranslateTransform _imageTranslate = new();
    private readonly int _cascadeSlot;
    private readonly DispatcherTimer _toastTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(1400)
    };
    private double _zoom = MinZoom;
    private bool _savedToNotes;
    private bool _isPinned = true;
    private bool _isPanning;
    private Point _lastPanPoint;
    private IPointer? _panPointer;

    public CaptureWindow(byte[] pngData)
    {
        InitializeComponent();
        _pngData = pngData;
        _image = ImageCodec.Decode(pngData);
        CaptureImage.Source = _image;
        CaptureImage.RenderTransform = new TransformGroup
        {
            Children = { _imageScale, _imageTranslate }
        };
        _cascadeSlot = ReserveCascadeSlot();

        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastPanel.IsVisible = false;
        };

        SizeAndPlaceWindow();
        UpdatePinVisuals();
        UpdateCursor();

        Closed += (_, _) =>
        {
            _toastTimer.Stop();
            _image.Dispose();
            ReleaseCascadeSlot(_cascadeSlot);
        };
    }

    public CaptureWindow()
        : this(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"))
    {
    }

    private static int ReserveCascadeSlot()
    {
        lock (CascadeLock)
        {
            int slot = _nextCascadeSlot;
            while (!OpenCascadeSlots.Add(slot))
                slot++;

            _nextCascadeSlot = (slot + 1) % 8;
            return slot;
        }
    }

    private static void ReleaseCascadeSlot(int slot)
    {
        lock (CascadeLock)
            OpenCascadeSlots.Remove(slot);
    }

    private void SizeAndPlaceWindow()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen == null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        double scaling = screen.Scaling > 0 ? screen.Scaling : 1;
        var workArea = screen.WorkingArea;
        const double chrome = 20;
        double imageWidth = _image.PixelSize.Width / scaling;
        double imageHeight = _image.PixelSize.Height / scaling;
        double maxWidth = Math.Max(100, workArea.Width / scaling * 0.9 - chrome);
        double maxHeight = Math.Max(100, workArea.Height / scaling * 0.9 - chrome);
        double fit = Math.Min(1, Math.Min(maxWidth / imageWidth, maxHeight / imageHeight));

        Width = Math.Max(MinWidth, imageWidth * fit + chrome);
        Height = Math.Max(MinHeight, imageHeight * fit + chrome);

        double widthPixels = Width * scaling;
        double heightPixels = Height * scaling;
        double left = workArea.X + (workArea.Width - widthPixels) / 2 + (_cascadeSlot % 8) * 28 * scaling;
        double top = workArea.Y + (workArea.Height - heightPixels) / 2 + (_cascadeSlot % 8) * 28 * scaling;
        double maxLeft = Math.Max(workArea.X, workArea.X + workArea.Width - widthPixels);
        double maxTop = Math.Max(workArea.Y, workArea.Y + workArea.Height - heightPixels);

        Position = new PixelPoint(
            (int)Math.Round(Math.Clamp(left, workArea.X, maxLeft)),
            (int)Math.Round(Math.Clamp(top, workArea.Y, maxTop)));
    }

    private void Window_PointerEntered(object? sender, PointerEventArgs e)
    {
        Toolbar.Opacity = 1;
        Toolbar.IsHitTestVisible = true;
    }

    private void Window_PointerExited(object? sender, PointerEventArgs e)
    {
        if (_isPanning) return;
        Toolbar.Opacity = 0;
        Toolbar.IsHitTestVisible = false;
    }

    private void ImageHost_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ImageHost).Properties.IsLeftButtonPressed ||
            NotesPanel.IsPointerOver)
            return;

        Focus();
        bool moveWindow = _zoom <= MinZoom ||
            (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (moveWindow)
        {
            BeginMoveDrag(e);
            e.Handled = true;
            return;
        }

        _isPanning = true;
        _lastPanPoint = e.GetPosition(ImageHost);
        _panPointer = e.Pointer;
        e.Pointer.Capture(ImageHost);
        UpdateCursor();
        e.Handled = true;
    }

    private void ImageHost_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        if (!e.GetCurrentPoint(ImageHost).Properties.IsLeftButtonPressed)
        {
            EndPan(e.Pointer);
            return;
        }

        Point current = e.GetPosition(ImageHost);
        _imageTranslate.X += current.X - _lastPanPoint.X;
        _imageTranslate.Y += current.Y - _lastPanPoint.Y;
        _lastPanPoint = current;
        ClampPanOffsets();
        e.Handled = true;
    }

    private void ImageHost_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
            EndPan(e.Pointer);
    }

    private void ImageHost_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
        _panPointer = null;
        UpdateCursor();
    }

    private void EndPan(IPointer pointer)
    {
        _isPanning = false;
        pointer.Capture(null);
        UpdateCursor();
    }

    private void ImageHost_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (NotesPanel.IsPointerOver) return;
        ResetZoom();
    }

    private void Window_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (NotesPanel.IsPointerOver || Toolbar.IsPointerOver) return;

        if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            Opacity = Math.Clamp(Opacity + (e.Delta.Y > 0 ? 0.05 : -0.05), 0.2, 1);
            ShowToast($"Opacidad {Math.Round(Opacity * 100)}%");
            e.Handled = true;
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        e.Handled = true;

        double newZoom = Math.Clamp(_zoom * Math.Pow(ZoomFactor, e.Delta.Y), MinZoom, MaxZoom);
        if (Math.Abs(newZoom - _zoom) < 0.0001) return;
        if (newZoom <= MinZoom)
        {
            ResetZoom();
            return;
        }

        Point pointer = e.GetPosition(ImageHost);
        double centerX = pointer.X - ImageHost.Bounds.Width / 2;
        double centerY = pointer.Y - ImageHost.Bounds.Height / 2;
        _imageTranslate.X = centerX - newZoom * ((centerX - _imageTranslate.X) / _zoom);
        _imageTranslate.Y = centerY - newZoom * ((centerY - _imageTranslate.Y) / _zoom);
        _zoom = newZoom;
        _imageScale.ScaleX = _zoom;
        _imageScale.ScaleY = _zoom;
        ClampPanOffsets();
        UpdateZoomBadge();
        UpdateCursor();
    }

    private void ClampPanOffsets()
    {
        double maxX = Math.Max(0, ImageHost.Bounds.Width * (_zoom - 1) / 2);
        double maxY = Math.Max(0, ImageHost.Bounds.Height * (_zoom - 1) / 2);
        _imageTranslate.X = Math.Clamp(_imageTranslate.X, -maxX, maxX);
        _imageTranslate.Y = Math.Clamp(_imageTranslate.Y, -maxY, maxY);
    }

    private void ResetZoom()
    {
        _zoom = MinZoom;
        _imageScale.ScaleX = MinZoom;
        _imageScale.ScaleY = MinZoom;
        _imageTranslate.X = 0;
        _imageTranslate.Y = 0;
        if (_isPanning)
        {
            _isPanning = false;
            _panPointer?.Capture(null);
            _panPointer = null;
        }
        UpdateZoomBadge();
        UpdateCursor();
    }

    private void UpdateZoomBadge()
    {
        ZoomBadge.IsVisible = _zoom > MinZoom + 0.001;
        if (ZoomBadge.IsVisible)
            ZoomText.Text = $"🔍 {Math.Round(_zoom * 100)}%";
    }

    private void UpdateCursor()
    {
        ImageHost.Cursor = new Cursor(_isPanning
            ? StandardCursorType.SizeAll
            : _zoom > MinZoom ? StandardCursorType.Hand : StandardCursorType.Arrow);
    }

    private void PinButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isPinned = !_isPinned;
        Topmost = _isPinned;
        UpdatePinVisuals();
        ShowToast(_isPinned ? "Fijada encima" : "Ya no está fijada");
    }

    private void UpdatePinVisuals()
    {
        PinButton.Content = _isPinned ? "Fijado" : "Fijar";
        ToolTip.SetTip(PinButton, _isPinned
            ? "Fijado encima de todo (clic para desfijar)"
            : "Desfijado (clic para fijar encima)");
    }

    private void NotesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleNotes();
    }

    private void ToggleNotes()
    {
        NotesPanel.IsVisible = !NotesPanel.IsVisible;
        if (NotesPanel.IsVisible)
            NotesBox.Focus();
        else
            Focus();
    }

    private void CloseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastPanel.IsVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private async void SaveNote_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_savedToNotes) return;

        try
        {
            NoteStore.SaveCapture(_pngData, NotesBox.Text ?? "");
            _savedToNotes = true;
            SaveNoteButton.IsEnabled = false;
            SaveNoteButton.Content = "Guardado";
            ShowToast("La captura se guardó en tus notas.");
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo guardar la captura: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo guardar", ex.Message);
        }
    }

    private async void Copy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                throw new InvalidOperationException("El portapapeles no está disponible en esta plataforma.");

            App.SuppressClipboardFor(TimeSpan.FromSeconds(2));
            await clipboard.SetBitmapAsync(_image);
            ShowToast("Imagen copiada al portapapeles.");
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo copiar la captura: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo copiar", ex.Message);
        }
    }

    private async void Menu_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Guardar captura",
            SuggestedFileName = "PinShot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png",
            DefaultExtension = "png",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Imagen PNG") { Patterns = new[] { "*.png" } },
                new FilePickerFileType("Imagen TIFF (sin pérdida)") { Patterns = new[] { "*.tif", "*.tiff" } },
                new FilePickerFileType("OpenEXR Half lineal") { Patterns = new[] { "*.exr" } }
            }
        });
        if (file == null) return;

        try
        {
            string extension = Path.GetExtension(file.Name).ToLowerInvariant();
            string format = extension switch
            {
                "" or ".png" => "png",
                ".tif" or ".tiff" => "tiff",
                ".exr" => "exr",
                _ => throw new InvalidOperationException(
                    "Usa una extensión .png, .tif, .tiff o .exr para exportar la captura.")
            };
            byte[] data = _pngData;
            if (format != "png")
            {
                byte[] rgbaPixels = ImageCodec.CopyRgbaPixels(_image);
                int width = _image.PixelSize.Width;
                int height = _image.PixelSize.Height;
                data = await Task.Run(async () => format == "tiff"
                    ? await ImageExportService.EncodeTiffAsync(rgbaPixels, width, height)
                    : ImageExportService.EncodeExr(rgbaPixels, width, height));
            }

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(data);
            ShowToast(format == "exr"
                ? "EXR exportado como RGB lineal Half; no añade información HDR a capturas SDR."
                : "Captura exportada como " + format.ToUpperInvariant() + ".");
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo exportar la captura: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo exportar", ex.Message);
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (NotesBox.IsFocused)
        {
            if (e.Key == Key.Escape)
            {
                NotesPanel.IsVisible = false;
                Focus();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Escape)
            Close();
        else if (e.Key == Key.N)
            ToggleNotes();
    }
}
