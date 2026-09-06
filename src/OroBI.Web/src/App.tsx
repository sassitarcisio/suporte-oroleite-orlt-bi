import { lazy, Suspense, useCallback, useEffect, useEffectEvent, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest, apiBaseUrl, authenticatedFetch, sessionRequestInit } from './api/client'
import { bootstrapCookieSession, confirmCookieSession, cookieSessionChangedKey, cookieSessionMode, notifyCookieSessionChanged, startCookieSession, accessTokenExpiresAt, clearAccessToken, consumeSessionExpired, expireAccessToken, isRememberedSession, passwordChangeRequiredEvent, readAccessToken, rememberedSessionKey, saveAccessToken, sessionExpiredEvent } from './auth/session'
import ChangePasswordForm from './auth/ChangePasswordForm'
import { MarginAnalysisPage } from './features/analytics/MarginAnalysisPage'
import type { MarginReport, NetMarginReport } from './features/analytics/marginTypes'
import { TradeAnalysisPage } from './features/analytics/TradeAnalysisPage'
import type { TradeAnalysis } from './features/analytics/TradeAnalysisPage'
import { ClosingsPage } from './features/closings/ClosingsPage'
import type { ClosingSummary } from './features/closings/ClosingsPage'
import { PayrollClosingPage } from './features/closings/PayrollClosingPage'
import type { PayrollClosing } from './features/closings/closingTypes'
import { DashboardPage } from './features/dashboard/DashboardPage'
import type { DashboardDetails, DashboardFilterOptions, DashboardFilters, DashboardSummary } from './features/dashboard/DashboardPage'
import { ImportPage } from './features/imports/ImportPage'
import RegistrationForm from './auth/RegistrationForm'
import './App.css'
import './ExecutiveGold.css'
import './CardPresentation.css'
import './auth/access.css'

