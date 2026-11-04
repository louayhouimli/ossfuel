using OSSFuel.Api.Features.GitHub;

namespace OSSFuel.Api.Features.Funding;

/// <summary>
/// A local stand-in for a hosted checkout. The sponsor lands on our own page,
/// clicks pay, and the completion endpoint feeds the same credit service a real
/// provider would. No card data and no external calls.
/// </summary>
public sealed class MockPaymentProvider(IConfiguration configuration) : IPaymentProvider
{
    public bool IsConfigured => true;

    public Task<ProviderSession> CreateCheckoutSessionAsync(
        Funding funding,
        CancellationToken cancellationToken)
    {
        var webAppUrl = configuration["WebAppUrl"] ?? "http://localhost:5173";

        return Task.FromResult(new ProviderSession(
            funding.Id.ToString("N"),
            $"{webAppUrl.TrimEnd('/')}/mock-checkout/{funding.Id}"));
    }
}
