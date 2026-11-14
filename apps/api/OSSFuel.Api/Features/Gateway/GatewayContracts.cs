using System.Text.Json;
using System.Text.Json.Serialization;

namespace OSSFuel.Api.Features.Gateway;

public sealed record ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "";

    [JsonPropertyName("messages")]
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; init; } = false;

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }

    public ChatCompletionRequest() { }

    public ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream = false,
        double? Temperature = null,
        int? MaxTokens = null,
        IDictionary<string, JsonElement>? AdditionalProperties = null)
    {
        this.Model = Model;
        this.Messages = Messages;
        this.Stream = Stream;
        this.Temperature = Temperature;
        this.MaxTokens = MaxTokens;
        this.AdditionalProperties = AdditionalProperties;
    }
}

public sealed record ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "";

    [JsonPropertyName("content")]
    public JsonElement? Content { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }

    public ChatMessage() { }

    public ChatMessage(string role, string content)
    {
        Role = role;
        Content = JsonSerializer.SerializeToElement(content);
    }

    [JsonIgnore]
    public string ContentText => Content switch
    {
        { ValueKind: JsonValueKind.String } s => s.GetString() ?? "",
        { ValueKind: JsonValueKind.Array } a => string.Join(" ", a.EnumerateArray()
            .Select(element => element.ValueKind == JsonValueKind.Object && element.TryGetProperty("text", out var textEl)
                ? textEl.GetString() ?? ""
                : element.ToString())),
        { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } other => other.ToString(),
        _ => ""
    };
}

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
