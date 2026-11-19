using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Funding;
using OSSFuel.Api.Features.Projects;
using Stripe;
using Stripe.Checkout;
using Xunit;

namespace OSSFuel.UnitTests.Features.Funding;

public class StripePaymentProviderTests
{
    private sealed class TestSessionService : SessionService
    {
        public SessionCreateOptions? LastOptions { get; private set; }
        public string? LastExpiredSessionId { get; private set; }
        public Exception? ExpireException { get; set; }
        public Session SessionToReturn { get; set; } = new()
        {
            Id = "cs_test_session_id",
            Url = "https://checkout.stripe.com/c/pay/cs_test_session_id",
        };

        public override Task<Session> CreateAsync(
            SessionCreateOptions options,
            RequestOptions? requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(SessionToReturn);
        }

        public override Task<Session> ExpireAsync(
            string id,
            SessionExpireOptions? options = null,
            RequestOptions? requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            LastExpiredSessionId = id;
            if (ExpireException is not null)
            {
                throw ExpireException;
            }

            return Task.FromResult(new Session { Id = id, Status = "expired" });
        }
    }

    private static OSSFuelDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OSSFuelDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OSSFuelDbContext(options);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("sk_test_12345", true)]
    public void IsConfigured_ReturnsExpectedValue(string? apiKey, bool expected)
    {
        var stripeOptions = Options.Create(new StripeOptions { ApiKey = apiKey });
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();

        var provider = new StripePaymentProvider(stripeOptions, config, db);

        Assert.Equal(expected, provider.IsConfigured);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        var stripeOptions = Options.Create(new StripeOptions { ApiKey = "" });
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();

        var provider = new StripePaymentProvider(stripeOptions, config, db);
        var funding = OSSFuel.Api.Features.Funding.Funding.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2500,
            DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateCheckoutSessionAsync(funding, CancellationToken.None));

        Assert.Contains("not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSessionCreateOptions_ConstructsExpectedParameters()
    {
        var stripeOptions = Options.Create(new StripeOptions
        {
            ApiKey = "sk_test_123",
        });

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["WebAppUrl"] = "https://ossfuel.test",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        using var db = CreateInMemoryDbContext();

        var provider = new StripePaymentProvider(stripeOptions, config, db);

        var fundingId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var funding = new OSSFuel.Api.Features.Funding.Funding
        {
            Id = fundingId,
            ProjectId = projectId,
            SponsorUserId = sponsorId,
            AmountMinor = 5000,
            Currency = "USD",
            CreatedAt = now,
        };

        var options = provider.BuildSessionCreateOptions(funding, "testorg/testrepo");

        Assert.Equal("payment", options.Mode);
        Assert.Equal(["card"], options.PaymentMethodTypes);
        Assert.Equal(fundingId.ToString(), options.ClientReferenceId);
        Assert.NotNull(options.LineItems);
        Assert.Single(options.LineItems);

        var lineItem = options.LineItems[0];
        Assert.Equal(1, lineItem.Quantity);
        Assert.Equal(5000, lineItem.PriceData.UnitAmount);
        Assert.Equal("usd", lineItem.PriceData.Currency);
        Assert.Equal("OSSFuel AI Balance — testorg/testrepo", lineItem.PriceData.ProductData.Name);

        Assert.Equal(fundingId.ToString(), options.Metadata["funding_id"]);
        Assert.Equal(projectId.ToString(), options.Metadata["project_id"]);
        Assert.Equal(sponsorId.ToString(), options.Metadata["sponsor_user_id"]);

        Assert.Equal($"https://ossfuel.test/fundings/{fundingId}?session_id={{CHECKOUT_SESSION_ID}}", options.SuccessUrl);
        Assert.Equal($"https://ossfuel.test/fundings/{fundingId}?cancelled=true", options.CancelUrl);
    }

    [Fact]
    public void BuildSessionCreateOptions_WithCustomConfiguredUrls_ReplacesPlaceholders()
    {
        var fundingId = Guid.NewGuid();
        var stripeOptions = Options.Create(new StripeOptions
        {
            ApiKey = "sk_test_123",
            SuccessUrl = "https://custom.domain/finish?id={id}&session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = "https://custom.domain/cancel?funding={funding.Id}",
        });

        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();

        var provider = new StripePaymentProvider(stripeOptions, config, db);

        var funding = new OSSFuel.Api.Features.Funding.Funding
        {
            Id = fundingId,
            ProjectId = Guid.NewGuid(),
            SponsorUserId = Guid.NewGuid(),
            AmountMinor = 1000,
            Currency = "USD",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var options = provider.BuildSessionCreateOptions(funding, "owner/repo");

        Assert.Equal($"https://custom.domain/finish?id={fundingId}&session_id={{CHECKOUT_SESSION_ID}}", options.SuccessUrl);
        Assert.Equal($"https://custom.domain/cancel?funding={fundingId}", options.CancelUrl);
    }

    [Fact]
    public async Task CreateCheckoutSessionAsync_WithMockedSessionService_FetchesProjectAndReturnsProviderSession()
    {
        var stripeOptions = Options.Create(new StripeOptions
        {
            ApiKey = "sk_test_123",
        });

        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["WebAppUrl"] = "https://ossfuel.dev",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        using var db = CreateInMemoryDbContext();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            OwnerLogin = "octocat",
            Name = "spoon-knife",
            FullName = "octocat/spoon-knife",
            Description = "A fun project",
            HtmlUrl = "https://github.com/octocat/spoon-knife",
            Stars = 42,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var testSessionService = new TestSessionService();
        var provider = new StripePaymentProvider(stripeOptions, config, db, testSessionService);

        var funding = OSSFuel.Api.Features.Funding.Funding.Create(
            project.Id,
            Guid.NewGuid(),
            3500,
            DateTimeOffset.UtcNow,
            OSSFuel.Api.Features.Funding.Funding.StripeProvider);

        var result = await provider.CreateCheckoutSessionAsync(funding, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("cs_test_session_id", result.SessionId);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_session_id", result.CheckoutUrl);

        Assert.NotNull(testSessionService.LastOptions);
        Assert.Equal("OSSFuel AI Balance — octocat/spoon-knife", testSessionService.LastOptions.LineItems[0].PriceData.ProductData.Name);
        Assert.Equal(3500, testSessionService.LastOptions.LineItems[0].PriceData.UnitAmount);
    }

    [Theory]
    [InlineData("stripe", true)]
    [InlineData("STRIPE", true)]
    [InlineData("Stripe", true)]
    [InlineData("mock", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void UsesStripeProvider_ReturnsExpectedResult(string? provider, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:Provider"] = provider,
            })
            .Build();

        var result = FundingSetup.UsesStripeProvider(config);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Funding_Create_SetsProviderProperly()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var sponsorId = Guid.NewGuid();

        var defaultFunding = OSSFuel.Api.Features.Funding.Funding.Create(projectId, sponsorId, 2500, now);
        Assert.Equal(OSSFuel.Api.Features.Funding.Funding.StripeProvider, defaultFunding.Provider);

        var mockFunding = OSSFuel.Api.Features.Funding.Funding.Create(
            projectId,
            sponsorId,
            2500,
            now,
            OSSFuel.Api.Features.Funding.Funding.MockProvider);
        Assert.Equal(OSSFuel.Api.Features.Funding.Funding.MockProvider, mockFunding.Provider);
    }

    [Fact]
    public async Task ExpireCheckoutSessionAsync_WhenNotConfigured_DoesNothing()
    {
        var stripeOptions = Options.Create(new StripeOptions { ApiKey = null });
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();
        var sessionService = new TestSessionService();

        var provider = new StripePaymentProvider(stripeOptions, config, db, sessionService);

        await provider.ExpireCheckoutSessionAsync("cs_test_123");

        Assert.Null(sessionService.LastExpiredSessionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExpireCheckoutSessionAsync_WhenSessionIdNullOrWhitespace_DoesNothing(string? sessionId)
    {
        var stripeOptions = Options.Create(new StripeOptions { ApiKey = "sk_test_123" });
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();
        var sessionService = new TestSessionService();

        var provider = new StripePaymentProvider(stripeOptions, config, db, sessionService);

        await provider.ExpireCheckoutSessionAsync(sessionId!);

        Assert.Null(sessionService.LastExpiredSessionId);
    }

    [Fact]
    public async Task ExpireCheckoutSessionAsync_CallsStripeSessionExpireAsync()
    {
        var stripeOptions = Options.Create(new StripeOptions { ApiKey = "sk_test_123" });
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();
        var sessionService = new TestSessionService();

        var provider = new StripePaymentProvider(stripeOptions, config, db, sessionService);

        await provider.ExpireCheckoutSessionAsync("cs_test_session_target");

        Assert.Equal("cs_test_session_target", sessionService.LastExpiredSessionId);
    }

    [Fact]
    public async Task ExpireCheckoutSessionAsync_WhenStripeThrowsStripeException_LogsAndDoesNotThrow()
    {
        var stripeOptions = Options.Create(new StripeOptions { ApiKey = "sk_test_123" });
        var config = new ConfigurationBuilder().Build();
        using var db = CreateInMemoryDbContext();
        var sessionService = new TestSessionService
        {
            ExpireException = new StripeException("The session cannot be expired because it is already complete."),
        };

        var provider = new StripePaymentProvider(stripeOptions, config, db, sessionService);

        var exception = await Record.ExceptionAsync(() =>
            provider.ExpireCheckoutSessionAsync("cs_test_already_complete"));

        Assert.Null(exception);
        Assert.Equal("cs_test_already_complete", sessionService.LastExpiredSessionId);
    }
}
