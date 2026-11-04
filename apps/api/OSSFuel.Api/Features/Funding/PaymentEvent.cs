namespace OSSFuel.Api.Features.Funding;

public class PaymentEvent
{
    public Guid Id { get; set; }

    public string Provider { get; set; } = Funding.MockProvider;

    public required string ProviderEventId { get; set; }

    public required string Type { get; set; }

    public string? ProviderObjectId { get; set; }

    public PaymentOutcome Outcome { get; set; }

    public string? Detail { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
