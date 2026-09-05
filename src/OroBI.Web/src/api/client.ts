import { expireAccessToken } from '../auth/session'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

export async function authenticatedFetch(path: string, token: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers })
  if (response.status === 401) {
    expireAccessToken(token)
    throw new Error('Sua sessão expirou. Entre novamente para continuar.')
  }
  return response
}

export async function apiRequest<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const response = await authenticatedFetch(path, token, init)
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { error?: unknown } | null
    const message = typeof error?.error === 'string' ? error.error : `API request failed: ${response.status}`
    throw new Error(message)
  }
  return response.json() as Promise<T>
}

export { apiBaseUrl }
