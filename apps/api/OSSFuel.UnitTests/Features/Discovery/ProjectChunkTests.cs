using OSSFuel.Api.Features.Discovery;
using Pgvector;

namespace OSSFuel.UnitTests.Features.Discovery;

public class ProjectChunkTests
{
    [Fact]
    public void Construction_sets_expected_properties()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var vector = new Vector(new float[ProjectChunk.VectorDimensions]);

        var chunk = new ProjectChunk
        {
            Id = id,
            ProjectId = projectId,
            ChunkType = ProjectChunkType.Overview,
            Content = "Project overview content",
            Embedding = vector,
            HeaderPath = "# Overview",
            CreatedAt = now,
            LastUpdatedAt = now,
        };

        Assert.Equal(id, chunk.Id);
        Assert.Equal(projectId, chunk.ProjectId);
        Assert.Equal(ProjectChunkType.Overview, chunk.ChunkType);
        Assert.Equal("Project overview content", chunk.Content);
        Assert.Same(vector, chunk.Embedding);
        Assert.Equal("# Overview", chunk.HeaderPath);
        Assert.Equal(now, chunk.CreatedAt);
        Assert.Equal(now, chunk.LastUpdatedAt);
    }

    [Fact]
    public void Create_initializes_chunk_with_generated_id_and_timestamps()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var vector = new Vector(new float[ProjectChunk.VectorDimensions]);

        var chunk = ProjectChunk.Create(
            projectId,
            ProjectChunkType.Architecture,
            "Architecture design content",
            now,
            headerPath: "# Architecture > Backend",
            embedding: vector);

        Assert.NotEqual(Guid.Empty, chunk.Id);
        Assert.Equal(projectId, chunk.ProjectId);
        Assert.Equal(ProjectChunkType.Architecture, chunk.ChunkType);
        Assert.Equal("Architecture design content", chunk.Content);
        Assert.Equal("# Architecture > Backend", chunk.HeaderPath);
        Assert.Same(vector, chunk.Embedding);
        Assert.Equal(now, chunk.CreatedAt);
        Assert.Equal(now, chunk.LastUpdatedAt);
    }

    [Theory]
    [InlineData("Overview")]
    [InlineData("Features")]
    [InlineData("Architecture")]
    [InlineData("Roadmap")]
    [InlineData("General")]
    public void Valid_chunk_types_are_accepted(string validChunkType)
    {
        Assert.True(ProjectChunkType.IsValid(validChunkType));
        Assert.True(ProjectChunkTypes.IsValid(validChunkType));

        var chunk = ProjectChunk.Create(
            Guid.NewGuid(),
            validChunkType,
            "Valid content",
            DateTimeOffset.UtcNow);

        Assert.Equal(validChunkType, chunk.ChunkType);
    }

    [Fact]
    public void Expected_chunk_types_are_registered()
    {
        var expected = new[] { "Overview", "Features", "Architecture", "Roadmap", "General" };

        Assert.Equal(expected.OrderBy(x => x), ProjectChunkType.All.OrderBy(x => x));
        Assert.Equal(expected.OrderBy(x => x), ProjectChunkTypes.All.OrderBy(x => x));
    }

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("")]
    [InlineData("overview")]
    [InlineData(null)]
    public void Invalid_chunk_types_are_rejected(string? invalidChunkType)
    {
        Assert.False(ProjectChunkType.IsValid(invalidChunkType));

        if (invalidChunkType is not null)
        {
            Assert.Throws<ArgumentException>(() =>
                ProjectChunk.Create(
                    Guid.NewGuid(),
                    invalidChunkType,
                    "Some content",
                    DateTimeOffset.UtcNow));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_or_whitespace_content(string invalidContent)
    {
        Assert.Throws<ArgumentException>(() =>
            ProjectChunk.Create(
                Guid.NewGuid(),
                ProjectChunkType.General,
                invalidContent,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void VectorDimensions_is_768()
    {
        Assert.Equal(768, ProjectChunk.VectorDimensions);
    }
}
