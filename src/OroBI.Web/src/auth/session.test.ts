import { afterEach, describe, expect, it, vi } from 'vitest'
import { clearAccessToken, expireAccessToken, readAccessToken, saveAccessToken, sessionExpiredEvent } from './session'

afterEach(() => { sessionStorage.clear(); localStorage.clear(); vi.useRealTimers() })
describe('Mobile session lifetime', () => {
  it('remembers an opted-in session after the PWA tab closes, only until its server expiry', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-09-05T12:00:00Z'))
    saveAccessToken('personal-token', { remember: true, expiresAtUtc: '2026-09-05T13:00:00Z' })
    sessionStorage.clear()
    expect(readAccessToken()).toBe('personal-token')
    vi.setSystemTime(new Date('2026-09-05T13:00:01Z'))
    expect(readAccessToken()).toBe('')
    expect(localStorage.getItem('orobi.remembered-session')).toBeNull()
  })
  it('caps remembered sessions at eight hours even if a response supplies a longer date', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-09-05T12:00:00Z'))
    saveAccessToken('personal-token', { remember: true, expiresAtUtc: '2026-09-30T12:00:00Z' })
    sessionStorage.clear(); expect(readAccessToken()).toBe('personal-token')
    vi.setSystemTime(new Date('2026-09-05T20:00:01Z'))
    expect(readAccessToken()).toBe('')
  })
  it('does not persist sessions without opt-in or a valid future expiry', () => {
    saveAccessToken('tab-only')
    expect(readAccessToken()).toBe('tab-only')
    sessionStorage.clear(); expect(readAccessToken()).toBe('')
    saveAccessToken('invalid-expiry', { remember: true, expiresAtUtc: 'invalid' })
    sessionStorage.clear(); expect(readAccessToken()).toBe('')
  })
  it('clears remembered access on logout and tolerates malformed stored data', () => {
    saveAccessToken('remembered', { remember: true, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() })
    clearAccessToken(); sessionStorage.clear(); expect(readAccessToken()).toBe('')
    localStorage.setItem('orobi.remembered-session', '{invalid')
    expect(readAccessToken()).toBe('')
  })
  it('does not erase a different account saved by another tab when clearing the old tab', () => {
    sessionStorage.setItem('orobi.access-token', 'old-account')
    localStorage.setItem('orobi.remembered-session', JSON.stringify({ token: 'new-account', expiresAt: Date.now() + 3600000 }))
    clearAccessToken('old-account')
    expect(readAccessToken()).toBe('new-account')
  })
  it('expires persistent access once and ignores an old request after a newer login', () => {
    const expired = vi.fn(); window.addEventListener(sessionExpiredEvent, expired)
    try {
      saveAccessToken('new', { remember: true, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() })
      expireAccessToken('old'); expect(readAccessToken()).toBe('new')
      expireAccessToken('new'); expireAccessToken('new')
      expect(expired).toHaveBeenCalledTimes(1)
      sessionStorage.clear(); expect(readAccessToken()).toBe('')
    } finally { window.removeEventListener(sessionExpiredEvent, expired) }
  })
  it('announces a server rejection even when the current token has just expired by the clock', () => {
    vi.useFakeTimers(); vi.setSystemTime(new Date('2026-09-05T12:00:00Z'))
    const expired = vi.fn(); window.addEventListener(sessionExpiredEvent, expired)
    try {
      saveAccessToken('expired', { remember: true, expiresAtUtc: '2026-09-05T12:00:01Z' })
      vi.setSystemTime(new Date('2026-09-05T12:00:02Z'))
      expireAccessToken('expired')
      expect(expired).toHaveBeenCalledTimes(1)
      expect(readAccessToken()).toBe('')
    } finally { window.removeEventListener(sessionExpiredEvent, expired) }
  })
})
