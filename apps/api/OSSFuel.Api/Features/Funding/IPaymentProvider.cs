namespace OSSFuel.Api.Features.Funding;

/// <summary>
/// The only thing the rest of the API knows about payments. The mock provider
/// stands in today; a Stripe adapter can implement the same contract later
/// without touching the credit path.
/// </summary>
public interface IPaymentProvider
{
    bool IsConfigured { get; }

    Task<ProviderSession> CreateCheckoutSessionAsync(
        Funding funding,
        CancellationToken cancellationToken);
}

public sealed record ProviderSession(string SessionId, string CheckoutUrl);
