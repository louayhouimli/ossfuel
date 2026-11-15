using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Features.Auth;

namespace OSSFuel.IntegrationTests.Infrastructure;

/// <summary>
/// Keeps the in-memory database and fake dependencies from
/// <see cref="ProjectsWebApplicationFactory"/> but restores the production authentication
/// pipeline so bearer tokens and cookies can be exercised end to end.
/// </summary>
public sealed class MobileAuthWebApplicationFactory : ProjectsWebApplicationFactory
{
    public FakeGitHubOAuthBackchannel OAuthBackchannel { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(AuthSetup.AppAuthenticationScheme);

            services.Configure<GitHubAuthenticationOptions>(
                GitHubAuthenticationDefaults.AuthenticationScheme,
                options => options.Backchannel = new HttpClient(
                    OAuthBackchannel,
                    disposeHandler: false));
        });
    }
}
