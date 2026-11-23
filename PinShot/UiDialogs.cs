using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfImage = System.Windows.Controls.Image;

namespace PinShot
{
    internal static class UiDialogs
    {
        public static bool ConfirmDelete(Window owner, string message)
        {
            var dialog = new Window
            {
                Title = "Eliminar nota - PinShot",
                Width = 400,
                Height = 228,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ShowInTaskbar = false,
                Background = BrushOf("#0B1120"),
                AllowsTransparency = false
            };

            var frame = new Border
            {
                Background = BrushOf("#0B1120"),
                BorderBrush = BrushOf("#334155"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9)
            };
            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });

            var header = new Grid { Background = BrushOf("#111B2B") };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            var logo = new WpfImage
            {
                Source = new System.Windows.Media.Imaging.BitmapImage(
                    new System.Uri("pack://application:,,,/PinShot_logo.png")),
                Width = 28,
                Height = 28,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(logo, 0);
            header.Children.Add(logo);

            var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(new TextBlock
            {
                Text = "PINSHOT",
                Foreground = BrushOf("#F8FAFC"),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            });
            title.Children.Add(new TextBlock
            {
                Text = "CONFIRMAR ACCIÓN",
                Foreground = BrushOf("#60A5FA"),
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            var close = new WpfButton
            {
                Content = "\uE711",
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                Foreground = BrushOf("#CBD5E1"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Cancelar"
            };
            close.Click += (s, e) => dialog.DialogResult = false;
            Grid.SetColumn(close, 2);
            header.Children.Add(close);
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) dialog.DragMove();
            };
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            var content = new StackPanel
            {
                Margin = new Thickness(22, 18, 22, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(new TextBlock
            {
                Text = "¿Eliminar esta nota?",
                Foreground = BrushOf("#F1F5F9"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            content.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = BrushOf("#AAB8CA"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetRow(content, 1);
            layout.Children.Add(content);

            var actions = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 16, 0)
            };
            var cancel = new WpfButton
            {
                Content = "Cancelar",
                Style = (Style)System.Windows.Application.Current.FindResource("ActionBtn"),
                MinWidth = 88,
                Margin = new Thickness(0, 0, 8, 0),
                IsCancel = true
            };
            cancel.Click += (s, e) => dialog.DialogResult = false;
            var delete = new WpfButton
            {
                Content = "Eliminar",
                Style = (Style)System.Windows.Application.Current.FindResource("ActionBtn"),
                Background = BrushOf("#B54708"),
                MinWidth = 88,
                IsDefault = false
            };
            delete.Click += (s, e) => dialog.DialogResult = true;
            actions.Children.Add(cancel);
            actions.Children.Add(delete);
            Grid.SetRow(actions, 2);
            layout.Children.Add(actions);

            frame.Child = layout;
            dialog.Content = frame;
            return dialog.ShowDialog() == true;
        }

        private static SolidColorBrush BrushOf(string color) =>
            new SolidColorBrush((System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString(color));
    }
}
