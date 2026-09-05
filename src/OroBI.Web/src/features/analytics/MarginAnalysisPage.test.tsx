import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { MarginAnalysisPage } from './MarginAnalysisPage'
import type { MarginReport, NetMarginReport, NetMarginRow } from './marginTypes'

const filters = { startDate: '2026-08-01', endDate: '2026-08-31', seller: '', brand: '', group: '', city: '', customerContains: '', productContains: '', movementType: '' }
const props = { filters, options: { brands: ['POWER'], groups: [], cities: [], movementTypes: [] }, sellers: ['ANA'], state: 'ready' as const, onSubmit: vi.fn() }
const gross: MarginReport = { revenue: 1000, cost: 801.6, grossProfit: 198.4, marginPercent: 19.84, customerCount: 2, productCount: 2, movementCount: 3, groups: {
  customer: [{ label: 'Menor lucro', revenue: 100, cost: 50, grossProfit: 50, marginPercent: 50, quantity: 1 }, { label: 'Maior lucro', revenue: 900, cost: 751.6, grossProfit: 148.4, marginPercent: 16.49, quantity: 2 }], product: [], brand: [],
} }
const netRow = (label: string, profit: number, quantity: number): NetMarginRow => ({ label, grossSales: 100, ownReturns: 10, customerReturns: 5, returns: 15, netSales: 85, netCost: 60, tradeLosses: 3, boletoDiscounts: 2, liquidProfit: profit, liquidMarginPercent: profit / 85 * 100, quantity, movementCount: 3, losses: 5 })
const net: NetMarginReport = { grossSales: 200, returns: 30, ownReturns: 20, customerReturns: 10, netSales: 170, netCost: 120, tradeLosses: 6, boletoDiscounts: 4, liquidProfit: 40, liquidMarginPercent: 23.53, quantity: 3, movementCount: 6, productCount: 2, groups: { product: [netRow('Produto A', 10, 1), netRow('Produto B', 30, 2)], seller: [netRow('ANA', 40, 3)], customer: [netRow('Cliente negativo', -10, 10), netRow('Cliente positivo', 50, 2)], brand: [], group: [], city: [] } }

