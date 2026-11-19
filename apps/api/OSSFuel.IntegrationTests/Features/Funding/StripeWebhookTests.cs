using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Funding;
using OSSFuel.IntegrationTests.Infrastructure;
using Stripe;
using BalanceTransaction = OSSFuel.Api.Features.Balances.BalanceTransaction;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class StripeWebhookTests : IDisposable
{
    private const string WebhookSecret = "whsec_test_stripe_secret_1234567890abcdef";

    private readonly StripeWebhookTestFactory _factory;
    private readonly HttpClient _client;

    public StripeWebhookTests()
    {
        _factory = new StripeWebhookTestFactory(WebhookSecret);
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task HandleWebhook_WhenSignatureHeaderMissing_ReturnsBadRequest()
    {
        var json = CreateCheckoutSessionCompletedJson(
            eventId: "evt_missing_sig",
            sessionId: "cs_test_missing_sig",
            amountMinor: 2500);

        var request = CreateWebhookRequest(json, signatureHeader: null);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_WhenSignatureInvalid_ReturnsBadRequest()
    {
        var json = CreateCheckoutSessionCompletedJson(
            eventId: "evt_invalid_sig",
            sessionId: "cs_test_invalid_sig",
            amountMinor: 2500);

        var request = CreateWebhookRequest(json, signatureHeader: "t=1234567890,v1=invalidsignaturevalue");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_WhenSignatureSecretMismatched_ReturnsBadRequest()
    {
        var json = CreateCheckoutSessionCompletedJson(
            eventId: "evt_wrong_secret",
            sessionId: "cs_test_wrong_secret",
            amountMinor: 2500);

        var signature = CreateTestHeader(json, "whsec_different_secret_key");
        var request = CreateWebhookRequest(json, signature);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HandleWebhook_WhenCheckoutSessionCompleted_TransitionsFundingToSucceededAndCreditsBalance()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId, amountMinor: 2500);
        var sessionId = _factory.SessionIdOf(fundingId);

        var eventId = $"evt_completed_{Guid.NewGuid():N}";
        var json = CreateCheckoutSessionCompletedJson(
            eventId: eventId,
            sessionId: sessionId,
            amountMinor: 2500,
            currency: "usd",
            paymentIntentId: "pi_stripe_success_123");

        var signature = CreateTestHeader(json, WebhookSecret);
        var request = CreateWebhookRequest(json, signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(responseContent.GetProperty("received").GetBoolean());

        var stored = _factory.ReadDb(db => db.Fundings.Single(f => f.Id == fundingId));
        Assert.Equal(FundingStatus.Succeeded, stored.Status);
        Assert.Equal("pi_stripe_success_123", stored.ProviderPaymentIntentId);
        Assert.NotNull(stored.CompletedAt);

        Assert.Equal(2500, _factory.BalanceOf(projectId));

        var transactions = _factory.ReadDb(db => db.BalanceTransactions.Where(t => t.FundingId == fundingId).ToList());
        var tx = Assert.Single(transactions);
        Assert.Equal(2500, tx.AmountMinor);
        Assert.Equal(BalanceTransaction.FundingCreditType, tx.Type);
        Assert.Equal(BalanceTransaction.PostedStatus, tx.Status);

        var paymentEvent = _factory.ReadDb(db => db.PaymentEvents.Single(e => e.ProviderEventId == eventId));
        Assert.Equal(PaymentOutcome.Credited, paymentEvent.Outcome);
        Assert.Equal(OSSFuel.Api.Features.Funding.Funding.StripeProvider, paymentEvent.Provider);
    }

    [Fact]
    public async Task HandleWebhook_WhenDuplicateDelivery_ReturnsOkWithoutCreditingTwice()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId, amountMinor: 2500);
        var sessionId = _factory.SessionIdOf(fundingId);

        var eventId = $"evt_duplicate_{Guid.NewGuid():N}";
        var json = CreateCheckoutSessionCompletedJson(
            eventId: eventId,
            sessionId: sessionId,
            amountMinor: 2500,
            currency: "usd",
            paymentIntentId: "pi_stripe_duplicate_123");

        var signature = CreateTestHeader(json, WebhookSecret);

        // First delivery
        var firstRequest = CreateWebhookRequest(json, signature);
        var firstResponse = await _client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(2500, _factory.BalanceOf(projectId));

        // Duplicate delivery with same event
        var secondRequest = CreateWebhookRequest(json, signature);
        var secondResponse = await _client.SendAsync(secondRequest);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        // Balance should still be 2500, credited only once
        Assert.Equal(2500, _factory.BalanceOf(projectId));

        var transactions = _factory.ReadDb(db => db.BalanceTransactions.Where(t => t.FundingId == fundingId).ToList());
        Assert.Single(transactions);
    }

    [Fact]
    public async Task HandleWebhook_WhenCalledWithoutCsrfHeader_AllowsRequestThrough()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId, amountMinor: 2500);
        var sessionId = _factory.SessionIdOf(fundingId);

        // Client has authentication cookie header but NO X-OSSFuel-Client CSRF header
        var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, sponsorId.ToString());

        var eventId = $"evt_csrf_exempt_{Guid.NewGuid():N}";
        var json = CreateCheckoutSessionCompletedJson(
            eventId: eventId,
            sessionId: sessionId,
            amountMinor: 2500,
            currency: "usd",
            paymentIntentId: "pi_csrf_exempt_123");

        var signature = CreateTestHeader(json, WebhookSecret);
        var request = CreateWebhookRequest(json, signature);

        var response = await authedClient.SendAsync(request);

        // Crucial assertion: Must NOT be 403 Forbidden
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = _factory.ReadDb(db => db.Fundings.Single(f => f.Id == fundingId));
        Assert.Equal(FundingStatus.Succeeded, stored.Status);
    }

    [Fact]
    public async Task HandleWebhook_WhenCheckoutSessionExpired_TransitionsFundingToCancelled()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId, amountMinor: 2500);
        var sessionId = _factory.SessionIdOf(fundingId);

        var eventId = $"evt_expired_{Guid.NewGuid():N}";
        var json = CreateCheckoutSessionExpiredJson(
            eventId: eventId,
            sessionId: sessionId,
            amountMinor: 2500);

        var signature = CreateTestHeader(json, WebhookSecret);
        var request = CreateWebhookRequest(json, signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = _factory.ReadDb(db => db.Fundings.Single(f => f.Id == fundingId));
        Assert.Equal(FundingStatus.Cancelled, stored.Status);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task HandleWebhook_WhenPaymentIntentFailed_TransitionsFundingToFailed()
    {
        var sponsorId = _factory.SeedUser();
        var ownerId = _factory.SeedUser(login: "owner");
        var projectId = _factory.SeedProject(ownerId);
        var fundingId = _factory.SeedFunding(projectId, sponsorId, amountMinor: 2500);
        var sessionId = _factory.SessionIdOf(fundingId);

        var eventId = $"evt_failed_{Guid.NewGuid():N}";
        var json = CreatePaymentIntentFailedJson(
            eventId: eventId,
            sessionId: sessionId,
            amountMinor: 2500);

        var signature = CreateTestHeader(json, WebhookSecret);
        var request = CreateWebhookRequest(json, signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stored = _factory.ReadDb(db => db.Fundings.Single(f => f.Id == fundingId));
        Assert.Equal(FundingStatus.Failed, stored.Status);
        Assert.Equal(0, _factory.BalanceOf(projectId));
    }

    [Fact]
    public async Task HandleWebhook_WhenUnhandledEventType_ReturnsOkWithReceivedTrue()
    {
        var eventId = $"evt_unhandled_{Guid.NewGuid():N}";
        var json = CreateUnhandledEventJson(eventId);

        var signature = CreateTestHeader(json, WebhookSecret);
        var request = CreateWebhookRequest(json, signature);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(responseContent.GetProperty("received").GetBoolean());
    }

    private static HttpRequestMessage CreateWebhookRequest(
        string json,
        string? signatureHeader,
        string path = "/api/v1/fundings/stripe-webhook")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (signatureHeader is not null)
        {
            request.Headers.Add("Stripe-Signature", signatureHeader);
        }

        return request;
    }

    public static string CreateTestHeader(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    private static string CreateCheckoutSessionCompletedJson(
        string eventId,
        string sessionId,
        long amountMinor,
        string currency = "usd",
        string paymentIntentId = "pi_test_123") =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "created": 1700000000,
          "type": "checkout.session.completed",
          "request": null,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "amount_total": {{amountMinor}},
              "currency": "{{currency}}",
              "payment_intent": "{{paymentIntentId}}",
              "payment_status": "paid",
              "status": "complete"
            }
          }
        }
        """;

    private static string CreateCheckoutSessionExpiredJson(
        string eventId,
        string sessionId,
        long amountMinor,
        string currency = "usd") =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "created": 1700000000,
          "type": "checkout.session.expired",
          "request": null,
          "data": {
            "object": {
              "id": "{{sessionId}}",
              "object": "checkout.session",
              "amount_total": {{amountMinor}},
              "currency": "{{currency}}",
              "status": "expired"
            }
          }
        }
        """;

    private static string CreatePaymentIntentFailedJson(
        string eventId,
        string sessionId,
        long amountMinor,
        string currency = "usd",
        string paymentIntentId = "pi_failed_123") =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "created": 1700000000,
          "type": "payment_intent.payment_failed",
          "request": null,
          "data": {
            "object": {
              "id": "{{paymentIntentId}}",
              "object": "payment_intent",
              "amount": {{amountMinor}},
              "currency": "{{currency}}",
              "metadata": {
                "checkout_session_id": "{{sessionId}}"
              }
            }
          }
        }
        """;

    private static string CreateUnhandledEventJson(string eventId) =>
        $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "created": 1700000000,
          "type": "customer.created",
          "request": null,
          "data": {
            "object": {
              "id": "cus_123",
              "object": "customer"
            }
          }
        }
        """;

    private sealed class StripeWebhookTestFactory(string webhookSecret) : ProjectsWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Payments:Stripe:WebhookSecret", webhookSecret);
        }
    }
}
