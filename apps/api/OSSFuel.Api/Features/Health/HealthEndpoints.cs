using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;

namespace OSSFuel.Api.Features.Health;

public static class HealthEndpoints
{
    public static IServiceCollection AddHealthFeature(this IServiceCollection services)
    {
        services.AddHealthChecks();

        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");

        app.MapGet("/health/db", async (OSSFuelDbContext db) =>
        {
            try
            {
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();

                return Results.Ok(new { status = "health", database = "connected" });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                detail: "An error occured while connecting to the database",
                statusCode: StatusCodes.Status503ServiceUnavailable
,
                title: "Database unavailable");
            }
        });

        app.MapGet("/health/info", (IHostEnvironment env) =>
        {
            return Results.Ok(new
            {
                environment = env.EnvironmentName,
                utcNow = DateTimeOffset.UtcNow,
                status = "alive"
            }

            );

        });

        return app;
    }
}