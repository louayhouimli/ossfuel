namespace OSSFuel.Api.Features.Funding;

/// <summary>
/// Used when a provider is selected but not configured yet. Every endpoint that
/// needs payments answers 503 instead of failing dependency injection.
/// </summary>
public sealed class UnconfiguredPaymentProvider : IPaymentProvider
{
    public bool IsConfigured => false;

    public Task<ProviderSession> CreateCheckoutSessionAsync(
        Funding funding,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Payments are not configured.");
}
