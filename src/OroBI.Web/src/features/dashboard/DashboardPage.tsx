import type { FormEvent } from 'react'

export type DashboardSummary = {
  grossSales: number
  negativeMovements: number
  negativePercentage: number
  netResult: number
  saleQuantity: number
  movementCount: number
}

type DashboardPageProps = {
  summary: DashboardSummary | null
  seller: string
  sellers: string[]
  state: 'idle' | 'loading' | 'ready' | 'error'
  onSellerChange: (seller: string) => void
  onSubmit: () => void
}

const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
const number = (value: number) => Number.isFinite(value) ? new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value) : '0'

export function DashboardPage({ summary, seller, sellers, state, onSellerChange, onSubmit }: DashboardPageProps) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit()
  }

  return <section className="dashboard-layout">
    <section className="dashboard-hero">
      <div><p className="eyebrow">CENTRAL DE RESULTADOS</p><h1>Visao geral <em>da operacao.</em></h1><p>Acompanhe os indicadores comerciais consolidados.</p></div>
      <form className="dashboard-filter" onSubmit={submit}>
        <label>VENDEDOR<select value={seller} onChange={event => onSellerChange(event.target.value)}><option value="">Todos os vendedores</option>{sellers.map(registeredSeller => <option key={registeredSeller} value={registeredSeller}>{registeredSeller}</option>)}</select></label>
        <button>Aplicar recorte</button>
      </form>
    </section>
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
