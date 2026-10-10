import { queryOptions } from "@tanstack/react-query"
import { apiFetch } from "./api"

export type AuthUser = {
  id: string
  login: string
  name: string | null
  email: string | null
  avatarUrl: string | null
}

export const authQueryOptions = () =>
  queryOptions({
    queryKey: ["auth", "user"],
    queryFn: async () => {
      try {
        return await apiFetch<AuthUser>("/validate")
      } catch {
        return null
      }
    },
    retry: false,
    staleTime: 60_000,
  })