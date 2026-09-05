import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ClosingsPage } from './ClosingsPage'

const summary = {
  ppp: { meanPercent: 75, award: 900 },
  revenueAward: 0, positivityAward: 50, tradeAward: 25, totalAwards: 975, total: 2985,
  compensation: { baseSalary: 2000, commission: 10, totalSalary: 2010 },
  monthly: {
    scope: 'seller', revenue: 1100, commissionableRevenue: 1000, tradeValue: 20, tradePercent: 2,
    documentCount: 1, movementCount: 2, customerCount: 1,
    documents: [{ documentNumber: 'NF123', date: '2026-08-01', seller: 'ANA', customerCode: '1', customerName: 'CLIENTE A', movementType: 'VENDA', totalValue: 1100 }],
  },
  pppSegments: [
    { segment: 'MERCADO', customerCount: 10, itemsPerSegment: 4, groupsPlaced: 30, achievementPercent: 75 },
    { segment: 'SEM BASE', customerCount: 0, itemsPerSegment: 4, groupsPlaced: 3, achievementPercent: null },
  ],
  brandAwards: [{ brand: 'NESTLE', revenueGoal: 2000, revenueActual: 1000, revenueAchievedPercent: 50, revenuePrize: 100, revenueAward: 0, positivityGoal: 20, positivityActual: 20, positivityAchievedPercent: 100, positivityPrize: 50, positivityAward: 50, tradeValue: 20, tradeActualPercent: 2, tradeGoalPercent: 2, tradePrize: 25, tradeAward: 25, totalAward: 75 }],
}

describe('Closing details', () => {
  it('renders monthly indicators, document totals, PPP rates and goal progress from the API', () => {
    render(<ClosingsPage summary={summary} sellers={['ANA']} state="ready" errorMessage={null} onSubmit={vi.fn()} />)
    expect(screen.getByRole('region', { name: 'Indicadores do mês' })).toHaveTextContent(/1\.100,00/)
    expect(screen.getByRole('region', { name: 'Indicadores do mês' })).toHaveTextContent('2%')
    expect(screen.getByTestId('closing-financial-summary')).toHaveTextContent(/2\.000,00/)
    expect(screen.getByTestId('closing-financial-summary')).toHaveTextContent(/2\.985,00/)
    const ppp = screen.getByRole('table', { name: 'Segmentos PPP' })
    expect(within(ppp).getByRole('row', { name: /MERCADO/ })).toHaveTextContent('75%')
    expect(within(ppp).getByRole('row', { name: /SEM BASE/ })).toHaveTextContent('Sem base')
    const brands = screen.getByRole('table', { name: 'Metas e prêmios por marca' })
    expect(within(brands).getByRole('row', { name: /NESTLE Faturamento/ })).toHaveTextContent(/2\.000,00.*1\.000,00.*50%.*100,00.*0,00/)
    fireEvent.click(screen.getByText('Ver 1 documentos'))
    expect(screen.getByRole('table', { name: 'Documentos do mês' })).toHaveTextContent('NF123')
  })

  it('shows empty detail states without inventing documents or segments', () => {
    render(<ClosingsPage summary={{ ...summary, monthly: { ...summary.monthly, documentCount: 0, documents: [] }, pppSegments: [], brandAwards: [] }} sellers={['ANA']} state="ready" errorMessage={null} onSubmit={vi.fn()} />)
    expect(screen.getByText('Nenhum documento identificado no período.')).toBeVisible()
    expect(screen.getByText('Nenhum segmento PPP no período.')).toBeVisible()
    expect(screen.getByText('Nenhuma meta por marca no período.')).toBeVisible()
    expect(screen.queryByText('NF123')).not.toBeInTheDocument()
  })
})
