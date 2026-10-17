using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OSSFuel.IntegrationTests.Features.Auth;

public class AuthRedirectTests(OSSFuelWebApplicationFactory factory) : IClassFixture<OSSFuelWebApplicationFactory>
{
    [Fact]
    public async Task GetLogin_BehindTlsTerminatingProxy_UsesHttpsRedirectUri()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "ossfuel-api.example.com");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(
            Uri.EscapeDataString("https://ossfuel-api.example.com/signin-github"),
            location);
    }
}
