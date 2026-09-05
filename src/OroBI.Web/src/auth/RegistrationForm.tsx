import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiBaseUrl } from '../api/client'
import './registration.css'

type Props = { onBack: () => void; onAccepted: (message: string, email: string) => void }

export default function RegistrationForm({ onBack, onAccepted }: Props) {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const request = useRef<AbortController | null>(null)

  useEffect(() => () => request.current?.abort(), [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy) return
    setError('')
    if (password !== confirmation) { setError('As senhas não coincidem.'); return }
    const controller = new AbortController()
    request.current = controller
    setBusy(true)
    try {
      const response = await fetch(`${apiBaseUrl}/api/v1/auth/register`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, cache: 'no-store',
        body: JSON.stringify({ name: name.trim(), email: email.trim(), password }), signal: controller.signal,
      })
      const body = await response.json().catch(() => null) as { message?: unknown; error?: unknown; errors?: unknown } | null
      if (controller.signal.aborted) return
      if (response.status !== 202) {
        const validation = Array.isArray(body?.errors) ? body.errors.filter((item): item is string => typeof item === 'string').join(' ') : ''
        throw new Error(typeof body?.error === 'string' ? body.error : validation || (response.status === 429 ? 'Muitas tentativas de cadastro. Aguarde alguns minutos e tente novamente.' : 'Não foi possível enviar seu cadastro. Tente novamente.'))
      }
      setPassword(''); setConfirmation('')
      onAccepted(typeof body?.message === 'string' ? body.message : 'Solicitação recebida. Aguarde a aprovação do administrador.', email.trim())
    } catch (reason) {
      if (!controller.signal.aborted) setError(reason instanceof TypeError ? 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.' : reason instanceof Error ? reason.message : 'Não foi possível enviar seu cadastro.')
    } finally { if (!controller.signal.aborted) setBusy(false) }
  }

  return <div className="registration-form">
    <div className="login-form-heading"><p className="eyebrow">PRIMEIRO ACESSO</p><h2>Crie sua conta.</h2><p>Solicite seu acesso ao Portal do Vendedor. Um administrador confere seu cadastro e vincula seus resultados antes de liberar a entrada.</p></div>
    <form onSubmit={submit}>
      <label>Nome completo<input autoComplete="name" required maxLength={120} value={name} onChange={event => setName(event.target.value)} /></label>
      <label>E-mail para cadastro<input type="email" autoComplete="email" required maxLength={254} value={email} onChange={event => setEmail(event.target.value)} /></label>
      <label>Crie sua senha<input type="password" autoComplete="new-password" required minLength={8} maxLength={128} aria-describedby="registration-password-hint" value={password} onChange={event => setPassword(event.target.value)} /></label>
      <p className="registration-hint" id="registration-password-hint">Use de 8 a 128 caracteres, com letra maiúscula, minúscula, número e símbolo.</p>
      <label>Confirme sua senha<input type="password" autoComplete="new-password" required minLength={8} maxLength={128} value={confirmation} onChange={event => setConfirmation(event.target.value)} /></label>
      {error && <p className="notice error" role="alert">{error}</p>}
      <button className="btn btn-dark" disabled={busy}>{busy ? 'Enviando cadastro...' : 'Solicitar cadastro'}</button>
    </form>
    <button className="registration-back" type="button" onClick={onBack}>Voltar ao login</button>
  </div>
}
