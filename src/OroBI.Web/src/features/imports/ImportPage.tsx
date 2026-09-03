import type { FormEvent } from 'react'

type ImportPageProps = {
  file: File | null
  fileType: string
  state: 'idle' | 'loading' | 'ready' | 'error'
  onBack: () => void
  onFileChange: (file: File | null) => void
  onFileTypeChange: (fileType: string) => void
  onSubmit: () => void
}

export function ImportPage({ file, fileType, state, onBack, onFileChange, onFileTypeChange, onSubmit }: ImportPageProps) {
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (file) onSubmit()
  }

  return <>
    <header className="topbar"><div className="brand"><img className="brand-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" /></div><button className="btn btn-ghost" onClick={onBack}><i className="fa-solid fa-arrow-left" aria-hidden="true" /> Voltar</button></header>
    <section className="hero import-hero">
      <p className="eyebrow"><i className="fa-solid fa-shield-halved" aria-hidden="true" /> IMPORTACOES AUDITADAS</p>
      <h1>Carregue a fonte,<br /><em>preserve a rastreabilidade.</em></h1>
      <form className="import-form" onSubmit={submit}>
        <label>TIPO<select value={fileType} onChange={event => onFileTypeChange(event.target.value)}><option>Power</option><option>Ppp</option><option>Goals</option><option>GoalValues</option></select></label>
        <div className="file-picker"><label htmlFor="import-file">ARQUIVO CSV</label><input id="import-file" type="file" accept=".csv,text/csv" required onChange={event => onFileChange(event.target.files?.[0] ?? null)} /><span>{file?.name ?? 'Escolha o arquivo de origem'}</span></div>
        <button className="btn btn-dark" disabled={state === 'loading'}>{state === 'loading' ? 'Processando...' : 'Enviar CSV'} <i className={`fa-solid ${state === 'loading' ? 'fa-spinner fa-spin' : 'fa-arrow-up-from-bracket'}`} aria-hidden="true" /></button>
      </form>
      {state === 'error' && <p className="notice error">A importacao falhou. Verifique seu perfil e o arquivo.</p>}
    </section>
  </>
}
