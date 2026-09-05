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
  it('shows separate commission and trade bases for the official Valdir closing', () => {
    render(<ClosingsPage summary={{ ...summary, monthly: { ...summary.monthly, scope: 'company-excluding-bauducco', revenue: 4557465.78, commissionableRevenue: 4557465.78, tradeRevenueBase: 4546665.61, tradeValue: 234910.48, tradePercent: 5.17 } }} initialSeller="VALDIR ZACARIAS" initialMonth="2026-08" sellers={[]} state="ready" errorMessage={null} onSubmit={vi.fn()} />)
    expect(screen.getByRole('region', { name: 'Comissão · 0,10%' })).toHaveTextContent(/4\.557\.465,78/)
    expect(screen.getByRole('table', { name: 'Resumo geral de vendas e trocas' })).toHaveTextContent(/4\.546\.665,61/)
    expect(screen.queryByRole('heading', { name: 'Segmentos PPP' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Imprimir demonstrativo' })).toBeEnabled()
    const print = vi.spyOn(window, 'print').mockImplementation(() => {})
    fireEvent.click(screen.getByRole('button', { name: 'Imprimir demonstrativo' }))
    expect(print).toHaveBeenCalledOnce()
    print.mockRestore()
  })

  it('does not print stale Valdir amounts when the reference month changes', () => {
    render(<ClosingsPage summary={summary} sellers={[]} initialSeller="VALDIR ZACARIAS" initialMonth="2026-08" state="ready" errorMessage={null} onSubmit={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-07' } })
    expect(screen.queryByTestId('closing-financial-summary')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Imprimir demonstrativo' })).toBeDisabled()
  })

  it('keeps Valdir selected even when the commercial seller catalog does not contain him', () => {
    const onSubmit = vi.fn()
    render(<ClosingsPage summary={null} sellers={['MARCIO FERNANDES', 'DEIVID MANNES']} initialSeller="VALDIR ZACARIAS" state="idle" errorMessage={null} onSubmit={onSubmit} />)
    expect(screen.getByLabelText('VENDEDOR')).toHaveValue('VALDIR ZACARIAS')
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-08' } })
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))
    expect(onSubmit).toHaveBeenCalledWith('VALDIR ZACARIAS', '2026-08')
  })

  it('preserves seller and month controls when the generic closing returns Valdir company scope', () => {
    const props = { summary: null, sellers: ['ANA', 'VALDIR ZACARIAS'], state: 'idle' as const, errorMessage: null, onSubmit: vi.fn() }
    const view = render(<ClosingsPage {...props} />)
    fireEvent.change(screen.getByLabelText('VENDEDOR'), { target: { value: 'VALDIR ZACARIAS' } })
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-08' } })
    view.rerender(<ClosingsPage {...props} state="ready" summary={{ ...summary, monthly: { ...summary.monthly, scope: 'company-excluding-bauducco' } }} />)
    expect(screen.getByLabelText('VENDEDOR')).toBeEnabled()
    expect(screen.getByLabelText('VENDEDOR')).toHaveValue('VALDIR ZACARIAS')
    expect(screen.getByLabelText('MES')).toHaveValue('2026-08')
  })

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
