import { useState, type ReactNode } from 'react'
import './DashboardBreakdowns.css'

export type DashboardGroupRow = { label: string, netResult: number, grossSales: number, negativeMovements: number, quantity: number, movementCount: number, documentCount: number }
export type DashboardGroups = Partial<Record<Dimension, DashboardGroupRow[]>>
const dimensions = { seller: 'Vendedor', brand: 'Marca', customer: 'Cliente', group: 'Grupo / rede', product: 'Produto', city: 'Cidade', movementType: 'Tipo de movimento', family: 'Família', date: 'Data' }
const metrics = { netResult: 'Valor líquido', grossSales: 'Vendas brutas', negativeMovements: 'Movimentos negativos', quantity: 'Quantidade', movementCount: 'Linhas', documentCount: 'Documentos' }
type Dimension = keyof typeof dimensions
type Metric = keyof typeof metrics
const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const number = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 })
const format = (value: number, metric: Metric) => (['quantity', 'movementCount', 'documentCount'].includes(metric) ? number : money).format(value)
const order = (rows: DashboardGroupRow[], metric: Metric, limit: number) => [...rows].sort((a, b) => b[metric] - a[metric] || a.label.localeCompare(b.label, 'pt-BR')).slice(0, limit)
const empty = 'Nenhum movimento para os filtros selecionados.'

function Panel({ title, icon, note, children }: { title: string, icon: string, note: string, children: ReactNode }) {
  return <article className="dashboard-chart-card dashboard-breakdown-panel"><header><h2><i className={`card-label-icon fa-solid fa-${icon}`} aria-hidden="true" /> {title}</h2><span>{note}</span></header>{children}</article>
}

function Bars({ rows, metric, title }: { rows: DashboardGroupRow[], metric: Metric, title: string }) {
  if (!rows.length) return <p className="dashboard-breakdown-empty">{empty}</p>
  const min = Math.min(0, ...rows.map(row => row[metric]))
  const max = Math.max(0, ...rows.map(row => row[metric]))
  const range = max - min || 1
  const zero = -min / range * 100
  return <ol className="dashboard-result-bars" aria-label={title}>{rows.map(row => <li key={row.label} className={row[metric] < 0 ? 'is-negative' : undefined}>
    <div className="dashboard-result-caption"><span title={row.label}>{row.label}</span><strong>{format(row[metric], metric)}</strong></div>
    <div className="dashboard-result-track" aria-hidden="true"><span className="dashboard-result-zero" style={{ left: `${zero}%` }} /><i style={{ left: `${row[metric] < 0 ? zero + row[metric] / range * 100 : zero}%`, width: `${Math.abs(row[metric]) / range * 100}%` }} /></div>
  </li>)}</ol>
}

export function DashboardBreakdowns({ groups, ready }: { groups?: DashboardGroups, ready: boolean }) {
  const [draft, setDraft] = useState<{ dimension: Dimension, metric: Metric, limit: number }>({ dimension: 'seller', metric: 'netResult', limit: 10 })
  const [selection, setSelection] = useState(draft)
  if (!ready) return null
  if (!groups) return <p className="notice">Detalhamento dos gráficos indisponível. Aplique os filtros para consultar novamente.</p>
  const rows = order(groups[selection.dimension] ?? [], selection.metric, selection.limit)
  return <section className="dashboard-breakdowns" aria-label="Análises detalhadas do dashboard">
    <div className="dashboard-breakdown-grid">
      <Panel title="Resultado por marca" icon="tags" note="Top 10 · valor líquido"><Bars title="Resultado por marca" rows={order(groups.brand ?? [], 'netResult', 10)} metric="netResult" /></Panel>
      <Panel title="Tipos de movimento" icon="arrow-right-arrow-left" note="Valor líquido por tipo"><Bars title="Tipos de movimento" rows={order(groups.movementType ?? [], 'netResult', Infinity)} metric="netResult" /></Panel>
      <Panel title="Top clientes" icon="users" note="Top 10 · valor líquido"><Bars title="Top clientes" rows={order(groups.customer ?? [], 'netResult', 10)} metric="netResult" /></Panel>
    </div>
    <div className="dashboard-dynamic-grid">
      <Panel title="Análise dinâmica" icon="chart-column" note="Escolha como agrupar">
        <form className="dashboard-dynamic-controls" onSubmit={event => { event.preventDefault(); setSelection(draft) }}>
          <label>Agrupar análise do dashboard<select value={draft.dimension} onChange={event => setDraft({ ...draft, dimension: event.target.value as Dimension })}>{Object.entries(dimensions).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
          <label>Métrica do dashboard<select value={draft.metric} onChange={event => setDraft({ ...draft, metric: event.target.value as Metric })}>{Object.entries(metrics).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
          <label>Limite do dashboard<select value={draft.limit} onChange={event => setDraft({ ...draft, limit: Number(event.target.value) })}>{[10, 15, 25, 50].map(limit => <option key={limit} value={limit}>Top {limit}</option>)}</select></label>
          <button type="submit" aria-label="Atualizar análise do dashboard">Atualizar</button>
        </form>
        <Bars title="Análise dinâmica do dashboard" rows={rows} metric={selection.metric} />
      </Panel>
      <Panel title="Ranking dinâmico" icon="ranking-star" note={`${rows.length} posições · ${metrics[selection.metric]}`}>
        <div className="dashboard-ranking-scroll" tabIndex={0} role="region" aria-label="Ranking com rolagem"><table aria-label="Ranking dinâmico do dashboard"><thead><tr><th>#</th><th>{dimensions[selection.dimension]}</th><th>{metrics[selection.metric]}</th></tr></thead><tbody>{rows.map((row, index) => <tr key={row.label}><td>{index + 1}</td><th scope="row" title={row.label}>{row.label}</th><td className={row[selection.metric] < 0 ? 'is-negative' : undefined}>{format(row[selection.metric], selection.metric)}</td></tr>)}{!rows.length && <tr><td colSpan={3}>{empty}</td></tr>}</tbody></table></div>
      </Panel>
    </div>
  </section>
}
