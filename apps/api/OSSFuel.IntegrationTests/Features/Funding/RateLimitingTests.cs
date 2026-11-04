using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class RateLimitingTests(OSSFuelWebApplicationFactory factory) : IClassFixture<OSSFuelWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RapidRequests_ExceedingLimit_Returns429TooManyRequests()
    {
        HttpResponseMessage? lastResponse = null;

        // The auth policy allows 20 requests per 10s. Send 25 rapid requests.
        for (var i = 0; i < 25; i++)
        {
            lastResponse = await _client.GetAsync("/login");
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.Equal("application/problem+json", lastResponse.Content.Headers.ContentType?.MediaType);

        var problem = await lastResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
    }
}
