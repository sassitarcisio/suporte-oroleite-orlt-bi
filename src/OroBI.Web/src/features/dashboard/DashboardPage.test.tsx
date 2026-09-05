import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { DashboardPage } from './DashboardPage'

describe('Dashboard chart consistency', () => {
  it('reads the API percentage and compares daily series on the same scale', () => {
    const { container } = render(<DashboardPage summary={{ grossSales: 100, netResult: 80, negativeMovements: 20, negativePercent: 20, saleQuantity: 1, movementCount: 2, customerCount: 1, documentCount: 1 }} details={{ dailyTrend: [{ date: '2026-08-01', grossSales: 100, netResult: 80, negativeMovements: 20 }], sellerResults: [], groups: {} }} filters={{ startDate: '', endDate: '', seller: '', brand: '', city: '', group: '', customerContains: '', productContains: '', movementType: '' }} options={{ brands: [], groups: [], cities: [], movementTypes: [] }} sellers={[]} state="ready" onFiltersChange={() => {}} onClear={() => {}} onSubmit={() => {}} />)
    expect(screen.getByText('20%')).toBeVisible()
    expect(container.querySelector('.trend-gross')).toHaveAttribute('points', '300,35')
    expect(container.querySelector('.trend-negative')).toHaveAttribute('points', '300,183')
  })
})
