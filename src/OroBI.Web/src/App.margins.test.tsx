import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const gross = { revenue: 1000, cost: 600, grossProfit: 400, marginPercent: 40, customerCount: 1, productCount: 1, movementCount: 1, groups: { customer: [], product: [], brand: [] } }
const net = { grossSales: 1000, returns: 100, netSales: 900, netCost: 500, tradeLosses: 50, boletoDiscounts: 20, liquidProfit: 330, liquidMarginPercent: 36.6667, productCount: 1, movementCount: 1, groups: { seller: [], brand: [], customer: [], group: [], product: [], city: [] } }
const json = (body: unknown) => Promise.resolve(new Response(JSON.stringify(body), { status: 200 }))
function respond(input: RequestInfo | URL) {
  const path = new URL(String(input), 'https://orobi.test').pathname
  if (path === '/api/me') return json({ roles: ['Administrador'] })
  if (path === '/api/sellers') return json(['ANA'])
  if (path === '/api/dashboard/filter-options') return json({ brands: ['NESTLE'], groups: ['GIASSI'], cities: ['BLUMENAU'], movementTypes: [] })
  if (path === '/api/dashboard/details') return json({ dailyTrend: [], sellerResults: [] })
  if (path === '/api/margins/details') return json(gross)
  if (path === '/api/net-margin/details') return json(net)
  return json({ grossSales: 0, negativeMovements: 0, negativePercentage: 0, netResult: 0, saleQuantity: 0, movementCount: 0 })
}
describe('Margin navigation and requests', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 5, 12))
    sessionStorage.setItem('orobi.access-token', 'margin-token')
    vi.stubGlobal('fetch', vi.fn(respond))
  })
  afterEach(() => { sessionStorage.clear(); vi.unstubAllGlobals(); vi.useRealTimers() })

  it.each([['Margem de Produtos', '/api/margins/details'], ['Margem Liquida', '/api/net-margin/details']])('loads %s for the previous calendar month', async (label, endpoint) => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: label }))
    await waitFor(() => {
      const call = vi.mocked(fetch).mock.calls.find(([url]) => String(url).includes(endpoint))
      expect(call).toBeDefined()
      const query = new URL(String(call![0]), 'https://orobi.test').searchParams
      expect(query.get('startDate')).toBe('2026-08-01')
      expect(query.get('endDate')).toBe('2026-08-31')
    })
  })

  it('ignores the response of the previous margin page', async () => {
    let finish: ((value: Response) => void) | undefined
    vi.mocked(fetch).mockImplementation(input => {
      if (new URL(String(input), 'https://orobi.test').pathname === '/api/margins/details') return new Promise(resolve => { finish = resolve })
      return respond(input)
    })
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Margem de Produtos' }))
    fireEvent.click(screen.getByRole('button', { name: 'Margem Liquida' }))
    await screen.findByText(/330,00/)
    await act(async () => { finish!(new Response(JSON.stringify({ ...gross, grossProfit: 99999 }), { status: 200 })) })
    expect(screen.queryByText(/99.999,00/)).not.toBeInTheDocument()
    expect(screen.getAllByText(/330,00/).length).toBeGreaterThan(0)
  })
  it('applies all commercial filters to the detailed endpoint', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Margem de Produtos' }))
    await screen.findByTestId('margin-metrics')
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '2026-07-01' } })
    fireEvent.change(screen.getByLabelText('Data final'), { target: { value: '2026-07-31' } })
    fireEvent.change(screen.getByLabelText('Vendedor'), { target: { value: 'ANA' } })
    fireEvent.change(screen.getByLabelText('Marca'), { target: { value: 'NESTLE' } })
    fireEvent.change(screen.getByLabelText('Grupo / rede'), { target: { value: 'GIASSI' } })
    fireEvent.change(screen.getByLabelText('Cidade'), { target: { value: 'BLUMENAU' } })
    fireEvent.change(screen.getByLabelText('Cliente contém'), { target: { value: 'Mercado' } })
    fireEvent.change(screen.getByLabelText('Produto contém'), { target: { value: 'Leite' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => {
      const calls = vi.mocked(fetch).mock.calls.filter(([url]) => String(url).includes('/api/margins/details'))
      expect(calls).toHaveLength(2)
      const query = new URL(String(calls[1][0]), 'https://orobi.test').searchParams
      expect(Object.fromEntries(query)).toEqual({ startDate: '2026-07-01', endDate: '2026-07-31', seller: 'ANA', brand: 'NESTLE', group: 'GIASSI', city: 'BLUMENAU', customerContains: 'Mercado', productContains: 'Leite' })
    })
  })
})
