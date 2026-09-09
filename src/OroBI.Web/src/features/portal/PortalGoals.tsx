import { Empty } from './PortalShared'
import { money, number, percent } from './portalFormatting'
import type { PortalGoal, PortalGoals } from './portalTypes'
import { isVisibleBrand } from '../brands/brandVisibility'
import './PortalGoals.css'

// Presentation markers for the existing backend rules, never used to calculate prizes.
const revenueTiers = [80, 90, 100]
const positivityTiers = [100]
const typeLabel = (type: string) => type === 'FATURAMENTO' ? 'Faturamento' : type === 'POSITIVACAO' ? 'Positivação' : type
const goalValue = (goal: PortalGoal, value: number) => goal.type === 'FATURAMENTO' ? money(value) : goal.type === 'POSITIVACAO' ? `${number(value)} ${value === 1 ? 'cliente' : 'clientes'}` : number(value)

function goalStatus(goal: PortalGoal) {
  const value = goal.achievedPercent
  if (value == null) return { label: 'Sem percentual', tone: 'unavailable' }
  if (value > 100) return { label: 'Acima da meta', tone: 'complete' }
  if (value === 100) return { label: 'Meta atingida', tone: 'complete' }
  if (goal.nextTierPercent != null && value < goal.nextTierPercent && goal.nextTierPercent - value <= 10)
    return { label: 'Próximo da faixa', tone: 'near' }
  return { label: 'Em andamento', tone: 'ongoing' }
}

function GoalProgress({ goal }: { goal: PortalGoal }) {
  if (goal.achievedPercent == null) return null
  const value = Math.max(0, Math.min(100, goal.achievedPercent))
  const tiers = goal.maximumPrize == null && goal.nextTierPercent == null ? []
    : goal.type === 'FATURAMENTO' ? revenueTiers : goal.type === 'POSITIVACAO' ? positivityTiers : []
  return <div className="goal-progress">
    <div className="goal-progress-track" role="progressbar" aria-label={`Meta ${goal.brand} · ${typeLabel(goal.type)}`}
      aria-valuemin={0} aria-valuemax={100} aria-valuenow={value} aria-valuetext={`${percent(goal.achievedPercent)} da meta`}>
      <span className="goal-progress-fill" style={{ width: `${value}%` }} />
      {tiers.map(tier => <span key={tier} className="goal-progress-marker" style={{ left: `${tier}%` }} aria-hidden="true" />)}
    </div>
    {tiers.length > 0 && <div className="goal-tier-row"><span>Faixas</span>
      <ol className="goal-tiers" aria-label={`Faixas de ${typeLabel(goal.type).toLocaleLowerCase('pt-BR')}`}>
        {tiers.map(tier => <li key={tier} className={tier <= goal.achievedPercent! ? 'is-reached' : tier === goal.nextTierPercent ? 'is-next' : undefined}
          aria-label={`${percent(tier)}${tier <= goal.achievedPercent! ? ' · atingida' : tier === goal.nextTierPercent ? ' · próxima faixa' : ''}`}>
          {tier <= goal.achievedPercent! && <i className="fa-solid fa-check" aria-hidden="true" />}{percent(tier)}
        </li>)}
      </ol>
    </div>}
  </div>
}

function NextThresholdCallout({ goal }: { goal: PortalGoal }) {
  if (goal.achievedPercent != null && goal.achievedPercent >= 100) return <div className="goal-next goal-next-complete">
    <i className="fa-solid fa-circle-check" aria-hidden="true" />
    <div><strong>{goal.achievedPercent > 100 ? 'Você superou a meta' : 'Você atingiu a meta'}</strong><p>100% da meta alcançada.</p></div>
  </div>
  if (goal.nextTierPercent == null) return <div className="goal-next goal-next-unavailable"><p>Próxima faixa não disponível.</p></div>
  return <div className="goal-next">
    <div>
      {goal.amountToNextTier != null ? <><span className="goal-eyebrow">{goal.type === 'POSITIVACAO' && goal.amountToNextTier === 1 ? 'Falta' : 'Faltam'}</span>
        <strong className="goal-needed">{goalValue(goal, goal.amountToNextTier)}</strong></> : <strong>Faltante não disponível</strong>}
      <p>para atingir {percent(goal.nextTierPercent)}</p>
    </div>
    <i className="fa-solid fa-arrow-trend-up" aria-hidden="true" />
  </div>
}

