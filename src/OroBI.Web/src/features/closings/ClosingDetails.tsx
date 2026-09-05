import type { ClosingSummary } from './closingTypes'
import { money, percent } from './closingFormat'
const number = (value: number) => new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(value)
const date = (value: string) => value.split('-').reverse().join('/')

export function ClosingIndicators({ monthly }: Pick<ClosingSummary, 'monthly'>) {
  return <section aria-label="Indicadores do mês" className="closing-details">
    <h2>Indicadores do mês</h2>
    <p>{monthly.scope === 'company-excluding-bauducco' ? 'Empresa, exceto Operação Bauducco. Comissão de 0,10% sobre o faturamento líquido com bonificações; percentual de troca calculado sem bonificações.' : monthly.scope === 'company' ? 'Empresa sem bonificações. A comissão de Deivid combina vendas próprias, equipe e redes Bistek/Giassi.' : 'Movimentos do vendedor no mês selecionado.'}</p>
    <div className="metrics">
      <article className="metric"><p>{monthly.scope === 'company-excluding-bauducco' ? 'Base da comissão' : 'Faturamento do mês'}</p><strong>{money(monthly.scope === 'company-excluding-bauducco' ? monthly.commissionableRevenue : monthly.revenue)}</strong><span>Soma líquida dos movimentos do escopo</span></article>
      <article className="metric"><p>Faturamento sem bonificações</p><strong>{money(monthly.tradeRevenueBase ?? monthly.commissionableRevenue)}</strong><span>Base do percentual consolidado de troca</span></article>
      <article className="metric"><p>Trocas</p><strong>{money(monthly.tradeValue)}</strong><span>{percent(monthly.tradePercent)} do faturamento sem bonificações</span></article>
      <article className="metric"><p>Documentos identificados</p><strong>{number(monthly.documentCount)}</strong><span>Itens agrupados por documento</span></article>
      <article className="metric"><p>Clientes</p><strong>{number(monthly.customerCount)}</strong><span>Clientes identificados nos movimentos</span></article>
      <article className="metric"><p>Movimentos</p><strong>{number(monthly.movementCount)}</strong><span>Registros no período</span></article>
    </div>
  </section>
}

export function ClosingDetails({ summary }: { summary: ClosingSummary }) {
  return <>
    <section className="closing-details">
      <h2>Segmentos PPP</h2>
      {summary.pppSegments.length === 0 ? <p>Nenhum segmento PPP no período.</p> : <div className="closing-table-scroll"><table aria-label="Segmentos PPP">
        <thead><tr><th>Segmento</th><th>Clientes</th><th>Itens por segmento</th><th>Grupos colocados</th><th>Realizado</th></tr></thead>
        <tbody>{summary.pppSegments.map((segment, index) => <tr key={`${segment.segment}-${index}`}><th scope="row">{segment.segment}</th><td>{number(segment.customerCount)}</td><td>{number(segment.itemsPerSegment)}</td><td>{number(segment.groupsPlaced)}</td><td>{segment.achievementPercent === null ? 'Sem base' : percent(segment.achievementPercent)}</td></tr>)}</tbody>
      </table></div>}
      <p>Média PPP: {percent(summary.ppp.meanPercent)} · Prêmio: {money(summary.ppp.award)}. Segmentos sem base não entram na média.</p>
    </section>
    <section className="closing-details">
      <h2>Premios por marca</h2>
      {summary.brandAwards.length > 0 && <p>A taxa de troca por marca usa o faturamento da marca com bonificações. A taxa consolidada usa o faturamento sem bonificações.</p>}
      {summary.brandAwards.length === 0 ? <p>Nenhuma meta por marca no período.</p> : <div className="closing-table-scroll"><table aria-label="Metas e prêmios por marca">
        <thead><tr><th>Marca</th><th>Indicador</th><th>Meta</th><th>Realizado</th><th>Atingimento / taxa</th><th>Prêmio previsto</th><th>Prêmio apurado</th></tr></thead>
        <tbody>{summary.brandAwards.flatMap(brand => [
          <tr key={`${brand.brand}-revenue`}><th scope="row">{brand.brand}</th><td>Faturamento</td><td>{money(brand.revenueGoal)}</td><td>{money(brand.revenueActual)}</td><td>{percent(brand.revenueAchievedPercent)}</td><td>{money(brand.revenuePrize)}</td><td>{money(brand.revenueAward)}</td></tr>,
          <tr key={`${brand.brand}-positivity`}><th scope="row">{brand.brand}</th><td>Positivação</td><td>{number(brand.positivityGoal)}</td><td>{number(brand.positivityActual)}</td><td>{percent(brand.positivityAchievedPercent)}</td><td>{money(brand.positivityPrize)}</td><td>{money(brand.positivityAward)}</td></tr>,
          <tr key={`${brand.brand}-trade`}><th scope="row">{brand.brand}</th><td>Troca</td><td>{percent(brand.tradeGoalPercent)}</td><td>{money(brand.tradeValue)}</td><td>{percent(brand.tradeActualPercent)}</td><td>{money(brand.tradePrize)}</td><td>{money(brand.tradeAward)}</td></tr>,
          <tr className="closing-brand-total" key={`${brand.brand}-total`}><th scope="row" colSpan={6}>Total {brand.brand}</th><td>{money(brand.totalAward)}</td></tr>,
        ])}</tbody>
      </table></div>}
    </section>
    <section className="closing-details">
      <h2>Documentos do mês</h2>
      {summary.monthly.documents.length === 0 ? <p>Nenhum documento identificado no período.</p> : <details><summary>Ver {number(summary.monthly.documentCount)} documentos</summary><div className="closing-table-scroll"><table aria-label="Documentos do mês">
        <thead><tr><th>Data</th><th>Documento</th><th>Vendedor</th><th>Cliente</th><th>Movimento</th><th>Valor</th></tr></thead>
        <tbody>{summary.monthly.documents.map((document, index) => <tr key={index}><td>{date(document.date)}</td><th scope="row">{document.documentNumber}</th><td>{document.seller}</td><td>{document.customerName || document.customerCode}</td><td>{document.movementType}</td><td>{money(document.totalValue)}</td></tr>)}</tbody>
      </table></div></details>}
    </section>
  </>
}
