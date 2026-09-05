import { useId, useState, type FormEvent } from 'react'
import type { DashboardFilterOptions, DashboardFilters } from '../dashboard/DashboardPage'
import type { MarginDimension, MarginReport, MarginRow, NetMarginDimension, NetMarginReport, NetMarginRow } from './marginTypes'
import './MarginAnalysis.css'

type Props = {
  mode: 'products' | 'net'
  data: MarginReport | NetMarginReport | null
  state: 'idle' | 'loading' | 'ready' | 'error'
  filters: DashboardFilters
  options: DashboardFilterOptions
  sellers: string[]
  onSubmit: (filters: DashboardFilters) => void
}
const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const decimal = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 })
const percent = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const safe = (value: number | null | undefined) => typeof value === 'number' && Number.isFinite(value) ? value : 0
const money = (value: number | null | undefined) => currency.format(safe(value))
const number = (value: number | null | undefined) => decimal.format(safe(value))
const pct = (value: number | null | undefined) => value == null ? '—' : `${percent.format(safe(value))}%`
const negative = (value: number | null | undefined) => safe(value) < 0 ? 'is-negative' : undefined
const dimensionNames: Record<NetMarginDimension, string> = { customer: 'Cliente', product: 'Produto', brand: 'Marca', seller: 'Vendedor', group: 'Grupo / rede', city: 'Cidade' }
const emptyText = 'Nenhum resultado para os filtros aplicados.'

function ranked<T extends { label: string }>(rows: T[], metric: keyof T, limit: number): T[] {
  return [...rows].sort((a, b) => {
    const av = typeof a[metric] === 'number' ? a[metric] as number : -Infinity
    const bv = typeof b[metric] === 'number' ? b[metric] as number : -Infinity
    return bv - av || a.label.localeCompare(b.label, 'pt-BR')
  }).slice(0, limit)
}

function Kpi({ label, value, hint, kind = 'money', deduction = false }: { label: string, value: number | undefined, hint: string, kind?: 'money' | 'percent' | 'number', deduction?: boolean }) {
  return <article className={`margin-kpi ${deduction ? 'margin-deduction' : ''}`}><p>{label}</p><strong className={negative(value)}>{kind === 'percent' ? pct(value ?? 0) : kind === 'number' ? number(value) : money(value)}</strong><span>{hint}</span></article>
}

function BarChart({ title, rows, format = money }: { title: string, rows: { label: string, value: number }[], format?: (value: number) => string }) {
  const min = Math.min(0, ...rows.map(row => safe(row.value)))
  const max = Math.max(0, ...rows.map(row => safe(row.value)))
  const range = max - min || 1
  const zero = -min / range * 100
  return rows.length === 0 ? <p className="margin-empty">{emptyText}</p> : <ol className="margin-bars" aria-label={title}>{rows.map((row, index) => <li key={`${row.label}-${index}`} className={negative(row.value)}>
    <div className="margin-bar-caption"><span title={row.label}>{index + 1}. <span>{row.label}</span></span><strong>{format(row.value)}</strong></div>
    <div className="margin-bar-track" aria-hidden="true"><i className="margin-bar-zero" style={{ left: `${zero}%` }} /><i className="margin-bar-fill" style={{ left: `${safe(row.value) < 0 ? zero + safe(row.value) / range * 100 : zero}%`, width: `${Math.abs(safe(row.value)) / range * 100}%` }} /></div>
  </li>)}</ol>
}

function Limits({ label, value, values, onChange }: { label: string, value: number, values: number[], onChange: (value: number) => void }) {
  return <label>{label}<select value={value} onChange={event => onChange(Number(event.target.value))}>{values.map(limit => <option key={limit} value={limit}>Top {limit}</option>)}</select></label>
}

