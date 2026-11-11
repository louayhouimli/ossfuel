namespace OSSFuel.Api.Features.Gateway;

public class AIUsage
{
    public const string OpenRouterProvider = "openrouter";
    public const string SucceededStatus = "succeeded";
    public const string FailedStatus = "failed";

    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid ApiKeyId { get; init; }

    public required string Model { get; init; }

    public required string Provider { get; init; }

    public int PromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    public int TotalTokens { get; init; }

    public long CostMinor { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public static AIUsage RecordSuccess(
        Guid projectId,
        Guid apiKeyId,
        string model,
        string provider,
        int promptTokens,
        int completionTokens,
        long costMinor,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ApiKeyId = apiKeyId,
            Model = model,
            Provider = provider,
            PromptTokens = Math.Max(0, promptTokens),
            CompletionTokens = Math.Max(0, completionTokens),
            TotalTokens = Math.Max(0, promptTokens) + Math.Max(0, completionTokens),
            CostMinor = costMinor,
            Status = SucceededStatus,
            CreatedAt = now,
        };

    public static AIUsage RecordFailure(
        Guid projectId,
        Guid apiKeyId,
        string model,
        string provider,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ApiKeyId = apiKeyId,
            Model = model,
            Provider = provider,
            PromptTokens = 0,
            CompletionTokens = 0,
            TotalTokens = 0,
            CostMinor = 0,
            Status = FailedStatus,
            CreatedAt = now,
        };
}
