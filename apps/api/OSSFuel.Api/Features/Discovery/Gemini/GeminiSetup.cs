using Microsoft.Extensions.Options;
using OSSFuel.Api.Features.Discovery.Ingestion;

namespace OSSFuel.Api.Features.Discovery.Gemini;

public static class GeminiSetup
{
    public static IServiceCollection AddGeminiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.PostConfigure<GeminiOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = configuration["GEMINI_API_KEY"]
                    ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            }
        });

        services.AddHttpClient<IGeminiClient, GeminiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            }
        });

        services.AddSingleton<IMarkdownChunker, MarkdownChunker>();

        return services;
    }
}
