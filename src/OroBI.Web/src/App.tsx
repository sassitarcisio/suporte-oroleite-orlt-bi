import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest, apiBaseUrl } from './api/client'
import { clearAccessToken, readAccessToken, saveAccessToken } from './auth/session'
import { AnalyticsPage } from './features/analytics/AnalyticsPage'
import { TradeAnalysisPage } from './features/analytics/TradeAnalysisPage'
import type { TradeAnalysis } from './features/analytics/TradeAnalysisPage'
import { ClosingsPage } from './features/closings/ClosingsPage'
import type { ClosingSummary } from './features/closings/ClosingsPage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import type { DashboardDetails, DashboardFilterOptions, DashboardFilters, DashboardSummary } from './features/dashboard/DashboardPage'
import { ImportPage } from './features/imports/ImportPage'
import './App.css'

type LoginResponse = { accessToken: string }
type CurrentUser = { roles: string[] }
type PageState = 'idle' | 'loading' | 'ready' | 'error'
type View = 'dashboard' | 'import' | 'trades' | 'sales-trades' | 'margins' | 'net-margin' | 'closings' | 'closing-rh' | 'closing-supervisor' | 'closing-valdir'

const specialClosingSellers: Partial<Record<View, string>> = {
  'closing-supervisor': 'DEIVID MANNES',
  'closing-valdir': 'VALDIR ZACARIAS',
}

const navigationItems: Array<{ view: Exclude<View, 'import'>, label: string, icon: string }> = [
  { view: 'dashboard', label: 'Dashboard', icon: 'fa-chart-pie' },
  { view: 'trades', label: 'Visao de Trocas', icon: 'fa-arrow-right-arrow-left' },
  { view: 'sales-trades', label: 'Analise Venda x Troca', icon: 'fa-scale-balanced' },
  { view: 'margins', label: 'Margem de Produtos', icon: 'fa-chart-line' },
  { view: 'net-margin', label: 'Margem Liquida', icon: 'fa-coins' },
  { view: 'closings', label: 'Fechamento por vendedor', icon: 'fa-wallet' },
  { view: 'closing-rh', label: 'Fechamento RH', icon: 'fa-users' },
  { view: 'closing-supervisor', label: 'Fechamento supervisor', icon: 'fa-user-tie' },
  { view: 'closing-valdir', label: 'Fechamento Valdir', icon: 'fa-building' },
]

