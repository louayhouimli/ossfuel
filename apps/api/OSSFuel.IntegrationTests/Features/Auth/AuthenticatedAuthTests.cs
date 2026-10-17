using System.Net;
using System.Net.Http.Json;

namespace OSSFuel.IntegrationTests.Features.Auth;

public class AuthenticatedAuthTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task GetValidate_WhenAuthenticated_ReturnsTheSignedInUser()
    {
        var userId = factory.SeedUser(login: "octocat");

        var response = await factory.CreateClientFor(userId).GetAsync("/validate");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthPayload>();
        Assert.NotNull(payload);
        Assert.Equal(userId, payload!.Id);
        Assert.Equal("octocat", payload.Login);
    }

    [Fact]
    public async Task GetValidate_WhenTheSignedInUserNoLongerExists_ReturnsUnauthorized()
    {
        // A cookie can outlive the row it points at.
        var response = await factory.CreateClientFor(Guid.NewGuid()).GetAsync("/validate");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostLogout_ReturnsNoContent()
    {
        var response = await factory.CreateClient().PostAsync("/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed record AuthPayload(
        Guid Id,
        string Login,
        string? Name,
        string? Email,
        string? AvatarUrl);
}
