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
  supervisor?: SupervisorClosingDetails | null
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

export type ClosingOperation = {
  key: string
  label: string
  revenue: number
  trade: number
  tradeReturns: number
  totalTrades: number
  tradePercent: number
}

export type SupervisorTeamMember = {
  seller: string
  includedInPayroll: boolean
  sales: ClosingOperation
  pppAward: number
  goalAward: number
  totalAward: number
}

export type SupervisorClosingDetails = {
  ownCommission: number
  teamCommission: number
  networkCommission: number
  operations: ClosingOperation[]
  team: SupervisorTeamMember[]
  teamAverageAward: number
  payrollTeamAverageAward: number
}

export type PayrollClosingRow = {
  seller: string
  sourceSeller: string
  reference: string
  revenue: number
  baseSalary: number
  commissionPercent: number | null
  commission: number
  pppAward: number
  goalAward: number
  tradeAward: number
  incentives: number
  total: number
}

export type PayrollClosing = {
  year: number
  month: number
  coverageSeller: string
  coverageSellers: string[]
  rows: PayrollClosingRow[]
  sellerCount: number
  totalBaseSalary: number
  totalCommission: number
  totalPppAward: number
  totalGoalAward: number
  totalIncentives: number
  total: number
}
