using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.GitHub;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Auth;

public static class AuthSetup
{
    public const string AppAuthenticationScheme = "App";

    public static IServiceCollection AddAuthFeature(this IServiceCollection services, IConfiguration configuration)
    {
        var gitHubClientId = configuration["Authentication:GitHub:ClientId"]
            ?? throw new InvalidOperationException("Authentication:GitHub:ClientId is not configured");

        var gitHubClientSecret = configuration["Authentication:GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("Authentication:GitHub:ClientSecret is not configured");

        var mobileOptions = MobileAuthOptions.FromConfiguration(configuration);

        services.AddSingleton(mobileOptions);
        services.AddSingleton<IMobileTokenIssuer, MobileTokenIssuer>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = AppAuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(AppAuthenticationScheme, "Cookie or mobile bearer token", options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.ToString()
                        .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
            })
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
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = mobileOptions.Jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = mobileOptions.Jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(mobileOptions.Jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                };
            })
            .AddGitHub(options =>
            {
                options.ClientId = gitHubClientId;
                options.ClientSecret = gitHubClientSecret;
                options.CallbackPath = "/api/v1/auth/callback";
                options.Scope.Add("read:user");
                options.Scope.Add("user:email");
                options.Scope.Add("public_repo");

                options.Events.OnCreatingTicket = GitHubEvents.OnCreatingTicket;
                options.Events.OnTicketReceived = context =>
                    GitHubEvents.OnTicketReceived(context, mobileOptions);
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

        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            var protector = context.HttpContext.RequestServices.GetRequiredService<IGitHubTokenProtector>();
            user.GitHubAccessToken = protector.Protect(context.AccessToken);
            user.GitHubAccessTokenUpdatedAt = now;
        }

        await db.SaveChangesAsync();

        var identity = context.Identity!;
        var existingIdentifier = identity.FindFirst(ClaimTypes.NameIdentifier);
        if (existingIdentifier is not null)
        {
            identity.RemoveClaim(existingIdentifier);
        }

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
    }

    public static async Task OnTicketReceived(
        TicketReceivedContext context,
        MobileAuthOptions mobileOptions)
    {
        // RemoteAuthenticationHandler moves Properties.RedirectUri into ReturnUri (and clears it)
        // before raising TicketReceived. Read ReturnUri, or mobile sign-ins fall through to the
        // cookie path and redirect back with no exchange code.
        var redirectUri = context.ReturnUri;

        if (string.IsNullOrWhiteSpace(redirectUri) || !mobileOptions.IsAllowedRedirect(redirectUri))
        {
            return;
        }

        var userId = Guid.TryParse(
            context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id)
            ? id
            : Guid.Empty;

        if (userId == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.HandleResponse();
            await context.Response.WriteAsync("Unable to start a mobile session.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<OSSFuelDbContext>();
        var cancellationToken = context.HttpContext.RequestAborted;
        var now = DateTimeOffset.UtcNow;

        var previousCodes = await db.MobileAuthCodes
            .Where(code => code.UserId == userId)
            .ToListAsync(cancellationToken);

        if (previousCodes.Count > 0)
        {
            // Only the most recent sign-in attempt should be redeemable.
            db.MobileAuthCodes.RemoveRange(previousCodes);
        }

        var (rawCode, code) = MobileAuthCodeFactory.Create(userId, now);
        db.MobileAuthCodes.Add(code);

        await db.SaveChangesAsync(cancellationToken);

        // Do not sign the browser in: the app exchanges this single-use code for its own token.
        context.Response.Redirect(QueryHelpers.AddQueryString(redirectUri, "code", rawCode));
        context.HandleResponse();
    }

    private static string? GetProperty(OAuthCreatingTicketContext context, string propertyName) =>
        context.User.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
