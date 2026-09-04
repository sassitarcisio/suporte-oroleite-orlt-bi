const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

export async function apiRequest<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${token}`)

  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers })
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { error?: unknown } | null
    const message = typeof error?.error === 'string' ? error.error : `API request failed: ${response.status}`
    throw new Error(message)
  }
  return response.json() as Promise<T>
}

export { apiBaseUrl }
