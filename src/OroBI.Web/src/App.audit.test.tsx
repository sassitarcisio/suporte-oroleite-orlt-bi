import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const summary = { grossSales: 1000, negativeMovements: 100, negativePercent: 10, netResult: 900, saleQuantity: 10, movementCount: 10, customerCount: 2, documentCount: 3 }
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
function response(input: RequestInfo | URL) {
  const path = new URL(String(input), 'https://orobi.test').pathname
  if (path === '/api/me') return json({ roles: ['Administrador'] })
  if (path === '/api/sellers') return json(['ANA'])
  if (path === '/api/dashboard/filter-options') return json({ brands: ['NESTLE'], groups: [], cities: [], movementTypes: [] })
  if (path === '/api/dashboard/details') return json({ dailyTrend: [], sellerResults: [], groups: {} })
  if (path === '/api/auth/login') return json({ accessToken: 'new-token' })
  if (path === '/api/trade-analysis') return json({ filteredMovementCount: 10, grossSales: 1000, netRevenue: 900, totalTradeValue: 100, tradeToRevenuePercent: 11, tradeDevValue: 100, tradeValue: 0, tradeQuantity: 1, tradeMovementCount: 1, customerCount: 1, productCount: 1, brandCount: 1, dailyTrend: [], sellerRanking: [], customerRanking: [], productRanking: [], brandRanking: [] })
  return json(summary)
}

describe('Audit regressions: applied filters and session lifecycle', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/')
    sessionStorage.setItem('orobi.access-token', 'old-token')
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => response(input)))
  })
  afterEach(() => { sessionStorage.clear(); vi.unstubAllGlobals() })

  it('does not label a draft as applied or send it to trades until submission', async () => {
    render(<App />)
    await screen.findByTestId('dashboard-metrics')
    expect(screen.getByText('2 filtros aplicados')).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }))
    await screen.findByRole('option', { name: 'NESTLE' })
    fireEvent.change(screen.getByLabelText('MARCA'), { target: { value: 'NESTLE' } })
    expect(screen.getByText('2 filtros aplicados')).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Analise Venda x Troca' }))
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/api/trade-analysis'))).toBe(true))
    expect(vi.mocked(fetch).mock.calls.filter(([url]) => String(url).includes('/api/trade-analysis')).every(([url]) => !String(url).includes('brand='))).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Dashboard' }))
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }))
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await screen.findByText('3 filtros aplicados')
    fireEvent.click(screen.getByRole('button', { name: 'Analise Venda x Troca' }))
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/api/trade-analysis') && String(url).includes('brand=NESTLE'))).toBe(true))
  })

  it('returns to login on a current 401 and clears the expired session', async () => {
    vi.mocked(fetch).mockImplementation(async input => String(input).includes('/api/dashboard?') ? json({}, 401) : response(input))
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Bem-vindo de volta.' })).toBeVisible()
    expect(screen.getByText(/sessão expirou/i)).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
    expect(screen.queryByTestId('dashboard-metrics')).not.toBeInTheDocument()
  })

  it('returns to login when a CSV submission receives 401', async () => {
    vi.mocked(fetch).mockImplementation(async input => String(input).endsWith('/api/imports') ? json({}, 401) : response(input))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Importar' }))
    fireEvent.change(screen.getByLabelText('ARQUIVO CSV'), { target: { files: [new File(['test'], 'power.csv', { type: 'text/csv' })] } })
    fireEvent.submit(screen.getByRole('button', { name: 'Enviar CSV' }).closest('form')!)
    expect(await screen.findByRole('heading', { name: 'Bem-vindo de volta.' })).toBeVisible()
    expect(screen.getByText(/sessão expirou/i)).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
  })

  it('does not navigate or reload when an old import completes after a new login', async () => {
    let finishImport: (value: Response) => void = () => {}
    vi.mocked(fetch).mockImplementation(input => String(input).endsWith('/api/imports') ? new Promise(resolve => { finishImport = resolve }) : Promise.resolve(response(input)))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Importar' }))
    fireEvent.change(screen.getByLabelText('ARQUIVO CSV'), { target: { files: [new File(['test'], 'power.csv', { type: 'text/csv' })] } })
    fireEvent.submit(screen.getByRole('button', { name: 'Enviar CSV' }).closest('form')!)
    fireEvent.click(screen.getByRole('button', { name: 'Voltar' }))
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'novo@example.test' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'not-a-real-password' } })
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))
    await screen.findByTestId('dashboard-metrics')
    fireEvent.click(screen.getByRole('button', { name: 'Analise Venda x Troca' }))
    await screen.findByRole('heading', { name: 'Analise venda x troca' })
    const requestCount = vi.mocked(fetch).mock.calls.length
    await act(async () => { finishImport(json({}, 201)) })
    expect(screen.getByRole('heading', { name: 'Analise venda x troca' })).toBeVisible()
    expect(vi.mocked(fetch).mock.calls).toHaveLength(requestCount)
  })

  it.each([200, 401])('ignores a late %s dashboard response after a new login', async status => {
    let finishOld: (value: Response) => void = () => {}
    vi.mocked(fetch).mockImplementation((input, init) => {
      if (String(input).includes('/api/dashboard?') && new Headers(init?.headers).get('Authorization') === 'Bearer old-token') return new Promise(resolve => { finishOld = resolve })
      return Promise.resolve(response(input))
    })
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Sair' }))
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'novo@example.test' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'not-a-real-password' } })
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))
    await screen.findByTestId('dashboard-metrics')
    await act(async () => { finishOld(json({ ...summary, grossSales: 99999 }, status)) })
    expect(sessionStorage.getItem('orobi.access-token')).toBe('new-token')
    expect(screen.getByTestId('dashboard-metrics')).toBeVisible()
    expect(screen.getByTestId('dashboard-metrics')).not.toHaveTextContent('99.999,00')
  })

  it('ignores old role and catalog responses after logout and a new login', async () => {
    let finishOldRoles: (value: Response) => void = () => {}
    vi.mocked(fetch).mockImplementation((input, init) => {
      const path = new URL(String(input), 'https://orobi.test').pathname
      const old = new Headers(init?.headers).get('Authorization') === 'Bearer old-token'
      if (path === '/api/me' && old) return new Promise(resolve => { finishOldRoles = resolve })
      if (path === '/api/me') return Promise.resolve(json({ roles: ['Gestor'] }))
      return Promise.resolve(response(input))
    })
    render(<App />)
    await screen.findByTestId('dashboard-metrics')
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }))
    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'gestor@example.test' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'not-a-real-password' } })
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))
    await screen.findByTestId('dashboard-metrics')
    await act(async () => { finishOldRoles(json({ roles: ['Administrador'] })) })
    expect(screen.queryByRole('button', { name: 'Importar' })).not.toBeInTheDocument()
  })
})
