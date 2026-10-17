using Microsoft.AspNetCore.DataProtection;
using OSSFuel.Api.Features.GitHub;

namespace OSSFuel.UnitTests.Features.GitHub;

public class GitHubTokenProtectorTests
{
    private static IGitHubTokenProtector CreateProtector() =>
        new GitHubTokenProtector(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_ThenUnprotect_ReturnsOriginalToken()
    {
        var protector = CreateProtector();

        var protectedToken = protector.Protect("gho_example-token");
        var token = protector.Unprotect(protectedToken);

        Assert.NotEqual("gho_example-token", protectedToken);
        Assert.Equal("gho_example-token", token);
    }

    [Fact]
    public void Protect_IsNotReversibleByAnotherApplication()
    {
        var protector = CreateProtector();
        var otherProtector = new GitHubTokenProtector(new EphemeralDataProtectionProvider());

        var protectedToken = protector.Protect("gho_example-token");

        Assert.Null(otherProtector.Unprotect(protectedToken));
    }

    [Fact]
    public void Unprotect_WhenTokenIsNull_ReturnsNull()
    {
        var protector = CreateProtector();

        Assert.Null(protector.Unprotect(null));
    }

    [Fact]
    public void Unprotect_WhenValueIsNotProtected_ReturnsNull()
    {
        var protector = CreateProtector();

        Assert.Null(protector.Unprotect("not-a-protected-value"));
    }
}
