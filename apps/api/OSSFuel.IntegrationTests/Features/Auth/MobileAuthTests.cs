using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OSSFuel.Api.Database;
using OSSFuel.Api.Features.Auth;
using OSSFuel.IntegrationTests.Infrastructure;

namespace OSSFuel.IntegrationTests.Features.Auth;

public class MobileAuthTests : IDisposable
{
    private readonly MobileAuthWebApplicationFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task PostExchange_WithValidCode_ReturnsBearerTokenThatAuthenticates()
    {
        var userId = factory.SeedUser(login: "octocat");
        var code = SeedCode(userId);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<MobileAuthPayload>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal(userId, payload.User.Id);
        Assert.Equal("octocat", payload.User.Login);
        Assert.InRange(
            payload.ExpiresAt,
            DateTimeOffset.UtcNow.AddDays(29),
            DateTimeOffset.UtcNow.AddDays(31));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);

        var me = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var user = await me.Content.ReadFromJsonAsync<AuthPayload>();
        Assert.NotNull(user);
        Assert.Equal(userId, user!.Id);
        Assert.Equal("octocat", user.Login);
    }

    [Fact]
    public async Task PostExchange_WhenCodeIsReused_ReturnsUnauthorized()
    {
        var userId = factory.SeedUser();
        var code = SeedCode(userId);
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });
        var second = await client.PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task PostExchange_WhenCodeIsExpired_ReturnsUnauthorized()
    {
        var userId = factory.SeedUser();
        var code = SeedCode(userId, expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostExchange_WithUnknownCode_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/mobile/exchange",
            new { code = "not-a-real-code" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithTamperedBearerToken_ReturnsUnauthorized()
    {
        var userId = factory.SeedUser();
        var code = SeedCode(userId);
        var client = factory.CreateClient();

        var exchange = await client.PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });
        var payload = await exchange.Content.ReadFromJsonAsync<MobileAuthPayload>();

        var token = payload!.AccessToken;
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithBearerToken_DoesNotRequireCsrfHeader()
    {
        var userId = factory.SeedUser();
        var code = SeedCode(userId);
        var client = factory.CreateClient();

        var exchange = await client.PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });
        var payload = await exchange.Content.ReadFromJsonAsync<MobileAuthPayload>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload!.AccessToken);

        // Deliberately no X-OSSFuel-Client header: bearer requests are not cookie-authenticated.
        var response = await client.PostAsync("/api/v1/auth/sign-out", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetCallback_AfterGitHubSignIn_RedirectsToTheAppWithAnExchangeableCode()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // The OAuth correlation cookie is marked Secure, so replay it over https.
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        const string appState = "app-state-123";
        var returnUrl = Uri.EscapeDataString($"ossfuel://auth/callback?state={appState}");

        var challenge = await client.GetAsync("/api/v1/auth/sign-in?returnUrl=" + returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var authorizationEndpoint = challenge.Headers.Location;
        Assert.NotNull(authorizationEndpoint);
        Assert.Equal("github.com", authorizationEndpoint!.Host);

        var oauthState = QueryHelpers.ParseQuery(authorizationEndpoint.Query)["state"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(oauthState));

        var callback = await client.GetAsync(
            "/api/v1/auth/callback?code=fake-code&state=" + Uri.EscapeDataString(oauthState));

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);

        var mobileRedirect = callback.Headers.Location;
        Assert.NotNull(mobileRedirect);
        Assert.Equal("ossfuel", mobileRedirect!.Scheme);
        Assert.Equal("auth", mobileRedirect.Host);
        Assert.Equal("/callback", mobileRedirect.AbsolutePath);

        var query = QueryHelpers.ParseQuery(mobileRedirect.Query);
        Assert.Equal(appState, query["state"]);

        var code = query["code"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(code));

        // The browser must not be signed in; the app trades the code for its own token.
        var sessionCookie = callback.Headers
            .Where(header => header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(header => header.Value)
            .FirstOrDefault(value => value.Contains("ossfuel.session", StringComparison.Ordinal));
        Assert.Null(sessionCookie);

        var exchange = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/mobile/exchange", new { code });

        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);

        var payload = await exchange.Content.ReadFromJsonAsync<MobileAuthPayload>();
        Assert.NotNull(payload);
        Assert.Equal("octocat", payload!.User.Login);
    }

    [Fact]
    public async Task GetSignIn_WithDisallowedReturnUrl_ReturnsBadRequest()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/auth/sign-in?returnUrl=" + Uri.EscapeDataString("https://evil.example/callback"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSignIn_WithMobileReturnUrl_RedirectsToGitHub()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var returnUrl = Uri.EscapeDataString("ossfuel://auth/callback?state=abc123");

        var response = await client.GetAsync(
            "/api/v1/auth/sign-in?returnUrl=" + returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("github.com", response.Headers.Location!.Host);
    }

    private string SeedCode(
        Guid userId,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? expiresAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OSSFuelDbContext>();

        var (rawCode, code) = MobileAuthCodeFactory.Create(userId, createdAt ?? DateTimeOffset.UtcNow);

        if (expiresAt is not null)
        {
            code.ExpiresAt = expiresAt.Value;
        }

        db.MobileAuthCodes.Add(code);
        db.SaveChanges();

        return rawCode;
    }

    private sealed record MobileAuthPayload(
        string AccessToken,
        DateTimeOffset ExpiresAt,
        AuthPayload User);

    private sealed record AuthPayload(
        Guid Id,
        string Login,
        string? Name,
        string? Email,
        string? AvatarUrl);
}
