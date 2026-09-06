import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest } from '../api/client'

type Props = { token: string; requiredChange?: boolean; onChanged: (newPassword: string) => void | Promise<void> }
export default function ChangePasswordForm({ token, requiredChange = false, onChanged }: Props) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const active = useRef(true)
  useEffect(() => { active.current = true; return () => { active.current = false } }, [])
  async function submit(event: FormEvent) {
    event.preventDefault(); setError('')
    if (newPassword !== confirmation) { setError('As novas senhas não coincidem. Confira a confirmação.'); return }
    if (currentPassword === newPassword) { setError('Escolha uma senha diferente da senha atual.'); return }
    setBusy(true)
    try {
      await apiRequest('/api/v1/me/change-password', token, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ currentPassword, newPassword }) })
      if (!active.current) return
      setCurrentPassword(''); setNewPassword(''); setConfirmation('')
      await onChanged(newPassword)
    } catch (reason) {
      if (active.current) { setError(reason instanceof Error ? reason.message : 'Não foi possível alterar a senha.'); setCurrentPassword(''); setNewPassword(''); setConfirmation('') }
    } finally { if (active.current) setBusy(false) }
  }
  return <form className="portal-form password-change-form" onSubmit={submit}>
    <p className="password-policy">Use pelo menos 8 caracteres, com letras maiúsculas e minúsculas, número e símbolo.</p>
    <label>Senha atual<input required type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label>
    <label>Nova senha<input required minLength={8} type="password" autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label>
    <label>Confirmar nova senha<input required minLength={8} type="password" autoComplete="new-password" value={confirmation} onChange={event => setConfirmation(event.target.value)} /></label>
    {error && <p className="notice error" role="alert">{error}</p>}
    <button disabled={busy}>{busy ? 'Salvando senha...' : requiredChange ? 'Definir senha' : 'Alterar senha'}</button>
  </form>
}
