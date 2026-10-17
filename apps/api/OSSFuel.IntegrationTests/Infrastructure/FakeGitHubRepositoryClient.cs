using OSSFuel.Api.Features.GitHub;

namespace OSSFuel.IntegrationTests.Infrastructure;

public sealed class FakeGitHubRepositoryClient : IGitHubRepositoryClient
{
    public GitHubRepository? Repository { get; set; }

    public Exception? GetException { get; set; }

    public IReadOnlyList<GitHubRepository> Repositories { get; set; } = [];

    public Exception? ListException { get; set; }

    public string? LastAccessToken { get; private set; }

    public string? LastRequestedFullName { get; private set; }

    public Task<IReadOnlyList<GitHubRepository>> ListClaimableRepositoriesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        LastAccessToken = accessToken;

        return ListException is null
            ? Task.FromResult(Repositories)
            : Task.FromException<IReadOnlyList<GitHubRepository>>(ListException);
    }

    public Task<GitHubRepository?> GetRepositoryAsync(
        string accessToken,
        string owner,
        string name,
        CancellationToken cancellationToken)
    {
        LastAccessToken = accessToken;
        LastRequestedFullName = $"{owner}/{name}";

        return GetException is null
            ? Task.FromResult(Repository)
            : Task.FromException<GitHubRepository?>(GetException);
    }
}

public static class TestRepository
{
    public static GitHubRepository Create(
        long id = 1296269,
        string owner = "octocat",
        string name = "Hello-World",
        string? description = "My first repository",
        int stars = 4821,
        int forks = 321,
        int openIssues = 12,
        string? language = "C#",
        string? defaultBranch = "main",
        string[]? topics = null,
        bool canPush = true) =>
        new(
            id,
            $"{owner}/{name}",
            owner,
            name,
            description,
            $"https://github.com/{owner}/{name}",
            null,
            language,
            stars,
            forks,
            openIssues,
            defaultBranch,
            topics ?? ["dotnet"],
            canPush);
}
