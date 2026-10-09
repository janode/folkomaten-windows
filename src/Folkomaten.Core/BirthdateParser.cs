namespace Folkomaten.Core;

/// <summary>
/// Utleder fødselsdatoen fra et norsk fødselsnummer, inkludert de
/// syntetiske testnumrene fra BankID preprod og Skatteetatens Tenor.
/// </summary>
/// <remarks>
/// Oppbygningen er <c>DDMMYYiiikk</c>. D-nummer legger 40 til dagen. Syntetiske testnumre
/// legger 80 til måneden, og H-nummer legger 40. Århundret følger av individnummeret
/// (siffer 7-9) etter Skatteetatens regler; tvetydige kombinasjoner,
/// som er vanlige i syntetiske numre, faller tilbake til 1900-tallet, noe som gjengir
/// fødselsdatoene i uttrekkene fra BankID preprod.
/// </remarks>
public static class BirthdateParser
{
    private const int FnrLength = 11;
    private const int DNumberDayOffset = 40;
    private const int SyntheticMonthOffset = 80;
    private const int HNumberMonthOffset = 40;

    public static DateOnly? BirthDate(string fnr)
    {
        int[] digits = [.. fnr.Where(char.IsAsciiDigit).Select(c => c - '0')];
        if (digits.Length != FnrLength)
        {
            return null;
        }

        var day = digits[0] * 10 + digits[1];
        var month = digits[2] * 10 + digits[3];
        var twoDigitYear = digits[4] * 10 + digits[5];
        var individualNumber = digits[6] * 100 + digits[7] * 10 + digits[8];

        if (day > DNumberDayOffset)
        {
            day -= DNumberDayOffset;
        }

        if (month > SyntheticMonthOffset)
        {
            month -= SyntheticMonthOffset;
        }
        else if (month > HNumberMonthOffset)
        {
            month -= HNumberMonthOffset;
        }

        var year = individualNumber switch
        {
            <= 499 => 1900 + twoDigitYear,
            <= 749 when twoDigitYear >= 54 => 1800 + twoDigitYear,
            _ when twoDigitYear <= 39 => 2000 + twoDigitYear,
            _ => 1900 + twoDigitYear,
        };

        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        return new DateOnly(year, month, day);
    }
}
