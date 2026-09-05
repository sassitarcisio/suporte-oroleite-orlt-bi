const tokenKey = 'orobi.access-token'
export const sessionExpiredEvent = 'orobi:session-expired'

export function readAccessToken(): string {
  return sessionStorage.getItem(tokenKey) ?? ''
}

export function saveAccessToken(token: string): void {
  sessionStorage.setItem(tokenKey, token)
}

export function clearAccessToken(): void {
  sessionStorage.removeItem(tokenKey)
}

export function expireAccessToken(requestToken: string): void {
  // A response from a previous login must not invalidate the current session.
  if (!requestToken || readAccessToken() !== requestToken) return
  clearAccessToken()
  window.dispatchEvent(new Event(sessionExpiredEvent))
}
