using Microsoft.AspNetCore.DataProtection;

namespace OSSFuel.Api.Features.GitHub;

public static class GitHubSetup
{
    public static IServiceCollection AddGitHubFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keysPath = ResolveKeysPath(configuration);
        Directory.CreateDirectory(keysPath);

        services
            .AddDataProtection()
            .SetApplicationName("OSSFuel")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        services.AddSingleton<IGitHubTokenProtector, GitHubTokenProtector>();

        services.AddHttpClient<IGitHubRepositoryClient, GitHubRepositoryClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
        });

        return services;
    }

    private static string ResolveKeysPath(IConfiguration configuration)
    {
        var configured = configuration["DataProtection:KeysPath"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrEmpty(basePath))
        {
            basePath = AppContext.BaseDirectory;
        }

        return Path.Combine(basePath, "OSSFuel", "keys");
    }
}
