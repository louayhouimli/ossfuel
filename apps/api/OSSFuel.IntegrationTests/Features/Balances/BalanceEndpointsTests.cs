using System.Net;
using System.Net.Http.Json;
using OSSFuel.Api.Features.Balances;

namespace OSSFuel.IntegrationTests.Features.Balances;

public class BalanceEndpointsTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetBalance_IsPublicAndReadsTheProjectBalance()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 2500);

        var response = await _factory.CreateClient().GetAsync($"/projects/{projectId}/balance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<BalancePayload>();
        Assert.NotNull(payload);
        Assert.Equal(projectId, payload!.ProjectId);
        Assert.Equal(2500, payload.AmountMinor);
        Assert.Equal("usd", payload.Currency);
    }

    [Fact]
    public async Task GetBalance_ForAnUnknownProject_ReturnsNotFound()
    {
        var response = await _factory.CreateClient().GetAsync($"/projects/{Guid.NewGuid()}/balance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBalance_WhenTheRowIsMissing_OpensOneInsteadOfFailing()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        _factory.ReadDb(db =>
        {
            var balance = db.AIBalances.Single(candidate => candidate.ProjectId == projectId);
            db.AIBalances.Remove(balance);
            db.SaveChanges();
            return true;
        });

        var response = await _factory.CreateClient().GetAsync($"/projects/{projectId}/balance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task GetTransactions_IsPublicAndReturnsNewestFirst()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        var older = _factory.SeedCredit(
            projectId,
            100,
            "Older credit",
            DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = _factory.SeedCredit(
            projectId,
            200,
            "Newer credit",
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await _factory.CreateClient()
            .GetAsync($"/projects/{projectId}/balance/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TransactionListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.TotalCount);
        Assert.Equal([newer, older], payload.Items.Select(item => item.Id));

        var first = payload.Items[0];
        Assert.Equal(200, first.AmountMinor);
        Assert.Equal(BalanceTransaction.FundingCreditType, first.Type);
        Assert.Equal(BalanceTransaction.PostedStatus, first.Status);
        Assert.Equal("Newer credit", first.Description);
        Assert.Null(first.Sponsor);
    }

    [Fact]
    public async Task GetTransactions_ShowsTheSponsorBehindACredit()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedFundingCredit(projectId);

        var response = await _factory.CreateClient()
            .GetAsync($"/projects/{projectId}/balance/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TransactionListPayload>();
        Assert.NotNull(payload);
        var sponsor = Assert.Single(payload!.Items).Sponsor;
        Assert.NotNull(sponsor);
        Assert.Equal("sponsor", sponsor!.Login);
        Assert.Equal("sponsor name", sponsor.Name);
        Assert.Equal("https://avatars.test/sponsor.png", sponsor.AvatarUrl);
    }

    [Fact]
    public async Task GetTransactions_PagesTheHistory()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        var now = DateTimeOffset.UtcNow;
        _factory.SeedCredit(projectId, 100, "First", now.AddMinutes(-3));
        _factory.SeedCredit(projectId, 200, "Second", now.AddMinutes(-2));
        _factory.SeedCredit(projectId, 300, "Third", now.AddMinutes(-1));

        var response = await _factory.CreateClient()
            .GetAsync($"/projects/{projectId}/balance/transactions?page=2&pageSize=2");

        var payload = await response.Content.ReadFromJsonAsync<TransactionListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload!.TotalCount);
        Assert.Equal(2, payload.Page);
        Assert.Equal(2, payload.PageSize);
        Assert.Single(payload.Items);
        Assert.Equal(100, payload.Items[0].AmountMinor);
    }

    [Fact]
    public async Task GetTransactions_ForAnUnknownProject_ReturnsNotFound()
    {
        var response = await _factory.CreateClient()
            .GetAsync($"/projects/{Guid.NewGuid()}/balance/transactions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_WhenThereAreNone_ReturnsAnEmptyPage()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory.CreateClient()
            .GetAsync($"/projects/{projectId}/balance/transactions");

        var payload = await response.Content.ReadFromJsonAsync<TransactionListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(0, payload!.TotalCount);
        Assert.Empty(payload.Items);
    }

    private sealed record BalancePayload(Guid ProjectId, long AmountMinor, string Currency);

    private sealed record TransactionSponsorPayload(
        Guid Id,
        string Login,
        string? Name,
        string? AvatarUrl);

    private sealed record TransactionPayload(
        Guid Id,
        long AmountMinor,
        string Type,
        string Status,
        string? Description,
        TransactionSponsorPayload? Sponsor,
        DateTimeOffset CreatedAt);

    private sealed record TransactionListPayload(
        List<TransactionPayload> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
