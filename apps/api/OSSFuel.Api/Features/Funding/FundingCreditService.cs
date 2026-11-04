using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Balances;

namespace OSSFuel.Api.Features.Funding;

/// <summary>
/// The only writer of balance changes. Every path that can credit a funding
/// goes through here, so the idempotency rules live in one place.
/// </summary>
public sealed class FundingCreditService(
    OSSFuelDbContext db,
    ILogger<FundingCreditService> logger)
{
    public async Task<PaymentOutcome> ApplyAsync(
        PaymentNotification notification,
        CancellationToken cancellationToken)
    {
        if (await db.PaymentEvents.AnyAsync(
                paymentEvent => paymentEvent.ProviderEventId == notification.EventId,
                cancellationToken))
        {
            return PaymentOutcome.Duplicate;
        }

        var funding = await db
            .Fundings
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(
                candidate => candidate.ProviderCheckoutSessionId == notification.ObjectId,
                cancellationToken);

        if (funding is null)
        {
            return await RecordAsync(
                notification,
                PaymentOutcome.Unmatched,
                "No funding matches this payment session.",
                cancellationToken);
        }

        if (notification.Action == PaymentAction.Ignore)
        {
            return await RecordAsync(notification, PaymentOutcome.Ignored, null, cancellationToken);
        }

        if (notification.AmountMinor != funding.AmountMinor
            || !string.Equals(notification.Currency, funding.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return await RecordAsync(
                notification,
                PaymentOutcome.Rejected,
                $"Expected {funding.AmountMinor} {funding.Currency}, received {notification.AmountMinor} {notification.Currency}.",
                cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;

        switch (notification.Action)
        {
            case PaymentAction.Fail:
                if (funding.Status != FundingStatus.Pending)
                {
                    return await RecordAsync(
                        notification,
                        PaymentOutcome.Ignored,
                        "Funding is not pending.",
                        cancellationToken);
                }

                funding.MarkFailed("The payment failed.", now);
                db.PaymentEvents.Add(CreateEvent(notification, PaymentOutcome.Failed, null, now));
                await db.SaveChangesAsync(cancellationToken);

                return PaymentOutcome.Failed;

            case PaymentAction.Cancel:
                if (funding.Status != FundingStatus.Pending)
                {
                    return await RecordAsync(
                        notification,
                        PaymentOutcome.Ignored,
                        "Funding is not pending.",
                        cancellationToken);
                }

                funding.MarkCancelled("The payment was cancelled.", now);
                db.PaymentEvents.Add(CreateEvent(notification, PaymentOutcome.Cancelled, null, now));
                await db.SaveChangesAsync(cancellationToken);

                return PaymentOutcome.Cancelled;

            case PaymentAction.Credit:
                if (funding.Status == FundingStatus.Succeeded)
                {
                    return await RecordAsync(
                        notification,
                        PaymentOutcome.Duplicate,
                        "Funding already succeeded.",
                        cancellationToken);
                }

                if (funding.Status != FundingStatus.Pending)
                {
                    return await RecordAsync(
                        notification,
                        PaymentOutcome.Ignored,
                        "Funding is not pending.",
                        cancellationToken);
                }

                return await CreditAsync(funding, notification, now, cancellationToken);

            default:
                return await RecordAsync(notification, PaymentOutcome.Ignored, null, cancellationToken);
        }
    }

    private async Task<PaymentOutcome> CreditAsync(
        Funding funding,
        PaymentNotification notification,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var balance = await db
                .AIBalances
                .FirstOrDefaultAsync(
                    candidate => candidate.ProjectId == funding.ProjectId,
                    cancellationToken);

            if (balance is null)
            {
                balance = AIBalance.Open(funding.ProjectId, now);
                db.AIBalances.Add(balance);
            }

            funding.MarkSucceeded(notification.PaymentIntentId, now);
            balance.Credit(funding.AmountMinor, funding.Id, "Funding received", now);

            db.PaymentEvents.Add(CreateEvent(notification, PaymentOutcome.Credited, null, now));

            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return PaymentOutcome.Credited;
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            // A unique index or the balance's xmin token rejected this write,
            // so another delivery already applied it.
            logger.LogWarning(
                exception,
                "Credit for funding {FundingId} lost a race with another delivery",
                funding.Id);

            db.ChangeTracker.Clear();

            return await RecordAsync(
                notification,
                PaymentOutcome.Duplicate,
                "Another delivery already applied this credit.",
                cancellationToken);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<PaymentOutcome> RecordAsync(
        PaymentNotification notification,
        PaymentOutcome outcome,
        string? detail,
        CancellationToken cancellationToken)
    {
        if (await db.PaymentEvents.AnyAsync(
                paymentEvent => paymentEvent.ProviderEventId == notification.EventId,
                cancellationToken))
        {
            return outcome;
        }

        db.PaymentEvents.Add(CreateEvent(notification, outcome, detail, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        return outcome;
    }

    private static PaymentEvent CreateEvent(
        PaymentNotification notification,
        PaymentOutcome outcome,
        string? detail,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Provider = notification.Provider,
            ProviderEventId = notification.EventId,
            Type = notification.Type,
            ProviderObjectId = notification.ObjectId,
            Outcome = outcome,
            Detail = detail,
            ReceivedAt = now,
        };
}
