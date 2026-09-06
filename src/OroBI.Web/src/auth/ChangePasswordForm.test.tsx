import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ChangePasswordForm from './ChangePasswordForm'

afterEach(() => vi.unstubAllGlobals())
describe('Shared password change form', () => {
  it('requires matching confirmation without sending an invalid request', async () => {
    vi.stubGlobal('fetch', vi.fn())
    render(<ChangePasswordForm token="temporary" requiredChange onChanged={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Senha atual'), { target: { value: 'Temporary-123!' } })
    fireEvent.change(screen.getByLabelText('Nova senha'), { target: { value: 'Personal-123!' } })
    fireEvent.change(screen.getByLabelText('Confirmar nova senha'), { target: { value: 'Different-123!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Definir senha' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('senhas não coincidem')
    expect(fetch).not.toHaveBeenCalled()
  })
  it('uses the existing endpoint and passes the new password only in memory after success', async () => {
    const changed = vi.fn()
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 204 })))
    render(<ChangePasswordForm token="temporary" requiredChange onChanged={changed} />)
    fireEvent.change(screen.getByLabelText('Senha atual'), { target: { value: 'Temporary-123!' } })
    for (const label of ['Nova senha', 'Confirmar nova senha']) fireEvent.change(screen.getByLabelText(label), { target: { value: 'Personal-123!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Definir senha' }))
    await waitFor(() => expect(changed).toHaveBeenCalledWith('Personal-123!'))
    expect(vi.mocked(fetch).mock.calls[0][0]).toContain('/api/v1/me/change-password')
    expect(JSON.parse(String(vi.mocked(fetch).mock.calls[0][1]?.body))).toEqual({ currentPassword: 'Temporary-123!', newPassword: 'Personal-123!' })
    expect(screen.getByLabelText('Senha atual')).toHaveValue('')
    expect(screen.getByLabelText('Nova senha')).toHaveValue('')
    expect(screen.getByLabelText('Confirmar nova senha')).toHaveValue('')
  })
})
