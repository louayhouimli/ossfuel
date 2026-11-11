using System.Security.Cryptography;
using System.Text;

namespace OSSFuel.Api.Features.ApiKeys;

public static class ApiKeyHasher
{
    public const string Prefix = "ossf_";

    public static (string RawKey, string KeyPrefix, string KeyHash) GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var rawKey = $"{Prefix}{token}";
        var keyPrefix = rawKey[..Math.Min(16, rawKey.Length)];
        var keyHash = ComputeSha256Hex(rawKey);

        return (rawKey, keyPrefix, keyHash);
    }

    public static string ComputeSha256Hex(string rawKey)
    {
        var bytes = Encoding.UTF8.GetBytes(rawKey);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexStringLower(hash);
    }
}
