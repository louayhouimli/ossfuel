using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Features.Funding;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class FundingCreditServiceTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ApplyingTheSameEventTwice_CreditsOnce()
    {
        var (projectId, fundingId) = SeedFunding();
        var notification = CreditNotification(fundingId, "evt_same");

        var first = await ApplyAsync(notification);
        var second = await ApplyAsync(notification);

        Assert.Equal(PaymentOutcome.Credited, first);
        Assert.Equal(PaymentOutcome.Duplicate, second);
        Assert.Equal(2500, _factory.BalanceOf(projectId));
        Assert.Single(_factory.ReadDb(db => db.BalanceTransactions.ToList()));
        Assert.Single(_factory.ReadDb(db => db.PaymentEvents.ToList()));
    }

    [Fact]
    public async Task ApplyingACreditForAnUnknownSession_RecordsAnUnmatchedEvent()
    {
        var outcome = await ApplyAsync(new PaymentNotification(
            EventId: "evt_unknown",
            Type: "mock.payment.succeeded",
            ObjectId: $"missing_{Guid.NewGuid():N}",
            AmountMinor: 2500,
            Currency: "usd",
            PaymentIntentId: null,
            Action: PaymentAction.Credit));

        Assert.Equal(PaymentOutcome.Unmatched, outcome);

        var stored = Assert.Single(_factory.ReadDb(db => db.PaymentEvents.ToList()));
        Assert.Equal(PaymentOutcome.Unmatched, stored.Outcome);
    }

    [Fact]
    public async Task ApplyingACreditWithTheWrongAmount_RejectsAndLeavesTheFundingPending()
    {
        var (projectId, fundingId) = SeedFunding();
        var notification = CreditNotification(fundingId, "evt_wrong_amount") with { AmountMinor = 9999 };

        var outcome = await ApplyAsync(notification);

        Assert.Equal(PaymentOutcome.Rejected, outcome);
        Assert.Equal(0, _factory.BalanceOf(projectId));
        Assert.Equal(
            FundingStatus.Pending,
            _factory.ReadDb(db => db.Fundings.Single(funding => funding.Id == fundingId).Status));
    }

    [Fact]
    public async Task ApplyingACreditWithTheWrongCurrency_Rejects()
    {
        var (projectId, fundingId) = SeedFunding();
        var notification = CreditNotification(fundingId, "evt_wrong_currency") with { Currency = "eur" };

        var outcome = await ApplyAsync(notification);

        Assert.Equal(PaymentOutcome.Rejected, outcome);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task ApplyingASecondCreditAfterSuccess_IsADuplicateAndDoesNotCreditAgain()
    {
        var (projectId, fundingId) = SeedFunding();

        await ApplyAsync(CreditNotification(fundingId, "evt_first"));
        var second = await ApplyAsync(CreditNotification(fundingId, "evt_second"));

        Assert.Equal(PaymentOutcome.Duplicate, second);
        Assert.Equal(2500, _factory.BalanceOf(projectId));
        Assert.Single(_factory.ReadDb(db => db.BalanceTransactions.ToList()));
    }

    [Fact]
    public async Task ApplyingACreditAfterAFailure_IsIgnored()
    {
        var (projectId, fundingId) = SeedFunding();

        await ApplyAsync(CreditNotification(fundingId, "evt_fail") with { Action = PaymentAction.Fail });
        var outcome = await ApplyAsync(CreditNotification(fundingId, "evt_late_credit"));

        Assert.Equal(PaymentOutcome.Ignored, outcome);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task ApplyingAFailureAfterSuccess_IsIgnored()
    {
        var (projectId, fundingId) = SeedFunding();

        await ApplyAsync(CreditNotification(fundingId, "evt_credit"));
        var outcome = await ApplyAsync(
            CreditNotification(fundingId, "evt_late_failure") with { Action = PaymentAction.Fail });

        Assert.Equal(PaymentOutcome.Ignored, outcome);
        Assert.Equal(2500, _factory.BalanceOf(projectId));

        var stored = _factory.ReadDb(db => db.Fundings.Single(funding => funding.Id == fundingId));
        Assert.Equal(FundingStatus.Succeeded, stored.Status);
    }

    [Fact]
    public async Task ApplyingACancelAfterSuccess_IsIgnored()
    {
        var (projectId, fundingId) = SeedFunding();

        await ApplyAsync(CreditNotification(fundingId, "evt_credit"));
        var outcome = await ApplyAsync(
            CreditNotification(fundingId, "evt_late_cancel") with { Action = PaymentAction.Cancel });

        Assert.Equal(PaymentOutcome.Ignored, outcome);
        Assert.Equal(2500, _factory.BalanceOf(projectId));
    }

    private (Guid ProjectId, Guid FundingId) SeedFunding()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);

        return (projectId, _factory.SeedFunding(projectId, sponsorId));
    }

    private PaymentNotification CreditNotification(Guid fundingId, string eventId) =>
        new(
            EventId: eventId,
            Type: "mock.payment.succeeded",
            ObjectId: _factory.SessionIdOf(fundingId),
            AmountMinor: 2500,
            Currency: "usd",
            PaymentIntentId: "mock_pi",
            Action: PaymentAction.Credit);

    private async Task<PaymentOutcome> ApplyAsync(PaymentNotification notification)
    {
        using var scope = _factory.Services.CreateScope();
        var credits = scope.ServiceProvider.GetRequiredService<FundingCreditService>();

        return await credits.ApplyAsync(notification, CancellationToken.None);
    }
}
