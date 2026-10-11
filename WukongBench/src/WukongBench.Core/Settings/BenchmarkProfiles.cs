using System.Collections.Immutable;

namespace WukongBench.Core.Settings;

public enum PassSelection { Both, CpuOnly, GpuOnly }

public static class BenchmarkProfiles
{
    public const int MinRenderScalePercent = 33;
    public const int MaxRenderScalePercent = 100;

    private const string ResolutionName = "Разрешение вывода";
    private const string ResolutionValue = "без изменений";
    private const string SyncName = "VSync и лимит FPS";
    private const string FrameGenerationName = "Генерация кадров";
    private const string Off = "выкл";

    public static ImmutableArray<BenchmarkProfile> Select(PassSelection selection, bool fullRayTracing) =>
        selection switch
        {
            PassSelection.CpuOnly => ImmutableArray.Create(Cpu()),
            PassSelection.GpuOnly => ImmutableArray.Create(Gpu(fullRayTracing)),
            _ => ImmutableArray.Create(Cpu(), Gpu(fullRayTracing)),
        };

    public static BenchmarkProfile Cpu() => new(
        Id: "cpu",
        Title: "CPU-тест",
        Quality: QualityPreset.Low,
        RenderScalePercent: MinRenderScalePercent,
        Upscaler: Upscaler.Tsr,
        FrameGeneration: false,
        FullRayTracing: false,
        RayTracingLevel: RayTracingLevel.Low,
        MotionBlur: MotionBlur.Off,
        Choices: ImmutableArray.Create(
            new SettingChoice("Качество (все группы)", "Низкое (1)",
                "Минимум работы для видеокарты: кадр упирается в игровую логику и подготовку draw calls на CPU."),
            new SettingChoice("Масштаб рендера", $"{MinRenderScalePercent}%",
                "Минимум, который разрешает меню: число пикселей падает примерно в 9 раз, GPU перестаёт быть узким местом."),
            new SettingChoice("Апскейлер", "TSR",
                "Работает на любой видеокарте и принимает произвольный масштаб, поэтому результат сравним между вендорами."),
            new SettingChoice(FrameGenerationName, Off,
                "Сгенерированные кадры не проходят через CPU и завысили бы FPS, скрыв процессорный предел."),
            new SettingChoice("Полная трассировка лучей", Off,
                "Самая тяжёлая для GPU опция, в CPU-тесте не нужна."),
            new SettingChoice("Размытие в движении", Off, "Лишний проход постобработки на GPU."),
            new SettingChoice(SyncName, Off, "Иначе FPS упрётся в частоту монитора, а не в процессор."),
            new SettingChoice(ResolutionName, ResolutionValue,
                "Нагрузку на GPU снимает масштаб рендера; режим экрана не трогаем, чтобы не зависеть от монитора.")));

    public static BenchmarkProfile Gpu(bool fullRayTracing) => new(
        Id: "gpu",
        Title: "GPU-тест",
        Quality: QualityPreset.Cinematic,
        RenderScalePercent: MaxRenderScalePercent,
        Upscaler: Upscaler.Tsr,
        FrameGeneration: false,
        FullRayTracing: fullRayTracing,
        RayTracingLevel: RayTracingLevel.Ultra,
        MotionBlur: MotionBlur.High,
        Choices: ImmutableArray.Create(
            new SettingChoice("Качество (все группы)", "Кинематографическое (5)",
                "Максимум по каждой группе: тени, Lumen, отражения, текстуры, растительность."),
            new SettingChoice("Масштаб рендера", $"{MaxRenderScalePercent}%",
                "Рендер в полном разрешении без апскейла: максимум пикселей на видеокарту."),
            new SettingChoice("Апскейлер", "TSR",
                "При 100% работает как сглаживание и одинаково доступен на NVIDIA, AMD и Intel."),
            new SettingChoice(FrameGenerationName, Off,
                "Она повышает FPS, а не нагрузку; без неё FPS отражает реальную скорость рендера."),
            new SettingChoice("Полная трассировка лучей",
                fullRayTracing ? "вкл, уровень «Очень высокий» (3)" : "выкл: не поддерживается видеокартой или отключена ключом --no-rt",
                "Path tracing — самая тяжёлая опция бенчмарка; включается только там, где игра её поддерживает (NVIDIA RTX)."),
            new SettingChoice("Размытие в движении", "сильное (2)", "Дополнительный проход постобработки на GPU."),
            new SettingChoice(SyncName, Off, "Иначе видеокарта простаивала бы в ожидании кадра."),
            new SettingChoice(ResolutionName, ResolutionValue,
                "Используется текущее разрешение из настроек бенчмарка (по умолчанию нативное).")));
}
