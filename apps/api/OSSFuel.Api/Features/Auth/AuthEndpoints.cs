using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OSSFuel.Api.Common;
using OSSFuel.Api.Database;

namespace OSSFuel.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapGet("/sign-in", (string? returnUrl, IConfiguration configuration, MobileAuthOptions mobileOptions) =>
        {
            var redirectUrl = ResolveRedirectUrl(returnUrl, configuration, mobileOptions);

            return redirectUrl is null
                ? Results.Problem(
                    title: "Invalid return URL.",
                    detail: "The requested return URL is not allowed.",
                    statusCode: StatusCodes.Status400BadRequest)
                : Results.Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = redirectUrl
                    },
                    [GitHubAuthenticationDefaults.AuthenticationScheme]);
        })
        .RequireRateLimiting(RateLimitingSetup.AuthPolicy);

        group.MapPost("/sign-out", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, OSSFuelDbContext db, CancellationToken cancellationToken) =>
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

        group.MapPost(
            "/mobile/exchange",
            async (
                MobileExchangeRequest request,
                OSSFuelDbContext db,
                IMobileTokenIssuer tokenIssuer,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 512)
                {
                    return Results.Unauthorized();
                }

                var now = DateTimeOffset.UtcNow;
                var codeHash = MobileAuthCodeFactory.Hash(request.Code.Trim());

                var authCode = await db.MobileAuthCodes
                    .FirstOrDefaultAsync(code => code.CodeHash == codeHash, cancellationToken);

                if (authCode is null || authCode.ExpiresAt <= now)
                {
                    return Results.Unauthorized();
                }

                // Deleting the row makes the code single-use: concurrent exchanges see zero rows affected.
                db.MobileAuthCodes.Remove(authCode);

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return Results.Unauthorized();
                }

                var user = await db.Users.FindAsync([authCode.UserId], cancellationToken);

                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var token = tokenIssuer.Issue(user);

                return Results.Ok(new MobileAuthResponse(
                    token.AccessToken,
                    token.ExpiresAt,
                    AuthResponse.From(user)));
            })
        .RequireRateLimiting(RateLimitingSetup.AuthPolicy);

        return app;
    }

    private static string? ResolveRedirectUrl(
        string? returnUrl,
        IConfiguration configuration,
        MobileAuthOptions mobileOptions)
    {
        var webAppUrl = configuration["WebAppUrl"] ?? "/";

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return webAppUrl;
        }

        if (returnUrl.StartsWith('/'))
        {
            return webAppUrl.TrimEnd('/') + returnUrl;
        }

        return mobileOptions.IsAllowedRedirect(returnUrl) ? returnUrl : null;
    }
}
