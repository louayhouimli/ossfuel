namespace OSSFuel.Api.Features.Funding;

public static class FundingSetup
{
    public const string MockProviderName = "mock";

    public static IServiceCollection AddFundingFeature(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<FundingOptions>(configuration.GetSection(FundingOptions.SectionName));
        services.AddScoped<FundingCreditService>();

        if (UsesMockProvider(configuration, environment))
        {
            services.AddSingleton<IPaymentProvider, MockPaymentProvider>();

            return services;
        }

        if (string.Equals(
                configuration["Payments:Provider"],
                MockProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The mock payment provider is only allowed in Development. "
                + "Set Payments:Provider to a real provider before running outside Development.");
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
}
