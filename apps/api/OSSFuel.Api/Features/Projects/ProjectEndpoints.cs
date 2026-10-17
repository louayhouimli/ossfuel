using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Projects;

public static class ProjectEndpoints
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/projects", GetProjectsAsync);
        app.MapGet("/projects/{id:guid}", GetProjectAsync);
        app.MapPost("/projects", CreateProjectAsync).RequireAuthorization();
        app.MapDelete("/projects/{id:guid}", DeleteProjectAsync).RequireAuthorization();
        app.MapPost("/projects/{id:guid}/refresh", RefreshProjectAsync).RequireAuthorization();
        app.MapGet("/repos/available", GetAvailableRepositoriesAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetProjectsAsync(
        OSSFuelDbContext db,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db
            .Projects
            .AsNoTracking()
            .OrderByDescending(project => project.Stars)
            .ThenBy(project => project.FullName);

        var totalCount = await query.CountAsync(cancellationToken);

        var projects = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = projects.Select(ToSummary).ToList();

        return Results.Ok(new ProjectListResponse(items, page, pageSize, totalCount));
    }

    private static async Task<IResult> GetProjectAsync(
        Guid id,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var project = await db
            .Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return project is null ? Results.NotFound() : Results.Ok(ToResponse(project));
    }

    private static async Task<IResult> DeleteProjectAsync(
        Guid id,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var project = await db
            .Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        if (project.OwnerUserId != user.Id)
        {
            return NotPermittedProblem();
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RefreshProjectAsync(
        Guid id,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        IGitHubRepositoryClient gitHub,
        IGitHubTokenProtector protector,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var project = await db
            .Projects
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        if (project.OwnerUserId != user.Id)
        {
            return NotPermittedProblem();
        }

        var accessToken = protector.Unprotect(user.GitHubAccessToken);

        if (accessToken is null)
        {
            return ReconnectProblem();
        }

        GitHubRepository? repository;

        try
        {
            repository = await gitHub.GetRepositoryAsync(
                accessToken,
                project.OwnerLogin,
                project.Name,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            loggerFactory
                .CreateLogger(nameof(ProjectEndpoints))
                .LogWarning(exception, "Refreshing GitHub repository {FullName} failed", project.FullName);

            return GitHubUnavailableProblem();
        }

        if (repository is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Repository not found",
                detail: $"GitHub no longer returns '{project.FullName}'.");
        }

        project.ApplySnapshot(repository, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(project));
    }

    private static async Task<IResult> GetAvailableRepositoriesAsync(
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        IGitHubRepositoryClient gitHub,
        IGitHubTokenProtector protector,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var accessToken = protector.Unprotect(user.GitHubAccessToken);

        if (accessToken is null)
        {
            return ReconnectProblem();
        }

        IReadOnlyList<GitHubRepository> repositories;

        try
        {
            repositories = await gitHub.ListClaimableRepositoriesAsync(accessToken, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            loggerFactory
                .CreateLogger(nameof(ProjectEndpoints))
                .LogWarning(exception, "Listing GitHub repositories failed for user {UserId}", user.Id);

            return GitHubUnavailableProblem();
        }

        var importedRepoIds = await db
            .Projects
            .AsNoTracking()
            .Select(project => project.GitHubRepoId)
            .ToListAsync(cancellationToken);

        var imported = importedRepoIds.ToHashSet();

        var available = repositories
            .Select(repository => new AvailableRepositoryResponse(
                repository.Id,
                repository.FullName,
                repository.OwnerLogin,
                repository.Name,
                repository.Description,
                repository.Language,
                repository.Stars,
                imported.Contains(repository.Id)))
            .ToList();

        return Results.Ok(available);
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest? request,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        IGitHubRepositoryClient gitHub,
        IGitHubTokenProtector protector,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var fullName = request?.FullName?.Trim();
        var parts = (fullName ?? string.Empty).Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["fullName"] = ["Provide the repository as owner/name."],
            });
        }

        var accessToken = protector.Unprotect(user.GitHubAccessToken);

        if (accessToken is null)
        {
            return ReconnectProblem();
        }

        GitHubRepository? repository;

        try
        {
            repository = await gitHub.GetRepositoryAsync(accessToken, parts[0], parts[1], cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            loggerFactory
                .CreateLogger(nameof(ProjectEndpoints))
                .LogWarning(exception, "Reading GitHub repository {FullName} failed", fullName);

            return GitHubUnavailableProblem();
        }

        if (repository is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Repository not found",
                detail: $"GitHub could not find '{fullName}'.");
        }

        if (!repository.CanPush)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Not permitted",
                detail: "You do not have permission to register this repository.");
        }

        if (await db.Projects.AnyAsync(project => project.GitHubRepoId == repository.Id, cancellationToken))
        {
            return AlreadyRegisteredProblem();
        }

        var project = Project.Register(repository, user.Id, DateTimeOffset.UtcNow);
        db.Projects.Add(project);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent import won the unique index race.
            return AlreadyRegisteredProblem();
        }

        return Results.Created($"/projects/{project.Id}", ToResponse(project));
    }

    private static async Task<User?> LoadCurrentUserAsync(
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            return null;
        }

        return await db.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    private static IResult ReconnectProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "GitHub connection required",
            detail: "Sign in with GitHub again to grant OSSFuel access to your repositories.");

    private static IResult GitHubUnavailableProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "GitHub unavailable",
            detail: "GitHub could not be reached. Try again in a moment.");

    private static IResult NotPermittedProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Not permitted",
            detail: "Only the maintainer who registered this project can change it.");

    private static IResult AlreadyRegisteredProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Repository already registered",
            detail: "This repository already has an OSSFuel project.");

    private static ProjectSummaryResponse ToSummary(Project project) =>
        new(
            project.Id,
            project.FullName,
            project.OwnerLogin,
            project.Name,
            project.Description,
            project.Language,
            project.Stars,
            project.Topics);

    private static ProjectResponse ToResponse(Project project) =>
        new(
            project.Id,
            project.FullName,
            project.OwnerLogin,
            project.Name,
            project.Description,
            project.HtmlUrl,
            project.HomepageUrl,
            project.Language,
            project.Stars,
            project.Forks,
            project.OpenIssues,
            project.DefaultBranch,
            project.Topics,
            project.CreatedAt,
            project.LastSyncedAt,
            project.OwnerUserId);
}
