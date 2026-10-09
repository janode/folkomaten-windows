namespace Folkomaten.Core.Settings;

/// <summary>Alt appen husker mellom oppstarter, bortsett fra legitimasjonen til Maskinporten.</summary>
public sealed class AppSettings
{
    /// <summary>Favoritter lagres per fødselsnummer og gjelder på tvers av datasett.</summary>
    public HashSet<string> FavoriteFnrs { get; set; } = [];

    public string? LastLoadedFilePath { get; set; }

    /// <summary>Brukeren tømte listen med vilje: ikke fall tilbake til de innebygde brukerne.</summary>
    public bool UserClearedList { get; set; }

    /// <summary><c>null</c> betyr standardsnarveien.</summary>
    public HotkeySetting? Hotkey { get; set; }
}

/// <summary>En global snarvei slik Win32 forventer den: <c>MOD_*</c>-flagg og en virtual-key-kode.</summary>
public sealed record HotkeySetting(uint Modifiers, uint VirtualKey);
