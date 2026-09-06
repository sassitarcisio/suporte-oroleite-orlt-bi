import { act, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import SellerPortal from './SellerPortal'

const permissions = { canViewRevenue: true, canViewCommission: false, canViewPrize: false, canViewPPP: false, canViewGoals: true, canViewTrades: false, canViewCustomers: true }
const identity = { userId: 'user-a', email: 'ana@example.test', roles: ['Vendedor'], sellerId: 'seller-a', seller: 'ANA', permissions }
const revenue = { grossSales: 100, netRevenue: 100, negativeMovements: 0, customerCount: 1, movementCount: 0, documentCount: 1 }
const dashboard = { startDate: '2026-09-01', endDate: '2026-09-05', referenceDate: '2026-09-05', period: revenue, month: revenue, today: revenue, dailyTrend: [], freshness: { updatedAtUtc: null, timestampKind: 'unavailable' } }
async function portal() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => new Response(JSON.stringify(String(input).endsWith('/me') ? identity : String(input).includes('/dashboard?') ? dashboard : { items: [] }), { status: 200 })))
  render(<SellerPortal token="seller" onLogout={vi.fn()} onSessionEnd={vi.fn()} onAdmin={vi.fn()} />)
  await screen.findByRole('heading', { name: 'Meu desempenho' })
}
afterEach(() => vi.unstubAllGlobals())

it('promotes goals and customers and moves focus inside More, restoring it with Escape', async () => {
  await portal()
  const nav = screen.getByRole('navigation', { name: 'Navegação rápida' })
  expect([...nav.querySelectorAll('button')].map(button => button.textContent)).toEqual(['Início', 'Vendas', 'Metas', 'Clientes', 'Mais'])
  fireEvent.click(screen.getByRole('button', { name: 'Mais' }))
  const dialog = screen.getByRole('dialog', { name: 'Mais módulos' })
  expect(dialog.contains(document.activeElement)).toBe(true)
  fireEvent.keyDown(document.activeElement!, { key: 'Escape' })
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Mais' })).toHaveFocus()
})

it('shows manual Android/iPhone instructions without claiming automatic installation', async () => {
  await portal()
  fireEvent.click(screen.getByRole('button', { name: 'Mais' }))
  fireEvent.click(screen.getByRole('button', { name: 'Instalar aplicativo' }))
  expect(screen.getByText(/Android:.*menu.*Instalar aplicativo/)).toBeVisible()
  expect(screen.getByText(/iPhone.*Safari.*Compartilhar.*Tela de Início/)).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Instalar agora' })).not.toBeInTheDocument()
})

it('captures the install event before opening help and prompts only on explicit action', async () => {
  await portal()
  const prompt = vi.fn().mockResolvedValue(undefined)
  const event = Object.assign(new Event('beforeinstallprompt', { cancelable: true }), { prompt, userChoice: Promise.resolve({ outcome: 'accepted' }) })
  act(() => window.dispatchEvent(event))
  fireEvent.click(screen.getByRole('button', { name: 'Mais' }))
  fireEvent.click(screen.getByRole('button', { name: 'Instalar aplicativo' }))
  expect(prompt).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'Instalar agora' }))
  expect(await screen.findByText(/Confirme a instalação no navegador/)).toBeVisible()
  expect(prompt).toHaveBeenCalledTimes(1)
  act(() => window.dispatchEvent(new Event('appinstalled')))
  expect(screen.getByText('Aplicativo instalado neste dispositivo.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Instalar agora' })).not.toBeInTheDocument()
})

it('recognizes standalone mode and keeps help available in Profile', async () => {
  vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })))
  await portal()
  fireEvent.click(within(screen.getByRole('navigation', { name: 'Navegação do portal' })).getByRole('button', { name: 'Perfil' }))
  expect(screen.getByText('Aplicativo instalado neste dispositivo.')).toBeVisible()
  expect(screen.getByLabelText('Confirmar nova senha')).toBeVisible()
})
