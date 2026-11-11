namespace OSSFuel.Api.Features.ApiKeys;

public class ProjectApiKey
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid CreatedByUserId { get; init; }

    public required string Name { get; init; }

    public required string KeyPrefix { get; init; }

    public required string KeyHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    public static ProjectApiKey Create(
        Guid projectId,
        Guid createdByUserId,
        string name,
        string keyPrefix,
        string keyHash,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedByUserId = createdByUserId,
            Name = name.Trim(),
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            CreatedAt = now,
        };

    public void MarkUsed(DateTimeOffset now) => LastUsedAt = now;

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
    }
}
