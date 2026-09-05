import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DashboardBreakdowns, type DashboardGroupRow } from './DashboardBreakdowns'

const row = (label: string, netResult: number, movementCount = 1): DashboardGroupRow => ({ label, netResult, grossSales: Math.max(0, netResult), negativeMovements: Math.max(0, -netResult), quantity: 3, movementCount, documentCount: 1 })
const groups = { seller: [row('ANA', 80), row('BRUNO', 20)], brand: [row('Marca A', 50, 2), row('Marca B', -10, 9)], customer: [row('Cliente A', 40)], movementType: [row('VENDA', 100), row('TROCA DEV', -20)] }

describe('Dashboard breakdowns', () => {
  it('shows the three new charts with signed movement values', () => {
    render(<DashboardBreakdowns groups={groups} ready />)
    for (const name of ['Resultado por marca', 'Tipos de movimento', 'Top clientes']) expect(screen.getByRole('heading', { name })).toBeVisible()
    const negative = within(screen.getByRole('list', { name: 'Tipos de movimento' })).getByText('TROCA DEV').closest('li')!
    expect(negative).toHaveClass('is-negative')
    expect(negative).toHaveTextContent('-R$ 20,00')
  })

  it('uses one selection for chart and ranking, and retains it through filter reloads', () => {
    const { rerender } = render(<DashboardBreakdowns groups={groups} ready />)
    fireEvent.change(screen.getByLabelText('Agrupar análise do dashboard'), { target: { value: 'brand' } })
    fireEvent.change(screen.getByLabelText('Métrica do dashboard'), { target: { value: 'movementCount' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise do dashboard' }))
    const ranking = screen.getByRole('table', { name: 'Ranking dinâmico do dashboard' })
    expect(within(ranking).getAllByRole('row')[1]).toHaveTextContent('Marca B9')
    expect(within(screen.getByRole('list', { name: 'Análise dinâmica do dashboard' })).getAllByRole('listitem')[0]).toHaveTextContent('Marca B9')
    rerender(<DashboardBreakdowns groups={groups} ready={false} />)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    rerender(<DashboardBreakdowns groups={{ ...groups, brand: [row('Nova marca', 0, 12)] }} ready />)
    expect(screen.getByLabelText('Métrica do dashboard')).toHaveValue('movementCount')
    expect(screen.getByRole('table')).toHaveTextContent('Nova marca12')
  })

  it('applies limits to full grouped data and handles empty results', () => {
    const { rerender } = render(<DashboardBreakdowns groups={{ ...groups, seller: Array.from({ length: 30 }, (_, i) => row(`Vendedor ${i}`, i)) }} ready />)
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(11)
    fireEvent.change(screen.getByLabelText('Limite do dashboard'), { target: { value: '25' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise do dashboard' }))
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(26)
    rerender(<DashboardBreakdowns groups={{}} ready />)
    expect(screen.getAllByText('Nenhum movimento para os filtros selecionados.').length).toBeGreaterThan(0)
  })
})
