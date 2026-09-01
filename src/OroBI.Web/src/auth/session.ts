const tokenKey = 'orobi.access-token'

export function readAccessToken(): string {
  return sessionStorage.getItem(tokenKey) ?? ''
}

export function saveAccessToken(token: string): void {
  sessionStorage.setItem(tokenKey, token)
}

export function clearAccessToken(): void {
  sessionStorage.removeItem(tokenKey)
}
