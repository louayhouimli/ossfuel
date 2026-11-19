export interface ProjectRecommendationCitation {
  chunkId: string
  headerPath?: string | null
  quote: string
}

export interface ProjectRecommendationCard {
  projectId: string
  projectName: string
  matchLevel: string
  category: string
  technologies: string[]
  whyItMatches: string
  activitySummary: string
  evidence: string
  citations: ProjectRecommendationCitation[]
}

export interface ChatMessage {
  id?: string
  sender: "bot" | "user"
  text: string
  time?: string
  recommendations?: ProjectRecommendationCard[]
}

export interface AssistantChatResponse {
  reply: string
  recommendations: ProjectRecommendationCard[]
}

export interface AssistantChatRequest {
  messages: Array<{
    sender: "bot" | "user" | string
    text: string
  }>
  language?: string | null
  minStars?: number | null
}
