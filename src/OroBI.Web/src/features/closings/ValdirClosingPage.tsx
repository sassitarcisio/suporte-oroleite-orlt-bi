import { useState } from 'react'
import type { FormEvent } from 'react'
import type { ClosingsPageProps } from './ClosingsPage'
import { ClosingDocuments } from './ClosingDetails'
import { money, percent } from './closingFormat'
import './ValdirClosingPage.css'

type Props = Pick<ClosingsPageProps, 'summary' | 'state' | 'errorMessage' | 'initialMonth' | 'onSubmit'> & {
  months: Array<{ value: string, label: string }>
}
const number = (value: number) => new Intl.NumberFormat('pt-BR').format(value)

export function ValdirClosingPage({ summary, state, errorMessage, initialMonth = '', onSubmit, months }: Props) {
  const [month, setMonth] = useState(initialMonth)
  const [submittedMonth, setSubmittedMonth] = useState(initialMonth)
  const pendingMonth = month !== submittedMonth
  const ready = Boolean(summary && state === 'ready' && !pendingMonth)
  const reference = months.find(item => item.value === submittedMonth)?.label ?? 'Período consultado'

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmittedMonth(month)
    onSubmit('VALDIR ZACARIAS', month)
  }

  return <section className="valdir-closing" aria-label="Demonstrativo de Valdir Zacarias">
    <header className="valdir-heading">
      <div><p className="valdir-kicker">FECHAMENTO GERAL</p><h1>Valdir Zacarias</h1><p>Remuneração sobre o faturamento líquido da empresa, sem a Operação Bauducco.</p><span className="valdir-reference">Referência: {reference}</span></div>
      <button className="valdir-print" type="button" disabled={!ready} onClick={() => window.print()}><i className="fa-solid fa-print" aria-hidden="true" /> Imprimir demonstrativo</button>
    </header>
    <form className="valdir-controls" onSubmit={submit}>
      <label>VENDEDOR<select disabled value="VALDIR ZACARIAS"><option>VALDIR ZACARIAS</option></select></label>
      <label><span aria-hidden="true">MÊS DE REFERÊNCIA</span><select aria-label="MES" required value={month} onChange={event => setMonth(event.target.value)}><option value="" disabled>Selecione o mês</option>{months.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <button type="submit" disabled={state === 'loading'}>{state === 'loading' ? 'Calculando…' : 'Consultar fechamento'}</button>
    </form>
    {state === 'loading' && <p className="valdir-notice" role="status">Calculando o fechamento de Valdir…</p>}
    {state === 'error' && <p className="valdir-notice valdir-error" role="alert">{errorMessage ?? 'Não foi possível consultar o fechamento. Tente novamente.'}</p>}
    {pendingMonth && state !== 'loading' && <p className="valdir-notice" role="status">Consulte o fechamento para carregar os valores do mês selecionado.</p>}
    {state === 'idle' && !pendingMonth && <p className="valdir-notice">Selecione o mês para consultar o demonstrativo.</p>}
    {ready && summary && <>
      <div className="valdir-primary">
        <section className="valdir-panel" aria-labelledby="valdir-salary-title">
          <h2 id="valdir-salary-title"><i className="card-label-icon fa-solid fa-wallet" aria-hidden="true" /> Salário-base</h2>
          <div className="valdir-panel-body"><p className="valdir-label">Remuneração fixa</p><strong className="valdir-amount">{money(summary.compensation.baseSalary)}</strong><p className="valdir-note">Salário-base utilizado no fechamento.</p><span className="valdir-period">{reference}</span></div>
        </section>
        <section className="valdir-panel" aria-labelledby="valdir-commission-title">
          <h2 id="valdir-commission-title"><i className="card-label-icon fa-solid fa-calculator" aria-hidden="true" /> Comissão · 0,10%</h2>
          <div className="valdir-panel-body"><p className="valdir-label">Base da comissão</p><strong className="valdir-amount">{money(summary.monthly.commissionableRevenue)}</strong><p className="valdir-note">Faturamento líquido com bonificações.</p><dl className="valdir-fields"><div><dt>Percentual</dt><dd>0,10%</dd></div><div><dt>Comissão</dt><dd>{money(summary.compensation.commission)}</dd></div></dl></div>
        </section>
        <section className="valdir-panel valdir-trade" aria-labelledby="valdir-trade-title">
          <h2 id="valdir-trade-title"><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Prêmio por troca geral</h2>
          <div className="valdir-panel-body"><p className="valdir-label">% de trocas</p><div className="valdir-trade-rate"><strong className="valdir-amount">{percent(summary.monthly.tradePercent)}</strong><span>{summary.tradeAward > 0 ? 'Com prêmio' : 'Sem prêmio'}</span></div><p className="valdir-note">Sobre o faturamento sem bonificações.</p><dl className="valdir-fields"><div><dt>Valor das trocas</dt><dd>{money(summary.monthly.tradeValue)}</dd></div><div><dt>Prêmio</dt><dd>{money(summary.tradeAward)}</dd></div></dl></div>
        </section>
      </div>
      <section className="valdir-totals" aria-label="Resumo da remuneração" data-testid="closing-financial-summary">
        <article><h2><i className="card-label-icon fa-solid fa-calculator" aria-hidden="true" /> Salário + comissão</h2><strong>{money(summary.compensation.totalSalary)}</strong><p>Remuneração sem prêmio</p></article>
        <article><h2><i className="card-label-icon fa-solid fa-trophy" aria-hidden="true" /> Prêmios no período</h2><strong>{money(summary.totalAwards)}</strong><p>Prêmio por troca geral</p></article>
        <article className="valdir-total"><h2><i className="card-label-icon fa-solid fa-money-check-dollar" aria-hidden="true" /> Total previsto</h2><strong>{money(summary.total)}</strong><p>Salário + comissão + prêmio</p></article>
        <article><h2><i className="card-label-icon fa-solid fa-filter-circle-xmark" aria-hidden="true" /> Base excluída</h2><strong className="valdir-exclusion">Operação Bauducco</strong><p>Fora do faturamento e das trocas</p></article>
      </section>
      <section className="valdir-sales" aria-labelledby="valdir-sales-title">
        <h2 id="valdir-sales-title"><i className="card-label-icon fa-solid fa-arrow-right-arrow-left" aria-hidden="true" /> Resumo geral de vendas e trocas</h2>
        <div className="valdir-table-scroll"><table aria-label="Resumo geral de vendas e trocas"><thead><tr><th>Base</th><th>Venda líquida sem bonificações</th><th>Bonificações</th><th>Total de trocas</th><th>% de trocas</th></tr></thead><tbody><tr><th scope="row">Empresa sem Operação Bauducco</th><td>{money(summary.monthly.tradeRevenueBase ?? summary.monthly.commissionableRevenue)}</td><td>{money(summary.monthly.revenue - (summary.monthly.tradeRevenueBase ?? summary.monthly.commissionableRevenue))}</td><td>{money(summary.monthly.tradeValue)}</td><td className={summary.tradeAward > 0 ? 'valdir-rate-earned' : 'valdir-rate-unearned'}>{percent(summary.monthly.tradePercent)}</td></tr></tbody></table></div>
      </section>
      <section className="valdir-rules" aria-label="Regras do fechamento">
        <article><h2><i className="fa-solid fa-calculator" aria-hidden="true" /> Comissão</h2><p>0,10% sobre o faturamento líquido com bonificações. A Operação Bauducco fica fora de toda a base.</p></article>
        <article><h2><i className="fa-solid fa-arrow-right-arrow-left" aria-hidden="true" /> Base de troca</h2><p>Trocas e devoluções de troca sobre a venda líquida sem bonificações, com os movimentos negativos abatidos.</p></article>
        <article className="valdir-award-rule"><h2><i className="fa-solid fa-award" aria-hidden="true" /> Faixas do prêmio</h2><dl><div><dt>Até 2,00%</dt><dd>R$ 5.000,00</dd></div><div><dt>Acima de 2,00% até 3,00%</dt><dd>R$ 3.000,00</dd></div><div><dt>Acima de 3,00% até 4,00%</dt><dd>R$ 2.000,00</dd></div><div><dt>Acima de 4,00%</dt><dd>Sem prêmio</dd></div></dl></article>
      </section>
      <section className="valdir-counts" aria-label="Indicadores do mês"><span><strong>{number(summary.monthly.documentCount)}</strong> documentos identificados</span><span><strong>{number(summary.monthly.customerCount)}</strong> clientes</span><span><strong>{number(summary.monthly.movementCount)}</strong> movimentos no mês</span></section>
      <div className="valdir-documents"><ClosingDocuments monthly={summary.monthly} /></div>
    </>}
  </section>
}
