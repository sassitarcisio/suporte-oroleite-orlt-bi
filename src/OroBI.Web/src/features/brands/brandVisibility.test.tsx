import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Ranking, Goals, HomeMonthly, Sales, CustomerDetail } from '../portal/PortalResults'
import type { PortalGoal, PortalRanking, PortalSale } from '../portal/portalTypes'
import { DashboardBreakdowns, type DashboardGroupRow } from '../dashboard/DashboardBreakdowns'
import { DashboardPage } from '../dashboard/DashboardPage'
import { MarginAnalysisPage } from '../analytics/MarginAnalysisPage'
import type { MarginReport, NetMarginReport, NetMarginRow } from '../analytics/marginTypes'
import { TradeAnalysisPage, type TradeAnalysis } from '../analytics/TradeAnalysisPage'
import { ClosingDetails } from '../closings/ClosingDetails'
import type { ClosingSummary } from '../closings/closingTypes'

const hidden = ['ZZZ - INATIVO F', 'SEM INFORMACAO', ' sem  informação ', 'MARCA (inativa)', 'INATIVOS', 'SEM INFORMAÇÕES', '   ']
const visible = ['OROLEITE', 'TIA SONIA', 'ATIVA COM DEVOLUÇÃO', 'ZERO NO PERÍODO']
const ranking: PortalRanking = {
  items: [...hidden, ...visible].map((label, index) => ({ label, grossSales: 0, netRevenue: label === 'ZERO NO PERÍODO' ? 0 : -index, quantity: 0, movementCount: 1, customerCount: 1, revenueSharePercent: -0.01 })),
  totalCount: hidden.length + visible.length, hasMore: false,
}
const goal = (brand: string): PortalGoal => ({ brand, type: 'FATURAMENTO', target: 100, actual: 20, achievedPercent: 20, maximumPrize: 30, currentPrize: 0, nextTierPercent: null, amountToNextTier: null, nextTierPrize: null })
const goals = { year: 2026, month: 9, available: true, unavailableReason: null, items: [...hidden, 'OROLEITE'].map(goal) }
const filters = { startDate: '', endDate: '', seller: '', brand: '', group: '', city: '', customerContains: '', productContains: '', movementType: '' }
const options = { brands: [...hidden, 'OROLEITE'], groups: [], cities: [], movementTypes: [] }
const groupRow = (label: string, netResult: number): DashboardGroupRow => ({ label, netResult, grossSales: 0, negativeMovements: 0, quantity: 0, movementCount: 1, documentCount: 1 })
const sale: PortalSale = { id: '1', date: '2026-09-09', documentNumber: '123', movementType: 'VENDA', customerCode: 'C1', customerName: 'Cliente A', productName: 'Leite integral', brand: hidden[0], quantity: 2, totalValue: -11.98 }
const gross: MarginReport = { revenue: 1234, cost: 1000, grossProfit: 234, marginPercent: 18.96, customerCount: 2, productCount: 2, movementCount: 3, groups: {
  customer: [], product: [], brand: [...hidden, 'OROLEITE'].map(label => ({ label, revenue: 1234, cost: 1000, grossProfit: 234, marginPercent: 18.96, quantity: 2 })),
} }
const netRow = (label: string): NetMarginRow => ({ label, grossSales: 100, ownReturns: 10, customerReturns: 5, returns: 15, netSales: 85, netCost: 60, tradeLosses: 3, boletoDiscounts: 2, liquidProfit: 20, liquidMarginPercent: 23.53, losses: 5, quantity: 2, movementCount: 3 })
const net: NetMarginReport = { grossSales: 100, returns: 15, ownReturns: 10, customerReturns: 5, netSales: 85, netCost: 60, tradeLosses: 3, boletoDiscounts: 2, liquidProfit: 20, liquidMarginPercent: 23.53, quantity: 2, movementCount: 3, productCount: 1, groups: { brand: [...hidden, 'OROLEITE'].map(netRow), seller: [], customer: [], product: [], group: [], city: [] } }

function expectHiddenBrandsAbsent(container: HTMLElement) {
  for (const name of hidden.filter(name => name.trim())) expect(container.textContent?.toUpperCase()).not.toContain(name.trim().replace(/\s+/g, ' ').toUpperCase())
}

