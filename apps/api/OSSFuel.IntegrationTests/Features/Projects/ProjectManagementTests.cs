using System.Net;
using System.Net.Http.Json;

namespace OSSFuel.IntegrationTests.Features.Projects;

public class ProjectManagementTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    // ---------- delete ----------

    [Fact]
    public async Task DeleteProject_WhenOwner_ReturnsNoContentAndRemovesIt()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory.CreateClientFor(ownerId).DeleteAsync($"/projects/{projectId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var reread = await _factory.CreateClient().GetAsync($"/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NotFound, reread.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_WhenNotOwner_ReturnsForbiddenAndKeepsIt()
    {
        var ownerId = _factory.SeedUser(login: "owner");
        var otherId = _factory.SeedUser(login: "someone-else");
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory.CreateClientFor(otherId).DeleteAsync($"/projects/{projectId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var reread = await _factory.CreateClient().GetAsync($"/projects/{projectId}");
        Assert.Equal(HttpStatusCode.OK, reread.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var userId = _factory.SeedUser();

        var response = await _factory.CreateClientFor(userId).DeleteAsync($"/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory.CreateClient().DeleteAsync($"/projects/{projectId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- refresh ----------

    [Fact]
    public async Task RefreshProject_WhenOwner_UpdatesSnapshotFromGitHub()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId, stars: 10);
        _factory.GitHub.Repository = TestRepository.Create(stars: 4821, description: "Refreshed");

        var response = await _factory.CreateClientFor(ownerId).PostAsync($"/projects/{projectId}/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectPayload>();
        Assert.NotNull(payload);
        Assert.Equal(4821, payload!.Stars);
        Assert.Equal("Refreshed", payload.Description);
        Assert.Equal("github-access-token", _factory.GitHub.LastAccessToken);
        Assert.Equal("octocat/Hello-World", _factory.GitHub.LastRequestedFullName);
    }

    [Fact]
    public async Task RefreshProject_WhenOwner_AdvancesLastSyncedAt()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.GitHub.Repository = TestRepository.Create();

        var response = await _factory.CreateClientFor(ownerId).PostAsync($"/projects/{projectId}/refresh", null);
        var payload = await response.Content.ReadFromJsonAsync<ProjectPayload>();

        Assert.NotNull(payload);
        Assert.True(
            payload!.LastSyncedAt > DateTimeOffset.UtcNow.AddMinutes(-1),
            "a refresh should stamp LastSyncedAt with roughly now");
    }

    [Fact]
    public async Task RefreshProject_WhenNotOwner_ReturnsForbidden()
    {
        var ownerId = _factory.SeedUser(login: "owner");
        var otherId = _factory.SeedUser(login: "someone-else");
        var projectId = _factory.SeedProject(ownerId);
        _factory.GitHub.Repository = TestRepository.Create();

        var response = await _factory.CreateClientFor(otherId).PostAsync($"/projects/{projectId}/refresh", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RefreshProject_WhenGitHubCannotFindIt_ReturnsNotFoundAndKeepsSnapshot()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId, stars: 10);
        _factory.GitHub.Repository = null;

        var response = await _factory.CreateClientFor(ownerId).PostAsync($"/projects/{projectId}/refresh", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var payload = await _factory.CreateClient().GetFromJsonAsync<ProjectPayload>($"/projects/{projectId}");
        Assert.Equal(10, payload!.Stars);
    }

    [Fact]
    public async Task RefreshProject_WhenGitHubUnavailable_ReturnsBadGateway()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.GitHub.GetException = new HttpRequestException("github is down");

        var response = await _factory.CreateClientFor(ownerId).PostAsync($"/projects/{projectId}/refresh", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task RefreshProject_WhenNoStoredToken_ReturnsForbidden()
    {
        var ownerId = _factory.SeedUser(accessToken: null);
        var projectId = _factory.SeedProject(ownerId);
        _factory.GitHub.Repository = TestRepository.Create();

        var response = await _factory.CreateClientFor(ownerId).PostAsync($"/projects/{projectId}/refresh", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RefreshProject_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var userId = _factory.SeedUser();

        var response = await _factory.CreateClientFor(userId).PostAsync($"/projects/{Guid.NewGuid()}/refresh", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RefreshProject_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var response = await _factory.CreateClient().PostAsync($"/projects/{projectId}/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- detail contract ----------

    [Fact]
    public async Task GetProject_ExposesTheRegisteringUser()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var payload = await _factory.CreateClient().GetFromJsonAsync<ProjectPayload>($"/projects/{projectId}");

        Assert.Equal(ownerId, payload!.OwnerUserId);
    }

    // ---------- import ----------

    [Fact]
    public async Task PostProjects_WhenOwnerImportsARepository_ReturnsCreatedProject()
    {
        var userId = _factory.SeedUser();
        _factory.GitHub.Repository = TestRepository.Create();

        var response = await _factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectPayload>();
        Assert.NotNull(payload);
        Assert.Equal("octocat/Hello-World", payload!.FullName);
        Assert.Equal(4821, payload.Stars);
        Assert.Equal(userId, payload.OwnerUserId);
        Assert.Equal($"/projects/{payload.Id}", response.Headers.Location?.ToString());
    }

    private sealed record ProjectPayload(
        Guid Id,
        string FullName,
        string OwnerLogin,
        string Name,
        string? Description,
        string HtmlUrl,
        int Stars,
        int Forks,
        int OpenIssues,
        string? DefaultBranch,
        List<string> Topics,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSyncedAt,
        Guid OwnerUserId);
}
