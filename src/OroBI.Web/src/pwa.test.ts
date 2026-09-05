// @vitest-environment node
/// <reference types="node" />
import { readFileSync, existsSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import { describe, expect, it, vi } from 'vitest'

describe('Portal offline worker', () => {
  it('leaves APIs and authorization-bearing requests entirely outside the cache handler', () => {
    const workerPath = new URL('../public/service-worker.js', import.meta.url)
    expect(existsSync(workerPath)).toBe(true)
    const listeners: Record<string, (event: unknown) => void> = {}
    runInNewContext(readFileSync(workerPath, 'utf8'), { self: { addEventListener: (name: string, callback: (event: unknown) => void) => { listeners[name] = callback }, location: { origin: 'https://oro.example' } }, URL })
    for (const url of ['https://oro.example/api/v1/me/dashboard', 'https://oro.example/api/me', 'https://elsewhere.example/public']) {
      const respondWith = vi.fn()
      listeners.fetch({ request: new Request(url), respondWith })
      expect(respondWith).not.toHaveBeenCalled()
    }
    const respondWith = vi.fn()
    listeners.fetch({ request: new Request('https://oro.example/portal', { headers: { Authorization: 'Bearer private' } }), respondWith })
    expect(respondWith).not.toHaveBeenCalled()
  })

  it('returns a public offline page when portal navigation cannot reach the server', async () => {
    const workerPath = new URL('../public/service-worker.js', import.meta.url)
    expect(existsSync(workerPath)).toBe(true)
    const listeners: Record<string, (event: unknown) => void> = {}
    const offline = new Response('Você está offline')
    runInNewContext(readFileSync(workerPath, 'utf8'), { self: { addEventListener: (name: string, callback: (event: unknown) => void) => { listeners[name] = callback }, location: { origin: 'https://oro.example' } }, URL, fetch: async () => { throw new TypeError('offline') }, caches: { match: async () => offline } })
    let result: Promise<Response> | undefined
    listeners.fetch({ request: { url: 'https://oro.example/portal', method: 'GET', mode: 'navigate', headers: new Headers() }, respondWith: (value: Promise<Response>) => { result = value } })
    expect(await (await result)?.text()).toBe('Você está offline')
  })
})
