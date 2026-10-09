using Folkomaten.Core.Settings;
using Folkomaten.Tests.Support;

namespace Folkomaten.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Given_saved_settings_should_round_trip_through_the_file()
    {
        // En mappe som ikke finnes enda: Save oppretter den.
        var path = Path.Combine(_temp.Path, "nested", "settings.json");
        var store = new JsonSettingsStore(path);
        store.Current.FavoriteFnrs.Add("05869797539");
        store.Current.FavoriteFnrs.Add("04869248709");
        store.Current.LastLoadedFilePath = @"C:\data\users.txt";
        store.Current.UserClearedList = true;
        store.Current.Hotkey = new HotkeySetting(Modifiers: 0x0003, VirtualKey: 0x46);

        store.Save();

        var reloaded = new JsonSettingsStore(path).Current;
        Assert.Equal(["04869248709", "05869797539"], reloaded.FavoriteFnrs.Order());
        Assert.Equal(@"C:\data\users.txt", reloaded.LastLoadedFilePath);
        Assert.True(reloaded.UserClearedList);
        Assert.Equal(new HotkeySetting(0x0003, 0x46), reloaded.Hotkey);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Given_a_missing_file_should_give_defaults()
    {
        var current = new JsonSettingsStore(_temp.File("missing.json")).Current;

        AssertDefaults(current);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    public void Given_a_corrupt_file_should_give_defaults_and_stay_savable(string content)
    {
        var path = _temp.File("settings.json");
        File.WriteAllText(path, content);

        var store = new JsonSettingsStore(path);

        AssertDefaults(store.Current);

        store.Current.UserClearedList = true;
        store.Save();
        Assert.True(new JsonSettingsStore(path).Current.UserClearedList);
    }

    private static void AssertDefaults(AppSettings current)
    {
        Assert.Empty(current.FavoriteFnrs);
        Assert.Null(current.LastLoadedFilePath);
        Assert.False(current.UserClearedList);
        Assert.Null(current.Hotkey);
    }
}
