using System.Net;
using System.Net.Http.Json;
using OSSFuel.Api.Features.Funding;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class FundingCreationTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateFunding_WhenSignedIn_CreatesAPendingFundingWithCheckout()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.NotNull(payload);
        Assert.Equal(projectId, payload!.ProjectId);
        Assert.Equal("octocat/Hello-World", payload.ProjectFullName);
        Assert.Equal(2500, payload.AmountMinor);
        Assert.Equal("usd", payload.Currency);
        Assert.Equal("pending", payload.Status);
        Assert.StartsWith("https://payments.test/checkout/", payload.CheckoutUrl);
        Assert.Equal($"/api/v1/fundings/{payload.Id}", response.Headers.Location?.ToString());

        var stored = _factory.ReadDb(db => db.Fundings.Single(funding => funding.Id == payload.Id));
        Assert.Equal(FundingStatus.Pending, stored.Status);
        Assert.Equal(_factory.Payments.LastSessionId, stored.ProviderCheckoutSessionId);
    }

    [Fact]
    public async Task CreateFunding_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory
            .CreateClient()
            .PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(1_000_001)]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreateFunding_WhenTheAmountIsOutsideTheLimits_ReturnsValidationProblem(long amountMinor)
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.ReadDb(db => db.Fundings.ToList()));
    }

    [Fact]
    public async Task CreateFunding_WhenTheProjectDoesNotExist_ReturnsNotFound()
    {
        var sponsorId = _factory.SeedUser();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync("/api/v1/fundings", new { projectId = Guid.NewGuid(), amountMinor = 2500 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateFunding_WhenPaymentsAreNotConfigured_ReturnsServiceUnavailable()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        _factory.Payments.IsConfigured = false;

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(_factory.ReadDb(db => db.Fundings.ToList()));
    }

    [Fact]
    public async Task CreateFunding_WhenTheProviderFails_ReturnsBadGatewayAndStoresTheFailure()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        _factory.Payments.CreateException = new HttpRequestException("payments are down");

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var stored = _factory.ReadDb(db => db.Fundings.Single());
        Assert.Equal(FundingStatus.Failed, stored.Status);
        Assert.NotNull(stored.FailureReason);
    }

    [Fact]
    public async Task CreateFunding_WhenTheSameAmountIsAlreadyPending_ReturnsTheExistingOne()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var client = _factory.CreateClientFor(sponsorId);

        var first = await client.PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });
        var firstPayload = await first.Content.ReadFromJsonAsync<FundingPayload>();

        var second = await client.PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });
        var secondPayload = await second.Content.ReadFromJsonAsync<FundingPayload>();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstPayload!.Id, secondPayload!.Id);
        Assert.NotNull(secondPayload.CheckoutUrl);
        Assert.Equal(firstPayload.CheckoutUrl, secondPayload.CheckoutUrl);
        Assert.Single(_factory.ReadDb(db => db.Fundings.ToList()));
    }

    [Fact]
    public async Task CancelFunding_WhenPending_CancelsAndAllowsCreatingNewFunding()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var client = _factory.CreateClientFor(sponsorId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });
        var created = await createResponse.Content.ReadFromJsonAsync<FundingPayload>();

        var cancelResponse = await client.PostAsync($"/api/v1/fundings/{created!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.Equal("cancelled", cancelled!.Status);

        var newResponse = await client.PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 5000 });
        Assert.Equal(HttpStatusCode.Created, newResponse.StatusCode);

        var newFunding = await newResponse.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.Equal(5000, newFunding!.AmountMinor);
    }

    [Fact]
    public async Task CreateFunding_WhenADifferentAmountIsAlreadyPending_ReturnsConflict()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var client = _factory.CreateClientFor(sponsorId);

        await client.PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 2500 });

        var response = await client.PostAsJsonAsync("/api/v1/fundings", new { projectId, amountMinor = 5000 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(_factory.ReadDb(db => db.Fundings.ToList()));
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
}
