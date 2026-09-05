import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { PayrollClosingPage } from './PayrollClosingPage'
import type { PayrollClosing } from './closingTypes'

const summary: PayrollClosing = {
  year: 2026, month: 8, coverageSeller: 'MARCIO LUIZ DA ROSA',
  coverageSellers: ['MARCIO LUIZ DA ROSA', 'RODRIGO'], sellerCount: 1,
  rows: [{ seller: 'TIAGO', sourceSeller: 'MARCIO LUIZ DA ROSA', reference: 'Cobertura de férias', revenue: 12345.67, baseSalary: 2000, commissionPercent: null, commission: 100.005, pppAward: 90, goalAward: 10, tradeAward: 20, incentives: 120, total: 2220.005 }],
  totalBaseSalary: 2000, totalCommission: 100.005, totalPppAward: 90, totalGoalAward: 10, totalIncentives: 120, total: 2220.005,
}
const props = { summary, state: 'ready' as const, errorMessage: null, initialMonth: '2026-08', onSubmit: vi.fn(), onExport: vi.fn() }

describe('Payroll statement', () => {
  it('uses API totals and keeps revenue out of the total row', () => {
    render(<PayrollClosingPage {...props} />)
    const table = screen.getByRole('table', { name: 'Fechamento para folha de pagamento' })
    const tiago = within(table).getByRole('row', { name: /TIAGO/ })
    expect(tiago).toHaveTextContent(/Conforme regra.*100,01.*2.220,01/)
    expect(within(tiago).getAllByRole('cell')[7]).toHaveTextContent('120,00')
    const total = within(table).getByRole('row', { name: /^Total da folha/ })
    expect(total).toHaveTextContent('Não consolidar')
    expect(total).not.toHaveTextContent('12.345,67')
    expect(total).toHaveTextContent('2.220,01')
    expect(within(total).getAllByRole('cell')[6]).toHaveTextContent('120,00')
    fireEvent.click(screen.getByRole('button', { name: 'Exportar Excel' }))
    expect(props.onExport).toHaveBeenCalledOnce()
  })

  it('only marks Tiago as vacation coverage when display and source names differ', () => {
    render(<PayrollClosingPage {...props} summary={{ ...summary, rows: [...summary.rows, { ...summary.rows[0], seller: 'SUPERVISOR: DEIVID MANNES', sourceSeller: 'DEIVID MANNES' }] }} />)
    const table = screen.getByRole('table', { name: 'Fechamento para folha de pagamento' })
    expect(within(table).getByRole('row', { name: /SUPERVISOR: DEIVID MANNES/ })).not.toHaveTextContent('Cobertura:')
    expect(within(table).getByRole('row', { name: /TIAGO/ })).toHaveTextContent('Cobertura: MARCIO LUIZ DA ROSA')
  })

  it('hides changed coverage data until the matching new response arrives and preserves controls during loading', () => {
    const onExport = vi.fn(), onSubmit = vi.fn()
    const view = render(<PayrollClosingPage {...props} onSubmit={onSubmit} onExport={onExport} />)
    fireEvent.change(screen.getByLabelText('Cobertura de férias do Tiago'), { target: { value: 'RODRIGO' } })
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Exportar Excel' }))
    expect(onExport).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Consultar fechamento' }))
    expect(onSubmit).toHaveBeenCalledWith('2026-08', 'RODRIGO')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    view.rerender(<PayrollClosingPage {...props} state="loading" summary={null} onSubmit={onSubmit} onExport={onExport} />)
    expect(screen.getByLabelText('Cobertura de férias do Tiago')).toHaveValue('RODRIGO')
    view.rerender(<PayrollClosingPage {...props} summary={{ ...summary, coverageSeller: 'RODRIGO' }} onSubmit={onSubmit} onExport={onExport} />)
    expect(screen.getByRole('button', { name: 'Exportar Excel' })).toBeEnabled()
  })

  it('does not export a previous month or show results on error', () => {
    const view = render(<PayrollClosingPage {...props} />)
    fireEvent.change(screen.getByLabelText('MES'), { target: { value: '2026-07' } })
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar Excel' })).toBeDisabled()
    view.rerender(<PayrollClosingPage {...props} state="error" errorMessage="Dados indisponíveis" />)
    expect(screen.getByRole('alert')).toHaveTextContent('Dados indisponíveis')
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
