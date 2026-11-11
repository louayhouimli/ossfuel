using System.Text.Json.Serialization;

namespace OSSFuel.Api.Features.Gateway;

public sealed record ChatCompletionRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream = false,
    [property: JsonPropertyName("temperature")] double? Temperature = null,
    [property: JsonPropertyName("max_tokens")] int? MaxTokens = null);

public sealed record ChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

public sealed record ChatCompletionProviderResult(
    bool Succeeded,
    int StatusCode,
    string ResponseJson,
    int PromptTokens,
    int CompletionTokens,
    decimal? ProviderCostUsd,
    IReadOnlyList<string>? StreamChunks = null);

public sealed record AIUsageSummaryResponse(
    Guid Id,
    Guid ProjectId,
    Guid ApiKeyId,
    string Model,
    string Provider,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long CostMinor,
    string Status,
    DateTimeOffset CreatedAt);