function GrossAnalysis({ data, ready }: { data: MarginReport | null, ready: boolean }) {
  const [draft, setDraft] = useState<{ dimension: MarginDimension, order: keyof MarginRow, limit: number }>({ dimension: 'customer', order: 'grossProfit', limit: 20 })
  const [selection, setSelection] = useState(draft)
  if (!ready || !data) return null
  const allRows = data.groups?.[selection.dimension] ?? []
  const rows = ranked(allRows, selection.order, selection.limit)
  return <>
    <section className="margin-metrics margin-metrics-gross" data-testid="margin-metrics" aria-label="Indicadores de margem bruta">
      <Kpi label="Faturamento" value={data.revenue} hint="Somente movimentos VENDA" />
      <Kpi label="Custo" value={data.cost} hint="Quantidade × custo unitário" deduction />
      <Kpi label="Lucro bruto" value={data.grossProfit} hint="Faturamento menos custo" />
      <Kpi label="Margem bruta" value={data.marginPercent} hint="Lucro bruto / faturamento" kind="percent" />
      <Kpi label="Clientes" value={data.customerCount} hint="Clientes distintos no filtro" kind="number" />
      <Kpi label="Produtos" value={data.productCount} hint="Produtos distintos no filtro" kind="number" />
    </section>
    <section className="margin-charts" aria-label="Maiores lucros brutos">{(['customer', 'product', 'brand'] as const).map(dimension => <article className="margin-panel" key={dimension}><header><h2>Lucro por {dimensionNames[dimension].toLowerCase()}</h2><span>Top 10 · lucro bruto</span></header><BarChart title={`Lucro bruto por ${dimensionNames[dimension]}`} rows={ranked(data.groups?.[dimension] ?? [], 'grossProfit', 10).map(row => ({ label: row.label, value: row.grossProfit }))} /></article>)}</section>
    <section className="margin-detail-grid">
      <article className="margin-panel"><header><h2>Detalhamento de margem</h2><span>{rows.length} de {allRows.length} resultados</span></header>
        <form className="margin-controls" onSubmit={event => { event.preventDefault(); setSelection(draft) }}>
          <label>Agrupar detalhamento<select value={draft.dimension} onChange={event => setDraft({ ...draft, dimension: event.target.value as MarginDimension })}>{(['customer', 'product', 'brand'] as const).map(dimension => <option key={dimension} value={dimension}>{dimensionNames[dimension]}</option>)}</select></label>
          <label>Ordenar detalhamento<select value={draft.order} onChange={event => setDraft({ ...draft, order: event.target.value as keyof MarginRow })}><option value="grossProfit">Maior lucro bruto</option><option value="marginPercent">Maior margem %</option><option value="revenue">Maior faturamento</option></select></label>
          <Limits label="Limite do detalhamento" value={draft.limit} values={[20, 50, 100]} onChange={limit => setDraft({ ...draft, limit })} /><button type="submit" aria-label="Atualizar detalhamento">Atualizar</button>
        </form>
        <div className="margin-table-wrap" tabIndex={0} role="region" aria-label="Tabela de margem com rolagem horizontal"><table aria-label="Detalhamento de margem"><thead><tr><th>{dimensionNames[selection.dimension]}</th><th>Faturamento</th><th>Custo</th><th>Lucro bruto</th><th>Margem %</th><th>Quantidade</th></tr></thead><tbody>{rows.map((row, index) => <tr key={`${row.label}-${index}`}><th scope="row" title={row.label}>{row.label}</th><td>{money(row.revenue)}</td><td>{money(row.cost)}</td><td className={negative(row.grossProfit)}>{money(row.grossProfit)}</td><td className={negative(row.marginPercent)}>{pct(row.marginPercent)}</td><td>{number(row.quantity)}</td></tr>)}{rows.length === 0 && <tr><td colSpan={6}>{emptyText}</td></tr>}</tbody></table></div>
      </article>
      <aside className="margin-panel margin-explanation"><h2>Como a margem é calculada</h2><p><strong>Faturamento</strong> considera somente movimentos do tipo VENDA.</p><p><strong>Custo</strong> soma a quantidade × custo unitário de cada venda.</p><p><strong>Lucro bruto</strong> = faturamento − custo.</p><p><strong>Margem bruta</strong> = lucro bruto ÷ faturamento × 100.</p><p>Uma margem sem faturamento é exibida como — no detalhamento.</p></aside>
    </section>
  </>
}

type NetMetric = 'liquidProfit' | 'netSales' | 'grossSales' | 'losses' | 'quantity' | 'movementCount'
const metricNames: Record<NetMetric, string> = { liquidProfit: 'Lucro líquido', netSales: 'Venda líquida', grossSales: 'Vendas brutas', losses: 'Perdas', quantity: 'Quantidade', movementCount: 'Movimentos' }

