using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OSSFuel.Api.Features.Gateway;

public sealed class OpenRouterClient(
    HttpClient httpClient,
    IOptions<GatewayOptions> options) : IAIProviderClient
{
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
            JsonSerializer.Serialize(payload),
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

            if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.TryGetInt32(out var ptVal))
            {
                promptTokens = ptVal;
            }

            if (usage.TryGetProperty("completion_tokens", out var ct) && ct.TryGetInt32(out var ctVal))
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
}
