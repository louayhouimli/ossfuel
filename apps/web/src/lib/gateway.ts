import { queryOptions } from "@tanstack/react-query"
import { apiFetch } from "@/lib/api"

export interface ApiKeySummary {
  id: string
  projectId: string
  name: string
  keyPrefix: string
  isActive: boolean
  createdAt: string
  lastUsedAt?: string | null
  revokedAt?: string | null
}

export interface CreateApiKeyResponse {
  id: string
  projectId: string
  name: string
  rawKey: string
  keyPrefix: string
  createdAt: string
}

export interface AIUsageSummary {
  id: string
  projectId: string
  apiKeyId: string
  model: string
  provider: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
  costMinor: number
  status: string
  createdAt: string
}

export const apiKeysQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: ["projects", projectId, "api-keys"],
    queryFn: () => apiFetch<ApiKeySummary[]>(`/projects/${projectId}/api-keys`),
  })

export const aiUsageQueryOptions = (projectId: string) =>
  queryOptions({
    queryKey: ["projects", projectId, "ai-usage"],
    queryFn: () => apiFetch<AIUsageSummary[]>(`/projects/${projectId}/ai-usage`),
  })

export async function createApiKey(
  projectId: string,
  name: string
): Promise<CreateApiKeyResponse> {
  return apiFetch<CreateApiKeyResponse>(`/projects/${projectId}/api-keys`, {
    method: "POST",
    body: JSON.stringify({ name }),
  })
}

export async function revokeApiKey(
  projectId: string,
  keyId: string
): Promise<void> {
  return apiFetch<void>(`/projects/${projectId}/api-keys/${keyId}`, {
    method: "DELETE",
  })
}
