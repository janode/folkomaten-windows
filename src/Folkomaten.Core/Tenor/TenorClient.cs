using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Folkomaten.Core.Tenor;

/// <summary>Søker i folkeregisterkilden (<c>freg</c>) i Skatteetatens testdatasøk Tenor.</summary>
public sealed class TenorClient(HttpClient httpClient, MaskinportenClient maskinporten, TimeProvider timeProvider)
{
    private const string SearchUrl = "https://testdata.api.skatteetaten.no/api/testnorge/v2/soek/freg";
    private const string Scope = "skatteetaten:testnorge/testdata.read";
    private const string Query = "personstatus:bosatt and identifikatorType:foedselsnummer";
    private const int MaximumPageSize = 100;

    private static readonly CultureInfo Norwegian = CultureInfo.GetCultureInfo("nb-NO");

    /// <summary>
    /// Henter <paramref name="count"/> testpersoner. Døde og ikke-bosatte personer
    /// utelates av spørringen på serversiden; D-nummer og mindreårige filtreres bort her, så
    /// hver returnert bruker kan bestilles som aktiv BankID-testbruker. Det bes om dobbelt
    /// så mange, så antallet holder etter filtreringen.
    /// </summary>
    /// <exception cref="TenorException">Tenor svarte med en feil eller uten brukbare personer.</exception>
    /// <exception cref="MaskinportenException">Det var ikke mulig å hente et tilgangstoken.</exception>
    public async Task<IReadOnlyList<TestUser>> FetchUsers(int count, CancellationToken cancellationToken)
    {
        var token = await maskinporten.AccessToken(Scope, cancellationToken);
        var requested = Math.Min(count * 2, MaximumPageSize);

        // Uten "vis" returnerer freg-søket bare metadata-id-en.
        var url = $"{SearchUrl}?antall={requested}&kql={Uri.EscapeDataString(Query)}&vis=fornavn&vis=etternavn&vis=id";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new TenorException($"Tenor svarte med feil {(int)response.StatusCode}: {body}");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        IReadOnlyList<TestUser> users =
        [
            .. ParseDocuments(body)
                .Select(ToTestUser)
                .OfType<TestUser>()
                .Where(user => TestUserFilter.IsOrderable(user.Fnr, today))
                .Take(count),
        ];

        return users.Count > 0
            ? users
            : throw new TenorException("Tenor returnerte ingen testbrukere. Prøv igjen eller reduser antallet.");
    }

    private static IReadOnlyList<Person> ParseDocuments(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<SearchResult>(body)?.Documents ?? [];
        }
        catch (JsonException)
        {
            throw new TenorException("Tenor svarte med noe annet enn forventet. Prøv igjen senere.");
        }
    }

    private static TestUser? ToTestUser(Person person)
    {
        if (string.IsNullOrEmpty(person.Id))
        {
            return null;
        }

        var firstName = Capitalize(person.FirstName);
        var lastName = Capitalize(person.LastName);
        var fullName = string.Join(' ', new[] { firstName, lastName }.Where(name => name.Length > 0));
        return new TestUser(person.Id, fullName.Length > 0 ? fullName : person.Id, lastName, firstName);
    }

    // Tenor returnerer navn med store bokstaver. ToTitleCase lar ord med bare store bokstaver stå, derfor små bokstaver først.
    private static string Capitalize(string? name) =>
        Norwegian.TextInfo.ToTitleCase((name ?? "").ToLower(Norwegian));

    private sealed record SearchResult(
        [property: JsonPropertyName("dokumentListe")] IReadOnlyList<Person>? Documents);

    /// <summary>Et freg-dokument slik det returneres for <c>vis=fornavn,etternavn,id</c>: feltene ligger flatt på dokumentet.</summary>
    private sealed record Person(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("fornavn")] string? FirstName,
        [property: JsonPropertyName("etternavn")] string? LastName);
}

/// <summary>En Tenor-feil med en norsk melding som kan vises til brukeren.</summary>
public sealed class TenorException(string message) : Exception(message);
