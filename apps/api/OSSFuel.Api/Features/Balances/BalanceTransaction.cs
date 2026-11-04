namespace OSSFuel.Api.Features.Balances;

public class BalanceTransaction
{
    public const string FundingCreditType = "funding_credit";
    public const string PostedStatus = "posted";

    public Guid Id { get; set; }

    public Guid AIBalanceId { get; set; }

    public Guid? FundingId { get; set; }

    public long AmountMinor { get; set; }

    public required string Type { get; set; }

    public required string Status { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