function NetAnalysis({ data, ready }: { data: NetMarginReport | null, ready: boolean }) {
  const [detailDraft, setDetailDraft] = useState<{ order: keyof NetMarginRow, limit: number }>({ order: 'liquidProfit', limit: 20 })
  const [detail, setDetail] = useState(detailDraft)
  const [draft, setDraft] = useState<{ dimension: NetMarginDimension, metric: NetMetric, limit: number }>({ dimension: 'seller', metric: 'liquidProfit', limit: 10 })
  const [selection, setSelection] = useState(draft)
  if (!ready || !data) return null
  const products = data.groups?.product ?? []
  const productRows = ranked(products, detail.order, detail.limit)
  const groups = data.groups?.[selection.dimension] ?? []
  const rows = ranked(groups, selection.metric, selection.limit)
  const format = selection.metric === 'quantity' || selection.metric === 'movementCount' ? number : money
  return <>
    <p className="margin-status">Venda líquida: <strong>{money(data.netSales)}</strong><span> · {number(data.movementCount)} movimentos · {number(data.quantity)} unidades</span></p>
    <section className="margin-metrics margin-metrics-net" data-testid="margin-metrics" aria-label="Indicadores de margem líquida">
      <Kpi label="Vendas brutas" value={data.grossSales} hint="Somente movimentos VENDA" />
      <Kpi label="Devoluções" value={data.returns} hint="Reduzem a venda e devolvem o custo" deduction />
      <Kpi label="Custo líquido" value={data.netCost} hint="Custo de venda menos retorno ao estoque" deduction />
      <Kpi label="Perda em trocas" value={data.tradeLosses} hint="Custo de TROCA e TROCA DEV" deduction />
      <Kpi label="Desconto de boleto" value={data.boletoDiscounts} hint="Abatimento financeiro" deduction />
      <Kpi label="Lucro líquido" value={data.liquidProfit} hint="Venda líquida − custo líquido − perdas" />
      <Kpi label="Margem líquida" value={data.liquidMarginPercent} hint="Lucro líquido / venda líquida" kind="percent" />
      <Kpi label="Produtos" value={data.productCount} hint="Com movimento no filtro" kind="number" />
    </section>
    <article className="margin-panel margin-product-detail"><header><h2>Margem líquida por produto</h2><span>{productRows.length} de {products.length} produtos · trocas pelo custo</span></header>
      <form className="margin-controls" onSubmit={event => { event.preventDefault(); setDetail(detailDraft) }}><label>Ordenar produtos<select value={detailDraft.order} onChange={event => setDetailDraft({ ...detailDraft, order: event.target.value as keyof NetMarginRow })}><option value="liquidProfit">Maior lucro líquido</option><option value="liquidMarginPercent">Maior margem líquida %</option><option value="losses">Maior perda</option><option value="grossSales">Maior venda</option></select></label><Limits label="Limite de produtos" value={detailDraft.limit} values={[20, 50, 100]} onChange={limit => setDetailDraft({ ...detailDraft, limit })} /><button type="submit" aria-label="Atualizar produtos">Atualizar</button></form>
      <div className="margin-table-wrap" tabIndex={0} role="region" aria-label="Tabela de produtos com rolagem horizontal"><table aria-label="Margem líquida por produto"><thead><tr><th>Produto</th><th>Vendas</th><th title="DEVOLUCAO">Devol. própria</th><th title="DEVOL ENT">Devol. cliente</th><th>Custo líquido</th><th>Trocas (custo)</th><th>Desc. boleto</th><th>Lucro líquido</th><th>Margem %</th></tr></thead><tbody>{productRows.map((row, index) => <tr key={`${row.label}-${index}`}><th scope="row" title={row.label}>{row.label}</th><td>{money(row.grossSales)}</td><td>{money(row.ownReturns)}</td><td>{money(row.customerReturns)}</td><td>{money(row.netCost)}</td><td>{money(row.tradeLosses)}</td><td>{money(row.boletoDiscounts)}</td><td className={negative(row.liquidProfit)}>{money(row.liquidProfit)}</td><td className={negative(row.liquidMarginPercent)}>{pct(row.liquidMarginPercent)}</td></tr>)}{productRows.length === 0 && <tr><td colSpan={9}>{emptyText}</td></tr>}</tbody></table></div>
    </article>
    <section className="margin-dynamic-grid"><article className="margin-panel"><header><h2>Análise dinâmica</h2><span>Escolha como agrupar</span></header>
      <form className="margin-controls" onSubmit={event => { event.preventDefault(); setSelection(draft) }}><label>Agrupar análise<select value={draft.dimension} onChange={event => setDraft({ ...draft, dimension: event.target.value as NetMarginDimension })}>{(['seller', 'brand', 'customer', 'group', 'product', 'city'] as const).map(dimension => <option key={dimension} value={dimension}>{dimensionNames[dimension]}</option>)}</select></label><label>Métrica da análise<select value={draft.metric} onChange={event => setDraft({ ...draft, metric: event.target.value as NetMetric })}>{(Object.keys(metricNames) as NetMetric[]).map(metric => <option key={metric} value={metric}>{metricNames[metric]}</option>)}</select></label><Limits label="Limite da análise" value={draft.limit} values={[10, 15, 25, 50]} onChange={limit => setDraft({ ...draft, limit })} /><button type="submit" aria-label="Atualizar análise">Atualizar</button></form>
      <BarChart title={`${metricNames[selection.metric]} por ${dimensionNames[selection.dimension]}`} rows={rows.map(row => ({ label: row.label, value: row[selection.metric] }))} format={format} />
    </article><article className="margin-panel"><header><h2>Ranking dinâmico</h2><span>{dimensionNames[selection.dimension]} · {rows.length} de {groups.length} resultados</span></header><div className="margin-table-wrap" tabIndex={0} role="region" aria-label="Ranking com rolagem horizontal"><table aria-label="Ranking dinâmico"><thead><tr><th>#</th><th>{dimensionNames[selection.dimension]}</th><th>{metricNames[selection.metric]}</th></tr></thead><tbody>{rows.map((row, index) => <tr key={`${row.label}-${index}`}><td>{index + 1}</td><th scope="row" title={row.label}>{row.label}</th><td className={negative(row[selection.metric])}>{format(row[selection.metric])}</td></tr>)}{rows.length === 0 && <tr><td colSpan={3}>{emptyText}</td></tr>}</tbody></table></div></article></section>
  </>
}

