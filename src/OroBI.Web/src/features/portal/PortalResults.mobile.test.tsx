import { render, screen, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { HomeMonthly, PersonalDashboard } from './PortalResults'
import type { PortalDashboard } from './portalTypes'

afterEach(() => vi.unstubAllGlobals())
it('shows target, actual and remaining by goal unit, including goals beyond the first four', async () => {
  const base = { achievedPercent: 70, maximumPrize: null, currentPrize: null, nextTierPercent: null, amountToNextTier: null, nextTierPrize: null }
  const items = [
    { ...base, brand: 'Receita', type: 'FATURAMENTO', target: 1000, actual: 700 },
    ...['Clientes A', 'Clientes B', 'Clientes C', 'Clientes D'].map(brand => ({ ...base, brand, type: 'POSITIVACAO', target: 10, actual: 12 })),
  ]
  vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ items, available: true }), { status: 200 })))
  render(<HomeMonthly token="seller" base="/api/v1/me" permissions={{ canViewRevenue: true, canViewCommission: false, canViewPrize: false, canViewPPP: false, canViewGoals: true, canViewCustomers: true, canViewTrades: false }} />)
  const revenue = (await screen.findByText('Receita')).closest('.portal-record')! as HTMLElement
  expect(within(revenue).getByText('Meta').nextElementSibling).toHaveTextContent('1.000,00')
  expect(within(revenue).getByText('Realizado').nextElementSibling).toHaveTextContent('700,00')
  expect(within(revenue).getByText('Quanto falta').nextElementSibling).toHaveTextContent('300,00')
  const customers = screen.getByText('Clientes D').closest('.portal-record')! as HTMLElement
  expect(within(customers).getByText('Meta').nextElementSibling).toHaveTextContent('10 clientes')
  expect(within(customers).getByText('Realizado').nextElementSibling).toHaveTextContent('12 clientes')
  expect(within(customers).getByText('Quanto falta').nextElementSibling).toHaveTextContent('0 clientes')
})

it.each([
  ['csv', 'import-started', 'Importação iniciada'],
  ['firebird', 'sync-completed', 'Dados atualizados'],
])('describes the source timestamp honestly for %s without technical source names', (source, timestampKind, expected) => {
  const revenue = { grossSales: 100, netRevenue: 100, negativeMovements: 0, saleQuantity: 1, customerCount: 1, movementCount: 0, documentCount: 1, averageTicket: 100 }
  const data: PortalDashboard = { startDate: '2026-09-01', endDate: '2026-09-05', referenceDate: '2026-09-05', period: revenue, month: revenue, today: revenue, dailyTrend: [], freshness: { source, timestampKind, updatedAtUtc: '2026-09-05T12:00:00Z' } }
  const { container } = render(<PersonalDashboard data={data} />)
  const freshness = container.querySelector('.portal-source')!
  expect(freshness).toHaveTextContent(expected)
  expect(freshness).toHaveTextContent('05/09/2026')
  expect(freshness).not.toHaveTextContent(/Firebird|CSV|SQL|Sincronização concluída/)
})
