using OSSFuel.Api.Features.Projects;

namespace OSSFuel.Api.Features.Users;

public class User
{
    public Guid Id { get; set; }

    public long GitHubId { get; set; }

    public required string Login { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }

    public string? GitHubAccessToken { get; set; }

    public DateTimeOffset? GitHubAccessTokenUpdatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastLoginAt { get; set; }

    public ICollection<Project> Projects { get; set; } = [];
}
