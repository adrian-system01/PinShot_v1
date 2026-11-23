using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace PinShot
{
    public partial class SettingsWindow : Window
    {
        private bool _loadingColorBlindMode = true;

        public SettingsWindow()
        {
            InitializeComponent();

            PathText.Text = NoteStore.NotesDir;
            ColorBlindModeCheck.IsChecked = NoteStore.Settings.ColorBlindMode;
            _loadingColorBlindMode = false;

            BuildDefaultPalette();
        }

        private void BuildDefaultPalette()
        {
            UiHelpers.BuildPalette(PalettePanel, NoteStore.Settings.DefaultColor, hex =>
            {
                string previous = NoteStore.Settings.DefaultColor;
                NoteStore.Settings.DefaultColor = hex;
                if (!NoteStore.SaveSettings())
                {
                    NoteStore.Settings.DefaultColor = previous;
                    MessageBox.Show(this, "No se pudo guardar la configuración.", "PinShot",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return previous;
                }

                NoteStore.NotifyChanged();
                return hex;
            });
        }

        private void ColorBlindMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingColorBlindMode || ColorBlindModeCheck == null) return;

            bool previous = NoteStore.Settings.ColorBlindMode;
            bool enabled = ColorBlindModeCheck.IsChecked == true;
            NoteStore.Settings.ColorBlindMode = enabled;
            if (!NoteStore.SaveSettings())
            {
                NoteStore.Settings.ColorBlindMode = previous;
                _loadingColorBlindMode = true;
                ColorBlindModeCheck.IsChecked = previous;
                _loadingColorBlindMode = false;
                MessageBox.Show(this, "No se pudo guardar la configuración.", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BuildDefaultPalette();
            NoteStore.NotifyChanged();
        }

        private void ApplyAll_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(this, "¿Cambiar el color de TODAS las notas guardadas?", "PinShot",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            int failed = 0;
            foreach (var n in NoteStore.LoadAll())
            {
                n.Color = NoteStore.Settings.DefaultColor;
                if (!NoteStore.Save(n, notify: false, touch: false)) failed++;
            }
            NoteStore.NotifyChanged();
            if (failed > 0)
                MessageBox.Show(this, "No se pudo actualizar el color de " + failed + " nota(s).", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(NoteStore.NotesDir);
                Process.Start(new ProcessStartInfo(NoteStore.NotesDir) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppLog.Write("No se pudo abrir la carpeta: " + ex.Message);
                MessageBox.Show(this, "No se pudo abrir la carpeta de notas.", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar respaldo de PinShot",
                Filter = "Respaldo PinShot (*.zip)|*.zip",
                FileName = "PinShot-respaldo-" + DateTime.Now.ToString("yyyyMMdd") + ".zip",
                AddExtension = true
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                NoteStore.ExportNotes(dialog.FileName);
                MessageBox.Show(this, "Respaldo exportado correctamente.", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo exportar el respaldo:\n" + ex.Message, "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar respaldo de PinShot",
                Filter = "Respaldo PinShot (*.zip)|*.zip",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != true) return;

            var answer = MessageBox.Show(this,
                "Las notas del respaldo se agregarán a las actuales; no se reemplazará ninguna.",
                "Importar respaldo", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;

            try
            {
                int imported = NoteStore.ImportNotes(dialog.FileName);
                MessageBox.Show(this, imported == 0 ? "El respaldo no contiene notas." :
                    "Se importaron " + imported + " nota(s).", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo importar el respaldo:\n" + ex.Message, "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            try { DragMove(); } catch { }
        }
    }
}
