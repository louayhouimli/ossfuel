export const API_URL: string =
  import.meta.env.VITE_API_URL ?? "http://localhost:5050"

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    credentials: "include",
    ...init,
  })

  if (!response.ok) {
    throw new Error(`API request failed (${response.status} ${response.statusText})`)
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}