using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OSSFuel.Api.Features.Balances;
using Stripe;
using Stripe.Checkout;

namespace OSSFuel.Api.Features.Funding;

public static class StripeWebhookEndpoint
{
    public static async Task<IResult> HandleWebhookAsync(
        HttpRequest request,
        IOptions<StripeOptions> options,
        FundingCreditService credits,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(StripeWebhookEndpoint));

        var webhookSecret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            logger.LogWarning("Stripe webhook secret is not configured.");
            return Results.BadRequest(new { error = "Webhook secret not configured." });
        }

        if (!request.Headers.TryGetValue("Stripe-Signature", out var signatureHeader)
            || string.IsNullOrWhiteSpace(signatureHeader))
        {
            logger.LogWarning("Missing Stripe-Signature header.");
            return Results.BadRequest(new { error = "Missing Stripe-Signature header." });
        }

        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            logger.LogWarning("Stripe webhook received empty body.");
            return Results.BadRequest(new { error = "Request body is empty." });
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException exception)
        {
            logger.LogWarning(exception, "Stripe webhook signature validation failed.");
            return Results.BadRequest(new { error = "Invalid Stripe signature." });
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to parse Stripe webhook event.");
            return Results.BadRequest(new { error = "Invalid payload." });
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                if (stripeEvent.Data.Object is Session completedSession)
                {
                    var paymentIntentId = completedSession.PaymentIntentId ?? completedSession.PaymentIntent?.Id;
                    var notification = new PaymentNotification(
                        EventId: stripeEvent.Id,
                        Type: stripeEvent.Type,
                        ObjectId: completedSession.Id,
                        AmountMinor: completedSession.AmountTotal ?? 0,
                        Currency: completedSession.Currency ?? Money.Currency,
                        PaymentIntentId: paymentIntentId,
                        Action: PaymentAction.Credit,
                        Provider: Funding.StripeProvider);

                    await credits.ApplyAsync(notification, cancellationToken);
                }
                else
                {
                    logger.LogWarning(
                        "Stripe event {EventId} of type {EventType} did not contain a Session object.",
                        stripeEvent.Id,
                        stripeEvent.Type);
                }
                break;

            case "checkout.session.expired":
                if (stripeEvent.Data.Object is Session expiredSession)
                {
                    var paymentIntentId = expiredSession.PaymentIntentId ?? expiredSession.PaymentIntent?.Id;
                    var notification = new PaymentNotification(
                        EventId: stripeEvent.Id,
                        Type: stripeEvent.Type,
                        ObjectId: expiredSession.Id,
                        AmountMinor: expiredSession.AmountTotal ?? 0,
                        Currency: expiredSession.Currency ?? Money.Currency,
                        PaymentIntentId: paymentIntentId,
                        Action: PaymentAction.Cancel,
                        Provider: Funding.StripeProvider);

                    await credits.ApplyAsync(notification, cancellationToken);
                }
                else
                {
                    logger.LogWarning(
                        "Stripe event {EventId} of type {EventType} did not contain a Session object.",
                        stripeEvent.Id,
                        stripeEvent.Type);
                }
                break;

            case "payment_intent.payment_failed":
                if (stripeEvent.Data.Object is PaymentIntent paymentIntent)
                {
                    var objectId = paymentIntent.Metadata?.GetValueOrDefault("checkout_session_id")
                        ?? paymentIntent.Metadata?.GetValueOrDefault("session_id")
                        ?? paymentIntent.Id;

                    var notification = new PaymentNotification(
                        EventId: stripeEvent.Id,
                        Type: stripeEvent.Type,
                        ObjectId: objectId,
                        AmountMinor: paymentIntent.Amount,
                        Currency: paymentIntent.Currency ?? Money.Currency,
                        PaymentIntentId: paymentIntent.Id,
                        Action: PaymentAction.Fail,
                        Provider: Funding.StripeProvider);

                    await credits.ApplyAsync(notification, cancellationToken);
                }
                else
                {
                    logger.LogWarning(
                        "Stripe event {EventId} of type {EventType} did not contain a PaymentIntent object.",
                        stripeEvent.Id,
                        stripeEvent.Type);
                }
                break;

            default:
                logger.LogInformation(
                    "Stripe webhook received unhandled event type: {EventType}",
                    stripeEvent.Type);
                break;
        }

        return Results.Ok(new { received = true });
    }
}
