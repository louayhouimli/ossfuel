namespace OSSFuel.Api.Features.Discovery.Gemini;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public string ChatModel { get; set; } = "gemini-2.5-flash";

    public int MaxRetries { get; set; } = 3;

    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 100;
}
