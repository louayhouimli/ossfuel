using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OSSFuel.Api.Features.Discovery.Gemini;

public sealed class GeminiClient : IGeminiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient>? _logger;
    private readonly TimeProvider _timeProvider;

    public GeminiClient(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiClient>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return Array.Empty<ReadOnlyMemory<float>>();
        }

        EnsureApiKeyConfigured();

        var cleanModel = NormalizeModelName(_options.EmbeddingModel);
        var requestModel = FormatModelForRequestBody(cleanModel);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{cleanModel}:batchEmbedContents";

        var batchSize = Math.Clamp(_options.BatchSize, 1, 100);
        var results = new List<ReadOnlyMemory<float>>(texts.Count);

        for (var i = 0; i < texts.Count; i += batchSize)
        {
            var currentBatchCount = Math.Min(batchSize, texts.Count - i);
            var batchRequests = new List<EmbedContentRequest>(currentBatchCount);

            for (var j = 0; j < currentBatchCount; j++)
            {
                var text = texts[i + j];
                batchRequests.Add(new EmbedContentRequest(
                    Model: requestModel,
                    Content: new GeminiContent([new GeminiPart(text)]),
                    OutputDimensionality: 768));
            }

            var requestDto = new BatchEmbedContentsRequest(batchRequests);

            using var response = await SendWithRetryAsync(
                () => CreateJsonRequest(HttpMethod.Post, url, requestDto),
                HttpCompletionOption.ResponseContentRead,
                ct);

            await EnsureSuccessStatusCodeAsync(response, ct);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var batchResponse = await JsonSerializer.DeserializeAsync<BatchEmbedContentsResponse>(stream, JsonOptions, ct);

            if (batchResponse?.Embeddings is null || batchResponse.Embeddings.Count != currentBatchCount)
            {
                throw new GeminiApiException(
                    $"Gemini API returned invalid embeddings count: expected {currentBatchCount}, got {batchResponse?.Embeddings?.Count ?? 0}.");
            }

            for (var k = 0; k < batchResponse.Embeddings.Count; k++)
            {
                var values = batchResponse.Embeddings[k].Values;
                if (values is null || values.Length == 0)
                {
                    throw new GeminiApiException($"Gemini API returned an empty embedding vector at index {i + k}.");
                }

                results.Add(new ReadOnlyMemory<float>(values));
            }
        }

        return results;
    }

    public async Task<ReadOnlyMemory<float>> EmbedQueryAsync(
        string query,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var results = await EmbedBatchAsync([query], ct);
        if (results.Count == 0)
        {
            throw new GeminiApiException("Gemini API returned no embeddings for query.");
        }

        return results[0];
    }

    public async Task<string> GenerateContentAsync(
        string prompt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        EnsureApiKeyConfigured();

        var cleanModel = NormalizeModelName(_options.ChatModel);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{cleanModel}:generateContent";

        var requestDto = new GenerateContentRequest(
            [new GeminiContent([new GeminiPart(prompt)])]);

        using var response = await SendWithRetryAsync(
            () => CreateJsonRequest(HttpMethod.Post, url, requestDto),
            HttpCompletionOption.ResponseContentRead,
            ct);

        await EnsureSuccessStatusCodeAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var generateResponse = await JsonSerializer.DeserializeAsync<GenerateContentResponse>(stream, JsonOptions, ct);

        var candidate = generateResponse?.Candidates?.FirstOrDefault();
        var parts = candidate?.Content?.Parts;

        if (parts is null || parts.Count == 0)
        {
            throw new GeminiApiException("Gemini API returned no candidate text in response.");
        }

        return string.Concat(parts.Select(p => p.Text));
    }

    public async IAsyncEnumerable<string> StreamContentAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        EnsureApiKeyConfigured();

        var cleanModel = NormalizeModelName(_options.ChatModel);
        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{cleanModel}:streamGenerateContent?alt=sse";

        var requestDto = new GenerateContentRequest(
            [new GeminiContent([new GeminiPart(prompt)])]);

        using var response = await SendWithRetryAsync(
            () => CreateJsonRequest(HttpMethod.Post, url, requestDto),
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        await EnsureSuccessStatusCodeAsync(response, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var json = line["data:".Length..].Trim();
            if (string.IsNullOrEmpty(json) || string.Equals(json, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GenerateContentResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<GenerateContentResponse>(json, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            var parts = chunk?.Candidates?.FirstOrDefault()?.Content?.Parts;
            if (parts is { Count: > 0 })
            {
                foreach (var part in parts)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        yield return part.Text;
                    }
                }
            }
        }
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new GeminiApiException("Gemini API key is not configured.");
        }
    }

    private HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string url, T body)
    {
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
        }

        var json = JsonSerializer.Serialize(body, JsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return request;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        HttpCompletionOption completionOption,
        CancellationToken ct)
    {
        var maxRetries = Math.Max(0, _options.MaxRetries);
        var initialDelay = _options.InitialRetryDelay;

        for (var attempt = 0; ; attempt++)
        {
            HttpRequestMessage request = requestFactory();
            HttpResponseMessage? response = null;
            Exception? caughtException = null;

            try
            {
                response = await _httpClient.SendAsync(request, completionOption, ct);
            }
            catch (HttpRequestException ex)
            {
                caughtException = ex;
            }

            var isTransient = caughtException is not null
                || (response is not null && (response.StatusCode == HttpStatusCode.TooManyRequests
                    || ((int)response.StatusCode >= 500 && (int)response.StatusCode <= 599)));

            if (!isTransient || attempt >= maxRetries)
            {
                if (caughtException is not null)
                {
                    throw new GeminiApiException($"Gemini API request failed: {caughtException.Message}", caughtException);
                }

                return response!;
            }

            _logger?.LogWarning(
                "Gemini request failed (status: {StatusCode}, exception: {Exception}). Retrying attempt {Attempt} of {MaxRetries}...",
                response?.StatusCode,
                caughtException?.Message,
                attempt + 1,
                maxRetries);

            response?.Dispose();

            var delay = initialDelay * Math.Pow(2, attempt);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _timeProvider, ct);
            }
        }
    }

    private static async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        throw new GeminiApiException(
            $"Gemini API returned HTTP {(int)response.StatusCode} ({response.StatusCode}): {errorBody}",
            response.StatusCode,
            errorBody);
    }

    private static string NormalizeModelName(string model)
    {
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model["models/".Length..]
            : model;
    }

    private static string FormatModelForRequestBody(string cleanModel)
    {
        return $"models/{cleanModel}";
    }
}
