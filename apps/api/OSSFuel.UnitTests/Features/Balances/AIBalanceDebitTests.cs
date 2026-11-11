using OSSFuel.Api.Features.Balances;
using Xunit;

namespace OSSFuel.UnitTests.Features.Balances;

public class AIBalanceDebitTests
{
    [Fact]
    public void Debit_WithSufficientFunds_DecreasesBalanceAndAddsTransaction()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = AIBalance.Open(Guid.NewGuid(), now);
        balance.Credit(500, Guid.NewGuid(), "Funding", now);

        var usageId = Guid.NewGuid();
        var tx = balance.Debit(150, usageId, "AI Request", now);

        Assert.Equal(350, balance.AmountMinor);
        Assert.Equal(-150, tx.AmountMinor);
        Assert.Equal(usageId, tx.AIUsageId);
        Assert.Equal(BalanceTransaction.AIUsageDebitType, tx.Type);
    }

    [Fact]
    public void Debit_WithInsufficientFunds_ThrowsInvalidOperationException()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = AIBalance.Open(Guid.NewGuid(), now);
        balance.Credit(100, Guid.NewGuid(), "Funding", now);

        Assert.Throws<InvalidOperationException>(() =>
            balance.Debit(150, Guid.NewGuid(), "Too expensive", now));
    }

    [Fact]
    public void HasSufficientFunds_ReturnsExpectedResult()
    {
        var now = DateTimeOffset.UtcNow;
        var balance = AIBalance.Open(Guid.NewGuid(), now);

        Assert.False(balance.HasSufficientFunds(1));

        balance.Credit(10, Guid.NewGuid(), "Credit", now);

        Assert.True(balance.HasSufficientFunds(10));
        Assert.False(balance.HasSufficientFunds(11));
    }
}
