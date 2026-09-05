import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

function fillRegistration(confirmation = 'ExamplePassword123!') {
  fireEvent.change(screen.getByLabelText('Nome completo'), { target: { value: 'Ana Silva' } })
  fireEvent.change(screen.getByLabelText('E-mail para cadastro'), { target: { value: 'ana@example.test' } })
  fireEvent.change(screen.getByLabelText('Crie sua senha'), { target: { value: 'ExamplePassword123!' } })
  fireEvent.change(screen.getByLabelText('Confirme sua senha'), { target: { value: confirmation } })
}

describe('Seller self registration', () => {
  beforeEach(() => { sessionStorage.clear(); localStorage.clear(); window.history.replaceState({}, '', '/'); vi.stubGlobal('fetch', vi.fn()) })
  afterEach(() => { sessionStorage.clear(); localStorage.clear(); vi.unstubAllGlobals() })

  it('submits a public registration and returns to login pending approval without creating a session', async () => {
    let payload: unknown
    let authorization: string | null = null
    vi.mocked(fetch).mockImplementation(async (input, init) => {
      if (String(input).endsWith('/api/v1/auth/register')) {
        payload = JSON.parse(String(init?.body)); authorization = new Headers(init?.headers).get('Authorization')
        return new Response(JSON.stringify({ message: 'Solicitação recebida. Aguarde a aprovação do administrador.' }), { status: 202 })
      }
      return new Response('{}', { status: 404 })
    })
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Criar minha conta' }))
    fillRegistration()
    fireEvent.click(screen.getByRole('button', { name: 'Solicitar cadastro' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Aguarde a aprovação do administrador.')
    expect(screen.getByRole('button', { name: /Entrar/ })).toBeVisible()
    expect(payload).toEqual({ name: 'Ana Silva', email: 'ana@example.test', password: 'ExamplePassword123!' })
    expect(authorization).toBeNull()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
    expect(JSON.stringify({ ...sessionStorage, ...localStorage })).not.toContain('ExamplePassword123!')
    expect(screen.queryByDisplayValue('ExamplePassword123!')).not.toBeInTheDocument()
  })

  it('rejects mismatched password confirmation before contacting the API', () => {
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Criar minha conta' }))
    fillRegistration('DifferentPassword123!')
    fireEvent.click(screen.getByRole('button', { name: 'Solicitar cadastro' }))
    expect(screen.getByRole('alert')).toHaveTextContent('As senhas não coincidem.')
    expect(fetch).not.toHaveBeenCalled()
  })

  it('shows server validation inline and keeps the registration form available', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({ errors: ['A senha deve ter pelo menos 12 caracteres.'] }), { status: 400 }))
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Criar minha conta' }))
    fillRegistration()
    fireEvent.click(screen.getByRole('button', { name: 'Solicitar cadastro' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('A senha deve ter pelo menos 12 caracteres.')
    expect(screen.getByRole('button', { name: 'Solicitar cadastro' })).toBeEnabled()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
  })

  it('ignores a registration response after returning to login', async () => {
    let finish: (response: Response) => void = () => {}
    vi.mocked(fetch).mockImplementation(() => new Promise(resolve => { finish = resolve }))
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Criar minha conta' }))
    fillRegistration()
    fireEvent.click(screen.getByRole('button', { name: 'Solicitar cadastro' }))
    fireEvent.click(screen.getByRole('button', { name: 'Voltar ao login' }))
    await act(async () => { finish(new Response(JSON.stringify({ message: 'Solicitação antiga recebida.' }), { status: 202 })) })
    expect(screen.getByRole('button', { name: /Entrar/ })).toBeVisible()
    expect(screen.queryByText('Solicitação antiga recebida.')).not.toBeInTheDocument()
  })
})
