using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;

namespace PinShot
{
    /// <summary>Biblioteca de capturas y notas guardadas.</summary>
    public partial class LibraryWindow : Window
    {
        private string _filter = "";
        private bool _pinnedOnly;

        public LibraryWindow()
        {
            InitializeComponent();

            NoteStore.Changed += OnStoreChanged;
            Closed += (s, e) => NoteStore.Changed -= OnStoreChanged;

            Refresh();
        }

        private void OnStoreChanged()
        {
            Dispatcher.BeginInvoke(new Action(Refresh));
        }

        // ───────────── Lista ─────────────
        private void Refresh()
        {
            CardsPanel.Children.Clear();
            CardsPanel.Children.Add(BuildQuickCard());

            string f = _filter.Trim();
            int shown = 0;

            foreach (var n in NoteStore.LoadAll())
            {
                bool matchesText = f.Length == 0 ||
                    n.Text.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    n.Tags.Any(tag => tag.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0);
                if (!matchesText || (_pinnedOnly && !n.IsPinned))
                    continue;

                CardsPanel.Children.Add(BuildCard(n));
                shown++;
            }

            if (shown == 0)
            {
                CardsPanel.Children.Add(new TextBlock
                {
                    Text = _pinnedOnly ? "No hay notas fijadas que coincidan." :
                        f.Length > 0 ? "No hay notas con ese texto o etiqueta." :
                        "Aun no hay notas guardadas.\nGuarda una captura desde su ventana o crea una nota nueva.",
                    Foreground = UiHelpers.BrushOf("#91A0B4"),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(4, 14, 4, 0)
                });
            }
        }

