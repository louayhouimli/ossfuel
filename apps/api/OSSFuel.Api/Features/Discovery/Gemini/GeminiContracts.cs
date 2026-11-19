using System.Text.Json.Serialization;

namespace OSSFuel.Api.Features.Discovery.Gemini;

public sealed record BatchEmbedContentsRequest(
    [property: JsonPropertyName("requests")] IReadOnlyList<EmbedContentRequest> Requests);

public sealed record EmbedContentRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("content")] GeminiContent Content,
    [property: JsonPropertyName("outputDimensionality")] int? OutputDimensionality = 768);

public sealed record GeminiContent(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart> Parts,
    [property: JsonPropertyName("role")] string? Role = null);

public sealed record GeminiPart(
    [property: JsonPropertyName("text")] string Text);

public sealed record BatchEmbedContentsResponse(
    [property: JsonPropertyName("embeddings")] IReadOnlyList<GeminiContentEmbedding>? Embeddings);

public sealed record GeminiContentEmbedding(
    [property: JsonPropertyName("values")] float[]? Values);

public sealed record GenerateContentRequest(
    [property: JsonPropertyName("contents")] IReadOnlyList<GeminiContent> Contents);

public sealed record GenerateContentResponse(
    [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates);

public sealed record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content,
    [property: JsonPropertyName("finishReason")] string? FinishReason);

public sealed record GeminiErrorResponse(
    [property: JsonPropertyName("error")] GeminiErrorDetails? Error);

public sealed record GeminiErrorDetails(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("status")] string? Status);
