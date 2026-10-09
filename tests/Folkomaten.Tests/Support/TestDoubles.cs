using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Folkomaten.Core.Settings;
using Folkomaten.Core.Tenor;

namespace Folkomaten.Tests.Support;

/// <summary>
/// Holder innstillingene i minnet, men serialiserer ved <see cref="Save"/> slik at en test bare kan se
/// data som faktisk ble lagret, slik den ville etter en omstart av appen (se <see cref="Reopen"/>).
/// </summary>
internal sealed class InMemorySettingsStore : ISettingsStore
{
    private string? _saved;

    public InMemorySettingsStore()
        : this(new AppSettings())
    {
    }

    private InMemorySettingsStore(AppSettings current)
    {
        Current = current;
    }

    public AppSettings Current { get; }

    public int SaveCount { get; private set; }

    public void Save()
    {
        SaveCount++;
        _saved = JsonSerializer.Serialize(Current);
    }

    /// <summary>Et nytt lager med det en fersk prosess ville lest tilbake: bare de lagrede dataene.</summary>
    public InMemorySettingsStore Reopen() =>
        new(_saved is null ? new AppSettings() : JsonSerializer.Deserialize<AppSettings>(_saved)!);
}

/// <summary>Snur bytene: reverserbart uten Windows DPAPI, og synlig ikke klartekst.</summary>
internal sealed class FakeSecretProtector : ISecretProtector
{
    public bool FailOnUnprotect { get; set; }

    public byte[] Protect(byte[] plaintext) => [.. plaintext.Reverse()];

    public byte[] Unprotect(byte[] protectedData) =>
        FailOnUnprotect
            ? throw new CryptographicException("The data cannot be decrypted by this user.")
            : [.. protectedData.Reverse()];
}

/// <summary>En forespørsel slik den falske handleren så den. Innholdet leses mens forespørselen er underveis.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri RequestUri,
    string? Authorization,
    string? ContentType,
    string Body);

internal sealed class FakeHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            body);
        Requests.Add(recorded);
        return respond(recorded);
    }
}

/// <summary>En klokke testen flytter for hånd. Lokal tid er lik UTC, så datoer ikke avhenger av maskinen.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan duration) => _now += duration;
}

/// <summary>En unik midlertidig mappe, slettet ved dispose. Rører aldri den ekte <c>%APPDATA%</c>.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "folkomaten-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

/// <summary>En engangs RSA-nøkkel, generert én gang per testkjøring.</summary>
internal static class TestRsaKey
{
    private static readonly RSA Key = RSA.Create(2048);

    public static string PrivateKeyPem { get; } = Key.ExportPkcs8PrivateKeyPem();

    public static RSA PublicKey { get; } = RSA.Create(Key.ExportParameters(includePrivateParameters: false));
}

/// <summary>En kompakt JWT plukket fra hverandre for assertions.</summary>
internal sealed record JwtParts(JsonElement Header, JsonElement Payload, byte[] Signature, string SigningInput)
{
    public static JwtParts Parse(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException($"Expected a compact JWT with 3 segments, got {parts.Length}.");
        }

        return new JwtParts(
            Decode(parts[0]),
            Decode(parts[1]),
            System.Buffers.Text.Base64Url.DecodeFromChars(parts[2]),
            $"{parts[0]}.{parts[1]}");
    }

    private static JsonElement Decode(string segment) =>
        JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(segment)).RootElement;
}
