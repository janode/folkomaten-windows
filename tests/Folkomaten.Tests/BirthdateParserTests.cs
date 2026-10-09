using System.Globalization;
using Folkomaten.Core;

namespace Folkomaten.Tests;

public class BirthdateParserTests
{
    private static string? Formatted(string fnr) =>
        BirthdateParser.BirthDate(fnr)?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    // Verifisert mot skjermbildet i oppgaven (syntetiske Tenor-nummer, måned + 80).
    [Theory]
    [InlineData("21906977751", "21.10.1969")] // individnummer 777 -> fallback til 1900-tallet
    [InlineData("08925498190", "08.12.1954")]
    [InlineData("25898999575", "25.09.1989")]
    [InlineData("31810849196", "31.01.1908")]
    [InlineData("21836499880", "21.03.1964")]
    public void Given_verified_screenshot_numbers_should_give_the_verified_birth_date(string fnr, string expected)
    {
        Assert.Equal(expected, Formatted(fnr));
    }

    // En av de innebygde eksempelbrukerne.
    [Fact]
    public void Given_embedded_sample_user_should_give_birth_date()
    {
        Assert.Equal("04.06.1992", Formatted("04869248709")); // Frode Aas
    }

    // D-nummer: 40 lagt til dagen.
    [Fact]
    public void Given_d_number_should_subtract_40_from_the_day()
    {
        Assert.Equal("01.05.1999", Formatted("41059912345"));
    }

    // Individnummer 500-999 med år 00-39 -> 2000-tallet.
    [Fact]
    public void Given_individual_number_500_to_999_and_year_00_to_39_should_give_the_2000s()
    {
        Assert.Equal("01.01.2005", Formatted("01010550112"));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefghijk")]
    [InlineData("")]
    [InlineData("01139912345")] // måned 13
    [InlineData("30029912345")] // 30. februar
    [InlineData("00019912345")] // dag 0
    public void Given_invalid_input_should_give_null(string fnr)
    {
        Assert.Null(BirthdateParser.BirthDate(fnr));
    }

    // Alle innebygde testbrukere kan tolkes og gir en gyldig fødselsdato.
    [Fact]
    public void Given_embedded_users_should_all_parse_to_a_birth_date()
    {
        var users = TestUserStore.EmbeddedUsers();

        Assert.Equal(50, users.Count);
        Assert.DoesNotContain(users, user => user.BirthDate is null);
    }
}
