import { useRef, useState } from 'react'
import type { FormEvent } from 'react'

type Props = { initialEmail: string; loading: boolean; errorMessage: string; statusMessage?: string; onRetryLogout?: () => void; onSubmit: (email: string, password: string, rememberDevice: boolean) => Promise<void> }

export default function LoginScreen({ initialEmail, loading, errorMessage, statusMessage, onRetryLogout, onSubmit }: Props) {
  const [email, setEmail] = useState(initialEmail)
  const [password, setPassword] = useState('')
  const [visible, setVisible] = useState(false)
  const [remember, setRemember] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const submittingRef = useRef(false)
  const [helpOpen, setHelpOpen] = useState(false)
  const helpButton = useRef<HTMLButtonElement>(null)
  const busy = loading || submitting
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy || submittingRef.current) return
    submittingRef.current = true
    setSubmitting(true)
    try { await onSubmit(email.trim(), password, remember) }
    finally { submittingRef.current = false; setSubmitting(false) }
  }
  function closeHelp() { setHelpOpen(false); helpButton.current?.focus() }
  return <main className="login-screen">
    <section className="login-layout">
      <aside className="login-institutional">
        <img className="login-desktop-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" />
        <div><p className="login-eyebrow">OROLEITE BI</p><h1 aria-label="Central de resultados">Central de<br /><span>resultados.</span></h1><p>Vendas, metas e desempenho comercial em um só lugar.</p></div>
        <p className="login-protected"><i className="fa-solid fa-shield-halved" aria-hidden="true" /> Ambiente corporativo protegido</p>
      </aside>
      <section className="login-access" aria-labelledby="login-title">
        <div className="login-mobile-identity"><img src="/logoOroleite.png" alt="Oroleite Distribuidora" /><strong>BI OROLEITE</strong><p>Acesse sua conta</p></div>
        <header className="login-heading"><p className="login-eyebrow">ACESSO RESTRITO</p><h2 id="login-title">Acesse o BI Oroleite</h2><p>Consulte seus indicadores e resultados comerciais.</p></header>
        {statusMessage && <p className="login-error" role="status">{statusMessage}</p>}
        {onRetryLogout && <button className="login-help" type="button" onClick={onRetryLogout}>Tentar sair novamente</button>}
        <form onSubmit={submit} aria-busy={busy}>
          <label htmlFor="login-email">E-MAIL</label>
          <input id="login-email" name="username" type="email" inputMode="email" autoComplete="username" autoCapitalize="none" spellCheck={false} required value={email} onChange={event => setEmail(event.target.value)} />
          <label htmlFor="login-password">SENHA</label>
          <div className="login-password"><input id="login-password" name="password" type={visible ? 'text' : 'password'} autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /><button type="button" aria-label={visible ? 'Ocultar senha' : 'Mostrar senha'} aria-pressed={visible} onClick={() => setVisible(value => !value)}><i className={`fa-solid ${visible ? 'fa-eye-slash' : 'fa-eye'}`} aria-hidden="true" /></button></div>
          <div className="login-remember"><label htmlFor="login-remember"><input id="login-remember" name="rememberDevice" type="checkbox" checked={remember} onChange={event => setRemember(event.target.checked)} aria-describedby="remember-help" />Manter-me conectado neste dispositivo</label><p id="remember-help">Use esta opção somente em seu dispositivo pessoal.</p></div>
          {errorMessage && <p className="login-error" role="alert" aria-live="polite">{errorMessage}</p>}
          <button className="login-submit" type="submit" disabled={busy}>{busy ? 'ENTRANDO...' : 'ENTRAR'} <i className="fa-solid fa-arrow-right" aria-hidden="true" /></button>
        </form>
        <button ref={helpButton} type="button" className="login-help" onClick={() => setHelpOpen(true)}>Esqueci minha senha</button>
        <p className="login-mobile-protected login-protected"><i className="fa-solid fa-lock" aria-hidden="true" /> Ambiente corporativo protegido</p>
      </section>
    </section>
    {helpOpen && <div className="login-help-backdrop"><section className="login-help-dialog" role="dialog" aria-modal="true" aria-labelledby="password-help-title" aria-describedby="password-help-description" onKeyDown={event => { if (event.key === 'Escape') closeHelp(); if (event.key === 'Tab') { event.preventDefault(); event.currentTarget.querySelector('button')?.focus() } }}><h2 id="password-help-title">Esqueci minha senha</h2><p id="password-help-description">Solicite a redefinição de senha ao administrador do BI Oroleite.</p><button type="button" autoFocus onClick={closeHelp}>Entendi</button></section></div>}
  </main>
}
