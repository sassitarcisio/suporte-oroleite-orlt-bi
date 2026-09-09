import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Goals } from './PortalResults'
import type { PortalGoal, PortalGoals } from './portalTypes'

const revenue: PortalGoal = { brand: 'AÇAÍ FUTURO', type: 'FATURAMENTO', target: 1500, actual: 330.8, achievedPercent: 22.05333333333333, maximumPrize: 100, currentPrize: 0, nextTierPercent: 80, amountToNextTier: 869.2, nextTierPrize: 50 }
const positivity: PortalGoal = { ...revenue, type: 'POSITIVACAO', target: 15, actual: 4, achievedPercent: 26.666666666666668, nextTierPercent: 100, amountToNextTier: 11, nextTierPrize: 100 }
const data = (items: PortalGoal[], isApproved = false): PortalGoals => ({ year: 2026, month: 8, available: true, unavailableReason: null, isApproved, items })

describe('Goal performance cards', () => {
  it('presents the reference values naturally without recomputing the API amounts', () => {
    const input = data([positivity, revenue])
    const before = JSON.stringify(input)
    Object.freeze(input.items); input.items.forEach(Object.freeze)
    render(<Goals data={input} />)
    const customers = within(screen.getByRole('article', { name: 'AÇAÍ FUTURO · Positivação' }))
    expect(customers.getByText('26,67%')).toBeVisible()
    expect(customers.getByText('4 de 15 clientes')).toBeVisible()
    expect(customers.getByText('11 clientes')).toBeVisible()
    expect(customers.getByText('Prêmio atual')).toBeVisible()
    expect(customers.getByText(/100,00/)).toBeVisible()
    const sales = within(screen.getByRole('article', { name: 'AÇAÍ FUTURO · Faturamento' }))
    expect(sales.getByText('22,05%')).toBeVisible()
    expect(sales.getByText(/R\$\s*330,80 de R\$\s*1\.500,00/)).toBeVisible()
    expect(sales.getByText(/869,20/)).toBeVisible()
    expect(sales.getByText(/50,00/)).toBeVisible()
    expect(JSON.stringify(input)).toBe(before)
  })

  it('uses the supplied next-tier amount and prize even when they differ from a local calculation', () => {
    render(<Goals data={data([{ ...revenue, amountToNextTier: 812.34, nextTierPrize: 47.89 }])} />)
    expect(screen.getByText(/812,34/)).toBeVisible()
    expect(screen.getByText(/47,89/)).toBeVisible()
    expect(screen.queryByText(/869,20/)).not.toBeInTheDocument()
  })

  it.each([
    [22.05, 80, 'Em andamento'],
    [70, 80, 'Próximo da faixa'],
    [87.4, 90, 'Próximo da faixa'],
    [100, null, 'Meta atingida'],
    [125.6, null, 'Acima da meta'],
    [null, null, 'Sem percentual'],
  ])('labels progress %s using existing percentages', (achievedPercent, nextTierPercent, label) => {
    render(<Goals data={data([{ ...revenue, achievedPercent, nextTierPercent }])} />)
    expect(screen.getByText(label)).toBeVisible()
  })

  it('keeps the exact percentage accessible and bounds only the visual progress', () => {
    const { rerender } = render(<Goals data={data([revenue])} />)
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', String(revenue.achievedPercent))
    rerender(<Goals data={data([{ ...revenue, achievedPercent: 125.6, nextTierPercent: null }])} />)
    expect(screen.getByText('125,6%')).toBeVisible()
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100')
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuetext', '125,6% da meta')
    expect(screen.queryByText('Faltam')).not.toBeInTheDocument()
  })

  it('shows the current and next bands between revenue tiers', () => {
    render(<Goals data={data([{ ...revenue, achievedPercent: 87.4, currentPrize: 50, nextTierPercent: 90, amountToNextTier: 39, nextTierPrize: 75 }])} />)
    expect(screen.getByText('Faixa atual: 80%')).toBeVisible()
    expect(screen.getByText('para atingir 90%')).toBeVisible()
    expect(screen.getByText('Prêmio ao atingir 90%')).toBeVisible()
    expect(screen.getByRole('list', { name: 'Faixas de faturamento' })).toBeVisible()
  })

  it('does not label an unapproved prize as already conquered', () => {
    render(<Goals data={data([{ ...revenue, achievedPercent: 100, currentPrize: 100, nextTierPercent: null }])} />)
    expect(screen.getByText('Prêmio atual')).toBeVisible()
    expect(screen.getByText('Prêmio estimado')).toBeVisible()
    expect(screen.queryByText('Prêmio conquistado')).not.toBeInTheDocument()
    expect(screen.getByText(/estimativas até a aprovação/)).toBeVisible()
  })

  it('highlights approved prizes without changing their frozen values', () => {
    render(<Goals data={data([{ ...revenue, achievedPercent: 100, currentPrize: 91.23, nextTierPercent: null }], true)} />)
    expect(screen.getByText('Prêmio conquistado')).toBeVisible()
    expect(screen.getByText('Prêmio oficial')).toBeVisible()
    expect(screen.getByText(/91,23/)).toBeVisible()
    expect(screen.getByText(/Valores congelados/)).toBeVisible()
  })

  it('keeps progress and the next tier useful when prizes are restricted', () => {
    render(<Goals data={data([{ ...positivity, maximumPrize: null, currentPrize: null, nextTierPrize: null }])} />)
    expect(screen.getByText('4 de 15 clientes')).toBeVisible()
    expect(screen.getByText('11 clientes')).toBeVisible()
    expect(screen.queryByText('Prêmio atual')).not.toBeInTheDocument()
    expect(screen.queryByText(/R\$/)).not.toBeInTheDocument()
  })

  it('does not invent a percentage or a remaining amount for unavailable goals', () => {
    render(<Goals data={{ ...data([{ ...revenue, target: 0, achievedPercent: null, nextTierPercent: null, amountToNextTier: null, maximumPrize: null, currentPrize: null, nextTierPrize: null }]), available: false, unavailableReason: 'Configuração pendente.' }} />)
    expect(screen.getByText('Configuração pendente.')).toBeVisible()
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
    expect(screen.queryByText('0%')).not.toBeInTheDocument()
    expect(screen.queryByText('Faltam')).not.toBeInTheDocument()
  })
})
