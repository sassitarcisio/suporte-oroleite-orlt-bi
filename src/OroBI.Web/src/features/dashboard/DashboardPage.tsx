import type { FormEvent } from 'react'

export type DashboardSummary = {
  grossSales: number
  negativeMovements: number
  negativePercentage: number
  netResult: number
  saleQuantity: number
  movementCount: number
}

export type DashboardFilters = {
  startDate: string
  endDate: string
  seller: string
  brand: string
  group: string
  city: string
  customerContains: string
  productContains: string
  movementType: string
}

export type DashboardFilterOptions = {
  brands: string[]
  groups: string[]
  cities: string[]
  movementTypes: string[]
}

type DashboardPageProps = {
  summary: DashboardSummary | null
  filters: DashboardFilters
  options: DashboardFilterOptions
  sellers: string[]
  state: 'idle' | 'loading' | 'ready' | 'error'
  onFiltersChange: (filters: DashboardFilters) => void
  onSubmit: () => void
  onClear: () => void
}

const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
const number = (value: number) => Number.isFinite(value) ? new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value) : '0'

export function DashboardPage({ summary, filters, options, sellers, state, onFiltersChange, onSubmit, onClear }: DashboardPageProps) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
  }

  return <section className="dashboard-layout">
    <section className="dashboard-hero">
      <div><p className="eyebrow">CENTRAL DE RESULTADOS</p><h1>Visao geral <em>da operacao.</em></h1><p>Acompanhe os indicadores comerciais consolidados.</p></div>
    </section>
    <form className="dashboard-filter" onSubmit={submit}>
      <div className="dashboard-filter-grid">
        <label>DATA INICIAL<input type="date" value={filters.startDate} onChange={event => onFiltersChange({ ...filters, startDate: event.target.value })} /></label>
        <label>DATA FINAL<input type="date" value={filters.endDate} onChange={event => onFiltersChange({ ...filters, endDate: event.target.value })} /></label>
        <label>VENDEDOR<select value={filters.seller} onChange={event => onFiltersChange({ ...filters, seller: event.target.value })}><option value="">Todos os vendedores</option>{sellers.map(registeredSeller => <option key={registeredSeller} value={registeredSeller}>{registeredSeller}</option>)}</select></label>
        <label>MARCA<select value={filters.brand} onChange={event => onFiltersChange({ ...filters, brand: event.target.value })}><option value="">Todas as marcas</option>{options.brands.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
        <label>GRUPO<select value={filters.group} onChange={event => onFiltersChange({ ...filters, group: event.target.value })}><option value="">Todos os grupos</option>{options.groups.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
        <label>TIPO MOVIMENTO<select value={filters.movementType} onChange={event => onFiltersChange({ ...filters, movementType: event.target.value })}><option value="">Todos os tipos</option>{options.movementTypes.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
        <label>CIDADE<select value={filters.city} onChange={event => onFiltersChange({ ...filters, city: event.target.value })}><option value="">Todas as cidades</option>{options.cities.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
        <label>CLIENTE CONTEM<input value={filters.customerContains} placeholder="Ex.: Mercado, Koch" onChange={event => onFiltersChange({ ...filters, customerContains: event.target.value })} /></label>
        <label>PRODUTO CONTEM<input value={filters.productContains} placeholder="Ex.: Leite, Queijo" onChange={event => onFiltersChange({ ...filters, productContains: event.target.value })} /></label>
      </div>
      <div className="dashboard-filter-actions"><button type="submit">Aplicar filtros</button><button className="dashboard-filter-clear" type="button" onClick={onClear}>Limpar filtros</button></div>
    </form>
    {state === 'loading' && <section className="notice">Consultando dados comerciais...</section>}
    {state === 'error' && <section className="notice error">Nao foi possivel carregar a API.</section>}
    {summary && state === 'ready' && summary.movementCount === 0 && <section className="notice">Nenhum movimento encontrado para os filtros aplicados.</section>}
    {summary && state === 'ready' && summary.movementCount > 0 && <section className="dashboard-metrics" data-testid="dashboard-metrics">
      <article className="dashboard-kpi primary"><p><i className="fa-solid fa-sack-dollar" aria-hidden="true" /> Faturamento bruto</p><strong>{money(summary.grossSales)}</strong><span>Somente movimentos de venda</span></article>
      <article className="dashboard-kpi petrol"><p><i className="fa-solid fa-chart-line" aria-hidden="true" /> Resultado liquido</p><strong>{money(summary.netResult)}</strong><span>Todos os movimentos</span></article>
      <article className="dashboard-kpi gold"><p><i className="fa-solid fa-arrow-trend-down" aria-hidden="true" /> Movimentos negativos</p><strong>{money(summary.negativeMovements)}</strong><span>{number(summary.negativePercentage)}% do bruto</span></article>
      <article className="dashboard-kpi neutral"><p><i className="fa-solid fa-boxes-stacked" aria-hidden="true" /> Quantidade vendida</p><strong>{number(summary.saleQuantity)}</strong><span>{summary.movementCount} movimentos importados</span></article>
    </section>}
  </section>
}
