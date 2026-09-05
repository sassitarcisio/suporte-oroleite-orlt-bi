import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

function json(body: unknown) { return new Response(JSON.stringify(body), { status: 200 }) }
describe('Portal access administration', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/portal')
    sessionStorage.setItem('orobi.access-token', 'admin-token')
  })
  afterEach(() => { sessionStorage.clear(); window.history.replaceState({}, '', '/'); vi.unstubAllGlobals() })
  it('creates a seller using an explicit imported alias and refreshes the visible directory', async () => {
    const sellers: Array<{ id: string; name: string; importedName: string; isActive: boolean }> = []
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/api/v1/me') || url.endsWith('/api/me')) return json({ userId: 'admin', email: 'admin@example.com', roles: ['Administrador'], sellerId: null, permissions: null })
      if (url.endsWith('/admin/sellers')) {
        if (init?.method === 'POST') sellers.push({ id: 'new-seller', ...JSON.parse(String(init.body)), isActive: true })
        return json(sellers)
      }
      return json([])
    }))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Mais' }, { timeout: 5000 }))
    fireEvent.click(within(screen.getByRole('region', { name: 'Mais módulos' })).getByRole('button', { name: 'Acessos' }))
    fireEvent.change(await screen.findByLabelText('Nome do vendedor'), { target: { value: 'Ana Silva' } })
    fireEvent.change(screen.getByLabelText('Nome no arquivo importado'), { target: { value: 'ANA SILVA' } })
    fireEvent.click(screen.getByRole('button', { name: 'Cadastrar vendedor' }))
    expect(await screen.findByRole('button', { name: 'Editar Ana Silva' })).toBeVisible()
  })
})
