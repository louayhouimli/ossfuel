using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.IntegrationTests.Infrastructure;

public class ProjectsWebApplicationFactory : WebApplicationFactory<Program>
{
    private static long _nextGitHubId = 1_000_000;

    private readonly string _databaseName = $"ossfuel-projects-{Guid.NewGuid()}";

    public FakeGitHubRepositoryClient GitHub { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Authentication:GitHub:ClientId", "test-client-id");
        builder.UseSetting("Authentication:GitHub:ClientSecret", "test-client-secret");

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<DbContextOptions<OSSFuelDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OSSFuelDbContext>>();
            services.RemoveAll<OSSFuelDbContext>();
            services.AddDbContext<OSSFuelDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IGitHubRepositoryClient>();
            services.AddSingleton<IGitHubRepositoryClient>(GitHub);
        });
    }

    public HttpClient CreateClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        return client;
    }

    public Guid SeedUser(string login = "octocat", string? accessToken = "github-access-token")
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<OSSFuelDbContext>();
        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            GitHubId = Interlocked.Increment(ref _nextGitHubId),
            Login = login,
            CreatedAt = now,
            LastLoginAt = now,
        };

        if (accessToken is not null)
        {
            user.GitHubAccessToken = provider.GetRequiredService<IGitHubTokenProtector>().Protect(accessToken);
            user.GitHubAccessTokenUpdatedAt = now;
        }

        db.Users.Add(user);
        db.SaveChanges();

        return user.Id;
    }

    public Guid SeedProject(
        Guid ownerUserId,
        long gitHubRepoId = 1296269,
        string fullName = "octocat/Hello-World",
        int stars = 4821,
        DateTimeOffset? lastSyncedAt = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var parts = fullName.Split('/');

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            GitHubRepoId = gitHubRepoId,
            FullName = fullName,
            OwnerLogin = parts[0],
            Name = parts[1],
            HtmlUrl = $"https://github.com/{fullName}",
            Description = "My first repository",
            Stars = stars,
            Forks = 321,
            OpenIssues = 12,
            Language = "C#",
            DefaultBranch = "main",
            Topics = ["dotnet"],
            CreatedAt = now.AddDays(-1),
            LastSyncedAt = lastSyncedAt ?? now.AddDays(-1),
        };

        db.Projects.Add(project);
        db.SaveChanges();

        return project.Id;
    }
}
