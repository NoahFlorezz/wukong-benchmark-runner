using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using WukongBench.Core.Cli;
using WukongBench.Core.Results;
using WukongBench.Steam;

namespace WukongBench.Automation;

internal sealed record BenchmarkRun(BenchmarkResult Result, string SourcePath);

/// <summary>Launches the Benchmark Tool, starts the test through its menu and waits for the result file.</summary>
internal static class BenchmarkRunner
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan StepInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan InputPause = TimeSpan.FromMilliseconds(1500);
    // The result file is about 1.5 MB; give the game time to finish writing it.
    private static readonly TimeSpan ResultSettleTime = TimeSpan.FromSeconds(2);

    private static string ResultsDirectory => Path.Combine(Path.GetTempPath(), "b1", "BenchMarkHistory", "Tool");

    /// <exception cref="TimeoutException">The game did not start or the run did not finish in time.</exception>
    /// <exception cref="InvalidOperationException">The game could not be launched or exited without a result.</exception>
    public static BenchmarkRun Run(Options options, CancellationToken cancellation)
    {
        GameProcess.Kill();
        var knownResults = ExistingResults();
        try
        {
            Launch();
            return WaitForResult(options, knownResults, cancellation);
        }
        finally
        {
            GameProcess.Kill();
        }
    }

    private static void Launch()
    {
        try
        {
            // Going through Steam keeps its DRM bootstrap intact, unlike starting the exe directly.
            using var launcher = Process.Start(new ProcessStartInfo($"steam://rungameid/{SteamLocator.AppId}")
            {
                UseShellExecute = true,
            });
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Не удалось запустить бенчмарк через Steam: " + ex.Message, ex);
        }
    }

    private static BenchmarkRun WaitForResult(
        Options options, ImmutableHashSet<string> knownResults, CancellationToken cancellation)
    {
        var elapsed = Stopwatch.StartNew();
        var gameSeen = false;
        while (elapsed.Elapsed < options.RunTimeout)
        {
            cancellation.ThrowIfCancellationRequested();
            var finished = FindNewResult(knownResults);
            if (finished is not null)
            {
                return finished;
            }

            var window = GameProcess.FindMainWindow();
            if (window != IntPtr.Zero)
            {
                gameSeen = true;
                StartTestFromMenu(window, options.BenchmarkMenuItem, cancellation);
            }
            else if (gameSeen && !GameProcess.IsRunning())
            {
                throw new InvalidOperationException("Бенчмарк закрылся, не записав результат.");
            }
            else if (!gameSeen && elapsed.Elapsed > LaunchTimeout)
            {
                throw new TimeoutException(
                    "Бенчмарк не запустился. Проверьте, что Steam запущен и Benchmark Tool установлен.");
            }

            Pause(StepInterval, cancellation);
        }

        throw new TimeoutException(
            $"Бенчмарк не завершился за {options.RunTimeout.TotalMinutes:0} мин. " +
            "Если тест не стартовал из меню, подберите координаты ключом --menu-point.");
    }

    /// <summary>
    /// Splash screen -> main menu -> confirmation dialog. The sequence needs no screen recognition:
    /// while the test itself runs, Enter and left click are ignored, so repeating it is harmless.
    /// </summary>
    private static void StartTestFromMenu(IntPtr window, MenuPoint benchmarkItem, CancellationToken cancellation)
    {
        if (!NativeInput.TryActivate(window))
        {
            return;
        }

        NativeInput.TapKey(NativeInput.ScanEnter);
        Pause(InputPause, cancellation);
        NativeInput.Click(window, benchmarkItem);
        Pause(InputPause, cancellation);
        NativeInput.TapKey(NativeInput.ScanEnter);
    }

    private static ImmutableHashSet<string> ExistingResults()
    {
        Directory.CreateDirectory(ResultsDirectory);
        return Directory.EnumerateFiles(ResultsDirectory).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static BenchmarkRun? FindNewResult(ImmutableHashSet<string> knownResults)
    {
        var settledBefore = DateTime.UtcNow - ResultSettleTime;
        return Directory.EnumerateFiles(ResultsDirectory)
            .Where(path => !knownResults.Contains(path))
            .Select(path => new FileInfo(path))
            .Where(file => file.LastWriteTimeUtc <= settledBefore)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => TryRead(file.FullName))
            .FirstOrDefault(run => run is not null);
    }

    private static BenchmarkRun? TryRead(string path)
    {
        try
        {
            var result = BenchmarkResult.TryParse(File.ReadAllText(path));
            return result is null ? null : new BenchmarkRun(result, path);
        }
        catch (IOException)
        {
            // The game still holds the file open; it is picked up on the next poll.
            return null;
        }
    }

    private static void Pause(TimeSpan duration, CancellationToken cancellation)
    {
        if (cancellation.WaitHandle.WaitOne(duration))
        {
            cancellation.ThrowIfCancellationRequested();
        }
    }
}
