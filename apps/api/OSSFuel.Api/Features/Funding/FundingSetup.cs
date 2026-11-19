namespace OSSFuel.Api.Features.Funding;

public static class FundingSetup
{
    public const string MockProviderName = "mock";
    public const string StripeProviderName = "stripe";

    public static IServiceCollection AddFundingFeature(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<FundingOptions>(configuration.GetSection(FundingOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.AddScoped<FundingCreditService>();

        if (UsesMockProvider(configuration, environment))
        {
            services.AddSingleton<IPaymentProvider, MockPaymentProvider>();

            return services;
        }

        if (UsesStripeProvider(configuration))
        {
            services.AddScoped<IPaymentProvider, StripePaymentProvider>();

            return services;
        }

        services.AddSingleton<IPaymentProvider, UnconfiguredPaymentProvider>();

        return services;
    }

    public static bool UsesMockProvider(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Payments:Provider"];

        return string.Equals(configured, MockProviderName, StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(configured) && environment.IsDevelopment());
    }

    public static bool UsesStripeProvider(IConfiguration configuration)
    {
        var configured = configuration["Payments:Provider"];

        return string.Equals(configured, StripeProviderName, StringComparison.OrdinalIgnoreCase);
    }
}