describe('Brand visibility across screens', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('hides inactive and missing brands while keeping active negative and zero results', () => {
    const { container, rerender } = render(<Ranking data={ranking} dimension="brand" />)
    expect(container.querySelectorAll('.portal-record')).toHaveLength(visible.length)
    expectHiddenBrandsAbsent(container)
    for (const name of visible) expect(screen.getByText(name)).toBeVisible()
    expect(screen.getAllByText(/-0,01% da receita líquida/)).toHaveLength(visible.length)
    expect(ranking.items).toHaveLength(hidden.length + visible.length)
    rerender(<Ranking data={{ ...ranking, items: ranking.items.slice(0, hidden.length) }} dimension="brand" />)
    expect(screen.getByText('Nenhum resultado no período selecionado.')).toBeVisible()
    rerender(<Ranking data={ranking} />)
    expect(screen.getByText('SEM INFORMACAO')).toBeVisible()
  })

  it('uses the visible count and avoids reporting a hidden-brand total for truncated results', () => {
    render(<Ranking data={{ ...ranking, totalCount: 999, hasMore: true }} dimension="brand" />)
    expect(screen.getByText(/Exibindo 4 marcas/)).toBeVisible()
    expect(screen.queryByText(/999/)).not.toBeInTheDocument()
  })

  it('hides brand goal cards and gives an empty state when all brands are hidden', () => {
    const { container, rerender } = render(<Goals data={goals} />)
    expectHiddenBrandsAbsent(container)
    expect(screen.getByRole('heading', { name: 'OROLEITE' })).toBeVisible()
    rerender(<Goals data={{ ...goals, items: hidden.map(goal) }} />)
    expect(screen.getByText('Metas não disponíveis para este mês.')).toBeVisible()
  })

  it('applies the same rule to the monthly home goal cards', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify(goals), { status: 200 })))
    const { container } = render(<HomeMonthly token="test-token" base="/api/v1/me" permissions={{ canViewGoals: true, canViewRevenue: false, canViewCommission: false, canViewPrize: false, canViewCustomers: false, canViewTrades: false, canViewPPP: false }} />)
    expect(await screen.findByText('OROLEITE')).toBeVisible()
    expectHiddenBrandsAbsent(container)
  })

  it('hides only the brand caption in sales and customer details, preserving the movement', () => {
    const { container, unmount } = render(<Sales data={{ items: [sale], totalCount: 1, page: 1, pageSize: 20 }} onPage={vi.fn()} />)
    expectHiddenBrandsAbsent(container)
    expect(screen.getByText('Leite integral')).toBeVisible()
    expect(screen.getByText(/11,98/)).toBeVisible()
    expect(screen.getByText(/1 movimentos/)).toBeVisible()
    unmount()
    const detail = render(<CustomerDetail data={{ customer: { customerCode: 'C1', customerName: 'Cliente A', city: '', grossSales: 0, netRevenue: -11.98, documentCount: 1, lastPurchaseDate: sale.date, averageTicket: -11.98, purchasedQuantity: 2 }, sales: [sale], totalCount: 1, hasMore: false }} />)
    expectHiddenBrandsAbsent(detail.container)
    expect(screen.getByText('Leite integral')).toBeVisible()
    expect(screen.getByText('Doc. 123')).toBeVisible()
  })

  it('filters dashboard brand charts before top limits and leaves customer labels alone', () => {
    const active = Array.from({ length: 10 }, (_, i) => groupRow(`Marca ${i}`, -i))
    render(<DashboardBreakdowns ready groups={{ brand: [...hidden.map(name => groupRow(name, 100)), ...active], customer: [groupRow('SEM INFORMACAO', 1)] }} />)
    const chart = screen.getByRole('list', { name: 'Resultado por marca' })
    expect(within(chart).getAllByRole('listitem')).toHaveLength(10)
    expectHiddenBrandsAbsent(chart)
    expect(within(screen.getByRole('list', { name: 'Top clientes' })).getByText('SEM INFORMACAO')).toBeVisible()
    fireEvent.change(screen.getByLabelText('Agrupar análise do dashboard'), { target: { value: 'brand' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise do dashboard' }))
    expectHiddenBrandsAbsent(screen.getByRole('table'))
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(11)
  })

  it('removes hidden brands from the dashboard selector', () => {
    render(<DashboardPage summary={null} filters={filters} options={options} details={null} sellers={[]} state="idle" onFiltersChange={vi.fn()} onSubmit={vi.fn()} onClear={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }))
    const select = screen.getByLabelText('MARCA')
    expect(within(select).getAllByRole('option')).toHaveLength(2)
    expect(within(select).getByRole('option', { name: 'OROLEITE' })).toBeVisible()
  })

  it.each(['products', 'net'] as const)('filters %s margin charts, tables and selectors while keeping the consolidated indicators', mode => {
    const { container } = render(<MarginAnalysisPage mode={mode} data={mode === 'net' ? net : gross} state="ready" filters={filters} options={options} sellers={[]} onSubmit={vi.fn()} />)
    expect(within(screen.getByLabelText('Marca')).getAllByRole('option')).toHaveLength(2)
    fireEvent.change(screen.getByLabelText(mode === 'net' ? 'Agrupar análise' : 'Agrupar detalhamento'), { target: { value: 'brand' } })
    fireEvent.click(screen.getByRole('button', { name: mode === 'net' ? 'Atualizar análise' : 'Atualizar detalhamento' }))
    expectHiddenBrandsAbsent(container)
    expect(screen.getAllByText('OROLEITE').length).toBeGreaterThan(1)
    expect(screen.getByTestId('margin-metrics')).toHaveTextContent(mode === 'net' ? /100,00/ : /1\.234,00/)
  })

  it('filters trade brand ranking and grouping without changing the trade total', () => {
    const data: TradeAnalysis = { filteredMovementCount: 3, grossSales: 1000, netRevenue: 900, totalTradeValue: 99, tradeToRevenuePercent: 11, tradeDevValue: 99, tradeValue: 0, tradeQuantity: 2, tradeMovementCount: 1, customerCount: 1, productCount: 1, brandCount: 3, dailyTrend: [], sellerRanking: [], customerRanking: [], productRanking: [], brandRanking: [...hidden, 'OROLEITE'].map(name => ({ name, value: 33 })), groups: { brand: [...hidden, 'OROLEITE'].map(label => ({ label, netRevenue: 300, tradeValue: 33, tradePercent: 11, tradeQuantity: 1 })) } }
    const { container } = render(<TradeAnalysisPage mode="sales-trades" state="ready" data={data} />)
    fireEvent.change(screen.getByLabelText('Agrupar venda e troca'), { target: { value: 'brand' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar' }))
    expectHiddenBrandsAbsent(container)
    expect(screen.getAllByText('OROLEITE')).toHaveLength(2)
    expect(container.querySelector('.trade-kpis')).toHaveTextContent(/99,00/)
  })

  it('hides brand award detail rows and keeps the summary unchanged', () => {
    const award: ClosingSummary['brandAwards'][number] = { brand: 'OROLEITE', revenueGoal: 100, revenueActual: 100, revenueAchievedPercent: 100, revenuePrize: 10, revenueAward: 10, positivityGoal: 1, positivityActual: 1, positivityAchievedPercent: 100, positivityPrize: 10, positivityAward: 10, tradeGoalPercent: 2, tradeActualPercent: 1, tradeValue: 1, tradePrize: 10, tradeAward: 10, totalAward: 30 }
    const summary: ClosingSummary = {
      pppSegments: [], ppp: { meanPercent: 0, award: 0 }, revenueAward: 10, positivityAward: 10, tradeAward: 10,
      compensation: { baseSalary: 0, commission: 0, totalSalary: 0 }, totalAwards: 30, total: 30,
      monthly: { scope: 'seller', revenue: 100, commissionableRevenue: 100, tradeValue: 1, tradePercent: 1, documentCount: 0, movementCount: 0, customerCount: 0, documents: [] },
      brandAwards: [...hidden, 'OROLEITE'].map(brand => ({ ...award, brand })),
    }
    const { container, rerender } = render(<ClosingDetails summary={summary} />)
    expectHiddenBrandsAbsent(container)
    expect(within(screen.getByRole('table', { name: 'Metas e prêmios por marca' })).getAllByRole('row')).toHaveLength(5)
    expect(summary.brandAwards).toHaveLength(hidden.length + 1)
    rerender(<ClosingDetails summary={{ ...summary, brandAwards: hidden.map(brand => ({ ...award, brand })) }} />)
    expect(screen.getByText('Nenhuma meta por marca no período.')).toBeVisible()
  })
})
