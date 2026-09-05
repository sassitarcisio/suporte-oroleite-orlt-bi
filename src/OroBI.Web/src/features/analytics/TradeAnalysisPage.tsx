import { TradeDetails, type TradeGroups } from './TradeDetails'

export type TradeAnalysis = {
  groups?: TradeGroups
  filteredMovementCount: number
  grossSales: number
  netRevenue: number
  totalTradeValue: number
  tradeToRevenuePercent: number
  tradeDevValue: number
  tradeValue: number
  tradeQuantity: number
  tradeMovementCount: number
  customerCount: number
  productCount: number
  brandCount: number
  dailyTrend: Array<{ date: string, value: number }>
  sellerRanking: Array<{ name: string, value: number }>
  customerRanking: Array<{ name: string, value: number }>
  productRanking: Array<{ name: string, value: number }>
  brandRanking: Array<{ name: string, value: number }>
}

type Props = {
  mode: 'trades' | 'sales-trades'
  data: TradeAnalysis | null
  state: 'idle' | 'loading' | 'ready' | 'error'
}

const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
const numeric = (value: number) => new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)

function LineChart({ points }: { points: TradeAnalysis['dailyTrend'] }) {
  const peak = Math.max(...points.map(point => point.value), 1)
  const line = points.map((point, index) => `${points.length === 1 ? 300 : 20 + index / (points.length - 1) * 560},${220 - point.value / peak * 180}`).join(' ')
  return <svg viewBox="0 0 600 250" role="img" aria-label="Evolucao diaria de trocas"><line x1="20" y1="220" x2="580" y2="220" /><line x1="20" y1="130" x2="580" y2="130" /><line x1="20" y1="40" x2="580" y2="40" /><polyline className="trade-trend-line" points={line} /></svg>
}

function Ranking({ title, items, color }: { title: string, items: Array<{ name: string, value: number }>, color: string }) {
  const peak = Math.max(...items.map(item => item.value), 1)
  return <article className="trade-ranking"><header><h2><i className="card-label-icon fa-solid fa-ranking-star" aria-hidden="true" /> {title}</h2><span>Top 10 por valor</span></header><ol>{items.map(item => <li key={item.name}><span>{item.name}</span><div><i style={{ width: `${Math.max(4, item.value / peak * 100)}%`, backgroundColor: color }} /></div><strong>{money(item.value)}</strong></li>)}</ol></article>
}

export function TradeAnalysisPage({ mode, data, state }: Props) {
  const salesMode = mode === 'sales-trades'
  const cards = data ? salesMode
    ? [
        ['Faturamento liquido', money(data.netRevenue), 'VENDA + DEVOL ENT + DEVOLUCAO'],
        ['Valor em trocas', money(data.totalTradeValue), 'TROCA + TROCA DEV'],
        ['% troca / faturamento', `${numeric(data.tradeToRevenuePercent)}%`, 'trocas sobre faturamento liquido'],
        ['Clientes com troca', numeric(data.customerCount), 'clientes com ocorrencia de troca'],
        ['Produtos com troca', numeric(data.productCount), 'produtos improprios para consumo'],
        ['Marcas com troca', numeric(data.brandCount), 'marcas com ocorrencia de troca'],
      ]
    : [
        ['Total de trocas', money(data.totalTradeValue), 'TROCA + TROCA DEV'],
        ['% troca / venda', `${numeric(data.grossSales === 0 ? 0 : data.totalTradeValue / data.grossSales * 100)}%`, 'trocas sobre vendas brutas'],
        ['Troca DEV', money(data.tradeDevValue), 'valor classificado como TROCA DEV'],
        ['Troca', money(data.tradeValue), 'valor classificado como TROCA'],
        ['Quantidade total', numeric(data.tradeQuantity), 'unidades em TROCA + TROCA DEV'],
        ['Movimentos', numeric(data.tradeMovementCount), 'linhas classificadas como troca'],
        ['Clientes', numeric(data.customerCount), 'clientes com TROCA ou TROCA DEV'],
      ]
    : []

  const cardIcons = salesMode ? ['sack-dollar', 'arrow-right-arrow-left', 'percent', 'users', 'box-open', 'tags'] : ['arrow-right-arrow-left', 'percent', 'rotate-left', 'right-left', 'boxes-stacked', 'list-check', 'users']

  const title = salesMode ? 'Analise venda x troca' : 'Visao de trocas'
  const description = salesMode ? 'Faturamento liquido e perdas comerciais por cliente, produto e marca.' : 'Acompanhe as perdas classificadas como TROCA e TROCA DEV.'

  return <section className="trade-analysis-layout">
    <header className="analysis-header"><div><p className="eyebrow">ANALISE COMERCIAL</p><h1>{title}</h1><p>{description}</p></div></header>
    {state === 'loading' && <section className="notice">Consultando dados comerciais...</section>}
    {state === 'error' && <section className="notice error">Nao foi possivel carregar a analise.</section>}
    {data && state === 'ready' && <>
      <p className="trade-context">Base filtrada: <strong>{numeric(data.filteredMovementCount)} movimentos</strong> <span>•</span> {salesMode ? <>Faturamento liquido: <strong>{money(data.netRevenue)}</strong> <span>•</span> Trocas: <strong>{money(data.totalTradeValue)}</strong></> : <>Trocas consideradas: <strong>{numeric(data.tradeMovementCount)}</strong> <span>•</span> Vendas brutas: <strong>{money(data.grossSales)}</strong></>}</p>
      <section className={`trade-kpis trade-kpis-${cards.length}`}>{cards.map(([label, value, note], index) => <article className={index === 0 ? 'trade-kpi primary' : index < 3 ? 'trade-kpi alert' : 'trade-kpi'} key={label}><p><i className={`card-label-icon fa-solid fa-${cardIcons[index]}`} aria-hidden="true" /> {label}</p><strong className={value.startsWith('R$') ? 'compact-currency-value' : undefined}>{value}</strong><span>{note}</span></article>)}</section>
      {salesMode ? <section className="trade-ranking-grid"><Ranking title="Clientes com maior troca" items={data.customerRanking} color="var(--gold)" /><Ranking title="Produtos com maior troca" items={data.productRanking} color="var(--gold-dark)" /><Ranking title="Marcas com maior troca" items={data.brandRanking} color="var(--negative)" /></section> : <section className="trade-chart-grid"><article className="trade-trend"><header><h2><i className="card-label-icon fa-solid fa-chart-line" aria-hidden="true" /> Evolucao diaria</h2><span>Evolucao de TROCA + TROCA DEV</span></header><LineChart points={data.dailyTrend} /></article><Ranking title="Perdas por vendedor" items={data.sellerRanking} color="var(--negative)" /></section>}
    </>}
    {salesMode && <TradeDetails groups={data?.groups} ready={state === 'ready' && data !== null} />}
  </section>
}
