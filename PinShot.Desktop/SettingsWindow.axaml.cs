using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PinShot;

namespace PinShot.Desktop;

public partial class SettingsWindow : Window
{
    private bool _loading;
    private HardwareSnapshot? _hardware;

    public SettingsWindow()
    {
        InitializeComponent();
        _loading = true;
        PathText.Text = NoteStore.NotesDir;
        ColorBlindCheck.IsChecked = NoteStore.Settings.ColorBlindMode;
        SelectProfile(NoteStore.Settings.ResourceProfile);
        BuildPalette();
        _loading = false;
        _ = RefreshHardwareAsync();
    }

    private void BuildPalette()
    {
        PalettePanel.Children.Clear();
        string[] colors = NoteStore.Settings.ColorBlindMode
            ? NoteStore.AccessiblePalette
            : NoteStore.Palette;

        foreach (string color in colors)
        {
            var button = new Button
            {
                Width = 30,
                Height = 30,
                Margin = new Avalonia.Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(Color.Parse(color)),
                BorderBrush = NoteStore.Settings.DefaultColor == color
                    ? Brushes.White
                    : new SolidColorBrush(Color.Parse("#263449")),
                BorderThickness = new Avalonia.Thickness(2),
                Content = NoteStore.Settings.DefaultColor == color ? "✓" : ""
            };
            ToolTip.SetTip(button, color);
            button.Click += (_, _) => SaveDefaultColor(color);
            PalettePanel.Children.Add(button);
        }
    }

    private async void SaveDefaultColor(string color)
    {
        string previous = NoteStore.Settings.DefaultColor;
        NoteStore.Settings.DefaultColor = color;
        if (!NoteStore.SaveSettings())
        {
            NoteStore.Settings.DefaultColor = previous;
            await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo guardar la configuración.");
            return;
        }

        BuildPalette();
        NoteStore.NotifyChanged();
    }

