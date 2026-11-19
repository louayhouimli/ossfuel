using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Features.Discovery.Assistant;
using OSSFuel.Api.Features.Discovery.Ingestion;
using OSSFuel.Api.Features.Discovery.Retrieval;

namespace OSSFuel.Api.Features.Discovery;

public static class DiscoverySetup
{
    public static IServiceCollection AddDiscoveryFeature(this IServiceCollection services)
    {
        services.AddScoped<IProjectIngestionService, ProjectIngestionService>();
        services.AddScoped<IProjectRetrievalService, ProjectRetrievalService>();
        services.AddScoped<IProjectAssistantService, ProjectAssistantService>();
        services.AddHostedService<ProjectIndexingBackgroundService>();
        return services;
    }
}
