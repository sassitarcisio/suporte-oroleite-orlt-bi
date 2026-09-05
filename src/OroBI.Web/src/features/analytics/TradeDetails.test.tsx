import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TradeDetails, type TradeDetailRow } from './TradeDetails'

const row = (label: string, tradeValue: number, tradePercent: number | null = 10): TradeDetailRow => ({ label, tradeValue, tradePercent, netRevenue: 1000, tradeQuantity: 2 })
describe('Sales and trades detail', () => {
  it('sorts all grouped rows, limits results, and preserves selection when filters reload', () => {
    const groups = { customer: [row('Cliente A', 30), row('Sem venda', 50, null)], brand: Array.from({ length: 25 }, (_, i) => row(`Marca ${i}`, i + 1, i)) }
    const { rerender } = render(<TradeDetails groups={groups} ready />)
    expect(within(screen.getByRole('table')).getAllByRole('row')[1]).toHaveTextContent('Sem venda')
    expect(screen.getByText('—')).toBeVisible()
    fireEvent.change(screen.getByLabelText('Agrupar venda e troca'), { target: { value: 'brand' } })
    fireEvent.change(screen.getByLabelText('Ordenar venda e troca'), { target: { value: 'tradePercent' } })
    fireEvent.change(screen.getByLabelText('Limite de venda e troca'), { target: { value: '10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar' }))
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(11)
    expect(within(screen.getByRole('table')).getAllByRole('row')[1]).toHaveTextContent('Marca 24')
    rerender(<TradeDetails ready={false} />)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    rerender(<TradeDetails ready groups={{ brand: [row('Marca filtrada', 4)] }} />)
    expect(screen.getByLabelText('Agrupar venda e troca')).toHaveValue('brand')
    expect(screen.getByRole('table')).toHaveTextContent('Marca filtrada')
  })
  it('distinguishes missing detail from a filter with no trades', () => {
    const { rerender } = render(<TradeDetails ready />)
    expect(screen.getByText('Detalhamento indisponível nesta consulta.')).toBeVisible()
    rerender(<TradeDetails ready groups={{ customer: [row('Só vendas', 0)] }} />)
    expect(screen.getByText('Sem trocas para os filtros selecionados.')).toBeVisible()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
