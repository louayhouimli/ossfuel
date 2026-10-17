using System.Net;

namespace OSSFuel.IntegrationTests.Features.Auth;

public class AuthTests(OSSFuelWebApplicationFactory factory) : IClassFixture<OSSFuelWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetValidate_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/validate");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}