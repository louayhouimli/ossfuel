namespace OSSFuel.Api.Features.Gateway;

public static class TokenCostCalculator
{
    private const decimal DefaultPromptUsdPerMillionTokens = 2.50m;
    private const decimal DefaultCompletionUsdPerMillionTokens = 10.00m;

    public static long CalculateCostMinor(
        int promptTokens,
        int completionTokens,
        decimal? providerCostUsd = null,
        string? model = null)
    {
        if (providerCostUsd == 0m
            || string.Equals(model, "openrouter/free", StringComparison.OrdinalIgnoreCase)
            || (model != null && model.EndsWith(":free", StringComparison.OrdinalIgnoreCase)))
        {
            return 0L;
        }

        decimal costUsd;

        if (providerCostUsd is > 0m)
        {
            costUsd = providerCostUsd.Value;
        }
        else
        {
            var promptUsd = Math.Max(0, promptTokens) * DefaultPromptUsdPerMillionTokens / 1_000_000m;
            var completionUsd = Math.Max(0, completionTokens) * DefaultCompletionUsdPerMillionTokens / 1_000_000m;
            costUsd = promptUsd + completionUsd;
        }

        var cents = (long)Math.Ceiling(costUsd * 100m);

        return Math.Max(1L, cents);
    }
}
