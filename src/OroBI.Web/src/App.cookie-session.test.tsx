// @vitest-environment-options {"url":"https://portal-bi.oroleite.com.br/portal"}
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import App from './App'
import { clearAccessToken, cookieSessionChangedKey, readAccessToken } from './auth/session'
import { apiRequest } from './api/client'

// Keep this transport contract test independent of the release hostname supplied by CI.
vi.hoisted(() => {
  vi.stubEnv('VITE_COOKIE_PORTAL_ORIGIN', 'https://portal-bi.oroleite.com.br')
  vi.stubEnv('VITE_COOKIE_API_BASE_URL', 'https://api-bi.oroleite.com.br')
})

vi.mock('./features/portal/SellerPortal', () => ({ default: ({ onLogout }: { onLogout: () => void }) => <><h1>Portal individual</h1><button onClick={onLogout}>Sair</button></> }))
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
const expiry = () => new Date(Date.now() + 3600000).toISOString()
const identity = () => ({ userId: 'ana', email: 'ana@example.invalid', roles: ['Vendedor'], expiresAtUtc: expiry(), mustChangePassword: false })
const loginResponse = () => ({ sessionMode: 'cookie', roles: ['Vendedor'], expiresAtUtc: expiry(), mustChangePassword: false })
beforeEach(() => { clearAccessToken(); sessionStorage.clear(); localStorage.clear(); window.history.replaceState({}, '', '/portal') })
afterEach(() => { clearAccessToken(); sessionStorage.clear(); localStorage.clear(); vi.useRealTimers(); vi.unstubAllGlobals() })

function enter() {
  fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'ana@example.invalid' } })
  fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'Personal-123!' } })
  fireEvent.click(screen.getByRole('button', { name: /ENTRAR/i }))
}
function expectCookieCalls() {
  for (const [url, init] of vi.mocked(fetch).mock.calls) {
    expect(String(url)).toMatch(/^https:\/\/api-bi\.oroleite\.com\.br\/api\//)
    expect(init?.credentials).toBe('include')
    const headers = new Headers(init?.headers)
    expect(headers.get('X-OroBI-Session')).toBe('cookie')
    expect(headers.has('Authorization')).toBe(false)
  }
}

it('bootstraps from the server before showing data and purges legacy credentials from both storages', async () => {
  sessionStorage.setItem('orobi.access-token', 'legacy-jwt')
  localStorage.setItem('orobi.remembered-session', JSON.stringify({ token: 'legacy-jwt', expiresAt: Date.now() + 3600000 }))
  let resolve!: (response: Response) => void
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(done => { resolve = done })))
  render(<App />)
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Validando seu acesso')
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1))
  expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
  expect(localStorage.getItem('orobi.remembered-session')).toBeNull()
  await act(async () => resolve(json(identity())))
  expect(await screen.findByText('Portal individual')).toBeVisible()
  expectCookieCalls()
  expect(JSON.stringify(localStorage) + JSON.stringify(sessionStorage)).not.toContain(readAccessToken())
})

it('shows ordinary login without an expiry warning and with opt-in cookie persistence when no cookie exists', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json({}, 401)))
  render(<App />)
  expect(await screen.findByRole('button', { name: /ENTRAR/i })).toBeVisible()
  expect(screen.queryByText(/Sua sessão expirou/)).not.toBeInTheDocument()
  expect(screen.getByRole('checkbox', { name: 'Manter-me conectado neste dispositivo' })).not.toBeChecked()
  expectCookieCalls()
})

it.each([false, true])('accepts cookie login with rememberDevice=%s without serializing credentials', async rememberDevice => {
  let signedIn = false
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    if (String(input).endsWith('/auth/login')) { signedIn = true; return json(loginResponse()) }
    return signedIn ? json(identity()) : json({}, 401)
  }))
  render(<App />)
  await screen.findByRole('button', { name: /ENTRAR/i })
  if (rememberDevice) fireEvent.click(screen.getByRole('checkbox'))
  enter()
  expect(await screen.findByText('Portal individual')).toBeVisible()
  const login = vi.mocked(fetch).mock.calls.find(([url]) => String(url).endsWith('/auth/login'))
  expect(JSON.parse(String(login?.[1]?.body)).rememberDevice).toBe(rememberDevice)
  expect(readAccessToken()).toBeTruthy()
  const stored = JSON.stringify(localStorage) + JSON.stringify(sessionStorage)
  expect(stored).not.toContain(readAccessToken())
  expect(stored).not.toContain('Personal-123!')
  expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
  expectCookieCalls()
})

