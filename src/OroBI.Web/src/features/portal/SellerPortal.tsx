import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest } from '../../api/client'
import type { PortalClosing, PortalCustomerDetail, PortalCustomers, PortalDashboard, PortalFilters, PortalGoals, PortalIdentity, PortalPage, PortalPermissions, PortalPpp, PortalRanking, PortalSale, PortalTrades } from './portalTypes'
import { Empty, Resource } from './PortalShared'
import { currentMonth, filterQuery, monthFilters, presetFilters } from './portalFormatting'
import { Closing, ClosingHistory, CustomerDetail, Customers, Goals, HomeMonthly, PersonalDashboard, Ppp, Ranking, Sales, Trades } from './PortalResults'
import PortalAccounts from './PortalAccounts'
import { permissionLabels } from './portalTypes'
import './portal.css'

type View = 'dashboard' | 'sales' | 'customers' | 'products' | 'brands' | 'goals' | 'ppp' | 'trades' | 'commission' | 'closings' | 'profile' | 'accounts'
type ScopedSeller = { sellerId: string; name: string; permissions: PortalPermissions }
const modules: Array<{ id: View; label: string; title: string; icon: string; permission?: keyof PortalPermissions }> = [
  { id: 'dashboard', label: 'Início', title: 'Meu desempenho', icon: 'fa-chart-simple' },
  { id: 'sales', label: 'Vendas', title: 'Minhas vendas', icon: 'fa-receipt', permission: 'canViewRevenue' },
  { id: 'customers', label: 'Clientes', title: 'Meus clientes', icon: 'fa-users', permission: 'canViewCustomers' },
  { id: 'products', label: 'Produtos', title: 'Meus produtos', icon: 'fa-box', permission: 'canViewRevenue' },
  { id: 'brands', label: 'Marcas', title: 'Minhas marcas', icon: 'fa-tags', permission: 'canViewRevenue' },
  { id: 'goals', label: 'Metas e prêmios', title: 'Metas e prêmios', icon: 'fa-bullseye', permission: 'canViewGoals' },
  { id: 'ppp', label: 'PPP', title: 'Meu PPP', icon: 'fa-layer-group', permission: 'canViewPPP' },
  { id: 'trades', label: 'Trocas', title: 'Minhas trocas', icon: 'fa-arrow-right-arrow-left', permission: 'canViewTrades' },
  { id: 'commission', label: 'Comissão', title: 'Minha comissão', icon: 'fa-coins', permission: 'canViewCommission' },
  { id: 'closings', label: 'Fechamento', title: 'Meu fechamento', icon: 'fa-wallet' },
  { id: 'profile', label: 'Perfil', title: 'Meu perfil', icon: 'fa-user' },
  { id: 'accounts', label: 'Acessos', title: 'Gestão de acessos', icon: 'fa-user-shield' },
]
type Props = { token: string; onLogout: () => void; onSessionEnd: (message?: string) => void; onAdmin: () => void }
export default function SellerPortal(props: Props) { return <main className="seller-portal"><header className="portal-header"><img src="/logoOroleite.png" alt="Oroleite Distribuidora" /><div><small>PORTAL DO VENDEDOR</small><strong>Seu espaço comercial</strong></div><button onClick={props.onLogout} aria-label="Sair"><i className="fa-solid fa-right-from-bracket" aria-hidden="true" /></button></header><Resource<PortalIdentity> token={props.token} path="/api/v1/me">{identity => <PortalWorkspace {...props} identity={identity} />}</Resource></main> }
function PortalWorkspace({ token, onSessionEnd, onAdmin, identity }: Props & { identity: PortalIdentity }) {
  const [view, setView] = useState<View>('dashboard')
  const [more, setMore] = useState(false)
  const [offline, setOffline] = useState(!navigator.onLine)
  const [sellers, setSellers] = useState<ScopedSeller[]>([])
  const [sellerId, setSellerId] = useState('')
  const [scopeError, setScopeError] = useState('')
  const [scopeRetry, setScopeRetry] = useState(0)
  const [month, setMonth] = useState(currentMonth)
  const [filters, setFilters] = useState(monthFilters)
  const [draft, setDraft] = useState<PortalFilters>(filters)
  const [filterError, setFilterError] = useState('')
  const filterPanel = useRef<HTMLDetailsElement>(null)
  const [page, setPage] = useState(1)
  const [customer, setCustomer] = useState('')
  const contentPanel = useRef<HTMLDivElement>(null)
  useEffect(() => { if (contentPanel.current) contentPanel.current.scrollTop = 0 }, [view, customer])
  const [revision, setRevision] = useState(0)
  const isSeller = identity.roles.includes('Vendedor') && !identity.roles.some(role => ['Administrador', 'Diretoria', 'Gestor', 'Gerente'].includes(role))
  const isAdmin = identity.roles.includes('Administrador')
  const canOpenAdmin = identity.roles.some(role => ['Administrador', 'Diretoria'].includes(role))
  const selected = sellers.find(seller => seller.sellerId === sellerId)
  const permissions = isSeller ? identity.permissions : selected?.permissions
  const base = isSeller ? '/api/v1/me' : sellerId ? `/api/v1/management/sellers/${encodeURIComponent(sellerId)}` : ''
  function moduleAllowed(item: typeof modules[number]) {
    if (item.id === 'accounts') return isAdmin
    if (item.id === 'sales' || item.id === 'customers') return !!permissions?.canViewRevenue && !!permissions?.canViewCustomers
    if (item.id === 'trades') return !!permissions?.canViewTrades && !!permissions?.canViewRevenue && !!permissions?.canViewCustomers
    if (item.id === 'closings') return !!permissions?.canViewCommission || !!permissions?.canViewPrize
    return !item.permission || !!permissions?.[item.permission]
  }
  const allowed = modules.filter(moduleAllowed)
  const selectedModule = modules.find(item => item.id === view)!
  const canRead = moduleAllowed(selectedModule)
  useEffect(() => { const off = () => setOffline(true); const on = () => { setOffline(false); setRevision(value => value + 1) }; window.addEventListener('offline', off); window.addEventListener('online', on); return () => { window.removeEventListener('offline', off); window.removeEventListener('online', on) } }, [])
  useEffect(() => {
    if (isSeller) return
    const abort = new AbortController()
    void apiRequest<ScopedSeller[]>('/api/v1/management/sellers', token, { signal: abort.signal }).then(value => { if (!abort.signal.aborted) { setScopeError(''); setSellers(value) } }).catch(error => { if (!abort.signal.aborted) setScopeError(error instanceof Error ? error.message : 'Não foi possível consultar seus vínculos.') })
    return () => abort.abort()
  }, [token, isSeller, scopeRetry])
  function navigate(next: View) { setView(next); setMore(false); setCustomer(''); setPage(1) }
  function apply(event: FormEvent) { event.preventDefault(); if (!draft.startDate || !draft.endDate || draft.startDate > draft.endDate) { setFilterError('Informe um intervalo de datas válido.'); return }; setFilterError(''); setFilters({ ...draft }); setPage(1); setCustomer('') }
  const query = filterQuery(filters)
  const monthly = ['goals', 'ppp', 'commission', 'closings'].includes(view)
  const showFilters = !monthly && !['profile', 'accounts'].includes(view)
  const navButton = (item: typeof modules[number]) => <button key={item.id} className={view === item.id ? 'active' : ''} aria-current={view === item.id ? 'page' : undefined} onClick={() => navigate(item.id)}><i className={`fa-solid ${item.icon}`} aria-hidden="true" /><span>{item.label}</span></button>
  return <>{offline && <p role="status" className="portal-offline">Você está offline. Os resultados precisam de conexão para serem atualizados.</p>}<div className="portal-layout"><aside className="portal-desktop-nav"><p>SEUS RESULTADOS</p><nav aria-label="Navegação do portal">{allowed.map(navButton)}</nav>{canOpenAdmin && <button onClick={onAdmin}>Painel administrativo <i className="fa-solid fa-arrow-up-right-from-square" aria-hidden="true" /></button>}</aside><div ref={contentPanel} className="portal-content"><div className="portal-heading"><div><p>{isSeller ? identity.seller ?? identity.userName ?? 'OROLEITE · RESULTADOS PESSOAIS' : selected?.name ?? 'OROLEITE · VISÃO GERENCIAL'}</p><h1>{selectedModule.title}</h1></div>{monthly && <label>Mês de referência<input type="month" value={month} onChange={event => { if (event.target.value) setMonth(event.target.value) }} /></label>}</div>{!isSeller && <div className="portal-scope"><label>Vendedor vinculado<select value={sellerId} onChange={event => { setSellerId(event.target.value); setCustomer(''); setPage(1) }}><option value="">Selecione um vendedor</option>{sellers.map(seller => <option key={seller.sellerId} value={seller.sellerId}>{seller.name}</option>)}</select></label>{scopeError && <div role="alert"><p>{scopeError}</p><button onClick={() => setScopeRetry(value => value + 1)}>Atualizar vínculos</button></div>}{sellers.length === 0 && !scopeError && <p>Nenhum vendedor disponível no seu escopo.</p>}</div>}{showFilters && base && canRead && <><div className="portal-shortcuts" aria-label="Atalhos de período">{['Hoje', 'Ontem', 'Semana', 'Mês', 'Últimos 30 dias'].map(label => <button key={label} onClick={() => { const range = { ...presetFilters(label), customerContains: filters.customerContains, productContains: filters.productContains, brand: filters.brand }; setFilters(range); setDraft(range); setPage(1); setCustomer(''); setFilterError('') }}>{label}</button>)}<button onClick={() => { if (filterPanel.current) filterPanel.current.open = true }}>Personalizado</button></div><details ref={filterPanel} className="portal-filters" open={view === 'sales'}><summary><i className="fa-solid fa-sliders" aria-hidden="true" /> Período e filtros</summary><form onSubmit={apply}><label>Data inicial<input type="date" required value={draft.startDate} onChange={event => setDraft({ ...draft, startDate: event.target.value })} /></label><label>Data final<input type="date" required value={draft.endDate} onChange={event => setDraft({ ...draft, endDate: event.target.value })} /></label><label>Cliente<input value={draft.customerContains} onChange={event => setDraft({ ...draft, customerContains: event.target.value })} /></label><label>Produto<input value={draft.productContains} onChange={event => setDraft({ ...draft, productContains: event.target.value })} /></label><label>Marca<input value={draft.brand} onChange={event => setDraft({ ...draft, brand: event.target.value })} /></label><button>Aplicar filtros</button><button type="button" className="portal-secondary" onClick={() => { const reset = monthFilters(); setFilters(reset); setDraft(reset); setPage(1); setCustomer(''); setFilterError('') }}>Limpar filtros</button></form>{filterError && <p role="alert">{filterError}</p>}</details></>}
    {view === 'profile' ? <Profile identity={identity} token={token} onSessionEnd={onSessionEnd} /> : view === 'accounts' && isAdmin ? <PortalAccounts token={token} /> : !base ? <Empty>Selecione um vendedor vinculado para consultar seus resultados.</Empty> : !canRead ? <Empty>Você não tem permissão para consultar este indicador.</Empty> : <div key={`${base}-${view}-${revision}`}>
      {view === 'dashboard' && <div className="portal-home-order">{permissions?.canViewRevenue && <Resource<PortalDashboard> token={token} path={`${base}/dashboard?${query}`}>{data => <PersonalDashboard data={data} />}</Resource>}<HomeMonthly token={token} base={base} permissions={permissions} /></div>}
      {view === 'sales' && <Resource<PortalPage<PortalSale>> token={token} path={`${base}/sales?${query}&page=${page}&pageSize=20`}>{data => <Sales data={data} onPage={setPage} />}</Resource>}
      {view === 'customers' && (customer ? <><button className="portal-back" onClick={() => setCustomer('')}><i className="fa-solid fa-arrow-left" aria-hidden="true" /> Voltar aos clientes</button><Resource<PortalCustomerDetail> token={token} path={`${base}/customers/${encodeURIComponent(customer)}?${query}`}>{data => <CustomerDetail data={data} />}</Resource></> : <Resource<PortalCustomers> token={token} path={`${base}/customers?${query}`}>{data => <Customers data={data} onSelect={setCustomer} />}</Resource>)}
      {(view === 'products' || view === 'brands') && <Resource<PortalRanking> token={token} path={`${base}/${view}?${query}`}>{data => <Ranking data={data} />}</Resource>}
      {view === 'goals' && <Resource<PortalGoals> token={token} path={`${base}/goals?month=${month}`}>{data => <Goals data={data} />}</Resource>}
      {view === 'ppp' && <Resource<PortalPpp> token={token} path={`${base}/ppp?month=${month}`}>{data => <Ppp data={data} />}</Resource>}
      {view === 'trades' && <Resource<PortalTrades> token={token} path={`${base}/trades?${query}`}>{data => <Trades data={data} />}</Resource>}
      {(view === 'commission' || view === 'closings') && <><Resource<PortalClosing> token={token} path={`${base}/${view}?month=${month}`}>{data => <Closing data={data} commissionOnly={view === 'commission'} token={token} base={base} month={month} canApprove={isAdmin && !isSeller} onChanged={() => setRevision(value => value + 1)} />}</Resource>{view === 'closings' && <ClosingHistory token={token} base={base} onSelect={setMonth} />}</>}
    </div>}</div></div>{more && <section className="portal-more" aria-label="Mais módulos"><div className="portal-record-top"><h2>Explorar resultados</h2><button onClick={() => setMore(false)} aria-label="Fechar menu"><i className="fa-solid fa-xmark" aria-hidden="true" /></button></div><nav>{allowed.filter(item => !['dashboard', 'sales', 'profile'].includes(item.id)).map(navButton)}</nav>{canOpenAdmin && <button onClick={onAdmin}>Painel administrativo</button>}</section>}<nav className="portal-bottom-nav" aria-label="Navegação rápida">{allowed.filter(item => ['dashboard', 'sales'].includes(item.id)).map(navButton)}<button className={more ? 'active' : ''} onClick={() => setMore(value => !value)} aria-expanded={more}><i className="fa-solid fa-grid-2 fa-bars" aria-hidden="true" /><span>Mais</span></button>{navButton(modules.find(item => item.id === 'profile')!)}</nav></>
}
function Profile({ identity, token, onSessionEnd }: { identity: PortalIdentity; token: string; onSessionEnd: (message?: string) => void }) {
  const active = useRef(true)
  useEffect(() => { active.current = true; return () => { active.current = false } }, [])
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function changePassword(event: FormEvent) { event.preventDefault(); setBusy(true); setError(''); try { await apiRequest('/api/v1/me/change-password', token, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ currentPassword, newPassword }) }); if (!active.current) return; setCurrentPassword(''); setNewPassword(''); onSessionEnd('Senha alterada. Entre novamente com a nova senha.') } catch (reason) { setError(reason instanceof Error ? reason.message : 'Não foi possível alterar a senha.'); setCurrentPassword(''); setNewPassword('') } finally { setBusy(false) } }
  return <div className="portal-profile"><section className="portal-panel"><h2>Sua conta</h2><dl className="portal-values"><div><dt>E-mail</dt><dd>{identity.email}</dd></div><div><dt>Perfil</dt><dd>{identity.roles.join(', ')}</dd></div>{identity.seller && <div><dt>Vendedor</dt><dd>{identity.seller}</dd></div>}</dl>{identity.permissions && <><h3>Indicadores autorizados</h3><div className="portal-permission-tags">{Object.entries(permissionLabels).filter(([key]) => identity.permissions?.[key as keyof PortalPermissions]).map(([key, label]) => <span key={key}>{label}</span>)}</div></>}</section><section className="portal-panel"><h2>Alterar senha</h2><p className="portal-help">Após salvar, entre novamente. Suas outras sessões também serão encerradas.</p><form className="portal-form" onSubmit={changePassword}><label>Senha atual<input required type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label><label>Nova senha<input required minLength={8} type="password" autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label><button disabled={busy}>Alterar senha</button></form>{error && <p className="portal-message" role="alert">{error}</p>}</section></div>
}
