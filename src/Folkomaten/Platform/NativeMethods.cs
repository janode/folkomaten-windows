using System.Runtime.InteropServices;

namespace Folkomaten.Platform;

internal static class NativeMethods
{
    public const int WmHotkey = 0x0312;
    public const uint ModNoRepeat = 0x4000;

    public const int DwmwaWindowCornerPreference = 33;
    public const int DwmwcpRound = 2;

    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// Bringer et vindu til forgrunnen selv når forgrunnslåsen i Windows ville nektet det.
    /// Retten til å ta forgrunnen tilhører prosessen som mottok den siste
    /// inndatahendelsen. En hotkey gir den, men tasteslippene som følger gir den tilbake til appen
    /// brukeren var i, og en treg første visning taper det kappløpet. En tom inndatahendelse fra denne
    /// prosessen tar retten tilbake.
    /// </summary>
    public static void ForceForeground(IntPtr hWnd)
    {
        Input[] emptyInput = [new Input { Type = InputMouse }];
        _ = SendInput(1, emptyInput, Marshal.SizeOf<Input>());
        SetForegroundWindow(hWnd);
    }

    private const uint InputMouse = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int valueSize);
}
