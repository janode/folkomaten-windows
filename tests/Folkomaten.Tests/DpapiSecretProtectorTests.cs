using System.Security.Cryptography;
using System.Text;
using Folkomaten.Core.Tenor;
using Folkomaten.Platform;
using Folkomaten.Tests.Support;

namespace Folkomaten.Tests;

// Disse kjører mot ekte Windows DPAPI for gjeldende bruker; ingenting skrives utenfor en temp-mappe.
public sealed class DpapiSecretProtectorTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly DpapiSecretProtector _protector = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Given_plaintext_should_protect_to_different_bytes_that_unprotect_to_the_original()
    {
        var plaintext = Encoding.UTF8.GetBytes("a secret with æøå");

        var protectedData = _protector.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedData);
        Assert.Equal(plaintext, _protector.Unprotect(protectedData));
    }

    [Fact]
    public void Given_data_that_was_not_protected_should_throw_a_cryptographic_exception()
    {
        byte[] garbage = [1, 2, 3, 4, 5, 6, 7, 8];

        Assert.ThrowsAny<CryptographicException>(() => _protector.Unprotect(garbage));
    }

    [Fact]
    public void Given_credentials_saved_through_dpapi_should_read_them_back_from_a_file_that_hides_them()
    {
        var path = _temp.File("credentials.bin");
        var credentials = new MaskinportenCredentials("client-1", TestRsaKey.PrivateKeyPem, "key-1");

        new CredentialStore(path, _protector).Save(credentials);

        Assert.Equal(credentials, new CredentialStore(path, new DpapiSecretProtector()).Read());
        Assert.DoesNotContain("client-1", Encoding.UTF8.GetString(File.ReadAllBytes(path)));
    }
}
