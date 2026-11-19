namespace OSSFuel.Api.Features.Discovery.Retrieval;

public interface IProjectRetrievalService
{
    Task<IReadOnlyList<ProjectSearchResult>> SearchProjectsAsync(
        ProjectSearchQuery query,
        CancellationToken ct = default);
}
