using System.Text;
using Folkomaten.Core.Tenor;
using Folkomaten.Tests.Support;

namespace Folkomaten.Tests;

public sealed class CredentialStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Given_saved_credentials_should_round_trip_through_the_protector()
    {
        // En mappe som ikke finnes enda: Save oppretter den.
        var path = Path.Combine(_temp.Path, "nested", "credentials.bin");
        var store = new CredentialStore(path, new FakeSecretProtector());
        var credentials = new MaskinportenCredentials("client-1", TestRsaKey.PrivateKeyPem, "key-1");

        store.Save(credentials);

        var read = new CredentialStore(path, new FakeSecretProtector()).Read();
        Assert.Equal(credentials, read);
        Assert.DoesNotContain("client-1", File.ReadAllText(path));
    }

    [Fact]
    public void Given_no_file_should_read_null()
    {
        var store = new CredentialStore(_temp.File("missing.bin"), new FakeSecretProtector());

        Assert.Null(store.Read());
    }

    [Fact]
    public void Given_data_the_user_cannot_decrypt_should_read_null()
    {
        var path = _temp.File("credentials.bin");
        new CredentialStore(path, new FakeSecretProtector())
            .Save(new MaskinportenCredentials("client-1", TestRsaKey.PrivateKeyPem, null));

        var store = new CredentialStore(path, new FakeSecretProtector { FailOnUnprotect = true });

        Assert.Null(store.Read());
    }

    [Fact]
    public void Given_decrypted_data_that_is_not_json_should_read_null()
    {
        var path = _temp.File("credentials.bin");
        var protector = new FakeSecretProtector();
        File.WriteAllBytes(path, protector.Protect(Encoding.UTF8.GetBytes("not json")));

        Assert.Null(new CredentialStore(path, protector).Read());
    }

    // Gyldig JSON som deserialiseres til et objekt med null-medlemmer må ikke nå kallerne som legitimasjon.
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"PrivateKeyPem":"pem"}""")]
    [InlineData("""{"ClientId":"client-1"}""")]
    [InlineData("null")]
    public void Given_decrypted_json_without_the_required_fields_should_read_null(string json)
    {
        var path = _temp.File("credentials.bin");
        var protector = new FakeSecretProtector();
        File.WriteAllBytes(path, protector.Protect(Encoding.UTF8.GetBytes(json)));

        Assert.Null(new CredentialStore(path, protector).Read());
    }

    [Theory]
    [InlineData("client-1", "pem", "key-1", true)]
    [InlineData("client-1", "pem", null, true)] // nøkkel-id er valgfri
    [InlineData("", "pem", "key-1", false)]
    [InlineData("client-1", "", "key-1", false)]
    public void Given_credentials_should_be_complete_when_client_id_and_key_are_present(
        string clientId, string privateKeyPem, string? kid, bool expected)
    {
        Assert.Equal(expected, new MaskinportenCredentials(clientId, privateKeyPem, kid).IsComplete);
    }
}
