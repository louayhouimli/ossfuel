using System.Net;
using System.Net.Http.Json;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class FundingHistoryTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetFundings_ReturnsOnlyTheSponsorsOwnHistoryNewestFirst()
    {
        var sponsorId = _factory.SeedUser(login: "sponsor");
        var otherId = _factory.SeedUser(login: "someone-else");
        var ownerId = _factory.SeedUser(login: "owner");
        var firstProject = _factory.SeedProject(ownerId, gitHubRepoId: 1, fullName: "owner/first");
        var secondProject = _factory.SeedProject(ownerId, gitHubRepoId: 2, fullName: "owner/second");

        var older = _factory.SeedFunding(
            firstProject,
            sponsorId,
            amountMinor: 1000,
            createdAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newer = _factory.SeedFunding(
            secondProject,
            sponsorId,
            amountMinor: 2000,
            status: "succeeded");
        _factory.SeedFunding(firstProject, otherId, amountMinor: 3000);

        var response = await _factory.CreateClientFor(sponsorId).GetAsync("/fundings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FundingListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.TotalCount);
        Assert.Equal([newer, older], payload.Items.Select(item => item.Id));

        Assert.Equal("owner/second", payload.Items[0].ProjectFullName);
        Assert.Equal("succeeded", payload.Items[0].Status);
        Assert.Null(payload.Items[0].CheckoutUrl);
    }

    [Fact]
    public async Task GetFundings_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("/fundings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFunding_WhenItBelongsToTheSponsor_ReturnsIt()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId);

        var payload = await _factory
            .CreateClientFor(sponsorId)
            .GetFromJsonAsync<FundingPayload>($"/fundings/{fundingId}");

        Assert.NotNull(payload);
        Assert.Equal(fundingId, payload!.Id);
        Assert.Equal("pending", payload.Status);
        Assert.Equal(2500, payload.AmountMinor);
        Assert.Equal($"https://payments.test/checkout/{fundingId}", payload.CheckoutUrl);
    }

    [Fact]
    public async Task GetFunding_WhenItBelongsToAnotherSponsor_ReturnsForbidden()
    {
        var sponsorId = _factory.SeedUser(login: "sponsor");
        var otherId = _factory.SeedUser(login: "someone-else");
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId);

        var response = await _factory.CreateClientFor(otherId).GetAsync($"/fundings/{fundingId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetFunding_WhenItDoesNotExist_ReturnsNotFound()
    {
        var sponsorId = _factory.SeedUser();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .GetAsync($"/fundings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record FundingPayload(
        Guid Id,
        Guid ProjectId,
        string ProjectFullName,
        long AmountMinor,
        string Currency,
        string Status,
        string? CheckoutUrl,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt);

    private sealed record FundingListPayload(
        List<FundingPayload> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
