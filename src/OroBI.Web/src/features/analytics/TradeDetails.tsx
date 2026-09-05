import { useState } from 'react'
import './TradeDetails.css'

export type TradeDetailRow = { label: string; netRevenue: number; tradeValue: number; tradePercent: number | null; tradeQuantity: number }
const dimensions = { customer: 'cliente', group: 'rede', product: 'produto', brand: 'marca', seller: 'vendedor', city: 'cidade' }
export type TradeGroups = Partial<Record<keyof typeof dimensions, TradeDetailRow[]>>
type Order = 'tradeValue' | 'tradePercent' | 'netRevenue'
const money = (value: number) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })

export function TradeDetails({ groups, ready }: { groups?: TradeGroups; ready: boolean }) {
  const [draft, setDraft] = useState({ dimension: 'customer' as keyof typeof dimensions, order: 'tradeValue' as Order, limit: 20 })
  const [selection, setSelection] = useState(draft)
  if (!ready) return null
  const rows = [...(groups?.[selection.dimension] ?? [])].filter(row => row.tradeValue > 0)
    .sort((a, b) => (b[selection.order] ?? -Infinity) - (a[selection.order] ?? -Infinity) || a.label.localeCompare(b.label, 'pt-BR'))
    .slice(0, selection.limit)

  return <article className="trade-ranking trade-details">
    <header><h2><i className="card-label-icon fa-solid fa-table-list" aria-hidden="true" /> Análise dinâmica de Venda × Troca</h2><span>Faturamento, troca e percentual</span></header>
    <form className="trade-detail-controls" onSubmit={event => { event.preventDefault(); setSelection(draft) }}>
      <label>Agrupamento<select aria-label="Agrupar venda e troca" value={draft.dimension} onChange={event => setDraft({ ...draft, dimension: event.target.value as keyof typeof dimensions })}>{Object.entries(dimensions).map(([value, label]) => <option key={value} value={value}>Por {label}</option>)}</select></label>
      <label>Ordenação<select aria-label="Ordenar venda e troca" value={draft.order} onChange={event => setDraft({ ...draft, order: event.target.value as Order })}><option value="tradeValue">Maior valor em troca</option><option value="tradePercent">Maior % de troca</option><option value="netRevenue">Maior faturamento</option></select></label>
      <label>Quantidade<select aria-label="Limite de venda e troca" value={draft.limit} onChange={event => setDraft({ ...draft, limit: Number(event.target.value) })}>{[10, 20, 50].map(value => <option key={value} value={value}>Top {value}</option>)}</select></label>
      <button type="submit">Atualizar</button>
    </form>
    {!groups ? <p>Detalhamento indisponível nesta consulta.</p> : rows.length === 0 ? <p>Sem trocas para os filtros selecionados.</p> : <div className="trade-detail-scroll" tabIndex={0} role="region" aria-label="Detalhamento de venda e troca">
      <table aria-label="Análise dinâmica de Venda × Troca"><thead><tr><th scope="col">#</th><th scope="col">{dimensions[selection.dimension]}</th><th scope="col">Faturamento líquido</th><th scope="col">Trocas</th><th scope="col">% troca</th><th scope="col">Qtde. troca</th></tr></thead>
        <tbody>{rows.map((row, index) => <tr key={row.label}><td>{index + 1}</td><td title={row.label}>{row.label}</td><td>{money(row.netRevenue)}</td><td>{money(row.tradeValue)}</td><td className="trade-detail-rate">{row.tradePercent === null ? '—' : `${row.tradePercent.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`}</td><td>{row.tradeQuantity.toLocaleString('pt-BR', { maximumFractionDigits: 2 })}</td></tr>)}</tbody>
      </table>
    </div>}
    <p className="trade-detail-explanation">Faturamento líquido: VENDA + DEVOL ENT + DEVOLUCAO, respeitando os sinais. Trocas e quantidade: soma absoluta de TROCA e TROCA DEV. Percentual: trocas ÷ faturamento líquido do agrupamento; sem base positiva, exibimos —.</p>
  </article>
}
