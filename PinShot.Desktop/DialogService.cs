using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PinShot.Desktop;

internal static class DialogService
{
    public static Task<bool> ConfirmAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, confirm: true);

    public static Task<bool> ShowErrorAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, confirm: false);

    public static Task<bool> ShowInfoAsync(Window owner, string title, string message) =>
        ShowAsync(owner, title, message, confirm: false);

    private static Task<bool> ShowAsync(Window owner, string title, string message, bool confirm)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#111B2B"),
            Foreground = Brush("#E2E8F0"),
            Padding = new Thickness(18)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 18, 0, 0)
        };

        if (confirm)
        {
            var cancel = new Button { Content = "Cancelar", MinWidth = 82 };
            cancel.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(cancel);
        }

        var accept = new Button { Content = confirm ? "Continuar" : "Cerrar", MinWidth = 82 };
        accept.Click += (_, _) => dialog.Close(confirm);
        buttons.Children.Add(accept);

        dialog.Content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons
            }
        };

        return dialog.ShowDialog<bool>(owner);
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
