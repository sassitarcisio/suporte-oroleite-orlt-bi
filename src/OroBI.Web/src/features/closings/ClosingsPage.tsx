import { useState } from 'react'
import type { FormEvent } from 'react'

export type ClosingSummary = {
  ppp: { meanPercent: number, award: number }
  revenueAward: number
  positivityAward: number
  tradeAward: number
  compensation: { commission: number, salary: number }
  totalAwards: number
}

type ClosingsPageProps = {
  summary: ClosingSummary | null
  sellers: string[]
  state: 'idle' | 'loading' | 'ready' | 'error'
  onSubmit: (seller: string, month: string) => void
}

const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
const percent = (value: number) => new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value) + '%'

export function ClosingsPage({ summary, sellers, state, onSubmit }: ClosingsPageProps) {
  const [seller, setSeller] = useState('')
  const [month, setMonth] = useState('')

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit(seller.trim(), month)
  }

  return <section className="closing-layout">
    <header className="closing-header"><p className="eyebrow">FECHAMENTO</p><h1>Fechamento por vendedor</h1><p>Consulte premios, comissao e salario no periodo selecionado.</p></header>
    <form className="closing-query-card" onSubmit={submit}>
      <div><p className="closing-query-title">Selecione o periodo</p><p className="closing-query-copy">O calculo usa as metas e regras comerciais configuradas para o vendedor.</p></div>
      <label>VENDEDOR<select required value={seller} onChange={event => setSeller(event.target.value)}><option value="" disabled>Selecione um vendedor</option>{sellers.map(registeredSeller => <option key={registeredSeller} value={registeredSeller}>{registeredSeller}</option>)}</select></label>
      <label>MES<input type="month" required value={month} onChange={event => setMonth(event.target.value)} /></label>
      <button disabled={state === 'loading'}>Consultar fechamento</button>
    </form>
    {state === 'loading' && <section className="notice">Calculando fechamento...</section>}
    {state === 'error' && <section className="closing-empty-state"><i className="fa-solid fa-sliders" aria-hidden="true" /><div><h2>Fechamento ainda nao configurado</h2><p>Faltam as regras de salario, comissao ou premio PPP para este vendedor e mes. Cadastre essas regras para liberar o calculo.</p></div></section>}
    {summary && state === 'ready' && <section className="metrics">
      <article className="metric main"><p>Premios totais</p><strong>{money(summary.totalAwards)}</strong><span>PPP, faturamento, positivacao e troca</span></article>
      <article className="metric"><p>Premio PPP</p><strong>{money(summary.ppp.award)}</strong><span>{percent(summary.ppp.meanPercent)} de media</span></article>
      <article className="metric"><p>Premio faturamento</p><strong>{money(summary.revenueAward)}</strong></article>
      <article className="metric"><p>Premio positivacao</p><strong>{money(summary.positivityAward)}</strong></article>
      <article className="metric"><p>Premio troca</p><strong>{money(summary.tradeAward)}</strong></article>
      <article className="metric"><p>Comissao</p><strong>{money(summary.compensation.commission)}</strong><span>Salario: {money(summary.compensation.salary)}</span></article>
    </section>}
  </section>
}
