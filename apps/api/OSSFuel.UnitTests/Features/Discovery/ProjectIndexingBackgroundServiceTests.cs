using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Discovery.Ingestion;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Projects;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.UnitTests.Features.Discovery;

public class ProjectIndexingBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenNoUnindexedProjects_CompletesWithoutIngestion()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<OSSFuelDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var fakeIngestion = new FakeProjectIngestionService();
        services.AddScoped<IProjectIngestionService>(_ => fakeIngestion);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var sut = new ProjectIndexingBackgroundService(
            scopeFactory,
            NullLogger<ProjectIndexingBackgroundService>.Instance);

        var processed = await sut.RunBackfillAsync(CancellationToken.None);

        Assert.Equal(0, processed);
        Assert.Equal(0, fakeIngestion.IngestCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnindexedProjectsExist_CallsIngestForEach()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<OSSFuelDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var fakeIngestion = new FakeProjectIngestionService();
        services.AddScoped<IProjectIngestionService>(_ => fakeIngestion);

        var sp = services.BuildServiceProvider();

        // Seed an unindexed project
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
            var user = new User
            {
                Id = Guid.NewGuid(),
                Login = "octocat",
                Name = "Octocat",
                CreatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow,
            };
            db.Users.Add(user);

            var project = Project.Register(
                new GitHubRepository(
                    Id: 1001,
                    FullName: "octocat/hello-world",
                    OwnerLogin: "octocat",
                    Name: "hello-world",
                    Description: "Sample repo",
                    HtmlUrl: "https://github.com/octocat/hello-world",
                    Homepage: null,
                    Language: "C#",
                    Stars: 10,
                    Forks: 2,
                    OpenIssues: 0,
                    DefaultBranch: "main",
                    Topics: [],
                    CanPush: true),
                user.Id,
                DateTimeOffset.UtcNow);

            db.Projects.Add(project);
            await db.SaveChangesAsync();
        }

        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var sut = new ProjectIndexingBackgroundService(
            scopeFactory,
            NullLogger<ProjectIndexingBackgroundService>.Instance);

        var processed = await sut.RunBackfillAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(1, fakeIngestion.IngestCallCount);
    }

    private sealed class FakeProjectIngestionService : IProjectIngestionService
    {
        public int IngestCallCount { get; private set; }

        public Task<ProjectIngestionResult> IngestProjectAsync(Guid projectId, CancellationToken ct = default)
        {
            IngestCallCount++;
            return Task.FromResult(new ProjectIngestionResult(projectId, "octocat/hello-world", 1, true));
        }

        public Task<IReadOnlyList<ProjectIngestionResult>> IngestAllProjectsAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ProjectIngestionResult>>([]);
        }
    }
}