const analysisPages: Partial<Record<View, { endpoint: string, title: string, description: string }>> = {
  trades: { endpoint: '/api/trades', title: 'Visao de trocas', description: 'Acompanhe as trocas fisicas e seu peso sobre as vendas.' },
  'sales-trades': { endpoint: '/api/sales-trades', title: 'Venda x troca', description: 'Compare receita comercial e movimentos de troca.' },
  margins: { endpoint: '/api/margins', title: 'Margem de produtos', description: 'Leia receita, custo, lucro bruto e margem da operacao.' },
  'net-margin': { endpoint: '/api/net-margin', title: 'Margem liquida', description: 'Acompanhe venda liquida, custos e perdas da operacao.' },
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

const emptyDashboardFilters: DashboardFilters = {
  startDate: '', endDate: '', seller: '', brand: '', group: '', city: '', customerContains: '', productContains: '', movementType: '',
}

const emptyDashboardFilterOptions: DashboardFilterOptions = { brands: [], groups: [], cities: [], movementTypes: [] }

function toDateInputValue(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function createDashboardFilters(seller = ''): DashboardFilters {
  const today = new Date()
  const previousMonth = new Date(today.getFullYear(), today.getMonth() - 1, 1)
  const previousMonthEnd = new Date(today.getFullYear(), today.getMonth(), 0)
  return { ...emptyDashboardFilters, startDate: toDateInputValue(previousMonth), endDate: toDateInputValue(previousMonthEnd), seller }
}

function filterQuery(filters: DashboardFilters): string {
  const parameters = new URLSearchParams()
  if (filters.startDate) parameters.set('startDate', filters.startDate)
  if (filters.endDate) parameters.set('endDate', filters.endDate)
  if (filters.seller.trim()) parameters.set('seller', filters.seller.trim())
  if (filters.brand.trim()) parameters.set('brand', filters.brand.trim())
  if (filters.group.trim()) parameters.set('group', filters.group.trim())
  if (filters.city.trim()) parameters.set('city', filters.city.trim())
  if (filters.customerContains.trim()) parameters.set('customerContains', filters.customerContains.trim())
  if (filters.productContains.trim()) parameters.set('productContains', filters.productContains.trim())
  if (filters.movementType.trim()) parameters.append('movementTypes', filters.movementType.trim())
  return parameters.toString()
}

export default function App() {
  const [token, setToken] = useState(readAccessToken)
  const [email, setEmail] = useState(() => window.localStorage.getItem('orobi:last-email') ?? '')
  const [password, setPassword] = useState('')
  const [passwordVisible, setPasswordVisible] = useState(false)
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [dashboardDetails, setDashboardDetails] = useState<DashboardDetails | null>(null)
  const [dashboardFilters, setDashboardFilters] = useState<DashboardFilters>(() => createDashboardFilters(readSellerFilter()))
  const [dashboardFilterOptions, setDashboardFilterOptions] = useState<DashboardFilterOptions>(emptyDashboardFilterOptions)
  const [state, setState] = useState<PageState>('idle')
  const [view, setView] = useState<View>('dashboard')
  const [fileType, setFileType] = useState('Power')
  const [file, setFile] = useState<File | null>(null)
  const [roles, setRoles] = useState<string[]>([])
  const [analysis, setAnalysis] = useState<Record<string, number> | null>(null)
  const [tradeAnalysis, setTradeAnalysis] = useState<TradeAnalysis | null>(null)
  const [analysisState, setAnalysisState] = useState<PageState>('idle')
  const [closing, setClosing] = useState<ClosingSummary | null>(null)
  const [closingState, setClosingState] = useState<PageState>('idle')
  const [closingError, setClosingError] = useState<string | null>(null)
  const closingRequestId = useRef(0)
  const [sellers, setSellers] = useState<string[]>([])
  const [menuOpen, setMenuOpen] = useState(false)

  async function loadDashboard(filters = dashboardFilters) {
    if (!token) return
    setState('loading')
    try {
      const parameters = filterQuery(filters)
      const query = parameters ? `?${parameters}` : ''
      const [dashboardSummary, details] = await Promise.all([
        apiRequest<DashboardSummary>(`/api/dashboard${query}`, token),
        apiRequest<DashboardDetails>(`/api/dashboard/details${query}`, token),
      ])
      setSummary(dashboardSummary)
      setDashboardDetails({
        dailyTrend: Array.isArray(details.dailyTrend) ? details.dailyTrend : [],
        sellerResults: Array.isArray(details.sellerResults) ? details.sellerResults : [],
      })
      setState('ready')
    } catch {
      setState('error')
    }
  }

  function applyDashboardFilter(filters: DashboardFilters) {
    writeSellerFilter(filters.seller)
    void loadDashboard(filters)
  }

  function clearDashboardFilters() {
    const clearedFilters = createDashboardFilters()
    setDashboardFilters(clearedFilters)
    writeSellerFilter('')
    void loadDashboard(clearedFilters)
  }

  function navigate(nextView: Exclude<View, 'import'>) {
    setMenuOpen(false)
    if (nextView === view) return
    closingRequestId.current += 1
    setClosing(null)
    setClosingState('idle')
    setClosingError(null)
    setView(nextView)
    const specialSeller = specialClosingSellers[nextView]
    if (specialSeller) void loadClosing(specialSeller, createDashboardFilters().startDate.slice(0, 7))
  }

  async function loadClosing(activeSeller: string, month: string) {
    if (!token || !activeSeller || !month) return
    const requestId = ++closingRequestId.current
    setClosingState('loading')
    setClosing(null)
    setClosingError(null)
    try {
      const query = new URLSearchParams({ seller: activeSeller, month })
      const result = await apiRequest<ClosingSummary>(`/api/closings?${query}`, token)
      if (requestId !== closingRequestId.current) return
      setClosing(result)
      setClosingState('ready')
    } catch (error) {
      if (requestId !== closingRequestId.current) return
      const message = error instanceof Error ? error.message : undefined
      const status = message?.match(/(\d{3})$/)?.[1]
      setClosingError(message && !status
        ? message
        : status
          ? `A API retornou erro ${status}. Tente novamente em alguns instantes.`
        : 'Nao foi possivel comunicar com a API. Verifique sua conexao e tente novamente.')
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
    void apiRequest<string[]>('/api/sellers', token).then(result => setSellers(Array.isArray(result) ? result : [])).catch(() => setSellers([]))
    void apiRequest<DashboardFilterOptions>('/api/dashboard/filter-options', token).then(result => setDashboardFilterOptions({
      brands: Array.isArray(result.brands) ? result.brands : [],
      groups: Array.isArray(result.groups) ? result.groups : [],
      cities: Array.isArray(result.cities) ? result.cities : [],
      movementTypes: Array.isArray(result.movementTypes) ? result.movementTypes : [],
    })).catch(() => setDashboardFilterOptions(emptyDashboardFilterOptions))
  }, [token])

  useEffect(() => {
    const page = analysisPages[view]
    if (!page || !token || view === 'trades' || view === 'sales-trades') return

    setAnalysis(null)
    setAnalysisState('loading')
    void apiRequest<Record<string, number>>(page.endpoint, token)
      .then(result => { setAnalysis(result); setAnalysisState('ready') })
      .catch(() => setAnalysisState('error'))
  }, [token, view])

  useEffect(() => {
    if (!token || (view !== 'trades' && view !== 'sales-trades')) return

    setTradeAnalysis(null)
    setAnalysisState('loading')
    const query = filterQuery(dashboardFilters)
    void apiRequest<TradeAnalysis>(`/api/trade-analysis${query ? `?${query}` : ''}`, token)
      .then(result => { setTradeAnalysis(result); setAnalysisState('ready') })
      .catch(() => setAnalysisState('error'))
  }, [token, view, dashboardFilters])

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
      window.localStorage.setItem('orobi:last-email', email.trim())
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

  function selectImportFile(nextFile: File | null) {
    setFile(nextFile)
    if (nextFile && /^VALOR[_ -]?METAS\.csv$/i.test(nextFile.name)) setFileType('GoalValues')
  }

  if (!token) return <main className="shell login-shell"><section className="login-layout shadow-lg"><aside className="login-brand-panel"><img className="login-brand-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" /><div><p className="eyebrow">OROLEITE BI</p><h1 aria-label="Central de resultados">Central de<br /><span>resultados.</span></h1><p>Inteligencia comercial para decisoes mais seguras, todos os dias.</p></div><p className="login-brand-footer"><i className="fa-solid fa-shield-halved" aria-hidden="true" /> Ambiente corporativo protegido</p></aside><section className="login-form-panel"><div className="login-form-heading"><p className="eyebrow">ACESSO RESTRITO</p><h2>Bem-vindo de volta.</h2><p>Informe suas credenciais para acessar os indicadores da operacao.</p></div><form onSubmit={login}><label>E-MAIL<input type="email" autoComplete="username" required value={email} onChange={event => setEmail(event.target.value)} /></label><label>SENHA<span className="password-field"><input type={passwordVisible ? 'text' : 'password'} autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /><button type="button" className="password-toggle" onClick={() => setPasswordVisible(visible => !visible)} aria-label={passwordVisible ? 'Ocultar senha' : 'Mostrar senha'}><i className={`fa-solid ${passwordVisible ? 'fa-eye-slash' : 'fa-eye'}`} aria-hidden="true" /></button></span></label><button className="btn btn-dark" type="submit" disabled={state === 'loading'}>{state === 'loading' ? 'Entrando...' : 'Entrar'} <i className="fa-solid fa-arrow-right" aria-hidden="true" /></button></form>{state === 'error' && <p className="notice error">Credenciais invalidas ou API indisponivel.</p>}</section></section></main>

  if (view === 'import') return <main className="shell import-workspace"><ImportPage file={file} fileType={fileType} state={state} onBack={() => setView('dashboard')} onFileChange={selectImportFile} onFileTypeChange={setFileType} onSubmit={() => void upload()} /></main>

  const page = analysisPages[view]
  return <main className="executive-layout">
    <aside className={`side-rail ${menuOpen ? 'is-open' : ''}`}><div className="brand"><img className="brand-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" /></div><p className="rail-label">CENTRAL DE RESULTADOS</p><nav id="main-navigation" className="side-navigation" aria-label="Modulos do BI">{navigationItems.map(item => <button className={view === item.view ? 'active' : ''} key={item.view} onClick={() => navigate(item.view)}><i className={`fa-solid ${item.icon}`} aria-hidden="true" /><span>{item.label}</span></button>)}</nav><div className="rail-footer"><i className="fa-solid fa-circle-check" aria-hidden="true" /> Dados sincronizados</div></aside>
    <section className={`main-canvas ${view === 'dashboard' ? 'dashboard-workspace' : 'analysis-workspace'}`}><header className="command-bar"><button className="menu-toggle" type="button" aria-label="Alternar navegacao" aria-controls="main-navigation" aria-expanded={menuOpen} onClick={() => setMenuOpen(open => !open)}><i className="fa-solid fa-bars" aria-hidden="true" /></button><div><p>PAINEL EXECUTIVO</p><strong>{navigationItems.find(item => item.view === view)?.label ?? 'Importar'}</strong></div><div className="command-actions">{roles.includes('Administrador') && <button className="btn btn-accent" onClick={() => setView('import')}><i className="fa-solid fa-file-arrow-up" aria-hidden="true" /> Importar</button>}<button className="btn btn-ghost" onClick={() => { clearAccessToken(); setToken('') }} aria-label="Sair"><i className="fa-solid fa-right-from-bracket" aria-hidden="true" /></button></div></header>
      {view === 'dashboard' && <DashboardPage summary={summary} details={dashboardDetails} filters={dashboardFilters} options={dashboardFilterOptions} sellers={sellers} state={state} onFiltersChange={setDashboardFilters} onSubmit={() => applyDashboardFilter(dashboardFilters)} onClear={clearDashboardFilters} />}
      {page && !(['trades', 'sales-trades'] as View[]).includes(view) && <AnalyticsPage title={page.title} description={page.description} data={analysis} state={analysisState} />}
      {(view === 'trades' || view === 'sales-trades') && <TradeAnalysisPage mode={view} data={tradeAnalysis} state={analysisState} />}
      {(['closings', 'closing-rh', 'closing-supervisor', 'closing-valdir'] as View[]).includes(view) && <ClosingsPage key={view} title={navigationItems.find(item => item.view === view)?.label} summary={closing} sellers={sellers} state={closingState} errorMessage={closingError} initialSeller={specialClosingSellers[view]} initialMonth={specialClosingSellers[view] ? createDashboardFilters().startDate.slice(0, 7) : ''} onSubmit={(activeSeller, month) => void loadClosing(activeSeller, month)} />}
    </section>
  </main>
}
