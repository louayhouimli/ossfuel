namespace OSSFuel.Api.Features.Gateway;

public interface IAIProviderClient
{
    string ProviderName { get; }

    Task<ChatCompletionProviderResult> CompleteChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken);

    Task<string> GetModelsJsonAsync(CancellationToken cancellationToken);
}
