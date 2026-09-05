import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { TradeAnalysisPage } from './features/analytics/TradeAnalysisPage'

describe('App dashboard', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/')
    sessionStorage.setItem('orobi.access-token', 'test-token')
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      const body = url.endsWith('/api/me')
        ? { roles: ['Administrador'] }
        : url.endsWith('/api/sellers')
          ? ['ANA', 'BRUNO']
        : url.endsWith('/api/dashboard/filter-options')
          ? { brands: ['OROLEITE'], groups: ['LATICINIOS'], cities: ['GOIANIA'], movementTypes: ['VENDA', 'TROCA'] }
          : url.includes('/api/dashboard/details')
            ? { dailyTrend: [{ date: '2026-08-01', grossSales: 100, netResult: 80, negativeMovements: 20 }], sellerResults: [{ seller: 'ANA', netResult: 80 }] }
        : url.endsWith('/api/auth/login')
          ? { accessToken: 'new-access-token' }
        : url.includes('/api/closings')
          ? { ppp: { meanPercent: 75, award: 300 }, revenueAward: 250, positivityAward: 100, tradeAward: 100, compensation: { baseSalary: 2000, commission: 120, totalSalary: 2120 }, totalAwards: 750, total: 2870, monthly: { scope: 'seller', revenue: 12000, commissionableRevenue: 12000, tradeValue: 0, tradePercent: 0, documentCount: 0, movementCount: 0, customerCount: 0, documents: [] }, pppSegments: [], brandAwards: [{ brand: 'NESTLE', revenueGoal: 1000, revenueActual: 1000, revenueAchievedPercent: 100, revenuePrize: 100, positivityGoal: 10, positivityActual: 10, positivityAchievedPercent: 100, positivityPrize: 100, tradeValue: 0, tradeActualPercent: 0, tradeGoalPercent: 2, tradePrize: 25, positivityAward: 100, revenueAward: 100, tradeAward: 25, totalAward: 225 }] }
          : url.includes('/api/trade-analysis')
            ? { filteredMovementCount: 100, grossSales: 1500, netRevenue: 1350, totalTradeValue: 150, tradeToRevenuePercent: 11.11, tradeDevValue: 120, tradeValue: 30, tradeQuantity: 25, tradeMovementCount: 10, customerCount: 3, productCount: 4, brandCount: 2, dailyTrend: [{ date: '2026-08-01', value: 150 }], sellerRanking: [{ name: 'ANA', value: 150 }], customerRanking: [{ name: 'CLIENTE A', value: 150 }], productRanking: [{ name: 'PRODUTO A', value: 150 }], brandRanking: [{ name: 'MARCA A', value: 150 }] }
          : url.includes('/api/sales-trades')
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

  it('renders dashboard charts below the operational filters when data exists', async () => {
    vi.mocked(fetch).mockImplementation((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/me')) return Promise.resolve(new Response(JSON.stringify({ roles: ['Administrador'] }), { status: 200 }))
      if (url.endsWith('/api/sellers')) return Promise.resolve(new Response(JSON.stringify(['ANA']), { status: 200 }))
      if (url.endsWith('/api/dashboard/filter-options')) return Promise.resolve(new Response(JSON.stringify({ brands: [], groups: [], cities: [], movementTypes: [] }), { status: 200 }))
      if (url.includes('/api/dashboard/details')) return Promise.resolve(new Response(JSON.stringify({ dailyTrend: [{ date: '2026-08-01', grossSales: 100, netResult: 80, negativeMovements: 20 }], sellerResults: [{ seller: 'ANA', netResult: 80 }] }), { status: 200 }))
      return Promise.resolve(new Response(JSON.stringify({ grossSales: 100, negativeMovements: 20, negativePercentage: 20, netResult: 80, saleQuantity: 1, movementCount: 1 }), { status: 200 }))
    })
    render(<App />)

    expect(await screen.findByTestId('dashboard-charts')).toBeVisible()
    expect(document.querySelector('.dashboard-workspace')).toBeInTheDocument()
  })

  it('opens the trades screen from the application navigation', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Visao de Trocas' }))

    expect(await screen.findByRole('heading', { name: 'Visao de trocas' })).toBeVisible()
    expect(document.querySelector('.trade-analysis-layout')).toBeInTheDocument()
    expect(document.querySelector('.trade-kpis')).toBeInTheDocument()
    expect(document.querySelector('.analysis-workspace')).toBeInTheDocument()
  })

  it('opens the responsive navigation menu', async () => {
    render(<App />)

    expect(document.querySelector('.executive-layout')).toBeInTheDocument()

    const toggle = await screen.findByRole('button', { name: 'Alternar navegacao' })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')

    fireEvent.click(toggle)

    expect(toggle).toHaveAttribute('aria-expanded', 'true')
  })

  it('persists the dashboard seller filter in the URL', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Filtros' }))
    fireEvent.change(await screen.findByLabelText('VENDEDOR'), { target: { value: 'ANA' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    await waitFor(() => expect(window.location.search).toBe('?seller=ANA'))
    expect(document.querySelector('.dashboard-layout')).toBeInTheDocument()
  })

  it('defaults dashboard dates to the previous calendar month', () => {
    const now = new Date()
    const previousMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1)
    const firstDay = previousMonth.toISOString().slice(0, 10)
    const lastDay = new Date(previousMonth.getFullYear(), previousMonth.getMonth() + 1, 0).toISOString().slice(0, 10)

    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }))

    expect(screen.getByLabelText('DATA INICIAL')).toHaveValue(firstDay)
    expect(screen.getByLabelText('DATA FINAL')).toHaveValue(lastDay)
  })

  it('sends the dashboard operational filters to the API', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Filtros' }))
    fireEvent.change(await screen.findByLabelText('DATA INICIAL'), { target: { value: '2026-08-01' } })
    fireEvent.change(screen.getByLabelText('DATA FINAL'), { target: { value: '2026-08-31' } })
    fireEvent.change(screen.getByLabelText('MARCA'), { target: { value: 'OROLEITE' } })
    fireEvent.change(screen.getByLabelText('CIDADE'), { target: { value: 'GOIANIA' } })
    fireEvent.change(screen.getByLabelText('CLIENTE CONTEM'), { target: { value: 'MERCADO' } })
    fireEvent.change(screen.getByLabelText('PRODUTO CONTEM'), { target: { value: 'LEITE' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))

    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/api/dashboard?startDate=2026-08-01&endDate=2026-08-31&brand=OROLEITE&city=GOIANIA&customerContains=MERCADO&productContains=LEITE'))).toBe(true))
  })

  it('clears every dashboard filter and reloads the unfiltered dashboard', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Filtros' }))
    fireEvent.change(await screen.findByLabelText('DATA INICIAL'), { target: { value: '2026-08-01' } })
    fireEvent.change(screen.getByLabelText('DATA FINAL'), { target: { value: '2026-08-31' } })
    fireEvent.change(screen.getByLabelText('VENDEDOR'), { target: { value: 'ANA' } })
    fireEvent.change(screen.getByLabelText('MARCA'), { target: { value: 'OROLEITE' } })
    fireEvent.change(screen.getByLabelText('CLIENTE CONTEM'), { target: { value: 'MERCADO' } })
    fireEvent.click(screen.getByRole('button', { name: 'Limpar filtros' }))

    expect(window.location.search).toBe('')
    expect(document.querySelector('#dashboard-filter-panel')).not.toBeInTheDocument()
  })

  it('uses compact currency rendering for long trade KPI values', () => {
    render(<TradeAnalysisPage mode="sales-trades" state="ready" data={{
      filteredMovementCount: 1,
      grossSales: 6349887.34,
      netRevenue: 6180226.98,
      totalTradeValue: 239884.77,
      tradeToRevenuePercent: 3.88,
      tradeDevValue: 235441.7,
      tradeValue: 4443.07,
      tradeQuantity: 44715,
      tradeMovementCount: 5368,
      customerCount: 214,
      productCount: 207,
      brandCount: 19,
      dailyTrend: [], sellerRanking: [], customerRanking: [], productRanking: [], brandRanking: [],
    }} />)

    const revenueCard = screen.getByText('Faturamento liquido').closest('article')
    expect(within(revenueCard!).getByText('R$ 6.180.226,98')).toHaveClass('compact-currency-value')
  })

  it('loads registered sellers into the dashboard filter', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Filtros' }))
    expect(await screen.findByRole('option', { name: 'ANA' })).toBeVisible()
    expect(screen.getByRole('option', { name: 'BRUNO' })).toBeVisible()
  })

  it('shows the closing award for the selected seller and month', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento por vendedor' }))
    fireEvent.change(screen.getByLabelText('VENDEDOR'), { target: { value: 'ANA' } })
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-08' } })
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))

    const financialSummary = await screen.findByTestId('closing-financial-summary')
    expect(financialSummary).toHaveTextContent(/750,00/)
    expect(screen.getByRole('heading', { name: 'Premios por marca' })).toBeVisible()
    expect(screen.getByRole('table', { name: 'Metas e prêmios por marca' })).toHaveTextContent('NESTLE')
    expect(document.querySelector('.analysis-workspace')).toBeInTheDocument()
  })

  it('shows an API failure instead of a missing closing configuration', async () => {
    vi.mocked(fetch).mockImplementation((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/me')) return Promise.resolve(new Response(JSON.stringify({ roles: ['Administrador'] }), { status: 200 }))
      if (url.endsWith('/api/sellers')) return Promise.resolve(new Response(JSON.stringify(['ANA']), { status: 200 }))
      if (url.includes('/api/closings?')) return Promise.resolve(new Response(JSON.stringify({ error: 'Nenhum arquivo VALOR_METAS concluido foi encontrado para configurar o fechamento.' }), { status: 404 }))
      return Promise.resolve(new Response(JSON.stringify({ grossSales: 0, negativeMovements: 0, negativePercentage: 0, netResult: 0, saleQuantity: 0, movementCount: 0 }), { status: 200 }))
    })
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Fechamento por vendedor' }))
    fireEvent.change(screen.getByLabelText('VENDEDOR'), { target: { value: 'ANA' } })
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-08' } })
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))

    expect(await screen.findByRole('heading', { name: 'Nao foi possivel consultar o fechamento' })).toBeVisible()
    expect(screen.getByText('Nenhum arquivo VALOR_METAS concluido foi encontrado para configurar o fechamento.')).toBeVisible()
  })

  it('loads the sales versus trades analysis from its dedicated endpoint', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Analise Venda x Troca' }))

    expect(await screen.findByRole('heading', { name: 'Analise venda x troca' })).toBeVisible()
  })

  it('loads the detailed trade analysis for sales versus trades', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Analise Venda x Troca' }))

    expect(await screen.findByText('Clientes com troca')).toBeVisible()
  })

  it('uploads a CSV from the administrator import screen', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Importar' }))
    expect(document.querySelector('.import-workspace')).toBeInTheDocument()
    fireEvent.change(screen.getByLabelText('ARQUIVO CSV'), {
      target: { files: [new File(['seller;value'], 'power.csv', { type: 'text/csv' })] },
    })
    fireEvent.submit(screen.getByRole('button', { name: 'Enviar CSV' }).closest('form')!)

    await waitFor(() => expect(screen.queryByText('IMPORTACOES AUDITADAS')).not.toBeInTheDocument())
    const importRequest = vi.mocked(fetch).mock.calls.find(([url]) => String(url).endsWith('/api/imports'))
    expect(importRequest).toBeDefined()
    expect(importRequest?.[1]).toMatchObject({ method: 'POST' })
    expect(importRequest?.[1]?.body).toBeInstanceOf(FormData)
  })

  it('makes the whole import dropzone activate the file input', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Importar' }))

    expect(screen.getByText('Escolha o arquivo de origem').closest('label')).toHaveAttribute('for', 'import-file')
  })

  it('selects goal values when the current VALOR_METAS file is chosen', async () => {
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Importar' }))
    fireEvent.change(screen.getByLabelText('ARQUIVO CSV'), {
      target: { files: [new File(['MARCA;FATURAMENTO'], 'VALOR_METAS.csv', { type: 'text/csv' })] },
    })

    expect(screen.getByLabelText('TIPO')).toHaveValue('GoalValues')
  })

  it('reloads the dashboard after a completed CSV import', async () => {
    let dashboardRequests = 0
    vi.mocked(fetch).mockImplementation((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/me')) return Promise.resolve(new Response(JSON.stringify({ roles: ['Administrador'] }), { status: 200 }))
      if (url.endsWith('/api/sellers')) return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }))
      if (url.endsWith('/api/imports')) return Promise.resolve(new Response(JSON.stringify({}), { status: 201 }))
      if (url.includes('/api/dashboard?') && !url.includes('/details')) {
        dashboardRequests += 1
        const summary = dashboardRequests === 1
          ? { grossSales: 0, negativeMovements: 0, negativePercentage: 0, netResult: 0, saleQuantity: 0, movementCount: 0 }
          : { grossSales: 100, negativeMovements: 0, negativePercentage: 0, netResult: 100, saleQuantity: 1, movementCount: 1 }
        return Promise.resolve(new Response(JSON.stringify(summary), { status: 200 }))
      }

      return Promise.resolve(new Response(JSON.stringify({}), { status: 200 }))
    })
    render(<App />)

    expect(await screen.findByText('Nenhum movimento encontrado para os filtros aplicados.')).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: 'Importar' }))
    fireEvent.change(screen.getByLabelText('ARQUIVO CSV'), {
      target: { files: [new File(['seller;value'], 'power.csv', { type: 'text/csv' })] },
    })
    fireEvent.submit(screen.getByRole('button', { name: 'Enviar CSV' }).closest('form')!)

    expect(await screen.findByText('Quantidade venda')).toBeVisible()
    expect(dashboardRequests).toBe(2)
  })

  it('persists a valid login session and opens the dashboard', async () => {
    sessionStorage.clear()
    render(<App />)

    fireEvent.change(screen.getByLabelText('E-MAIL'), { target: { value: 'admin@oroleite.com' } })
    fireEvent.change(screen.getByLabelText('SENHA'), { target: { value: 'senha-segura' } })
    fireEvent.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('button', { name: 'Filtros' })).toBeVisible()
    expect(sessionStorage.getItem('orobi.access-token')).toBe('new-access-token')
  })

  it('presents the Oroleite identity in the redesigned login screen', () => {
    sessionStorage.clear()
    render(<App />)

    expect(document.querySelector('.login-layout')).toBeInTheDocument()
    expect(screen.getByAltText('Oroleite Distribuidora')).toHaveAttribute('src', '/logoOroleite.png')
    expect(screen.getByRole('heading', { name: /Central de resultados/ })).toBeVisible()
  })
})
