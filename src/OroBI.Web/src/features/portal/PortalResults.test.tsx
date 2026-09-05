import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Goals, PersonalDashboard, Ppp, Ranking } from './PortalResults'
import type { PortalDashboard, PortalGoals, PortalPpp } from './portalTypes'

describe('Approved personal indicators', () => {
  it('prefixes product names with their codes while keeping uncoded results readable', () => {
    const metrics = { grossSales: 100, netRevenue: 100, quantity: 2, movementCount: 1, customerCount: 1, revenueSharePercent: 25 }
    render(<Ranking data={{ items: [
      { ...metrics, label: 'Leite integral', productCode: '00123' },
      { ...metrics, label: 'Leite integral', productCode: '00456' },
      { ...metrics, label: 'Produto legado' },
    ], totalCount: 3, hasMore: false }} />)
    expect(screen.getByText('00123 · Leite integral')).toBeVisible()
    expect(screen.getByText('00456 · Leite integral')).toBeVisible()
    expect(screen.getByText('Produto legado')).toBeVisible()
  })
  it('presents a daily chart from server amounts with an accessible description', () => {
    const revenue = { grossSales: 100, netRevenue: 80, negativeMovements: 20, saleQuantity: 2, movementCount: 2, customerCount: 1, documentCount: 2, averageTicket: 40 }
    const data: PortalDashboard = { startDate: '2026-09-01', endDate: '2026-09-02', referenceDate: '2026-09-02', period: revenue, month: revenue, today: revenue, dailyTrend: [{ date: '2026-09-01', grossSales: 100, netRevenue: 100, negativeMovements: 0 }, { date: '2026-09-02', grossSales: 0, netRevenue: -20, negativeMovements: 20 }], freshness: { source: 'csv', updatedAtUtc: null, timestampKind: 'unavailable' } }
    render(<PersonalDashboard data={data} />)
    expect(screen.getByRole('img', { name: 'Receita líquida diária' })).toBeVisible()
    expect(screen.getByText('01/09/2026: R$ 100,00', { exact: false })).toBeInTheDocument()
  })
  it('keeps goal progress visible when prize configuration is missing', () => {
    const data = { year: 2026, month: 8, available: false, unavailableReason: 'Prêmios indisponíveis: falta configuração.', items: [{ brand: 'OROLEITE', type: 'FATURAMENTO', target: 1000, actual: 900, achievedPercent: 90, maximumPrize: null, currentPrize: null, nextTierPercent: null, amountToNextTier: null, nextTierPrize: null }] }
    render(<Goals data={data as PortalGoals} />)
    expect(screen.getByText('90%')).toBeVisible()
    expect(screen.getByText('Prêmios indisponíveis: falta configuração.')).toBeVisible()
  })
  it('keeps PPP segments visible when prize configuration is missing', () => {
    const data = { year: 2026, month: 8, available: false, unavailableReason: 'Prêmio não disponível.', achievementPercent: 80, award: null, segments: [{ segment: 'PADARIA', customerCount: 2, itemsPerSegment: 5, groupsPlaced: 8, achievementPercent: 80 }] }
    render(<Ppp data={data as PortalPpp} />)
    expect(screen.getByText('PADARIA')).toBeVisible()
    expect(screen.getByText('Prêmio não disponível.')).toBeVisible()
  })
  it('labels approved goal prizes as official', () => {
    const data = { year: 2026, month: 8, available: true, isApproved: true, unavailableReason: null, items: [{ brand: 'OROLEITE', type: 'FATURAMENTO', target: 1000, actual: 1000, achievedPercent: 100, maximumPrize: 100, currentPrize: 100, nextTierPercent: null, amountToNextTier: null, nextTierPrize: null }] }
    render(<Goals data={data as PortalGoals} />)
    expect(screen.getByText('Prêmio oficial')).toBeVisible()
    expect(screen.queryByText(/Prêmio estimado/)).not.toBeInTheDocument()
  })
  it('labels approved PPP as official', () => {
    const data = { year: 2026, month: 8, available: true, isApproved: true, unavailableReason: null, achievementPercent: 80, award: 120, segments: [] }
    render(<Ppp data={data as PortalPpp} />)
    expect(screen.getByText('Prêmio PPP oficial')).toBeVisible()
  })
  it('omits restricted PPP customer and placement counts while preserving authorized indicators', () => {
    const data = { year: 2026, month: 8, available: true, isApproved: true, unavailableReason: null, achievementPercent: 80, award: 120, segments: [{ segment: 'PADARIA', customerCount: null, itemsPerSegment: 5, groupsPlaced: null, achievementPercent: 80 }] }
    render(<Ppp data={data as PortalPpp} />)
    expect(screen.getByText('PADARIA')).toBeVisible()
    expect(screen.getByText('5 itens por segmento')).toBeVisible()
    expect(screen.getAllByText('80%')).toHaveLength(2)
    expect(screen.getByText(/120,00/)).toBeVisible()
    expect(screen.queryByText(/clientes|grupos positivados/)).not.toBeInTheDocument()
  })
})
