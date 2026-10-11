using System.ComponentModel;
using System.Diagnostics;

namespace WukongBench.Automation;

internal static class GameProcess
{
    private const string GameProcessName = "b1-Win64-Shipping";
    private const string LauncherProcessName = "b1_benchmark";
    private const int ExitWaitMilliseconds = 10_000;

    public static bool IsRunning() => WithProcesses(GameProcessName, processes => processes.Length > 0);

    /// <returns>The game's main window, or <see cref="IntPtr.Zero"/> while it has none.</returns>
    public static IntPtr FindMainWindow() => WithProcesses(
        GameProcessName,
        processes => processes.Select(MainWindowOf).FirstOrDefault(handle => handle != IntPtr.Zero));

    /// <summary>
    /// Terminates the game instead of closing it: on a normal exit it rewrites
    /// GameUserSettings.ini, which must stay under this tool's control.
    /// </summary>
    public static void Kill()
    {
        foreach (var name in new[] { GameProcessName, LauncherProcessName })
        {
            WithProcesses(name, processes =>
            {
                foreach (var process in processes)
                {
                    KillOne(process);
                }

                return true;
            });
        }
    }

    private static IntPtr MainWindowOf(Process process)
    {
        try
        {
            return process.MainWindowHandle;
        }
        catch (InvalidOperationException)
        {
            // The process exited between enumeration and the query.
            return IntPtr.Zero;
        }
    }

    private static void KillOne(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(ExitWaitMilliseconds);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception
                                       or AggregateException or NotSupportedException)
        {
            // Either the process exited on its own between enumeration and Kill, which is the outcome
            // we wanted, or part of its tree could not be terminated; the caller's wait loop copes with both.
        }
    }

    private static T WithProcesses<T>(string name, Func<Process[], T> use)
    {
        var processes = Process.GetProcessesByName(name);
        try
        {
            return use(processes);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }
}
