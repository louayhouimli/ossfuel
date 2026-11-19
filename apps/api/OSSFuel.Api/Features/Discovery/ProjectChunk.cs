using OSSFuel.Api.Features.Projects;
using Pgvector;

namespace OSSFuel.Api.Features.Discovery;

public static class ProjectChunkType
{
    public const string Overview = "Overview";
    public const string Features = "Features";
    public const string Architecture = "Architecture";
    public const string Roadmap = "Roadmap";
    public const string General = "General";

    public static readonly IReadOnlyCollection<string> All =
    [
        Overview,
        Features,
        Architecture,
        Roadmap,
        General,
    ];

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}

public static class ProjectChunkTypes
{
    public const string Overview = ProjectChunkType.Overview;
    public const string Features = ProjectChunkType.Features;
    public const string Architecture = ProjectChunkType.Architecture;
    public const string Roadmap = ProjectChunkType.Roadmap;
    public const string General = ProjectChunkType.General;

    public static readonly IReadOnlyCollection<string> All = ProjectChunkType.All;

    public static bool IsValid(string? type) => ProjectChunkType.IsValid(type);
}

public class ProjectChunk
{
    public const int VectorDimensions = 768;

    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    public string ChunkType { get; set; } = null!;

    public string Content { get; set; } = null!;

    public Vector? Embedding { get; set; }

    public string? HeaderPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastUpdatedAt { get; set; }

    public static ProjectChunk Create(
        Guid projectId,
        string chunkType,
        string content,
        DateTimeOffset now,
        string? headerPath = null,
        Vector? embedding = null)
    {
        if (!ProjectChunkType.IsValid(chunkType))
        {
            throw new ArgumentException($"Invalid chunk type: '{chunkType}'", nameof(chunkType));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new ProjectChunk
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ChunkType = chunkType,
            Content = content,
            HeaderPath = headerPath,
            Embedding = embedding,
            CreatedAt = now,
            LastUpdatedAt = now,
        };
    }
}
