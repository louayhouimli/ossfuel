using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.ApiKeys;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").WithTags("API Keys");

        group.MapGet("/projects/{projectId:guid}/api-keys", GetProjectApiKeysAsync).RequireAuthorization();
        group.MapPost("/projects/{projectId:guid}/api-keys", CreateProjectApiKeyAsync).RequireAuthorization();
        group.MapDelete("/projects/{projectId:guid}/api-keys/{keyId:guid}", RevokeProjectApiKeyAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetProjectApiKeysAsync(
        Guid projectId,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var project = await db
            .Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        if (project.OwnerUserId != user.Id)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "Only the project maintainer can view API keys.");
        }

        var keys = await db
            .ProjectApiKeys
            .AsNoTracking()
            .Where(apiKey => apiKey.ProjectId == projectId)
            .OrderByDescending(apiKey => apiKey.CreatedAt)
            .Select(apiKey => new ApiKeySummaryResponse(
                apiKey.Id,
                apiKey.ProjectId,
                apiKey.Name,
                apiKey.KeyPrefix,
                apiKey.RevokedAt == null,
                apiKey.CreatedAt,
                apiKey.LastUsedAt,
                apiKey.RevokedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(keys);
    }

    private static async Task<IResult> CreateProjectApiKeyAsync(
        Guid projectId,
        CreateApiKeyRequest request,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["API key name is required and must be at most 100 characters."]
            });
        }

        var project = await db
            .Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        if (project.OwnerUserId != user.Id)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "Only the project maintainer can create API keys.");
        }

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();
        var now = DateTimeOffset.UtcNow;

        var apiKey = ProjectApiKey.Create(
            projectId,
            user.Id,
            request.Name,
            keyPrefix,
            keyHash,
            now);

        db.ProjectApiKeys.Add(apiKey);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/v1/projects/{projectId}/api-keys/{apiKey.Id}",
            new CreateApiKeyResponse(
                apiKey.Id,
                apiKey.ProjectId,
                apiKey.Name,
                rawKey,
                apiKey.KeyPrefix,
                apiKey.CreatedAt));
    }

    private static async Task<IResult> RevokeProjectApiKeyAsync(
        Guid projectId,
        Guid keyId,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var project = await db
            .Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        if (project.OwnerUserId != user.Id)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "Only the project maintainer can revoke API keys.");
        }

        var apiKey = await db
            .ProjectApiKeys
            .FirstOrDefaultAsync(
                candidate => candidate.Id == keyId && candidate.ProjectId == projectId,
                cancellationToken);

        if (apiKey is null)
        {
            return Results.NotFound();
        }

        apiKey.Revoke(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}
