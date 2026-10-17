export const API_URL: string =
  import.meta.env.VITE_API_URL ?? "http://localhost:5050"

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = "ApiError"
    this.status = status
  }
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)

  if (init?.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json")
  }

  const response = await fetch(`${API_URL}${path}`, {
    credentials: "include",
    ...init,
    headers,
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readErrorMessage(response))
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as {
      detail?: string
      title?: string
      errors?: Record<string, string[]>
    }

    const validationMessage = body.errors
      ? Object.values(body.errors)[0]?.[0]
      : undefined

    return (
      validationMessage ??
      body.detail ??
      body.title ??
      `Request failed (${response.status} ${response.statusText})`
    )
  } catch {
    return `Request failed (${response.status} ${response.statusText})`
  }
}
