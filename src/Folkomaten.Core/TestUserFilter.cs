namespace Folkomaten.Core;

/// <summary>
/// Avgjør om en testperson kan bestilles som aktiv BankID-testbruker.
/// Utledes fra fødselsnummeret alene, så det er deterministisk: D-nummer
/// avvises (mange testsystemer godtar bare ordinære nummer), og det gjør mindreårige også.
/// Døde og ikke-bosatte personer utelates på serversiden av <see cref="Tenor.TenorClient"/>.
/// </summary>
public static class TestUserFilter
{
    private const int FnrLength = 11;
    private const int DNumberDayOffset = 40;
    private const int DefaultMinimumAge = 18;

    public static bool IsDNumber(string fnr)
    {
        int[] digits = [.. fnr.Where(char.IsAsciiDigit).Select(c => c - '0')];
        return digits.Length == FnrLength && digits[0] * 10 + digits[1] > DNumberDayOffset;
    }

    /// <summary>Alder i hele år på <paramref name="referenceDate"/>, eller <c>null</c> når ingen fødselsdato kan utledes.</summary>
    public static int? Age(string fnr, DateOnly referenceDate)
    {
        if (BirthdateParser.BirthDate(fnr) is not { } birthDate)
        {
            return null;
        }

        var years = referenceDate.Year - birthDate.Year;
        var birthdayPassed = (referenceDate.Month, referenceDate.Day).CompareTo((birthDate.Month, birthDate.Day)) >= 0;
        return birthdayPassed ? years : years - 1;
    }

    public static bool IsOrderable(string fnr, DateOnly referenceDate, int minimumAge = DefaultMinimumAge) =>
        !IsDNumber(fnr) && Age(fnr, referenceDate) >= minimumAge;
}
