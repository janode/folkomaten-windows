using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Folkomaten.Core.Tenor;
using Folkomaten.Tests.Support;

namespace Folkomaten.Tests;

public class MaskinportenClientTests
{
    private const string ClientId = "client-1";
    private const string Scope = "skatteetaten:testnorge/testdata.read";

    private static readonly DateTimeOffset Start = new(2026, 6, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Start);

    [Fact]
    public void Given_a_kid_should_build_a_signed_assertion_with_the_expected_header_and_claims()
    {
        var client = CreateClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException()), kid: "key-1");

        var jwt = JwtParts.Parse(client.BuildAssertion(Scope));

        Assert.Equal("RS256", jwt.Header.GetProperty("alg").GetString());
        Assert.Equal("JWT", jwt.Header.GetProperty("typ").GetString());
        Assert.Equal("key-1", jwt.Header.GetProperty("kid").GetString());

        var iat = jwt.Payload.GetProperty("iat").GetInt64();
        Assert.Equal(Start.ToUnixTimeSeconds(), iat);
        Assert.Equal(iat + 120, jwt.Payload.GetProperty("exp").GetInt64());
        Assert.Equal(ClientId, jwt.Payload.GetProperty("iss").GetString());
        Assert.Equal("https://test.maskinporten.no/", jwt.Payload.GetProperty("aud").GetString());
        Assert.Equal(Scope, jwt.Payload.GetProperty("scope").GetString());
        Assert.True(Guid.TryParse(jwt.Payload.GetProperty("jti").GetString(), out _));

        Assert.True(TestRsaKey.PublicKey.VerifyData(
            Encoding.ASCII.GetBytes(jwt.SigningInput),
            jwt.Signature,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Given_no_kid_should_leave_it_out_of_the_header(string? kid)
    {
        var client = CreateClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException()), kid);

        var jwt = JwtParts.Parse(client.BuildAssertion(Scope));

        Assert.False(jwt.Header.TryGetProperty("kid", out _));
    }

    [Fact]
    public void Given_two_assertions_should_use_a_fresh_jti_each()
    {
        var client = CreateClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException()));

        var first = JwtParts.Parse(client.BuildAssertion(Scope)).Payload.GetProperty("jti").GetString();
        var second = JwtParts.Parse(client.BuildAssertion(Scope)).Payload.GetProperty("jti").GetString();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task Given_an_access_token_request_should_post_the_assertion_as_a_jwt_bearer_form()
    {
        var handler = TokenHandler("token-1", expiresIn: 120);
        var client = CreateClient(handler);

        var token = await client.AccessToken(Scope, TestContext.Current.CancellationToken);

        Assert.Equal("token-1", token);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://test.maskinporten.no/token", request.RequestUri.ToString());
        Assert.Equal("application/x-www-form-urlencoded", request.ContentType);

        var form = HttpUtility.ParseQueryString(request.Body);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", form["grant_type"]);
        var jwt = JwtParts.Parse(form["assertion"]!);
        Assert.Equal(Scope, jwt.Payload.GetProperty("scope").GetString());
        Assert.Equal(ClientId, jwt.Payload.GetProperty("iss").GetString());
    }

    [Fact]
    public async Task Given_a_cached_token_should_reuse_it_until_within_ten_seconds_of_expiry()
    {
        var tokens = new Queue<string>(["token-1", "token-2"]);
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, $$"""{"access_token":"{{tokens.Dequeue()}}","expires_in":120}"""));
        var client = CreateClient(handler);
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal("token-1", await client.AccessToken(Scope, cancellationToken));

        // 20 sekunder igjen: mer enn marginen på 10 sekunder, så det mellomlagrede tokenet gjenbrukes.
        _time.Advance(TimeSpan.FromSeconds(100));
        Assert.Equal("token-1", await client.AccessToken(Scope, cancellationToken));
        Assert.Single(handler.Requests);

        // 9 sekunder igjen: innenfor marginen, så et nytt token hentes.
        _time.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal("token-2", await client.AccessToken(Scope, cancellationToken));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Given_a_non_success_answer_should_throw_a_maskinporten_exception()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest, """{"error":"invalid_client"}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<MaskinportenException>(
            () => client.AccessToken(Scope, TestContext.Current.CancellationToken));

        Assert.Contains("400", exception.Message);
    }

    [Theory]
    [InlineData("<html>Bad gateway</html>")]
    [InlineData("{}")]
    [InlineData("""{"access_token":"","expires_in":120}""")]
    [InlineData("null")]
    public async Task Given_a_success_answer_without_a_token_should_throw_a_maskinporten_exception(string body)
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, body));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<MaskinportenException>(
            () => client.AccessToken(Scope, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_an_invalid_pem_should_throw_a_maskinporten_exception_without_calling_maskinporten()
    {
        var handler = TokenHandler("token-1", expiresIn: 120);
        var client = CreateClient(handler, privateKeyPem: "this is not a PEM key");

        await Assert.ThrowsAsync<MaskinportenException>(
            () => client.AccessToken(Scope, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    private static FakeHttpMessageHandler TokenHandler(string token, int expiresIn) =>
        new(_ => FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, $$"""{"access_token":"{{token}}","expires_in":{{expiresIn}}}"""));

    private MaskinportenClient CreateClient(
        FakeHttpMessageHandler handler, string? kid = "key-1", string? privateKeyPem = null) =>
        new(
            new HttpClient(handler),
            new MaskinportenCredentials(ClientId, privateKeyPem ?? TestRsaKey.PrivateKeyPem, kid),
            _time);
}