const SellerPortal = lazy(() => import('./features/portal/SellerPortal'))
type LoginResponse = { accessToken?: string; sessionMode?: 'cookie'; roles?: string[]; expiresAtUtc?: string; mustChangePassword?: boolean }
type CurrentUser = { expiresAtUtc?: string; roles: string[]; email?: string; mustChangePassword?: boolean }
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
  const [token, setToken] = useState(cookieSessionMode ? bootstrapCookieSession : readAccessToken)
  const [portalRoute, setPortalRoute] = useState(() => window.location.pathname.startsWith('/portal'))
  const [email, setEmail] = useState(() => { try { return window.localStorage.getItem('orobi:last-email') ?? '' } catch { return '' } })
  const [password, setPassword] = useState('')
  const [passwordVisible, setPasswordVisible] = useState(false)
  const [rememberSession, setRememberSession] = useState(false)
  const [mustChangePassword, setMustChangePassword] = useState(false)
  const [identityToken, setIdentityToken] = useState('')
  const [identityEmail, setIdentityEmail] = useState('')
  const [identityError, setIdentityError] = useState('')
  const [identityRevision, setIdentityRevision] = useState(0)
  const [loginError, setLoginError] = useState('')
  const [logoutPending, setLogoutPending] = useState(false)
  const [logoutFailed, setLogoutFailed] = useState(false)
  const [registrationOpen, setRegistrationOpen] = useState(false)
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [dashboardDetails, setDashboardDetails] = useState<DashboardDetails | null>(null)
  const [dashboardFilters, setDashboardFilters] = useState<DashboardFilters>(() => createDashboardFilters(readSellerFilter()))
  const [dashboardDraft, setDashboardDraft] = useState(dashboardFilters)
  const appliedFiltersRef = useRef(dashboardFilters)
  const [dashboardFilterOptions, setDashboardFilterOptions] = useState<DashboardFilterOptions>(emptyDashboardFilterOptions)
  const [state, setState] = useState<PageState>('idle')
  const [view, setView] = useState<View>('dashboard')
  const [fileType, setFileType] = useState('Power')
  const [file, setFile] = useState<File | null>(null)
  const [roles, setRoles] = useState<string[]>([])
  const [marginData, setMarginData] = useState<MarginReport | NetMarginReport | null>(null)
  const [marginState, setMarginState] = useState<PageState>('idle')
  const [marginFilters, setMarginFilters] = useState<DashboardFilters>(createDashboardFilters)
  const marginRequestId = useRef(0)
  const [tradeAnalysis, setTradeAnalysis] = useState<TradeAnalysis | null>(null)
  const [analysisState, setAnalysisState] = useState<PageState>('idle')
  const [closing, setClosing] = useState<ClosingSummary | null>(null)
  const [closingState, setClosingState] = useState<PageState>('idle')
  const [closingError, setClosingError] = useState<string | null>(null)
  const closingRequestId = useRef(0)
  const [payroll, setPayroll] = useState<PayrollClosing | null>(null)
  const [payrollState, setPayrollState] = useState<PageState>('idle')
  const [payrollError, setPayrollError] = useState<string | null>(null)
  const [exporting, setExporting] = useState(false)
  const [exportError, setExportError] = useState<string | null>(null)
  const payrollRequestId = useRef(0)
  const [sellers, setSellers] = useState<string[]>([])
  const [menuOpen, setMenuOpen] = useState(false)
  const dashboardRequestId = useRef(0)
  const sessionVersion = useRef(0)
  const cookieAuthenticationPending = useRef(false)
  const [sessionMessage, setSessionMessage] = useState(() => consumeSessionExpired() ? 'Sua sessão expirou. Entre novamente para continuar.' : '')

  const loadDashboard = useCallback(async (filters: DashboardFilters) => {
    if (!token) return
    const requestId = ++dashboardRequestId.current
    setState('loading')
    try {
      const parameters = filterQuery(filters)
      const query = parameters ? `?${parameters}` : ''
      const [dashboardSummary, details] = await Promise.all([
        apiRequest<DashboardSummary>(`/api/dashboard${query}`, token),
        apiRequest<DashboardDetails>(`/api/dashboard/details${query}`, token),
      ])
      if (requestId !== dashboardRequestId.current) return
      setSummary(dashboardSummary)
      setDashboardDetails({
        groups: details.groups,
        dailyTrend: Array.isArray(details.dailyTrend) ? details.dailyTrend : [],
        sellerResults: Array.isArray(details.sellerResults) ? details.sellerResults : [],
      })
      setState('ready')
    } catch {
      if (requestId === dashboardRequestId.current) setState('error')
    }
  }, [token])

  function applyDashboardFilter(filters: DashboardFilters) {
    const applied = { ...filters }
    appliedFiltersRef.current = applied
    setDashboardFilters(applied)
    setDashboardDraft(applied)
    writeSellerFilter(filters.seller)
    void loadDashboard(applied)
  }

  function clearDashboardFilters() {
    const clearedFilters = createDashboardFilters()
    setDashboardFilters(clearedFilters)
    setDashboardDraft(clearedFilters)
    appliedFiltersRef.current = clearedFilters
    writeSellerFilter('')
    void loadDashboard(clearedFilters)
  }

  function navigate(nextView: Exclude<View, 'import'>) {
    setMenuOpen(false)
    if (nextView === view) return
    clearClosingRequests()
    setView(nextView)
    const specialSeller = specialClosingSellers[nextView]
    if (specialSeller) void loadClosing(specialSeller, createDashboardFilters().startDate.slice(0, 7))
    if (nextView === 'closing-rh') void loadPayroll(createDashboardFilters().startDate.slice(0, 7), 'MARCIO LUIZ DA ROSA')
    if (nextView === 'margins' || nextView === 'net-margin') void loadMargins(nextView, marginFilters)
  }

  const clearClosingRequests = useCallback(() => {
    marginRequestId.current += 1
    setMarginData(null)
    setMarginState('idle')
    closingRequestId.current += 1
    payrollRequestId.current += 1
    setPayroll(null)
    setPayrollState('idle')
    setPayrollError(null)
    setExportError(null)
    setExporting(false)
    setClosing(null)
    setClosingState('idle')
    setClosingError(null)
  }, [])

  function endSession(message = '') {
    sessionVersion.current += 1
    dashboardRequestId.current += 1
    clearClosingRequests()
    clearAccessToken(cookieSessionMode ? undefined : token)
    setToken('')
    setIdentityToken('')
    setIdentityEmail('')
    setIdentityError('')
    setMustChangePassword(false)
    setRememberSession(false)
    setLoginError('')
    setView('dashboard')
    setState('idle')
    setSummary(null)
    setDashboardDetails(null)
    setTradeAnalysis(null)
    setAnalysisState('idle')
    setRoles([])
    setSellers([])
    setDashboardFilterOptions(emptyDashboardFilterOptions)
    const filters = createDashboardFilters()
    appliedFiltersRef.current = filters
    setDashboardFilters(filters)
    setDashboardDraft(filters)
    setMarginFilters(filters)
    writeSellerFilter('')
    setFile(null)
    setPassword('')
    setPasswordVisible(false)
    setMenuOpen(false)
    setSessionMessage(message)
    setRegistrationOpen(false)
  }

  function logout() {
    if (logoutPending) return
    const previousToken = token
    if (cookieSessionMode) { setLogoutPending(true); setLogoutFailed(false) }
    endSession()
    void apiRequest('/api/v1/auth/logout', previousToken, { method: 'POST' }).then(() => {
      if (cookieSessionMode) notifyCookieSessionChanged()
    }).catch(() => {
      if (!readAccessToken()) {
        if (cookieSessionMode) { setLogoutFailed(true); setSessionMessage('Não foi possível encerrar a sessão no servidor. A sessão pode continuar ativa neste navegador. Conecte-se e tente sair novamente.') }
        else setSessionMessage('Sessão encerrada neste dispositivo. Não foi possível confirmar a revogação no servidor.')
      }
    }).finally(() => { if (cookieSessionMode) setLogoutPending(false) })
  }

  const openPortal = useCallback(() => {
    dashboardRequestId.current += 1
    clearClosingRequests()
    setSummary(null)
    setDashboardDetails(null)
    setTradeAnalysis(null)
    setView('dashboard')
    setPortalRoute(true)
    window.history.replaceState({}, '', '/portal')
  }, [clearClosingRequests])

  const expireSession = useEffectEvent((silent = false) => endSession(silent ? '' : 'Sua sessão expirou. Entre novamente para continuar.'))
  const enforcePasswordChange = useEffectEvent(() => {
    sessionVersion.current += 1
    dashboardRequestId.current += 1
    clearClosingRequests()
    setSummary(null)
    setDashboardDetails(null)
    setTradeAnalysis(null)
    setMustChangePassword(true)
  })
  const revalidateCookie = useEffectEvent((allowAnonymous = false) => {
    if (!cookieSessionMode || logoutPending || cookieAuthenticationPending.current || (!token && !allowAnonymous)) return
    if (token && identityToken !== token) return
    sessionVersion.current += 1
    dashboardRequestId.current += 1
    clearClosingRequests()
    setSummary(null)
    setDashboardDetails(null)
    setTradeAnalysis(null)
    setIdentityToken('')
    setIdentityError('')
    setMustChangePassword(false)
    setToken(startCookieSession(!!token))
  })
  const checkOtherTab = useEffectEvent((event: StorageEvent) => {
    if (cookieSessionMode) { if (event.key === cookieSessionChangedKey) revalidateCookie(true); return }
    if (!token || event.key !== rememberedSessionKey) return
    if (!isRememberedSession(token)) endSession('Sessão encerrada ou alterada em outra aba. Entre novamente.')
  })
  useEffect(() => {
    const expired = (event: Event) => expireSession((event as CustomEvent<{ silent?: boolean }>).detail?.silent === true)
    const required = () => enforcePasswordChange()
    const storage = (event: StorageEvent) => checkOtherTab(event)
    const foreground = () => { if (document.visibilityState !== 'hidden') revalidateCookie() }
    window.addEventListener(sessionExpiredEvent, expired)
    window.addEventListener(passwordChangeRequiredEvent, required)
    window.addEventListener('storage', storage)
    window.addEventListener('focus', foreground)
    document.addEventListener('visibilitychange', foreground)
    return () => {
      window.removeEventListener(sessionExpiredEvent, expired)
      window.removeEventListener(passwordChangeRequiredEvent, required)
      window.removeEventListener('storage', storage)
      window.removeEventListener('focus', foreground)
      document.removeEventListener('visibilitychange', foreground)
    }
  }, [])

  useEffect(() => {
    const expiresAt = accessTokenExpiresAt(token)
    if (!expiresAt) return
    const expireWhenDue = () => { if (Date.now() >= expiresAt) expireAccessToken(token) }
    const timeout = window.setTimeout(expireWhenDue, Math.max(0, expiresAt - Date.now()))
    window.addEventListener('focus', expireWhenDue)
    document.addEventListener('visibilitychange', expireWhenDue)
    return () => {
      window.clearTimeout(timeout)
      window.removeEventListener('focus', expireWhenDue)
      document.removeEventListener('visibilitychange', expireWhenDue)
    }
  }, [token, identityToken])

  async function loadMargins(page: 'margins' | 'net-margin', filters: DashboardFilters) {
    if (!token) return
    const requestId = ++marginRequestId.current
    const applied = { ...filters, movementType: '' }
    setMarginFilters(applied)
    setMarginData(null)
    setMarginState('loading')
    try {
      const result = await apiRequest<MarginReport | NetMarginReport>(`/api/${page}/details?${filterQuery(applied)}`, token)
      if (requestId !== marginRequestId.current) return
      setMarginData(result)
      setMarginState('ready')
    } catch {
      if (requestId === marginRequestId.current) setMarginState('error')
    }
  }

  async function loadPayroll(month: string, coverageSeller: string) {
    if (!token) return
    const requestId = ++payrollRequestId.current
    setPayroll(null)
    setPayrollState('loading')
    setPayrollError(null)
    setExportError(null)
    setExporting(false)
    try {
      const query = new URLSearchParams({ month, coverageSeller })
      const result = await apiRequest<PayrollClosing>(`/api/closings/payroll?${query}`, token)
      if (requestId !== payrollRequestId.current) return
      setPayroll(result)
      setPayrollState('ready')
    } catch (error) {
      if (requestId !== payrollRequestId.current) return
      setPayrollError(error instanceof Error ? error.message : 'Não foi possível consultar a folha de pagamento.')
      setPayrollState('error')
    }
  }

  async function exportPayroll() {
    if (!token || !payroll || payrollState !== 'ready' || exporting) return
    const requestId = payrollRequestId.current
    const month = `${payroll.year}-${String(payroll.month).padStart(2, '0')}`
    setExporting(true)
    setExportError(null)
    try {
      const query = new URLSearchParams({ month, coverageSeller: payroll.coverageSeller })
      const response = await authenticatedFetch(`/api/closings/payroll/export?${query}`, token)
      if (!response.ok) throw new Error(`Não foi possível exportar a folha (erro ${response.status}).`)
      const blob = await response.blob()
      if (requestId !== payrollRequestId.current) return
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `fechamento-rh-${month}.xlsx`
      document.body.append(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (error) {
      if (requestId === payrollRequestId.current) setExportError(error instanceof Error ? error.message : 'Não foi possível exportar a folha.')
    } finally {
      if (requestId === payrollRequestId.current) setExporting(false)
    }
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
    if (!token) return
    let active = true
    const version = sessionVersion.current
    const current = () => active && version === sessionVersion.current

    void apiRequest<CurrentUser>(portalRoute ? '/api/v1/me' : '/api/me', token).then(user => {
      if (!current()) return
      if (cookieSessionMode) confirmCookieSession(token, user.expiresAtUtc)
      const userRoles = Array.isArray(user.roles) ? user.roles : []
      setRoles(userRoles)
      setIdentityEmail(user.email ?? '')
      setIdentityToken(token)
      setIdentityError('')
      setMustChangePassword(user.mustChangePassword === true)
      if (user.mustChangePassword || portalRoute) return
      if (userRoles.some(role => ['Vendedor', 'Gestor', 'Gerente'].includes(role)) && !userRoles.some(role => ['Administrador', 'Diretoria'].includes(role))) { openPortal(); return }
      void loadDashboard(appliedFiltersRef.current)
      void apiRequest<string[]>('/api/sellers', token).then(result => { if (current()) setSellers(Array.isArray(result) ? result : []) }).catch(() => { if (current()) setSellers([]) })
      void apiRequest<DashboardFilterOptions>('/api/dashboard/filter-options', token).then(result => { if (current()) setDashboardFilterOptions({
      brands: Array.isArray(result.brands) ? result.brands : [],
      groups: Array.isArray(result.groups) ? result.groups : [],
      cities: Array.isArray(result.cities) ? result.cities : [],
      movementTypes: Array.isArray(result.movementTypes) ? result.movementTypes : [],
      }) }).catch(() => { if (current()) setDashboardFilterOptions(emptyDashboardFilterOptions) })
    }).catch(() => { if (current()) { setRoles([]); setIdentityError('Não foi possível validar seu acesso. Verifique sua conexão e tente novamente.'); setState('error') } })
    return () => { active = false }
  }, [token, portalRoute, loadDashboard, openPortal, identityRevision])

  useEffect(() => {
    if (!token || identityToken !== token || mustChangePassword || portalRoute || (view !== 'trades' && view !== 'sales-trades')) return

    let active = true
    const version = sessionVersion.current
    setTradeAnalysis(null)
    setAnalysisState('loading')
    const query = filterQuery(dashboardFilters)
    void apiRequest<TradeAnalysis>(`/api/trade-analysis${query ? `?${query}` : ''}`, token)
      .then(result => { if (active && version === sessionVersion.current) { setTradeAnalysis(result); setAnalysisState('ready') } })
      .catch(() => { if (active && version === sessionVersion.current) setAnalysisState('error') })
    return () => { active = false }
  }, [token, identityToken, mustChangePassword, portalRoute, view, dashboardFilters])

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const version = ++sessionVersion.current
    setSessionMessage('')
    setLoginError('')
    setState('loading')
    try {
      const result = await requestLogin(email, password)
      if (version !== sessionVersion.current) return
      acceptLogin(result, rememberSession)
      try { window.localStorage.setItem('orobi:last-email', email.trim()) } catch { /* Login also works with local storage disabled. */ }
    } catch (error) {
      if (version === sessionVersion.current) {
        setLoginError(error instanceof Error ? error.message : 'Não foi possível entrar. Tente novamente.')
        setState('error')
      }
    }
  }

  async function requestLogin(loginEmail: string, loginPassword: string): Promise<LoginResponse> {
    const generation = cookieSessionMode ? startCookieSession(!!token) : ''
    cookieAuthenticationPending.current = cookieSessionMode
    try {
      const response = await fetch(`${apiBaseUrl}/api/auth/login`, sessionRequestInit({
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: loginEmail.trim(), password: loginPassword }),
      })).catch(() => { throw new Error('Não foi possível entrar. Verifique sua conexão e tente novamente.') })
      if (!response.ok) throw new Error(response.status === 401 || response.status === 403
        ? 'Usuário ou senha incorretos.'
        : response.status === 429 ? 'Muitas tentativas. Aguarde alguns minutos e tente novamente.'
          : 'Não foi possível entrar. Verifique sua conexão e tente novamente.')
      return await response.json() as LoginResponse
    } catch (error) {
      if (cookieSessionMode) clearAccessToken(generation)
      throw error
    } finally { cookieAuthenticationPending.current = false }
  }

  function acceptLogin(result: LoginResponse, remember: boolean) {
    let nextToken: string
    if (cookieSessionMode) {
      if (result.sessionMode !== 'cookie') throw new Error('Não foi possível estabelecer a sessão corporativa.')
      nextToken = startCookieSession(true)
      confirmCookieSession(nextToken, result.expiresAtUtc)
      notifyCookieSessionChanged()
    } else {
      if (!result.accessToken) throw new Error('Não foi possível estabelecer sua sessão.')
      nextToken = result.accessToken
      saveAccessToken(nextToken, { remember, expiresAtUtc: result.expiresAtUtc })
    }
    setLogoutFailed(false)
    setIdentityToken('')
    setIdentityError('')
    setMustChangePassword(result.mustChangePassword === true)
    setToken(nextToken)
    setPassword('')
    setState('idle')
    if (result.roles?.some(role => ['Vendedor', 'Gestor', 'Gerente'].includes(role)) && !result.roles.some(role => ['Administrador', 'Diretoria'].includes(role))) openPortal()
  }

  async function completePasswordChange(newPassword: string) {
    const version = ++sessionVersion.current
    const remember = isRememberedSession(token)
    try {
      const result = await requestLogin(identityEmail || email, newPassword)
      if (version === sessionVersion.current) acceptLogin(result, remember)
    } catch {
      if (version === sessionVersion.current) endSession('Senha alterada. Entre novamente com a nova senha.')
    }
  }

  async function upload() {
    if (!file) return
    const version = sessionVersion.current
    setState('loading')
    try {
      const form = new FormData()
      form.append('fileType', fileType)
      form.append('file', file)
      await apiRequest('/api/imports', token, { method: 'POST', body: form })
      if (version !== sessionVersion.current) return
      setFile(null)
      setState('ready')
      setView('dashboard')
      await loadDashboard(appliedFiltersRef.current)
    } catch {
      if (version === sessionVersion.current) setState('error')
    }
  }

  function selectImportFile(nextFile: File | null) {
    setFile(nextFile)
    if (nextFile && /^VALOR[_ -]?METAS\.csv$/i.test(nextFile.name)) setFileType('GoalValues')
  }

  if (logoutPending) return <main className="shell login-shell"><section className="first-access-panel login-form-panel"><p role="status">Encerrando sua sessão no servidor...</p></section></main>

  if (!token && registrationOpen) return <main className="shell login-shell"><section className="login-layout registration-layout shadow-lg"><aside className="login-brand-panel"><img className="login-brand-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" /><div><p className="eyebrow">PORTAL DO VENDEDOR</p><h1>Seus resultados.<br /><span>Seu espaço.</span></h1><p>Acompanhe suas vendas, metas e fechamento em um só lugar.</p></div><p className="login-brand-footer">Acesso liberado após aprovação do administrador.</p></aside><section className="login-form-panel"><RegistrationForm onBack={() => setRegistrationOpen(false)} onAccepted={(message, registeredEmail) => { setRegistrationOpen(false); setEmail(registeredEmail); setSessionMessage(message); setState('idle') }} /></section></section></main>
  if (!token) return <main className="shell login-shell"><section className="login-layout shadow-lg"><aside className="login-brand-panel"><img className="login-brand-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" /><div><p className="eyebrow">OROLEITE BI</p><h1 aria-label="Central de resultados">Central de<br /><span>resultados.</span></h1><p>Inteligencia comercial para decisoes mais seguras, todos os dias.</p></div><p className="login-brand-footer"><i className="fa-solid fa-shield-halved" aria-hidden="true" /> Ambiente corporativo protegido</p></aside><section className="login-form-panel"><div className="login-form-heading"><p className="eyebrow">ACESSO RESTRITO</p><h2>Bem-vindo de volta.</h2><p>Informe suas credenciais para acessar os indicadores da operacao.</p></div>{sessionMessage && <p className="notice" role="status">{sessionMessage}</p>}{logoutFailed && <button type="button" className="registration-link" onClick={logout}>Tentar sair novamente</button>}<form onSubmit={login}><label>E-MAIL<input type="email" autoComplete="username" required value={email} onChange={event => setEmail(event.target.value)} /></label><label>SENHA<span className="password-field"><input type={passwordVisible ? 'text' : 'password'} autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /><button type="button" className="password-toggle" onClick={() => setPasswordVisible(visible => !visible)} aria-label={passwordVisible ? 'Ocultar senha' : 'Mostrar senha'}><i className={`fa-solid ${passwordVisible ? 'fa-eye-slash' : 'fa-eye'}`} aria-hidden="true" /></button></span></label>{cookieSessionMode ? <p className="remember-help">Seu acesso permanece neste dispositivo por até 8 horas. Use Sair ao terminar em um dispositivo compartilhado.</p> : <><label className="remember-session"><input type="checkbox" checked={rememberSession} onChange={event => setRememberSession(event.target.checked)} /><span>Manter acesso neste dispositivo por até 8 horas</span></label><p className="remember-help">Use esta opção somente no seu dispositivo pessoal.</p></>}<button className="btn btn-dark" type="submit" disabled={state === 'loading'}>{state === 'loading' ? 'Entrando...' : 'Entrar'} <i className="fa-solid fa-arrow-right" aria-hidden="true" /></button></form>{state === 'error' && <p className="notice error" role="alert">{loginError || 'Não foi possível entrar. Verifique sua conexão e tente novamente.'}</p>}<button type="button" className="registration-link" onClick={() => setSessionMessage('Solicite ao administrador uma nova senha temporária. No próximo acesso, você deverá criar sua própria senha.')}>Esqueci minha senha</button><button type="button" className="registration-link" disabled={state === 'loading'} onClick={() => { setPassword(''); setPasswordVisible(false); setSessionMessage(''); setState('idle'); setRegistrationOpen(true) }}>Criar minha conta</button></section></section></main>

  if (mustChangePassword) return <main className="shell login-shell"><section className="first-access-panel login-form-panel">
    <img className="first-access-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" />
    <p className="eyebrow">PRIMEIRO ACESSO</p><h1>Crie sua nova senha</h1>
    <p>Antes de acessar seus resultados, substitua a senha temporária por uma senha que só você conhece.</p>
    <ChangePasswordForm key={token} token={token} requiredChange onChanged={completePasswordChange} />
    <button type="button" className="registration-link" onClick={logout}>Sair</button>
  </section></main>

  if (identityToken !== token) return <main className="shell login-shell"><section className="first-access-panel login-form-panel">
    {identityError ? <><p className="notice error" role="alert">{identityError}</p><button type="button" onClick={() => { setIdentityError(''); setIdentityRevision(value => value + 1) }}>Tentar novamente</button></> : <p role="status">Validando seu acesso...</p>}
    <button type="button" className="registration-link" onClick={logout}>Sair</button>
  </section></main>
  if (portalRoute) return <Suspense fallback={<main className="shell" role="status">Abrindo portal...</main>}><SellerPortal token={token} onLogout={logout} onSessionEnd={endSession} onAdmin={() => { setPortalRoute(false); window.history.replaceState({}, '', '/') }} /></Suspense>

  if (view === 'import') return <main className="shell import-workspace"><ImportPage file={file} fileType={fileType} state={state} onBack={() => setView('dashboard')} onFileChange={selectImportFile} onFileTypeChange={setFileType} onSubmit={() => void upload()} /></main>

  return <main className="executive-layout">
    <aside className={`side-rail ${menuOpen ? 'is-open' : ''}`}><div className="brand"><img className="brand-logo" src="/logoOroleite.png" alt="Oroleite Distribuidora" /></div><p className="rail-label">CENTRAL DE RESULTADOS</p><nav id="main-navigation" className="side-navigation" aria-label="Modulos do BI">{navigationItems.map(item => <button className={view === item.view ? 'active' : ''} key={item.view} onClick={() => navigate(item.view)}><i className={`fa-solid ${item.icon}`} aria-hidden="true" /><span>{item.label}</span></button>)}</nav><div className="rail-footer"><i className="fa-solid fa-circle-check" aria-hidden="true" /> Dados sincronizados</div></aside>
    <section className={`main-canvas ${view === 'dashboard' ? 'dashboard-workspace' : 'analysis-workspace'}`}><header className="command-bar"><button className="menu-toggle" type="button" aria-label="Alternar navegacao" aria-controls="main-navigation" aria-expanded={menuOpen} onClick={() => setMenuOpen(open => !open)}><i className="fa-solid fa-bars" aria-hidden="true" /></button><div><p>PAINEL EXECUTIVO</p><strong>{navigationItems.find(item => item.view === view)?.label ?? 'Importar'}</strong></div><div className="command-actions"><button className="btn btn-ghost" onClick={openPortal}>Portal do vendedor</button>{roles.includes('Administrador') && <button className="btn btn-accent" onClick={() => { clearClosingRequests(); setView('import') }}><i className="fa-solid fa-file-arrow-up" aria-hidden="true" /> Importar</button>}<button className="btn btn-ghost" onClick={logout} aria-label="Sair"><i className="fa-solid fa-right-from-bracket" aria-hidden="true" /></button></div></header>
      {view === 'dashboard' && <DashboardPage summary={summary} details={dashboardDetails} filters={dashboardDraft} appliedFilters={dashboardFilters} options={dashboardFilterOptions} sellers={sellers} state={state} onFiltersChange={setDashboardDraft} onSubmit={() => applyDashboardFilter(dashboardDraft)} onClear={clearDashboardFilters} />}
      {(view === 'margins' || view === 'net-margin') && <MarginAnalysisPage key={view} mode={view === 'margins' ? 'products' : 'net'} data={marginData} state={marginState} filters={marginFilters} options={dashboardFilterOptions} sellers={sellers} onSubmit={filters => void loadMargins(view, filters)} />}
      {(view === 'trades' || view === 'sales-trades') && <TradeAnalysisPage mode={view} data={tradeAnalysis} state={analysisState} />}
      {view === 'closing-rh' && <><PayrollClosingPage summary={payroll} state={payrollState} errorMessage={payrollError} initialMonth={createDashboardFilters().startDate.slice(0, 7)} onSubmit={(month, coverage) => void loadPayroll(month, coverage)} onExport={() => void exportPayroll()} exporting={exporting} />{exportError && <p className="notice error" role="alert">{exportError}</p>}</>}
      {(['closings', 'closing-supervisor', 'closing-valdir'] as View[]).includes(view) && <ClosingsPage key={view} title={navigationItems.find(item => item.view === view)?.label} summary={closing} sellers={sellers} state={closingState} errorMessage={closingError} initialSeller={specialClosingSellers[view]} initialMonth={specialClosingSellers[view] ? createDashboardFilters().startDate.slice(0, 7) : ''} onSubmit={(activeSeller, month) => void loadClosing(activeSeller, month)} />}
    </section>
  </main>
}
