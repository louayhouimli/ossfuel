using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace OSSFuel.Api.Database;

public static class DatabaseSetup
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OSSFuelDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), o => o.UseVector()));

        return services;
    }

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();
        await db.Database.MigrateAsync();
    }
}
