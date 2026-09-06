import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import PortalAccounts from './PortalAccounts'

const permissions = { canViewRevenue: true, canViewCommission: true, canViewPrize: true, canViewPPP: true, canViewGoals: true, canViewTrades: true, canViewCustomers: true }
const seller = { id: 'seller-a', name: 'ANA', importedName: 'ANA IMPORTADA', externalId: 'ERP-42', isActive: true }
const user = { id: 'user-a', email: 'ana@example.test', registrationName: 'Ana Silva', roles: ['Vendedor'], isActive: true, lastLoginAtUtc: '2026-09-05T12:00:00Z', mustChangePassword: true, sellerAccesses: [{ sellerId: seller.id, isActive: true, permissions }] }
const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200 })
function setup(users: unknown[] = []) {
  const writes: Array<{ url: string; body: Record<string, unknown> }> = []
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    if (init?.method && init.method !== 'GET') { writes.push({ url, body: JSON.parse(String(init.body)) }); return json({ temporaryPassword: 'Synthetic9!Once' }) }
    return json(url.endsWith('/admin/users') ? users : [seller])
  }))
  const { rerender } = render(<PortalAccounts token="admin" />)
  return { writes, rerender }
}
afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); sessionStorage.clear() })

it('creates and edits the ERP code without changing imported seller identity', async () => {
  const { writes } = setup()
  fireEvent.change(await screen.findByLabelText('Nome do vendedor'), { target: { value: 'BIA' } })
  fireEvent.change(screen.getByLabelText('Nome no arquivo importado'), { target: { value: 'BIA CSV' } })
  fireEvent.change(screen.getByLabelText('Código do vendedor no ERP'), { target: { value: 'ERP-43' } })
  fireEvent.click(screen.getByRole('button', { name: 'Cadastrar vendedor' }))
  await waitFor(() => expect(writes[0].body).toEqual({ name: 'BIA', importedName: 'BIA CSV', externalId: 'ERP-43' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Editar ANA' }))
  fireEvent.change(screen.getByLabelText('Editar código ERP'), { target: { value: 'ERP-44' } })
  fireEvent.click(screen.getByRole('button', { name: 'Salvar código ERP' }))
  await waitFor(() => expect(writes[1]).toEqual({ url: expect.stringContaining('/seller-a/external-id'), body: { externalId: 'ERP-44' } }))
})

it('keeps the generated password available once across data refresh and only copies on a click', async () => {
  const copy = vi.fn().mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: copy } })
  const { writes } = setup()
  fireEvent.change(await screen.findByLabelText('Nome da pessoa'), { target: { value: 'Ana Silva' } })
  fireEvent.change(screen.getByLabelText('E-mail da conta'), { target: { value: 'ana@example.test' } })
  fireEvent.click(screen.getByRole('checkbox', { name: /ANA$/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Gerar senha temporária e criar conta' }))
  expect(await screen.findByLabelText('Senha temporária gerada')).toHaveAttribute('type', 'password')
  await screen.findByRole('button', { name: 'Cadastrar vendedor' })
  expect(writes[0].body).toMatchObject({ name: 'Ana Silva', generateTemporaryPassword: true, sellerAccesses: [{ sellerId: 'seller-a' }] })
  expect(writes[0].body).not.toHaveProperty('password')
  expect(copy).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'Mostrar senha temporária' }))
  expect(screen.getByLabelText('Senha temporária gerada')).toHaveAttribute('type', 'text')
  fireEvent.click(screen.getByRole('button', { name: 'Copiar senha temporária' }))
  expect(await screen.findByRole('status')).toHaveTextContent('Senha copiada')
  expect(copy).toHaveBeenCalledWith('Synthetic9!Once')
  expect(JSON.stringify(localStorage) + JSON.stringify(sessionStorage)).not.toContain('Synthetic9!Once')
  fireEvent.click(screen.getByRole('button', { name: 'Descartar senha temporária' }))
  expect(screen.queryByLabelText('Senha temporária gerada')).not.toBeInTheDocument()
})

it('opens an associated seller account and shows login/password metadata instead of creating a duplicate', async () => {
  const { writes } = setup([user])
  fireEvent.click(await screen.findByRole('button', { name: 'Editar ANA' }))
  fireEvent.click(screen.getByRole('button', { name: 'Gerenciar acesso de ANA' }))
  expect(screen.getByLabelText('Conta para editar')).toHaveValue('user-a')
  expect(screen.getByLabelText('Nome da pessoa')).toHaveValue('Ana Silva')
  expect(screen.getByText(/Último acesso:/)).toHaveTextContent('05/09/2026')
  expect(screen.getByText('Troca de senha obrigatória no próximo acesso.')).toBeVisible()
  expect(screen.getByRole('checkbox', { name: /ANA$/ })).toBeChecked()
  expect(writes).toHaveLength(0)
  fireEvent.click(screen.getByRole('button', { name: 'Gerar nova senha temporária' }))
  expect(await screen.findByLabelText('Senha temporária gerada')).toHaveValue('Synthetic9!Once')
  expect(writes[0]).toEqual({ url: expect.stringContaining('/user-a/reset-password'), body: { generateTemporaryPassword: true } })
})

it('preselects a seller for a new account while preserving management accounts with multiple links', async () => {
  setup([{ ...user, roles: ['Gestor'] }])
  fireEvent.click(await screen.findByRole('button', { name: 'Editar ANA' }))
  fireEvent.click(screen.getByRole('button', { name: 'Criar acesso para ANA' }))
  expect(screen.getByLabelText('Conta para editar')).toHaveValue('')
  expect(screen.getByRole('checkbox', { name: /ANA$/ })).toBeChecked()
  expect(screen.getByLabelText('Perfil de acesso')).toHaveValue('Vendedor')
})

it('discards a generated password when the administrator session changes and never restores it', async () => {
  const { rerender } = setup([user])
  fireEvent.change(await screen.findByLabelText('Conta para editar'), { target: { value: 'user-a' } })
  fireEvent.click(screen.getByRole('button', { name: 'Gerar nova senha temporária' }))
  expect(await screen.findByLabelText('Senha temporária gerada')).toHaveValue('Synthetic9!Once')
  rerender(<PortalAccounts token="another-admin" />)
  expect(screen.queryByLabelText('Senha temporária gerada')).not.toBeInTheDocument()
  await screen.findByLabelText('Conta para editar')
  rerender(<PortalAccounts token="admin" />)
  expect(screen.queryByLabelText('Senha temporária gerada')).not.toBeInTheDocument()
})

it('places ERP codes before seller names in access and approval choices and uses readable list punctuation', async () => {
  setup([{ id: 'pending', email: 'pending@example.test', registrationName: 'Pessoa pendente', isRegistrationPending: true, roles: [], sellerAccesses: [] }])
  const edit = await screen.findByRole('button', { name: 'Editar ANA' })
  expect(edit).toHaveTextContent('ANA IMPORTADA · Ativo · Código ERP: ERP-42')
  expect(screen.getByRole('checkbox', { name: 'ERP-42 · ANA' })).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: 'Analisar cadastro de Pessoa pendente' }))
  expect(within(screen.getByLabelText('Vendedor para este cadastro')).getByRole('option', { name: 'ERP-42 · ANA · ANA IMPORTADA' })).toBeVisible()
})
