import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const permissions = { canViewRevenue: true, canViewCommission: true, canViewPrize: true, canViewPPP: true, canViewGoals: true, canViewTrades: true, canViewCustomers: true }
const identity = { id: 'user-a', email: 'ana@example.com', roles: ['Vendedor'], sellerId: 'seller-a', sellerName: 'ANA', permissions }
const revenue = { grossSales: 1250, netRevenue: 1234, negativeMovements: 16, saleQuantity: 10, customerCount: 2, movementCount: 3, documentCount: 2 }
const dashboard = { startDate: '2026-09-01', endDate: '2026-09-30', referenceDate: '2026-09-05', period: revenue, month: revenue, today: revenue, dailyTrend: [], freshness: { source: 'csv', updatedAtUtc: null, timestampKind: 'unavailable' } }
function reply(body: unknown) { return new Response(JSON.stringify(body), { status: 200 }) }

describe('Seller portal', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/portal')
    sessionStorage.setItem('orobi.access-token', 'seller-token')
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/v1/me') || url.endsWith('/api/me')) return reply(identity)
      if (url.includes('/api/v1/me/dashboard')) return reply(dashboard)
      if (url.endsWith('/api/v1/auth/logout')) return new Response(null, { status: 204 })
      return reply({})
    }))
  })
  afterEach(() => { sessionStorage.clear(); window.history.replaceState({}, '', '/'); vi.unstubAllGlobals() })

  it('opens the personal dashboard without requesting administrative data', async () => {
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Meu desempenho' }, { timeout: 5000 })).toBeVisible()
    const paths = vi.mocked(fetch).mock.calls.map(([url]) => String(url))
    expect(paths.some(path => path.includes('/api/dashboard'))).toBe(false)
    expect(screen.queryByText('Margem de Produtos')).not.toBeInTheDocument()
  })

  it('clears personal results and revokes the session on logout', async () => {
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
    expect(await screen.findByRole('button', { name: /Entrar/ })).toBeVisible()
    expect(screen.queryByRole('heading', { name: 'Meu desempenho' })).not.toBeInTheDocument()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
    expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url).endsWith('/api/v1/auth/logout') && init?.method === 'POST')).toBe(true)
  })

  it('shows an offline notice and removes it after reconnection', async () => {
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    act(() => window.dispatchEvent(new Event('offline')))
    expect(screen.getByText(/Você está offline/)).toBeVisible()
    act(() => window.dispatchEvent(new Event('online')))
    expect(screen.queryByText(/Você está offline/)).not.toBeInTheDocument()
  })

  it('routes a seller logging in at the root to the portal', async () => {
    window.history.replaceState({}, '', '/')
    sessionStorage.clear()
    const original = vi.mocked(fetch).getMockImplementation()!
    vi.mocked(fetch).mockImplementation((input, init) => String(input).endsWith('/api/auth/login') ? Promise.resolve(reply({ accessToken: 'seller-token', roles: ['Vendedor'] })) : original(input, init))
    render(<App />)
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'ana@example.com' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'Password123!' } })
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))
    expect(await screen.findByRole('heading', { name: 'Meu desempenho' })).toBeVisible()
    await waitFor(() => expect(window.location.pathname).toBe('/portal'))
    expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/api/dashboard'))).toBe(false)
  })

  it('paginates sales using the server count and applies customer filters', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    vi.mocked(fetch).mockImplementation((input, init) => {
      const url = new URL(String(input), 'http://localhost')
      if (!url.pathname.endsWith('/me/sales')) return original(input, init)
      const second = url.searchParams.get('page') === '2'
      const filtered = url.searchParams.get('customerContains') === 'Padaria'
      return Promise.resolve(reply({ items: [{ id: 'sale', date: '2026-09-01', documentNumber: '123', movementType: 'VENDA', customerCode: 'C1', customerName: filtered ? 'Padaria Central' : second ? 'Cliente página dois' : 'Cliente página um', productName: 'Leite integral', brand: 'OROLEITE', quantity: 5, totalValue: 25 }], page: second ? 2 : 1, pageSize: 20, totalCount: filtered ? 1 : 21 }))
    })
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    fireEvent.click(within(screen.getByRole('navigation', { name: 'Navegação rápida' })).getByRole('button', { name: 'Vendas' }))
    expect(await screen.findByText('Cliente página um')).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Próxima página' }))
    expect(await screen.findByText('Cliente página dois')).toBeVisible()
    fireEvent.change(screen.getByLabelText('Cliente'), { target: { value: 'Padaria' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(await screen.findByText('Padaria Central')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Próxima página' })).toBeDisabled()
  })

  it('does not expose modules disabled by the personal permissions', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    vi.mocked(fetch).mockImplementation((input, init) => String(input).endsWith('/api/v1/me') ? Promise.resolve(reply({ ...identity, permissions: { ...permissions, canViewCommission: false, canViewCustomers: false } })) : original(input, init))
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    fireEvent.click(screen.getByRole('button', { name: 'Mais' }))
    expect(screen.queryByRole('button', { name: 'Comissão' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Clientes' })).not.toBeInTheDocument()
  })

  it('ends the session after changing password without retaining secrets', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    vi.mocked(fetch).mockImplementation((input, init) => String(input).endsWith('/me/change-password') ? Promise.resolve(new Response(null, { status: 204 })) : original(input, init))
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    fireEvent.click(within(screen.getByRole('navigation', { name: 'Navegação rápida' })).getByRole('button', { name: 'Perfil' }))
    fireEvent.change(screen.getByLabelText('Senha atual'), { target: { value: 'OldPassword123!' } })
    fireEvent.change(screen.getByLabelText('Nova senha'), { target: { value: 'NewPassword123!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Alterar senha' }))
    expect(await screen.findByRole('button', { name: /Entrar/ })).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
    expect(screen.queryByDisplayValue('NewPassword123!')).not.toBeInTheDocument()
  })

  it('shows an API denial without stale sales and supports retry', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    let failed = true
    vi.mocked(fetch).mockImplementation((input, init) => String(input).includes('/me/sales') ? Promise.resolve(failed ? new Response(JSON.stringify({ error: 'Acesso não permitido.' }), { status: 403 }) : reply({ items: [], page: 1, pageSize: 20, totalCount: 0 })) : original(input, init))
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    fireEvent.click(within(screen.getByRole('navigation', { name: 'Navegação rápida' })).getByRole('button', { name: 'Vendas' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Acesso não permitido.')
    failed = false
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }))
    expect(await screen.findByText('Nenhuma venda no período selecionado.')).toBeVisible()
  })

  it('lets a manager select only a listed seller before requesting results', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    vi.mocked(fetch).mockImplementation((input, init) => {
      const url = String(input)
      if (url.endsWith('/api/v1/me')) return Promise.resolve(reply({ ...identity, roles: ['Gestor'], sellerId: null, sellerName: null, permissions: null }))
      if (url.endsWith('/management/sellers')) return Promise.resolve(reply([{ sellerId: 'seller-b', name: 'BRUNO', permissions }]))
      if (url.includes('/management/sellers/seller-b/dashboard')) return Promise.resolve(reply(dashboard))
      return original(input, init)
    })
    render(<App />)
    const selector = await screen.findByLabelText('Vendedor vinculado')
    await screen.findByRole('option', { name: 'BRUNO' })
    fireEvent.change(selector, { target: { value: 'seller-b' } })
    expect((await screen.findAllByText(/1.234,00/))[0]).toBeVisible()
    expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/api/v1/me/dashboard'))).toBe(false)
    expect(screen.queryByRole('button', { name: 'Acessos' })).not.toBeInTheDocument()
  })

  it('displays the server ticket and purchased quantity on customer detail', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    const customer = { customerCode: 'C1', customerName: 'Padaria Central', city: 'Goiânia', grossSales: 1200, netRevenue: 1000, documentCount: 2, lastPurchaseDate: '2026-09-04', averageTicket: 500, purchasedQuantity: 73 }
    vi.mocked(fetch).mockImplementation((input, init) => String(input).includes('/me/customers/C1') ? Promise.resolve(reply({ customer, sales: [], totalCount: 0, hasMore: false })) : String(input).includes('/me/customers?') ? Promise.resolve(reply({ observedBuyersOnly: true, items: [customer], totalCount: 1, hasMore: false })) : original(input, init))
    render(<App />)
    await screen.findByRole('heading', { name: 'Meu desempenho' })
    fireEvent.click(screen.getByRole('button', { name: 'Mais' }))
    fireEvent.click(within(screen.getByRole('region', { name: 'Mais módulos' })).getByRole('button', { name: 'Clientes' }))
    fireEvent.click(await screen.findByRole('button', { name: /Padaria Central/ }))
    expect(await screen.findByText('Ticket líquido por documento')).toBeVisible()
    expect(screen.getByText(/500,00/)).toBeVisible()
    expect(screen.getByText('73')).toBeVisible()
  })

  it('composes monthly commission, PPP and goal progress on the home dashboard', async () => {
    const original = vi.mocked(fetch).getMockImplementation()!
    vi.mocked(fetch).mockImplementation((input, init) => {
      const path = new URL(String(input), 'http://localhost').pathname
      if (path.endsWith('/commission')) return Promise.resolve(reply({ commission: 417, totalAwards: 250, isEstimated: true }))
      if (path.endsWith('/ppp')) return Promise.resolve(reply({ available: true, achievementPercent: 84, award: 120, segments: [] }))
      if (path.endsWith('/goals')) return Promise.resolve(reply({ available: true, items: [{ brand: 'OROLEITE', type: 'FATURAMENTO', achievedPercent: 90, currentPrize: 130 }] }))
      return original(input, init)
    })
    render(<App />)
    expect(await screen.findByText('Comissão estimada')).toBeVisible()
    expect(await screen.findByText(/417,00/)).toBeVisible()
    expect(await screen.findByText('84%')).toBeVisible()
    expect(await screen.findByText('90%')).toBeVisible()
  })

  it('allows local signout when the identity request fails', async () => {
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'))
    render(<App />)
    expect(await screen.findByRole('alert')).toHaveTextContent('Failed to fetch')
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
    expect(await screen.findByRole('button', { name: /Entrar/ })).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
  })
})
