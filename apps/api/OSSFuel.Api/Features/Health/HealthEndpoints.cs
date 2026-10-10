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

                return Results.Ok("Database connected");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Database connection failed",
                    detail: ex.ToString());
            }
        });

        return app;
    }
}