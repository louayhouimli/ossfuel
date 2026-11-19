using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OSSFuel.Api.Features.Discovery.Gemini;

namespace OSSFuel.UnitTests.Features.Discovery;

public class GeminiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task EmbedBatchAsync_FormatsRequestCorrectly_AndParses768Vectors()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var sampleVector1 = GenerateVector(768, 0.1f);
        var sampleVector2 = GenerateVector(768, -0.2f);

        var responseDto = new BatchEmbedContentsResponse([
            new GeminiContentEmbedding(sampleVector1),
            new GeminiContentEmbedding(sampleVector2)
        ]);

        var handler = new StubHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            capturedBody = req.Content is not null ? await req.Content.ReadAsStringAsync() : null;
            return Json(responseDto);
        });

        var options = new GeminiOptions
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
            EmbeddingModel = "text-embedding-004",
            InitialRetryDelay = TimeSpan.Zero
        };

        var client = CreateClient(handler, options);

        var inputTexts = new[] { "Text chunk one", "Text chunk two" };
        var results = await client.EmbedBatchAsync(inputTexts);

        // Verify request structure
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:batchEmbedContents", capturedRequest.RequestUri?.ToString());
        Assert.True(capturedRequest.Headers.TryGetValues("x-goog-api-key", out var apiKeys));
        Assert.Equal("test-api-key", apiKeys.First());

        Assert.NotNull(capturedBody);
        var parsedRequest = JsonSerializer.Deserialize<BatchEmbedContentsRequest>(capturedBody!, JsonOptions);
        Assert.NotNull(parsedRequest);
        Assert.Equal(2, parsedRequest!.Requests.Count);

        Assert.Equal("models/text-embedding-004", parsedRequest.Requests[0].Model);
        Assert.Equal(768, parsedRequest.Requests[0].OutputDimensionality);
        Assert.Equal("Text chunk one", parsedRequest.Requests[0].Content.Parts[0].Text);

        Assert.Equal("models/text-embedding-004", parsedRequest.Requests[1].Model);
        Assert.Equal(768, parsedRequest.Requests[1].OutputDimensionality);
        Assert.Equal("Text chunk two", parsedRequest.Requests[1].Content.Parts[0].Text);

        // Verify returned embeddings
        Assert.Equal(2, results.Count);
        Assert.Equal(768, results[0].Length);
        Assert.Equal(768, results[1].Length);
        Assert.Equal(0.1f, results[0].Span[0]);
        Assert.Equal(-0.2f, results[1].Span[0]);
    }

    [Fact]
    public async Task EmbedBatchAsync_WhenMoreThan100Texts_BatchesRequestsInChunksOf100()
    {
        var requestCount = 0;
        var batchSizes = new List<int>();

        var handler = new StubHttpMessageHandler(async req =>
        {
            requestCount++;
            var body = await req.Content!.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<BatchEmbedContentsRequest>(body, JsonOptions);
            batchSizes.Add(parsed!.Requests.Count);

            var embeddings = parsed.Requests
                .Select(_ => new GeminiContentEmbedding(GenerateVector(768, 0.5f)))
                .ToList();

            return Json(new BatchEmbedContentsResponse(embeddings));
        });

        var options = new GeminiOptions
        {
            ApiKey = "test-key",
            InitialRetryDelay = TimeSpan.Zero
        };

        var client = CreateClient(handler, options);

        // 105 texts: should split into 100 + 5
        var texts = Enumerable.Range(1, 105).Select(i => $"Text chunk {i}").ToList();
        var results = await client.EmbedBatchAsync(texts);

        Assert.Equal(2, requestCount);
        Assert.Equal([100, 5], batchSizes);
        Assert.Equal(105, results.Count);
    }

    [Fact]
    public async Task EmbedBatchAsync_WhenEmpty_ReturnsEmptyWithoutCallingHttp()
    {
        var called = false;
        var handler = new StubHttpMessageHandler(_ =>
        {
            called = true;
            return Json(new BatchEmbedContentsResponse([]));
        });

        var client = CreateClient(handler, new GeminiOptions { ApiKey = "test-key" });
        var results = await client.EmbedBatchAsync([]);

        Assert.False(called);
        Assert.Empty(results);
    }

    [Fact]
    public async Task EmbedQueryAsync_DelegatesToBatchEmbedProperly()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var sampleVector = GenerateVector(768, 0.42f);
        var responseDto = new BatchEmbedContentsResponse([
            new GeminiContentEmbedding(sampleVector)
        ]);

        var handler = new StubHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            capturedBody = await req.Content!.ReadAsStringAsync();
            return Json(responseDto);
        });

        var client = CreateClient(handler, new GeminiOptions { ApiKey = "test-key" });
        var result = await client.EmbedQueryAsync("c# open source funding");

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedBody);
        var parsedRequest = JsonSerializer.Deserialize<BatchEmbedContentsRequest>(capturedBody!, JsonOptions);
        Assert.NotNull(parsedRequest);
        var single = Assert.Single(parsedRequest!.Requests);
        Assert.Equal("c# open source funding", single.Content.Parts[0].Text);

        Assert.Equal(768, result.Length);
        Assert.Equal(0.42f, result.Span[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmbedQueryAsync_WhenQueryInvalid_ThrowsArgumentException(string? invalidQuery)
    {
        var handler = new StubHttpMessageHandler(_ => Json(new BatchEmbedContentsResponse([])));
        var client = CreateClient(handler, new GeminiOptions { ApiKey = "test-key" });

        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.EmbedQueryAsync(invalidQuery!));
    }

    [Fact]
    public async Task SendWithRetry_OnHttp429_RetriesAndSucceeds()
    {
        var callCount = 0;
        var sampleVector = GenerateVector(768, 0.99f);

        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\": {\"message\": \"Resource exhausted\"}}", Encoding.UTF8, "application/json")
                };
            }

            return Json(new BatchEmbedContentsResponse([
                new GeminiContentEmbedding(sampleVector)
            ]));
        });

        var options = new GeminiOptions
        {
            ApiKey = "test-key",
            MaxRetries = 2,
            InitialRetryDelay = TimeSpan.Zero
        };

        var client = CreateClient(handler, options);
        var results = await client.EmbedBatchAsync(["Hello retry"]);

        Assert.Equal(2, callCount);
        Assert.Single(results);
        Assert.Equal(0.99f, results[0].Span[0]);
    }

    [Fact]
    public async Task SendWithRetry_WhenRetriesExhausted_ThrowsGeminiApiException()
    {
        var callCount = 0;

        var handler = new StubHttpMessageHandler(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("Rate limit exceeded", Encoding.UTF8, "text/plain")
            };
        });

        var options = new GeminiOptions
        {
            ApiKey = "test-key",
            MaxRetries = 2,
            InitialRetryDelay = TimeSpan.Zero
        };

        var client = CreateClient(handler, options);

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => client.EmbedBatchAsync(["Fail please"]));
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("Rate limit exceeded", ex.ResponseBody);
        Assert.Equal(3, callCount); // 1 initial + 2 retries
    }

    [Fact]
    public async Task GenerateContentAsync_FormatsRequest_AndReturnsCandidateText()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var generateResponse = new GenerateContentResponse([
            new GeminiCandidate(
                Content: new GeminiContent([new GeminiPart("OSS Fuel helps open source creators sustain their projects.")]),
                FinishReason: "STOP")
        ]);

        var handler = new StubHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            capturedBody = await req.Content!.ReadAsStringAsync();
            return Json(generateResponse);
        });

        var options = new GeminiOptions
        {
            ApiKey = "chat-api-key",
            ChatModel = "gemini-1.5-flash",
            InitialRetryDelay = TimeSpan.Zero
        };

        var client = CreateClient(handler, options);
        var reply = await client.GenerateContentAsync("Explain OSS Fuel");

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent", capturedRequest!.RequestUri?.ToString());
        Assert.True(capturedRequest.Headers.TryGetValues("x-goog-api-key", out var keys));
        Assert.Equal("chat-api-key", keys.First());

        Assert.NotNull(capturedBody);
        var requestDto = JsonSerializer.Deserialize<GenerateContentRequest>(capturedBody!, JsonOptions);
        Assert.NotNull(requestDto);
        Assert.Equal("Explain OSS Fuel", requestDto!.Contents[0].Parts[0].Text);

        Assert.Equal("OSS Fuel helps open source creators sustain their projects.", reply);
    }

    [Fact]
    public async Task StreamContentAsync_YieldsStreamedChunks()
    {
        var sseData = new StringBuilder()
            .AppendLine("data: {\"candidates\": [{\"content\": {\"parts\": [{\"text\": \"Chunk 1: \"}]}}]}")
            .AppendLine()
            .AppendLine("data: {\"candidates\": [{\"content\": {\"parts\": [{\"text\": \"Chunk 2\"}]}}]}")
            .AppendLine()
            .AppendLine("data: [DONE]")
            .ToString();

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseData, Encoding.UTF8, "text/event-stream")
        });

        var options = new GeminiOptions
        {
            ApiKey = "stream-key",
            InitialRetryDelay = TimeSpan.Zero
        };

        var client = CreateClient(handler, options);

        var chunks = new List<string>();
        await foreach (var chunk in client.StreamContentAsync("Stream test"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Chunk 1: ", chunks[0]);
        Assert.Equal("Chunk 2", chunks[1]);
    }

    [Fact]
    public async Task WhenApiKeyMissing_ThrowsGeminiApiException()
    {
        var handler = new StubHttpMessageHandler(_ => Json(new BatchEmbedContentsResponse([])));
        var client = CreateClient(handler, new GeminiOptions { ApiKey = null });

        var ex = await Assert.ThrowsAsync<GeminiApiException>(() => client.EmbedBatchAsync(["Any text"]));
        Assert.Contains("Gemini API key is not configured", ex.Message);
    }

    private static float[] GenerateVector(int dimensions, float fillValue)
    {
        var vector = new float[dimensions];
        Array.Fill(vector, fillValue);
        return vector;
    }

    private static GeminiClient CreateClient(StubHttpMessageHandler handler, GeminiOptions options) =>
        new(new HttpClient(handler), Options.Create(options));

    private static HttpResponseMessage Json<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> syncResponder)
            : this(req => Task.FromResult(syncResponder(req)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responder(request);
    }
}
