using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Funding;

public class Funding
{
    public const string MockProvider = "mock";

    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid SponsorUserId { get; init; }

    public long AmountMinor { get; init; }

    public string Currency { get; init; } = Money.Currency;

    public FundingStatus Status { get; private set; }

    public string Provider { get; init; } = MockProvider;

    public string? ProviderCheckoutSessionId { get; private set; }

    public string? ProviderCheckoutUrl { get; private set; }

    public string? ProviderPaymentIntentId { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Project Project { get; init; } = null!;

    public User Sponsor { get; init; } = null!;

    public static Funding Create(Guid projectId, Guid sponsorUserId, long amountMinor, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SponsorUserId = sponsorUserId,
            AmountMinor = amountMinor,
            Currency = Money.Currency,
            Status = FundingStatus.Pending,
            Provider = MockProvider,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public bool CanTransitionTo(FundingStatus next) =>
        Status == FundingStatus.Pending
        && next is FundingStatus.Succeeded or FundingStatus.Failed or FundingStatus.Cancelled;

    public void MarkCheckoutCreated(string sessionId, string checkoutUrl, DateTimeOffset now)
    {
        EnsurePending();

        ProviderCheckoutSessionId = sessionId;
        ProviderCheckoutUrl = checkoutUrl;
        UpdatedAt = now;
    }

    public void MarkSucceeded(string? paymentIntentId, DateTimeOffset now)
    {
        EnsurePending();

        Status = FundingStatus.Succeeded;
        ProviderPaymentIntentId = paymentIntentId;
        CompletedAt = now;
        UpdatedAt = now;
    }

    public void MarkFailed(string reason, DateTimeOffset now) =>
        MarkTerminal(FundingStatus.Failed, reason, now);

    public void MarkCancelled(string reason, DateTimeOffset now) =>
        MarkTerminal(FundingStatus.Cancelled, reason, now);

    private void MarkTerminal(FundingStatus status, string reason, DateTimeOffset now)
    {
        EnsurePending();

        Status = status;
        FailureReason = reason;
        CompletedAt = now;
        UpdatedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != FundingStatus.Pending)
        {
            throw new InvalidOperationException("Funding is not pending.");
        }
    }
}
