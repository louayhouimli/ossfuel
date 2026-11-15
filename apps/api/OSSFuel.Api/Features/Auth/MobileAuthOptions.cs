using System.Text;

namespace OSSFuel.Api.Features.Auth;

public sealed class MobileAuthOptions
{
    public const string SectionName = "Authentication:Mobile";

    public string[] AllowedRedirectUris { get; set; } = [];

    public MobileJwtOptions Jwt { get; set; } = new();

    public static MobileAuthOptions FromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<MobileAuthOptions>() ?? new MobileAuthOptions();

        options.AllowedRedirectUris = options.AllowedRedirectUris
            .Where(uri => !string.IsNullOrWhiteSpace(uri))
            .Select(uri => uri.Trim())
            .ToArray();

        if (options.AllowedRedirectUris.Length == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:AllowedRedirectUris must configure at least one mobile redirect URI.");
        }

        if (options.Jwt.LifetimeDays <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:Jwt:LifetimeDays must be greater than zero.");
        }

        if (Encoding.UTF8.GetByteCount(options.Jwt.SigningKey) < MobileJwtOptions.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Jwt:SigningKey must be at least {MobileJwtOptions.MinimumSigningKeyBytes} bytes long.");
        }

        return options;
    }

    public bool IsAllowedRedirect(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        foreach (var allowedValue in AllowedRedirectUris)
        {
            if (!Uri.TryCreate(allowedValue, UriKind.Absolute, out var allowed))
            {
                continue;
            }

            if (string.Equals(candidate.Scheme, allowed.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Host, allowed.Host, StringComparison.OrdinalIgnoreCase)
                && candidate.Port == allowed.Port
                && string.Equals(
                    candidate.AbsolutePath.TrimEnd('/'),
                    allowed.AbsolutePath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class MobileJwtOptions
{
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "ossfuel-api";

    public string Audience { get; set; } = "ossfuel-android";

    public string SigningKey { get; set; } = string.Empty;

    public int LifetimeDays { get; set; } = 30;
}
