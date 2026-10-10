using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OSSFuel.Api.Database;

namespace OSSFuel.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", (string? returnUrl, IConfiguration configuration) =>
            Results.Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = ResolveRedirectUrl(returnUrl, configuration)
                },
                [GitHubAuthenticationDefaults.AuthenticationScheme]));

        app.MapPost("/logout", (IConfiguration configuration) =>
            Results.SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = configuration["WebAppUrl"] ?? "/"
                },
                [CookieAuthenticationDefaults.AuthenticationScheme]));

        app.MapGet("/validate", async (ClaimsPrincipal principal, OSSFuelDbContext db, CancellationToken cancellationToken) =>
        {
            var userId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : Guid.Empty;

            if (userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FindAsync([userId], cancellationToken);

            return user is null
                ? Results.Unauthorized()
                : Results.Ok(AuthResponse.From(user));
        })
        .RequireAuthorization();

        return app;
    }

    private static string ResolveRedirectUrl(string? returnUrl, IConfiguration configuration)
    {
        var webAppUrl = configuration["WebAppUrl"] ?? "/";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return webAppUrl;
        }

        return returnUrl.StartsWith('/')
            ? webAppUrl.TrimEnd('/') + returnUrl
            : returnUrl;
    }
}