using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery;
using OSSFuel.Api.Features.Discovery.Gemini;
using OSSFuel.Api.Features.Discovery.Retrieval;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;
using Pgvector;

namespace OSSFuel.UnitTests.Features.Discovery;

public class ProjectRetrievalServiceTests
{
    private readonly OSSFuelDbContext _db;
    private readonly TestGeminiClient _geminiClient = new();
    private readonly ProjectRetrievalService _sut;

    public ProjectRetrievalServiceTests()
    {
        var options = new DbContextOptionsBuilder<OSSFuelDbContext>()
            .UseInMemoryDatabase($"ossfuel-retrieval-test-{Guid.NewGuid()}")
            .Options;

        _db = new OSSFuelDbContext(options);
        _sut = new ProjectRetrievalService(_db, _geminiClient);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchProjectsAsync_WithEmptyOrWhitespaceQuery_ThrowsArgumentException(string query)
    {
        var searchQuery = new ProjectSearchQuery(query);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.SearchProjectsAsync(searchQuery));
    }

    [Fact]
    public async Task SearchProjectsAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.SearchProjectsAsync(null!));
    }

    [Fact]
    public async Task SearchProjectsAsync_WithLanguageFilter_ExcludesMismatchedProjects()
    {
        var user = CreateUser();
        var csharpProject = CreateProject(user, "csharp-lib", language: "C#", stars: 200);
        var rustProject = CreateProject(user, "rust-lib", language: "Rust", stars: 200);
        _db.Users.Add(user);
        _db.Projects.AddRange(csharpProject, rustProject);

        var embedding = CreateNormalizedVector(1.0f, 0.0f);
        _db.ProjectChunks.AddRange(
            CreateChunk(csharpProject.Id, embedding),
            CreateChunk(rustProject.Id, embedding));
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = embedding;

        // Act - search with case-insensitive "c#"
        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("database orm", Language: "c#"));

        // Assert
        Assert.Single(results);
        Assert.Equal(csharpProject.Id, results[0].ProjectId);
        Assert.Equal("C#", results[0].Language);
    }

    [Fact]
    public async Task SearchProjectsAsync_WithMinStarsFilter_ExcludesLowStarProjects()
    {
        var user = CreateUser();
        var highStarProject = CreateProject(user, "popular-repo", stars: 1500);
        var lowStarProject = CreateProject(user, "niche-repo", stars: 50);
        _db.Users.Add(user);
        _db.Projects.AddRange(highStarProject, lowStarProject);

        var embedding = CreateNormalizedVector(1.0f, 0.0f);
        _db.ProjectChunks.AddRange(
            CreateChunk(highStarProject.Id, embedding),
            CreateChunk(lowStarProject.Id, embedding));
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = embedding;

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("search tool", MinStars: 500));

        Assert.Single(results);
        Assert.Equal(highStarProject.Id, results[0].ProjectId);
        Assert.Equal(1500, results[0].Stars);
    }

    [Fact]
    public async Task SearchProjectsAsync_WithMaxStarsFilter_ExcludesHighStarProjects()
    {
        var user = CreateUser();
        var highStarProject = CreateProject(user, "massive-repo", stars: 10000);
        var lowStarProject = CreateProject(user, "hidden-gem", stars: 80);
        _db.Users.Add(user);
        _db.Projects.AddRange(highStarProject, lowStarProject);

        var embedding = CreateNormalizedVector(1.0f, 0.0f);
        _db.ProjectChunks.AddRange(
            CreateChunk(highStarProject.Id, embedding),
            CreateChunk(lowStarProject.Id, embedding));
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = embedding;

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("hidden gem", MaxStars: 200));

        Assert.Single(results);
        Assert.Equal(lowStarProject.Id, results[0].ProjectId);
        Assert.Equal(80, results[0].Stars);
    }

    [Fact]
    public async Task SearchProjectsAsync_WithTopicsFilter_ExcludesMismatchedProjects()
    {
        var user = CreateUser();
        var aiProject = CreateProject(user, "ai-agent", topics: ["rag", "llm", "dotnet"]);
        var webProject = CreateProject(user, "web-app", topics: ["react", "vite", "css"]);
        _db.Users.Add(user);
        _db.Projects.AddRange(aiProject, webProject);

        var embedding = CreateNormalizedVector(1.0f, 0.0f);
        _db.ProjectChunks.AddRange(
            CreateChunk(aiProject.Id, embedding),
            CreateChunk(webProject.Id, embedding));
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = embedding;

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("assistant", Topics: ["rag"]));

        Assert.Single(results);
        Assert.Equal(aiProject.Id, results[0].ProjectId);
    }

    [Fact]
    public async Task SearchProjectsAsync_ScoringProducesProperCombinedScoreRanking()
    {
        var user = CreateUser();
        // High semantic similarity project (lower stars)
        var semanticallyRelevant = CreateProject(user, "semantic-match", stars: 100);
        // Low semantic similarity project (higher stars)
        var popularProject = CreateProject(user, "popular-match", stars: 10000);
        _db.Users.Add(user);
        _db.Projects.AddRange(semanticallyRelevant, popularProject);

        var queryVec = CreateNormalizedVector(1.0f, 0.0f);
        // Close vector (high similarity)
        var closeVec = CreateNormalizedVector(0.95f, 0.31f);
        // Far vector (lower similarity)
        var farVec = CreateNormalizedVector(0.50f, 0.866f);

        _db.ProjectChunks.AddRange(
            CreateChunk(semanticallyRelevant.Id, closeVec),
            CreateChunk(popularProject.Id, farVec));
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = queryVec;

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("vector search"));

        Assert.Equal(2, results.Count);

        // Verify ordering: combined score = (0.70 * semantic) + (0.30 * vitality)
        Assert.True(results[0].CombinedScore >= results[1].CombinedScore);

        foreach (var r in results)
        {
            var expectedScore = (0.70f * r.SemanticSimilarity) + (0.30f * r.VitalityScore);
            Assert.Equal(expectedScore, r.CombinedScore, 0.001f);
            Assert.InRange(r.SemanticSimilarity, 0.0f, 1.0f);
            Assert.InRange(r.VitalityScore, 0.0f, 1.0f);
            Assert.InRange(r.CombinedScore, 0.0f, 1.0f);
        }
    }

    [Fact]
    public async Task SearchProjectsAsync_ValidatesMatchedChunkInclusion()
    {
        var user = CreateUser();
        var project = CreateProject(user, "chunked-repo");
        _db.Users.Add(user);
        _db.Projects.Add(project);

        var queryVec = CreateNormalizedVector(1.0f, 0.0f);

        // Add 5 chunks with varying vectors
        var chunk1 = CreateChunk(project.Id, CreateNormalizedVector(0.99f, 0.14f), ProjectChunkType.Overview, "Chunk 1", "# Overview");
        var chunk2 = CreateChunk(project.Id, CreateNormalizedVector(0.95f, 0.31f), ProjectChunkType.Features, "Chunk 2", "# Features");
        var chunk3 = CreateChunk(project.Id, CreateNormalizedVector(0.90f, 0.43f), ProjectChunkType.Architecture, "Chunk 3", "# Architecture");
        var chunk4 = CreateChunk(project.Id, CreateNormalizedVector(0.70f, 0.71f), ProjectChunkType.Roadmap, "Chunk 4", "# Roadmap");
        var chunk5 = CreateChunk(project.Id, CreateNormalizedVector(0.50f, 0.866f), ProjectChunkType.General, "Chunk 5", "# General");

        _db.ProjectChunks.AddRange(chunk1, chunk2, chunk3, chunk4, chunk5);
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = queryVec;

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("chunk test"));

        Assert.Single(results);
        var res = results[0];

        // Should return top 3 best matching chunks per project
        Assert.Equal(3, res.MatchedChunks.Count);

        // Verified ascending distance order
        Assert.True(res.MatchedChunks[0].CosineDistance <= res.MatchedChunks[1].CosineDistance);
        Assert.True(res.MatchedChunks[1].CosineDistance <= res.MatchedChunks[2].CosineDistance);

        // Top chunk should be chunk1
        Assert.Equal(chunk1.Id, res.MatchedChunks[0].ChunkId);
        Assert.Equal(ProjectChunkType.Overview, res.MatchedChunks[0].ChunkType);
        Assert.Equal("Chunk 1", res.MatchedChunks[0].Content);
        Assert.Equal("# Overview", res.MatchedChunks[0].HeaderPath);
    }

    [Fact]
    public async Task SearchProjectsAsync_RespectsQueryLimit()
    {
        var user = CreateUser();
        _db.Users.Add(user);

        var embedding = CreateNormalizedVector(1.0f, 0.0f);
        for (var i = 0; i < 7; i++)
        {
            var project = CreateProject(user, $"project-{i}", stars: i * 10);
            _db.Projects.Add(project);
            _db.ProjectChunks.Add(CreateChunk(project.Id, embedding));
        }
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = embedding;

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("search", Limit: 3));

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchProjectsAsync_WhenNoChunksMatchFilters_ReturnsEmptyList()
    {
        var user = CreateUser();
        var project = CreateProject(user, "python-project", language: "Python");
        _db.Users.Add(user);
        _db.Projects.Add(project);
        _db.ProjectChunks.Add(CreateChunk(project.Id, CreateNormalizedVector(1.0f, 0.0f)));
        await _db.SaveChangesAsync();

        _geminiClient.QueryVector = CreateNormalizedVector(1.0f, 0.0f);

        var results = await _sut.SearchProjectsAsync(new ProjectSearchQuery("query", Language: "Rust"));

        Assert.Empty(results);
    }

    private static User CreateUser() =>
        new()
        {
            Id = Guid.NewGuid(),
            GitHubId = Random.Shared.Next(1000, 99999),
            Login = "octocat",
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow,
        };

    private static Project CreateProject(
        User user,
        string name,
        string? language = "C#",
        int stars = 100,
        string[]? topics = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Owner = user,
            GitHubRepoId = Random.Shared.Next(10000, 999999),
            FullName = $"{user.Login}/{name}",
            OwnerLogin = user.Login,
            Name = name,
            Description = $"{name} description",
            HtmlUrl = $"https://github.com/{user.Login}/{name}",
            Language = language,
            Stars = stars,
            Forks = 10,
            OpenIssues = 2,
            Topics = topics ?? ["oss", "tool"],
            CreatedAt = DateTimeOffset.UtcNow,
            LastSyncedAt = DateTimeOffset.UtcNow,
        };

    private static ProjectChunk CreateChunk(
        Guid projectId,
        Vector? embedding,
        string chunkType = ProjectChunkType.Overview,
        string content = "Sample content",
        string? headerPath = "# Overview") =>
        ProjectChunk.Create(
            projectId,
            chunkType,
            content,
            DateTimeOffset.UtcNow,
            headerPath,
            embedding);

    private static Vector CreateNormalizedVector(float x, float y)
    {
        var array = new float[ProjectChunk.VectorDimensions];
        array[0] = x;
        array[1] = y;
        return new Vector(array);
    }

    private sealed class TestGeminiClient : IGeminiClient
    {
        public Vector? QueryVector { get; set; }

        public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
            IReadOnlyList<string> texts,
            CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<ReadOnlyMemory<float>> EmbedQueryAsync(
            string query,
            CancellationToken ct = default)
        {
            var floats = QueryVector is not null ? QueryVector.ToArray() : new float[ProjectChunk.VectorDimensions];
            return Task.FromResult<ReadOnlyMemory<float>>(floats);
        }

        public Task<string> GenerateContentAsync(string prompt, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<string> StreamContentAsync(string prompt, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
