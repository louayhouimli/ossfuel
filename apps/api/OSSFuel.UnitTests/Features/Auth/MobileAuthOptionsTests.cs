using Microsoft.Extensions.Configuration;
using OSSFuel.Api.Features.Auth;

namespace OSSFuel.UnitTests.Features.Auth;

public class MobileAuthOptionsTests
{
    [Theory]
    [InlineData("ossfuel://auth/callback")]
    [InlineData("ossfuel://auth/callback?state=abc123")]
    [InlineData("OSSFUEL://AUTH/CALLBACK")]
    public void IsAllowedRedirect_WithMatchingUri_ReturnsTrue(string value)
    {
        var options = CreateOptions();

        Assert.True(options.IsAllowedRedirect(value));
    }

    [Theory]
    [InlineData("https://evil.example/auth/callback")]
    [InlineData("ossfuel://other/callback")]
    [InlineData("ossfuel://auth/other")]
    [InlineData("/relative/path")]
    public void IsAllowedRedirect_WithUnlistedUri_ReturnsFalse(string value)
    {
        var options = CreateOptions();

        Assert.False(options.IsAllowedRedirect(value));
    }

    [Fact]
    public void FromConfiguration_WhenSigningKeyIsTooShort_Throws()
    {
        var configuration = BuildConfiguration(
            ("Authentication:Mobile:AllowedRedirectUris:0", "ossfuel://auth/callback"),
            ("Authentication:Mobile:Jwt:SigningKey", "short"));

        Assert.Throws<InvalidOperationException>(
            () => MobileAuthOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void FromConfiguration_WhenNoRedirectUrisConfigured_Throws()
    {
        var configuration = BuildConfiguration(
            ("Authentication:Mobile:Jwt:SigningKey", "unit-test-mobile-jwt-signing-key-at-least-32-bytes"));

        Assert.Throws<InvalidOperationException>(
            () => MobileAuthOptions.FromConfiguration(configuration));
    }

    private static MobileAuthOptions CreateOptions() => new()
    {
        AllowedRedirectUris = ["ossfuel://auth/callback"],
        Jwt = new MobileJwtOptions
        {
            SigningKey = "unit-test-mobile-jwt-signing-key-at-least-32-bytes",
        },
    };

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
