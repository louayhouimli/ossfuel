using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OSSFuel.Api.Features.Users;

namespace OSSFuel.Api.Features.Auth;

public sealed record IssuedMobileToken(string AccessToken, DateTimeOffset ExpiresAt);

public interface IMobileTokenIssuer
{
    IssuedMobileToken Issue(User user);
}

public sealed class MobileTokenIssuer(MobileAuthOptions options) : IMobileTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Jwt.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public IssuedMobileToken Issue(User user)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddDays(options.Jwt.LifetimeDays);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Jwt.Issuer,
            Audience = options.Jwt.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("jti", Guid.NewGuid().ToString("N")),
                new Claim("login", user.Login),
                new Claim("name", user.Name ?? user.Login),
            ]),
            SigningCredentials = _credentials,
        };

        return new IssuedMobileToken(
            _handler.CreateToken(descriptor),
            new DateTimeOffset(expiresAt, TimeSpan.Zero));
    }
}
