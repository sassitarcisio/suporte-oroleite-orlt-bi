const tokenKey = 'orobi.access-token'
const expiryKey = 'orobi.access-token-expiry'
export const rememberedSessionKey = 'orobi.remembered-session'
export const sessionExpiredEvent = 'orobi:session-expired'
export const passwordChangeRequiredEvent = 'orobi:password-change-required'
const maximumLifetime = 8 * 60 * 60 * 1000
export const cookieSessionMode = window.location.origin === (import.meta.env.VITE_COOKIE_PORTAL_ORIGIN ?? 'https://portal-bi.oroleite.com.br').replace(/\/$/, '')
export const cookieSessionChangedKey = 'orobi.cookie-session-changed'
let cookieGeneration = 0
let cookieSession: { handle: string; expiresAt: number | null; authenticated: boolean } | null = null
let expiredOnRead = false
type RememberedSession = { token: string; expiresAt: number }

function purgeLegacyCredentials(): void {
  try { sessionStorage.removeItem(tokenKey); sessionStorage.removeItem(expiryKey) } catch { /* No browser storage is required in cookie mode. */ }
  try { localStorage.removeItem(rememberedSessionKey) } catch { /* No browser storage is required in cookie mode. */ }
}

export function startCookieSession(authenticated = false): string {
  purgeLegacyCredentials()
  expiredOnRead = false
  const handle = `cookie-generation-${++cookieGeneration}`
  cookieSession = { handle, expiresAt: null, authenticated }
  return handle
}

export function bootstrapCookieSession(): string {
  purgeLegacyCredentials()
  return cookieSession?.handle ?? startCookieSession()
}

export function confirmCookieSession(handle: string, expiresAtUtc: string | undefined): void {
  if (cookieSession?.handle !== handle) return
  const serverExpiry = Date.parse(expiresAtUtc ?? '')
  if (!Number.isFinite(serverExpiry) || serverExpiry <= Date.now()) throw new Error('Não foi possível validar o prazo da sessão.')
  cookieSession.expiresAt = Math.min(serverExpiry, cookieSession.expiresAt ?? Date.now() + maximumLifetime)
  cookieSession.authenticated = true
}

export function notifyCookieSessionChanged(): void {
  // This signal contains no identity or credential. Other tabs must consult /me.
  try { localStorage.setItem(cookieSessionChangedKey, crypto.randomUUID()) } catch { /* Focus still revalidates when storage is unavailable. */ }
}

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
  if (cookieSessionMode) return cookieSession?.handle ?? ''
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
  if (cookieSessionMode) { purgeLegacyCredentials(); throw new Error('A sessão corporativa requer autenticação por cookie.') }
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
  if (cookieSessionMode) {
    purgeLegacyCredentials()
    if (!expectedToken || cookieSession?.handle === expectedToken) cookieSession = null
    return
  }
  sessionStorage.removeItem(tokenKey)
  sessionStorage.removeItem(expiryKey)
  if (!expectedToken || rememberedSession()?.token === expectedToken) {
    try { localStorage.removeItem(rememberedSessionKey) } catch { /* Storage may be disabled. */ }
  }
}

export function expireAccessToken(requestToken: string): void {
  if (cookieSessionMode) {
    if (!requestToken || cookieSession?.handle !== requestToken) return
    const silent = !cookieSession.authenticated
    clearAccessToken(requestToken)
    window.dispatchEvent(new CustomEvent(sessionExpiredEvent, { detail: { silent } }))
    return
  }
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
  if (cookieSessionMode) return false
  return rememberedSession()?.token === token
}

export function accessTokenExpiresAt(token: string): number | null {
  if (cookieSessionMode) return cookieSession?.handle === token ? cookieSession.expiresAt : null
  if (sessionStorage.getItem(tokenKey) !== token) return null
  const expiresAt = Number(sessionStorage.getItem(expiryKey))
  return Number.isFinite(expiresAt) && expiresAt > 0 ? expiresAt : null
}
