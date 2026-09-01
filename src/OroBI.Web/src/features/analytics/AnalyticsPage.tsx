type AnalyticsPageProps = {
  title: string
  description: string
  data: Record<string, number> | null
  state: 'idle' | 'loading' | 'ready' | 'error'
}

const labels: Record<string, string> = {
  physicalTrades: 'Trocas fisicas',
  tradeToSalesPercent: 'Trocas sobre vendas',
  tradeMovementCount: 'Movimentos de troca',
  revenue: 'Receita',
  trades: 'Trocas',
  tradeToRevenuePercent: 'Trocas sobre receita',
  cost: 'Custo',
  grossProfit: 'Lucro bruto',
  marginPercent: 'Margem',
}

const moneyKeys = new Set(['revenue', 'trades', 'cost', 'grossProfit'])
const percentKeys = new Set(['tradeToSalesPercent', 'tradeToRevenuePercent', 'marginPercent'])

function formatMetric(key: string, value: number): string {
  if (moneyKeys.has(key)) return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
  if (percentKeys.has(key)) return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value / 100) + '%'
  return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
}

export function AnalyticsPage({ title, description, data, state }: AnalyticsPageProps) {
  return <section className="hero">
    <p className="eyebrow">ANALISE COMERCIAL</p>
    <h1>{title}</h1>
    <p>{description}</p>
    {state === 'loading' && <section className="notice">Consultando dados comerciais...</section>}
    {state === 'error' && <section className="notice error">Nao foi possivel carregar a analise.</section>}
    {data && state === 'ready' && <section className="metrics">{Object.entries(data).map(([key, value]) => <article className="metric" key={key}><p>{labels[key] ?? key}</p><strong>{formatMetric(key, value)}</strong></article>)}</section>}
  </section>
}
