using System.Collections.Immutable;

namespace WukongBench.Core.SystemInfo;

public sealed record GpuSpec(string Name, double? VramGb, string? DriverVersion);

public sealed record SystemSpecs(
    string Cpu,
    int Cores,
    int Threads,
    ImmutableArray<GpuSpec> Gpus,
    double RamGb,
    string? RamDetails,
    string Os,
    string? Motherboard,
    string? Display)
{
    /// <summary>The benchmark offers full ray tracing (path tracing) only on NVIDIA RTX cards.</summary>
    public bool SupportsFullRayTracing =>
        Gpus.Any(gpu => gpu.Name.Contains("RTX", StringComparison.OrdinalIgnoreCase));
}
