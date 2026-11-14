using System.Net;
using System.Net.Http.Json;
using OSSFuel.Api.Features.ApiKeys;
using OSSFuel.IntegrationTests.Infrastructure;
using Xunit;

namespace OSSFuel.IntegrationTests.Features.ApiKeys;

public class ApiKeyEndpointsTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task GetApiKeys_WithoutAuth_Returns401()
    {
        var projectId = Guid.NewGuid();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/projects/{projectId}/api-keys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetApiKeys_WhenNotOwner_Returns403()
    {
        var ownerId = _factory.SeedUser(login: "owner");
        var otherId = _factory.SeedUser(login: "other");
        var projectId = _factory.SeedProject(ownerId);

        var client = _factory.CreateClientFor(otherId);
        var response = await client.GetAsync($"/projects/{projectId}/api-keys");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateApiKey_WhenOwner_Returns201AndStoresKey()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var client = _factory.CreateClientFor(ownerId);
        var response = await client.PostAsJsonAsync($"/projects/{projectId}/api-keys", new CreateApiKeyRequest("Dev Key"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        Assert.NotNull(created);
        Assert.Equal("Dev Key", created.Name);
        Assert.StartsWith("ossf_", created.RawKey);
        Assert.Equal(created.RawKey[..16], created.KeyPrefix);

        // Verify the raw key is not stored in the database, only the hash
        _factory.ReadDb(db =>
        {
            var keyInDb = db.ProjectApiKeys.FirstOrDefault(k => k.Id == created.Id);
            Assert.NotNull(keyInDb);
            Assert.Equal("Dev Key", keyInDb.Name);
            Assert.Equal(ApiKeyHasher.ComputeSha256Hex(created.RawKey), keyInDb.KeyHash);
            Assert.True(keyInDb.IsActive);
            return true;
        });

        // Verify GET returns the key without raw secret
        var listResponse = await client.GetAsync($"/projects/{projectId}/api-keys");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<List<ApiKeySummaryResponse>>();
        Assert.NotNull(list);
        var item = Assert.Single(list);
        Assert.Equal(created.Id, item.Id);
        Assert.Equal(created.KeyPrefix, item.KeyPrefix);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task CreateApiKey_WhenNotOwner_Returns403()
    {
        var ownerId = _factory.SeedUser(login: "owner");
        var otherId = _factory.SeedUser(login: "other");
        var projectId = _factory.SeedProject(ownerId);

        var client = _factory.CreateClientFor(otherId);
        var response = await client.PostAsJsonAsync($"/projects/{projectId}/api-keys", new CreateApiKeyRequest("Hacker Key"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokeApiKey_WhenOwner_MarksKeyAsRevoked()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);

        var client = _factory.CreateClientFor(ownerId);
        var createResponse = await client.PostAsJsonAsync($"/projects/{projectId}/api-keys", new CreateApiKeyRequest("Temporary Key"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        Assert.NotNull(created);

        var deleteResponse = await client.DeleteAsync($"/projects/{projectId}/api-keys/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        _factory.ReadDb(db =>
        {
            var keyInDb = db.ProjectApiKeys.First(k => k.Id == created.Id);
            Assert.False(keyInDb.IsActive);
            Assert.NotNull(keyInDb.RevokedAt);
            return true;
        });
    }

    [Fact]
    public async Task RevokeApiKey_WhenNotOwner_Returns403()
    {
        var ownerId = _factory.SeedUser(login: "owner");
        var otherId = _factory.SeedUser(login: "other");
        var projectId = _factory.SeedProject(ownerId);

        var ownerClient = _factory.CreateClientFor(ownerId);
        var createResponse = await ownerClient.PostAsJsonAsync($"/projects/{projectId}/api-keys", new CreateApiKeyRequest("My Key"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        Assert.NotNull(created);

        var otherClient = _factory.CreateClientFor(otherId);
        var deleteResponse = await otherClient.DeleteAsync($"/projects/{projectId}/api-keys/{created.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }
}
