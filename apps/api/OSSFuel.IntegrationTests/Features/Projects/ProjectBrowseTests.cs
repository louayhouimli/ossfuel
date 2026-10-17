using System.Net;
using System.Net.Http.Json;

namespace OSSFuel.IntegrationTests.Features.Projects;

public class ProjectBrowseTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task GetProjects_OrdersByStarsDescendingThenFullName()
    {
        var userId = factory.SeedUser();
        factory.SeedProject(userId, gitHubRepoId: 1, fullName: "zzz/Popular", stars: 100);
        factory.SeedProject(userId, gitHubRepoId: 2, fullName: "aaa/AlsoPopular", stars: 100);
        factory.SeedProject(userId, gitHubRepoId: 3, fullName: "mmm/Quiet", stars: 10);

        var response = await factory.CreateClient().GetAsync("/projects?pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(
            ["aaa/AlsoPopular", "zzz/Popular", "mmm/Quiet"],
            payload!.Items.Select(item => item.FullName));
    }

    [Fact]
    public async Task GetProjects_ClampsPageAndPageSize()
    {
        var userId = factory.SeedUser();
        factory.SeedProject(userId, gitHubRepoId: 1, fullName: "aaa/One", stars: 3);

        var response = await factory.CreateClient().GetAsync("/projects?page=0&pageSize=9999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Page);
        Assert.Equal(50, payload.PageSize);
        Assert.Single(payload.Items);
    }

    [Fact]
    public async Task GetProjects_ReturnsTheRequestedPageAndTheTotalCount()
    {
        var userId = factory.SeedUser();
        factory.SeedProject(userId, gitHubRepoId: 1, fullName: "aaa/One", stars: 300);
        factory.SeedProject(userId, gitHubRepoId: 2, fullName: "bbb/Two", stars: 200);
        factory.SeedProject(userId, gitHubRepoId: 3, fullName: "ccc/Three", stars: 100);

        var response = await factory.CreateClient().GetAsync("/projects?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Page);
        Assert.Equal(2, payload.PageSize);
        Assert.Equal(3, payload.TotalCount);
        Assert.Equal("ccc/Three", Assert.Single(payload.Items).FullName);
    }

    private sealed record ProjectListPayload(
        List<ProjectPayload> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record ProjectPayload(Guid Id, string FullName, int Stars);
}
