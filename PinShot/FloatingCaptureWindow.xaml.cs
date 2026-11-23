using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PinShot
{
    public partial class FloatingCaptureWindow : Window
    {
        private static int _openCount = 0;

        private bool _isPinned = true;

        // Zoom
        private double _zoom = 1.0;
        private const double ZoomMin = 1.0;
        private const double ZoomMax = 8.0;
        private const double ZoomFactor = 1.15;

        // Mover imagen con zoom
        private bool _isPanning = false;
        private System.Windows.Point _lastPanPoint;

        // Aviso temporal
        private readonly DispatcherTimer _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1400)
        };

        public FloatingCaptureWindow(BitmapSource image)
        {
            InitializeComponent();
            CaptureImage.Source = image;

            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                ToastPanel.Visibility = Visibility.Collapsed;
            };

            SizeAndPlaceWindow(image);
            UpdatePinVisuals();
            UpdateNotesVisuals();

            _openCount++;
            Closed += (s, e) =>
            {
                _openCount = Math.Max(0, _openCount - 1);
                _toastTimer.Stop();
            };
        }

        // ───────────── Tamaño y posición ─────────────
        private void SizeAndPlaceWindow(BitmapSource image)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            // Tamaño real de la captura en pantalla
            double w = image.PixelWidth / scaleX;
            double h = image.PixelHeight / scaleY;

            var wa = SystemParameters.WorkArea;
            const double chrome = 20; // margen 8*2 + borde 2*2

            double maxW = Math.Max(100, wa.Width * 0.9 - chrome);
            double maxH = Math.Max(100, wa.Height * 0.9 - chrome);
            double fit = Math.Min(1.0, Math.Min(maxW / w, maxH / h));

            Width = Math.Max(MinWidth, w * fit + chrome);
            Height = Math.Max(MinHeight, h * fit + chrome);

            // Cascada para que varias capturas no queden una sobre otra
            int step = _openCount % 8;
            double left = wa.Left + (wa.Width - Width) / 2.0 + step * 28;
            double top = wa.Top + (wa.Height - Height) / 2.0 + step * 28;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = Math.Max(wa.Left, Math.Min(left, wa.Right - Width));
            Top = Math.Max(wa.Top, Math.Min(top, wa.Bottom - Height));
        }

        // ───────────── Pin ─────────────
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            this.Topmost = _isPinned;
            UpdatePinVisuals();
            ShowToast(_isPinned ? "Fijada encima" : "Ya no esta fijada");
        }

        private void UpdatePinVisuals()
        {
            string hex = _isPinned ? "#E62563EB" : "#CC374151";
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            PinButton.Background = new SolidColorBrush(color);
            PinButton.ToolTip = _isPinned
                ? "Fijado encima de todo (Clic para desfijar)"
                : "Desfijado (Clic para fijar encima)";
        }

        // ───────────── Notas ─────────────
        private void NotesButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleNotes();
        }

        private void ToggleNotes()
        {
            if (NotesPanel.Visibility == Visibility.Visible)
            {
                NotesPanel.Visibility = Visibility.Collapsed;
                this.Focus();
            }
            else
            {
                NotesPanel.Visibility = Visibility.Visible;
                NotesBox.Focus();
                NotesBox.CaretIndex = NotesBox.Text.Length;
            }
            UpdateNotesVisuals();
        }

        private void NotesBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateNotesVisuals();
        }

        private void UpdateNotesVisuals()
        {
            if (NotesButton == null || NotesBox == null || NotesPanel == null) return;

            bool hasNotes = !string.IsNullOrWhiteSpace(NotesBox.Text);
            bool open = NotesPanel.Visibility == Visibility.Visible;
            string hex = (open || hasNotes) ? "#E62563EB" : "#CC374151";
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            NotesButton.Background = new SolidColorBrush(color);
            NotesButton.ToolTip = hasNotes ? "Notas (hay texto) (N)" : "Notas (N)";
        }

        // ───────────── Copiar / Guardar ─────────────
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            CopyImage();
        }

        private void SaveNoteButton_Click(object sender, RoutedEventArgs e)
        {
            SaveToNotes();
        }

        private void LibraryButton_Click(object sender, RoutedEventArgs e)
        {
            App.OpenLibrary();
        }

        private void MenuSaveNote_Click(object sender, RoutedEventArgs e) { SaveToNotes(); }
        private void MenuSaveFile_Click(object sender, RoutedEventArgs e) { SaveImageAsFile(); }
        private void MenuCopy_Click(object sender, RoutedEventArgs e) { CopyImage(); }
        private void MenuLibrary_Click(object sender, RoutedEventArgs e) { App.OpenLibrary(); }
        private void MenuClose_Click(object sender, RoutedEventArgs e) { Close(); }

        /// <summary>Guarda la captura (y lo escrito en Notas) en la lista de notas.</summary>
        private void SaveToNotes()
        {
            try
            {
                if (CaptureImage.Source is not BitmapSource bmp) return;
                NoteStore.SaveCapture(WpfImageCodec.ToPng(bmp), NotesBox.Text);
                ShowToast("Guardado en notas ✔");
            }
            catch (Exception ex)
            {
                AppLog.Write("Guardar en notas fallo: " + ex.Message);
                ShowToast("No se pudo guardar");
            }
        }

        private void CopyImage()
        {
            try
            {
                if (CaptureImage.Source is not BitmapSource bmp) return;
                App.SuppressClipboard();
                System.Windows.Clipboard.SetImage(bmp);
                ShowToast("Copiado ✔");
            }
            catch (Exception ex)
            {
                AppLog.Write("Copiar fallo: " + ex.Message);
                ShowToast("No se pudo copiar");
            }
        }

        private void SaveImageAsFile()
        {
            try
            {
                if (CaptureImage.Source is not BitmapSource bmp) return;

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Guardar captura",
                    Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg",
                    DefaultExt = ".png",
                    FileName = "PinShot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                };

                if (dlg.ShowDialog(this) != true) return;

                string ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                BitmapEncoder encoder;
                if (ext == ".jpg" || ext == ".jpeg")
                    encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                else
                    encoder = new PngBitmapEncoder();

                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (var fs = System.IO.File.Create(dlg.FileName))
                {
                    encoder.Save(fs);
                }
                ShowToast("Guardado ✔");
            }
            catch (Exception ex)
            {
                AppLog.Write("Guardar fallo: " + ex.Message);
                ShowToast("No se pudo guardar");
            }
        }

        private void ShowToast(string text)
        {
            ToastText.Text = text;
            ToastPanel.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        // ───────────── Barra de botones al pasar el mouse ─────────────
        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Toolbar.Opacity = 1;
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanning) Toolbar.Opacity = 0;
        }

        // ───────────── Mover ventana / mover imagen ─────────────
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (NotesPanel.IsMouseOver) return; // el cuadro de notas funciona normal

            this.Focus();

            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (_zoom > 1.0 && !shift)
            {
                // Con zoom: arrastrar mueve la imagen
                _isPanning = true;
                _lastPanPoint = e.GetPosition(this);
                CaptureImage.CaptureMouse();
                this.Cursor = System.Windows.Input.Cursors.ScrollAll;
            }
            else
            {
                // Sin zoom (o con Shift): arrastrar mueve la ventana
                try { this.DragMove(); } catch { }
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanning) return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                EndPan();
                return;
            }

            System.Windows.Point current = e.GetPosition(this);
            ImageTranslate.X += current.X - _lastPanPoint.X;
            ImageTranslate.Y += current.Y - _lastPanPoint.Y;
            _lastPanPoint = current;

            ClampPanOffsets();
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning) EndPan();
        }

        private void EndPan()
        {
            _isPanning = false;
            CaptureImage.ReleaseMouseCapture();
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            this.Cursor = _zoom > 1.0
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.Arrow;
        }

        // ───────────── Rueda: Ctrl = zoom, Alt = opacidad ─────────────
        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (NotesPanel.IsMouseOver) return;

            ModifierKeys mods = Keyboard.Modifiers;

            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                double op = Math.Clamp(this.Opacity + (e.Delta > 0 ? 0.05 : -0.05), 0.2, 1.0);
                this.Opacity = op;
                ShowToast("Opacidad " + (int)Math.Round(op * 100) + "%");
                e.Handled = true;
                return;
            }

            if ((mods & ModifierKeys.Control) != ModifierKeys.Control) return;

            e.Handled = true;

            double newZoom = Math.Clamp(_zoom * Math.Pow(ZoomFactor, e.Delta / 120.0), ZoomMin, ZoomMax);
            if (Math.Abs(newZoom - _zoom) < 0.0001) return;

            if (newZoom <= ZoomMin + 0.001)
            {
                ResetZoom();
                return;
            }

            // Mantener fijo el punto que esta bajo el cursor
            System.Windows.Point c = e.GetPosition(ImageHost);
            double cx = c.X - ImageHost.ActualWidth / 2.0;
            double cy = c.Y - ImageHost.ActualHeight / 2.0;

            ImageTranslate.X = cx - newZoom * ((cx - ImageTranslate.X) / _zoom);
            ImageTranslate.Y = cy - newZoom * ((cy - ImageTranslate.Y) / _zoom);

            _zoom = newZoom;
            ImageScale.ScaleX = _zoom;
            ImageScale.ScaleY = _zoom;

            UpdateBitmapScalingMode();
            ClampPanOffsets();
            UpdateZoomBadge();
            UpdateCursor();
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (NotesPanel.IsMouseOver) return;
            ResetZoom();
        }

        private void ResetZoom()
        {
            _zoom = 1.0;
            ImageScale.ScaleX = 1.0;
            ImageScale.ScaleY = 1.0;
            ImageTranslate.X = 0;
            ImageTranslate.Y = 0;
            UpdateBitmapScalingMode();
            if (_isPanning) EndPan();
            UpdateZoomBadge();
            UpdateCursor();
        }

        private void UpdateBitmapScalingMode()
        {
            RenderOptions.SetBitmapScalingMode(
                CaptureImage,
                _zoom > 1.001 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
        }

        private void UpdateZoomBadge()
        {
            if (_zoom > 1.001)
            {
                ZoomText.Text = (int)Math.Round(_zoom * 100) + "%";
                ZoomBadge.Visibility = Visibility.Visible;
            }
            else
            {
                ZoomBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void ClampPanOffsets()
        {
            double maxX = Math.Max(0, ImageHost.ActualWidth * (_zoom - 1.0) / 2.0);
            double maxY = Math.Max(0, ImageHost.ActualHeight * (_zoom - 1.0) / 2.0);

            ImageTranslate.X = Math.Clamp(ImageTranslate.X, -maxX, maxX);
            ImageTranslate.Y = Math.Clamp(ImageTranslate.Y, -maxY, maxY);
        }

        // ───────────── Teclado ─────────────
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (NotesBox.IsKeyboardFocusWithin)
            {
                if (e.Key == Key.Escape)
                {
                    this.Focus();
                    e.Handled = true;
                }
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.C)
            {
                CopyImage();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.S)
            {
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                if (shift) SaveImageAsFile();
                else SaveToNotes();
                e.Handled = true;
            }
            else if (ctrl && (e.Key == Key.D0 || e.Key == Key.NumPad0))
            {
                ResetZoom();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.L)
            {
                App.OpenLibrary();
                e.Handled = true;
            }
            else if (e.Key == Key.N)
            {
                ToggleNotes();
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
