using System.Collections.Immutable;

namespace WukongBench.Core.Settings;

// Numeric values are the ones the benchmark menu itself writes to UISettingData.
public enum QualityPreset { Low = 1, Medium = 2, High = 3, VeryHigh = 4, Cinematic = 5 }

public enum Upscaler { Fsr = 0, XeSS = 1, Dlss = 2, Tsr = 3 }

public enum MotionBlur { Off = 0, Low = 1, High = 2 }

public enum RayTracingLevel { Low = 1, Medium = 2, Ultra = 3 }

/// <summary>One line of the "settings used" table: what was set and why.</summary>
public sealed record SettingChoice(string Name, string Value, string Reason);

public sealed record BenchmarkProfile(
    string Id,
    string Title,
    QualityPreset Quality,
    int RenderScalePercent,
    Upscaler Upscaler,
    bool FrameGeneration,
    bool FullRayTracing,
    RayTracingLevel RayTracingLevel,
    MotionBlur MotionBlur,
    ImmutableArray<SettingChoice> Choices);
