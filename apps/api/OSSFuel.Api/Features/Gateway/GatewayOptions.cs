namespace OSSFuel.Api.Features.Gateway;

public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string Provider { get; set; } = AIUsage.OpenRouterProvider;

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string? ApiKey { get; set; }
}
