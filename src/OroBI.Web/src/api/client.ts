import { cookieSessionMode, expireAccessToken, requirePasswordChange } from '../auth/session'

const apiBaseUrl = (cookieSessionMode ? import.meta.env.VITE_COOKIE_API_BASE_URL ?? 'https://api-bi.oroleite.com.br' : import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export function sessionRequestInit(init: RequestInit = {}): RequestInit {
  if (!cookieSessionMode) return init
  const headers = new Headers(init.headers)
  headers.delete('Authorization')
  headers.set('X-OroBI-Session', 'cookie')
  return { ...init, headers, credentials: 'include' }
}

export async function authenticatedFetch(path: string, token: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  if (!cookieSessionMode) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(`${apiBaseUrl}${path}`, sessionRequestInit({ ...init, headers }))
  if (response.status === 401) {
    expireAccessToken(token)
    throw new Error('Sua sessão expirou. Entre novamente para continuar.')
  }
  if (response.status === 403) {
    const problem = await response.clone().json().catch(() => null) as { code?: string } | null
    if (problem?.code === 'password_change_required') requirePasswordChange(token)
  }
  return response
}

export async function apiRequest<T>(path: string, token: string, init: RequestInit = {}): Promise<T> {
  const response = await authenticatedFetch(path, token, init)
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { error?: unknown; errors?: unknown } | null
    const validation = Array.isArray(error?.errors) ? error.errors.filter((item): item is string => typeof item === 'string').join(' ') : ''
    const message = typeof error?.error === 'string' ? error.error : validation || `API request failed: ${response.status}`
    throw new Error(message)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export { apiBaseUrl }
