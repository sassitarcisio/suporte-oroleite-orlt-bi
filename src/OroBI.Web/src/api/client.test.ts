import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest, authenticatedFetch } from './client'
import { sessionExpiredEvent } from '../auth/session'

afterEach(() => { sessionStorage.clear(); vi.unstubAllGlobals() })

describe('Authenticated requests', () => {
  it('preserves safe validation messages returned by account endpoints', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ errors: ['A senha deve conter um número.', 'E-mail já cadastrado.'] }), { status: 400 })))
    await expect(apiRequest('/api/v1/admin/users', 'current')).rejects.toThrow('A senha deve conter um número. E-mail já cadastrado.')
  })
  it('accepts successful no-content mutations', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 204 })))
    await expect(apiRequest('/api/v1/auth/logout', 'current', { method: 'POST' })).resolves.toBeUndefined()
  })
  it.each(['json', 'download'])('expires the current session once for %s requests', async kind => {
    sessionStorage.setItem('orobi.access-token', 'current')
    const expired = vi.fn()
    window.addEventListener(sessionExpiredEvent, expired)
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401 })))
    try {
      const request = kind === 'json' ? apiRequest : authenticatedFetch
      await expect(request('/api/protected', 'current')).rejects.toThrow(/sessão expirou/)
      await expect(request('/api/protected', 'current')).rejects.toThrow(/sessão expirou/)
      expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
      expect(expired).toHaveBeenCalledTimes(1)
    } finally { window.removeEventListener(sessionExpiredEvent, expired) }
  })

  it('keeps the session for forbidden responses and network failures', async () => {
    sessionStorage.setItem('orobi.access-token', 'current')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 403 })))
    await expect(apiRequest('/api/protected', 'current')).rejects.toThrow('403')
    expect(sessionStorage.getItem('orobi.access-token')).toBe('current')
    vi.mocked(fetch).mockRejectedValueOnce(new TypeError('Failed to fetch'))
    await expect(apiRequest('/api/protected', 'current')).rejects.toThrow('Failed to fetch')
    expect(sessionStorage.getItem('orobi.access-token')).toBe('current')
  })
})
