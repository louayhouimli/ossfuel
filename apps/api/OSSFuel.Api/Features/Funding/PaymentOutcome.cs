namespace OSSFuel.Api.Features.Funding;

public enum PaymentOutcome
{
    Credited,
    Duplicate,
    Ignored,
    Rejected,
    Unmatched,
    Failed,
    Cancelled,
}
