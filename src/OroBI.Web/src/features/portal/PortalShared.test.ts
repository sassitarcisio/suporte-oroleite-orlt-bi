import { describe, expect, it } from 'vitest'
import { presetFilters as preset } from './portalFormatting'

describe('Brazil calendar shortcuts', () => {
  it('uses the Brazil day near midnight UTC and keeps the last 30 days inclusive', () => {
    expect(preset('Hoje', new Date('2026-09-06T01:00:00Z'))).toMatchObject({ startDate: '2026-09-05', endDate: '2026-09-05' })
    expect(preset('Últimos 30 dias', new Date('2026-09-06T01:00:00Z'))).toMatchObject({ startDate: '2026-08-07', endDate: '2026-09-05' })
    expect(preset('Semana', new Date('2026-09-06T01:00:00Z'))).toMatchObject({ startDate: '2026-08-31', endDate: '2026-09-05' })
  })
})