export function MarginAnalysisPage({ mode, data, state, filters, options, sellers, onSubmit }: Props) {
  const [draft, setDraft] = useState(filters)
  const listId = useId()
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); onSubmit({ ...draft, movementType: '' }) }
  return <section className="margin-analysis" aria-label={mode === 'net' ? 'Análise de margem líquida' : 'Análise de margem bruta'}>
    <div className="margin-rule"><h1>{mode === 'net' ? 'Margem líquida por produto' : 'Margem bruta'}</h1><span>{mode === 'net' ? 'Venda líquida − custo líquido − perdas' : 'Faturamento − custo dos produtos vendidos'}</span><p>{mode === 'net' ? 'Devoluções reduzem a venda e retornam o custo ao estoque. Perdas incluem TROCA, TROCA DEV e DESC BOLETO.' : 'A análise considera somente movimentos do tipo VENDA.'}</p></div>
    <form className="margin-filter margin-panel" onSubmit={submit} aria-label="Filtros de margem">
      <label>Data inicial<input type="date" value={draft.startDate} onChange={event => setDraft({ ...draft, startDate: event.target.value })} /></label><label>Data final<input type="date" value={draft.endDate} min={draft.startDate || undefined} onChange={event => setDraft({ ...draft, endDate: event.target.value })} /></label>
      <label>Vendedor<select value={draft.seller} onChange={event => setDraft({ ...draft, seller: event.target.value })}><option value="">Todos os vendedores</option>{sellers.map(seller => <option key={seller}>{seller}</option>)}</select></label>
      <label>Marca<select value={draft.brand} onChange={event => setDraft({ ...draft, brand: event.target.value })}><option value="">Todas as marcas</option>{options.brands.map(brand => <option key={brand}>{brand}</option>)}</select></label>
      <label>Grupo / rede<input list={`${listId}-groups`} value={draft.group} placeholder="Todos os grupos" onChange={event => setDraft({ ...draft, group: event.target.value })} /></label><datalist id={`${listId}-groups`}>{options.groups.map(group => <option key={group} value={group} />)}</datalist>
      <label>Cidade<select value={draft.city} onChange={event => setDraft({ ...draft, city: event.target.value })}><option value="">Todas as cidades</option>{options.cities.map(city => <option key={city}>{city}</option>)}</select></label>
      <label>Cliente contém<input value={draft.customerContains} placeholder="Nome do cliente" onChange={event => setDraft({ ...draft, customerContains: event.target.value })} /></label><label>Produto contém<input value={draft.productContains} placeholder="Nome do produto" onChange={event => setDraft({ ...draft, productContains: event.target.value })} /></label>
      <div className="margin-filter-actions"><button type="submit" disabled={state === 'loading'}>Aplicar filtros</button><button type="button" className="margin-secondary" disabled={state === 'loading'} onClick={() => { const cleared = { ...draft, startDate: '', endDate: '', seller: '', brand: '', group: '', city: '', customerContains: '', productContains: '', movementType: '' }; setDraft(cleared); onSubmit(cleared) }}>Limpar filtros</button></div>
    </form>
    {state === 'loading' && <p className="margin-status" role="status">Consultando dados de margem...</p>}
    {state === 'error' && <p className="margin-status is-negative" role="alert">Não foi possível carregar a análise. Aplique os filtros para tentar novamente.</p>}
    {(state === 'idle' || (state === 'ready' && !data)) && <p className="margin-status" role="status">Aplique os filtros para consultar a margem.</p>}
    {mode === 'net' ? <NetAnalysis data={data as NetMarginReport | null} ready={state === 'ready'} /> : <GrossAnalysis data={data as MarginReport | null} ready={state === 'ready'} />}
  </section>
}
