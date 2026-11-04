namespace OSSFuel.Api.Features.Funding;

public enum PaymentAction
{
    Credit,
    Fail,
    Cancel,
    Ignore,
}

public sealed record PaymentNotification(
    string EventId,
    string Type,
    string ObjectId,
    long AmountMinor,
    string Currency,
    string? PaymentIntentId,
    PaymentAction Action);
