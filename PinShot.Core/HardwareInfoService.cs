using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PinShot;

public sealed record HardwareSnapshot(
    string OperatingSystem,
    string OperatingSystemVersion,
    string Architecture,
    string CpuName,
    int LogicalProcessors,
    long? TotalMemoryBytes,
    long? AvailableMemoryBytes,
    string? GpuName,
    bool? VulkanAvailable,
    string HardwareBackendStatus,
    long? GpuMemoryBytes = null);

public enum ResourceProfile
{
    Light,
    Balanced,
    Performance
}

public sealed record ResourceBudget(
    int TileSize,
    int MaxConcurrentOperations,
    int PreviewMaxDimension,
    long CacheLimitBytes,
    long MaxWorkingMemoryBytes);

public static partial class HardwareInfoService
{
    private static readonly TimeSpan BackendProbeTimeout = TimeSpan.FromSeconds(4);
    private const long Megabyte = 1024L * 1024L;

    public static HardwareSnapshot DetectBasic()
    {
        (long? total, long? available) = GetMemory();

        return new HardwareSnapshot(
            RuntimeInformation.OSDescription,
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture.ToString(),
            GetCpuName(),
            Math.Max(1, Environment.ProcessorCount),
            total,
            available,
            null,
            null,
            "La GPU y Vulkan todavía no se han comprobado.");
    }

    public static async Task<HardwareSnapshot> DetectAsync(CancellationToken cancellationToken = default)
    {
        HardwareSnapshot basic = await Task.Run(DetectBasic, cancellationToken).ConfigureAwait(false);
        try
        {
            (bool? available, string? gpuName, string status) =
                await ProbeVulkanAsync(cancellationToken).ConfigureAwait(false);
            return basic with
            {
                GpuName = gpuName,
                VulkanAvailable = available,
                HardwareBackendStatus = status
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    public static ResourceProfile Recommend(HardwareSnapshot hardware)
    {
        if (hardware.TotalMemoryBytes is long memory && memory <= 6 * 1024L * Megabyte ||
            hardware.LogicalProcessors <= 2)
        {
            return ResourceProfile.Light;
        }

        if (hardware.TotalMemoryBytes is long moderateMemory && moderateMemory <= 12 * 1024L * Megabyte ||
            hardware.LogicalProcessors <= 4)
        {
            return ResourceProfile.Balanced;
        }

        return ResourceProfile.Performance;
    }

    public static ResourceBudget CreateBudget(ResourceProfile profile, HardwareSnapshot hardware)
    {
        long available = hardware.AvailableMemoryBytes ?? hardware.TotalMemoryBytes ?? 2 * 1024L * Megabyte;
        int halfCores = Math.Max(1, hardware.LogicalProcessors / 2);

        return profile switch
        {
            ResourceProfile.Light => new ResourceBudget(
                TileSize: 128,
                MaxConcurrentOperations: 1,
                PreviewMaxDimension: 720,
                CacheLimitBytes: 128 * Megabyte,
                MaxWorkingMemoryBytes: Math.Min(available / 8, 512 * Megabyte)),
            ResourceProfile.Balanced => new ResourceBudget(
                TileSize: 256,
                MaxConcurrentOperations: Math.Min(2, halfCores),
                PreviewMaxDimension: 1440,
                CacheLimitBytes: 256 * Megabyte,
                MaxWorkingMemoryBytes: Math.Min(available / 5, 1536 * Megabyte)),
            _ => new ResourceBudget(
                TileSize: 384,
                MaxConcurrentOperations: Math.Min(2, halfCores),
                PreviewMaxDimension: 2048,
                CacheLimitBytes: 512 * Megabyte,
                MaxWorkingMemoryBytes: Math.Min(available / 4, 3 * 1024L * Megabyte))
        };
    }

    private static (long? Total, long? Available) GetMemory()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status))
                return (checked((long)status.TotalPhysical), checked((long)status.AvailablePhysical));
        }
        else if (OperatingSystem.IsLinux())
        {
            return ReadLinuxMemoryInfo();
        }
        else if (OperatingSystem.IsMacOS())
        {
            return ReadMacMemoryInfo();
        }

