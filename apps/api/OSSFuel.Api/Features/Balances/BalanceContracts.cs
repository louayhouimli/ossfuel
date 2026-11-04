namespace OSSFuel.Api.Features.Balances;

public sealed record BalanceResponse(Guid ProjectId, long AmountMinor, string Currency);

public sealed record BalanceTransactionSponsorResponse(
    Guid Id,
    string Login,
    string? Name,
    string? AvatarUrl);

public sealed record BalanceTransactionResponse(
    Guid Id,
    long AmountMinor,
    string Type,
    string Status,
    string? Description,
    BalanceTransactionSponsorResponse? Sponsor,
    DateTimeOffset CreatedAt);

public sealed record BalanceTransactionListResponse(
    IReadOnlyList<BalanceTransactionResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
