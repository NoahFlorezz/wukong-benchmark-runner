using System.Collections.Immutable;
using System.Globalization;

namespace WukongBench.Core.Settings;

/// <summary>Turns the user's GameUserSettings.ini into one that carries a benchmark profile.</summary>
public static class SettingsApplier
{
    public const string MainSection = "/Script/GSGameSettings.GSGameUserSettings";
    public const string ScalabilitySection = "ScalabilityGroups";
    public const string UiSettingKey = "UISettingData";

    private static readonly ImmutableArray<string> UiQualityKeys = ImmutableArray.Create(
        "QualityLevel", "ViewDistance", "AntiAliasing", "PostProcessing", "ShadowQuality", "TextureQuality",
        "FxQuality", "MaterialQuality", "VegetationQuality", "GlobalIllumination", "ReflectionQuality");

    private static readonly ImmutableArray<string> ScalabilityKeys = ImmutableArray.Create(
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.GlobalIlluminationQuality",
        "sg.ReflectionQuality", "sg.PostProcessQuality", "sg.TextureQuality", "sg.EffectsQuality",
        "sg.FoliageQuality", "sg.ShadingQuality");

    /// <param name="fallbackOutputHeight">Used when the ini does not say which resolution the game runs at.</param>
    public static string Apply(string iniText, BenchmarkProfile profile, int fallbackOutputHeight)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var ini = IniDocument.Parse(iniText);
        var ui = UiSettingData.Parse(ini.Get(MainSection, UiSettingKey));
        if (ui.IsEmpty)
        {
            throw new InvalidDataException(
                $"В файле настроек нет строки {UiSettingKey}. Запустите бенчмарк один раз вручную до главного меню.");
        }

        var renderHeight = Math.Max(1, OutputHeight(ini, fallbackOutputHeight) * profile.RenderScalePercent / 100);
        var withMain = MainValues(BuildUi(ui, profile, renderHeight))
            .Aggregate(ini, (doc, pair) => doc.Set(MainSection, pair.Key, pair.Value));
        return ScalabilityValues(profile)
            .Aggregate(withMain, (doc, pair) => doc.Set(ScalabilitySection, pair.Key, pair.Value))
            .ToString();
    }

    private static int OutputHeight(IniDocument ini, int fallback) =>
        int.TryParse(ini.Get(MainSection, "ResolutionSizeY"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
        && height > 0
            ? height
            : fallback;

    private static UiSettingData BuildUi(UiSettingData current, BenchmarkProfile profile, int renderHeight)
    {
        var quality = Number((int)profile.Quality);
        var withQuality = UiQualityKeys.Aggregate(current, (ui, key) => ui.With(key, quality));
        return withQuality
            .With("Vsync", "0")
            .With("LockFrameRate", "0")
            .With("SuperResolutionSampling", Number((int)profile.Upscaler))
            // The menu stores render scale as the render height in pixels.
            .With("ImageQuality", Number(renderHeight))
            .With("InsertFrame", Flag(profile.FrameGeneration))
            .With("Rtx", Flag(profile.FullRayTracing))
            .With("RtxLevel", Number((int)profile.RayTracingLevel))
            .With("MotionBlur", Number((int)profile.MotionBlur));
    }

    private static IEnumerable<KeyValuePair<string, string>> MainValues(UiSettingData ui) => new[]
    {
        KeyValuePair.Create(UiSettingKey, ui.ToString()),
        KeyValuePair.Create("bUseVSync", "False"),
        KeyValuePair.Create("bUseDynamicResolution", "False"),
        KeyValuePair.Create("FrameRateLimit", "0.000000"),
    };

    private static IEnumerable<KeyValuePair<string, string>> ScalabilityValues(BenchmarkProfile profile)
    {
        // Unreal scalability groups are zero-based, the menu values are one-based.
        var level = Number((int)profile.Quality - 1);
        return ScalabilityKeys
            .Select(key => KeyValuePair.Create(key, level))
            .Prepend(KeyValuePair.Create("sg.ResolutionQuality", Number(profile.RenderScalePercent)));
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "1" : "0";
}
