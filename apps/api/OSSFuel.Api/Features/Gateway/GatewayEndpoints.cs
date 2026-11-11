using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.ApiKeys;

namespace OSSFuel.Api.Features.Gateway;

public static class GatewayEndpoints
{
    public static IEndpointRouteBuilder MapGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/chat/completions", HandleChatCompletionsAsync);
        app.MapPost("/api/v1/chat/completions", HandleChatCompletionsAsync);
        app.MapPost("/v1/responses", HandleResponsesAsync);
        app.MapPost("/api/v1/responses", HandleResponsesAsync);
        app.MapGet("/v1/models", GetModelsAsync);
        app.MapGet("/api/v1/models", GetModelsAsync);
        app.MapGet("/projects/{projectId:guid}/ai-usage", GetProjectAIUsageAsync);

        return app;
    }

    private static async Task<IResult> GetModelsAsync(
        IAIProviderClient providerClient,
        CancellationToken cancellationToken)
    {
        var json = await providerClient.GetModelsJsonAsync(cancellationToken);
        return Results.Content(json, "application/json");
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

        if (request.Messages is not { Count: > 0 })
        {
            return OpenAiError(
                StatusCodes.Status400BadRequest,
                "At least one message is required.",
                "invalid_request_error");
        }

        var effectiveRequest = string.IsNullOrWhiteSpace(request.Model)
            ? request with { Model = "openrouter/free" }
            : request;

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

        var result = await providerClient.CompleteChatAsync(effectiveRequest, cancellationToken);

        if (!result.Succeeded
            && !string.Equals(effectiveRequest.Model, "openrouter/free", StringComparison.OrdinalIgnoreCase)
            && effectiveRequest.Model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackRequest = effectiveRequest with { Model = "openrouter/free" };
            var fallbackResult = await providerClient.CompleteChatAsync(fallbackRequest, cancellationToken);
            if (fallbackResult.Succeeded)
            {
                result = fallbackResult;
                effectiveRequest = fallbackRequest;
            }
        }

        var now = DateTimeOffset.UtcNow;

        if (!result.Succeeded)
        {
            var failedUsage = AIUsage.RecordFailure(
                apiKey.ProjectId,
                apiKey.Id,
                effectiveRequest.Model,
                providerClient.ProviderName,
                now);

            apiKey.MarkUsed(now);
            db.AIUsages.Add(failedUsage);
            await db.SaveChangesAsync(cancellationToken);

            return OpenAiError(
                StatusCodes.Status502BadGateway,
                "Upstream AI provider failed to process the request.",
                "upstream_error",
                new { upstream_status = result.StatusCode, upstream_response = result.ResponseJson });
        }

        var rawCostMinor = TokenCostCalculator.CalculateCostMinor(
            result.PromptTokens,
            result.CompletionTokens,
            result.ProviderCostUsd,
            effectiveRequest.Model);

        var balanceAmount = balance?.AmountMinor ?? 0;
        var debitMinor = Math.Min(balanceAmount, rawCostMinor);

        var usage = AIUsage.RecordSuccess(
            apiKey.ProjectId,
            apiKey.Id,
            effectiveRequest.Model,
            providerClient.ProviderName,
            result.PromptTokens,
            result.CompletionTokens,
            debitMinor,
            now);

        apiKey.MarkUsed(now);
        db.AIUsages.Add(usage);

        if (balance is not null && debitMinor > 0)
        {
            var transaction = balance.Debit(
                debitMinor,
                usage.Id,
                $"AI usage ({effectiveRequest.Model})",
                now);

            db.BalanceTransactions.Add(transaction);
        }

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

    private static async Task<IResult> HandleResponsesAsync(
        HttpContext httpContext,
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

        using var bodyReader = new StreamReader(httpContext.Request.Body);
        var bodyJson = await bodyReader.ReadToEndAsync(cancellationToken);

        string model = "openrouter/free";
        bool isStream = false;

        if (!string.IsNullOrWhiteSpace(bodyJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(bodyJson);
                if (doc.RootElement.TryGetProperty("model", out var m) && m.GetString() is { } mStr && !string.IsNullOrWhiteSpace(mStr))
                {
                    model = mStr;
                }
                if (doc.RootElement.TryGetProperty("stream", out var s) && s.ValueKind is JsonValueKind.True)
                {
                    isStream = true;
                }
            }
            catch (JsonException) { }
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

        var result = await providerClient.CompleteResponsesAsync(bodyJson, isStream, cancellationToken);

        if (!result.Succeeded
            && !string.Equals(model, "openrouter/free", StringComparison.OrdinalIgnoreCase)
            && model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackJson = System.Text.RegularExpressions.Regex.Replace(bodyJson, "\"model\"\\s*:\\s*\"[^\"]+\"", "\"model\":\"openrouter/free\"");
            var fallbackResult = await providerClient.CompleteResponsesAsync(fallbackJson, isStream, cancellationToken);
            if (fallbackResult.Succeeded)
            {
                result = fallbackResult;
                model = "openrouter/free";
            }
        }

        var now = DateTimeOffset.UtcNow;

        if (!result.Succeeded)
        {
            var failedUsage = AIUsage.RecordFailure(
                apiKey.ProjectId,
                apiKey.Id,
                model,
                providerClient.ProviderName,
                now);

            apiKey.MarkUsed(now);
            db.AIUsages.Add(failedUsage);
            await db.SaveChangesAsync(cancellationToken);

            return OpenAiError(
                StatusCodes.Status502BadGateway,
                "Upstream AI provider failed to process the request.",
                "upstream_error",
                new { upstream_status = result.StatusCode, upstream_response = result.ResponseJson });
        }

        var rawCostMinor = TokenCostCalculator.CalculateCostMinor(
            result.PromptTokens,
            result.CompletionTokens,
            result.ProviderCostUsd,
            model);

        var balanceAmount = balance?.AmountMinor ?? 0;
        var debitMinor = Math.Min(balanceAmount, rawCostMinor);

        var usage = AIUsage.RecordSuccess(
            apiKey.ProjectId,
            apiKey.Id,
            model,
            providerClient.ProviderName,
            result.PromptTokens,
            result.CompletionTokens,
            debitMinor,
            now);

        apiKey.MarkUsed(now);
        db.AIUsages.Add(usage);

        if (balance is not null && debitMinor > 0)
        {
            var transaction = balance.Debit(
                debitMinor,
                usage.Id,
                $"AI usage ({model})",
                now);

            db.BalanceTransactions.Add(transaction);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (isStream && result.StreamChunks is { Count: > 0 })
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

    private static IResult OpenAiError(int statusCode, string message, string code, object? details = null) =>
        Results.Json(
            new
            {
                error = new
                {
                    message,
                    type = "ossfuel_gateway_error",
                    code,
                    details
                }
            },
            statusCode: statusCode);
}
