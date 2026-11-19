using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery;
using OSSFuel.Api.Features.Discovery.Gemini;
using OSSFuel.Api.Features.Discovery.Ingestion;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.UnitTests.Features.Discovery;

public class ProjectIngestionServiceTests
{
    private readonly OSSFuelDbContext _db;
    private readonly TestGitHubClient _gitHubClient = new();
    private readonly TestTokenProtector _tokenProtector = new();
    private readonly IMarkdownChunker _markdownChunker = new MarkdownChunker();
    private readonly TestGeminiClient _geminiClient = new();
    private readonly ProjectIngestionService _sut;

    public ProjectIngestionServiceTests()
    {
        var options = new DbContextOptionsBuilder<OSSFuelDbContext>()
            .UseInMemoryDatabase($"ossfuel-ingestion-test-{Guid.NewGuid()}")
            .Options;

        _db = new OSSFuelDbContext(options);

        _sut = new ProjectIngestionService(
            _db,
            _gitHubClient,
            _tokenProtector,
            _markdownChunker,
            _geminiClient,
            NullLogger<ProjectIngestionService>.Instance,
            TimeProvider.System,
            TimeSpan.Zero);
    }

    [Fact]
    public async Task IngestProjectAsync_WithValidReadme_CreatesChunksAndEmbeddings()
    {
        var user = CreateUser("gh-token-123");
        var project = CreateProject(user, "My project description");
        _db.Users.Add(user);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _gitHubClient.ReadmeToReturn = """
            # Awesome Project
            This is an awesome open-source project.

            ## Features
            - Feature A
            - Feature B

            ## Architecture
            Built with modern .NET and PostgreSQL.
            """;

        var initialSync = project.LastSyncedAt;

        var result = await _sut.IngestProjectAsync(project.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(project.Id, result.ProjectId);
        Assert.Equal(project.FullName, result.ProjectFullName);
        Assert.True(result.ChunksCount >= 3);
        Assert.Null(result.Error);

        Assert.Equal("gh-token-123", _gitHubClient.CapturedToken);
        Assert.Equal(project.OwnerLogin, _gitHubClient.CapturedOwner);
        Assert.Equal(project.Name, _gitHubClient.CapturedName);

        var savedChunks = await _db.ProjectChunks
            .Where(c => c.ProjectId == project.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        Assert.Equal(result.ChunksCount, savedChunks.Count);
        Assert.Contains(savedChunks, c => c.ChunkType == ProjectChunkType.Overview);
        Assert.Contains(savedChunks, c => c.ChunkType == ProjectChunkType.Features);
        Assert.Contains(savedChunks, c => c.ChunkType == ProjectChunkType.Architecture);

        var reloadedProject = await _db.Projects.FindAsync(project.Id);
        Assert.NotNull(reloadedProject);
        Assert.True(reloadedProject!.LastSyncedAt > initialSync);

        Assert.Single(_geminiClient.CapturedBatchCalls);
        Assert.Equal(result.ChunksCount, _geminiClient.CapturedBatchCalls[0].Count);
    }

    [Fact]
    public async Task IngestProjectAsync_WhenReadmeIs404_FallsBackToProjectDescription()
    {
        var user = CreateUser();
        var project = CreateProject(user, "Detailed project description for fallback.");
        _db.Users.Add(user);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _gitHubClient.ReadmeToReturn = null;

        var result = await _sut.IngestProjectAsync(project.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ChunksCount);

        var savedChunks = await _db.ProjectChunks
            .Where(c => c.ProjectId == project.Id)
            .ToListAsync();

        var chunk = Assert.Single(savedChunks);
        Assert.Equal(ProjectChunkType.Overview, chunk.ChunkType);
        Assert.Contains("Detailed project description for fallback.", chunk.Content);
    }

    [Fact]
    public async Task IngestProjectAsync_WhenReadmeAndDescriptionAreNull_FallsBackToMinimalOverview()
    {
        var user = CreateUser();
        var project = CreateProject(user, null);
        _db.Users.Add(user);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _gitHubClient.ReadmeToReturn = null;

        var result = await _sut.IngestProjectAsync(project.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ChunksCount);

        var savedChunks = await _db.ProjectChunks
            .Where(c => c.ProjectId == project.Id)
            .ToListAsync();

        var chunk = Assert.Single(savedChunks);
        Assert.Equal(ProjectChunkType.Overview, chunk.ChunkType);
        Assert.Contains(project.FullName, chunk.Content);
    }

    [Fact]
    public async Task IngestProjectAsync_ReIndexing_DeletesPreviousChunksAndSavesFreshChunks()
    {
        var user = CreateUser();
        var project = CreateProject(user, "Desc");
        _db.Users.Add(user);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _gitHubClient.ReadmeToReturn = """
            # Initial Version
            First version overview.

            ## Features
            Features v1.
            """;

        var firstResult = await _sut.IngestProjectAsync(project.Id);
        Assert.True(firstResult.Succeeded);

        var firstChunks = await _db.ProjectChunks
            .Where(c => c.ProjectId == project.Id)
            .ToListAsync();
        Assert.Equal(firstResult.ChunksCount, firstChunks.Count);

        _gitHubClient.ReadmeToReturn = """
            # Updated Version
            Second version overview.
            """;

        var secondResult = await _sut.IngestProjectAsync(project.Id);
        Assert.True(secondResult.Succeeded);

        var secondChunks = await _db.ProjectChunks
            .Where(c => c.ProjectId == project.Id)
            .ToListAsync();

        Assert.Equal(secondResult.ChunksCount, secondChunks.Count);
        Assert.DoesNotContain(secondChunks, c => firstChunks.Select(fc => fc.Id).Contains(c.Id));
        Assert.Contains(secondChunks, c => c.Content.Contains("Second version overview."));
        Assert.DoesNotContain(secondChunks, c => c.Content.Contains("Features v1."));
    }

    [Fact]
    public async Task IngestProjectAsync_WhenProjectNotFound_ReturnsNotFoundFailure()
    {
        var nonExistentId = Guid.NewGuid();

        var result = await _sut.IngestProjectAsync(nonExistentId);

        Assert.False(result.Succeeded);
        Assert.Equal(nonExistentId, result.ProjectId);
        Assert.Equal("Project not found", result.Error);
    }

    [Fact]
    public async Task IngestProjectAsync_WhenGeminiClientFails_ReturnsFailureResult()
    {
        var user = CreateUser();
        var project = CreateProject(user, "Project description");
        _db.Users.Add(user);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _gitHubClient.ReadmeToReturn = "# Readme\nTest content";
        _geminiClient.Handler = _ => throw new InvalidOperationException("Gemini API rate limit exceeded");

        var result = await _sut.IngestProjectAsync(project.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(project.Id, result.ProjectId);
        Assert.Equal(0, result.ChunksCount);
        Assert.Contains("Gemini API rate limit exceeded", result.Error);

        var chunks = await _db.ProjectChunks
            .Where(c => c.ProjectId == project.Id)
            .ToListAsync();
        Assert.Empty(chunks);
    }

    [Fact]
    public async Task IngestAllProjectsAsync_IngestsAllProjectsInDatabase()
    {
        var user = CreateUser();
        var project1 = CreateProject(user, "Project 1 description", name: "repo-1");
        var project2 = CreateProject(user, "Project 2 description", name: "repo-2");
        _db.Users.Add(user);
        _db.Projects.AddRange(project1, project2);
        await _db.SaveChangesAsync();

        _gitHubClient.ReadmeToReturn = "# Readme\nContent";

        var results = await _sut.IngestAllProjectsAsync();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Succeeded));
        Assert.Contains(results, r => r.ProjectId == project1.Id);
        Assert.Contains(results, r => r.ProjectId == project2.Id);

        var allChunks = await _db.ProjectChunks.ToListAsync();
        Assert.Contains(allChunks, c => c.ProjectId == project1.Id);
        Assert.Contains(allChunks, c => c.ProjectId == project2.Id);
    }

    private static User CreateUser(string? rawToken = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            GitHubId = 12345,
            Login = "octocat",
            GitHubAccessToken = rawToken is not null ? $"protected:{rawToken}" : null,
            CreatedAt = DateTimeOffset.UtcNow,
            LastLoginAt = DateTimeOffset.UtcNow,
        };

    private static Project CreateProject(User user, string? description, string name = "hello-world") =>
        new()
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Owner = user,
            GitHubRepoId = Random.Shared.Next(1000, 99999),
            FullName = $"{user.Login}/{name}",
            OwnerLogin = user.Login,
            Name = name,
            Description = description,
            HtmlUrl = $"https://github.com/{user.Login}/{name}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

    private sealed class TestGitHubClient : IGitHubRepositoryClient
    {
        public string? ReadmeToReturn { get; set; }
        public string? CapturedToken { get; private set; }
        public string? CapturedOwner { get; private set; }
        public string? CapturedName { get; private set; }

        public Task<string?> GetReadmeAsync(string? accessToken, string owner, string name, CancellationToken cancellationToken = default)
        {
            CapturedToken = accessToken;
            CapturedOwner = owner;
            CapturedName = name;
            return Task.FromResult(ReadmeToReturn);
        }

        public Task<IReadOnlyList<GitHubRepository>> ListClaimableRepositoriesAsync(string accessToken, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<GitHubRepository?> GetRepositoryAsync(string accessToken, string owner, string name, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class TestGeminiClient : IGeminiClient
    {
        public List<IReadOnlyList<string>> CapturedBatchCalls { get; } = [];
        public Func<IReadOnlyList<string>, IReadOnlyList<ReadOnlyMemory<float>>>? Handler { get; set; }

        public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            CapturedBatchCalls.Add(texts);
            if (Handler is not null)
            {
                return Task.FromResult(Handler(texts));
            }

            var results = texts.Select(_ => (ReadOnlyMemory<float>)new float[ProjectChunk.VectorDimensions]).ToList();
            return Task.FromResult<IReadOnlyList<ReadOnlyMemory<float>>>(results);
        }

        public Task<ReadOnlyMemory<float>> EmbedQueryAsync(string query, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<string> GenerateContentAsync(string prompt, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<string> StreamContentAsync(string prompt, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class TestTokenProtector : IGitHubTokenProtector
    {
        public string Protect(string accessToken) => $"protected:{accessToken}";
        public string? Unprotect(string? protectedAccessToken) =>
            protectedAccessToken?.StartsWith("protected:") == true
                ? protectedAccessToken["protected:".Length..]
                : protectedAccessToken;
    }
}
