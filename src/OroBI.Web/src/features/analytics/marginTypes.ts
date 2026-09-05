export type MarginDimension = 'customer' | 'product' | 'brand'
export type NetMarginDimension = MarginDimension | 'seller' | 'group' | 'city'

export type MarginRow = {
  label: string
  revenue: number
  cost: number
  grossProfit: number
  marginPercent: number | null
  quantity: number
}

export type MarginReport = {
  revenue: number
  cost: number
  grossProfit: number
  marginPercent: number
  customerCount: number
  productCount: number
  movementCount: number
  groups: Partial<Record<MarginDimension, MarginRow[]>>
}

export type NetMarginRow = {
  label: string
  grossSales: number
  ownReturns: number
  customerReturns: number
  returns: number
  netSales: number
  netCost: number
  tradeLosses: number
  boletoDiscounts: number
  liquidProfit: number
  liquidMarginPercent: number | null
  quantity: number
  movementCount: number
  losses: number
}

export type NetMarginReport = Omit<NetMarginRow, 'label' | 'losses' | 'liquidMarginPercent'> & {
  liquidMarginPercent: number
  productCount: number
  groups: Partial<Record<NetMarginDimension, NetMarginRow[]>>
}
