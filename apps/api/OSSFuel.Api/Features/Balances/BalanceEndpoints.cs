using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;

namespace OSSFuel.Api.Features.Balances;

public static class BalanceEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    public static IEndpointRouteBuilder MapBalanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").WithTags("Balances");

        // A balance is public project information, same as stars or topics.
        // The sponsor on each credit is public too; the sponsor's full funding
        // list stays private to their account.
        group.MapGet("/projects/{id:guid}/balance", GetBalanceAsync);
        group.MapGet("/projects/{id:guid}/balance/transactions", GetTransactionsAsync);

        return app;
    }

    private static async Task<IResult> GetBalanceAsync(
        Guid id,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(project => project.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var balance = await FindOrOpenBalanceAsync(id, db, cancellationToken);

        return Results.Ok(new BalanceResponse(balance.ProjectId, balance.AmountMinor, balance.Currency));
    }

    private static async Task<IResult> GetTransactionsAsync(
        Guid id,
        OSSFuelDbContext db,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        if (!await db.Projects.AnyAsync(project => project.Id == id, cancellationToken))
        {
            return Results.NotFound();
        }

        var balance = await FindOrOpenBalanceAsync(id, db, cancellationToken);

        var query = db
            .BalanceTransactions
            .AsNoTracking()
            .Where(transaction => transaction.AIBalanceId == balance.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(transaction => new BalanceTransactionResponse(
                transaction.Id,
                transaction.AmountMinor,
                transaction.Type,
                transaction.Status,
                transaction.Description,
                transaction.FundingId == null
                    ? null
                    : db
                        .Fundings
                        .Where(funding => funding.Id == transaction.FundingId)
                        .Select(funding => new BalanceTransactionSponsorResponse(
                            funding.Sponsor.Id,
                            funding.Sponsor.Login,
                            funding.Sponsor.Name,
                            funding.Sponsor.AvatarUrl))
                        .FirstOrDefault(),
                transaction.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new BalanceTransactionListResponse(items, page, pageSize, totalCount));
    }

    /// <summary>
    /// The balance is created with the project, so a missing row means a legacy
    /// project or a lost write. Opening it here keeps a read from becoming a 500
    /// and stays safe under the unique ProjectId index.
    /// </summary>
    private static async Task<AIBalance> FindOrOpenBalanceAsync(
        Guid projectId,
        OSSFuelDbContext db,
        CancellationToken cancellationToken)
    {
        var balance = await db
            .AIBalances
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId, cancellationToken);

        if (balance is not null)
        {
            return balance;
        }

        balance = AIBalance.Open(projectId, DateTimeOffset.UtcNow);
        db.AIBalances.Add(balance);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return balance;
        }
        catch (DbUpdateException)
        {
            db.Entry(balance).State = EntityState.Detached;

            return await db
                .AIBalances
                .FirstAsync(candidate => candidate.ProjectId == projectId, cancellationToken);
        }
    }
}
