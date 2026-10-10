namespace OSSFuel.Api.Common;

public static class CorsSetup
{
    public const string Web = "web";

    public static IServiceCollection AddAppCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigin = configuration["AllowedOrigin"]
            ?? throw new InvalidOperationException("AllowedOrigin is not configured");

        services.AddCors(options => options.AddPolicy(Web, policy =>
            policy
                .WithOrigins(allowedOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

        return services;
    }
}