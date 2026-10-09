using Folkomaten.Core;

namespace Folkomaten.Tests;

public class TestUserFilterTests
{
    // Fast referansedato (2026-06-22) så aldersberegningen er deterministisk.
    private static readonly DateOnly ReferenceDate = new(2026, 6, 22);

    // Et D-nummer kjennes igjen på at dagen (de to første sifrene) har fått 40 lagt til.
    [Theory]
    [InlineData("51887000763", true)] // Åpen Biografi, dag 51 -> 11
    [InlineData("59867600781", true)] // Engasjert Vare, dag 59 -> 19
    [InlineData("07877999402", false)] // Gåen Sandkasse, dag 07
    [InlineData("05869797539", false)] // Eventyrlig Kusine, dag 05
    public void Given_fnr_should_detect_d_numbers_by_the_day_offset(string fnr, bool expected)
    {
        Assert.Equal(expected, TestUserFilter.IsDNumber(fnr));
    }

    // Alderen utledes fra fødselsnummeret på referansedatoen.
    [Theory]
    [InlineData("05869797539", 29)] // 05.06.1997
    [InlineData("01811550100", 11)] // 01.01.2015 (konstruert mindreårig)
    public void Given_fnr_should_compute_age_on_the_reference_date(string fnr, int expected)
    {
        Assert.Equal(expected, TestUserFilter.Age(fnr, ReferenceDate));
    }

    // Født 29.02.2000 (individnummer 500 og år 00 gir 2000-tallet): uten en 29. februar i
    // referanseåret faller bursdagen på 1. mars, så 28. februar har ikke passert den enda.
    [Theory]
    [InlineData("29020050000", "2026-02-27", 25)]
    [InlineData("29020050000", "2026-02-28", 25)]
    [InlineData("29020050000", "2026-03-01", 26)]
    [InlineData("29020050000", "2028-02-28", 27)]
    [InlineData("29020050000", "2028-02-29", 28)]
    public void Given_a_leap_day_birth_date_should_count_the_birthday_from_1_march_in_a_non_leap_year(
        string fnr, string referenceDate, int expected)
    {
        Assert.Equal(expected, TestUserFilter.Age(fnr, DateOnly.Parse(referenceDate)));
    }

    // D-nummer og mindreårige kan ikke bestilles; ordinære voksne kan.
    [Theory]
    [InlineData("05869797539", true)] // ordinært nummer, voksen
    [InlineData("51887000763", false)] // D-nummer, ellers voksen
    [InlineData("01811550100", false)] // mindreårig (født 2015)
    public void Given_fnr_should_order_only_adults_with_an_ordinary_number(string fnr, bool expected)
    {
        Assert.Equal(expected, TestUserFilter.IsOrderable(fnr, ReferenceDate));
    }

    // Ingen innebygd eksempelbruker er et D-nummer nå som listen er ryddet, og alle er voksne.
    [Fact]
    public void Given_embedded_users_should_all_be_orderable()
    {
        var notOrderable = TestUserStore.EmbeddedUsers()
            .Where(user => !TestUserFilter.IsOrderable(user.Fnr, ReferenceDate))
            .Select(user => $"{user.Fnr} ({user.FullName})");

        Assert.Empty(notOrderable);
    }
}
