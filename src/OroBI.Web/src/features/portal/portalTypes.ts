export type PortalPermissions = {
  canViewRevenue: boolean; canViewCommission: boolean; canViewPrize: boolean;
  canViewPPP: boolean; canViewGoals: boolean; canViewTrades: boolean; canViewCustomers: boolean
}
export type PortalIdentity = { userId: string; email: string; userName?: string; roles: string[]; sellerId: string | null; seller: string | null; permissions: PortalPermissions | null; sellerAccesses?: Array<{ sellerId: string; name: string; permissions: PortalPermissions }> }
export type PortalFilters = { startDate: string; endDate: string; customerContains: string; productContains: string; brand: string }
export type RevenueSummary = { grossSales: number; netRevenue: number; negativeMovements: number; saleQuantity: number; movementCount: number; customerCount: number | null; documentCount: number; averageTicket: number | null }
export type PortalDashboard = { startDate: string; endDate: string; referenceDate: string; period: RevenueSummary; month: RevenueSummary; today: RevenueSummary; dailyTrend: Array<{ date: string; grossSales: number; netRevenue: number; negativeMovements: number }>; freshness: { source: string; updatedAtUtc: string | null; timestampKind: string } }
export type PortalSale = { id: string; date: string; documentNumber: string; movementType: string; customerCode: string; customerName: string; productName: string; brand: string; quantity: number; totalValue: number }
export type PortalPage<T> = { items: T[]; page: number; pageSize: number; totalCount: number }
export type PortalCustomer = { customerCode: string; customerName: string; city: string; grossSales: number; netRevenue: number; documentCount: number; lastPurchaseDate: string; averageTicket: number | null; purchasedQuantity: number }
export type PortalCustomers = { observedBuyersOnly: boolean; items: PortalCustomer[]; totalCount: number; hasMore: boolean }
export type PortalCustomerDetail = { customer: PortalCustomer; sales: PortalSale[]; totalCount: number; hasMore: boolean }
export type PortalRanking = { items: Array<{ label: string; grossSales: number; netRevenue: number; quantity: number; movementCount: number; customerCount: number | null; revenueSharePercent: number | null }>; totalCount: number; hasMore: boolean }
export type PortalGoal = { brand: string; type: string; target: number; actual: number; achievedPercent: number | null; maximumPrize: number | null; currentPrize: number | null; nextTierPercent: number | null; amountToNextTier: number | null; nextTierPrize: number | null }
export type PortalGoals = { year: number; month: number; available: boolean; isApproved?: boolean; unavailableReason: string | null; items: PortalGoal[] }
export type PortalPpp = { year: number; month: number; available: boolean; isApproved?: boolean; unavailableReason: string | null; achievementPercent: number | null; award: number | null; segments: Array<{ segment: string; customerCount: number | null; itemsPerSegment: number; groupsPlaced: number; achievementPercent: number | null }> }
export type PortalTrades = { physicalTrades: number; tradeToSalesPercent: number; movementCount: number; items: PortalSale[]; hasMore: boolean }
export type PortalClosing = { year: number; month: number; status: 'EmApuracao' | 'EmConferencia' | 'Aprovado'; isEstimated: boolean; approvedAtUtc: string | null; revenue: number | null; commissionableRevenue: number | null; commission: number | null; commissionPercent: number | null; pppPercent: number | null; pppAward: number | null; revenueAward: number | null; positivityAward: number | null; tradeAward: number | null; totalAwards: number | null; tradeValue: number | null; tradePercent: number | null; commissionAndAwards: number | null }

export const permissionLabels: Record<keyof PortalPermissions, string> = { canViewRevenue: 'Faturamento', canViewCommission: 'Comissão', canViewPrize: 'Prêmios', canViewPPP: 'PPP', canViewGoals: 'Metas', canViewTrades: 'Trocas', canViewCustomers: 'Clientes' }
