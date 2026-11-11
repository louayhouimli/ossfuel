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
}
