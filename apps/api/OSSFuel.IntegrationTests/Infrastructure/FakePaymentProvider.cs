using OSSFuel.Api.Features.Funding;

namespace OSSFuel.IntegrationTests.Infrastructure;

public sealed class FakePaymentProvider : IPaymentProvider
{
    public bool IsConfigured { get; set; } = true;

    public Exception? CreateException { get; set; }

    public string? LastSessionId { get; private set; }

    public Task<ProviderSession> CreateCheckoutSessionAsync(
        Funding funding,
        CancellationToken cancellationToken)
    {
        if (CreateException is not null)
        {
            return Task.FromException<ProviderSession>(CreateException);
        }

        LastSessionId = $"fake_{funding.Id:N}";

        return Task.FromResult(new ProviderSession(
            LastSessionId,
            $"https://payments.test/checkout/{funding.Id}"));
    }
}
