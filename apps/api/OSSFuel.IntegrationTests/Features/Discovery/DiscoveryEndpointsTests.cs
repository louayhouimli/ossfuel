using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Discovery;

public class DiscoveryEndpointsTests(ProjectsWebApplicationFactory factory)
    : IClassFixture<ProjectsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

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

    private sealed record ProjectIngestionResultPayload(
        Guid ProjectId,
        string ProjectFullName,
        int ChunksCount,
        bool Succeeded,
        string? Error);
}
