using System.Net;
using System.Net.Http.Json;

namespace OSSFuel.IntegrationTests.Features.Balances;

public class BalanceCreationTests(ProjectsWebApplicationFactory factory)
    : IClassFixture<ProjectsWebApplicationFactory>
{
    [Fact]
    public async Task Registering_a_project_opens_one_zero_balance()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClientFor(userId);
        factory.GitHub.Repository = TestRepository.Create(id: 4242, name: "new-project");

        var response = await client.PostAsJsonAsync("/projects", new { fullName = "octocat/new-project" });
        response.EnsureSuccessStatusCode();
        var project = await response.Content.ReadFromJsonAsync<ProjectPayload>();

        var balances = factory.ReadDb(db => db.AIBalances.Where(b => b.ProjectId == project!.Id).ToList());
        var balance = Assert.Single(balances);
        Assert.Equal(0, balance.AmountMinor);
    }

    [Fact]
    public async Task Registering_the_same_repository_twice_does_not_add_a_second_balance()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClientFor(userId);
        factory.GitHub.Repository = TestRepository.Create(id: 4243, name: "twice-project");

        var first = await client.PostAsJsonAsync("/projects", new { fullName = "octocat/twice-project" });
        first.EnsureSuccessStatusCode();
        var project = await first.Content.ReadFromJsonAsync<ProjectPayload>();

        var second = await client.PostAsJsonAsync("/projects", new { fullName = "octocat/twice-project" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var balances = factory.ReadDb(db => db.AIBalances.Where(b => b.ProjectId == project!.Id).ToList());
        Assert.Single(balances);
    }

    private sealed record ProjectPayload(Guid Id, string FullName);
}
