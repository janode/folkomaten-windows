using System.Windows.Input;
using Folkomaten.Core.Settings;
using Folkomaten.Platform;

namespace Folkomaten.Tests;

// Bare den rene mappingen testes: ingenting her registrerer en global hotkey eller trenger et vindu.
public class HotkeyTests
{
    private const uint VirtualKeyF = 0x46;

    [Fact]
    public void Given_the_default_should_be_ctrl_alt_f()
    {
        Assert.Equal("Ctrl+Alt+F", Hotkey.Default.Display);
        Assert.Equal(Hotkey.ModControl | Hotkey.ModAlt, Hotkey.Default.Modifiers);
        Assert.Equal(VirtualKeyF, Hotkey.Default.VirtualKey);
    }

    [Theory]
    [InlineData(Hotkey.ModWin | Hotkey.ModShift | Hotkey.ModAlt | Hotkey.ModControl, VirtualKeyF, "Ctrl+Alt+Shift+Win+F")]
    [InlineData(Hotkey.ModWin | Hotkey.ModAlt, VirtualKeyF, "Alt+Win+F")]
    [InlineData(Hotkey.ModShift, VirtualKeyF, "Shift+F")]
    [InlineData(Hotkey.ModControl, 0x30u, "Ctrl+0")] // tallrekken vises som et rent siffer
    [InlineData(Hotkey.ModControl, 0x39u, "Ctrl+9")]
    [InlineData(Hotkey.ModControl, 0x70u, "Ctrl+F1")]
    [InlineData(Hotkey.ModAlt, 0x7Bu, "Alt+F12")]
    public void Given_modifiers_and_a_key_should_display_modifiers_in_a_fixed_order(
        uint modifiers, uint virtualKey, string expected)
    {
        Assert.Equal(expected, new Hotkey(modifiers, virtualKey).Display);
    }

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt, Key.F, Hotkey.ModControl | Hotkey.ModAlt, 0x46u)]
    [InlineData(ModifierKeys.Shift, Key.F5, Hotkey.ModShift, 0x74u)]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Control, Key.D1, Hotkey.ModWin | Hotkey.ModControl, 0x31u)]
    [InlineData(
        ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows,
        Key.A,
        Hotkey.ModControl | Hotkey.ModAlt | Hotkey.ModShift | Hotkey.ModWin,
        0x41u)]
    [InlineData(ModifierKeys.None, Key.F, 0u, 0x46u)]
    public void Given_a_key_press_should_map_to_win32_modifier_flags_and_virtual_key(
        ModifierKeys modifiers, Key key, uint expectedModifiers, uint expectedVirtualKey)
    {
        var hotkey = Hotkey.FromKeyPress(modifiers, key);

        Assert.Equal(expectedModifiers, hotkey.Modifiers);
        Assert.Equal(expectedVirtualKey, hotkey.VirtualKey);
    }

    [Fact]
    public void Given_no_setting_should_use_the_default()
    {
        Assert.Equal(Hotkey.Default, Hotkey.FromSetting(null));
    }

    [Fact]
    public void Given_a_hotkey_should_round_trip_through_the_setting()
    {
        var hotkey = Hotkey.FromKeyPress(ModifierKeys.Control | ModifierKeys.Shift, Key.F9);

        var setting = hotkey.ToSetting();

        Assert.Equal(new HotkeySetting(Hotkey.ModControl | Hotkey.ModShift, 0x78), setting);
        Assert.Equal(hotkey, Hotkey.FromSetting(setting));
    }
}
