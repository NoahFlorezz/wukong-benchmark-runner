using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using WukongBench.Automation;
using WukongBench.Core.Cli;
using WukongBench.Core.Report;
using WukongBench.Core.Settings;
using WukongBench.Settings;
using WukongBench.Steam;
using WukongBench.SystemInfo;

namespace WukongBench;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitFailed = 1;
    private const int ExitBadArguments = 2;
    private const int ExitCancelled = 130;

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(Options.HelpText);
            return ExitBadArguments;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(Options.HelpText);
            return ExitOk;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Keep the process alive so the finally blocks can restore the user's settings.
            e.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            Run(options, cancellation.Token);
            return ExitOk;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Прервано пользователем. Исходные настройки бенчмарка восстановлены.");
            return ExitCancelled;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException
                                       or TimeoutException or UnauthorizedAccessException)
        {
            // IOException also covers the missing-folder and missing-file cases.
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return ExitFailed;
        }
        catch (Exception ex)
        {
            // Must not escape: finally blocks are not guaranteed to run for an unhandled exception,
            // and they are what restores the user's settings.
            Console.Error.WriteLine($"Непредвиденная ошибка: {ex}");
            return ExitFailed;
        }
    }

    private static void Run(Options options, CancellationToken cancellation)
    {
        NativeInput.EnableDpiAwareness();

        var installation = SteamLocator.Locate(options.InstallPath);
        Console.WriteLine($"Бенчмарк: {installation.RootDirectory}");

        var backup = new SettingsBackup(installation.SettingsPath);
        if (backup.RestoreIfStale())
        {
            Console.WriteLine("Восстановлены настройки, оставшиеся от прерванного прошлого запуска.");
        }

        if (!File.Exists(installation.SettingsPath))
        {
            throw new FileNotFoundException(
                "Файл настроек бенчмарка ещё не создан. Запустите Benchmark Tool один раз вручную до главного меню.",
                installation.SettingsPath);
        }

        Console.WriteLine("Сбор характеристик компьютера...");
        var specs = SystemInfoCollector.Collect();
        var profiles = BenchmarkProfiles.Select(
            options.Passes, options.AllowRayTracing && specs.SupportsFullRayTracing);

        var outputDirectory = Path.Combine(
            options.OutputDirectory, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(outputDirectory);

        var (originalSettings, encoding) = SettingsFile.Read(installation.SettingsPath);
        backup.Create();
        ImmutableArray<PassReport> passes;
        try
        {
            passes = profiles
                .Select(profile => RunPass(profile, installation, originalSettings, encoding, options, outputDirectory, cancellation))
                .ToImmutableArray();
        }
        finally
        {
            try
            {
                GameProcess.Kill();
            }
            finally
            {
                backup.Restore();
            }
        }

        var report = ReportBuilder.Build(specs, passes, DateTimeOffset.Now);
        var reportPath = Path.Combine(outputDirectory, "report.md");
        File.WriteAllText(reportPath, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Console.WriteLine();
        Console.WriteLine(report);
        Console.WriteLine($"Отчёт сохранён: {Path.GetFullPath(reportPath)}");
    }

    private static PassReport RunPass(
        BenchmarkProfile profile,
        BenchmarkInstallation installation,
        string originalSettings,
        Encoding encoding,
        Options options,
        string outputDirectory,
        CancellationToken cancellation)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {profile.Title} ===");

        // Settings are written while the game is closed: some of them are only read at startup.
        GameProcess.Kill();
        var settings = SettingsApplier.Apply(originalSettings, profile, NativeInput.PrimaryScreenHeight());
        SettingsFile.Write(installation.SettingsPath, settings, encoding);
        Console.WriteLine("Настройки применены, запуск бенчмарка (не трогайте мышь и клавиатуру)...");

        var run = BenchmarkRunner.Run(options, cancellation);
        File.Copy(run.SourcePath, Path.Combine(outputDirectory, $"{profile.Id}_raw.json"), overwrite: true);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture, $"Проход завершён: средний FPS {run.Result.FpsAvg:0.#}."));
        return new PassReport(profile, run.Result);
    }
}
