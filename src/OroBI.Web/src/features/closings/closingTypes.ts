export type ClosingBrandAward = {
  brand: string
  positivityAward: number
  revenueAward: number
  tradeAward: number
  totalAward: number
  revenueGoal: number
  revenueActual: number
  revenueAchievedPercent: number
  revenuePrize: number
  positivityGoal: number
  positivityActual: number
  positivityAchievedPercent: number
  positivityPrize: number
  tradeValue: number
  tradeActualPercent: number
  tradeGoalPercent: number
  tradePrize: number
}

export type ClosingMonthlySummary = {
  scope: string
  revenue: number
  commissionableRevenue: number
  tradeRevenueBase?: number
  tradeValue: number
  tradePercent: number
  documentCount: number
  movementCount: number
  customerCount: number
  documents: Array<{
    documentNumber: string
    date: string
    seller: string
    customerCode: string
    customerName: string
    movementType: string
    totalValue: number
  }>
}

export type ClosingSummary = {
  ppp: { meanPercent: number, award: number }
  revenueAward: number
  positivityAward: number
  tradeAward: number
  compensation: { baseSalary: number, commission: number, totalSalary: number }
  totalAwards: number
  total: number
  monthly: ClosingMonthlySummary
  pppSegments: Array<{
    segment: string
    customerCount: number
    itemsPerSegment: number
    groupsPlaced: number
    achievementPercent: number | null
  }>
  brandAwards: ClosingBrandAward[]
}
