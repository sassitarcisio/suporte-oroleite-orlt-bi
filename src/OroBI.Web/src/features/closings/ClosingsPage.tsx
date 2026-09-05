import { useState } from 'react'
import type { FormEvent } from 'react'
import { ClosingDetails, ClosingIndicators } from './ClosingDetails'
import { money, percent } from './closingFormat'
import type { ClosingSummary } from './closingTypes'
import { ValdirClosingPage } from './ValdirClosingPage'
import { SupervisorClosingPage } from './SupervisorClosingPage'
export type { ClosingSummary } from './closingTypes'

export type ClosingsPageProps = {
  summary: ClosingSummary | null
  sellers: string[]
  state: 'idle' | 'loading' | 'ready' | 'error'
  errorMessage: string | null
  initialSeller?: string
  initialMonth?: string
  title?: string
  onSubmit: (seller: string, month: string) => void
}

const referenceMonths = Array.from({ length: 24 }, (_, index) => {
  const date = new Date()
  date.setDate(1)
  date.setMonth(date.getMonth() - index)
  const value = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
  const label = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(date)
  return { value, label: label.charAt(0).toUpperCase() + label.slice(1) }
})

export function ClosingsPage({ summary, sellers, state, errorMessage, initialSeller = '', initialMonth = '', title = 'Fechamento por vendedor', onSubmit }: ClosingsPageProps) {
  const [seller, setSeller] = useState(initialSeller)
  const [month, setMonth] = useState(initialMonth)
  const sellerOptions = initialSeller ? [initialSeller] : sellers

  if (initialSeller === 'VALDIR ZACARIAS') {
    return <ValdirClosingPage summary={summary} state={state} errorMessage={errorMessage} initialMonth={initialMonth} onSubmit={onSubmit} months={referenceMonths} />
  }
  if (initialSeller === 'DEIVID MANNES') {
    return <SupervisorClosingPage summary={summary} state={state} errorMessage={errorMessage} initialMonth={initialMonth} onSubmit={onSubmit} />
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit(initialSeller || seller.trim(), month)
  }

  return <section className="closing-layout">
    <header className="closing-header"><p className="eyebrow">FECHAMENTO</p><h1>{title}</h1><p>Consulte premios, comissao e salario no periodo selecionado.</p></header>
    <form className="closing-query-card" onSubmit={submit}>
      <div><p className="closing-query-title">Selecione o periodo</p><p className="closing-query-copy">O calculo usa as metas e regras comerciais configuradas para o vendedor.</p></div>
      <label>VENDEDOR<select required disabled={Boolean(initialSeller)} value={initialSeller || seller} onChange={event => setSeller(event.target.value)}><option value="" disabled>Selecione um vendedor</option>{sellerOptions.map(registeredSeller => <option key={registeredSeller} value={registeredSeller}>{registeredSeller}</option>)}</select></label>
      <label>MES<select required value={month} onChange={event => setMonth(event.target.value)}><option value="" disabled>Selecione o mes</option>{referenceMonths.map(reference => <option key={reference.value} value={reference.value}>{reference.label}</option>)}</select></label>
      <button disabled={state === 'loading'}>Consultar fechamento</button>
    </form>
    {state === 'loading' && <section className="notice">Calculando fechamento...</section>}
    {state === 'error' && <section className="closing-empty-state"><i className="fa-solid fa-triangle-exclamation" aria-hidden="true" /><div><h2><i className="card-label-icon fa-solid fa-circle-info" aria-hidden="true" /> Nao foi possivel consultar o fechamento</h2><p>{errorMessage ?? 'A consulta de fechamento falhou. Tente novamente em alguns instantes.'}</p></div></section>}
    {summary && state === 'ready' && <>
      <section className="closing-financial-summary" data-testid="closing-financial-summary">
        <article><p><i className="card-label-icon fa-solid fa-calculator" aria-hidden="true" /> Salario + comissao</p><strong>{money(summary.compensation.totalSalary)}</strong><span>Salário-base: {money(summary.compensation.baseSalary)}</span><span>Comissão: {money(summary.compensation.commission)}</span></article>
        <article><p><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Premios no periodo</p><strong>{money(summary.totalAwards)}</strong><span>PPP, faturamento, positivacao e troca</span></article>
        <article><p><i className="card-label-icon fa-solid fa-money-check-dollar" aria-hidden="true" /> Total previsto</p><strong>{money(summary.total)}</strong><span>Salario, comissao e premios</span></article>
      </section>
      {summary.monthly.scope === 'company-excluding-bauducco' && <section className="closing-general-trade-award" aria-labelledby="general-trade-award-title">
        <h2 id="general-trade-award-title"><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Prêmio por troca geral</h2>
        <dl>
          <div><dt>% de trocas</dt><dd>{percent(summary.monthly.tradePercent)}</dd></div>
          <div><dt>Valor das trocas</dt><dd>{money(summary.monthly.tradeValue)}</dd></div>
          <div><dt>Prêmio</dt><dd>{money(summary.tradeAward)}</dd></div>
        </dl>
      </section>}
      <ClosingIndicators monthly={summary.monthly} />
      <section className="metrics">
      <article className="metric main"><p><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Premios totais</p><strong>{money(summary.totalAwards)}</strong><span>PPP, faturamento, positivacao e troca</span></article>
      <article className="metric"><p><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Premio PPP</p><strong>{money(summary.ppp.award)}</strong><span>{percent(summary.ppp.meanPercent)} de media</span></article>
      <article className="metric"><p><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> {summary.monthly.scope === 'company' ? 'Prêmio da equipe' : 'Premio faturamento'}</p><strong>{money(summary.revenueAward)}</strong></article>
      <article className="metric"><p><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Premio positivacao</p><strong>{money(summary.positivityAward)}</strong></article>
      <article className="metric"><p><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Premio troca</p><strong>{money(summary.tradeAward)}</strong></article>
      <article className="metric"><p><i className="card-label-icon fa-solid fa-calculator" aria-hidden="true" /> Comissao</p><strong>{money(summary.compensation.commission)}</strong><span>Salario: {money(summary.compensation.baseSalary)}</span></article>
    </section>
    <ClosingDetails summary={summary} />
    </>}
  </section>
}
