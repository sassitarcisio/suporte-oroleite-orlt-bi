// Imported brand names carry the inactive/missing markers; there is no brand
// status field. This is a presentation rule, never a filter on financial data.
export function isVisibleBrand(name: string | null | undefined): boolean {
  const normalized = (name ?? '').normalize('NFD').replace(/\p{M}/gu, '')
    .toUpperCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim()
  return normalized.length > 0 && !/^SEM INFORMAC(?:AO|OES)$/.test(normalized)
    && !normalized.split(' ').some(word => /^INATIV[OA]S?$/.test(word))
}

export function visibleGroupRows<T extends { label: string }>(rows: T[], dimension: string): T[] {
  return dimension === 'brand' ? rows.filter(row => isVisibleBrand(row.label)) : rows
}
