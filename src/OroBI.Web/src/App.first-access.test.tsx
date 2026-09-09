import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { saveAccessToken } from './auth/session'

vi.mock('./features/portal/SellerPortal', () => ({ default: () => <h1>Portal individual</h1> }))
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
beforeEach(() => { sessionStorage.clear(); localStorage.clear(); window.history.replaceState({}, '', '/portal') })
afterEach(() => { sessionStorage.clear(); localStorage.clear(); vi.useRealTimers(); vi.unstubAllGlobals(); window.history.replaceState({}, '', '/') })

describe('Required first access', () => {
  it.each(['/portal', '/'])('checks the persisted requirement before opening any data at %s', async path => {
    window.history.replaceState({}, '', path)
    saveAccessToken('temporary-token')
    vi.stubGlobal('fetch', vi.fn(async () => json({ email: 'ana@example.invalid', roles: path === '/' ? ['Administrador'] : ['Vendedor'], mustChangePassword: true })))
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Crie sua nova senha' })).toBeVisible()
    expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
    expect(vi.mocked(fetch).mock.calls.every(([url]) => /\/api(?:\/v1)?\/me$/.test(String(url)))).toBe(true)
  })
  it('changes the temporary password and signs in to the personal portal with the new password', async () => {
    let required = true
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/auth/login')) {
        const body = JSON.parse(String(init?.body))
        return json({ accessToken: body.password === 'Personal-123!' ? 'personal-token' : 'temporary-token', roles: ['Vendedor'], mustChangePassword: required, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() })
      }
      if (url.endsWith('/me/change-password')) { required = false; return new Response(null, { status: 204 }) }
      return json({ email: 'ana@example.invalid', roles: ['Vendedor'], mustChangePassword: required })
    }))
    render(<App />)
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'ana@example.invalid' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'Temporary-123!' } })
    fireEvent.click(screen.getByRole('button', { name: /ENTRAR/i }))
    await screen.findByRole('heading', { name: 'Crie sua nova senha' })
    fireEvent.change(screen.getByLabelText('Senha atual'), { target: { value: 'Temporary-123!' } })
    for (const label of ['Nova senha', 'Confirmar nova senha']) fireEvent.change(screen.getByLabelText(label), { target: { value: 'Personal-123!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Definir senha' }))
    expect(await screen.findByRole('heading', { name: 'Portal individual' })).toBeVisible()
    const logins = vi.mocked(fetch).mock.calls.filter(([url]) => String(url).endsWith('/auth/login'))
    expect(logins).toHaveLength(2)
    expect(JSON.parse(String(logins[1][1]?.body)).password).toBe('Personal-123!')
    expect(sessionStorage.getItem('orobi.access-token')).toBe('personal-token')
    expect(JSON.stringify(localStorage)).not.toContain('Personal-123!')
  })
  it('offers administrative password recovery and announces invalid login without identifying an account', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json({}, 401)))
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Esqueci minha senha' }))
    expect(screen.getByRole('dialog')).toHaveTextContent(/administrador/)
    fireEvent.click(screen.getByRole('button', { name: 'Entendi' }))
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'missing@example.invalid' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'Invalid-123!' } })
    fireEvent.click(screen.getByRole('button', { name: /ENTRAR/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Usuário ou senha incorretos.')
  })
  it('reopens an opted-in session and waits for server identity before rendering the portal', async () => {
    saveAccessToken('remembered', { remember: true, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() })
    sessionStorage.clear()
    let resolve!: (value: Response) => void
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(done => { resolve = done })))
    render(<App />)
    expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
    await waitFor(() => expect(fetch).toHaveBeenCalled())
    resolve(json({ roles: ['Vendedor'], mustChangePassword: false }))
    expect(await screen.findByText('Portal individual')).toBeVisible()
  })
  it('clears displayed results when the session expires while the portal is idle', async () => {
    vi.useFakeTimers()
    saveAccessToken('short-session', { remember: true, expiresAtUtc: new Date(Date.now() + 1000).toISOString() })
    vi.stubGlobal('fetch', vi.fn(async () => json({ roles: ['Vendedor'], mustChangePassword: false })))
    await act(async () => { render(<App />) })
    expect(screen.getByText('Portal individual')).toBeVisible()
    await act(async () => { vi.advanceTimersByTime(1100) })
    expect(screen.queryByText('Portal individual')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Sua sessão expirou.')
    expect(localStorage.getItem('orobi.remembered-session')).toBeNull()
  })
  it('shows a Portuguese connection message if login cannot reach the API', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch') }))
    render(<App />)
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'ana@example.invalid' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'Personal-123!' } })
    fireEvent.click(screen.getByRole('button', { name: /ENTRAR/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Verifique sua conexão')
  })
})
