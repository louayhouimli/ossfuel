namespace OSSFuel.Api.Features.Balances;

public class AIBalance
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public long AmountMinor { get; set; }

    public string Currency { get; set; } = Money.Currency;

    public uint RowVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyCollection<BalanceTransaction> Transactions => _transactions;

    private readonly List<BalanceTransaction> _transactions = [];

    public static AIBalance Open(Guid projectId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            AmountMinor = 0,
            Currency = Money.Currency,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public BalanceTransaction Credit(long amountMinor, Guid fundingId, string? description, DateTimeOffset now)
    {
        if (amountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountMinor),
                amountMinor,
                "A credit must be a positive amount.");
        }

        AmountMinor += amountMinor;
        UpdatedAt = now;

        var transaction = new BalanceTransaction
        {
            AIBalanceId = Id,
            FundingId = fundingId,
            AmountMinor = amountMinor,
            Type = BalanceTransaction.FundingCreditType,
            Status = BalanceTransaction.PostedStatus,
            Description = description,
            CreatedAt = now,
        };

        _transactions.Add(transaction);

        return transaction;
    }
}
