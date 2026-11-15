using System.Net;
using System.Text;

namespace OSSFuel.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for github.com during the OAuth dance so the callback flow can be exercised
/// end to end without network access.
/// </summary>
public sealed class FakeGitHubOAuthBackchannel : HttpMessageHandler
{
    public string Login { get; set; } = "octocat";

    public long GitHubId { get; set; } = 9_000_001;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath;

        if (request.Method == HttpMethod.Post && path == "/login/oauth/access_token")
        {
            return Task.FromResult(Json(
                """
                {"access_token":"fake-access-token","token_type":"bearer","scope":"read:user,user:email"}
                """));
        }

        if (request.Method == HttpMethod.Get && path == "/user")
        {
            return Task.FromResult(Json(
                $$"""
                {
                  "id": {{GitHubId}},
                  "login": "{{Login}}",
                  "name": "The Octocat",
                  "email": "octocat@example.com",
                  "avatar_url": "https://avatars.test/octocat.png",
                  "url": "https://github.com/{{Login}}"
                }
                """));
        }

        if (request.Method == HttpMethod.Get && path == "/user/emails")
        {
            return Task.FromResult(Json(
                """
                [{"email":"octocat@example.com","primary":true,"verified":true}]
                """));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
