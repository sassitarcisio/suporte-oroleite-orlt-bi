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
    <header><div className="brand"><span>ORO</span> BI <small>OROLEITE</small></div><button onClick={onBack}>Voltar</button></header>
    <section className="hero">
      <p className="eyebrow">IMPORTACOES AUDITADAS</p>
      <h1>Carregue a fonte,<br /><em>preserve a rastreabilidade.</em></h1>
      <form onSubmit={submit}>
        <label>TIPO<select value={fileType} onChange={event => onFileTypeChange(event.target.value)}><option>Power</option><option>Ppp</option><option>Goals</option><option>GoalValues</option></select></label>
        <label>ARQUIVO CSV<input type="file" accept=".csv,text/csv" required onChange={event => onFileChange(event.target.files?.[0] ?? null)} /></label>
        <button disabled={state === 'loading'}>Enviar CSV</button>
      </form>
      {state === 'error' && <p className="notice error">A importacao falhou. Verifique seu perfil e o arquivo.</p>}
    </section>
  </>
}
