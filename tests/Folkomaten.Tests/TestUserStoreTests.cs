using Folkomaten.Core;
using Folkomaten.Tests.Support;

namespace Folkomaten.Tests;

public sealed class TestUserStoreTests : IDisposable
{
    private const string FixtureFileName = "testbrukere-preprod-utf16.txt";

    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureFileName);

    private static readonly TestUser[] SortingUsers =
    [
        new("10000000001", "Zara Zed", "Zed", "Zara"),
        new("10000000002", "Åse Dahl", "Dahl", "Åse"),
        new("20000000003", "Anna Nilsen", "Nilsen", "Anna"),
        new("30000000004", "Øystein Berg", "Berg", "Øystein"),
        new("40000000005", "Ærlig Hansen", "Hansen", "Ærlig"),
        new("50000000006", "Bjørn Olsen", "Olsen", "Bjørn"),
    ];

    private static readonly string[] AllSortedNames =
        ["Anna Nilsen", "Bjørn Olsen", "Zara Zed", "Ærlig Hansen", "Øystein Berg", "Åse Dahl"];

    private readonly TempDirectory _temp = new();

    public static TheoryData<string, string[]> SearchCases() => new()
    {
        { "anna", ["Anna Nilsen"] },
        { "  ANNA ", ["Anna Nilsen"] },
        { "ærlig", ["Ærlig Hansen"] },
        { "ØYSTEIN", ["Øystein Berg"] },
        { "20000000", ["Anna Nilsen"] },
        { "", AllSortedNames },
        { "nobody", [] },
    };

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Given_clear_should_empty_the_list_and_persist_across_launches()
    {
        var settings = new InMemorySettingsStore();

        var store = new TestUserStore(settings);
        Assert.NotEmpty(store.Users); // innebygde brukere som standard

        store.Clear();
        Assert.Empty(store.Users);

        // En ny oppstart på de samme innstillingene forblir tom.
        var reopened = new TestUserStore(settings.Reopen());
        Assert.Empty(reopened.Users);
    }

    [Fact]
    public void Given_the_source_should_reflect_whether_the_embedded_users_are_shown()
    {
        var store = new TestUserStore(new InMemorySettingsStore());
        Assert.True(store.IsShowingEmbedded); // innebygde brukere som standard

        store.Clear();
        Assert.False(store.IsShowingEmbedded); // en tom liste er ikke eksempelbrukerne

        store.LoadEmbedded();
        Assert.True(store.IsShowingEmbedded);
    }

    [Fact]
    public void Given_load_embedded_after_clear_should_restore_the_users_across_launches()
    {
        var settings = new InMemorySettingsStore();

        var store = new TestUserStore(settings);
        store.Clear();
        store.LoadEmbedded();
        Assert.NotEmpty(store.Users);

        // Valget om å vise eksempelbrukerne overlever også en omstart.
        var reopened = new TestUserStore(settings.Reopen());
        Assert.NotEmpty(reopened.Users);
    }

    // Fixturen er UTF-16LE med BOM og CRLF, slik BankID preprod leverer filene sine.
    // Dekker lesing av ekte byte fra disk; generatortesten dekker bare byte i minnet.
    [Fact]
    public void Given_a_utf16_file_with_bom_on_disk_should_parse_the_users()
    {
        var data = File.ReadAllBytes(FixturePath);
        Assert.Equal([0xFF, 0xFE], data.Take(2));

        var users = TestUserStore.ParseFile(FixturePath);

        Assert.Equal(10, users.Count);
        Assert.Equal("05818697610", users[0].Fnr);

        // Æ, Ø og Å overlever tegnkodingsdeteksjonen.
        Assert.Contains(users, user => user.LastName == "Ostehøvel");
        Assert.Contains(users, user => user.FirstName == "Øvrige");
    }

    [Fact]
    public void Given_load_file_should_show_the_file_and_remember_it_for_the_next_launch()
    {
        var settings = new InMemorySettingsStore();
        var store = new TestUserStore(settings);
        var changed = 0;
        store.Changed += (_, _) => changed++;

        store.LoadFile(FixturePath);

        Assert.Equal(10, store.Users.Count);
        Assert.Equal(FixtureFileName, store.SourceName);
        Assert.False(store.IsShowingEmbedded);
        Assert.Equal(1, changed);

        var reopened = new TestUserStore(settings.Reopen());
        Assert.Equal(10, reopened.Users.Count);
        Assert.Equal(FixtureFileName, reopened.SourceName);
        Assert.False(reopened.IsShowingEmbedded);
    }

    [Fact]
    public void Given_a_remembered_file_that_no_longer_exists_should_fall_back_to_the_embedded_users()
    {
        var settings = new InMemorySettingsStore();
        var path = _temp.File("remembered.txt");
        File.Copy(FixturePath, path);
        new TestUserStore(settings).LoadFile(path);
        File.Delete(path);

        var reopenedSettings = settings.Reopen();
        var reopened = new TestUserStore(reopenedSettings);

        Assert.True(reopened.IsShowingEmbedded);
        Assert.Equal(TestUserStore.EmbeddedUsers().Count, reopened.Users.Count);

        // Den utdaterte stien glemmes for godt, og prøves ikke igjen ved hver oppstart.
        Assert.Null(reopenedSettings.Reopen().Current.LastLoadedFilePath);
    }

    [Fact]
    public void Given_toggle_favorite_should_flip_the_flag_and_persist_it()
    {
        var settings = new InMemorySettingsStore();
        var store = new TestUserStore(settings);
        var user = store.Users[0];
        var changed = 0;
        store.Changed += (_, _) => changed++;

        Assert.False(store.IsFavorite(user));

        store.ToggleFavorite(user);
        Assert.True(store.IsFavorite(user));
        Assert.True(new TestUserStore(settings.Reopen()).IsFavorite(user));

        store.ToggleFavorite(user);
        Assert.False(store.IsFavorite(user));
        Assert.False(new TestUserStore(settings.Reopen()).IsFavorite(user));
        Assert.Equal(2, changed);
    }

    [Fact]
    public void Given_filtered_without_criteria_should_sort_by_name_with_ae_oe_aa_after_z()
    {
        var store = CreateStoreWith(SortingUsers);

        Assert.Equal(AllSortedNames, store.Filtered("", onlyFavorites: false).Select(user => user.FullName));
    }

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void Given_a_search_should_match_name_case_insensitively_and_fnr_by_substring(
        string search, string[] expectedNames)
    {
        var store = CreateStoreWith(SortingUsers);

        Assert.Equal(expectedNames, store.Filtered(search, onlyFavorites: false).Select(user => user.FullName));
    }

    [Fact]
    public void Given_only_favorites_should_return_just_the_favorites_and_still_apply_the_search()
    {
        var store = CreateStoreWith(SortingUsers);
        store.ToggleFavorite(SortingUsers[1]); // Åse Dahl
        store.ToggleFavorite(SortingUsers[2]); // Anna Nilsen

        Assert.Equal(
            ["Anna Nilsen", "Åse Dahl"],
            store.Filtered("", onlyFavorites: true).Select(user => user.FullName));
        Assert.Equal(
            ["Åse Dahl"],
            store.Filtered("åse", onlyFavorites: true).Select(user => user.FullName));
        Assert.Empty(store.Filtered("zara", onlyFavorites: true));
    }

    private TestUserStore CreateStoreWith(IEnumerable<TestUser> users)
    {
        var path = _temp.File("users.txt");
        File.WriteAllLines(path, users.Select(TestUserGenerator.Line));

        var store = new TestUserStore(new InMemorySettingsStore());
        store.LoadFile(path);
        return store;
    }
}
