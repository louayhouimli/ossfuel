using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Health;

public class ProblemDetailsTests(OSSFuelWebApplicationFactory factory) : IClassFixture<OSSFuelWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task NotFoundEndpoint_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync("/non-existent-route-404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.NotNull(problem.Instance);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
