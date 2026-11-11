using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.ApiKeys;

namespace OSSFuel.Api.Features.Gateway;

public static class GatewayEndpoints
{
    public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/chat/completions", HandleChatCompletionsAsync);
        app.MapGet("/projects/{projectId:guid}/ai-usage", GetProjectAIUsageAsync);

        return app;
    }

    private static async Task<IResult> HandleChatCompletionsAsync(
        HttpContext httpContext,
        ChatCompletionRequest request,
        IAIProviderClient providerClient,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var rawKey = ExtractBearerToken(httpContext.Request.Headers.Authorization);

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return OpenAiError(
                StatusCodes.Status401Unauthorized,
                "Missing or invalid Authorization Bearer header.",
                "invalid_api_key");
        }

        var keyHash = ApiKeyHasher.ComputeSha256Hex(rawKey);
        var apiKey = await db
            .ProjectApiKeys
            .FirstOrDefaultAsync(candidate => candidate.KeyHash == keyHash, cancellationToken);

        if (apiKey is null || !apiKey.IsActive)
        {
            return OpenAiError(
                StatusCodes.Status401Unauthorized,
                "Invalid or revoked OSSFuel API key.",
                "invalid_api_key");
        }

        if (string.IsNullOrWhiteSpace(request.Model) || request.Messages is not { Count: > 0 })
        {
            return OpenAiError(
                StatusCodes.Status400BadRequest,
                "Both model and at least one message are required.",
                "invalid_request_error");
        }

        var balance = await db
            .AIBalances
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == apiKey.ProjectId, cancellationToken);

        if (balance is null || !balance.HasSufficientFunds(1))
        {
            return OpenAiError(
                StatusCodes.Status402PaymentRequired,
                "Project AI balance is empty or insufficient to process this request.",
                "insufficient_quota");
        }

        var result = await providerClient.CompleteChatAsync(request, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (!result.Succeeded)
        {
            var failedUsage = AIUsage.RecordFailure(
                apiKey.ProjectId,
                apiKey.Id,
                request.Model,
                providerClient.ProviderName,
                now);

            apiKey.MarkUsed(now);
            db.AIUsages.Add(failedUsage);
            await db.SaveChangesAsync(cancellationToken);

            return OpenAiError(
                StatusCodes.Status502BadGateway,
                "Upstream AI provider failed to process the request.",
                "upstream_error");
        }

        var rawCostMinor = TokenCostCalculator.CalculateCostMinor(
            result.PromptTokens,
            result.CompletionTokens,
            result.ProviderCostUsd);

        var debitMinor = Math.Min(balance.AmountMinor, rawCostMinor);

        var usage = AIUsage.RecordSuccess(
            apiKey.ProjectId,
            apiKey.Id,
            request.Model,
            providerClient.ProviderName,
            result.PromptTokens,
            result.CompletionTokens,
            debitMinor,
            now);

        apiKey.MarkUsed(now);
        db.AIUsages.Add(usage);

        var transaction = balance.Debit(
            debitMinor,
            usage.Id,
            $"AI usage ({request.Model})",
            now);

        db.BalanceTransactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        if (request.Stream && result.StreamChunks is { Count: > 0 })
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";

            foreach (var chunk in result.StreamChunks)
            {
                await httpContext.Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
            }

            await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await httpContext.Response.Body.FlushAsync(cancellationToken);

            return Results.Empty;
        }

        return Results.Content(result.ResponseJson, "application/json", statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetProjectAIUsageAsync(
        Guid projectId,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var exists = await db
            .Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        if (!exists)
        {
            return Results.NotFound();
        }

        var usages = await db
            .AIUsages
            .AsNoTracking()
            .Where(usage => usage.ProjectId == projectId)
            .OrderByDescending(usage => usage.CreatedAt)
            .Take(50)
            .Select(usage => new AIUsageSummaryResponse(
                usage.Id,
                usage.ProjectId,
                usage.ApiKeyId,
                usage.Model,
                usage.Provider,
                usage.PromptTokens,
                usage.CompletionTokens,
                usage.TotalTokens,
                usage.CostMinor,
                usage.Status,
                usage.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(usages);
    }

    private static string? ExtractBearerToken(string? authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader)
            || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader["Bearer ".Length..].Trim();
    }

    private static IResult OpenAiError(int statusCode, string message, string code) =>
        Results.Json(
            new
            {
                error = new
                {
                    message,
                    type = "ossfuel_gateway_error",
                    code,
                }
            },
            statusCode: statusCode);
}
