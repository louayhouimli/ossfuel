using OSSFuel.Api.Features.Balances;

namespace OSSFuel.UnitTests.Features.Balances;

public class AIBalanceTests
{
    [Fact]
    public void Open_starts_at_zero_in_usd()
    {
        var balance = AIBalance.Open(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(0, balance.AmountMinor);
        Assert.Equal("usd", balance.Currency);
    }

    [Fact]
    public void Credit_adds_the_amount_and_appends_one_transaction()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = AIBalance.Open(Guid.NewGuid(), now);
        var fundingId = Guid.NewGuid();

        var transaction = balance.Credit(2500, fundingId, "Funding received", now);

        Assert.Equal(2500, balance.AmountMinor);
        Assert.Single(balance.Transactions);
        Assert.Equal(2500, transaction.AmountMinor);
        Assert.Equal(fundingId, transaction.FundingId);
        Assert.Equal("funding_credit", transaction.Type);
        Assert.Equal("posted", transaction.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Credit_rejects_non_positive_amounts(long amount)
    {
        var balance = AIBalance.Open(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => balance.Credit(amount, Guid.NewGuid(), null, DateTimeOffset.UtcNow));
    }
}
