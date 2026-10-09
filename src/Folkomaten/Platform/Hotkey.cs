using System.Windows.Input;
using Folkomaten.Core.Settings;

namespace Folkomaten.Platform;

/// <summary>En global snarvei: Win32 <c>MOD_*</c>-flagg pluss en virtual-key-kode.</summary>
internal sealed record Hotkey(uint Modifiers, uint VirtualKey)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    /// <summary>Ctrl+Alt+F: sjelden brukt av andre apper, så den globale snarveien ikke kaprer en kjent kombinasjon.</summary>
    public static readonly Hotkey Default = new(ModControl | ModAlt, VirtualKey: 0x46);

    public string Display
    {
        get
        {
            List<string> parts = [];
            if ((Modifiers & ModControl) != 0) { parts.Add("Ctrl"); }
            if ((Modifiers & ModAlt) != 0) { parts.Add("Alt"); }
            if ((Modifiers & ModShift) != 0) { parts.Add("Shift"); }
            if ((Modifiers & ModWin) != 0) { parts.Add("Win"); }
            parts.Add(KeyName(KeyInterop.KeyFromVirtualKey((int)VirtualKey)));
            return string.Join('+', parts);
        }
    }

    public static Hotkey FromSetting(HotkeySetting? setting) =>
        setting is null ? Default : new Hotkey(setting.Modifiers, setting.VirtualKey);

    public static Hotkey FromKeyPress(ModifierKeys modifiers, Key key)
    {
        uint flags = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) { flags |= ModControl; }
        if (modifiers.HasFlag(ModifierKeys.Alt)) { flags |= ModAlt; }
        if (modifiers.HasFlag(ModifierKeys.Shift)) { flags |= ModShift; }
        if (modifiers.HasFlag(ModifierKeys.Windows)) { flags |= ModWin; }
        return new Hotkey(flags, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public HotkeySetting ToSetting() => new(Modifiers, VirtualKey);

    // Key.D0..D9 er tallrekken; vis dem som rene sifre.
    private static string KeyName(Key key) =>
        key is >= Key.D0 and <= Key.D9 ? ((int)(key - Key.D0)).ToString() : key.ToString();
}
