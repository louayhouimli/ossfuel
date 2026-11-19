using OSSFuel.Api.Features.Discovery;
using OSSFuel.Api.Features.Discovery.Gemini;

namespace OSSFuel.IntegrationTests.Infrastructure;

public sealed class FakeGeminiClient : IGeminiClient
{
    public List<IReadOnlyList<string>> CapturedBatchCalls { get; } = [];

    public Func<IReadOnlyList<string>, IReadOnlyList<ReadOnlyMemory<float>>>? BatchHandler { get; set; }

    public Func<string, Task<string>>? ContentHandler { get; set; }

    public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        CapturedBatchCalls.Add(texts);
        if (BatchHandler is not null)
        {
            return Task.FromResult(BatchHandler(texts));
        }

        var results = texts
            .Select(_ => (ReadOnlyMemory<float>)new float[ProjectChunk.VectorDimensions])
            .ToList();

        return Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(results);
    }

    public Task<ReadOnlyMemory<float>> EmbedQueryAsync(
        string query,
        CancellationToken ct = default)
    {
        return Task.FromResult((ReadOnlyMemory<float>)new float[ProjectChunk.VectorDimensions]);
    }

    public Task<string> GenerateContentAsync(
        string prompt,
        CancellationToken ct = default)
    {
        if (ContentHandler is not null)
        {
            return ContentHandler(prompt);
        }

        return Task.FromResult("Mock generated content");
    }

    public async IAsyncEnumerable<string> StreamContentAsync(
        string prompt,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield return "Mock stream response";
    }
}