        private UIElement BuildQuickCard()
        {
            var accent = UiHelpers.BrushOf(UiHelpers.DisplayColor(NoteStore.Settings.DefaultColor));
            var normalBg = UiHelpers.BrushOf("#111B2B");
            var hoverBg = UiHelpers.BrushOf("#172438");

            var card = new Border
            {
                Background = normalBg,
                BorderBrush = UiHelpers.BrushOf("#2A3A50"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Child = new Border
                {
                    BorderBrush = accent,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(12, 14, 10, 14),
                    Child = new StackPanel
                    {
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "\uE710",
                                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                                Foreground = UiHelpers.BrushOf("#60A5FA"),
                                FontSize = 15,
                                Margin = new Thickness(0, 0, 10, 0),
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = "Crear una nota nueva",
                                Foreground = UiHelpers.BrushOf("#D7E2F0"),
                                FontSize = 13,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    }
                }
            };

            card.MouseEnter += (s, e) => card.Background = hoverBg;
            card.MouseLeave += (s, e) => card.Background = normalBg;
            card.MouseLeftButtonUp += (s, e) => App.OpenNewNote();
            return card;
        }

        private UIElement BuildCard(NoteItem n)
        {
            var accent = UiHelpers.BrushOf(UiHelpers.DisplayColor(n.Color));
            var normalBg = UiHelpers.BrushOf("#111B2B");
            var hoverBg = UiHelpers.BrushOf("#172438");

            var stack = new StackPanel();

            // Fecha (con el color de la nota)
            var metadata = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 7, 10, 5)
            };
            if (n.IsPinned)
            {
                metadata.Children.Add(new TextBlock
                {
                    Text = "\uE718",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    Foreground = accent,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
            metadata.Children.Add(new TextBlock
            {
                Text = UiHelpers.FormatDate(n.Modified),
                Foreground = UiHelpers.BrushOf("#8392A8"),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(metadata);

            if (n.Tags.Count > 0)
            {
                var tags = new WrapPanel { Margin = new Thickness(8, 0, 8, 4) };
                foreach (string tag in n.Tags)
                {
                    tags.Children.Add(new Border
                    {
                        Background = UiHelpers.BrushOf("#1D2A3B"),
                        CornerRadius = new CornerRadius(5),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 4, 4),
                        Child = new TextBlock
                        {
                            Text = "#" + tag,
                            Foreground = UiHelpers.BrushOf("#AFC2D9"),
                            FontSize = 10
                        }
                    });
                }
                stack.Children.Add(tags);
            }

            // Miniaturas (hasta 4, la 4ª muestra "N mas")
            if (n.Images.Count > 0)
            {
                int shown = Math.Min(4, n.Images.Count);
                var grid = new UniformGrid
                {
                    Rows = 1,
                    Columns = shown,
                    Height = 110,
                    Margin = new Thickness(6, 2, 6, 6)
                };

                for (int i = 0; i < shown; i++)
                {
                    var cell = new Grid { Margin = new Thickness(2), ClipToBounds = true };

                    cell.Children.Add(new Image
                    {
                        Stretch = Stretch.UniformToFill,
                        Source = WpfImageCodec.Load(NoteStore.ImagePath(n, n.Images[i]), 240)
                    });

                    if (i == 3 && n.Images.Count > 4)
                    {
                        cell.Children.Add(new Border
                        {
                            Background = UiHelpers.BrushOf("#99000000"),
                            Child = new TextBlock
                            {
                                Text = (n.Images.Count - 3) + " más",
                                Foreground = Brushes.White,
                                FontSize = 13,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        });
                    }

                    grid.Children.Add(cell);
                }

                stack.Children.Add(grid);
            }

            // Texto
            string text = n.Text.Trim();
            if (text.Length > 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = text,
                    Foreground = UiHelpers.BrushOf("#DCE6F2"),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 66,
                    Margin = new Thickness(12, 3, 12, 12)
                });
            }
            else if (n.Images.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "(nota vacía)",
                    Foreground = UiHelpers.BrushOf("#718198"),
                    Margin = new Thickness(12, 2, 12, 10)
                });
            }
            else
            {
                stack.Children.Add(new Border { Height = 4 });
            }

            var card = new Border
            {
                Background = normalBg,
                BorderBrush = UiHelpers.BrushOf("#2A3A50"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Child = new Border
                {
                    BorderBrush = accent,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(8),
                    Child = stack
                }
            };

            card.MouseEnter += (s, e) => card.Background = hoverBg;
            card.MouseLeave += (s, e) => card.Background = normalBg;
            card.MouseLeftButtonUp += (s, e) => NoteEditorWindow.Open(n);
            card.ContextMenu = BuildMenu(n);

            return card;
        }

        private ContextMenu BuildMenu(NoteItem n)
        {
            var menu = new ContextMenu();

            var open = new MenuItem { Header = "Abrir nota" };
            open.Click += (s, e) => NoteEditorWindow.Open(n);

            var pin = new MenuItem { Header = n.IsPinned ? "Quitar de fijadas" : "Fijar nota" };
            pin.Click += (s, e) =>
            {
                n.IsPinned = !n.IsPinned;
                if (!NoteStore.Save(n))
                {
                    n.IsPinned = !n.IsPinned;
                    MessageBox.Show(this, "No se pudo actualizar la nota.", "PinShot",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                NoteEditorWindow.UpdatePinnedState(n.Id, n.IsPinned);
            };

            var delete = new MenuItem { Header = "Eliminar nota" };
            delete.Click += (s, e) =>
            {
                if (!UiDialogs.ConfirmDelete(this, "La nota y sus imágenes se eliminarán permanentemente."))
                    return;

                if (!NoteStore.Delete(n))
                {
                    MessageBox.Show(this, "No se pudo eliminar la nota.", "PinShot",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                NoteEditorWindow.CloseIfOpen(n.Id);
            };

            menu.Items.Add(open);
            menu.Items.Add(pin);
            menu.Items.Add(delete);
            return menu;
        }

        // ───────────── Eventos ─────────────
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchHint == null || CardsPanel == null) return;

            _filter = SearchBox.Text;
            SearchHint.Visibility = string.IsNullOrEmpty(_filter) ? Visibility.Visible : Visibility.Collapsed;
            Refresh();
        }

        private void PinnedOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (PinnedOnlyCheck == null) return;
            _pinnedOnly = PinnedOnlyCheck.IsChecked == true;
            Refresh();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl && e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.N)
            {
                App.OpenNewNote();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
            {
                SearchBox.Clear();
                e.Handled = true;
            }
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            App.OpenNewNote();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            App.OpenSettings();
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
