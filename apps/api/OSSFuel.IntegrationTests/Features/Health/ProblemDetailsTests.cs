using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task UnhandledException_InProduction_ReturnsProblemDetailsWithoutStackTrace()
    {
        var prodFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedOrigin", "http://localhost:5173");
            builder.ConfigureServices(services =>
            {
                services.AddTransient<IStartupFilter, ThrowingStartupFilter>();
            });
        });

        var client = prodFactory.CreateClient();
        var response = await client.GetAsync("/simulate-unhandled-error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        // Verify stack trace details are suppressed
        Assert.DoesNotContain("Simulated unexpected crash", content);
        Assert.DoesNotContain("ThrowingStartupFilter", content);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private sealed class ThrowingStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);
                app.Use(async (context, innerNext) =>
                {
                    if (context.Request.Path == "/simulate-unhandled-error")
                    {
                        throw new InvalidOperationException("Simulated unexpected crash with sensitive internal details");
                    }

                    await innerNext();
                });
            };
        }
    }
}
