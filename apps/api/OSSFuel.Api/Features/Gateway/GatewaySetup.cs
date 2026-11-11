namespace OSSFuel.Api.Features.Gateway;

public static class GatewaySetup
{
    public static IServiceCollection AddGatewayFeature(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.SectionName));

        var provider = configuration["Gateway:Provider"];
        var apiKey = configuration["Gateway:ApiKey"];

        if (string.Equals(provider, MockAIProviderClient.Name, StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(apiKey) && environment.IsDevelopment()))
        {
            services.AddSingleton<IAIProviderClient, MockAIProviderClient>();

            return services;
        }

        services.AddHttpClient<IAIProviderClient, OpenRouterClient>();

        return services;
    }
}
