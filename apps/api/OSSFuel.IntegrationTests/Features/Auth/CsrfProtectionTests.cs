using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Auth;

public class CsrfProtectionTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Post_WhenAuthenticatedAndMissingCsrfHeader_ReturnsForbiddenProblemDetails()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClient();
        // Set user auth cookie header, but do NOT set X-OSSFuel-Client header
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        var response = await client.PostAsJsonAsync("/projects", new { gitHubRepoId = 1296269 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("CSRF protection check failed.", problem.Title);
    }

    [Fact]
    public async Task Post_WhenAuthenticatedWithCsrfHeader_PassesCsrfCheck()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add("X-OSSFuel-Client", "web");

        // Request will pass CSRF check (may fail validation or repo lookup, but not 403 Forbidden CSRF)
        var response = await client.PostAsJsonAsync("/projects", new { gitHubRepoId = 1296269 });

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenAuthenticatedWithoutCsrfHeader_IsAllowed()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        var response = await client.GetAsync("/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
