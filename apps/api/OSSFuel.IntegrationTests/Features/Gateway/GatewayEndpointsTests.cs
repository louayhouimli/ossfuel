using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OSSFuel.Api.Features.ApiKeys;
using OSSFuel.Api.Features.Balances;
using OSSFuel.Api.Features.Gateway;
using OSSFuel.IntegrationTests.Infrastructure;
using Xunit;

namespace OSSFuel.IntegrationTests.Features.Gateway;

public class GatewayEndpointsTests : IDisposable
{
    private readonly ProjectsWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task PostChatCompletions_WithoutApiKey_Returns401()
    {
        var request = new ChatCompletionRequest(
            Model: "openai/gpt-4o-mini",
            Messages: [new ChatMessage("user", "Hello")]);

        var response = await _factory.CreateClient().PostAsJsonAsync("/v1/chat/completions", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostChatCompletions_WithValidKeyAndBalance_DebitsBalanceAndRecordsUsage()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 1000); // $10.00 balance

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "CliKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        var request = new ChatCompletionRequest(
            Model: "openai/gpt-4o-mini",
            Messages: [new ChatMessage("user", "Write a hello world function")]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var response = await _factory.CreateClient().SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _factory.ReadDb(db =>
        {
            var balance = db.AIBalances.First(b => b.ProjectId == projectId);
            Assert.True(balance.AmountMinor < 1000, "Balance should have been debited");

            var usage = db.AIUsages.First(u => u.ProjectId == projectId);
            Assert.Equal("openai/gpt-4o-mini", usage.Model);
            Assert.Equal(AIUsage.SucceededStatus, usage.Status);

            var debitTx = db.BalanceTransactions.First(t => t.AIBalanceId == balance.Id && t.Type == BalanceTransaction.AIUsageDebitType);
            Assert.True(debitTx.AmountMinor < 0);
            return true;
        });
    }

    [Fact]
    public async Task PostChatCompletions_WhenBalanceIsZero_Returns402PaymentRequired()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId); // Balance = 0

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "EmptyKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        var request = new ChatCompletionRequest(
            Model: "openai/gpt-4o-mini",
            Messages: [new ChatMessage("user", "Hello")]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var response = await _factory.CreateClient().SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task GetModels_ReturnsOpenRouterFreeModelsList()
    {
        var response = await _factory.CreateClient().GetAsync("/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("openrouter/free", body);
        Assert.Contains("meta-llama/llama-3.3-70b-instruct:free", body);
        Assert.Contains("deepseek/deepseek-r1:free", body);
    }

    [Fact]
    public async Task PostChatCompletions_WhenModelOmitted_DefaultsToOpenRouterFree()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 500);

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "AutoModelKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        var request = new ChatCompletionRequest(
            Model: "",
            Messages: [new ChatMessage("user", "Hello router")]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var response = await _factory.CreateClient().SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _factory.ReadDb(db =>
        {
            var usage = db.AIUsages.First(u => u.ProjectId == projectId);
            Assert.Equal("openrouter/free", usage.Model);
            return true;
        });
    }

    [Fact]
    public async Task PostChatCompletions_WhenApiKeyIsRevoked_Returns401Unauthorized()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 500);

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "RevokedKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            apiKey.Revoke(DateTimeOffset.UtcNow);
            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        var request = new ChatCompletionRequest(
            Model: "openai/gpt-4o-mini",
            Messages: [new ChatMessage("user", "Hello")]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var response = await _factory.CreateClient().SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostChatCompletions_WithStreaming_StreamsSSEChunks()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 500);

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "StreamKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        var request = new ChatCompletionRequest(
            Model: "openai/gpt-4o-mini",
            Messages: [new ChatMessage("user", "Hello stream")],
            Stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var response = await _factory.CreateClient().SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var streamContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("data: ", streamContent);
        Assert.Contains("data: [DONE]", streamContent);
    }

    [Fact]
    public async Task PostChatCompletions_WithToolsAndAgentPayload_PreservesParameters()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 500);

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "AgentKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        // Request payload simulating agent tool-calling format
        var agentJson = """
        {
          "model": "openai/gpt-4o-mini",
          "messages": [
            { "role": "user", "content": "What is the weather?" }
          ],
          "tools": [
            {
              "type": "function",
              "function": {
                "name": "get_weather",
                "description": "Get current weather",
                "parameters": { "type": "object", "properties": { "location": { "type": "string" } } }
              }
            }
          ],
          "tool_choice": "auto"
        }
        """;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(agentJson, System.Text.Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var response = await _factory.CreateClient().SendAsync(httpRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProjectAIUsage_IsPublicAndReturnsUsageSummary()
    {
        var ownerId = _factory.SeedUser();
        var projectId = _factory.SeedProject(ownerId);
        _factory.SeedCredit(projectId, 1000);

        var (rawKey, keyPrefix, keyHash) = ApiKeyHasher.GenerateKey();

        _factory.ReadDb(db =>
        {
            var apiKey = ProjectApiKey.Create(
                projectId,
                ownerId,
                "CliKey",
                keyPrefix,
                keyHash,
                DateTimeOffset.UtcNow);

            db.ProjectApiKeys.Add(apiKey);
            db.SaveChanges();
            return true;
        });

        var request = new ChatCompletionRequest(
            Model: "openai/gpt-4o-mini",
            Messages: [new ChatMessage("user", "Hello")]);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rawKey);

        var chatResponse = await _factory.CreateClient().SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        // An unauthenticated client should be able to fetch the project's AI usage
        var unauthenticatedClient = _factory.CreateClient();
        var usageResponse = await unauthenticatedClient.GetAsync($"/projects/{projectId}/ai-usage");

        Assert.Equal(HttpStatusCode.OK, usageResponse.StatusCode);
        var usages = await usageResponse.Content.ReadFromJsonAsync<List<AIUsageSummaryResponse>>();
        Assert.NotNull(usages);
        Assert.Single(usages);
        Assert.Equal("openai/gpt-4o-mini", usages[0].Model);
        Assert.Equal(projectId, usages[0].ProjectId);
    }
}
