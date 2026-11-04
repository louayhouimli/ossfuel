namespace OSSFuel.Api.Features.Funding;

public sealed class FundingOptions
{
    public const string SectionName = "Funding";

    public long MinAmountMinor { get; set; } = 100;

    public long MaxAmountMinor { get; set; } = 1_000_000;
}
