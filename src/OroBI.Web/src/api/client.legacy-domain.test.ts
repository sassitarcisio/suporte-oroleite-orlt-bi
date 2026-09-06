// @vitest-environment-options {"url":"https://bi.oroleite.com.br/"}
import { afterEach, expect, it, vi } from 'vitest'
import { apiRequest } from './client'
import { cookieSessionMode } from '../auth/session'

afterEach(() => vi.unstubAllGlobals())
it('keeps the old BI domain on the existing Bearer transport', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  await apiRequest('/api/me', 'legacy-session')
  expect(cookieSessionMode).toBe(false)
  const [url, init] = vi.mocked(fetch).mock.calls[0]
  expect(String(url)).not.toMatch(/^https:\/\/api-bi\.oroleite\.com\.br\//)
  expect(init?.credentials).not.toBe('include')
  const headers = new Headers(init?.headers)
  expect(headers.get('Authorization')).toBe('Bearer legacy-session')
  expect(headers.has('X-OroBI-Session')).toBe(false)
})
