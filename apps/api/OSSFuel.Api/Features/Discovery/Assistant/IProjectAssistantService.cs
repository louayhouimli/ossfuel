namespace OSSFuel.Api.Features.Discovery.Assistant;

public interface IProjectAssistantService
{
    Task<AssistantChatResponse> ChatAsync(AssistantChatRequest request, CancellationToken ct = default);
}
