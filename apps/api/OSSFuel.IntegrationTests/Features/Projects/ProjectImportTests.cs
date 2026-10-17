using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace OSSFuel.IntegrationTests.Features.Projects;

public class ProjectImportTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory factory = new();

    public void Dispose() => factory.Dispose();

    // ----- the repository picker -----

    [Fact]
    public async Task GetAvailableRepositories_MarksRepositoriesThatAlreadyHaveAProject()
    {
        var userId = factory.SeedUser();
        factory.SeedProject(userId, gitHubRepoId: 1296269);
        factory.GitHub.Repositories =
        [
            TestRepository.Create(id: 1296269),
            TestRepository.Create(id: 222222, name: "Fresh-Repo"),
        ];

        var response = await factory.CreateClientFor(userId).GetAsync("/repos/available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<AvailableRepositoryPayload>>();
        Assert.NotNull(payload);

        var imported = Assert.Single(payload, repository => repository.AlreadyImported);
        Assert.Equal(1296269, imported.Id);

        var fresh = Assert.Single(payload, repository => !repository.AlreadyImported);
        Assert.Equal("octocat/Fresh-Repo", fresh.FullName);
        Assert.Equal("github-access-token", factory.GitHub.LastAccessToken);
    }

    [Fact]
    public async Task GetAvailableRepositories_MarksRepositoriesRegisteredByAnotherUser()
    {
        // A repository belongs to OSSFuel as a whole, so one maintainer registering
        // it has to keep it out of everybody else's picker.
        var owner = factory.SeedUser(login: "owner");
        var other = factory.SeedUser(login: "someone-else");
        factory.SeedProject(owner, gitHubRepoId: 424242, fullName: "owner/Taken");
        factory.GitHub.Repositories = [TestRepository.Create(id: 424242, owner: "owner", name: "Taken")];

        var response = await factory.CreateClientFor(other).GetAsync("/repos/available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<List<AvailableRepositoryPayload>>();
        Assert.True(Assert.Single(payload!).AlreadyImported);
    }

    [Fact]
    public async Task GetAvailableRepositories_WhenNoStoredToken_ReturnsForbiddenAndAsksToReconnect()
    {
        var userId = factory.SeedUser(accessToken: null);

        var response = await factory.CreateClientFor(userId).GetAsync("/repos/available");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Equal("GitHub connection required", problem!.Title);
    }

    [Fact]
    public async Task GetAvailableRepositories_WhenGitHubUnavailable_ReturnsBadGateway()
    {
        var userId = factory.SeedUser();
        factory.GitHub.ListException = new HttpRequestException("github is down");

        var response = await factory.CreateClientFor(userId).GetAsync("/repos/available");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    // ----- registering a repository -----

    [Fact]
    public async Task PostProjects_WhenBodyIsNull_ReturnsValidationProblem()
    {
        var userId = factory.SeedUser();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/projects")
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json"),
        };

        var response = await factory.CreateClientFor(userId).SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostProjects_WhenFullNameHasNoOwner_ReturnsValidationProblemNamingTheField()
    {
        var userId = factory.SeedUser();

        var response = await factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>();
        Assert.NotNull(problem!.Errors);
        Assert.True(problem.Errors.ContainsKey("fullName"));
    }

    [Fact]
    public async Task PostProjects_WhenNoStoredToken_ReturnsForbiddenAndAsksToReconnect()
    {
        var userId = factory.SeedUser(accessToken: null);

        var response = await factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Equal("GitHub connection required", problem!.Title);
    }

    [Fact]
    public async Task PostProjects_WhenGitHubUnavailable_ReturnsBadGateway()
    {
        var userId = factory.SeedUser();
        factory.GitHub.GetException = new HttpRequestException("github is down");

        var response = await factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task PostProjects_WhenGitHubCannotFindRepository_ReturnsNotFound()
    {
        var userId = factory.SeedUser();
        factory.GitHub.Repository = null;

        var response = await factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostProjects_WhenUserCannotPush_ReturnsForbidden()
    {
        var userId = factory.SeedUser();
        factory.GitHub.Repository = TestRepository.Create(canPush: false);

        var response = await factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Equal("Not permitted", problem!.Title);
    }

    [Fact]
    public async Task PostProjects_WhenRepositoryAlreadyHasAProject_ReturnsConflict()
    {
        var userId = factory.SeedUser();
        factory.SeedProject(userId, gitHubRepoId: 1296269);
        factory.GitHub.Repository = TestRepository.Create(id: 1296269);

        var response = await factory
            .CreateClientFor(userId)
            .PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
        Assert.Equal("Repository already registered", problem!.Title);
    }

    private sealed record AvailableRepositoryPayload(
        long Id,
        string FullName,
        string OwnerLogin,
        string Name,
        string? Description,
        string? Language,
        int Stars,
        bool AlreadyImported);

    private sealed record ProblemPayload(int Status, string? Title, string? Detail);

    private sealed record ValidationProblemPayload(
        int Status,
        string? Title,
        Dictionary<string, string[]>? Errors);
}
