import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest, apiBaseUrl } from './api/client'
import { clearAccessToken, readAccessToken, saveAccessToken } from './auth/session'
import { AnalyticsPage } from './features/analytics/AnalyticsPage'
import { ClosingsPage } from './features/closings/ClosingsPage'
import type { ClosingSummary } from './features/closings/ClosingsPage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import type { DashboardSummary } from './features/dashboard/DashboardPage'
import { ImportPage } from './features/imports/ImportPage'
import './App.css'

type LoginResponse = { accessToken: string }
type CurrentUser = { roles: string[] }
type PageState = 'idle' | 'loading' | 'ready' | 'error'
type View = 'dashboard' | 'import' | 'trades' | 'sales-trades' | 'margins' | 'closings'

const analysisPages: Partial<Record<View, { endpoint: string, title: string, description: string }>> = {
  trades: { endpoint: '/api/trades', title: 'Visao de trocas', description: 'Acompanhe as trocas fisicas e seu peso sobre as vendas.' },
  'sales-trades': { endpoint: '/api/sales-trades', title: 'Venda x troca', description: 'Compare receita comercial e movimentos de troca.' },
  margins: { endpoint: '/api/margins', title: 'Margem de produtos', description: 'Leia receita, custo, lucro bruto e margem da operacao.' },
}

function readSellerFilter(): string {
  return new URLSearchParams(window.location.search).get('seller') ?? ''
}

function writeSellerFilter(seller: string): void {
  const parameters = new URLSearchParams(window.location.search)
  if (seller.trim()) parameters.set('seller', seller.trim())
  else parameters.delete('seller')
  const query = parameters.toString()
  window.history.replaceState({}, '', query ? `${window.location.pathname}?${query}` : window.location.pathname)
}

export default function App() {
  const [token, setToken] = useState(readAccessToken)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [seller, setSeller] = useState(readSellerFilter)
  const [state, setState] = useState<PageState>('idle')
  const [view, setView] = useState<View>('dashboard')
  const [fileType, setFileType] = useState('Power')
  const [file, setFile] = useState<File | null>(null)
  const [roles, setRoles] = useState<string[]>([])
  const [analysis, setAnalysis] = useState<Record<string, number> | null>(null)
  const [analysisState, setAnalysisState] = useState<PageState>('idle')
  const [closing, setClosing] = useState<ClosingSummary | null>(null)
  const [closingState, setClosingState] = useState<PageState>('idle')

  async function loadDashboard(activeSeller = '') {
    if (!token) return
    setState('loading')
    try {
      const query = activeSeller.trim() ? `?seller=${encodeURIComponent(activeSeller.trim())}` : ''
      setSummary(await apiRequest<DashboardSummary>(`/api/dashboard${query}`, token))
      setState('ready')
    } catch {
      setState('error')
    }
  }

  function applyDashboardFilter(activeSeller: string) {
    writeSellerFilter(activeSeller)
    void loadDashboard(activeSeller)
  }

  async function loadClosing(activeSeller: string, month: string) {
    if (!token || !activeSeller || !month) return
    setClosingState('loading')
    setClosing(null)
    try {
      const query = new URLSearchParams({ seller: activeSeller, month })
      setClosing(await apiRequest<ClosingSummary>(`/api/closings?${query}`, token))
      setClosingState('ready')
    } catch {
      setClosingState('error')
    }
  }

  useEffect(() => {
    if (!token) {
      setRoles([])
      return
    }

    void loadDashboard()
    void apiRequest<CurrentUser>('/api/me', token).then(user => setRoles(user.roles)).catch(() => setRoles([]))
  }, [token])

  useEffect(() => {
    const page = analysisPages[view]
    if (!page || !token) return

    setAnalysis(null)
    setAnalysisState('loading')
    void apiRequest<Record<string, number>>(page.endpoint, token)
      .then(result => { setAnalysis(result); setAnalysisState('ready') })
      .catch(() => setAnalysisState('error'))
  }, [token, view])

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setState('loading')
    try {
      const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password }),
      })
      if (!response.ok) throw new Error('Login failed')
      const result = await response.json() as LoginResponse
      saveAccessToken(result.accessToken)
      setToken(result.accessToken)
    } catch {
      setState('error')
    }
  }

  async function upload() {
    if (!file) return
    setState('loading')
    try {
      const form = new FormData()
      form.append('fileType', fileType)
      form.append('file', file)
      await apiRequest('/api/imports', token, { method: 'POST', body: form })
      setFile(null)
      setState('ready')
      setView('dashboard')
      await loadDashboard()
    } catch {
      setState('error')
    }
  }

  if (!token) return <main className="shell login"><section className="login-card"><div className="brand"><span>ORO</span> BI <small>OROLEITE</small></div><p className="eyebrow">ACESSO RESTRITO</p><h1>Entre no centro<br /><em>de resultados.</em></h1><form onSubmit={login}><label>E-MAIL<input type="email" required value={email} onChange={event => setEmail(event.target.value)} /></label><label>SENHA<input type="password" required value={password} onChange={event => setPassword(event.target.value)} /></label><button>Entrar</button></form>{state === 'error' && <p className="notice error">Credenciais invalidas ou API indisponivel.</p>}</section></main>

  if (view === 'import') return <main className="shell"><ImportPage file={file} fileType={fileType} state={state} onBack={() => setView('dashboard')} onFileChange={setFile} onFileTypeChange={setFileType} onSubmit={() => void upload()} /></main>

  const page = analysisPages[view]
  return <main className="shell">
    <header><div className="brand"><span>ORO</span> BI <small>OROLEITE</small></div><div>{roles.includes('Administrador') && <button onClick={() => setView('import')}>Importar</button>}<button onClick={() => { clearAccessToken(); setToken('') }}>Sair</button></div></header>
    <nav aria-label="Modulos do BI"><button onClick={() => setView('dashboard')}>Dashboard</button><button onClick={() => setView('trades')}>Trocas</button><button onClick={() => setView('sales-trades')}>Venda x troca</button><button onClick={() => setView('margins')}>Margem</button><button onClick={() => setView('closings')}>Fechamento</button></nav>
    {view === 'dashboard' && <DashboardPage summary={summary} seller={seller} state={state} onSellerChange={setSeller} onSubmit={() => applyDashboardFilter(seller)} />}
    {page && <AnalyticsPage title={page.title} description={page.description} data={analysis} state={analysisState} />}
    {view === 'closings' && <ClosingsPage summary={closing} state={closingState} onSubmit={(activeSeller, month) => void loadClosing(activeSeller, month)} />}
  </main>
}
