using System.Net;
using System.Text.Json;
using System.Web;
using Folkomaten.Core;
using Folkomaten.Core.Tenor;
using Folkomaten.Tests.Support;

namespace Folkomaten.Tests;

public class TenorClientTests
{
    private const string AdultFnr = "05869797539"; // født 05.06.1997
    private const string DNumber = "51887000763"; // dag 51 -> 11
    private const string MinorFnr = "01811550100"; // født 01.01.2015

    private static readonly DateTimeOffset Start = new(2026, 6, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Start);

    [Theory]
    [InlineData(5, "10")]
    [InlineData(50, "100")]
    [InlineData(60, "100")] // begrenset til maksimal sidestørrelse
    public async Task Given_a_count_should_request_twice_as_many_capped_at_100(int count, string expectedRequested)
    {
        var (client, tenor, maskinporten) = CreateClient(_ => Search(Document(AdultFnr, "KARI", "NORDMANN")));

        await client.FetchUsers(count, TestContext.Current.CancellationToken);

        var request = Assert.Single(tenor.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("testdata.api.skatteetaten.no", request.RequestUri.Host);
        Assert.Equal("/api/testnorge/v2/soek/freg", request.RequestUri.AbsolutePath);

        var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.Equal(expectedRequested, query["antall"]);
        Assert.Equal("personstatus:bosatt and identifikatorType:foedselsnummer", query["kql"]);
        Assert.Equal(["fornavn", "etternavn", "id"], query.GetValues("vis")!);

        Assert.Equal("Bearer token-1", request.Authorization);
        var assertion = JwtParts.Parse(HttpUtility.ParseQueryString(Assert.Single(maskinporten.Requests).Body)["assertion"]!);
        Assert.Equal("skatteetaten:testnorge/testdata.read", assertion.Payload.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task Given_upper_case_names_should_title_case_them()
    {
        var (client, _, _) = CreateClient(_ => Search(
            Document(AdultFnr, "KARI", "NORDMANN"),
            Document("04869248709", "ØYVIND", "ÅSEN")));

        var users = await client.FetchUsers(2, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new TestUser(AdultFnr, "Kari Nordmann", "Nordmann", "Kari"),
                new TestUser("04869248709", "Øyvind Åsen", "Åsen", "Øyvind"),
            ],
            users);
    }

    [Fact]
    public async Task Given_a_person_without_names_should_use_the_fnr_as_the_full_name()
    {
        var (client, _, _) = CreateClient(_ => Search(Document(AdultFnr, null, null)));

        var user = Assert.Single(await client.FetchUsers(1, TestContext.Current.CancellationToken));

        Assert.Equal(AdultFnr, user.FullName);
    }

    [Fact]
    public async Task Given_d_numbers_and_minors_should_drop_them()
    {
        var (client, _, _) = CreateClient(_ => Search(
            Document(DNumber, "DAG", "NUMMER"),
            Document(MinorFnr, "MIA", "MINDRE"),
            Document(AdultFnr, "KARI", "NORDMANN")));

        var users = await client.FetchUsers(10, TestContext.Current.CancellationToken);

        Assert.Equal([AdultFnr], users.Select(user => user.Fnr));
    }

    [Fact]
    public async Task Given_more_persons_than_requested_should_cut_the_result_to_the_count_in_order()
    {
        var generated = TestUserGenerator.Generate(8);
        var (client, _, _) = CreateClient(_ => Search(
            [.. generated.Select(user => Document(user.Fnr, user.FirstName.ToUpperInvariant(), user.LastName.ToUpperInvariant()))]));

        var users = await client.FetchUsers(3, TestContext.Current.CancellationToken);

        Assert.Equal(generated.Take(3).Select(user => user.Fnr), users.Select(user => user.Fnr));
    }

    [Fact]
    public async Task Given_an_empty_result_should_throw_a_tenor_exception()
    {
        var (client, _, _) = CreateClient(_ => Search());

        await Assert.ThrowsAsync<TenorException>(
            () => client.FetchUsers(5, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_only_persons_that_cannot_be_ordered_should_throw_a_tenor_exception()
    {
        var (client, _, _) = CreateClient(_ => Search(Document(DNumber, "DAG", "NUMMER"), Document(MinorFnr, "MIA", "MINDRE")));

        await Assert.ThrowsAsync<TenorException>(
            () => client.FetchUsers(5, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_a_success_answer_that_is_not_json_should_throw_a_tenor_exception()
    {
        var (client, _, _) = CreateClient(_ =>
            FakeHttpMessageHandler.Json(HttpStatusCode.OK, "<html>Bad gateway</html>"));

        await Assert.ThrowsAsync<TenorException>(
            () => client.FetchUsers(5, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_a_non_success_answer_should_throw_a_tenor_exception()
    {
        var (client, _, _) = CreateClient(_ =>
            FakeHttpMessageHandler.Json(HttpStatusCode.Unauthorized, """{"message":"unauthorized"}"""));

        var exception = await Assert.ThrowsAsync<TenorException>(
            () => client.FetchUsers(5, TestContext.Current.CancellationToken));

        Assert.Contains("401", exception.Message);
    }

    private static HttpResponseMessage Search(params object[] documents) =>
        FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, JsonSerializer.Serialize(new { dokumentListe = documents }));

    private static object Document(string id, string? firstName, string? lastName) =>
        new { id, fornavn = firstName, etternavn = lastName };

    private (TenorClient Client, FakeHttpMessageHandler Tenor, FakeHttpMessageHandler Maskinporten) CreateClient(
        Func<RecordedRequest, HttpResponseMessage> tenorResponse)
    {
        var maskinporten = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"access_token":"token-1","expires_in":3600}"""));
        var maskinportenClient = new MaskinportenClient(
            new HttpClient(maskinporten),
            new MaskinportenCredentials("client-1", TestRsaKey.PrivateKeyPem, "key-1"),
            _time);

        var tenor = new FakeHttpMessageHandler(tenorResponse);
        return (new TenorClient(new HttpClient(tenor), maskinportenClient, _time), tenor, maskinporten);
    }
}
