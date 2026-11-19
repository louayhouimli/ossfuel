using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OSSFuel.Api.Features.Discovery.Assistant;
using OSSFuel.Api.Features.Discovery.Ingestion;
using OSSFuel.Api.Features.Discovery.Retrieval;

namespace OSSFuel.Api.Features.Discovery;

public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discovery").WithTags("Discovery");

        group.MapPost("/projects/{id:guid}/index", IndexProjectAsync);
        group.MapPost("/projects/bulk-index", BulkIndexProjectsAsync);
        group.MapPost("/search", SearchProjectsAsync);
        group.MapPost("/assistant/chat", ChatAsync);

        app.MapPost("/assistant/chat", ChatAsync).WithTags("Assistant");

        return app;
    }

    private static async Task<IResult> ChatAsync(
        AssistantChatRequest request,
        IProjectAssistantService assistantService,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request cannot be null." });
        }

        try
        {
            var response = await assistantService.ChatAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SearchProjectsAsync(
        ProjectSearchQuery query,
        IProjectRetrievalService retrievalService,
        CancellationToken cancellationToken)
    {
        if (query is null || string.IsNullOrWhiteSpace(query.Query))
        {
            return Results.BadRequest(new { error = "Search query cannot be empty or whitespace." });
        }

        try
        {
            var results = await retrievalService.SearchProjectsAsync(query, cancellationToken);
            return Results.Ok(results);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> IndexProjectAsync(
        Guid id,
        IProjectIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        var result = await ingestionService.IngestProjectAsync(id, cancellationToken);
        if (!result.Succeeded && result.Error == "Project not found")
        {
            return Results.NotFound();
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> BulkIndexProjectsAsync(
        IProjectIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        var results = await ingestionService.IngestAllProjectsAsync(cancellationToken);
        return Results.Ok(results);
    }
}
