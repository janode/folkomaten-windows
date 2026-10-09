using System.Text;
using Folkomaten.Core;

namespace Folkomaten.Tests;

public class TestUserGeneratorTests
{
    private static readonly int[] FirstControlWeights = [3, 7, 6, 1, 8, 9, 4, 5, 2];
    private static readonly int[] SecondControlWeights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    [Fact]
    public void Given_a_count_should_generate_exactly_that_many_users()
    {
        Assert.Empty(TestUserGenerator.Generate(0));
        Assert.Equal(37, TestUserGenerator.Generate(37).Count);
    }

    [Fact]
    public void Given_generated_users_should_have_valid_parseable_numbers()
    {
        var users = TestUserGenerator.Generate(200);

        foreach (var user in users)
        {
            Assert.Equal(11, user.Fnr.Length);
            Assert.True(user.Fnr.All(char.IsAsciiDigit), $"Not all digits: {user.Fnr}");
            Assert.True(HasValidControlDigits(user.Fnr), $"Invalid control digit: {user.Fnr}");
            Assert.NotNull(user.BirthDate);
        }
    }

    [Fact]
    public void Given_generated_users_should_have_unique_numbers()
    {
        var users = TestUserGenerator.Generate(300);

        Assert.Equal(users.Count, users.Select(user => user.Fnr).Distinct().Count());
    }

    [Fact]
    public void Given_users_should_round_trip_through_the_preprod_file_format()
    {
        var users = TestUserGenerator.Generate(25);
        var data = TestUserGenerator.FileData(users);

        // UTF-16 BOM (little-endian: FF FE) først.
        Assert.Equal([0xFF, 0xFE], data.Take(2));

        using var reader = new StreamReader(new MemoryStream(data), Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        var parsed = reader.ReadToEnd()
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(TestUser.TryParseLine)
            .OfType<TestUser>()
            .ToList();

        Assert.Equal(users.Count, parsed.Count);
        Assert.Equal(users.Select(user => user.Fnr), parsed.Select(user => user.Fnr));
        Assert.Equal(users[0].FullName, parsed[0].FullName);
    }

    // Validerer et fødselsnummer ved å regne ut de to mod-11-kontrollsifrene på nytt.
    private static bool HasValidControlDigits(string fnr)
    {
        int[] digits = [.. fnr.Where(char.IsAsciiDigit).Select(c => c - '0')];
        if (digits.Length != 11)
        {
            return false;
        }

        return Control(digits[..9], FirstControlWeights) == digits[9]
            && Control(digits[..10], SecondControlWeights) == digits[10];
    }

    private static int? Control(int[] digits, int[] weights)
    {
        var remainder = digits.Zip(weights, (digit, weight) => digit * weight).Sum() % 11;
        var control = remainder == 0 ? 0 : 11 - remainder;
        return control == 10 ? null : control;
    }
}
