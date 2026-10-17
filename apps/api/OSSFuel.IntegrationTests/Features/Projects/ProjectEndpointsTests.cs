using System.Net;
using System.Net.Http.Json;

namespace OSSFuel.IntegrationTests.Features.Projects;

public class ProjectEndpointsTests(ProjectsWebApplicationFactory factory)
    : IClassFixture<ProjectsWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetProjects_WhenNoProjectsExist_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ProjectListPayload>();
        Assert.NotNull(payload);
        Assert.Empty(payload!.Items);
        Assert.Equal(0, payload.TotalCount);
    }

    [Fact]
    public async Task GetProject_WhenProjectDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostProjects_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/projects", new { fullName = "octocat/Hello-World" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailableRepositories_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/repos/available");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record ProjectListPayload(
        List<ProjectSummaryPayload> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record ProjectSummaryPayload(Guid Id, string FullName, string Name);
}