describe('Margin analysis', () => {
  it('renders percentage points and sorts grouped gross detail by selected metric', () => {
    render(<MarginAnalysisPage {...props} mode="products" data={gross} />)
    expect(screen.getByTestId('margin-metrics')).toHaveTextContent('19,84%')
    const table = screen.getByRole('table', { name: 'Detalhamento de margem' })
    expect(within(table).getAllByRole('row')[1]).toHaveTextContent('Maior lucro')
    fireEvent.change(screen.getByLabelText('Ordenar detalhamento'), { target: { value: 'marginPercent' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar detalhamento' }))
    expect(within(table).getAllByRole('row')[1]).toHaveTextContent('Menor lucro')
  })
  it('submits all filters and preserves the financially relevant movement types', () => {
    const onSubmit = vi.fn()
    render(<MarginAnalysisPage {...props} mode="products" data={gross} onSubmit={onSubmit} />)
    fireEvent.change(screen.getByLabelText('Cliente contém'), { target: { value: 'Mercado' } })
    fireEvent.change(screen.getByLabelText('Grupo / rede'), { target: { value: 'REDE A' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    expect(onSubmit).toHaveBeenCalledWith({ ...filters, customerContains: 'Mercado', group: 'REDE A', movementType: '' })
  })
  it('shares grouping and metric between net chart and ranking after update', () => {
    render(<MarginAnalysisPage {...props} mode="net" data={net} />)
    expect(within(screen.getByTestId('margin-metrics')).getAllByRole('article')).toHaveLength(8)
    expect(screen.getByRole('table', { name: 'Margem líquida por produto' })).toHaveTextContent(/Devol. própria.*Devol. cliente/)
    fireEvent.change(screen.getByLabelText('Agrupar análise'), { target: { value: 'customer' } })
    fireEvent.change(screen.getByLabelText('Métrica da análise'), { target: { value: 'quantity' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise' }))
    const ranking = screen.getByRole('table', { name: 'Ranking dinâmico' })
    expect(within(ranking).getAllByRole('row')[1]).toHaveTextContent('Cliente negativo')
    expect(screen.getByRole('list', { name: 'Quantidade por Cliente' })).toHaveTextContent(/Cliente negativo.*10/)
    fireEvent.change(screen.getByLabelText('Métrica da análise'), { target: { value: 'liquidProfit' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise' }))
    const negativeBar = within(screen.getByRole('list', { name: 'Lucro líquido por Cliente' })).getByText('Cliente negativo').closest('li')!
    expect(negativeBar).toHaveClass('is-negative')
    expect(negativeBar).toHaveTextContent('-R$')
  })
  it('handles missing grouping fields without fabricating detail or NaN and hides stale data', () => {
    const { rerender, container } = render(<MarginAnalysisPage {...props} mode="products" data={{ revenue: 0, cost: 0, grossProfit: 0, marginPercent: 0 } as MarginReport} />)
    expect(screen.getAllByText('Nenhum resultado para os filtros aplicados.').length).toBeGreaterThan(0)
    expect(container).not.toHaveTextContent('NaN')
    rerender(<MarginAnalysisPage {...props} mode="products" data={gross} state="loading" />)
    expect(screen.queryByTestId('margin-metrics')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('Consultando')
  })
  it('applies dynamic limits to both views and preserves undefined percentage for zero revenue', () => {
    const manyCustomers = Array.from({ length: 18 }, (_, i) => netRow(`Cliente ${i + 1}`, i, i))
    const noRevenue = { ...netRow('Sem faturamento', -5, 1), netSales: 0, liquidMarginPercent: null }
    render(<MarginAnalysisPage {...props} mode="net" data={{ ...net, groups: { ...net.groups, customer: manyCustomers, product: [noRevenue] } }} />)
    expect(screen.getByRole('table', { name: 'Margem líquida por produto' })).toHaveTextContent('—')
    fireEvent.change(screen.getByLabelText('Agrupar análise'), { target: { value: 'customer' } })
    fireEvent.change(screen.getByLabelText('Limite da análise'), { target: { value: '15' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise' }))
    expect(within(screen.getByRole('table', { name: 'Ranking dinâmico' })).getAllByRole('row')).toHaveLength(16)
    expect(within(screen.getByRole('list', { name: 'Lucro líquido por Cliente' })).getAllByRole('listitem')).toHaveLength(15)
  })
  it('preserves gross detail settings through filter reloads without displaying stale totals', () => {
    const report = { ...gross, groups: { ...gross.groups, brand: gross.groups.customer } }
    const { rerender } = render(<MarginAnalysisPage {...props} mode="products" data={report} />)
    fireEvent.change(screen.getByLabelText('Agrupar detalhamento'), { target: { value: 'brand' } })
    fireEvent.change(screen.getByLabelText('Ordenar detalhamento'), { target: { value: 'marginPercent' } })
    fireEvent.change(screen.getByLabelText('Limite do detalhamento'), { target: { value: '50' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar detalhamento' }))
    fireEvent.change(screen.getByLabelText('Marca'), { target: { value: 'POWER' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    rerender(<MarginAnalysisPage {...props} mode="products" state="loading" data={report} />)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByTestId('margin-metrics')).not.toBeInTheDocument()
    rerender(<MarginAnalysisPage {...props} mode="products" data={{ ...report }} />)
    expect(screen.getByLabelText('Agrupar detalhamento')).toHaveValue('brand')
    expect(screen.getByLabelText('Ordenar detalhamento')).toHaveValue('marginPercent')
    expect(screen.getByLabelText('Limite do detalhamento')).toHaveValue('50')
    const table = screen.getByRole('table', { name: 'Detalhamento de margem' })
    expect(within(table).getByRole('columnheader', { name: 'Marca' })).toBeInTheDocument()
    expect(within(table).getAllByRole('row')[1]).toHaveTextContent('Menor lucro')
  })
  it('preserves net product and dynamic settings including unapplied drafts through reloads', () => {
    const { rerender } = render(<MarginAnalysisPage {...props} mode="net" data={net} />)
    fireEvent.change(screen.getByLabelText('Ordenar produtos'), { target: { value: 'grossSales' } })
    fireEvent.change(screen.getByLabelText('Limite de produtos'), { target: { value: '100' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar produtos' }))
    fireEvent.change(screen.getByLabelText('Agrupar análise'), { target: { value: 'customer' } })
    fireEvent.change(screen.getByLabelText('Métrica da análise'), { target: { value: 'quantity' } })
    fireEvent.change(screen.getByLabelText('Limite da análise'), { target: { value: '25' } })
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar análise' }))
    fireEvent.change(screen.getByLabelText('Métrica da análise'), { target: { value: 'losses' } })
    fireEvent.change(screen.getByLabelText('Data inicial'), { target: { value: '2026-08-02' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    rerender(<MarginAnalysisPage {...props} mode="net" state="loading" data={null} />)
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByTestId('margin-metrics')).not.toBeInTheDocument()
    rerender(<MarginAnalysisPage {...props} mode="net" data={{ ...net }} />)
    expect(screen.getByLabelText('Ordenar produtos')).toHaveValue('grossSales')
    expect(screen.getByLabelText('Limite de produtos')).toHaveValue('100')
    expect(screen.getByLabelText('Agrupar análise')).toHaveValue('customer')
    expect(screen.getByLabelText('Métrica da análise')).toHaveValue('losses')
    expect(screen.getByLabelText('Limite da análise')).toHaveValue('25')
    expect(screen.getByRole('list', { name: 'Quantidade por Cliente' })).toBeInTheDocument()
    const ranking = screen.getByRole('table', { name: 'Ranking dinâmico' })
    expect(within(ranking).getAllByRole('row')[1]).toHaveTextContent('Cliente negativo')
    const products = screen.getByRole('table', { name: 'Margem líquida por produto' })
    expect(within(products).getAllByRole('row')[1]).toHaveTextContent('Produto A')
  })
})
