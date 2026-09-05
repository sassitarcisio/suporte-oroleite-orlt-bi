import { useState } from 'react'
import type { FormEvent } from 'react'
import type { PayrollClosing } from './closingTypes'
import { money, percent } from './closingFormat'
import { closingPeriodLabel, closingPeriods } from './closingPeriods'
import './ClosingStatement.css'

export type PayrollClosingPageProps = {
  summary: PayrollClosing | null
  state: 'idle' | 'loading' | 'ready' | 'error'
  errorMessage: string | null
  initialMonth: string
  onSubmit: (month: string, coverageSeller: string) => void
  onExport: () => void
  exporting?: boolean
}

const defaultCoverageSellers = ['ANDERSON GONCALVES SOUZA', 'MARCELO IVONEI DA ROSA', 'MARCIO FERNANDES', 'MARCIO LUIZ DA ROSA', 'RAMON DO NASCIMENTO', 'RODRIGO']

export function PayrollClosingPage({ summary, state, errorMessage, initialMonth, onSubmit, onExport, exporting = false }: PayrollClosingPageProps) {
  const [month, setMonth] = useState(initialMonth)
  const [coverage, setCoverage] = useState(summary?.coverageSeller ?? 'MARCIO LUIZ DA ROSA')
  const [submitted, setSubmitted] = useState({ month: initialMonth, coverage, previousSummary: null as PayrollClosing | null })
  const pending = month !== submitted.month || coverage !== submitted.coverage
  const matches = summary && `${summary.year}-${String(summary.month).padStart(2, '0')}` === month && summary.coverageSeller === coverage
  const ready = state === 'ready' && !pending && matches && summary !== submitted.previousSummary
  const reference = closingPeriodLabel(month)
  const coverageSellers = Array.from(new Set([...(summary?.coverageSellers ?? defaultCoverageSellers), coverage]))

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted({ month, coverage, previousSummary: summary })
    onSubmit(month, coverage)
  }

  return <section className="closing-statement payroll-statement" aria-label="Fechamento para folha de pagamento">
    <header className="statement-heading"><div><p className="statement-kicker">RECURSOS HUMANOS · FECHAMENTO MENSAL</p><h1>Fechamento para folha de pagamento</h1><p>Salários, comissões e incentivos por vendedor, prontos para conferência.</p><span className="statement-reference">Referência: {reference}</span></div></header>
    <form className="statement-controls" onSubmit={submit}>
      <label><span aria-hidden="true">MÊS DE REFERÊNCIA</span><select aria-label="MES" required value={month} onChange={event => setMonth(event.target.value)}><option value="" disabled>Selecione o mês</option>{closingPeriods(initialMonth).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <label><span>Cobertura de férias do Tiago</span><select value={coverage} onChange={event => setCoverage(event.target.value)}>{coverageSellers.map(seller => <option key={seller}>{seller}</option>)}</select></label>
      <button type="submit" disabled={!month || state === 'loading'}>{state === 'loading' ? 'Calculando…' : 'Consultar fechamento'}</button>
      <button className="statement-secondary" type="button" disabled={!ready || exporting} onClick={() => { if (ready && !exporting) onExport() }}><i className="fa-solid fa-file-excel" aria-hidden="true" /> {exporting ? 'Exportando…' : 'Exportar Excel'}</button>
    </form>
    {state === 'loading' && <p className="statement-notice" role="status">Calculando a folha de pagamento…</p>}
    {state === 'error' && <p className="statement-notice statement-error" role="alert">{errorMessage ?? 'Não foi possível consultar a folha de pagamento. Tente novamente.'}</p>}
    {pending && state !== 'loading' && state !== 'error' && <p className="statement-notice" role="status">Consulte novamente para carregar o mês e a cobertura selecionados.</p>}
    {state === 'idle' && !pending && <p className="statement-notice">Selecione o mês e consulte a folha de pagamento.</p>}
    {ready && summary && <>
      <section className="statement-totals payroll-totals" aria-label="Resumo da folha">
        <article><h2>Vendedores</h2><strong>{summary.sellerCount}</strong><p>Participantes da folha</p></article>
        <article><h2>Faturamento / base</h2><strong className="statement-text-value">Não consolidar</strong><p>As bases podem se sobrepor</p></article>
        <article><h2>Comissões</h2><strong>{money(summary.totalCommission)}</strong><p>Total do período</p></article>
        <article><h2>Incentivos</h2><strong>{money(summary.totalIncentives)}</strong><p>PPP, metas e troca</p></article>
        <article className="statement-total"><h2>Total da folha</h2><strong>{money(summary.total)}</strong><p>Salários + comissões + incentivos</p></article>
      </section>
      <section className="statement-table-panel"><h2>Demonstrativo por vendedor</h2><div className="statement-table-scroll"><table className="payroll-table" aria-label="Fechamento para folha de pagamento">
        <thead><tr><th>Vendedor</th><th>Mês / referência</th><th>Faturamento</th><th>Salário-base</th><th>Comissão %</th><th>Comissão</th><th>PPP Nestlé</th><th>Prêmio metas / equipe</th><th>Incentivos / prêmio troca</th><th>Total</th></tr></thead>
        <tbody>{summary.rows.map(row => <tr key={row.seller}><th scope="row">{row.seller}{row.seller.split(' ')[0] === 'TIAGO' && row.sourceSeller !== row.seller && <small>Cobertura: {row.sourceSeller}</small>}</th><td className="statement-reference-cell">{reference}<small>{row.reference}</small></td><td>{money(row.revenue)}</td><td>{money(row.baseSalary)}</td><td>{row.commissionPercent === null ? 'Conforme regra' : percent(row.commissionPercent)}</td><td>{money(row.commission)}</td><td>{money(row.pppAward)}</td><td>{money(row.goalAward)}</td><td>{money(row.incentives)}</td><td className="statement-row-total">{money(row.total)}</td></tr>)}</tbody>
        <tfoot><tr><th scope="row" colSpan={2}>Total da folha</th><td>Não consolidar</td><td>{money(summary.totalBaseSalary)}</td><td>—</td><td>{money(summary.totalCommission)}</td><td>{money(summary.totalPppAward)}</td><td>{money(summary.totalGoalAward)}</td><td>{money(summary.totalIncentives)}</td><td>{money(summary.total)}</td></tr></tfoot>
      </table></div></section>
      <section className="statement-rules" aria-label="Critérios da folha de pagamento">
        <article><h2>Base de faturamento</h2><p>O faturamento é informado por vendedor e não é somado: supervisão, cobertura de férias e fechamento geral podem compartilhar vendas.</p></article>
        <article><h2>Cobertura de férias</h2><p>Tiago utiliza o faturamento, a comissão e os prêmios de {summary.coverageSeller}. O salário-base é o salário padrão da folha.</p></article>
        <article><h2>Prêmio da supervisão</h2><p>Na folha, a média de Deivid considera os prêmios calculados dos sete vendedores, incluindo Paulo. No demonstrativo da supervisão, Paulo participa das vendas e do divisor, mas seu prêmio exibido é zero por não integrar as linhas da folha.</p></article>
      </section>
      <p className="statement-footnote">Os totais são calculados com a precisão integral dos valores. O arredondamento ocorre apenas na exibição, por isso a soma dos valores visíveis pode apresentar diferença de centavos. Incentivos = PPP + metas/equipe + prêmio de troca.</p>
    </>}
  </section>
}
