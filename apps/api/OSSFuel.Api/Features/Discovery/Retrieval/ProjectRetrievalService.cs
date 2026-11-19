using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery.Gemini;
using OSSFuel.Api.Features.Projects;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace OSSFuel.Api.Features.Discovery.Retrieval;

public class ProjectRetrievalService : IProjectRetrievalService
{
    private readonly OSSFuelDbContext _db;
    private readonly IGeminiClient _geminiClient;

    public ProjectRetrievalService(OSSFuelDbContext db, IGeminiClient geminiClient)
    {
        _db = db;
        _geminiClient = geminiClient;
    }

    public async Task<IReadOnlyList<ProjectSearchResult>> SearchProjectsAsync(
        ProjectSearchQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Query))
        {
            throw new ArgumentException("Search query cannot be empty or whitespace.", nameof(query));
        }

        var queryEmbedding = await _geminiClient.EmbedQueryAsync(query.Query, ct);
        var queryVector = new Vector(queryEmbedding);

        IQueryable<ProjectChunk> chunksQuery = _db.ProjectChunks
            .AsNoTracking()
            .Include(c => c.Project);

        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            var targetLanguage = query.Language.Trim().ToLowerInvariant();
            chunksQuery = chunksQuery.Where(c => c.Project.Language != null && c.Project.Language.ToLower() == targetLanguage);
        }

        if (query.MinStars.HasValue)
        {
            chunksQuery = chunksQuery.Where(c => c.Project.Stars >= query.MinStars.Value);
        }

        if (query.MaxStars.HasValue)
        {
            chunksQuery = chunksQuery.Where(c => c.Project.Stars <= query.MaxStars.Value);
        }

        List<ChunkMatch> candidateChunks;

        if (_db.Database.IsRelational())
        {
            var candidateLimit = Math.Max(50, query.Limit * 10);
            var matches = await chunksQuery
                .Where(c => c.Embedding != null)
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .Take(candidateLimit)
                .Select(c => new
                {
                    Chunk = c,
                    Project = c.Project,
                    Distance = (float)c.Embedding!.CosineDistance(queryVector)
                })
                .ToListAsync(ct);

            candidateChunks = matches
                .Select(m =>
                {
                    m.Chunk.Project = m.Project;
                    return new ChunkMatch(m.Chunk, m.Distance);
                })
                .ToList();
        }
        else
        {
            var allChunks = await chunksQuery.ToListAsync(ct);
            var querySpan = queryEmbedding.Span;

            candidateChunks = new List<ChunkMatch>(allChunks.Count);
            foreach (var chunk in allChunks)
            {
                if (chunk.Embedding is null)
                {
                    continue;
                }

                var distance = CalculateCosineDistance(chunk.Embedding.ToArray(), querySpan);
                candidateChunks.Add(new ChunkMatch(chunk, distance));
            }

            candidateChunks.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        if (query.Topics is { Length: > 0 })
        {
            var cleanTopics = query.Topics
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet();

            if (cleanTopics.Count > 0)
            {
                candidateChunks = candidateChunks
                    .Where(m => m.Chunk.Project.Topics != null &&
                                m.Chunk.Project.Topics.Any(t => cleanTopics.Contains(t.ToLowerInvariant())))
                    .ToList();
            }
        }

        var projectGroups = candidateChunks.GroupBy(c => c.Chunk.ProjectId);
        var results = new List<ProjectSearchResult>();

        foreach (var group in projectGroups)
        {
            var bestChunks = group
                .OrderBy(c => c.Distance)
                .Take(3)
                .Select(c => new ProjectSearchChunkMatch(
                    c.Chunk.Id,
                    c.Chunk.ChunkType,
                    c.Chunk.Content,
                    c.Chunk.HeaderPath,
                    c.Distance))
                .ToList();

            if (bestChunks.Count == 0)
            {
                continue;
            }

            var project = group.First().Chunk.Project;
            var bestDistance = bestChunks[0].CosineDistance;
            var semanticSimilarity = Math.Clamp(1.0f - bestDistance, 0.0f, 1.0f);
            var vitalityScore = CalculateVitalityScore(project);

            var nameBonus = 0.0f;
            if (query.Query.Contains(project.Name, StringComparison.OrdinalIgnoreCase) ||
                query.Query.Contains(project.FullName, StringComparison.OrdinalIgnoreCase))
            {
                nameBonus = 0.35f;
            }
            else if (project.Name.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
            {
                nameBonus = 0.25f;
            }

            var combinedScore = (0.85f * semanticSimilarity) + (0.15f * vitalityScore) + nameBonus;

            results.Add(new ProjectSearchResult(
                project.Id,
                project.FullName,
                project.Name,
                project.Description,
                project.HtmlUrl,
                project.Language,
                project.Stars,
                project.Forks,
                project.OpenIssues,
                project.Topics,
                semanticSimilarity,
                vitalityScore,
                combinedScore,
                bestChunks));
        }

        var limit = query.Limit > 0 ? query.Limit : 5;

        return results
            .OrderByDescending(r => r.CombinedScore)
            .Take(limit)
            .ToList();
    }

    internal static float CalculateVitalityScore(Project project)
    {
        var stars = Math.Max(0, project.Stars);
        var score = (float)(Math.Log10(stars + 1) / 4.0);
        return Math.Clamp(score, 0.0f, 1.0f);
    }

    internal static float CalculateCosineDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 1.0f;
        }

        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            var valA = a[i];
            var valB = b[i];
            dot += valA * valB;
            normA += valA * valA;
            normB += valB * valB;
        }

        if (normA <= 0.0 && normB <= 0.0)
        {
            return 0.0f;
        }

        if (normA <= 0.0 || normB <= 0.0)
        {
            return 1.0f;
        }

        var cosineSimilarity = dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        var cosineDistance = (float)(1.0 - cosineSimilarity);
        return Math.Clamp(cosineDistance, 0.0f, 2.0f);
    }

    private sealed record ChunkMatch(ProjectChunk Chunk, float Distance);
}