it('reopens a cookie session by validating the server again after the app is remounted', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json(identity())))
  const first = render(<App />)
  await screen.findByText('Portal individual')
  first.unmount()
  clearAccessToken()
  let resolve!: (response: Response) => void
  vi.mocked(fetch).mockImplementation(() => new Promise<Response>(done => { resolve = done }))
  render(<App />)
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2))
  await act(async () => resolve(json(identity())))
  expect(await screen.findByText('Portal individual')).toBeVisible()
})

it('uses the server identity deadline to remove idle results even when login returned a longer deadline', async () => {
  vi.useFakeTimers()
  let signedIn = false
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    if (String(input).endsWith('/auth/login')) { signedIn = true; return json(loginResponse()) }
    return signedIn ? json({ ...identity(), expiresAtUtc: new Date(Date.now() + 1000).toISOString() }) : json({}, 401)
  }))
  await act(async () => { render(<App />) })
  await act(async () => enter())
  expect(screen.getByText('Portal individual')).toBeVisible()
  await act(async () => vi.advanceTimersByTime(1001))
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou')
})

it('treats a cross-tab signal only as a reason to validate the server, never as authentication', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json({}, 401)))
  render(<App />)
  await screen.findByRole('button', { name: /ENTRAR/i })
  let resolve!: (response: Response) => void
  vi.mocked(fetch).mockImplementation(() => new Promise<Response>(done => { resolve = done }))
  act(() => window.dispatchEvent(new StorageEvent('storage', { key: cookieSessionChangedKey, newValue: 'not-an-authentication-credential' })))
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
  expect(screen.getByRole('status')).toHaveTextContent('Validando seu acesso')
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2))
  await act(async () => resolve(json(identity())))
  expect(await screen.findByText('Portal individual')).toBeVisible()
})

it('keeps data hidden if the cookie session metadata has no valid absolute deadline', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json({ ...identity(), expiresAtUtc: undefined })))
  render(<App />)
  expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível validar seu acesso')
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
})

it('keeps public registration out of the corporate login', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json({}, 401)))
  render(<App />)
  await screen.findByRole('button', { name: /ENTRAR/i })
  expect(screen.queryByRole('button', { name: 'Criar minha conta' })).not.toBeInTheDocument()
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/auth/register'))).toBe(false)
})

it('revalidates on foreground before showing results and clears a revoked account', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => json(identity())))
  render(<App />)
  await screen.findByText('Portal individual')
  let resolve!: (response: Response) => void
  vi.mocked(fetch).mockImplementation(() => new Promise<Response>(done => { resolve = done }))
  act(() => window.dispatchEvent(new Event('focus')))
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2))
  await act(async () => resolve(json({}, 401)))
  expect(await screen.findByRole('button', { name: /ENTRAR/i })).toBeVisible()
  expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou')
})

it('changes a temporary cookie password and signs in again using only the cookie transport', async () => {
  let required = true
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    if (url.endsWith('/me/change-password')) { required = false; return new Response(null, { status: 204 }) }
    if (url.endsWith('/auth/login')) return json(loginResponse())
    return json({ ...identity(), mustChangePassword: required })
  }))
  render(<App />)
  await screen.findByRole('heading', { name: 'Crie sua nova senha' })
  fireEvent.change(screen.getByLabelText('Senha atual'), { target: { value: 'Temporary-123!' } })
  for (const label of ['Nova senha', 'Confirmar nova senha']) fireEvent.change(screen.getByLabelText(label), { target: { value: 'Personal-123!' } })
  fireEvent.click(screen.getByRole('button', { name: 'Definir senha' }))
  expect(await screen.findByText('Portal individual')).toBeVisible()
  expectCookieCalls()
  expect(JSON.stringify(localStorage) + JSON.stringify(sessionStorage)).not.toMatch(/Temporary-123|Personal-123/)
})

