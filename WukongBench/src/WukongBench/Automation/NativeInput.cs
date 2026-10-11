using System.Runtime.InteropServices;
using WukongBench.Core.Cli;

namespace WukongBench.Automation;

/// <summary>Win32 input emulation: the Benchmark Tool has no command line for starting a run.</summary>
internal static class NativeInput
{
    public const ushort ScanEnter = 0x1C;

    private const ushort ScanAlt = 0x38;
    private const double ReferenceHeight = 1080.0;
    private const int KeyHoldMilliseconds = 60;

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const int ShowWindowRestore = 9;
    private const int SystemMetricScreenHeight = 1;
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    /// <summary>Without this, window coordinates are virtualised on scaled displays and clicks miss.</summary>
    public static void EnableDpiAwareness() => SetProcessDpiAwarenessContext(PerMonitorAwareV2);

    public static int PrimaryScreenHeight() => GetSystemMetrics(SystemMetricScreenHeight);

    public static bool TryActivate(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, ShowWindowRestore);
        }

        if (SetForegroundWindow(window) && GetForegroundWindow() == window)
        {
            return true;
        }

        // Windows only hands over focus to a process that has just received input; an Alt tap satisfies that.
        TapKey(ScanAlt);
        SetForegroundWindow(window);
        return GetForegroundWindow() == window;
    }

    public static void TapKey(ushort scanCode)
    {
        Send(Key(scanCode, KeyEventScanCode));
        Thread.Sleep(KeyHoldMilliseconds);
        Send(Key(scanCode, KeyEventScanCode | KeyEventKeyUp));
    }

    /// <summary>Moves the cursor onto a UI point and left-clicks it. The game UI scales with window height.</summary>
    public static bool Click(IntPtr window, MenuPoint point)
    {
        if (!GetClientRect(window, out var client) || client.Bottom <= 0)
        {
            return false;
        }

        var scale = client.Bottom / ReferenceHeight;
        var target = new Point { X = (int)Math.Round(point.X * scale), Y = (int)Math.Round(point.Y * scale) };
        if (!ClientToScreen(window, ref target) || !SetCursorPos(target.X, target.Y))
        {
            return false;
        }

        // A real mouse event is needed for the game to notice the hover; SetCursorPos alone is silent.
        Send(Mouse(MouseEventMove, 1));
        Thread.Sleep(KeyHoldMilliseconds);
        Send(Mouse(MouseEventLeftDown, 0));
        Thread.Sleep(KeyHoldMilliseconds);
        Send(Mouse(MouseEventLeftUp, 0));
        return true;
    }

    private static Input Key(ushort scanCode, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInput { ScanCode = scanCode, Flags = flags } },
    };

    private static Input Mouse(uint flags, int deltaX) => new()
    {
        Type = InputMouse,
        Data = new InputUnion { Mouse = new MouseInput { DeltaX = deltaX, Flags = flags } },
    };

    private static void Send(Input input) => SendInput(1, new[] { input }, Marshal.SizeOf<Input>());

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int DeltaX;
        public int DeltaY;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
}
