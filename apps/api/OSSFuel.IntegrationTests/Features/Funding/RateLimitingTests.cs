using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Funding;

public class RateLimitingTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task RapidRequests_ExceedingLimit_Returns429TooManyRequests()
    {
        var client = factory.CreateClient();
        HttpResponseMessage? lastResponse = null;

        // The auth policy allows 20 requests per 10s. Send 25 rapid requests.
        for (var i = 0; i < 25; i++)
        {
            lastResponse = await client.GetAsync("/api/v1/auth/sign-in");
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.Equal("application/problem+json", lastResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(lastResponse.Headers.RetryAfter);

        var problem = await lastResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(429, problem.Status);
        Assert.Equal("Too Many Requests", problem.Title);
    }

    [Fact]
    public async Task AuthenticatedFundingRequests_ExceedingLimit_Returns429AndPartitionsByUser()
    {
        var userId = factory.SeedUser();
        var client = factory.CreateClientFor(userId);
        var project = factory.SeedProject(userId);

        HttpResponseMessage? lastResponse = null;

        // The funding-create policy allows 10 requests per 10s.
        for (var i = 0; i < 15; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/fundings", new
            {
                projectId = project,
                amountMinor = 2500,
            });

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.NotNull(lastResponse.Headers.RetryAfter);

        // A different user should NOT be rate limited (user-level partitioning works)
        var anotherUser = factory.SeedUser(login: "another-sponsor");
        var anotherClient = factory.CreateClientFor(anotherUser);

        var anotherResponse = await anotherClient.PostAsJsonAsync("/api/v1/fundings", new
        {
            projectId = project,
            amountMinor = 2500,
        });

        // Should NOT be 429 Too Many Requests
        Assert.NotEqual(HttpStatusCode.TooManyRequests, anotherResponse.StatusCode);
    }
}