    private async void ColorBlind_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_loading) return;

        bool previous = NoteStore.Settings.ColorBlindMode;
        NoteStore.Settings.ColorBlindMode = ColorBlindCheck.IsChecked == true;
        if (!NoteStore.SaveSettings())
        {
            NoteStore.Settings.ColorBlindMode = previous;
            _loading = true;
            ColorBlindCheck.IsChecked = previous;
            _loading = false;
            await DialogService.ShowErrorAsync(this, "PinShot", "No se pudo guardar la configuración.");
            return;
        }

        BuildPalette();
        NoteStore.NotifyChanged();
    }

    private async void ApplyAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool confirmed = await DialogService.ConfirmAsync(
            this, "Aplicar color", "¿Cambiar el color de todas las notas guardadas?");
        if (!confirmed) return;

        int failed = 0;
        foreach (NoteItem note in NoteStore.LoadAll())
        {
            note.Color = NoteStore.Settings.DefaultColor;
            if (!NoteStore.Save(note, notify: false, touch: false)) failed++;
        }

        NoteStore.NotifyChanged();
        if (failed > 0)
            await DialogService.ShowErrorAsync(this, "PinShot",
                $"No se pudo actualizar el color de {failed} nota(s).");
    }

    private async void Profile_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfileCombo.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string value) return;

        string previous = NoteStore.Settings.ResourceProfile;
        NoteStore.Settings.ResourceProfile = value;
        if (!NoteStore.SaveSettings())
        {
            NoteStore.Settings.ResourceProfile = previous;
            _loading = true;
            SelectProfile(previous);
            _loading = false;
            await DialogService.ShowErrorAsync(this, "PinShot",
                "No se pudo guardar el perfil de recursos.");
            return;
        }

        UpdateBudget();
    }

    private void SelectProfile(string value)
    {
        ProfileCombo.SelectedItem = ProfileCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), value,
                StringComparison.OrdinalIgnoreCase))
            ?? ProfileCombo.Items.OfType<ComboBoxItem>().First();
    }

    private async void DetectHardware_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshHardwareAsync();
    }

    private async Task RefreshHardwareAsync()
    {
        DetectHardwareButton.IsEnabled = false;
        HardwareText.Text = "Comprobando CPU, memoria y Vulkan…";
        try
        {
            _hardware = await HardwareInfoService.DetectAsync();
            HardwareText.Text =
                $"{_hardware.OperatingSystem} ({_hardware.OperatingSystemVersion})\n" +
                $"Arquitectura: {_hardware.Architecture}\n" +
                $"CPU: {_hardware.CpuName} — {_hardware.LogicalProcessors} procesador(es) lógicos\n" +
                $"RAM total: {FormatBytes(_hardware.TotalMemoryBytes)}; disponible: {FormatBytes(_hardware.AvailableMemoryBytes)}\n" +
                $"GPU: {_hardware.GpuName ?? "no identificada"}; VRAM: {FormatBytes(_hardware.GpuMemoryBytes)}\n" +
                $"{_hardware.HardwareBackendStatus}";
            UpdateBudget();
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo detectar el hardware: " + ex);
            HardwareText.Text = "No se pudo completar la detección del hardware.";
        }
        finally
        {
            DetectHardwareButton.IsEnabled = true;
        }
    }

    private void UpdateBudget()
    {
        if (_hardware == null) return;

        ResourceProfile profile = GetSelectedProfile(_hardware);
        ResourceBudget budget = HardwareInfoService.CreateBudget(profile, _hardware);
        ProfileBudgetText.Text =
            $"Perfil activo: {ProfileLabel(profile)}. Bloques: {budget.TileSize}px; " +
            $"operaciones simultáneas: {budget.MaxConcurrentOperations}; " +
            $"caché: {FormatBytes(budget.CacheLimitBytes)}; " +
            $"memoria de trabajo máxima: {FormatBytes(budget.MaxWorkingMemoryBytes)}.";
    }

    private ResourceProfile GetSelectedProfile(HardwareSnapshot hardware) =>
        NoteStore.Settings.ResourceProfile.ToLowerInvariant() switch
        {
            "light" => ResourceProfile.Light,
            "balanced" => ResourceProfile.Balanced,
            "performance" => ResourceProfile.Performance,
            _ => HardwareInfoService.Recommend(hardware)
        };

    private static string ProfileLabel(ResourceProfile profile) => profile switch
    {
        ResourceProfile.Light => "Ligero",
        ResourceProfile.Balanced => "Equilibrado",
        _ => "Rendimiento"
    };

    private async void OpenFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(NoteStore.NotesDir);
            string executable = OperatingSystem.IsWindows() ? "explorer.exe"
                : OperatingSystem.IsMacOS() ? "open"
                : "xdg-open";
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(NoteStore.NotesDir);
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo abrir la carpeta de notas: " + ex);
            await DialogService.ShowErrorAsync(this, "PinShot",
                "No se pudo abrir la carpeta de notas: " + ex.Message);
        }
    }

    private async void Export_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar respaldo de PinShot",
            SuggestedFileName = "PinShot-respaldo-" + DateTime.Now.ToString("yyyyMMdd") + ".zip",
            DefaultExtension = "zip",
            FileTypeChoices = new[] { new FilePickerFileType("Respaldo ZIP") { Patterns = new[] { "*.zip" } } }
        });
        if (file?.Path?.IsFile != true) return;

        try
        {
            NoteStore.ExportNotes(file.Path.LocalPath);
            await DialogService.ShowInfoAsync(this, "PinShot", "Respaldo exportado correctamente.");
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo exportar el respaldo: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo exportar", ex.Message);
        }
    }

    private async void Import_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Importar respaldo de PinShot",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Respaldo ZIP") { Patterns = new[] { "*.zip" } } }
            });
        if (files.Count == 0 || files[0].Path?.IsFile != true) return;

        bool confirmed = await DialogService.ConfirmAsync(this, "Importar respaldo",
            "Las notas se agregarán a las actuales; no se reemplazará ninguna.");
        if (!confirmed) return;

        try
        {
            int imported = NoteStore.ImportNotes(files[0].Path.LocalPath);
            await DialogService.ShowInfoAsync(this, "PinShot",
                imported == 0 ? "El respaldo no contiene notas." : $"Se importaron {imported} nota(s).");
        }
        catch (Exception ex)
        {
            AppLog.Write("No se pudo importar el respaldo: " + ex);
            await DialogService.ShowErrorAsync(this, "No se pudo importar", ex.Message);
        }
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private static string FormatBytes(long? value) =>
        value is long bytes ? $"{bytes / (1024d * 1024d * 1024d):F1} GB" : "no disponible";
}
