import { useState } from 'react'
import type { FormEvent } from 'react'

export type ClosingSummary = {
  ppp: { meanPercent: number, award: number }
  revenueAward: number
  positivityAward: number
  tradeAward: number
  compensation: { commission: number, salary: number }
  totalAwards: number
  brandAwards?: Array<{ brand: string, positivityAward: number, revenueAward: number, tradeAward: number, totalAward: number }>
}

type ClosingsPageProps = {
  summary: ClosingSummary | null
  sellers: string[]
  state: 'idle' | 'loading' | 'ready' | 'error'
  errorMessage: string | null
  onSubmit: (seller: string, month: string) => void
}

const money = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value)
const percent = (value: number) => new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value) + '%'
const referenceMonths = Array.from({ length: 24 }, (_, index) => {
  const date = new Date()
  date.setDate(1)
  date.setMonth(date.getMonth() - index)
  const value = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
  const label = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(date)
  return { value, label: label.charAt(0).toUpperCase() + label.slice(1) }
})

export function ClosingsPage({ summary, sellers, state, errorMessage, onSubmit }: ClosingsPageProps) {
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
      <label>MES<select required value={month} onChange={event => setMonth(event.target.value)}><option value="" disabled>Selecione o mes</option>{referenceMonths.map(reference => <option key={reference.value} value={reference.value}>{reference.label}</option>)}</select></label>
      <button disabled={state === 'loading'}>Consultar fechamento</button>
    </form>
    {state === 'loading' && <section className="notice">Calculando fechamento...</section>}
    {state === 'error' && <section className="closing-empty-state"><i className="fa-solid fa-triangle-exclamation" aria-hidden="true" /><div><h2>Nao foi possivel consultar o fechamento</h2><p>{errorMessage ?? 'A consulta de fechamento falhou. Tente novamente em alguns instantes.'}</p></div></section>}
    {summary && state === 'ready' && <><section className="closing-financial-summary" data-testid="closing-financial-summary"><article><p>Salario + comissao</p><strong>{money(summary.compensation.salary)}</strong><span>Comissao: {money(summary.compensation.commission)}</span></article><article><p>Premios no periodo</p><strong>{money(summary.totalAwards)}</strong><span>PPP, faturamento, positivacao e troca</span></article><article><p>Total previsto</p><strong>{money(summary.compensation.salary + summary.totalAwards)}</strong><span>Salario, comissao e premios</span></article></section><section className="metrics">
      <article className="metric main"><p>Premios totais</p><strong>{money(summary.totalAwards)}</strong><span>PPP, faturamento, positivacao e troca</span></article>
      <article className="metric"><p>Premio PPP</p><strong>{money(summary.ppp.award)}</strong><span>{percent(summary.ppp.meanPercent)} de media</span></article>
      <article className="metric"><p>Premio faturamento</p><strong>{money(summary.revenueAward)}</strong></article>
      <article className="metric"><p>Premio positivacao</p><strong>{money(summary.positivityAward)}</strong></article>
      <article className="metric"><p>Premio troca</p><strong>{money(summary.tradeAward)}</strong></article>
      <article className="metric"><p>Comissao</p><strong>{money(summary.compensation.commission)}</strong><span>Salario: {money(summary.compensation.salary)}</span></article>
    </section>{summary.brandAwards && summary.brandAwards.length > 0 && <section className="closing-brand-awards"><h2>Premios por marca</h2><div className="closing-brand-awards-table"><div className="closing-brand-awards-head"><span>Marca</span><span>Faturamento</span><span>Positivacao</span><span>Troca</span><span>Total</span></div>{summary.brandAwards.map(award => <div className="closing-brand-awards-row" key={award.brand}><strong>{award.brand}</strong><span>{money(award.revenueAward)}</span><span>{money(award.positivityAward)}</span><span>{money(award.tradeAward)}</span><strong>{money(award.totalAward)}</strong></div>)}</div></section>}</>}
  </section>
}
