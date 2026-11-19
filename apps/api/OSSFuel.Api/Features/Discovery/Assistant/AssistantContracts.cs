namespace OSSFuel.Api.Features.Discovery.Assistant;

public record ChatMessage(string Sender, string Text);

public record AssistantChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? Language = null,
    int? MinStars = null);

public record ProjectRecommendationCitation(
    Guid ChunkId,
    string? HeaderPath,
    string Quote);

public record ProjectRecommendationCard(
    Guid ProjectId,
    string ProjectName,
    string MatchLevel,
    string Category,
    string[] Technologies,
    string WhyItMatches,
    string ActivitySummary,
    string Evidence,
    IReadOnlyList<ProjectRecommendationCitation> Citations);

public record AssistantChatResponse(
    string Reply,
    IReadOnlyList<ProjectRecommendationCard> Recommendations);
