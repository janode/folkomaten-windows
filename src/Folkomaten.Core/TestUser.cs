using System.Globalization;

namespace Folkomaten.Core;

/// <summary>
/// En BankID-testbruker, lest fra en kommaseparert linje:
/// <c>fnr,fullt navn,etternavn,fornavn</c>.
/// </summary>
public sealed record TestUser(string Fnr, string FullName, string LastName, string FirstName)
{
    private const int FnrLength = 11;
    private const int FieldCount = 4;

    /// <summary>Fødselsdato utledet fra fødselsnummeret.</summary>
    public DateOnly? BirthDate => BirthdateParser.BirthDate(Fnr);

    /// <summary>Fødselsdato som <c>dd.MM.yyyy</c>, eller en strek når den ikke kan utledes.</summary>
    public string BirthDateFormatted =>
        BirthDate?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "—";

    /// <summary>
    /// Tolker én linje. Returnerer <c>null</c> for blanke linjer og linjer uten et
    /// 11-sifret nummer i det første feltet.
    /// </summary>
    public static TestUser? TryParseLine(string line)
    {
        var parts = line.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < FieldCount)
        {
            return null;
        }

        var fnr = parts[0];
        if (fnr.Length != FnrLength || !fnr.All(char.IsAsciiDigit))
        {
            return null;
        }

        return new TestUser(fnr, parts[1], parts[2], parts[3]);
    }
}
