import { fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PortalAccounts from './PortalAccounts'

const permissions = { canViewRevenue: true, canViewCommission: true, canViewPrize: true, canViewPPP: true, canViewGoals: true, canViewTrades: true, canViewCustomers: true }
const pending = { id: 'pending-a', email: 'ana@example.test', registrationName: 'Ana Silva', isRegistrationPending: true, isActive: false, roles: [], sellerAccesses: [] }
const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200 })
afterEach(() => vi.unstubAllGlobals())

describe('Pending registration approval', () => {
  it('approves a pending seller with exactly one selected seller and explicit permissions', async () => {
    let approved = false
    let approval: unknown
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/admin/sellers')) return json([{ id: 'seller-a', name: 'ANA IMPORTADA', importedName: 'ANA', isActive: true }, { id: 'seller-off', name: 'INATIVO', importedName: 'OFF', isActive: false }])
      if (url.endsWith('/admin/users')) return json(approved ? [{ ...pending, isRegistrationPending: false, isActive: true }] : [pending])
      if (url.endsWith('/pending-a/approve-registration')) { approval = JSON.parse(String(init?.body)); approved = true; return new Response(null, { status: 204 }) }
      return new Response('{}', { status: 404 })
    }))
    render(<div className="seller-portal"><PortalAccounts token="admin" /></div>)
    fireEvent.click(await screen.findByRole('button', { name: 'Analisar cadastro de Ana Silva' }))
    const review = screen.getByRole('region', { name: 'Aprovar cadastro de Ana Silva' })
    expect(within(review).queryByLabelText('Perfil de acesso')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ativar conta' })).not.toBeInTheDocument()
    expect(within(review).getByRole('button', { name: 'Aprovar cadastro' })).toBeDisabled()
    expect(within(review).queryByRole('option', { name: 'INATIVO' })).not.toBeInTheDocument()
    fireEvent.change(within(review).getByLabelText('Vendedor para este cadastro'), { target: { value: 'seller-a' } })
    fireEvent.click(within(review).getByLabelText('Comissão'))
    fireEvent.click(within(review).getByRole('button', { name: 'Aprovar cadastro' }))
    expect(await screen.findByText('Nenhum cadastro pendente de aprovação.')).toBeVisible()
    expect(approval).toEqual({ sellerId: 'seller-a', permissions: { ...permissions, canViewCommission: false } })
  })

  it('guides the administrator to create a seller first when no active seller exists', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => json(String(input).endsWith('/admin/users') ? [pending] : [])))
    render(<div className="seller-portal"><PortalAccounts token="admin" /></div>)
    fireEvent.click(await screen.findByRole('button', { name: 'Analisar cadastro de Ana Silva' }))
    expect(screen.getByText('Cadastre ou ative um vendedor antes de aprovar esta solicitação.')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Aprovar cadastro' })).toBeDisabled()
  })

  it('retains the pending review after an approval conflict', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/approve-registration')) return new Response(JSON.stringify({ error: 'Cadastro já analisado. Atualize a lista.' }), { status: 409 })
      return json(url.endsWith('/admin/users') ? [pending] : [{ id: 'seller-a', name: 'ANA', importedName: 'ANA', isActive: true }])
    }))
    render(<div className="seller-portal"><PortalAccounts token="admin" /></div>)
    fireEvent.click(await screen.findByRole('button', { name: 'Analisar cadastro de Ana Silva' }))
    fireEvent.change(screen.getByLabelText('Vendedor para este cadastro'), { target: { value: 'seller-a' } })
    fireEvent.click(screen.getByRole('button', { name: 'Aprovar cadastro' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Cadastro já analisado. Atualize a lista.')
    expect(screen.getByRole('button', { name: 'Aprovar cadastro' })).toBeEnabled()
  })
})
