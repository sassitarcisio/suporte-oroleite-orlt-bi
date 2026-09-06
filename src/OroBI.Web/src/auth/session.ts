const tokenKey = 'orobi.access-token'
const expiryKey = 'orobi.access-token-expiry'
export const rememberedSessionKey = 'orobi.remembered-session'
export const sessionExpiredEvent = 'orobi:session-expired'
export const passwordChangeRequiredEvent = 'orobi:password-change-required'
const maximumLifetime = 8 * 60 * 60 * 1000
let expiredOnRead = false
type RememberedSession = { token: string; expiresAt: number }

function rememberedSession(): RememberedSession | null {
  try {
    const stored = localStorage.getItem(rememberedSessionKey)
    if (!stored) return null
    const value = JSON.parse(stored) as RememberedSession
    if (typeof value.token === 'string' && value.token && Number.isFinite(value.expiresAt)) return value
  } catch { /* Unavailable or malformed browser storage is not an authenticated session. */ }
  return null
}

export function consumeSessionExpired(): boolean {
  const expired = expiredOnRead
  expiredOnRead = false
  return expired
}

export function readAccessToken(): string {
  const tabToken = sessionStorage.getItem(tokenKey)
  const tabExpiry = Number(sessionStorage.getItem(expiryKey))
  if (tabToken && (!tabExpiry || tabExpiry > Date.now())) return tabToken
  if (tabToken) { clearAccessToken(tabToken); expiredOnRead = true; return '' }
  const stored = rememberedSession()
  if (!stored) return ''
  if (stored.expiresAt <= Date.now()) {
    clearAccessToken(stored.token); expiredOnRead = true; return ''
  }
  sessionStorage.setItem(tokenKey, stored.token)
  sessionStorage.setItem(expiryKey, String(stored.expiresAt))
  return stored.token
}

export function saveAccessToken(token: string, options: { remember?: boolean; expiresAtUtc?: string } = {}): void {
  clearAccessToken()
  expiredOnRead = false
  sessionStorage.setItem(tokenKey, token)
  const serverExpiry = options.expiresAtUtc ? Date.parse(options.expiresAtUtc) : NaN
  const expiresAt = Math.min(serverExpiry, Date.now() + maximumLifetime)
  if (Number.isFinite(expiresAt) && expiresAt > Date.now()) {
    sessionStorage.setItem(expiryKey, String(expiresAt))
    if (options.remember) {
      try { localStorage.setItem(rememberedSessionKey, JSON.stringify({ token, expiresAt })) }
      catch { /* Keep the current tab usable when persistent storage is unavailable. */ }
    }
  }
}

export function clearAccessToken(expectedToken?: string): void {
  sessionStorage.removeItem(tokenKey)
  sessionStorage.removeItem(expiryKey)
  if (!expectedToken || rememberedSession()?.token === expectedToken) {
    try { localStorage.removeItem(rememberedSessionKey) } catch { /* Storage may be disabled. */ }
  }
}

export function expireAccessToken(requestToken: string): void {
  // A response from a previous login must not invalidate the current session.
  const currentToken = sessionStorage.getItem(tokenKey) ?? rememberedSession()?.token
  if (!requestToken || currentToken !== requestToken) return
  clearAccessToken(requestToken)
  window.dispatchEvent(new Event(sessionExpiredEvent))
}

export function requirePasswordChange(requestToken: string): void {
  if (requestToken && readAccessToken() === requestToken) window.dispatchEvent(new Event(passwordChangeRequiredEvent))
}

export function isRememberedSession(token: string): boolean {
  return rememberedSession()?.token === token
}

export function accessTokenExpiresAt(token: string): number | null {
  if (sessionStorage.getItem(tokenKey) !== token) return null
  const expiresAt = Number(sessionStorage.getItem(expiryKey))
  return Number.isFinite(expiresAt) && expiresAt > 0 ? expiresAt : null
}
