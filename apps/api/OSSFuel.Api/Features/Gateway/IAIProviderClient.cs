namespace OSSFuel.Api.Features.Gateway;

public interface IAIProviderClient
{
    string ProviderName { get; }

    Task<ChatCompletionProviderResult> CompleteChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken);

    Task<ChatCompletionProviderResult> CompleteResponsesAsync(
        string requestJson,
        bool isStream,
        CancellationToken cancellationToken);

    Task<string> GetModelsJsonAsync(CancellationToken cancellationToken);
}
