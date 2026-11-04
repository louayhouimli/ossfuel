namespace OSSFuel.Api.Features.Funding;

public sealed record CreateFundingRequest(Guid ProjectId, long AmountMinor);

public sealed record MockCompleteRequest(string? Outcome);

public sealed record FundingResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectFullName,
    long AmountMinor,
    string Currency,
    string Status,
    string? CheckoutUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record FundingListResponse(
    IReadOnlyList<FundingResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
