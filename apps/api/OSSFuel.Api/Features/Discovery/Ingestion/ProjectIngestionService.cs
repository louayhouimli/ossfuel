using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery.Gemini;
using OSSFuel.Api.Features.GitHub;
using Pgvector;

namespace OSSFuel.Api.Features.Discovery.Ingestion;

public class ProjectIngestionService : IProjectIngestionService
{
    private readonly OSSFuelDbContext _db;
    private readonly IGitHubRepositoryClient _gitHubClient;
    private readonly IGitHubTokenProtector _tokenProtector;
    private readonly IMarkdownChunker _markdownChunker;
    private readonly IGeminiClient _geminiClient;
    private readonly ILogger<ProjectIngestionService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _bulkDelay;

    public ProjectIngestionService(
        OSSFuelDbContext db,
        IGitHubRepositoryClient gitHubClient,
        IGitHubTokenProtector tokenProtector,
        IMarkdownChunker markdownChunker,
        IGeminiClient geminiClient,
        ILogger<ProjectIngestionService> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? bulkDelay = null)
    {
        _db = db;
        _gitHubClient = gitHubClient;
        _tokenProtector = tokenProtector;
        _markdownChunker = markdownChunker;
        _geminiClient = geminiClient;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _bulkDelay = bulkDelay ?? TimeSpan.FromMilliseconds(500);
    }

    public async Task<ProjectIngestionResult> IngestProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project is null)
        {
            return new ProjectIngestionResult(projectId, string.Empty, 0, false, "Project not found");
        }

        try
        {
            string? accessToken = null;
            if (!string.IsNullOrEmpty(project.Owner?.GitHubAccessToken))
            {
                accessToken = _tokenProtector.Unprotect(project.Owner.GitHubAccessToken);
            }

            string? readme = null;
            try
            {
                readme = await _gitHubClient.GetReadmeAsync(accessToken, project.OwnerLogin, project.Name, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve README for project {FullName}", project.FullName);
            }

            var content = !string.IsNullOrWhiteSpace(readme)
                ? readme
                : (!string.IsNullOrWhiteSpace(project.Description)
                    ? project.Description
                    : $"{project.FullName} repository.");

            var chunkResults = _markdownChunker.ChunkMarkdown(content);
            if (chunkResults.Count == 0)
            {
                chunkResults = [new MarkdownChunkResult(ProjectChunkType.Overview, content.Trim(), null)];
            }

            var texts = chunkResults.Select(c => c.Content).ToList();
            var embeddings = await _geminiClient.EmbedBatchAsync(texts, ct);

            var now = _timeProvider.GetUtcNow();
            var chunks = new List<ProjectChunk>(chunkResults.Count);
            for (var i = 0; i < chunkResults.Count; i++)
            {
                var chunkResult = chunkResults[i];
                Vector? vector = i < embeddings.Count ? new Vector(embeddings[i]) : null;
                chunks.Add(ProjectChunk.Create(
                    projectId,
                    chunkResult.ChunkType,
                    chunkResult.Content,
                    now,
                    chunkResult.HeaderPath,
                    vector));
            }

            IDbContextTransaction? transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(ct)
                : null;

            try
            {
                var existing = await _db.ProjectChunks
                    .Where(c => c.ProjectId == projectId)
                    .ToListAsync(ct);

                if (existing.Count > 0)
                {
                    _db.ProjectChunks.RemoveRange(existing);
                }

                await _db.ProjectChunks.AddRangeAsync(chunks, ct);
                project.LastSyncedAt = now;

                await _db.SaveChangesAsync(ct);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(ct);
                }
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(ct);
                }
                throw;
            }

            return new ProjectIngestionResult(project.Id, project.FullName, chunks.Count, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest project {FullName} ({ProjectId})", project.FullName, projectId);
            return new ProjectIngestionResult(project.Id, project.FullName, 0, false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<ProjectIngestionResult>> IngestAllProjectsAsync(CancellationToken ct = default)
    {
        var projectIds = await _db.Projects
            .AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync(ct);

        var results = new List<ProjectIngestionResult>(projectIds.Count);

        for (var i = 0; i < projectIds.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var projectId = projectIds[i];
            var result = await IngestProjectAsync(projectId, ct);
            results.Add(result);

            if (i < projectIds.Count - 1 && _bulkDelay > TimeSpan.Zero)
            {
                await Task.Delay(_bulkDelay, _timeProvider, ct);
            }
        }

        return results;
    }
}
