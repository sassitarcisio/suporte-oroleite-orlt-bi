import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import type { PayrollClosing } from './features/closings/closingTypes'

const coverageSellers = ['ANDERSON GONCALVES SOUZA', 'MARCELO IVONEI DA ROSA', 'MARCIO FERNANDES', 'MARCIO LUIZ DA ROSA', 'RAMON DO NASCIMENTO', 'RODRIGO']
const payroll: PayrollClosing = {
  year: 2026, month: 8, coverageSeller: 'MARCIO LUIZ DA ROSA', coverageSellers, sellerCount: 9,
  rows: [...coverageSellers, 'SUPERVISOR: DEIVID MANNES', 'TIAGO MARTINS', 'VALDIR ZACARIAS'].map(seller => ({
    seller, sourceSeller: seller === 'TIAGO MARTINS' ? 'MARCIO LUIZ DA ROSA' : seller.replace('SUPERVISOR: ', ''),
    reference: seller === 'TIAGO MARTINS' ? 'Cobertura de férias' : 'Fechamento mensal',
    revenue: 10000, baseSalary: 1951, commissionPercent: 1, commission: 100,
    pppAward: 200, goalAward: 50, tradeAward: 25, incentives: 275, total: 2326,
  })),
  totalBaseSalary: 17559, totalCommission: 900, totalPppAward: 1800, totalGoalAward: 450, totalIncentives: 2475, total: 20934,
}

function json(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), { status: 200 }))
}

