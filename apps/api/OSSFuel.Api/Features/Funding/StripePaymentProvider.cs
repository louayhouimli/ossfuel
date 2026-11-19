using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Balances;
using Stripe;
using Stripe.Checkout;

namespace OSSFuel.Api.Features.Funding;

/// <summary>
/// Hosted Stripe Checkout provider creating checkout sessions for project fundings.
/// </summary>
public sealed class StripePaymentProvider : IPaymentProvider
{
    private readonly IOptions<StripeOptions> _stripeOptions;
    private readonly IConfiguration _configuration;
    private readonly OSSFuelDbContext _db;
    private readonly ILogger<StripePaymentProvider> _logger;
    private readonly SessionService? _sessionService;

    public StripePaymentProvider(
        IOptions<StripeOptions> stripeOptions,
        IConfiguration configuration,
        OSSFuelDbContext db)
        : this(stripeOptions, configuration, db, logger: null, sessionService: null)
    {
    }

    public StripePaymentProvider(
        IOptions<StripeOptions> stripeOptions,
        IConfiguration configuration,
        OSSFuelDbContext db,
        SessionService? sessionService)
        : this(stripeOptions, configuration, db, logger: null, sessionService: sessionService)
    {
    }

    public StripePaymentProvider(
        IOptions<StripeOptions> stripeOptions,
        IConfiguration configuration,
        OSSFuelDbContext db,
        ILogger<StripePaymentProvider>? logger,
        SessionService? sessionService)
    {
        _stripeOptions = stripeOptions;
        _configuration = configuration;
        _db = db;
        _logger = logger ?? NullLogger<StripePaymentProvider>.Instance;
        _sessionService = sessionService;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_stripeOptions.Value.ApiKey);

    public async Task<ProviderSession> CreateCheckoutSessionAsync(
        Funding funding,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Stripe payment provider is not configured.");
        }

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == funding.ProjectId)
            .Select(p => new { p.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        var projectName = project?.FullName ?? "Project";
        var options = BuildSessionCreateOptions(funding, projectName);

        var service = _sessionService ?? new SessionService(new StripeClient(_stripeOptions.Value.ApiKey));
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new ProviderSession(session.Id, session.Url);
    }

    public SessionCreateOptions BuildSessionCreateOptions(Funding funding, string projectName)
    {
        var webAppUrl = (_configuration["WebAppUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var successUrl = !string.IsNullOrWhiteSpace(_stripeOptions.Value.SuccessUrl)
            ? _stripeOptions.Value.SuccessUrl.Replace("{funding.Id}", funding.Id.ToString()).Replace("{id}", funding.Id.ToString())
            : $"{webAppUrl}/fundings/{funding.Id}?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = !string.IsNullOrWhiteSpace(_stripeOptions.Value.CancelUrl)
            ? _stripeOptions.Value.CancelUrl.Replace("{funding.Id}", funding.Id.ToString()).Replace("{id}", funding.Id.ToString())
            : $"{webAppUrl}/fundings/{funding.Id}?cancelled=true";

        return new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = (funding.Currency ?? Money.Currency).ToLowerInvariant(),
                        UnitAmount = funding.AmountMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"OSSFuel AI Balance — {projectName}",
                        },
                    },
                    Quantity = 1,
                },
            ],
            ClientReferenceId = funding.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["funding_id"] = funding.Id.ToString(),
                ["project_id"] = funding.ProjectId.ToString(),
                ["sponsor_user_id"] = funding.SponsorUserId.ToString(),
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        };
    }

    public async Task ExpireCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var service = _sessionService ?? new SessionService(new StripeClient(_stripeOptions.Value.ApiKey));

        try
        {
            await service.ExpireAsync(sessionId, cancellationToken: cancellationToken);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to expire Stripe checkout session {SessionId}: {ErrorMessage}",
                sessionId,
                ex.Message);
        }
    }
}
