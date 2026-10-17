namespace OSSFuel.Api.Features.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string FullName,
    string OwnerLogin,
    string Name,
    string? Description,
    string HtmlUrl,
    string? HomepageUrl,
    string? Language,
    int Stars,
    int Forks,
    int OpenIssues,
    string? DefaultBranch,
    IReadOnlyList<string> Topics,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSyncedAt,
    Guid OwnerUserId);

public sealed record ProjectSummaryResponse(
    Guid Id,
    string FullName,
    string OwnerLogin,
    string Name,
    string? Description,
    string? Language,
    int Stars,
    IReadOnlyList<string> Topics);

public sealed record ProjectListResponse(
    IReadOnlyList<ProjectSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AvailableRepositoryResponse(
    long Id,
    string FullName,
    string OwnerLogin,
    string Name,
    string? Description,
    string? Language,
    int Stars,
    bool AlreadyImported);

public sealed record CreateProjectRequest(string? FullName);
