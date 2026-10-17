using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Projects;

public class Project
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public long GitHubRepoId { get; set; }

    public required string FullName { get; set; }

    public required string OwnerLogin { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string HtmlUrl { get; set; }

    public string? HomepageUrl { get; set; }

    public string? Language { get; set; }

    public int Stars { get; set; }

    public int Forks { get; set; }

    public int OpenIssues { get; set; }

    public string? DefaultBranch { get; set; }

    public string[] Topics { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }

    public User Owner { get; set; } = null!;

    public static Project Register(GitHubRepository repository, Guid ownerUserId, DateTimeOffset now)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            FullName = repository.FullName,
            OwnerLogin = repository.OwnerLogin,
            Name = repository.Name,
            HtmlUrl = repository.HtmlUrl,
            CreatedAt = now,
        };

        project.ApplySnapshot(repository, now);

        return project;
    }

    /// <summary>
    /// Copies the GitHub snapshot onto the project. The only writer of the
    /// fields GitHub owns, so import and refresh cannot drift apart.
    /// </summary>
    public void ApplySnapshot(GitHubRepository repository, DateTimeOffset now)
    {
        GitHubRepoId = repository.Id;
        FullName = repository.FullName;
        OwnerLogin = repository.OwnerLogin;
        Name = repository.Name;
        Description = repository.Description;
        HtmlUrl = repository.HtmlUrl;
        HomepageUrl = repository.Homepage;
        Language = repository.Language;
        Stars = repository.Stars;
        Forks = repository.Forks;
        OpenIssues = repository.OpenIssues;
        DefaultBranch = repository.DefaultBranch;
        Topics = [.. repository.Topics];
        LastSyncedAt = now;
    }
}
