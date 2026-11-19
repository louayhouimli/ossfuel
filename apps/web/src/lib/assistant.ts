import { apiFetch } from "./api"
import type {
  AssistantChatRequest,
  AssistantChatResponse,
} from "@/components/assistant/types"

export async function sendAssistantMessage(
  request: AssistantChatRequest
): Promise<AssistantChatResponse> {
  return apiFetch<AssistantChatResponse>("/assistant/chat", {
    method: "POST",
    body: JSON.stringify(request),
  })
}
