using System.Text;

namespace Folkomaten.Core;

/// <summary>
/// Genererer syntetiske BankID-testbrukere: gyldige Tenor-fødselsnummer (måned + 80
/// og riktige mod-11-kontrollsifre) med tilfeldige norske navn, og serialiserer
/// brukere til filformatet i BankID preprods portal for massebestilling.
/// </summary>
public static class TestUserGenerator
{
    private const int SyntheticMonthOffset = 80;

    private static readonly int[] FirstControlWeights = [3, 7, 6, 1, 8, 9, 4, 5, 2];
    private static readonly int[] SecondControlWeights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    private static readonly string[] FirstNames =
    [
        "Frode", "Espen", "Dag", "Gunhild", "Bjørn", "Knut", "Roar", "Else", "Wenche",
        "Kari", "Astrid", "Leif", "Petter", "Camilla", "Magnus", "Geir", "Per", "Tone",
        "Rune", "Nora", "Stein", "Erik", "Trygve", "Anette", "Ola", "Berit", "Ida",
        "Silje", "Kjersti", "Hanne", "Mette", "Liv", "Solveig", "Arne", "Inger", "Marit",
    ];

    private static readonly string[] LastNames =
    [
        "Aas", "Næss", "Sæther", "Hansen", "Iversen", "Johnsen", "Eriksen", "Pedersen",
        "Karlsen", "Bø", "Kristoffersen", "Sørensen", "Gundersen", "Vik", "Kristiansen",
        "Strøm", "Lien", "Isaksen", "Nilsen", "Haugen", "Wold", "Henriksen", "Solberg",
        "Tangen", "Berg", "Larsen", "Johansen", "Andreassen", "Olsen", "Dahl",
    ];

    /// <summary>Lager <paramref name="count"/> unike, tilfeldige testbrukere.</summary>
    public static IReadOnlyList<TestUser> Generate(int count)
    {
        List<TestUser> users = [];
        HashSet<string> seen = [];
        while (users.Count < count)
        {
            if (RandomFnr() is not { } fnr || !seen.Add(fnr))
            {
                continue;
            }

            var firstName = FirstNames[Random.Shared.Next(FirstNames.Length)];
            var lastName = LastNames[Random.Shared.Next(LastNames.Length)];
            users.Add(new TestUser(fnr, $"{firstName} {lastName}", lastName, firstName));
        }

        return users;
    }

    /// <summary>Serialiserer til filformatet for BankID preprod: kommaseparert, UTF-16 med BOM, CRLF.</summary>
    public static byte[] FileData(IEnumerable<TestUser> users)
    {
        var text = string.Concat(users.Select(user => Line(user) + "\r\n"));
        return [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];
    }

    internal static string Line(TestUser user) =>
        $"{user.Fnr},{user.FullName},{user.LastName},{user.FirstName}";

    private static string? RandomFnr()
    {
        var year = Random.Shared.Next(1945, 2005);
        var month = Random.Shared.Next(1, 13);
        // Alltid en gyldig dato, uansett måned.
        var day = Random.Shared.Next(1, 29);
        var twoDigitYear = year % 100;

        // Individnummeret koder århundret (se BirthdateParser).
        var individualNumber = year < 2000 ? Random.Shared.Next(0, 500) : Random.Shared.Next(500, 1000);
        var storedMonth = month + SyntheticMonthOffset;

        List<int> digits =
        [
            day / 10, day % 10,
            storedMonth / 10, storedMonth % 10,
            twoDigitYear / 10, twoDigitYear % 10,
            individualNumber / 100, individualNumber / 10 % 10, individualNumber % 10,
        ];

        if (ControlDigit(digits, FirstControlWeights) is not { } first)
        {
            return null;
        }

        digits.Add(first);
        if (ControlDigit(digits, SecondControlWeights) is not { } second)
        {
            return null;
        }

        digits.Add(second);
        return string.Concat(digits);
    }

    /// <summary>
    /// Mod-11-kontrollsiffer. Returnerer <c>null</c> når det ville blitt 10: et slikt nummer
    /// er ugyldig, og kalleren prøver igjen med andre sifre.
    /// </summary>
    private static int? ControlDigit(IReadOnlyList<int> digits, int[] weights)
    {
        var sum = weights.Select((weight, index) => weight * digits[index]).Sum();
        var remainder = sum % 11;
        var control = remainder == 0 ? 0 : 11 - remainder;
        return control == 10 ? null : control;
    }
}
