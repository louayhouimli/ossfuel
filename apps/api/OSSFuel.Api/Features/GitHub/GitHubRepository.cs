namespace OSSFuel.Api.Features.GitHub;

public sealed record GitHubRepository(
    long Id,
    string FullName,
    string OwnerLogin,
    string Name,
    string? Description,
    string HtmlUrl,
    string? Homepage,
    string? Language,
    int Stars,
    int Forks,
    int OpenIssues,
    string? DefaultBranch,
    IReadOnlyList<string> Topics,
    bool CanPush);
