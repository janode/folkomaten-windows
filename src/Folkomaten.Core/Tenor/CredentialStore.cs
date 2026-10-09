using System.Security.Cryptography;
using System.Text.Json;

namespace Folkomaten.Core.Tenor;

/// <summary>
/// Holder legitimasjonen til Maskinporten i en kryptert fil. Nøkkelen er en testnøkkel
/// for testmiljøet til Maskinporten, og den forlater aldri maskinen.
/// </summary>
public sealed class CredentialStore(string filePath, ISecretProtector protector)
{
    /// <summary>Den lagrede legitimasjonen, fullstendig eller ikke, eller <c>null</c> når ingenting leselig er lagret.</summary>
    public MaskinportenCredentials? Read()
    {
        try
        {
            var json = protector.Unprotect(File.ReadAllBytes(filePath));
            // En fil uten de påkrevde feltene deserialiseres til null i medlemmer som ikke er nullable.
            return JsonSerializer.Deserialize<MaskinportenCredentials>(json) is { ClientId: not null, PrivateKeyPem: not null } credentials
                ? credentials
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void Save(MaskinportenCredentials credentials)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, protector.Protect(JsonSerializer.SerializeToUtf8Bytes(credentials)));
    }
}
