namespace OSSFuel.Api.Features.Gateway;

public interface IAIProviderClient
{
    string ProviderName { get; }

    Task<ChatCompletionProviderResult> CompleteChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken,
        Func<string, Task>? onChunk = null);

    Task<ChatCompletionProviderResult> CompleteResponsesAsync(
        string requestJson,
        bool isStream,
        CancellationToken cancellationToken,
        Func<string, Task>? onChunk = null);

    Task<string> GetModelsJsonAsync(CancellationToken cancellationToken);
}
