using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Folkomaten.Core.Tenor;

/// <summary>Henter Maskinporten-tilgangstokener med JWT bearer grant (RS256), fra testmiljøet.</summary>
public sealed class MaskinportenClient(HttpClient httpClient, MaskinportenCredentials credentials, TimeProvider timeProvider)
{
    private const string TokenUrl = "https://test.maskinporten.no/token";
    private const string Audience = "https://test.maskinporten.no/";
    private const string GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    private const int AssertionLifetimeSeconds = 120;
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(10);

    private (string Token, DateTimeOffset ExpiresAt)? _cached;

    /// <summary>Returnerer et gyldig tilgangstoken, og gjenbruker det mellomlagrede til like før det utløper.</summary>
    /// <exception cref="MaskinportenException">Nøkkelen er ugyldig, eller Maskinporten avviste forespørselen.</exception>
    public async Task<string> AccessToken(string scope, CancellationToken cancellationToken)
    {
        if (_cached is { } cached && cached.ExpiresAt > timeProvider.GetUtcNow() + ExpiryMargin)
        {
            return cached.Token;
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = GrantType,
            ["assertion"] = BuildAssertion(scope),
        });
        using var response = await httpClient.PostAsync(TokenUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MaskinportenException($"Maskinporten svarte med feil {(int)response.StatusCode}: {body}");
        }

        var token = ParseToken(body);
        _cached = (token.AccessToken, timeProvider.GetUtcNow().AddSeconds(token.ExpiresIn));
        return token.AccessToken;
    }

    internal string BuildAssertion(string scope)
    {
        using var key = ImportKey();
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        Dictionary<string, object> header = new() { ["alg"] = "RS256", ["typ"] = "JWT" };
        if (!string.IsNullOrEmpty(credentials.Kid))
        {
            header["kid"] = credentials.Kid;
        }

        Dictionary<string, object> payload = new()
        {
            ["iss"] = credentials.ClientId,
            ["aud"] = Audience,
            ["scope"] = scope,
            ["iat"] = now,
            ["exp"] = now + AssertionLifetimeSeconds,
            ["jti"] = Guid.NewGuid().ToString(),
        };

        var signingInput = $"{Encode(header)}.{Encode(payload)}";
        var signature = key.SignData(
            Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private static TokenResponse ParseToken(string body)
    {
        try
        {
            if (JsonSerializer.Deserialize<TokenResponse>(body) is { AccessToken.Length: > 0 } token)
            {
                return token;
            }
        }
        catch (JsonException)
        {
            // Ikke JSON i det hele tatt, for eksempel en HTML-side fra en proxy: rapporteres nedenfor.
        }

        throw new MaskinportenException("Maskinporten svarte uten token.");
    }

    private static string Encode(Dictionary<string, object> value) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(value));

    private RSA ImportKey()
    {
        var key = RSA.Create();
        try
        {
            key.ImportFromPem(credentials.PrivateKeyPem);
            return key;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            key.Dispose();
            throw new MaskinportenException("Ugyldig privat nøkkel. Forventer en RSA-nøkkel i PEM-format (PKCS#8).");
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>En Maskinporten-feil med en norsk melding som kan vises til brukeren.</summary>
public sealed class MaskinportenException(string message) : Exception(message);
