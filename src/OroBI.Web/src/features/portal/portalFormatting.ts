import type { PortalFilters } from './portalTypes'

export const money = (value: number | null | undefined) => value == null ? 'Não disponível' : value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
export const number = (value: number | null | undefined) => value == null ? 'Não disponível' : value.toLocaleString('pt-BR', { maximumFractionDigits: 2 })
export const percent = (value: number | null | undefined) => value == null ? 'Não disponível' : `${number(value)}%`
export const date = (value: string | null | undefined) => value ? new Date(value.length === 10 ? `${value}T12:00:00` : value).toLocaleDateString('pt-BR') : 'Data não disponível'
export const dateTime = (value: string | null | undefined) => value ? new Date(value).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo', dateStyle: 'short', timeStyle: 'short' }) : 'Data não disponível'
export function currentMonth() { return new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit' }).format(new Date()) }
export function monthFilters(month = currentMonth()): PortalFilters {
  const [year, index] = month.split('-').map(Number)
  const lastDay = new Date(year, index, 0).getDate()
  return { startDate: `${month}-01`, endDate: `${month}-${lastDay}`, customerContains: '', productContains: '', brand: '' }
}
export function presetFilters(label: string, now = new Date()): PortalFilters {
  const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).format(now)
  const values = monthFilters(today.slice(0, 7))
  if (label === 'Mês') return values
  const start = new Date(`${today}T12:00:00Z`)
  const daysBack = label === 'Ontem' ? 1 : label === 'Semana' ? (start.getUTCDay() + 6) % 7 : label === 'Últimos 30 dias' ? 29 : 0
  start.setUTCDate(start.getUTCDate() - daysBack)
  return { ...values, startDate: start.toISOString().slice(0, 10), endDate: label === 'Ontem' ? start.toISOString().slice(0, 10) : today }
}
export function filterQuery(filters: PortalFilters) { return new URLSearchParams(Object.entries(filters).filter(([, value]) => value.trim())).toString() }
