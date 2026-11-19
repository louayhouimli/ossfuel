namespace OSSFuel.Api.Features.Discovery.Ingestion;

public record ProjectIngestionResult(
    Guid ProjectId,
    string ProjectFullName,
    int ChunksCount,
    bool Succeeded,
    string? Error = null);

public interface IProjectIngestionService
{
    Task<ProjectIngestionResult> IngestProjectAsync(Guid projectId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectIngestionResult>> IngestAllProjectsAsync(CancellationToken ct = default);
}
