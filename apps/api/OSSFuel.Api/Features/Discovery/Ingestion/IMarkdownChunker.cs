namespace OSSFuel.Api.Features.Discovery.Ingestion;

public record MarkdownChunkResult(string ChunkType, string Content, string? HeaderPath);

public interface IMarkdownChunker
{
    IReadOnlyList<MarkdownChunkResult> ChunkMarkdown(
        string? markdown,
        int maxChunkChars = 1200,
        int overlapChars = 150);
}
