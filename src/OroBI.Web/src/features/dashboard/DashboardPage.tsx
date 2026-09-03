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

export type DashboardDetails = {
  dailyTrend: Array<{ date: string, grossSales: number, netResult: number, negativeMovements: number }>
  sellerResults: Array<{ seller: string, netResult: number }>
}

type DashboardPageProps = {
  summary: DashboardSummary | null
  filters: DashboardFilters
  options: DashboardFilterOptions
  details: DashboardDetails | null
  sellers: string[]
  state: 'idle' | 'loading' | 'ready' | 'error'
  onFiltersChange: (filters: DashboardFilters) => void
  onSubmit: () => void
  onClear: () => void
}

const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
const number = (value: number) => Number.isFinite(value) ? new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value) : '0'

function trendPoints(points: DashboardDetails['dailyTrend'], pick: (point: DashboardDetails['dailyTrend'][number]) => number) {
  if (points.length === 0) return ''
  const peak = Math.max(...points.map(pick), 1)
  return points.map((point, index) => `${points.length === 1 ? 300 : 20 + index / (points.length - 1) * 560},${220 - pick(point) / peak * 185}`).join(' ')
}

export function DashboardPage({ summary, filters, options, details, sellers, state, onFiltersChange, onSubmit, onClear }: DashboardPageProps) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
  }

  return <section className="dashboard-layout">
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
    {summary && details && state === 'ready' && summary.movementCount > 0 && details.dailyTrend.length > 0 && <section className="dashboard-charts" data-testid="dashboard-charts">
      <article className="dashboard-chart-card trend-chart"><header><div><p>Evolucao diaria</p><span>Faturamento bruto e resultado liquido</span></div><div className="chart-legend"><span><i className="gross" />Bruto</span><span><i className="net" />Liquido</span></div></header><svg viewBox="0 0 600 250" role="img" aria-label="Evolucao diaria de faturamento e resultado"><line x1="20" y1="220" x2="580" y2="220" /><line x1="20" y1="130" x2="580" y2="130" /><line x1="20" y1="40" x2="580" y2="40" /><polyline className="trend-gross" points={trendPoints(details.dailyTrend, point => point.grossSales)} /><polyline className="trend-net" points={trendPoints(details.dailyTrend, point => point.netResult)} /></svg><footer><span>{new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(new Date(`${details.dailyTrend[0].date}T12:00:00`))}</span><span>{new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(new Date(`${details.dailyTrend.at(-1)?.date}T12:00:00`))}</span></footer></article>
      <article className="dashboard-chart-card seller-chart"><header><div><p>Resultado por vendedor</p><span>Top 10 resultado liquido</span></div><i className="fa-solid fa-ranking-star" aria-hidden="true" /></header><ol>{details.sellerResults.map(result => <li key={result.seller}><span>{result.seller}</span><div><i style={{ width: `${Math.max(4, result.netResult / Math.max(...details.sellerResults.map(item => Math.abs(item.netResult)), 1) * 100)}%` }} /></div><strong>{money(result.netResult)}</strong></li>)}</ol></article>
    </section>}
  </section>
}
