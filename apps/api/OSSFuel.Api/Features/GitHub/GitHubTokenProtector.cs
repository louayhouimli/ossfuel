using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace OSSFuel.Api.Features.GitHub;

public interface IGitHubTokenProtector
{
    string Protect(string accessToken);

    string? Unprotect(string? protectedAccessToken);
}

public sealed class GitHubTokenProtector : IGitHubTokenProtector
{
    private const string Purpose = "OSSFuel.Features.GitHub.AccessToken.v1";

    private readonly IDataProtector _protector;

    public GitHubTokenProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public string Protect(string accessToken) => _protector.Protect(accessToken);

    public string? Unprotect(string? protectedAccessToken)
    {
        if (string.IsNullOrEmpty(protectedAccessToken))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedAccessToken);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
