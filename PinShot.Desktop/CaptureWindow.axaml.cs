using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PinShot;

namespace PinShot.Desktop;

public partial class CaptureWindow : Window
{
    private readonly byte[] _pngData;
    private readonly Bitmap _image;
    private double _zoom = 1;
    private bool _savedToNotes;

    public CaptureWindow(byte[] pngData)
    {
        InitializeComponent();
        _pngData = pngData;
        _image = ImageCodec.Decode(pngData);
        CaptureImage.Source = _image;
        UpdateZoom();
        Closed += (_, _) => _image.Dispose();
    }

    public CaptureWindow()
        : this(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC"))
    {
    }

    private void ZoomIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _zoom = Math.Min(8, _zoom * 1.25);
        UpdateZoom();
    }

    private void ZoomOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _zoom = Math.Max(0.25, _zoom / 1.25);
        UpdateZoom();
    }

    private void UpdateZoom()
    {
        CaptureImage.Width = _image.PixelSize.Width * _zoom;
        CaptureImage.Height = _image.PixelSize.Height * _zoom;

        ZoomText.Text = $"{Math.Round(_zoom * 100)}%";
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
            await DialogService.ShowInfoAsync(this, "PinShot", "La captura se guardó en tus notas.");
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
            ShowStatus("Imagen copiada al portapapeles.");
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
            ShowStatus(format == "exr"
                ? "EXR exportado como RGB lineal Half; no añade información HDR a capturas SDR."
                : "Captura exportada como " + format.ToUpperInvariant() + ".");
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo exportar la captura: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo exportar", ex.Message);
        }
    }

    private void ShowStatus(string message)
    {
        Title = "Captura - PinShot — " + message;
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
