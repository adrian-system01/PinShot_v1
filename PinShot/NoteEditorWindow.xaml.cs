using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace PinShot
{
    /// <summary>Editor de una nota: texto, imagenes y color. Se guarda solo.</summary>
    public partial class NoteEditorWindow : Window
    {
        private static readonly Dictionary<string, NoteEditorWindow> OpenEditors =
            new Dictionary<string, NoteEditorWindow>();

        private readonly NoteItem _note;
        private bool _loading = true;
        private bool _deleted = false;

        private readonly DispatcherTimer _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };

        public static void Open(NoteItem note)
        {
            if (OpenEditors.TryGetValue(note.Id, out var existing))
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }

            var win = new NoteEditorWindow(note);
            OpenEditors[note.Id] = win;
            win.Show();
            win.Activate();
        }

        public static void CloseIfOpen(string id)
        {
            if (OpenEditors.TryGetValue(id, out var w))
            {
                w._deleted = true;
                w.Close();
            }
        }

        public static void UpdatePinnedState(string id, bool isPinned)
        {
            if (OpenEditors.TryGetValue(id, out var window))
                window._note.IsPinned = isPinned;
        }

        public NoteEditorWindow(NoteItem note)
        {
            InitializeComponent();
            _note = note;

            NoteText.Text = note.Text;
            _loading = false;

            DateText.Text = "Editado " + UiHelpers.FormatDate(note.Modified);
            UpdatePlaceholder();
            ApplyColor();
            RebuildImages();
            RebuildTags();
            if (File.Exists(Path.Combine(NoteStore.NoteDir(note.Id), "nota.json")))
            {
                SetSaveStatus(true);
            }
            else
            {
                SaveStatus.Text = "Nueva nota";
                SaveStatus.Foreground = UiHelpers.BrushOf("#8A8A8A");
            }

            UiHelpers.BuildPalette(PalettePanel, _note.Color, hex =>
            {
                _note.Color = hex;
                ApplyColor();
                Persist();
                return _note.Color;
            });

            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                Persist();
            };

            Loaded += (s, e) =>
            {
                NoteText.Focus();
                NoteText.CaretIndex = NoteText.Text.Length;
            };

            Closing += (s, e) =>
            {
                _saveTimer.Stop();
                if (_deleted) return;

                _note.Text = NoteText.Text;
                if (NoteStore.IsEmpty(_note))
                {
                    if (!NoteStore.Delete(_note))
                    {
                        e.Cancel = true;
                        SetSaveStatus(false);
                        MessageBox.Show(this, "No se pudo eliminar la nota vacía.", "PinShot",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                if (!Persist())
                {
                    e.Cancel = true;
                    MessageBox.Show(this, "No se pudieron guardar los cambios. La nota seguirá abierta para que no pierdas el texto.",
                        "PinShot", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            Closed += (s, e) => OpenEditors.Remove(_note.Id);
        }

        // ───────────── Guardado ─────────────
        private bool Persist()
        {
            _note.Text = NoteText.Text;
            if (NoteStore.IsEmpty(_note))
            {
                SaveStatus.Text = "Sin cambios";
                SaveStatus.Foreground = UiHelpers.BrushOf("#8A8A8A");
                return true;
            }

            bool saved = NoteStore.Save(_note);
            SetSaveStatus(saved);
            if (saved)
            {
                DateText.Text = "Editado " + UiHelpers.FormatDate(_note.Modified);
            }
            return saved;
        }

        private void SetSaveStatus(bool saved)
        {
            SaveStatus.Text = saved ? "Guardado " + DateTime.Now.ToString("HH:mm") : "No se pudo guardar";
            SaveStatus.Foreground = UiHelpers.BrushOf(saved ? "#8A8A8A" : "#EF4444");
        }

        private void ApplyColor()
        {
            var brush = UiHelpers.BrushOf(_note.Color);
            HeaderBar.Background = brush;
            FrameBorder.BorderBrush = brush;
        }

        private void UpdatePlaceholder()
        {
            Hint.Visibility = string.IsNullOrEmpty(NoteText.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NoteText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;

            UpdatePlaceholder();
            SaveStatus.Text = "Cambios pendientes…";
            SaveStatus.Foreground = UiHelpers.BrushOf("#F59E0B");
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void RebuildTags()
        {
            TagsPanel.Children.Clear();
            foreach (string tag in _note.Tags)
            {
                var chip = new Border
                {
                    Background = UiHelpers.BrushOf("#343434"),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 6, 4),
                    Padding = new Thickness(8, 3, 4, 3)
                };
                var content = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                content.Children.Add(new TextBlock
                {
                    Text = "#" + tag,
                    Foreground = UiHelpers.BrushOf("#D1D5DB"),
                    VerticalAlignment = VerticalAlignment.Center
                });
                var remove = new Button
                {
                    Content = "×",
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = UiHelpers.BrushOf("#9CA3AF"),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(5, 0, 2, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Quitar etiqueta"
                };
                remove.Click += (s, e) =>
                {
                    _note.Tags.Remove(tag);
                    RebuildTags();
                    Persist();
                };
                content.Children.Add(remove);
                chip.Child = content;
                TagsPanel.Children.Add(chip);
            }
        }

        private void AddTag_Click(object sender, RoutedEventArgs e)
        {
            AddTag();
        }

        private void TagInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddTag();
                e.Handled = true;
            }
        }

        private void AddTag()
        {
            string tag = TagInput.Text.Trim().TrimStart('#').Trim();
            if (tag.Length == 0) return;
            if (tag.Length > 32)
            {
                SaveStatus.Text = "Las etiquetas permiten hasta 32 caracteres.";
                SaveStatus.Foreground = UiHelpers.BrushOf("#F59E0B");
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
            Persist();
        }

        // ───────────── Imagenes ─────────────
        private void RebuildImages()
        {
            ImagesPanel.Children.Clear();
            ImagesScroll.Visibility = _note.Images.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (string file in _note.Images.ToList())
            {
                string path = NoteStore.ImagePath(_note, file);

                var host = new Grid
                {
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Clic para ver en grande"
                };

                host.Children.Add(new Image
                {
                    Height = 100,
                    Stretch = Stretch.Uniform,
                    Source = NoteStore.LoadImage(path, 360)
                });

                var remove = new Button
                {
                    Style = (Style)FindResource("FlatBtn"),
                    Content = "✕",
                    Width = 22,
                    Height = 22,
                    FontSize = 11,
                    Background = UiHelpers.BrushOf("#AA000000"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(2),
                    ToolTip = "Quitar imagen"
                };
                remove.Click += (s, e) =>
                {
                    if (!NoteStore.RemoveImage(_note, file))
                    {
                        MessageBox.Show(this, "No se pudo quitar la imagen.", "PinShot",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Persist();
                    RebuildImages();
                };
                host.Children.Add(remove);

                host.MouseLeftButtonUp += (s, e) => OpenFull(path);
                ImagesPanel.Children.Add(host);
            }
        }

        private void OpenFull(string path)
        {
            var bmp = NoteStore.LoadImage(path);
            if (bmp == null) return;

            var win = new FloatingCaptureWindow(bmp);
            win.Show();
            win.Activate();
        }

        private bool TryPasteImage()
        {
            if (!Clipboard.ContainsImage()) return false;

            var img = App.ReadClipboardImage();
            if (img == null) return false;

            NoteStore.AddImage(_note, img);
            Persist();
            RebuildImages();
            return true;
        }

        private void PasteImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!TryPasteImage())
                    MessageBox.Show(this, "No hay ninguna imagen en el portapapeles.", "PinShot",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLog.Write("Pegar imagen en nota fallo: " + ex.Message);
                MessageBox.Show(this, "No se pudo pegar la imagen.", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Añadir imagen",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog(this) != true) return;

            int failed = 0;
            foreach (string f in dlg.FileNames)
            {
                var bi = NoteStore.LoadImage(f);
                if (bi == null)
                {
                    failed++;
                    continue;
                }

                try
                {
                    NoteStore.AddImage(_note, bi);
                }
                catch (Exception ex)
                {
                    AppLog.Write("No se pudo añadir imagen a la nota: " + ex.Message);
                    failed++;
                }
            }

            Persist();
            RebuildImages();
            if (failed > 0)
                MessageBox.Show(this, "No se pudieron añadir " + failed + " imagen(es).", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ───────────── Otros eventos ─────────────
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(this, "¿Eliminar esta nota y sus imágenes?", "PinShot",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            _deleted = true;
            _saveTimer.Stop();
            if (!NoteStore.Delete(_note))
            {
                _deleted = false;
                MessageBox.Show(this, "No se pudo eliminar la nota. Revisa el registro para más detalles.", "PinShot",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Close();
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

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl && e.Key == Key.S)
            {
                _saveTimer.Stop();
                Persist();
                e.Handled = true;
                return;
            }

            if (ctrl && e.Key == Key.V)
            {
                try
                {
                    // Si hay una imagen (y no texto), se pega como imagen de la nota
                    if (Clipboard.ContainsImage() && !Clipboard.ContainsText())
                    {
                        if (TryPasteImage()) e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Write("Pegar imagen con teclado fallo: " + ex.Message);
                    SaveStatus.Text = "No se pudo pegar la imagen";
                    SaveStatus.Foreground = UiHelpers.BrushOf("#EF4444");
                    e.Handled = true;
                }
            }
        }
    }
}
