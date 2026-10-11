using System.Collections.Immutable;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using WukongBench.Core.SystemInfo;

namespace WukongBench.SystemInfo;

/// <summary>Collects PC specs from WMI. Every field degrades to a placeholder rather than failing the run.</summary>
internal static class SystemInfoCollector
{
    private const double BytesPerGb = 1024.0 * 1024 * 1024;
    private const string Unknown = "не определено";
    private const string DisplayAdapterClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public static SystemSpecs Collect()
    {
        var cpu = Query("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor", item => new
        {
            Name = Text(item, "Name") ?? Unknown,
            Cores = Number(item, "NumberOfCores"),
            Threads = Number(item, "NumberOfLogicalProcessors"),
        });
        var memory = Query("SELECT Capacity, Speed FROM Win32_PhysicalMemory", item => new
        {
            Bytes = Number(item, "Capacity"),
            Speed = Number(item, "Speed"),
        });

        return new SystemSpecs(
            Cpu: cpu.Select(item => item.Name).FirstOrDefault() ?? Unknown,
            Cores: (int)cpu.Sum(item => item.Cores),
            Threads: (int)cpu.Sum(item => item.Threads),
            Gpus: CollectGpus(),
            RamGb: memory.Sum(module => module.Bytes) / BytesPerGb,
            RamDetails: memory.IsEmpty
                ? null
                : $"{memory.Length} модул., {memory.Max(module => module.Speed).ToString("0", CultureInfo.InvariantCulture)} МГц",
            Os: CollectOs(),
            Motherboard: Query("SELECT Manufacturer, Product FROM Win32_BaseBoard",
                    item => $"{Text(item, "Manufacturer")} {Text(item, "Product")}".Trim())
                .FirstOrDefault(board => board.Length > 0),
            Display: CollectDisplay());
    }

    private static ImmutableArray<GpuSpec> CollectGpus() =>
        Query("SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController", item =>
        {
            var name = Text(item, "Name") ?? Unknown;
            // AdapterRAM is a 32-bit value and caps at 4 GB, so prefer the driver's 64-bit registry value.
            var bytes = ReadVramBytesFromRegistry(name) ?? Number(item, "AdapterRAM");
            return new GpuSpec(name, bytes > 0 ? bytes / BytesPerGb : null, Text(item, "DriverVersion"));
        });

    private static double? ReadVramBytesFromRegistry(string adapterName)
    {
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(DisplayAdapterClassKey);
            if (classKey is null)
            {
                return null;
            }

            return classKey.GetSubKeyNames()
                .Select(subKeyName => ReadAdapterVramBytes(classKey, subKeyName, adapterName))
                .FirstOrDefault(bytes => bytes is not null);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // Not fatal: the caller falls back to the WMI value.
            return null;
        }
    }

    private static double? ReadAdapterVramBytes(RegistryKey classKey, string subKeyName, string adapterName)
    {
        try
        {
            using var adapter = classKey.OpenSubKey(subKeyName);
            if (adapter is null || adapter.GetValue("DriverDesc") as string != adapterName)
            {
                return null;
            }

            return adapter.GetValue("HardwareInformation.qwMemorySize") is long bytes ? bytes : null;
        }
        catch (SecurityException)
        {
            // The "Properties" subkey is admin-only; it never describes an adapter.
            return null;
        }
    }

    private static string CollectOs() =>
        Query("SELECT Caption, Version FROM Win32_OperatingSystem",
                item => $"{Text(item, "Caption")} ({Text(item, "Version")})")
            .FirstOrDefault()
        ?? RuntimeInformation.OSDescription;

    private static string? CollectDisplay() =>
        Query("SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController",
                item => new
                {
                    Width = Number(item, "CurrentHorizontalResolution"),
                    Height = Number(item, "CurrentVerticalResolution"),
                    Refresh = Number(item, "CurrentRefreshRate"),
                })
            .Where(mode => mode.Width > 0 && mode.Height > 0)
            .Select(mode => string.Create(CultureInfo.InvariantCulture, $"{mode.Width:0}x{mode.Height:0}, {mode.Refresh:0} Гц"))
            .FirstOrDefault();

    private static ImmutableArray<T> Query<T>(string wql, Func<ManagementBaseObject, T> map)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(wql);
            using var items = searcher.Get();
            return items.Cast<ManagementBaseObject>().Select(map).ToImmutableArray();
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Предупреждение: не удалось выполнить WMI-запрос ({ex.Message}).");
            return ImmutableArray<T>.Empty;
        }
    }

    private static string? Text(ManagementBaseObject item, string property) =>
        item[property]?.ToString()?.Trim() is { Length: > 0 } text ? text : null;

    private static double Number(ManagementBaseObject item, string property) =>
        item[property] is { } value ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0;
}
