using OSSFuel.Api.Features.Gateway;
using Xunit;

namespace OSSFuel.UnitTests.Features.Gateway;

public class TokenCostCalculatorTests
{
    [Fact]
    public void CalculateCostMinor_WithZeroTokens_ReturnsMinimumOneCent()
    {
        var cost = TokenCostCalculator.CalculateCostMinor(0, 0);
        Assert.Equal(1L, cost);
    }

    [Fact]
    public void CalculateCostMinor_WithHighTokens_RoundsUpCents()
    {
        // 1M prompt tokens ($2.50) + 1M completion tokens ($10.00) = $12.50 = 1250 cents
        var cost = TokenCostCalculator.CalculateCostMinor(1_000_000, 1_000_000);
        Assert.Equal(1250L, cost);
    }

    [Fact]
    public void CalculateCostMinor_WithProviderCostUsd_UsesProviderCostDirectly()
    {
        // $0.054 -> 6 cents
        var cost = TokenCostCalculator.CalculateCostMinor(100, 100, 0.054m);
        Assert.Equal(6L, cost);
    }
}
