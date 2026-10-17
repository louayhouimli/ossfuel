using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OSSFuel.Api.Features.GitHub;

public class GitHubRepositoryClient(HttpClient httpClient) : IGitHubRepositoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<IReadOnlyList<GitHubRepository>> ListClaimableRepositoriesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requestUri =
            "user/repos?per_page=100&sort=updated&affiliation=owner,collaborator,organization_member";
        var repositories = await GetAsync<List<RepositoryDto>>(requestUri, accessToken, cancellationToken);

        return (repositories ?? [])
            .Where(repository => repository.Permissions?.Push is true)
            .Select(ToGitHubRepository)
            .ToList();
    }

    public async Task<GitHubRepository?> GetRepositoryAsync(
        string accessToken,
        string owner,
        string name,
        CancellationToken cancellationToken)
    {
        var repository = await GetAsync<RepositoryDto>($"repos/{owner}/{name}", accessToken, cancellationToken);

        return repository is null ? null : ToGitHubRepository(repository);
    }

    private async Task<T?> GetAsync<T>(string requestUri, string accessToken, CancellationToken cancellationToken)
        where T : class
    {
        using var request = CreateRequest(requestUri, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(string requestUri, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("OSSFuel");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static GitHubRepository ToGitHubRepository(RepositoryDto repository) =>
        new(
            repository.Id,
            repository.FullName,
            repository.Owner.Login,
            repository.Name,
            repository.Description,
            repository.HtmlUrl,
            repository.Homepage,
            repository.Language,
            repository.StargazersCount,
            repository.ForksCount,
            repository.OpenIssuesCount,
            repository.DefaultBranch,
            repository.Topics ?? [],
            repository.Permissions?.Push ?? false);

    private sealed class RepositoryDto
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public OwnerDto Owner { get; set; } = new();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
        public string? Homepage { get; set; }
        public string? Language { get; set; }
        public int StargazersCount { get; set; }
        public int ForksCount { get; set; }
        public int OpenIssuesCount { get; set; }
        public string? DefaultBranch { get; set; }
        public List<string>? Topics { get; set; }
        public PermissionsDto? Permissions { get; set; }
    }

    private sealed class OwnerDto
    {
        public string Login { get; set; } = string.Empty;
    }

    private sealed class PermissionsDto
    {
        public bool Push { get; set; }
    }
}
