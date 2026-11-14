using System.Net;
using System.Net.Http.Json;
using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Funding;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class MockCompletionTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CompletingASuccessfulPayment_CreditsTheBalanceExactlyOnce()
    {
        var (sponsorId, projectId, fundingId) = SeedPendingFunding();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.Equal("succeeded", payload!.Status);
        Assert.NotNull(payload.CompletedAt);

        Assert.Equal(2500, _factory.BalanceOf(projectId));

        var transactions = _factory.ReadDb(db => db.BalanceTransactions.ToList());
        var transaction = Assert.Single(transactions);
        Assert.Equal(2500, transaction.AmountMinor);
        Assert.Equal(fundingId, transaction.FundingId);
        Assert.Equal(BalanceTransaction.FundingCreditType, transaction.Type);
        Assert.Equal(BalanceTransaction.PostedStatus, transaction.Status);
    }

    [Fact]
    public async Task CompletingTheSamePaymentTwice_DoesNotCreditTwice()
    {
        var (sponsorId, projectId, fundingId) = SeedPendingFunding();
        var client = _factory.CreateClientFor(sponsorId);

        await client.PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "succeeded" });
        var second = await client.PostAsJsonAsync(
            $"/api/v1/fundings/{fundingId}/mock-complete",
            new { outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(2500, _factory.BalanceOf(projectId));
        Assert.Single(_factory.ReadDb(db => db.BalanceTransactions.ToList()));
    }

    [Fact]
    public async Task CompletingAFailedPayment_LeavesTheBalanceUntouched()
    {
        var (sponsorId, projectId, fundingId) = SeedPendingFunding();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "failed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.Equal("failed", payload!.Status);
        Assert.Equal(0, _factory.BalanceOf(projectId));
        Assert.Empty(_factory.ReadDb(db => db.BalanceTransactions.ToList()));

        var paymentEvent = Assert.Single(_factory.ReadDb(db => db.PaymentEvents.ToList()));
        Assert.Equal(PaymentOutcome.Failed, paymentEvent.Outcome);
    }

    [Fact]
    public async Task CompletingACancelledPayment_LeavesTheBalanceUntouched()
    {
        var (sponsorId, projectId, fundingId) = SeedPendingFunding();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "cancelled" });

        var payload = await response.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.Equal("cancelled", payload!.Status);
        Assert.Equal(0, _factory.BalanceOf(projectId));
        Assert.Empty(_factory.ReadDb(db => db.BalanceTransactions.ToList()));
    }

    [Fact]
    public async Task CompletingAnAlreadyFailedPayment_DoesNotCreditItLater()
    {
        var (sponsorId, projectId, fundingId) = SeedPendingFunding();
        var client = _factory.CreateClientFor(sponsorId);

        await client.PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "failed" });
        var response = await client.PostAsJsonAsync(
            $"/api/v1/fundings/{fundingId}/mock-complete",
            new { outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FundingPayload>();
        Assert.Equal("failed", payload!.Status);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task CompletingAPaymentThatBelongsToAnotherSponsor_ReturnsForbidden()
    {
        var (_, projectId, fundingId) = SeedPendingFunding();
        var otherId = _factory.SeedUser(login: "someone-else");

        var response = await _factory
            .CreateClientFor(otherId)
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task CompletingWithAnUnknownOutcome_ReturnsValidationProblem()
    {
        var (sponsorId, _, fundingId) = SeedPendingFunding();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "maybe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            FundingStatus.Pending,
            _factory.ReadDb(db => db.Fundings.Single(funding => funding.Id == fundingId).Status));
    }

    [Fact]
    public async Task CompletingAPaymentUnauthenticated_ReturnsUnauthorized()
    {
        var (_, _, fundingId) = SeedPendingFunding();

        var response = await _factory
            .CreateClient()
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompletingAnUnknownFunding_ReturnsNotFound()
    {
        var sponsorId = _factory.SeedUser();

        var response = await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync($"/api/v1/fundings/{Guid.NewGuid()}/mock-complete", new { outcome = "succeeded" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CompletingASuccessfulPayment_AddsToAnExistingBalance()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 1000);

        var fundingId = _factory.SeedFunding(projectId, sponsorId, amountMinor: 2500);

        await _factory
            .CreateClientFor(sponsorId)
            .PostAsJsonAsync($"/api/v1/fundings/{fundingId}/mock-complete", new { outcome = "succeeded" });

        Assert.Equal(3500, _factory.BalanceOf(projectId));
    }

    private (Guid SponsorId, Guid ProjectId, Guid FundingId) SeedPendingFunding()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId);

        return (sponsorId, projectId, fundingId);
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
