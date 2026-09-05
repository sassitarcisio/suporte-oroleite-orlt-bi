import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { SupervisorClosingPage } from './SupervisorClosingPage'
import type { ClosingSummary } from './closingTypes'

const summary: ClosingSummary = {
  ppp: { meanPercent: 0, award: 0 }, revenueAward: 100, positivityAward: 0, tradeAward: 200, totalAwards: 300, total: 2360,
  compensation: { baseSalary: 2000, commission: 60, totalSalary: 2060 },
  monthly: { scope: 'company', revenue: 4000, commissionableRevenue: 4000, tradeValue: 20, tradePercent: 0.5, documentCount: 0, movementCount: 0, customerCount: 0, documents: [] },
  pppSegments: [], brandAwards: [],
  supervisor: {
    ownCommission: 30, teamCommission: 20, networkCommission: 10, teamAverageAward: 100, payrollTeamAverageAward: 235.7142857,
    operations: [{ key: 'total', label: 'Total consolidado', revenue: 4000, trade: 25, tradeReturns: -5, totalTrades: 20, tradePercent: 0.5 }],
    team: [{ seller: 'PAULO', includedInPayroll: false, sales: { key: 'paulo', label: 'PAULO', revenue: 1000, trade: 10, tradeReturns: -2, totalTrades: 8, tradePercent: 0.8 }, pppAward: 0, goalAward: 0, totalAward: 0 }],
  },
}
const props = { summary, state: 'ready' as const, errorMessage: null, initialMonth: '2026-08', onSubmit: vi.fn() }

describe('Supervisor statement', () => {
  it('displays the API commission scopes, union and distinct team average criteria', () => {
    render(<SupervisorClosingPage {...props} />)
    expect(screen.getByLabelText('VENDEDOR')).toHaveValue('DEIVID MANNES')
    expect(screen.getByLabelText('VENDEDOR')).toBeDisabled()
    expect(screen.getByTestId('closing-financial-summary')).toHaveTextContent(/300,00.*2.360,00/)
    const operations = screen.getByRole('table', { name: 'Vendas e trocas por operação' })
    expect(within(operations).getByRole('row', { name: /Total consolidado/ })).toHaveTextContent(/4.000,00.*25,00.*-.*5,00.*20,00.*0,5%/)
    expect(screen.getByRole('region', { name: 'Comissões por operação' })).toHaveTextContent(/30,00.*20,00.*10,00/)
    const team = screen.getByRole('table', { name: 'Prêmios da equipe' })
    expect(within(team).getByRole('row', { name: /PAULO/ })).toHaveTextContent(/1.000,00.*0,00/)
    expect(screen.getByRole('region', { name: 'Critérios da média da equipe' })).toHaveTextContent(/100,00.*235,71/)
  })

  it('hides stale values and print until a fresh response to the selected period arrives', () => {
    const onSubmit = vi.fn()
    const view = render(<SupervisorClosingPage {...props} onSubmit={onSubmit} />)
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-07' } })
    expect(screen.queryByTestId('closing-financial-summary')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Imprimir demonstrativo' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))
    expect(onSubmit).toHaveBeenCalledWith('DEIVID MANNES', '2026-07')
    expect(screen.queryByTestId('closing-financial-summary')).not.toBeInTheDocument()
    view.rerender(<SupervisorClosingPage {...props} state="loading" summary={null} onSubmit={onSubmit} />)
    expect(screen.getByLabelText('MES')).toHaveValue('2026-07')
    view.rerender(<SupervisorClosingPage {...props} summary={{ ...summary }} onSubmit={onSubmit} />)
    expect(screen.getByTestId('closing-financial-summary')).toBeVisible()
    const print = vi.spyOn(window, 'print').mockImplementation(() => {})
    fireEvent.click(screen.getByRole('button', { name: 'Imprimir demonstrativo' }))
    expect(print).toHaveBeenCalledOnce()
    print.mockRestore()
  })

  it('preserves known totals without inventing missing supervisor details', () => {
    render(<SupervisorClosingPage {...props} summary={{ ...summary, supervisor: null }} />)
    expect(screen.getByTestId('closing-financial-summary')).toHaveTextContent('2.360,00')
    expect(screen.getByText(/Detalhamento da supervisão indisponível/)).toBeVisible()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
