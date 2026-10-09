namespace Folkomaten.Core.Tenor;

/// <param name="Kid">
/// Nøkkel-id fra selvbetjeningsportalen. Legges i JWT-headeren så Maskinporten finner riktig nøkkel.
/// </param>
public sealed record MaskinportenCredentials(string ClientId, string PrivateKeyPem, string? Kid)
{
    /// <summary>Nok til å be om et token: nøkkel-id er valgfri.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsComplete => ClientId.Length > 0 && PrivateKeyPem.Length > 0;
}
