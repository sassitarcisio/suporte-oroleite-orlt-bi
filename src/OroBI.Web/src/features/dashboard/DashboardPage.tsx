import { useState, type FormEvent } from 'react'
import { DashboardBreakdowns, type DashboardGroups } from './DashboardBreakdowns'

export type DashboardSummary = {
  grossSales: number
  negativeMovements: number
  negativePercent?: number
  negativePercentage?: number
  netResult: number
  saleQuantity: number
  movementCount: number
  customerCount: number
  documentCount: number
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
  groups?: DashboardGroups
  dailyTrend: Array<{ date: string, grossSales: number, netResult: number, negativeMovements: number }>
  sellerResults: Array<{ seller: string, netResult: number }>
}

type DashboardPageProps = {
  summary: DashboardSummary | null
  filters: DashboardFilters
  appliedFilters?: DashboardFilters
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
  const values = points.flatMap(point => [point.grossSales, point.netResult, point.negativeMovements])
  const min = Math.min(0, ...values)
  const range = Math.max(0, ...values) - min || 1
  return points.map((point, index) => `${points.length === 1 ? 300 : 20 + index / (points.length - 1) * 560},${220 - (pick(point) - min) / range * 185}`).join(' ')
}

export function DashboardPage({ summary, filters, appliedFilters = filters, options, details, sellers, state, onFiltersChange, onSubmit, onClear }: DashboardPageProps) {
  const [filtersOpen, setFiltersOpen] = useState(false)
  const activeFilters = [appliedFilters.startDate, appliedFilters.endDate, appliedFilters.seller, appliedFilters.brand, appliedFilters.group, appliedFilters.city, appliedFilters.customerContains, appliedFilters.productContains, appliedFilters.movementType].filter(Boolean).length

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
    setFiltersOpen(false)
  }

  return <section className="dashboard-layout">
    <section className="dashboard-filter-bar" aria-label="Resumo de filtros">
      <div><p>RECORTE ATUAL</p><strong>{activeFilters === 0 ? 'Visao consolidada' : `${activeFilters} filtro${activeFilters === 1 ? '' : 's'} aplicado${activeFilters === 1 ? '' : 's'}`}</strong></div>
      <button type="button" aria-expanded={filtersOpen} aria-controls="dashboard-filter-panel" onClick={() => setFiltersOpen(open => !open)}><i className="fa-solid fa-sliders" aria-hidden="true" /> Filtros <i className={`fa-solid fa-chevron-${filtersOpen ? 'up' : 'down'}`} aria-hidden="true" /></button>
    </section>
    {filtersOpen && <form className="dashboard-filter" id="dashboard-filter-panel" onSubmit={submit}>
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
      <div className="dashboard-filter-actions"><button type="submit">Aplicar filtros</button><button className="dashboard-filter-clear" type="button" onClick={() => { onClear(); setFiltersOpen(false) }}>Limpar filtros</button></div>
    </form>}
    {state === 'loading' && <section className="notice">Consultando dados comerciais...</section>}
    {state === 'error' && <section className="notice error">Nao foi possivel carregar a API.</section>}
    {summary && state === 'ready' && summary.movementCount === 0 && <section className="notice">Nenhum movimento encontrado para os filtros aplicados.</section>}
    {summary && state === 'ready' && summary.movementCount > 0 && <section className="dashboard-metrics" data-testid="dashboard-metrics">
      <article className="dashboard-kpi primary"><p><i className="fa-solid fa-sack-dollar" aria-hidden="true" /> Faturamento bruto</p><strong className="currency-value">{money(summary.grossSales)}</strong><span>Somente movimentos de venda</span></article>
      <article className="dashboard-kpi negative"><p><i className="fa-solid fa-arrow-trend-down" aria-hidden="true" /> Movimentos negativos</p><strong className="currency-value">{money(summary.negativeMovements)}</strong><span>Devolucoes, trocas e descontos</span></article>
      <article className="dashboard-kpi petrol"><p><i className="fa-solid fa-chart-line" aria-hidden="true" /> Resultado liquido</p><strong className="currency-value">{money(summary.netResult)}</strong><span>Soma de todos os movimentos</span></article>
      <article className="dashboard-kpi percentage"><p><i className="fa-solid fa-percent" aria-hidden="true" /> % mov. negativos</p><strong>{number(summary.negativePercent ?? summary.negativePercentage ?? 0)}%</strong><span>Negativos sobre vendas brutas</span></article>
      <article className="dashboard-kpi neutral"><p><i className="fa-solid fa-boxes-stacked" aria-hidden="true" /> Quantidade venda</p><strong>{number(summary.saleQuantity)}</strong><span>Registros classificados como venda</span></article>
      <article className="dashboard-kpi neutral"><p><i className="fa-solid fa-users" aria-hidden="true" /> Clientes</p><strong>{number(summary.customerCount)}</strong><span>Clientes distintos no filtro</span></article>
      <article className="dashboard-kpi neutral"><p><i className="fa-solid fa-file-lines" aria-hidden="true" /> Documentos</p><strong>{number(summary.documentCount)}</strong><span>Documentos distintos no filtro</span></article>
    </section>}
    {summary && details && state === 'ready' && summary.movementCount > 0 && details.dailyTrend.length > 0 && <section className="dashboard-charts" data-testid="dashboard-charts">
      <article className="dashboard-chart-card trend-chart"><header><div><p><i className="card-label-icon fa-solid fa-chart-line" aria-hidden="true" /> Evolucao diaria</p><span>Vendas, liquido e movimentos negativos</span></div><div className="chart-legend"><span><i className="gross" />Bruto</span><span><i className="net" />Liquido</span><span><i className="negative" />Negativos</span></div></header><svg viewBox="0 0 600 250" role="img" aria-label="Evolucao diaria de faturamento, resultado e movimentos negativos"><line x1="20" y1="220" x2="580" y2="220" /><line x1="20" y1="130" x2="580" y2="130" /><line x1="20" y1="40" x2="580" y2="40" /><polyline className="trend-gross" points={trendPoints(details.dailyTrend, point => point.grossSales)} /><polyline className="trend-net" points={trendPoints(details.dailyTrend, point => point.netResult)} /><polyline className="trend-negative" points={trendPoints(details.dailyTrend, point => point.negativeMovements)} /></svg><footer><span>{new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(new Date(`${details.dailyTrend[0].date}T12:00:00`))}</span><span>{new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short' }).format(new Date(`${details.dailyTrend.at(-1)?.date}T12:00:00`))}</span></footer></article>
      <article className="dashboard-chart-card seller-chart"><header><div><p>Resultado por vendedor</p><span>Top 10 resultado liquido</span></div><i className="fa-solid fa-ranking-star" aria-hidden="true" /></header><ol>{details.sellerResults.map(result => <li key={result.seller}><span>{result.seller}</span><div><i style={{ width: `${Math.max(4, result.netResult / Math.max(...details.sellerResults.map(item => Math.abs(item.netResult)), 1) * 100)}%` }} /></div><strong>{money(result.netResult)}</strong></li>)}</ol></article>
    </section>}
    <DashboardBreakdowns groups={details?.groups} ready={state === 'ready' && summary !== null && summary.movementCount > 0} />
  </section>
}
