using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery;
using OSSFuel.Api.Features.Discovery.Assistant;
using OSSFuel.Api.Features.Discovery.Retrieval;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Discovery;

public class DiscoveryEndpointsTests(ProjectsWebApplicationFactory factory)
    : IClassFixture<ProjectsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SearchProjects_WithEmptyQuery_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/discovery/search", new ProjectSearchQuery(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchProjects_WithWhitespaceQuery_ReturnsBadRequest(string query)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/discovery/search", new ProjectSearchQuery(query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchProjects_WithValidQuery_ReturnsOkAndCandidateResults()
    {
        var userId = factory.SeedUser("octocat-search-1");
        var projectId = factory.SeedProject(userId, fullName: "octocat-search-1/repo-search");

        factory.GitHub.ReadmeContent = """
            # Searchable Project
            This project provides hybrid vector search.

            ## Features
            - Fast indexing
            - Semantic search
            """;

        // Index project first to create chunks and embeddings
        var indexResponse = await _client.PostAsync($"/api/v1/discovery/projects/{projectId}/index", null);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);

        // Search for project
        var searchResponse = await _client.PostAsJsonAsync(
            "/api/v1/discovery/search",
            new ProjectSearchQuery("vector search"));

        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var results = await searchResponse.Content.ReadFromJsonAsync<List<ProjectSearchResult>>();
        Assert.NotNull(results);
        Assert.NotEmpty(results!);

        var match = results!.FirstOrDefault(r => r.ProjectId == projectId);
        Assert.NotNull(match);
        Assert.Equal("octocat-search-1/repo-search", match!.FullName);
        Assert.NotEmpty(match.MatchedChunks);
        Assert.InRange(match.SemanticSimilarity, 0.0f, 1.0f);
        Assert.InRange(match.VitalityScore, 0.0f, 1.0f);
        Assert.InRange(match.CombinedScore, 0.0f, 1.0f);
    }

    [Fact]
    public async Task SearchProjects_WithFiltering_ExcludesMismatchedProjects()
    {
        var userId = factory.SeedUser("octocat-filter-1");
        var projectId = factory.SeedProject(userId, fullName: "octocat-filter-1/repo-filter");

        factory.GitHub.ReadmeContent = "# Filter Test\nContent for filtering test.";
        await _client.PostAsync($"/api/v1/discovery/projects/{projectId}/index", null);

        // Filter with non-matching language
        var response = await _client.PostAsJsonAsync(
            "/api/v1/discovery/search",
            new ProjectSearchQuery("test", Language: "NonExistentLanguage"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<ProjectSearchResult>>();
        Assert.NotNull(results);
        Assert.DoesNotContain(results!, r => r.ProjectId == projectId);

        // Filter with impossible minimum stars
        var highStarResponse = await _client.PostAsJsonAsync(
            "/api/v1/discovery/search",
            new ProjectSearchQuery("test", MinStars: 999999));

        Assert.Equal(HttpStatusCode.OK, highStarResponse.StatusCode);
        var highStarResults = await highStarResponse.Content.ReadFromJsonAsync<List<ProjectSearchResult>>();
        Assert.NotNull(highStarResults);
        Assert.DoesNotContain(highStarResults!, r => r.ProjectId == projectId);
    }

    [Fact]
    public async Task IndexProject_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.PostAsync($"/api/v1/discovery/projects/{Guid.NewGuid()}/index", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IndexProject_WhenProjectExists_ReturnsOkAndPersistsChunks()
    {
        var userId = factory.SeedUser("octocat-discovery-1");
        var projectId = factory.SeedProject(userId, fullName: "octocat-discovery-1/repo-alpha");

        factory.GitHub.ReadmeContent = """
            # Alpha Project
            Alpha is an awesome framework.

            ## Features
            - Feature 1
            - Feature 2
            """;

        var response = await _client.PostAsync($"/api/v1/discovery/projects/{projectId}/index", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectIngestionResultPayload>();
        Assert.NotNull(payload);
        Assert.True(payload!.Succeeded);
        Assert.Equal(projectId, payload.ProjectId);
        Assert.Equal("octocat-discovery-1/repo-alpha", payload.ProjectFullName);
        Assert.True(payload.ChunksCount >= 2);
        Assert.Null(payload.Error);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
        var chunks = await db.ProjectChunks
            .Where(c => c.ProjectId == projectId)
            .ToListAsync();

        Assert.Equal(payload.ChunksCount, chunks.Count);
        Assert.Contains(chunks, c => c.ChunkType == ProjectChunkType.Overview);
        Assert.Contains(chunks, c => c.ChunkType == ProjectChunkType.Features);
    }

    [Fact]
    public async Task BulkIndexProjects_IndexesAllProjects_ReturnsOkWithList()
    {
        var userId = factory.SeedUser("octocat-discovery-bulk");
        var project1Id = factory.SeedProject(userId, fullName: "octocat-discovery-bulk/bulk-1");
        var project2Id = factory.SeedProject(userId, fullName: "octocat-discovery-bulk/bulk-2");

        factory.GitHub.ReadmeContent = "# Readme\nBulk index content.";

        var response = await _client.PostAsync("/api/v1/discovery/projects/bulk-index", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var results = await response.Content.ReadFromJsonAsync<List<ProjectIngestionResultPayload>>();
        Assert.NotNull(results);
        Assert.Contains(results!, r => r.ProjectId == project1Id && r.Succeeded);
        Assert.Contains(results!, r => r.ProjectId == project2Id && r.Succeeded);
    }

    [Fact]
    public async Task Chat_WithEmptyOrMissingMessages_ReturnsOkWithWelcomeGreeting()
    {
        var request = new AssistantChatRequest([]);

        var response = await _client.PostAsJsonAsync("/api/v1/assistant/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AssistantChatResponse>();
        Assert.NotNull(result);
        Assert.Equal(ProjectAssistantService.DefaultGreeting, result!.Reply);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task Chat_WithValidUserMessage_ReturnsOkWithRecommendations()
    {
        var userId = factory.SeedUser("octocat-chat-1");
        var projectId = factory.SeedProject(userId, fullName: "octocat-chat-1/chat-db");

        factory.GitHub.ReadmeContent = """
            # High Performance Database
            A fast, distributed document database with real-time replication.
            """;

        // Index project
        await _client.PostAsync($"/api/v1/discovery/projects/{projectId}/index", null);

        var chunkId = Guid.NewGuid();
        factory.Gemini.ContentHandler = _ => Task.FromResult($$"""
            ```json
            {
              "reply": "Based on your criteria, I recommend octocat-chat-1/chat-db.",
              "recommendations": [
                {
                  "projectId": "{{projectId}}",
                  "projectName": "octocat-chat-1/chat-db",
                  "matchLevel": "Strong Match",
                  "category": "Database",
                  "technologies": ["C#", "PostgreSQL"],
                  "whyItMatches": "High-performance distributed document database.",
                  "activitySummary": "Active open source project.",
                  "evidence": "Fast, distributed document database with real-time replication.",
                  "citations": [
                    {
                      "chunkId": "{{chunkId}}",
                      "headerPath": "# High Performance Database",
                      "quote": "A fast, distributed document database with real-time replication."
                    }
                  ]
                }
              ]
            }
            ```
            """);

        var request = new AssistantChatRequest([
            new ChatMessage("user", "Looking for a high performance database")
        ]);

        var response = await _client.PostAsJsonAsync("/api/v1/assistant/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AssistantChatResponse>();
        Assert.NotNull(result);
        Assert.Equal("Based on your criteria, I recommend octocat-chat-1/chat-db.", result!.Reply);
        Assert.Single(result.Recommendations);
        Assert.Equal(projectId, result.Recommendations[0].ProjectId);
        Assert.Equal("octocat-chat-1/chat-db", result.Recommendations[0].ProjectName);
        Assert.Single(result.Recommendations[0].Citations);
        Assert.Equal(chunkId, result.Recommendations[0].Citations[0].ChunkId);
    }

    [Fact]
    public async Task Chat_WithUnparseableModelOutput_ReturnsOkWithFallbackRawText()
    {
        factory.Gemini.ContentHandler = _ =>
            Task.FromResult("I am FuelBot. Here is a helpful conversational response without JSON.");

        var request = new AssistantChatRequest([
            new ChatMessage("user", "Hello there!")
        ]);

        var response = await _client.PostAsJsonAsync("/api/v1/assistant/chat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AssistantChatResponse>();
        Assert.NotNull(result);
        Assert.Equal("I am FuelBot. Here is a helpful conversational response without JSON.", result!.Reply);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task Chat_SupportsBothV1AndDiscoveryRoutes()
    {
        var request = new AssistantChatRequest([]);

        var v1Response = await _client.PostAsJsonAsync("/api/v1/assistant/chat", request);
        var discoveryResponse = await _client.PostAsJsonAsync("/api/v1/discovery/assistant/chat", request);

        Assert.Equal(HttpStatusCode.OK, v1Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, discoveryResponse.StatusCode);
    }

    private sealed record ProjectIngestionResultPayload(
        Guid ProjectId,
        string ProjectFullName,
        int ChunksCount,
        bool Succeeded,
        string? Error);
}
