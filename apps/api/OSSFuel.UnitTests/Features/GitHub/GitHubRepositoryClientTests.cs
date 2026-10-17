using System.Net;
using System.Text;
using OSSFuel.Api.Features.GitHub;

namespace OSSFuel.UnitTests.Features.GitHub;

public class GitHubRepositoryClientTests
{
    private const string RepoJson = """
        {
          "id": 1296269,
          "name": "Hello-World",
          "full_name": "octocat/Hello-World",
          "owner": { "login": "octocat" },
          "description": "My first repository",
          "html_url": "https://github.com/octocat/Hello-World",
          "homepage": "https://example.com",
          "language": "C#",
          "stargazers_count": 4821,
          "forks_count": 3210,
          "open_issues_count": 17,
          "default_branch": "main",
          "topics": ["dotnet", "cli"],
          "permissions": { "admin": false, "maintain": false, "push": true, "triage": true, "pull": true }
        }
        """;

    [Fact]
    public async Task GetRepositoryAsync_MapsGitHubResponse()
    {
        var handler = new StubHttpMessageHandler(_ => Json(RepoJson));
        var client = CreateClient(handler);

        var repository = await client.GetRepositoryAsync("token", "octocat", "Hello-World", CancellationToken.None);

        Assert.NotNull(repository);
        Assert.Equal(1296269, repository!.Id);
        Assert.Equal("octocat/Hello-World", repository.FullName);
        Assert.Equal("octocat", repository.OwnerLogin);
        Assert.Equal("Hello-World", repository.Name);
        Assert.Equal("My first repository", repository.Description);
        Assert.Equal("https://github.com/octocat/Hello-World", repository.HtmlUrl);
        Assert.Equal("https://example.com", repository.Homepage);
        Assert.Equal("C#", repository.Language);
        Assert.Equal(4821, repository.Stars);
        Assert.Equal(3210, repository.Forks);
        Assert.Equal(17, repository.OpenIssues);
        Assert.Equal("main", repository.DefaultBranch);
        Assert.Equal(new[] { "dotnet", "cli" }, repository.Topics.ToArray());
        Assert.True(repository.CanPush);
    }

    [Fact]
    public async Task GetRepositoryAsync_WhenGitHubReturnsNotFound_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var repository = await client.GetRepositoryAsync("token", "octocat", "missing", CancellationToken.None);

        Assert.Null(repository);
    }

    [Fact]
    public async Task ListClaimableRepositoriesAsync_KeepsOnlyRepositoriesTheUserCanPush()
    {
        var handler = new StubHttpMessageHandler(_ => Json($"[{RepoJson}, {RepoJson.Replace("\"push\": true", "\"push\": false")}]"));
        var client = CreateClient(handler);

        var repositories = await client.ListClaimableRepositoriesAsync("token", CancellationToken.None);

        var repository = Assert.Single(repositories);
        Assert.Equal(1296269, repository.Id);
    }

    [Fact]
    public async Task GetRepositoryAsync_SendsBearerTokenAndUserAgent()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return Json(RepoJson);
        });
        var client = CreateClient(handler);

        await client.GetRepositoryAsync("secret-token", "octocat", "Hello-World", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", captured.Headers.Authorization?.Parameter);
        Assert.Contains(captured.Headers.UserAgent, header => header.Product?.Name == "OSSFuel");
        Assert.Equal("/repos/octocat/Hello-World", captured.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetRepositoryAsync_WhenTopicsAreNull_ReturnsAnEmptyList()
    {
        var handler = new StubHttpMessageHandler(_ => Json(RepoWithNullTopicsJson));
        var client = CreateClient(handler);

        var repository = await client.GetRepositoryAsync("token", "octocat", "Hello-World", CancellationToken.None);

        Assert.NotNull(repository);
        Assert.NotNull(repository!.Topics);
        Assert.Empty(repository.Topics);
    }

    [Fact]
    public async Task GetRepositoryAsync_WhenPermissionsAreAbsent_ReportsNoPushAccess()
    {
        var handler = new StubHttpMessageHandler(_ => Json(MinimalRepoJson));
        var client = CreateClient(handler);

        var repository = await client.GetRepositoryAsync("token", "octocat", "Hello-World", CancellationToken.None);

        Assert.NotNull(repository);
        Assert.False(repository!.CanPush);
    }

    private const string MinimalRepoJson = """
        {
          "id": 42,
          "name": "Hello-World",
          "full_name": "octocat/Hello-World",
          "owner": { "login": "octocat" },
          "html_url": "https://github.com/octocat/Hello-World",
          "stargazers_count": 1,
          "forks_count": 2,
          "open_issues_count": 3
        }
        """;

    private const string RepoWithNullTopicsJson = """
        {
          "id": 42,
          "name": "Hello-World",
          "full_name": "octocat/Hello-World",
          "owner": { "login": "octocat" },
          "html_url": "https://github.com/octocat/Hello-World",
          "stargazers_count": 1,
          "forks_count": 2,
          "open_issues_count": 3,
          "topics": null
        }
        """;

    private static GitHubRepositoryClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
