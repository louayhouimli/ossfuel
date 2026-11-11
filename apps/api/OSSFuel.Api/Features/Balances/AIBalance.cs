namespace OSSFuel.Api.Features.Balances;

public class AIBalance
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public long AmountMinor { get; private set; }

    public string Currency { get; init; } = Money.Currency;

    public uint RowVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; private set; }

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

    public bool HasSufficientFunds(long requiredAmountMinor = 1) =>
        requiredAmountMinor > 0 && AmountMinor >= requiredAmountMinor;

    public BalanceTransaction Debit(long amountMinor, Guid aiUsageId, string? description, DateTimeOffset now)
    {
        if (amountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amountMinor),
                amountMinor,
                "A debit must be a positive amount.");
        }

        if (AmountMinor < amountMinor)
        {
            throw new InvalidOperationException("Insufficient AI balance for this request.");
        }

        AmountMinor -= amountMinor;
        UpdatedAt = now;

        var transaction = new BalanceTransaction
        {
            AIBalanceId = Id,
            AIUsageId = aiUsageId,
            AmountMinor = -amountMinor,
            Type = BalanceTransaction.AIUsageDebitType,
            Status = BalanceTransaction.PostedStatus,
            Description = description,
            CreatedAt = now,
        };

        _transactions.Add(transaction);

        return transaction;
    }
}
