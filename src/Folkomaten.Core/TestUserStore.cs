using System.Globalization;
using Folkomaten.Core.Settings;

namespace Folkomaten.Core;

/// <summary>
/// Holder testbrukerne og favorittene, og tar seg av innlasting og persistens.
/// Standarddatasettet er den innebygde eksempelfilen. Brukeren kan laste inn andre filer;
/// den siste huskes og lastes inn ved oppstart.
/// </summary>
public sealed class TestUserStore
{
    private const string EmbeddedResourceName = "testbrukere.txt";
    private const string EmbeddedSourceName = "Innebygde testbrukere";
    private const string EmptySourceName = "Ingen testbrukere";

    private static readonly StringComparer NameComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("nb-NO"), ignoreCase: true);

    private readonly ISettingsStore _settingsStore;

    public TestUserStore(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        LoadInitial();
    }

    /// <summary>Utløses etter at brukerne eller favorittene er endret.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<TestUser> Users { get; private set; } = [];

    /// <summary>Kilden som vises i UI-et: et filnavn, eller en fast tekst for den innebygde eller tomme listen.</summary>
    public string SourceName { get; private set; } = "";

    public bool IsShowingEmbedded { get; private set; }

    private AppSettings Settings => _settingsStore.Current;

    public void LoadEmbedded()
    {
        Settings.LastLoadedFilePath = null;
        Settings.UserClearedList = false;
        _settingsStore.Save();
        ShowEmbedded();
    }

    /// <summary>Laster inn brukere fra en fil brukeren valgte, og husker stien til neste oppstart.</summary>
    public void LoadFile(string filePath)
    {
        var loaded = ParseFile(filePath);
        Settings.LastLoadedFilePath = filePath;
        Settings.UserClearedList = false;
        _settingsStore.Save();
        Show(loaded, Path.GetFileName(filePath));
    }

    /// <summary>
    /// Tømmer listen. Valget huskes, så appen starter tom også neste
    /// gang, til brukeren laster inn en fil eller de innebygde brukerne igjen.
    /// </summary>
    public void Clear()
    {
        Settings.LastLoadedFilePath = null;
        Settings.UserClearedList = true;
        _settingsStore.Save();
        Show([], EmptySourceName);
    }

    public bool IsFavorite(TestUser user) => Settings.FavoriteFnrs.Contains(user.Fnr);

    public void ToggleFavorite(TestUser user)
    {
        if (!Settings.FavoriteFnrs.Remove(user.Fnr))
        {
            Settings.FavoriteFnrs.Add(user.Fnr);
        }

        _settingsStore.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Den filtrerte listen for visning, sortert alfabetisk på navn.</summary>
    public IReadOnlyList<TestUser> Filtered(string search, bool onlyFavorites)
    {
        var query = search.Trim();
        return
        [
            .. Users
                .Where(user => !onlyFavorites || IsFavorite(user))
                .Where(user => query.Length == 0
                    || user.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || user.Fnr.Contains(query, StringComparison.Ordinal))
                .OrderBy(user => user.FullName, NameComparer),
        ];
    }

    internal static IReadOnlyList<TestUser> EmbeddedUsers()
    {
        using var stream = typeof(TestUserStore).Assembly.GetManifestResourceStream(EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        return ParseLines(reader.ReadToEnd());
    }

    /// <summary>
    /// Leser en fil med tegnkodingen hentet fra BOM-en (UTF-16 slik BankID
    /// preprod skriver den, ellers UTF-8) og tolker linjene til testbrukere.
    /// </summary>
    internal static IReadOnlyList<TestUser> ParseFile(string filePath) => ParseLines(File.ReadAllText(filePath));

    private static IReadOnlyList<TestUser> ParseLines(string content) =>
    [
        .. content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(TestUser.TryParseLine)
            .OfType<TestUser>(),
    ];

    private void LoadInitial()
    {
        if (Settings.UserClearedList)
        {
            Show([], EmptySourceName);
            return;
        }

        if (Settings.LastLoadedFilePath is { } filePath)
        {
            if (TryParseFile(filePath) is { Count: > 0 } loaded)
            {
                Show(loaded, Path.GetFileName(filePath));
                return;
            }

            // Den sist brukte filen er borte eller tom: fall tilbake til de innebygde brukerne.
            Settings.LastLoadedFilePath = null;
            _settingsStore.Save();
        }

        ShowEmbedded();
    }

    private static IReadOnlyList<TestUser>? TryParseFile(string filePath)
    {
        try
        {
            return ParseFile(filePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ShowEmbedded()
    {
        Show(EmbeddedUsers(), EmbeddedSourceName, isEmbedded: true);
    }

    private void Show(IReadOnlyList<TestUser> users, string sourceName, bool isEmbedded = false)
    {
        Users = users;
        SourceName = sourceName;
        IsShowingEmbedded = isEmbedded;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
