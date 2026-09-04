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
  grossSales: 'Vendas brutas',
  returns: 'Devolucoes',
  netSales: 'Venda liquida',
  netCost: 'Custo liquido',
  tradeLosses: 'Perdas em trocas',
  boletoDiscounts: 'Desconto de boleto',
  liquidProfit: 'Lucro liquido',
  liquidMarginPercent: 'Margem liquida',
  productCount: 'Produtos',
}

const moneyKeys = new Set(['revenue', 'trades', 'cost', 'grossProfit', 'grossSales', 'returns', 'netSales', 'netCost', 'tradeLosses', 'boletoDiscounts', 'liquidProfit'])
const percentKeys = new Set(['tradeToSalesPercent', 'tradeToRevenuePercent', 'marginPercent', 'liquidMarginPercent'])
const icons: Record<string, string> = {
  physicalTrades: 'fa-arrow-right-arrow-left',
  tradeToSalesPercent: 'fa-chart-pie',
  tradeMovementCount: 'fa-receipt',
  revenue: 'fa-sack-dollar',
  trades: 'fa-arrow-right-arrow-left',
  tradeToRevenuePercent: 'fa-scale-balanced',
  cost: 'fa-boxes-stacked',
  grossProfit: 'fa-chart-line',
  marginPercent: 'fa-percent',
}

function formatMetric(key: string, value: number): string {
  if (moneyKeys.has(key)) return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
  if (percentKeys.has(key)) return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value / 100) + '%'
  return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
}

export function AnalyticsPage({ title, description, data, state }: AnalyticsPageProps) {
  const metrics = data ? Object.entries(data) : []

  return <section className="analysis-layout">
    <header className="analysis-header"><div><p className="eyebrow">ANALISE COMERCIAL</p><h1>{title}</h1><p>{description}</p></div>{state === 'ready' && <p className="analysis-status"><i className="fa-solid fa-circle-check" aria-hidden="true" /> Dados consolidados</p>}</header>
    {state === 'loading' && <section className="notice">Consultando dados comerciais...</section>}
    {state === 'error' && <section className="notice error">Nao foi possivel carregar a analise.</section>}
    {data && state === 'ready' && <section className={`analysis-metrics analysis-metrics-${metrics.length}`}>{metrics.map(([key, value], index) => <article className={`analysis-card ${index === 0 ? 'primary' : ''}`} key={key}><div className="analysis-card-top"><span>0{index + 1}</span><i className={`fa-solid ${icons[key] ?? 'fa-chart-simple'}`} aria-hidden="true" /></div><p>{labels[key] ?? key}</p><strong>{formatMetric(key, value)}</strong><span className="analysis-card-note">Indicador consolidado</span></article>)}</section>}
  </section>
}