function baseFetch(input: RequestInfo | URL): Promise<Response> {
  const url = new URL(String(input), 'https://orobi.test')
  if (url.pathname === '/api/me') return json({ roles: ['Administrador'] })
  if (url.pathname === '/api/sellers') return json(coverageSellers)
  if (url.pathname === '/api/dashboard/filter-options') return json({ brands: [], groups: [], cities: [], movementTypes: [] })
  if (url.pathname === '/api/dashboard/details') return json({ dailyTrend: [], sellerResults: [] })
  if (url.pathname === '/api/closings/payroll/export') return Promise.resolve(new Response('xlsx-content', { status: 200, headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' } }))
  if (url.pathname === '/api/closings/payroll') {
    const [year, month] = url.searchParams.get('month')!.split('-').map(Number)
    const coverageSeller = url.searchParams.get('coverageSeller')!
    return json({ ...payroll, year, month, coverageSeller, rows: payroll.rows.map(row => row.seller === 'TIAGO MARTINS' ? { ...row, sourceSeller: coverageSeller } : row) })
  }
  return json({ grossSales: 0, negativeMovements: 0, negativePercentage: 0, netResult: 0, saleQuantity: 0, movementCount: 0 })
}

describe('App payroll integration', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 8, 5, 12))
    window.history.replaceState({}, '', '/')
    sessionStorage.setItem('orobi.access-token', 'payroll-test-token')
    vi.stubGlobal('fetch', vi.fn(baseFetch))
  })

  afterEach(() => {
    sessionStorage.clear()
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
    vi.useRealTimers()
  })

  it('expires the session if payroll export returns 401', async () => {
    vi.mocked(fetch).mockImplementation(input => String(input).includes('/api/closings/payroll/export?')
      ? Promise.resolve(new Response('{}', { status: 401 })) : baseFetch(input))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento RH' }))
    await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    fireEvent.click(screen.getByRole('button', { name: 'Exportar Excel' }))
    expect(await screen.findByRole('heading', { name: 'Bem-vindo de volta.' })).toBeVisible()
    expect(screen.getByText(/sessão expirou/i)).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBeNull()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('opens RH with the previous month and default coverage and automatically loads nine payroll rows', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento RH' }))
    expect(screen.getByRole('heading', { name: 'Fechamento para folha de pagamento' })).toBeVisible()
    expect(screen.getByLabelText('MES')).toHaveValue('2026-08')
    expect(screen.getByLabelText('Cobertura de férias do Tiago')).toHaveValue('MARCIO LUIZ DA ROSA')
    const table = await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    expect(within(table).getAllByRole('row')).toHaveLength(11)
    expect(screen.getByRole('region', { name: 'Resumo da folha' })).toHaveTextContent('20.934,00')
    const calls = vi.mocked(fetch).mock.calls.filter(([input]) => String(input).includes('/api/closings/payroll?'))
    expect(calls).toHaveLength(1)
    const query = new URL(String(calls[0][0]), 'https://orobi.test').searchParams
    expect(query.get('month')).toBe('2026-08')
    expect(query.get('coverageSeller')).toBe('MARCIO LUIZ DA ROSA')
    expect(new Headers(calls[0][1]?.headers).get('Authorization')).toBe('Bearer payroll-test-token')
  })

  it('queries the selected month and coverage before making that payroll exportable', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento RH' }))
    await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-07' } })
    fireEvent.change(screen.getByLabelText('Cobertura de férias do Tiago'), { target: { value: 'RODRIGO' } })
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar Excel' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))
    const table = await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    expect(within(table).getByRole('row', { name: /TIAGO MARTINS/ })).toHaveTextContent('Cobertura: RODRIGO')
    expect(screen.getByLabelText('MES')).toHaveValue('2026-07')
    expect(screen.getByRole('button', { name: 'Exportar Excel' })).toBeEnabled()
    const calls = vi.mocked(fetch).mock.calls.filter(([input]) => String(input).includes('/api/closings/payroll?'))
    expect(calls).toHaveLength(2)
    const query = new URL(String(calls[1][0]), 'https://orobi.test').searchParams
    expect(query.get('month')).toBe('2026-07')
    expect(query.get('coverageSeller')).toBe('RODRIGO')
  })

  it('downloads an authenticated Excel export for the queried period with an xlsx filename', async () => {
    const createObjectURL = vi.fn((_blob: Blob) => 'blob:payroll-export')
    vi.stubGlobal('URL', class extends URL {
      static createObjectURL = createObjectURL
      static revokeObjectURL = vi.fn()
    })
    let download: { filename: string, href: string } | undefined
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      download = { filename: this.download, href: this.href }
    })
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento RH' }))
    await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-07' } })
    fireEvent.change(screen.getByLabelText('Cobertura de férias do Tiago'), { target: { value: 'RODRIGO' } })
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))
    await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    fireEvent.click(screen.getByRole('button', { name: 'Exportar Excel' }))
    await waitFor(() => expect(download).toEqual({ filename: 'fechamento-rh-2026-07.xlsx', href: 'blob:payroll-export' }))
    const request = vi.mocked(fetch).mock.calls.find(([input]) => String(input).includes('/api/closings/payroll/export?'))!
    const query = new URL(String(request[0]), 'https://orobi.test').searchParams
    expect(query.get('month')).toBe('2026-07')
    expect(query.get('coverageSeller')).toBe('RODRIGO')
    expect(new Headers(request[1]?.headers).get('Authorization')).toBe('Bearer payroll-test-token')
    expect(createObjectURL.mock.calls[0][0]).toMatchObject({ size: 12, type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    expect(screen.getByRole('button', { name: 'Exportar Excel' })).toBeEnabled()
  })

  it('ignores an older payroll response after leaving RH and querying again', async () => {
    let finishFirst: ((response: Response) => void) | undefined
    let count = 0
    vi.mocked(fetch).mockImplementation(input => {
      if (String(input).includes('/api/closings/payroll?')) {
        count += 1
        if (count === 1) return new Promise<Response>(resolve => { finishFirst = resolve })
      }
      return baseFetch(input)
    })
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento RH' }))
    expect(screen.getByRole('status')).toHaveTextContent('Calculando a folha')
    fireEvent.click(screen.getByRole('button', { name: 'Dashboard' }))
    fireEvent.click(screen.getByRole('button', { name: 'Fechamento RH' }))
    await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    await act(async () => { finishFirst!(new Response(JSON.stringify({ ...payroll, total: 99999 }), { status: 200 })) })
    expect(screen.getByRole('region', { name: 'Resumo da folha' })).toHaveTextContent('20.934,00')
    expect(screen.getByRole('region', { name: 'Resumo da folha' })).not.toHaveTextContent('99.999,00')
  })

  it.each(['Importar', 'Sair'])('does not download a delayed export after %s', async destination => {
    let finishExport: ((response: Response) => void) | undefined
    vi.mocked(fetch).mockImplementation(input => {
      if (String(input).includes('/api/closings/payroll/export?')) {
        return new Promise<Response>(resolve => { finishExport = resolve })
      }
      return baseFetch(input)
    })
    const createObjectURL = vi.fn((_blob: Blob) => 'blob:stale-payroll-export')
    vi.stubGlobal('URL', class extends URL {
      static createObjectURL = createObjectURL
      static revokeObjectURL = vi.fn()
    })
    const download = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento RH' }))
    await screen.findByRole('table', { name: 'Fechamento para folha de pagamento' })
    fireEvent.click(screen.getByRole('button', { name: 'Exportar Excel' }))
    expect(screen.getByRole('button', { name: 'Exportando…' })).toBeDisabled()
    expect(finishExport).toBeDefined()
    fireEvent.click(await screen.findByRole('button', { name: destination }))
    await act(async () => {
      finishExport!(new Response('xlsx-content', { status: 200, headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' } }))
    })
    expect(screen.queryByRole('heading', { name: 'Fechamento para folha de pagamento' })).not.toBeInTheDocument()
    expect(createObjectURL).not.toHaveBeenCalled()
    expect(download).not.toHaveBeenCalled()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
