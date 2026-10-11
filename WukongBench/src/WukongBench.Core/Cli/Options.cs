using System.Globalization;
using WukongBench.Core.Settings;

namespace WukongBench.Core.Cli;

/// <summary>A point of the game UI in the 1920x1080 reference layout; scaled by window height at run time.</summary>
public sealed record MenuPoint(double X, double Y);

public sealed record Options(
    string? InstallPath,
    PassSelection Passes,
    bool AllowRayTracing,
    TimeSpan RunTimeout,
    MenuPoint BenchmarkMenuItem,
    string OutputDirectory,
    bool ShowHelp)
{
    public const string HelpText = """
        WukongBench — автоматический прогон Black Myth: Wukong Benchmark Tool (CPU-тест и GPU-тест).

        Использование: WukongBench [ключи]

          --path <папка>        Папка установки бенчмарка (по умолчанию ищется через Steam).
          --only cpu|gpu        Выполнить только один проход.
          --no-rt               Не включать полную трассировку лучей в GPU-тесте.
          --timeout <минуты>    Предел ожидания одного прохода (по умолчанию 15).
          --menu-point <x,y>    Координаты пункта «Тест быстродействия» в раскладке 1920x1080
                                (по умолчанию 220,484).
          --out <папка>         Куда сохранять отчёты (по умолчанию results).
          -h, --help            Показать эту справку.
        """;

    private const string DefaultOutputDirectory = "results";
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromMinutes(15);
    private static readonly MenuPoint DefaultMenuItem = new(220, 484);

    public static Options Default { get; } = new(
        null, PassSelection.Both, true, DefaultRunTimeout, DefaultMenuItem, DefaultOutputDirectory, false);

    /// <exception cref="ArgumentException">Unknown key or malformed value.</exception>
    public static Options Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = Default;
        for (var i = 0; i < args.Count; i++)
        {
            var key = args[i];
            options = key switch
            {
                "-h" or "--help" => options with { ShowHelp = true },
                "--no-rt" => options with { AllowRayTracing = false },
                "--path" => options with { InstallPath = Value(args, ++i, key) },
                "--out" => options with { OutputDirectory = Value(args, ++i, key) },
                "--only" => options with { Passes = ParsePasses(Value(args, ++i, key)) },
                "--timeout" => options with { RunTimeout = ParseTimeout(Value(args, ++i, key)) },
                "--menu-point" => options with { BenchmarkMenuItem = ParsePoint(Value(args, ++i, key)) },
                _ => throw new ArgumentException($"Неизвестный ключ: {key}"),
            };
        }

        return options;
    }

    private static string Value(IReadOnlyList<string> args, int index, string key) =>
        index < args.Count ? args[index] : throw new ArgumentException($"Для ключа {key} нужно значение.");

    private static PassSelection ParsePasses(string value) => value.ToLowerInvariant() switch
    {
        "cpu" => PassSelection.CpuOnly,
        "gpu" => PassSelection.GpuOnly,
        _ => throw new ArgumentException($"--only принимает cpu или gpu, получено: {value}"),
    };

    private static TimeSpan ParseTimeout(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : throw new ArgumentException($"--timeout принимает положительное число минут, получено: {value}");

    private static MenuPoint ParsePoint(string value)
    {
        var parts = value.Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            && x >= 0 && y >= 0)
        {
            return new MenuPoint(x, y);
        }

        throw new ArgumentException($"--menu-point принимает координаты вида 220,484, получено: {value}");
    }
}
