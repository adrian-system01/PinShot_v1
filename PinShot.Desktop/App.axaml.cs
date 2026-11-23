using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System;

namespace PinShot.Desktop;

public partial class App : Application
{
    private static DateTime _suppressClipboardUntil;

    public static void SuppressClipboardFor(TimeSpan duration) =>
        _suppressClipboardUntil = DateTime.UtcNow.Add(duration);

    public static bool IsClipboardSuppressed => DateTime.UtcNow < _suppressClipboardUntil;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            ConfigureTray(mainWindow, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureTray(MainWindow mainWindow, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            using Stream iconStream = AssetLoader.Open(new Uri("avares://PinShot/Assets/PinShot.ico"));
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(iconStream),
                ToolTipText = "PinShot",
                IsVisible = true
            };

            var menu = new NativeMenu();
            var open = new NativeMenuItem { Header = "Abrir biblioteca" };
            open.Click += (_, _) => mainWindow.ShowAndActivate();
            menu.Items.Add(open);

            var newNote = new NativeMenuItem { Header = "Nueva nota" };
            newNote.Click += (_, _) =>
            {
                mainWindow.ShowAndActivate();
                mainWindow.OpenNewNote();
            };
            menu.Items.Add(newNote);

            var settings = new NativeMenuItem { Header = "Configuración" };
            settings.Click += (_, _) =>
            {
                mainWindow.ShowAndActivate();
                mainWindow.OpenSettings();
            };
            menu.Items.Add(settings);
            menu.Items.Add(new NativeMenuItemSeparator());

            var exit = new NativeMenuItem { Header = "Salir" };
            exit.Click += (_, _) => desktop.Shutdown();
            menu.Items.Add(exit);

            _trayIcon.Menu = menu;
            _trayIcon.Clicked += (_, _) => mainWindow.ShowAndActivate();
            TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
        }
        catch (Exception ex)
        {
            PinShot.AppLog.Write("No se pudo inicializar el icono de bandeja: " + ex);
        }
    }
}