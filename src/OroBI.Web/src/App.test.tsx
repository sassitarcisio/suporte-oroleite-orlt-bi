import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App dashboard', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/')
    sessionStorage.setItem('orobi.access-token', 'test-token')
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      const body = url.endsWith('/api/me')
        ? { roles: ['Administrador'] }
        : url.endsWith('/api/auth/login')
          ? { accessToken: 'new-access-token' }
        : url.startsWith('/api/closings')
          ? { ppp: { meanPercent: 75, award: 300 }, revenueAward: 250, positivityAward: 100, tradeAward: 100, compensation: { commission: 120, salary: 2120 }, totalAwards: 750 }
          : url.endsWith('/api/sales-trades')
            ? { revenue: 1500, trades: 150, tradeToRevenuePercent: 10 }
          : { grossSales: 0, negativeMovements: 0, negativePercentage: 0, netResult: 0, saleQuantity: 0, movementCount: 0 }

      return Promise.resolve(new Response(JSON.stringify(body), { status: 200 }))
    }))
  })

  afterEach(() => {
    sessionStorage.clear()
    vi.unstubAllGlobals()
  })

  it('shows an empty state when the dashboard has no movements', async () => {
    render(<App />)

    expect(await screen.findByText('Nenhum movimento encontrado para os filtros aplicados.')).toBeVisible()
  })

  it('opens the trades screen from the application navigation', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Trocas' }))

    expect(await screen.findByRole('heading', { name: 'Visao de trocas' })).toBeVisible()
  })

  it('persists the dashboard seller filter in the URL', async () => {
    render(<App />)

    fireEvent.change(await screen.findByPlaceholderText('Todos os vendedores'), { target: { value: 'ANA' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar recorte' }))

    await waitFor(() => expect(window.location.search).toBe('?seller=ANA'))
  })

  it('shows the closing award for the selected seller and month', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento' }))
    fireEvent.change(screen.getByLabelText('VENDEDOR'), { target: { value: 'ANA' } })
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-08' } })
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))

    expect(await screen.findByText(/750,00/)).toBeVisible()
  })

  it('loads the sales versus trades analysis from its dedicated endpoint', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Venda x troca' }))

    expect(await screen.findByRole('heading', { name: 'Venda x troca' })).toBeVisible()
    expect(await screen.findByText('R$ 1.500,00')).toBeVisible()
    const salesTradesRequest = vi.mocked(fetch).mock.calls.find(([url]) => url === '/api/sales-trades')
    expect(salesTradesRequest).toBeDefined()
    expect(new Headers(salesTradesRequest?.[1]?.headers).get('Authorization')).toBe('Bearer test-token')
  })

  it('uploads a CSV from the administrator import screen', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Importar' }))
    fireEvent.change(screen.getByLabelText('ARQUIVO CSV'), {
      target: { files: [new File(['seller;value'], 'power.csv', { type: 'text/csv' })] },
    })
    fireEvent.submit(screen.getByRole('button', { name: 'Enviar CSV' }).closest('form')!)

    await waitFor(() => expect(screen.queryByText('IMPORTACOES AUDITADAS')).not.toBeInTheDocument())
    const importRequest = vi.mocked(fetch).mock.calls.find(([url]) => url === '/api/imports')
    expect(importRequest).toBeDefined()
    expect(importRequest?.[1]).toMatchObject({ method: 'POST' })
    expect(importRequest?.[1]?.body).toBeInstanceOf(FormData)
  })

  it('persists a valid login session and opens the dashboard', async () => {
    sessionStorage.clear()
    render(<App />)

    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'admin@oroleite.com' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'senha-segura' } })
    fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('CENTRAL DE RESULTADOS')).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBe('new-access-token')
  })
})