it('waits for remote logout before another login and honestly reports a failed cookie removal', async () => {
  let reject!: (reason: unknown) => void
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => String(input).endsWith('/auth/logout') ? new Promise<Response>((_, fail) => { reject = fail }) : json(identity())))
  render(<App />)
  await screen.findByText('Portal individual')
  fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
  expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /ENTRAR/i })).not.toBeInTheDocument()
  await act(async () => reject(new TypeError('offline')))
  expect(await screen.findByRole('button', { name: /ENTRAR/i })).toBeVisible()
  expect(screen.getByRole('status')).toHaveTextContent(/sessão.*pode continuar ativa/)
  expect(screen.getByRole('button', { name: 'Tentar sair novamente' })).toBeVisible()
  expectCookieCalls()
})

it('ignores an old 401 after a new cookie login has created a different in-memory generation', async () => {
  let signedIn = true
  let resolveOld!: (response: Response) => void
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    if (url.endsWith('/old-request')) return new Promise<Response>(done => { resolveOld = done })
    if (url.endsWith('/auth/logout')) { signedIn = false; return new Response(null, { status: 204 }) }
    if (url.endsWith('/auth/login')) { signedIn = true; return json(loginResponse()) }
    return signedIn ? json(identity()) : json({}, 401)
  }))
  render(<App />)
  await screen.findByText('Portal individual')
  const oldGeneration = readAccessToken()
  const oldRequest = apiRequest('/api/old-request', oldGeneration).catch(() => undefined)
  fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
  await screen.findByRole('button', { name: /ENTRAR/i })
  enter()
  await screen.findByText('Portal individual')
  const newGeneration = readAccessToken()
  expect(newGeneration).not.toBe(oldGeneration)
  await act(async () => { resolveOld(json({}, 401)); await oldRequest })
  expect(screen.getByText('Portal individual')).toBeVisible()
  expect(readAccessToken()).toBe(newGeneration)
})

it('does not let a response from the temporary session cancel cookie reauthentication already in progress', async () => {
  let required = true
  let resolveOld!: (response: Response) => void
  let resolveLogin!: (response: Response) => void
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    if (url.endsWith('/old-request')) return new Promise<Response>(done => { resolveOld = done })
    if (url.endsWith('/auth/login')) return new Promise<Response>(done => { resolveLogin = done })
    if (url.endsWith('/me/change-password')) { required = false; return new Response(null, { status: 204 }) }
    return json({ ...identity(), mustChangePassword: required })
  }))
  render(<App />)
  await screen.findByRole('heading', { name: 'Crie sua nova senha' })
  const oldRequest = apiRequest('/api/old-request', readAccessToken()).catch(() => undefined)
  fireEvent.change(screen.getByLabelText('Senha atual'), { target: { value: 'Temporary-123!' } })
  for (const label of ['Nova senha', 'Confirmar nova senha']) fireEvent.change(screen.getByLabelText(label), { target: { value: 'Personal-123!' } })
  fireEvent.click(screen.getByRole('button', { name: 'Definir senha' }))
  await waitFor(() => expect(resolveLogin).toBeDefined())
  await act(async () => { resolveOld(json({}, 401)); await oldRequest })
  await act(async () => resolveLogin(json(loginResponse())))
  expect(await screen.findByText('Portal individual')).toBeVisible()
})

it('can validate a corporate cookie when JavaScript storage is unavailable', async () => {
  const read = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new DOMException('Storage blocked', 'SecurityError') })
  vi.stubGlobal('fetch', vi.fn(async () => json(identity())))
  try {
    render(<App />)
    expect(await screen.findByText('Portal individual')).toBeVisible()
  } finally { read.mockRestore() }
})
