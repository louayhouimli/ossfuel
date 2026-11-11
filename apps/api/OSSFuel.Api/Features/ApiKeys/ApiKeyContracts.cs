namespace OSSFuel.Api.Features.ApiKeys;

public sealed record CreateApiKeyRequest(string Name);

public sealed record CreateApiKeyResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string RawKey,
    string KeyPrefix,
    DateTimeOffset CreatedAt);

public sealed record ApiKeySummaryResponse(
    Guid Id,
    Guid ProjectId,
    string Name,
    string KeyPrefix,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);
