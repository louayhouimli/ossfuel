using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace OSSFuel.Api.Features.Gateway;

public sealed class OpenRouterClient(
    HttpClient httpClient,
    IOptions<GatewayOptions> options) : IAIProviderClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ProviderName => AIUsage.OpenRouterProvider;

    public async Task<ChatCompletionProviderResult> CompleteChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        var endpoint = $"{config.BaseUrl.TrimEnd('/')}/chat/completions";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        httpRequest.Headers.TryAddWithoutValidation("HTTP-Referer", "https://ossfuel-web.vercel.app");
        httpRequest.Headers.TryAddWithoutValidation("X-Title", "OSSFuel");

        var payload = new
        {
            model = request.Model,
            messages = request.Messages,
            stream = request.Stream,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream_options = request.Stream ? new { include_usage = true } : null,
        };

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(
            httpRequest,
            request.Stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ChatCompletionProviderResult(
                Succeeded: false,
                StatusCode: (int)response.StatusCode,
                ResponseJson: errorBody,
                PromptTokens: 0,
                CompletionTokens: 0,
                ProviderCostUsd: null);
        }

        if (request.Stream)
        {
            var chunks = new List<string>();
            var promptTokens = 0;
            var completionTokens = 0;
            decimal? costUsd = null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = line["data:".Length..].Trim();

                if (string.IsNullOrEmpty(data) || string.Equals(data, "[DONE]", StringComparison.Ordinal))
                {
                    continue;
                }

                chunks.Add(data);
                TryExtractUsage(data, ref promptTokens, ref completionTokens, ref costUsd);
            }

            return new ChatCompletionProviderResult(
                Succeeded: true,
                StatusCode: (int)response.StatusCode,
                ResponseJson: chunks.LastOrDefault() ?? "{}",
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                ProviderCostUsd: costUsd,
                StreamChunks: chunks);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsedPrompt = 0;
        var parsedCompletion = 0;
        decimal? parsedCost = null;

        TryExtractUsage(body, ref parsedPrompt, ref parsedCompletion, ref parsedCost);

        return new ChatCompletionProviderResult(
            Succeeded: true,
            StatusCode: (int)response.StatusCode,
            ResponseJson: body,
            PromptTokens: parsedPrompt,
            CompletionTokens: parsedCompletion,
            ProviderCostUsd: parsedCost);
    }

    private static void TryExtractUsage(
        string json,
        ref int promptTokens,
        ref int completionTokens,
        ref decimal? costUsd)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("usage", out var usage)
                || usage.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if ((usage.TryGetProperty("prompt_tokens", out var pt) || usage.TryGetProperty("input_tokens", out pt)) && pt.TryGetInt32(out var ptVal))
            {
                promptTokens = ptVal;
            }

            if ((usage.TryGetProperty("completion_tokens", out var ct) || usage.TryGetProperty("output_tokens", out ct)) && ct.TryGetInt32(out var ctVal))
            {
                completionTokens = ctVal;
            }

            if (usage.TryGetProperty("cost", out var cost) && cost.TryGetDecimal(out var costVal))
            {
                costUsd = costVal;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed intermediate SSE lines.
        }
    }

    public async Task<ChatCompletionProviderResult> CompleteResponsesAsync(
        string requestJson,
        bool isStream,
        CancellationToken cancellationToken)
    {
        var config = options.Value;
        var endpoint = $"{config.BaseUrl.TrimEnd('/')}/responses";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        httpRequest.Headers.TryAddWithoutValidation("HTTP-Referer", "https://ossfuel-web.vercel.app");
        httpRequest.Headers.TryAddWithoutValidation("X-Title", "OSSFuel");

        httpRequest.Content = new StringContent(
            requestJson,
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(
            httpRequest,
            isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ChatCompletionProviderResult(
                Succeeded: false,
                StatusCode: (int)response.StatusCode,
                ResponseJson: errorBody,
                PromptTokens: 0,
                CompletionTokens: 0,
                ProviderCostUsd: null);
        }

        if (isStream)
        {
            var chunks = new List<string>();
            var promptTokens = 0;
            var completionTokens = 0;
            decimal? costUsd = null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var data = line["data:".Length..].Trim();

                if (string.IsNullOrEmpty(data) || string.Equals(data, "[DONE]", StringComparison.Ordinal))
                {
                    continue;
                }

                chunks.Add(data);
                TryExtractUsage(data, ref promptTokens, ref completionTokens, ref costUsd);
            }

            return new ChatCompletionProviderResult(
                Succeeded: true,
                StatusCode: (int)response.StatusCode,
                ResponseJson: chunks.LastOrDefault() ?? "{}",
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                ProviderCostUsd: costUsd,
                StreamChunks: chunks);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var pTokens = 0;
        var cTokens = 0;
        decimal? cost = null;
        TryExtractUsage(responseBody, ref pTokens, ref cTokens, ref cost);

        return new ChatCompletionProviderResult(
            Succeeded: true,
            StatusCode: (int)response.StatusCode,
            ResponseJson: responseBody,
            PromptTokens: pTokens,
            CompletionTokens: cTokens,
            ProviderCostUsd: cost);
    }

    private static (string Json, DateTimeOffset ExpiresAt)? _cachedModels;
    private static readonly SemaphoreSlim _modelsLock = new(1, 1);

    public async Task<string> GetModelsJsonAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedModels is { } cached && cached.ExpiresAt > now)
        {
            return cached.Json;
        }

        await _modelsLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_cachedModels is { } doubleChecked && doubleChecked.ExpiresAt > now)
            {
                return doubleChecked.Json;
            }

            var config = options.Value;
            var endpoint = $"{config.BaseUrl.TrimEnd('/')}/models";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            if (!string.IsNullOrWhiteSpace(config.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            }

            request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://ossfuel-web.vercel.app");
            request.Headers.TryAddWithoutValidation("X-Title", "OSSFuel");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                _cachedModels = (json, now.AddMinutes(15));
                return json;
            }

            if (_cachedModels is { } stale)
            {
                return stale.Json;
            }

            return FallbackModelsJson;
        }
        catch
        {
            if (_cachedModels is { } stale)
            {
                return stale.Json;
            }

            return FallbackModelsJson;
        }
        finally
        {
            _modelsLock.Release();
        }
    }

    private const string FallbackModelsJson = """
    {
      "object": "list",
      "data": [
        { "id": "openrouter/free", "object": "model", "name": "Free Models Router", "owned_by": "openrouter" },
        { "id": "meta-llama/llama-3.3-70b-instruct:free", "object": "model", "name": "Llama 3.3 70B (free)", "owned_by": "meta" },
        { "id": "deepseek/deepseek-r1:free", "object": "model", "name": "DeepSeek R1 (free)", "owned_by": "deepseek" },
        { "id": "deepseek/deepseek-chat:free", "object": "model", "name": "DeepSeek Chat (free)", "owned_by": "deepseek" },
        { "id": "qwen/qwen-2.5-coder-32b-instruct:free", "object": "model", "name": "Qwen 2.5 Coder 32B (free)", "owned_by": "qwen" },
        { "id": "google/gemini-2.0-flash-exp:free", "object": "model", "name": "Gemini 2.0 Flash Exp (free)", "owned_by": "google" },
        { "id": "mistralai/mistral-7b-instruct:free", "object": "model", "name": "Mistral 7B (free)", "owned_by": "mistralai" }
      ]
    }
    """;
}
