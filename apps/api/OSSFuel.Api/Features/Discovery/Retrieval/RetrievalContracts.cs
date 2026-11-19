namespace OSSFuel.Api.Features.Discovery.Retrieval;

public record ProjectSearchQuery(
    string Query,
    string? Language = null,
    int? MinStars = null,
    int? MaxStars = null,
    string[]? Topics = null,
    int Limit = 5);

public record ProjectSearchChunkMatch(
    Guid ChunkId,
    string ChunkType,
    string Content,
    string? HeaderPath,
    float CosineDistance);

public record ProjectSearchResult(
    Guid ProjectId,
    string FullName,
    string Name,
    string? Description,
    string HtmlUrl,
    string? Language,
    int Stars,
    int Forks,
    int OpenIssues,
    string[] Topics,
    float SemanticSimilarity,
    float VitalityScore,
    float CombinedScore,
    IReadOnlyList<ProjectSearchChunkMatch> MatchedChunks);
