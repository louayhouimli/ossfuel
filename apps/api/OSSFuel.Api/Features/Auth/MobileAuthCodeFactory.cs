using System.Security.Cryptography;
using System.Text;

namespace OSSFuel.Api.Features.Auth;

public static class MobileAuthCodeFactory
{
    public const int LifetimeMinutes = 2;

    private const int CodeByteLength = 32;

    public static (string RawCode, MobileAuthCode Code) Create(Guid userId, DateTimeOffset now)
    {
        var rawCode = Generate();
        var code = new MobileAuthCode
        {
            Id = Guid.NewGuid(),
            CodeHash = Hash(rawCode),
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(LifetimeMinutes),
        };

        return (rawCode, code);
    }

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeByteLength);

        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public static string Hash(string rawCode) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawCode)));
}
