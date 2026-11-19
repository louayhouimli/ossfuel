namespace OSSFuel.Api.Features.Discovery.Gemini;

public interface IGeminiClient
{
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);

    Task<ReadOnlyMemory<float>> EmbedQueryAsync(
        string query,
        CancellationToken ct = default);

    Task<string> GenerateContentAsync(
        string prompt,
        CancellationToken ct = default);

    IAsyncEnumerable<string> StreamContentAsync(
        string prompt,
        CancellationToken ct = default);
}
