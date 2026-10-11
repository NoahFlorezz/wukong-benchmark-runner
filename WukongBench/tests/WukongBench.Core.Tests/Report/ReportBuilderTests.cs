using System.Collections.Immutable;
using WukongBench.Core.Report;
using WukongBench.Core.Results;
using WukongBench.Core.Settings;
using WukongBench.Core.SystemInfo;
using Xunit;

namespace WukongBench.Core.Tests.Report;

public sealed class ReportBuilderTests
{
    private static readonly SystemSpecs Specs = new(
        Cpu: "Test CPU 9000",
        Cores: 8,
        Threads: 16,
        Gpus: ImmutableArray.Create(new GpuSpec("Test RTX 1", 12, "1.2.3")),
        RamGb: 32,
        RamDetails: "2 модул., 6000 МГц",
        Os: "Test OS",
        Motherboard: null,
        Display: "2560x1440, 144 Гц");

    private static readonly DateTimeOffset FinishedAt = new(2026, 10, 10, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Build_ListsComputerSpecs()
    {
        var report = ReportBuilder.Build(Specs, Passes(), FinishedAt);

        Assert.Contains("Test CPU 9000 (8 ядер / 16 потоков)", report);
        Assert.Contains("Test RTX 1, 12 ГБ VRAM, драйвер 1.2.3", report);
        Assert.Contains("32 ГБ (2 модул., 6000 МГц)", report);
        Assert.Contains("2560x1440, 144 Гц", report);
        Assert.Contains("Дата: 2026-10-10 12:30", report);
    }

    [Fact]
    public void Build_ShowsBothPassesSideBySide()
    {
        var report = ReportBuilder.Build(Specs, Passes(), FinishedAt);

        var fpsLine = report.Split(Environment.NewLine).Single(line => line.StartsWith("| Средний FPS", StringComparison.Ordinal));
        Assert.Contains("240.5", fpsLine);
        Assert.Contains("61", fpsLine);
        Assert.Contains("CPU-тест", report);
        Assert.Contains("GPU-тест", report);
    }

    [Fact]
    public void Build_ExplainsWhichComponentLimitedEachPass()
    {
        var report = ReportBuilder.Build(Specs, Passes(), FinishedAt);

        Assert.Contains("CPU-тест: средняя загрузка GPU 57% — видеокарта загружена не полностью, FPS ограничен процессором.", report);
        Assert.Contains("GPU-тест: средняя загрузка GPU 98% — видеокарта загружена почти полностью, FPS ограничен GPU.", report);
    }

    [Fact]
    public void Build_ListsSettingsWithReasonsAndWhatTheToolEchoed()
    {
        var report = ReportBuilder.Build(Specs, Passes(), FinishedAt);

        Assert.Contains("## Настройки: CPU-тест", report);
        Assert.Contains("## Настройки: GPU-тест", report);
        Assert.Contains("Кинематографическое (5)", report);
        Assert.Contains("По данным самого бенчмарка: QualityLevel=1", report);
    }

    [Fact]
    public void Build_MarksMissingValues()
    {
        var report = ReportBuilder.Build(Specs, Passes(), FinishedAt);

        var boardLine = report.Split(Environment.NewLine).Single(line => line.StartsWith("| Материнская плата", StringComparison.Ordinal));
        Assert.Contains("н/д", boardLine);
    }

    [Fact]
    public void Render_PadsColumnsToTheWidestCell()
    {
        var table = TableFormatter.Render(
            new[] { "A", "Bb" },
            new IReadOnlyList<string>[] { new[] { "1", "2" }, new[] { "333", "4" } });

        var expected = string.Join(Environment.NewLine, "| A   | Bb |", "|-----|----|", "| 1   | 2  |", "| 333 | 4  |");
        Assert.Equal(expected, table);
    }

    private static PassReport[] Passes() => new[]
    {
        new PassReport(BenchmarkProfiles.Cpu(), BenchmarkResult.Parse("{\"FPSAvg\":240.5,\"GPUAvg\":57,\"QualityLevel\":1}")),
        new PassReport(BenchmarkProfiles.Gpu(fullRayTracing: true), BenchmarkResult.Parse("{\"FPSAvg\":61,\"GPUAvg\":98}")),
    };
}
