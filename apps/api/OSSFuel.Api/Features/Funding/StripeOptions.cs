namespace OSSFuel.Api.Features.Funding;

public sealed class StripeOptions
{
    public const string SectionName = "Payments:Stripe";

    public string? ApiKey { get; set; }

    public string? WebhookSecret { get; set; }

    public string? SuccessUrl { get; set; }

    public string? CancelUrl { get; set; }
}
