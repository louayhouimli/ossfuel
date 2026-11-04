using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Funding;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.IntegrationTests.Infrastructure;

public class ProjectsWebApplicationFactory : WebApplicationFactory<Program>
{
    private static long _nextGitHubId = 1_000_000;

    private readonly string _databaseName = $"ossfuel-projects-{Guid.NewGuid()}";

    public FakeGitHubRepositoryClient GitHub { get; } = new();

    public FakePaymentProvider Payments { get; } = new();

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

            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(Payments);
        });
    }

    public HttpClient CreateClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        return client;
    }

    public Guid SeedUser(
        string login = "octocat",
        string? accessToken = "github-access-token",
        string? avatarUrl = null)
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
            Name = $"{login} name",
            AvatarUrl = avatarUrl,
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
        db.AIBalances.Add(AIBalance.Open(project.Id, now));
        db.SaveChanges();

        return project.Id;
    }

    public T ReadDb<T>(Func<OSSFuelDbContext, T> read)
    {
        using var scope = Services.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>());
    }

    public Guid SeedCredit(
        Guid projectId,
        long amountMinor,
        string? description = "Funding received",
        DateTimeOffset? createdAt = null,
        Guid? fundingId = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
        var balance = db.AIBalances.Single(b => b.ProjectId == projectId);
        var transaction = balance.Credit(
            amountMinor,
            fundingId ?? Guid.NewGuid(),
            description,
            createdAt ?? DateTimeOffset.UtcNow);
        db.SaveChanges();

        return transaction.Id;
    }

    public Guid SeedFunding(
        Guid projectId,
        Guid sponsorUserId,
        long amountMinor = 2500,
        string status = "pending",
        DateTimeOffset? createdAt = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
        var now = createdAt ?? DateTimeOffset.UtcNow;
        var funding = Funding.Create(projectId, sponsorUserId, amountMinor, now);
        funding.MarkCheckoutCreated(
            $"fake_{funding.Id:N}",
            $"https://payments.test/checkout/{funding.Id}",
            now);

        switch (status)
        {
            case "succeeded":
                funding.MarkSucceeded($"fake_pi_{funding.Id:N}", now);
                break;
            case "failed":
                funding.MarkFailed("Seeded failure.", now);
                break;
            case "cancelled":
                funding.MarkCancelled("Seeded cancellation.", now);
                break;
        }

        db.Fundings.Add(funding);
        db.SaveChanges();

        return funding.Id;
    }

    public Guid SeedFundingCredit(
        Guid projectId,
        string sponsorLogin = "sponsor",
        string? sponsorAvatarUrl = "https://avatars.test/sponsor.png",
        long amountMinor = 1000)
    {
        var sponsorId = SeedUser(sponsorLogin, accessToken: null, avatarUrl: sponsorAvatarUrl);
        var fundingId = SeedFunding(projectId, sponsorId, amountMinor, "succeeded");

        return SeedCredit(projectId, amountMinor, fundingId: fundingId);
    }

    public string SessionIdOf(Guid fundingId) =>
        ReadDb(db => db.Fundings.Single(funding => funding.Id == fundingId).ProviderCheckoutSessionId!);

    public long BalanceOf(Guid projectId) =>
        ReadDb(db => db.AIBalances.Single(balance => balance.ProjectId == projectId).AmountMinor);
}