        return (null, null);
    }

    private static (long? Total, long? Available) ReadLinuxMemoryInfo()
    {
        const string memInfoPath = "/proc/meminfo";
        if (!File.Exists(memInfoPath)) return (null, null);

        try
        {
            long? total = null;
            long? available = null;
            foreach (string line in File.ReadLines(memInfoPath))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !long.TryParse(parts[1], out long kilobytes)) continue;

                if (parts[0] == "MemTotal:") total = checked(kilobytes * 1024);
                else if (parts[0] == "MemAvailable:") available = checked(kilobytes * 1024);
            }

            return (total, available);
        }
        catch (IOException)
        {
            return (null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    private static (long? Total, long? Available) ReadMacMemoryInfo()
    {
        string? totalText = RunMacTool("/usr/sbin/sysctl", "-n", "hw.memsize");
        long? total = long.TryParse(totalText, out long totalBytes) ? totalBytes : null;

        string? pageSizeText = RunMacTool("/usr/sbin/sysctl", "-n", "hw.pagesize");
        long pageSize = long.TryParse(pageSizeText, out long parsedPageSize) ? parsedPageSize : 4096;
        string? vmStatistics = RunMacTool("/usr/bin/vm_stat");
        if (vmStatistics == null) return (total, null);

        long availablePages = 0;
        foreach (Match match in MacMemoryPageRegex().Matches(vmStatistics))
        {
            if (long.TryParse(match.Groups[2].Value, out long pages))
                availablePages = checked(availablePages + pages);
        }

        return (total, availablePages > 0 ? checked(availablePages * pageSize) : null);
    }

    private static string? RunMacTool(string executable, params string[] arguments)
    {
        if (!File.Exists(executable)) return null;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            foreach (string argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            if (!process.Start()) return null;
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(800))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return null;
            }

            return process.ExitCode == 0 ? outputTask.GetAwaiter().GetResult().Trim() : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string GetCpuName()
    {
        string? configuredName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
        if (!string.IsNullOrWhiteSpace(configuredName)) return configuredName;

        if (OperatingSystem.IsLinux())
        {
            try
            {
                foreach (string line in File.ReadLines("/proc/cpuinfo"))
                {
                    if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("Hardware", StringComparison.OrdinalIgnoreCase))
                    {
                        int separator = line.IndexOf(':');
                        if (separator >= 0) return line[(separator + 1)..].Trim();
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        return RunMacTool("/usr/sbin/sysctl", "-n", "machdep.cpu.brand_string")
                            ?? "Desconocido";
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return "Desconocido";
    }

    private static async Task<(bool? Available, string? GpuName, string Status)> ProbeVulkanAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(BackendProbeTimeout);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "vulkaninfo.exe" : "vulkaninfo",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ArgumentList = { "--summary" }
            }
        };

        try
        {
            if (!process.Start())
                return (null, null, "No se pudo iniciar la comprobación de Vulkan.");

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
                return (false, null, "No se detectó un controlador Vulkan funcional.");

            string? gpu = DeviceNameRegex().Match(output) is { Success: true } match
                ? match.Groups[1].Value.Trim()
                : null;
            return (true, gpu, "La herramienta vulkaninfo confirmó un controlador Vulkan.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryStop(process);
            return (null, null, "La comprobación de Vulkan superó el límite de tiempo.");
        }
        catch (OperationCanceledException)
        {
            TryStop(process);
            throw;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (null, null, "vulkaninfo no está instalado o no está disponible en PATH.");
        }
        catch (IOException)
        {
            return (null, null, "No se pudo leer la respuesta de vulkaninfo.");
        }
    }

    [GeneratedRegex(@"deviceName\s*=\s*(.+)", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceNameRegex();

    [GeneratedRegex(@"Pages (free|inactive|speculative):\s+(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex MacMemoryPageRegex();

    private static void TryStop(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
