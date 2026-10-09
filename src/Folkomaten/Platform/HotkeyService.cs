using System.Windows.Interop;

namespace Folkomaten.Platform;

/// <summary>
/// Registrerer én systemvid hotkey, så appen kan åpnes fra en hvilken som helst annen app og
/// uavhengig av tray-ikonet, som Windows ofte gjemmer i overflytsområdet.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private readonly HwndSource _messageWindow;

    public HotkeyService()
    {
        _messageWindow = new HwndSource(new HwndSourceParameters("FolkomatenHotkey")
        {
            ParentWindow = MessageOnlyParent,
            WindowStyle = 0,
        });
        _messageWindow.AddHook(HandleMessage);
    }

    public event EventHandler? Pressed;

    /// <summary>Hotkeyen som er registrert nå, eller <c>null</c> når siste registrering mislyktes.</summary>
    public Hotkey? Current { get; private set; }

    /// <summary>Erstatter den registrerte hotkeyen. Returnerer false når en annen app allerede holder kombinasjonen.</summary>
    public bool Register(Hotkey hotkey)
    {
        Unregister();
        var registered = NativeMethods.RegisterHotKey(
            _messageWindow.Handle, HotkeyId, hotkey.Modifiers | NativeMethods.ModNoRepeat, hotkey.VirtualKey);
        Current = registered ? hotkey : null;
        return registered;
    }

    public void Dispose()
    {
        Unregister();
        _messageWindow.Dispose();
    }

    /// <summary>Frigir hotkeyen, for eksempel mens brukeren tar opp en ny.</summary>
    public void Unregister()
    {
        if (Current is not null)
        {
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
            Current = null;
        }
    }

    private IntPtr HandleMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }
}
