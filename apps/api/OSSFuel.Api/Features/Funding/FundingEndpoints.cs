using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OSSFuel.Api.Common;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Funding;

public static class FundingEndpoints
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapFundingEndpoints(
        this IEndpointRouteBuilder app,
        bool includeMockEndpoints)
    {
        app.MapPost("/fundings", CreateFundingAsync)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingSetup.FundingCreatePolicy);
        app.MapGet("/fundings", GetFundingsAsync).RequireAuthorization();
        app.MapGet("/fundings/{id:guid}", GetFundingAsync).RequireAuthorization();
        app.MapPost("/fundings/{id:guid}/cancel", CancelFundingAsync).RequireAuthorization();

        if (includeMockEndpoints)
        {
            app.MapPost("/fundings/{id:guid}/mock-complete", CompleteMockPaymentAsync).RequireAuthorization();
        }

        return app;
    }

    private static async Task<IResult> CreateFundingAsync(
        CreateFundingRequest? request,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        IPaymentProvider payments,
        IOptions<FundingOptions> options,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (request is null || request.ProjectId == Guid.Empty)
        {
            return ValidationProblem("projectId", "Choose a project to fund.");
        }

        var limits = options.Value;

        if (request.AmountMinor < limits.MinAmountMinor || request.AmountMinor > limits.MaxAmountMinor)
        {
            return ValidationProblem(
                "amountMinor",
                $"Enter an amount between {limits.MinAmountMinor / 100} and {limits.MaxAmountMinor / 100} USD.");
        }

        var project = await db
            .Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.ProjectId, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        var pending = await db
            .Fundings
            .FirstOrDefaultAsync(
                funding => funding.SponsorUserId == user.Id
                    && funding.ProjectId == project.Id
                    && funding.Status == FundingStatus.Pending,
                cancellationToken);

        if (pending is not null)
        {
            return pending.AmountMinor == request.AmountMinor
                ? Results.Ok(ToResponse(pending, project.FullName, includeCheckoutUrl: true))
                : Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Funding already pending",
                    detail: "Finish or wait for your pending funding.");
        }

        if (!payments.IsConfigured)
        {
            return PaymentsNotConfiguredProblem();
        }

        var now = DateTimeOffset.UtcNow;
        var funding = Funding.Create(project.Id, user.Id, request.AmountMinor, now);
        db.Fundings.Add(funding);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var session = await payments.CreateCheckoutSessionAsync(funding, cancellationToken);
            funding.MarkCheckoutCreated(session.SessionId, session.CheckoutUrl, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            funding.MarkFailed("The checkout session could not be created.", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Payment provider unavailable",
                detail: "The payment provider is unavailable. Try again in a moment.");
        }

        return Results.Created(
            $"/fundings/{funding.Id}",
            ToResponse(funding, project.FullName, includeCheckoutUrl: true));
    }

    private static async Task<IResult> GetFundingsAsync(
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db
            .Fundings
            .AsNoTracking()
            .Include(funding => funding.Project)
            .Where(funding => funding.SponsorUserId == user.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(funding => funding.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(funding => new FundingResponse(
                funding.Id,
                funding.ProjectId,
                funding.Project.FullName,
                funding.AmountMinor,
                funding.Currency,
                funding.Status.ToString().ToLowerInvariant(),
                null,
                funding.CreatedAt,
                funding.CompletedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new FundingListResponse(items, page, pageSize, totalCount));
    }

    private static async Task<IResult> GetFundingAsync(
        Guid id,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var funding = await db
            .Fundings
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (funding is null)
        {
            return Results.NotFound();
        }

        return funding.SponsorUserId == user.Id
            ? Results.Ok(ToResponse(funding, funding.Project.FullName, includeCheckoutUrl: funding.Status == FundingStatus.Pending))
            : NotPermittedProblem();
    }

    private static async Task<IResult> CancelFundingAsync(
        Guid id,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        FundingCreditService credits,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var funding = await db
            .Fundings
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (funding is null)
        {
            return Results.NotFound();
        }

        if (funding.SponsorUserId != user.Id)
        {
            return NotPermittedProblem();
        }

        if (funding.Status != FundingStatus.Pending)
        {
            return Results.Ok(ToResponse(funding, funding.Project.FullName));
        }

        var now = DateTimeOffset.UtcNow;

        await credits.ApplyAsync(
            new PaymentNotification(
                EventId: $"cancel:{funding.Id:N}:{now:O}",
                Type: "payment.cancelled",
                ObjectId: funding.ProviderCheckoutSessionId ?? funding.Id.ToString("N"),
                AmountMinor: funding.AmountMinor,
                Currency: funding.Currency,
                PaymentIntentId: null,
                Action: PaymentAction.Cancel,
                Provider: funding.Provider),
            cancellationToken);

        var current = await db
            .Fundings
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .FirstAsync(candidate => candidate.Id == id, cancellationToken);

        return Results.Ok(ToResponse(current, current.Project.FullName));
    }

    private static async Task<IResult> CompleteMockPaymentAsync(
        Guid id,
        MockCompleteRequest? request,
        ClaimsPrincipal principal,
        OSSFuelDbContext db,
        FundingCreditService credits,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUser.LoadAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var funding = await db
            .Fundings
            .Include(candidate => candidate.Project)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (funding is null)
        {
            return Results.NotFound();
        }

        if (funding.SponsorUserId != user.Id)
        {
            return NotPermittedProblem();
        }

        var outcome = request?.Outcome?.Trim().ToLowerInvariant();

        var action = outcome switch
        {
            "succeeded" => PaymentAction.Credit,
            "failed" => PaymentAction.Fail,
            "cancelled" => PaymentAction.Cancel,
            _ => (PaymentAction?)null,
        };

        if (action is null)
        {
            return ValidationProblem("outcome", "Choose succeeded, failed, or cancelled.");
        }

        if (funding.Status != FundingStatus.Pending)
        {
            return Results.Ok(ToResponse(funding, funding.Project.FullName));
        }

        var now = DateTimeOffset.UtcNow;

        await credits.ApplyAsync(
            new PaymentNotification(
                EventId: $"mock:{funding.Id:N}:{now:O}",
                Type: $"mock.payment.{outcome}",
                ObjectId: funding.ProviderCheckoutSessionId ?? funding.Id.ToString("N"),
                AmountMinor: funding.AmountMinor,
                Currency: funding.Currency,
                PaymentIntentId: action == PaymentAction.Credit ? $"mock_pi_{funding.Id:N}" : null,
                Action: action.Value,
                Provider: funding.Provider),
            cancellationToken);

        var current = await db
            .Fundings
            .AsNoTracking()
            .Include(candidate => candidate.Project)
            .FirstAsync(candidate => candidate.Id == id, cancellationToken);

        return Results.Ok(ToResponse(current, current.Project.FullName));
    }

    private static FundingResponse ToResponse(
        Funding funding,
        string projectFullName,
        bool includeCheckoutUrl = false) =>
        new(
            funding.Id,
            funding.ProjectId,
            projectFullName,
            funding.AmountMinor,
            funding.Currency,
            funding.Status.ToString().ToLowerInvariant(),
            includeCheckoutUrl ? funding.ProviderCheckoutUrl : null,
            funding.CreatedAt,
            funding.CompletedAt);

    private static IResult ValidationProblem(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });

    private static IResult NotPermittedProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Not permitted",
            detail: "This funding belongs to another sponsor.");

    private static IResult PaymentsNotConfiguredProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Payments are not configured",
            detail: "Payments are not configured.");
}
