using System.Text.Json;

namespace OSSFuel.Api.Features.Gateway;

public sealed class MockAIProviderClient : IAIProviderClient
{
    public const string Name = "mock";

    public string ProviderName => AIUsage.OpenRouterProvider;

    public Task<ChatCompletionProviderResult> CompleteChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var promptText = string.Join(" ", request.Messages.Select(message => message.Content));
        var promptTokens = Math.Max(10, promptText.Length / 4);
        var completionTokens = 25;

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (request.Stream)
        {
            var chunk1 = JsonSerializer.Serialize(new
            {
                id = completionId,
                @object = "chat.completion.chunk",
                created,
                model = request.Model,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { role = "assistant", content = "OSSFuel streamed " },
                        finish_reason = (string?)null,
                    }
                }
            });

            var chunk2 = JsonSerializer.Serialize(new
            {
                id = completionId,
                @object = "chat.completion.chunk",
                created,
                model = request.Model,
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        delta = new { content = "response." },
                        finish_reason = "stop",
                    }
                },
                usage = new
                {
                    prompt_tokens = promptTokens,
                    completion_tokens = completionTokens,
                    total_tokens = promptTokens + completionTokens,
                }
            });

            return Task.FromResult(new ChatCompletionProviderResult(
                Succeeded: true,
                StatusCode: StatusCodes.Status200OK,
                ResponseJson: chunk2,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                ProviderCostUsd: null,
                StreamChunks: [chunk1, chunk2]));
        }

        var responseJson = JsonSerializer.Serialize(new
        {
            id = completionId,
            @object = "chat.completion",
            created,
            model = request.Model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "OSSFuel gateway response.",
                    },
                    finish_reason = "stop",
                }
            },
            usage = new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
            }
        });

        return Task.FromResult(new ChatCompletionProviderResult(
            Succeeded: true,
            StatusCode: StatusCodes.Status200OK,
            ResponseJson: responseJson,
            PromptTokens: promptTokens,
            CompletionTokens: completionTokens,
            ProviderCostUsd: null));
    }

    public Task<ChatCompletionProviderResult> CompleteResponsesAsync(
        string requestJson,
        bool isStream,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ChatCompletionProviderResult(
            Succeeded: true,
            StatusCode: StatusCodes.Status200OK,
            ResponseJson: """{"id":"resp_mock","object":"response","status":"completed","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"OSSFuel mock response."}]}],"usage":{"input_tokens":10,"output_tokens":20}}""",
            PromptTokens: 10,
            CompletionTokens: 20,
            ProviderCostUsd: null,
            StreamChunks: isStream ? new[] { """{"type":"response.reasoning_text.delta","delta":"mock"}""" } : null));
    }

    public Task<string> GetModelsJsonAsync(CancellationToken cancellationToken)
    {
        const string mockModels = """
        {
          "object": "list",
          "data": [
            { "id": "openrouter/free", "object": "model", "name": "Free Models Router", "owned_by": "openrouter" },
            { "id": "meta-llama/llama-3.3-70b-instruct:free", "object": "model", "name": "Llama 3.3 70B (free)", "owned_by": "meta" },
            { "id": "deepseek/deepseek-r1:free", "object": "model", "name": "DeepSeek R1 (free)", "owned_by": "deepseek" }
          ]
        }
        """;

        return Task.FromResult(mockModels);
    }
}
