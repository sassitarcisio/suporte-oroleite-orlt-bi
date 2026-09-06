import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest, authenticatedFetch } from './client'
import { passwordChangeRequiredEvent, sessionExpiredEvent } from '../auth/session'

afterEach(() => { sessionStorage.clear(); vi.unstubAllGlobals() })

describe('Authenticated requests', () => {
  it('requests the first-access gate only for the current session and the specific backend code', async () => {
    sessionStorage.setItem('orobi.access-token', 'current')
    const required = vi.fn()
    window.addEventListener(passwordChangeRequiredEvent, required)
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ code: 'password_change_required', error: 'Crie uma nova senha para continuar.' }), { status: 403 })))
    try {
      await expect(apiRequest('/api/protected', 'old')).rejects.toThrow('Crie uma nova senha')
      expect(required).not.toHaveBeenCalled()
      await expect(apiRequest('/api/protected', 'current')).rejects.toThrow('Crie uma nova senha')
      expect(required).toHaveBeenCalledTimes(1)
      expect(sessionStorage.getItem('orobi.access-token')).toBe('current')
    } finally { window.removeEventListener(passwordChangeRequiredEvent, required) }
  })
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
