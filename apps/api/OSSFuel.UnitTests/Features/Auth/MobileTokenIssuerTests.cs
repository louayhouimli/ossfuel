using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OSSFuel.Api.Features.Auth;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.UnitTests.Features.Auth;

public class MobileTokenIssuerTests
{
    private const string SigningKey = "unit-test-mobile-jwt-signing-key-at-least-32-bytes";

    [Fact]
    public async Task Issue_ProducesTokenWithUserSubjectAndThirtyDayLifetime()
    {
        var options = CreateOptions();
        var issuer = new MobileTokenIssuer(options);
        var user = CreateUser();

        var token = issuer.Issue(user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = options.Jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Jwt.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(SigningKey)),
            });

        Assert.True(result.IsValid);
        Assert.Equal(user.Id.ToString(), result.Claims["sub"]);
        Assert.Equal("octocat", result.Claims["login"]);
        Assert.InRange(
            token.ExpiresAt,
            DateTimeOffset.UtcNow.AddDays(29),
            DateTimeOffset.UtcNow.AddDays(31));
    }

    [Fact]
    public void Issue_UsesAUniqueJtiPerToken()
    {
        var issuer = new MobileTokenIssuer(CreateOptions());
        var user = CreateUser();
        var handler = new JsonWebTokenHandler();

        var first = handler.ReadJsonWebToken(issuer.Issue(user).AccessToken);
        var second = handler.ReadJsonWebToken(issuer.Issue(user).AccessToken);

        Assert.NotEqual(first.GetClaim("jti").Value, second.GetClaim("jti").Value);
    }

    [Fact]
    public async Task Issue_SignsWithTheConfiguredKey()
    {
        var issuer = new MobileTokenIssuer(CreateOptions());
        var token = issuer.Issue(CreateUser());

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            token.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes("a-different-signing-key-that-is-long-enough!!")),
            });

        Assert.False(validation.IsValid);
    }

    private static MobileAuthOptions CreateOptions() => new()
    {
        AllowedRedirectUris = ["ossfuel://auth/callback"],
        Jwt = new MobileJwtOptions
        {
            Issuer = "ossfuel-api",
            Audience = "ossfuel-android",
            SigningKey = SigningKey,
            LifetimeDays = 30,
        },
    };

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        GitHubId = 42,
        Login = "octocat",
        Name = "The Octocat",
        CreatedAt = DateTimeOffset.UtcNow,
        LastLoginAt = DateTimeOffset.UtcNow,
    };
}
