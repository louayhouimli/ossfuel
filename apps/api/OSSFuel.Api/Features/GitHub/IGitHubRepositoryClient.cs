namespace OSSFuel.Api.Features.GitHub;

public interface IGitHubRepositoryClient
{
    Task<IReadOnlyList<GitHubRepository>> ListClaimableRepositoriesAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<GitHubRepository?> GetRepositoryAsync(
        string accessToken,
        string owner,
        string name,
        CancellationToken cancellationToken);
}
