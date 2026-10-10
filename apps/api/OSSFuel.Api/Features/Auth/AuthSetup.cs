using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Auth;

public static class AuthSetup
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services, IConfiguration configuration)
    {
        var gitHubClientId = configuration["Authentication:GitHub:ClientId"]
            ?? throw new InvalidOperationException("Authentication:GitHub:ClientId is not configured");

        var gitHubClientSecret = configuration["Authentication:GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication:GitHub:ClientSecret is not configured");

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "ossfuel.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = configuration.GetValue("Authentication:Cookie:SameSite", SameSiteMode.Lax);
                options.Cookie.SecurePolicy = configuration.GetValue("Authentication:Cookie:SecurePolicy", CookieSecurePolicy.SameAsRequest);
                options.SlidingExpiration = true;

                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddGitHub(options =>
            {
                options.ClientId = gitHubClientId;
                options.ClientSecret = gitHubClientSecret;
                options.CallbackPath = "/signin-github";
                options.Scope.Add("read:user");
                options.Scope.Add("user:email");

                options.Events.OnCreatingTicket = GitHubEvents.OnCreatingTicket;
            });

        services.AddAuthorization();

        return services;
    }
}

public static class GitHubEvents
{
    public static async Task OnCreatingTicket(OAuthCreatingTicketContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<OSSFuelDbContext>();

        var gitHubId = context.User.GetProperty("id").GetInt64();
        var login = context.User.GetProperty("login").GetString()
            ?? throw new InvalidOperationException("GitHub did not return a login claim");

        var now = DateTimeOffset.UtcNow;

        var user = await db.Users.FirstOrDefaultAsync(u => u.GitHubId == gitHubId);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                GitHubId = gitHubId,
                Login = login,
                CreatedAt = now,
            };

            db.Users.Add(user);
        }

        user.Login = login;
        user.Name = GetProperty(context, "name");
        user.AvatarUrl = GetProperty(context, "avatar_url");
        user.Email = context.Identity?.FindFirst(ClaimTypes.Email)?.Value;
        user.LastLoginAt = now;

        await db.SaveChangesAsync();

        var identity = context.Identity!;
        var existingIdentifier = identity.FindFirst(ClaimTypes.NameIdentifier);
        if (existingIdentifier is not null)
        {
            identity.RemoveClaim(existingIdentifier);
        }

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    }

    private static string? GetProperty(OAuthCreatingTicketContext context, string propertyName) =>
        context.User.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}