function PrizeSummary({ goal, approved }: { goal: PortalGoal; approved: boolean }) {
  const showNext = goal.nextTierPercent != null && goal.nextTierPrize != null && (goal.achievedPercent == null || goal.achievedPercent < 100)
  if (goal.currentPrize == null && !showNext) return null
  return <div className="goal-prizes">
    <span className={`goal-prize-kind${approved ? ' is-official' : ''}`}>{approved ? 'Prêmio oficial' : 'Prêmio estimado'}</span>
    <dl className="goal-prize-values">
      {goal.currentPrize != null && <div className={goal.currentPrize > 0 ? 'goal-prize-current has-prize' : 'goal-prize-current'}>
        <dt>{approved && goal.currentPrize > 0 ? 'Prêmio conquistado' : 'Prêmio atual'}</dt><dd>{money(goal.currentPrize)}</dd>
      </div>}
      {showNext && <div className="goal-prize-next"><dt>Prêmio ao atingir {percent(goal.nextTierPercent)}</dt><dd>{money(goal.nextTierPrize)}</dd></div>}
    </dl>
  </div>
}

function GoalCard({ goal, approved }: { goal: PortalGoal; approved: boolean }) {
  const status = goalStatus(goal)
  const currentTier = goal.achievedPercent != null && goal.nextTierPercent != null && goal.type === 'FATURAMENTO'
    ? revenueTiers.filter(tier => tier <= goal.achievedPercent!).at(-1) : undefined
  const realization = goal.type === 'FATURAMENTO' ? `${money(goal.actual)} de ${money(goal.target)}`
    : goal.type === 'POSITIVACAO' ? `${number(goal.actual)} de ${number(goal.target)} ${goal.target === 1 ? 'cliente' : 'clientes'}`
      : `${number(goal.actual)} de ${number(goal.target)}`
  return <article className={`portal-panel goal-card goal-card-${status.tone}`} aria-label={`${goal.brand} · ${typeLabel(goal.type)}`}>
    <header className="goal-card-heading"><h2>{goal.brand}</h2><span className="goal-type">{typeLabel(goal.type)}</span></header>
    <div className="goal-performance">
      <strong className={`goal-percentage${goal.achievedPercent == null ? ' is-unavailable' : ''}`}>{percent(goal.achievedPercent)}</strong>
      <span className={`goal-status goal-status-${status.tone}`}>{status.label}</span>
    </div>
    <p className="goal-realization">{realization}</p>
    <GoalProgress goal={goal} />
    {currentTier != null && <p className="goal-current-tier">Faixa atual: {percent(currentTier)}</p>}
    <NextThresholdCallout goal={goal} />
    <PrizeSummary goal={goal} approved={approved} />
  </article>
}

export function Goals({ data }: { data: PortalGoals }) {
  const items = data.items.filter(goal => isVisibleBrand(goal.brand))
  if (!items.length) return <Empty>{data.unavailableReason ?? 'Metas não disponíveis para este mês.'}</Empty>
  return <>
    {!data.available && <p className="portal-message" role="status">{data.unavailableReason ?? 'Prêmios não disponíveis para este mês.'}</p>}
    <div className="portal-goals goal-grid">{items.map((goal, index) => <GoalCard goal={goal} approved={data.isApproved === true} key={`${goal.brand}-${goal.type}-${index}`} />)}</div>
    <p className="portal-help goal-footnote"><i className={`fa-solid ${data.isApproved ? 'fa-lock' : 'fa-circle-info'}`} aria-hidden="true" />{data.isApproved ? 'Prêmios oficiais do fechamento aprovado. Valores congelados.' : 'Prêmios são estimativas até a aprovação do fechamento mensal.'}</p>
  </>
}
