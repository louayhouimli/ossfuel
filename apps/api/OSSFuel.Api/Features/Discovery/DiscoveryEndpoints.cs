using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OSSFuel.Api.Features.Discovery.Ingestion;

namespace OSSFuel.Api.Features.Discovery;

public static class DiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/discovery").WithTags("Discovery");

        group.MapPost("/projects/{id:guid}/index", IndexProjectAsync);
        group.MapPost("/projects/bulk-index", BulkIndexProjectsAsync);

        return app;
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
