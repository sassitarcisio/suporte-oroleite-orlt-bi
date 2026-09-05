import { useState } from 'react'
import type { FormEvent } from 'react'
import type { ClosingSummary } from './closingTypes'
import { money, percent } from './closingFormat'
import { closingPeriodLabel, closingPeriods } from './closingPeriods'
import './ClosingStatement.css'

export type SupervisorClosingPageProps = {
  summary: ClosingSummary | null
  state: 'idle' | 'loading' | 'ready' | 'error'
  errorMessage: string | null
  initialMonth: string
  onSubmit: (seller: string, month: string) => void
}

export function SupervisorClosingPage({ summary, state, errorMessage, initialMonth, onSubmit }: SupervisorClosingPageProps) {
  const [month, setMonth] = useState(initialMonth)
  const [submitted, setSubmitted] = useState({ month: initialMonth, previousSummary: null as ClosingSummary | null })
  const pending = month !== submitted.month
  const ready = Boolean(summary && state === 'ready' && !pending && summary !== submitted.previousSummary)
  const reference = closingPeriodLabel(month)
  const detail = summary?.supervisor

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted({ month, previousSummary: summary })
    onSubmit('DEIVID MANNES', month)
  }

  return <section className="closing-statement supervisor-statement" aria-label="Demonstrativo de Deivid Mannes">
    <header className="statement-heading"><div><p className="statement-kicker">FECHAMENTO DA SUPERVISÃO</p><h1>Deivid Mannes</h1><p>Vendas próprias, equipe comercial e redes Bistek e Giassi.</p><span className="statement-reference">Referência: {reference}</span></div><button className="statement-secondary statement-print" type="button" disabled={!ready} onClick={() => { if (ready) window.print() }}><i className="fa-solid fa-print" aria-hidden="true" /> Imprimir demonstrativo</button></header>
    <form className="statement-controls" onSubmit={submit}>
      <label>VENDEDOR<select disabled value="DEIVID MANNES"><option>DEIVID MANNES</option></select></label>
      <label><span aria-hidden="true">MÊS DE REFERÊNCIA</span><select aria-label="MES" required value={month} onChange={event => setMonth(event.target.value)}><option value="" disabled>Selecione o mês</option>{closingPeriods(initialMonth).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <button type="submit" disabled={!month || state === 'loading'}>{state === 'loading' ? 'Calculando…' : 'Consultar fechamento'}</button>
    </form>
    {state === 'loading' && <p className="statement-notice" role="status">Calculando o fechamento de Deivid…</p>}
    {state === 'error' && <p className="statement-notice statement-error" role="alert">{errorMessage ?? 'Não foi possível consultar o fechamento. Tente novamente.'}</p>}
    {pending && state !== 'loading' && state !== 'error' && <p className="statement-notice" role="status">Consulte o fechamento para carregar os valores do mês selecionado.</p>}
    {state === 'idle' && !pending && <p className="statement-notice">Selecione o mês para consultar o demonstrativo.</p>}
    {ready && summary && <>
      <section className="statement-totals" aria-label="Resumo da remuneração" data-testid="closing-financial-summary">
        <article><h2>Salário-base</h2><strong>{money(summary.compensation.baseSalary)}</strong><p>Remuneração fixa</p></article>
        <article><h2>Comissões</h2><strong>{money(summary.compensation.commission)}</strong><p>Própria + equipe + redes</p></article>
        <article><h2>Prêmios no período</h2><strong>{money(summary.totalAwards)}</strong><p>Equipe + troca geral</p></article>
        <article className="statement-total"><h2>Total previsto</h2><strong>{money(summary.total)}</strong><p>Salário + comissões + prêmios</p></article>
      </section>
      {detail ? <>
        <section className="statement-primary" aria-label="Comissões por operação">
          <article className="statement-panel"><h2>Vendas próprias · 1%</h2><div className="statement-panel-body"><p className="statement-label">Comissão própria</p><strong className="statement-amount">{money(detail.ownCommission)}</strong><p>1% sobre as vendas próprias sem bonificações.</p></div></article>
          <article className="statement-panel"><h2>Equipe comercial · 0,15%</h2><div className="statement-panel-body"><p className="statement-label">Comissão da equipe</p><strong className="statement-amount">{money(detail.teamCommission)}</strong><p>0,15% sobre as vendas dos sete vendedores da equipe.</p></div></article>
          <article className="statement-panel"><h2>Redes · 0,15%</h2><div className="statement-panel-body"><p className="statement-label">Comissão Bistek e Giassi</p><strong className="statement-amount">{money(detail.networkCommission)}</strong><p>0,15% sobre as vendas das redes, sem a Operação Bauducco.</p></div></article>
        </section>
        <section className="statement-table-panel"><h2>Resumo de vendas e trocas</h2><div className="statement-table-scroll"><table aria-label="Vendas e trocas por operação"><thead><tr><th>Operação</th><th>Venda líquida</th><th>Trocas</th><th>Devoluções de troca</th><th>Total de trocas</th><th>% de trocas</th></tr></thead><tbody>{detail.operations.map(operation => <tr key={operation.key} className={operation.key === 'total' ? 'statement-consolidated' : undefined}><th scope="row">{operation.label}</th><td>{money(operation.revenue)}</td><td>{money(operation.trade)}</td><td>{money(operation.tradeReturns)}</td><td>{money(operation.totalTrades)}</td><td>{percent(operation.tradePercent)}</td></tr>)}</tbody></table></div><p className="statement-table-note">Venda líquida sem bonificações. O total consolidado reúne vendas próprias, equipe e redes sem repetir movimentos presentes em mais de uma operação.</p></section>
        <div className="statement-awards">
          <section className="statement-panel" aria-label="Critérios da média da equipe"><h2>Prêmio médio da equipe</h2><div className="statement-panel-body"><p className="statement-label">Média deste demonstrativo</p><strong className="statement-amount">{money(detail.teamAverageAward)}</strong><p>Divisão pelos sete vendedores. Paulo participa das vendas e do divisor; seu prêmio exibido é zero porque ele não integra as linhas da folha.</p><dl className="statement-fields"><div><dt>Média aplicada na folha de pagamento</dt><dd>{money(detail.payrollTeamAverageAward)}</dd></div></dl><p>A folha considera os sete prêmios calculados, incluindo o prêmio de Paulo. Por isso, as médias podem ser diferentes.</p></div></section>
          <section className="statement-panel statement-trade" aria-label="Prêmio por troca geral"><h2>Prêmio por troca geral</h2><div className="statement-panel-body"><p className="statement-label">Trocas sobre a base consolidada</p><strong className="statement-amount">{percent(summary.monthly.tradePercent)}</strong><dl className="statement-fields"><div><dt>Total de trocas</dt><dd>{money(summary.monthly.tradeValue)}</dd></div><div><dt>Prêmio</dt><dd>{money(summary.tradeAward)}</dd></div></dl><p>Até 1,25%: R$ 5.000; até 1,75%: R$ 3.000; até 2,25%: R$ 2.000; acima de 2,25%: sem prêmio. Percentual arredondado a duas casas para aplicação das faixas. O prêmio exige base líquida positiva.</p></div></section>
        </div>
        <section className="statement-table-panel"><h2>Detalhamento dos prêmios da equipe</h2><div className="statement-table-scroll"><table aria-label="Prêmios da equipe"><thead><tr><th>Vendedor</th><th>Venda líquida</th><th>Trocas</th><th>Devoluções de troca</th><th>Total de trocas</th><th>% de trocas</th><th>PPP Nestlé</th><th>Prêmio metas</th><th>Prêmio total</th></tr></thead><tbody>{detail.team.map(member => <tr key={member.seller}><th scope="row">{member.seller}{!member.includedInPayroll && <small>Fora das linhas da folha</small>}</th><td>{money(member.sales.revenue)}</td><td>{money(member.sales.trade)}</td><td>{money(member.sales.tradeReturns)}</td><td>{money(member.sales.totalTrades)}</td><td>{percent(member.sales.tradePercent)}</td><td>{money(member.pppAward)}</td><td>{money(member.goalAward)}</td><td className="statement-row-total">{money(member.totalAward)}</td></tr>)}</tbody></table></div></section>
        <p className="statement-footnote">Valores calculados com precisão integral e arredondados somente para apresentação. O prêmio da equipe segue o critério deste demonstrativo; a folha de pagamento apresenta seu próprio critério acima.</p>
      </> : <p className="statement-notice" role="status">Detalhamento da supervisão indisponível. Os totais acima correspondem ao fechamento retornado.</p>}
    </>}
  </section>
}
