import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiRequest } from '../../api/client'
import { Empty, Resource } from './PortalShared'
import { dateTime } from './portalFormatting'
import type { PortalPermissions } from './portalTypes'

type Seller = { id: string; name: string; importedName: string; externalId?: string | null; isActive: boolean }
type SellerAccess = { sellerId: string; name?: string; isActive: boolean; permissions: PortalPermissions }
type User = { id: string; email: string; isActive: boolean; roles: string[]; sellerAccesses: SellerAccess[]; registrationName?: string | null; lastLoginAtUtc?: string | null; mustChangePassword?: boolean; isRegistrationPending?: boolean }
import { permissionLabels } from './portalTypes'
const allPermissions: PortalPermissions = { canViewRevenue: true, canViewCommission: true, canViewPrize: true, canViewPPP: true, canViewGoals: true, canViewTrades: true, canViewCustomers: true }

function PendingRegistrations({ token, sellers, users, onChanged }: { token: string; sellers: Seller[]; users: User[]; onChanged: () => void }) {
  const pending = users.filter(user => user.isRegistrationPending)
  const availableSellers = sellers.filter(seller => seller.isActive)
  const [selected, setSelected] = useState<User | null>(null)
  const [sellerId, setSellerId] = useState('')
  const [permissions, setPermissions] = useState<PortalPermissions>({ ...allPermissions })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  function select(user: User) { setSelected(user); setSellerId(''); setPermissions({ ...allPermissions }); setError('') }
  async function approve(event: FormEvent) {
    event.preventDefault()
    if (busy || !selected || !availableSellers.some(seller => seller.id === sellerId)) return
    setBusy(true); setError('')
    try {
      await apiRequest(`/api/v1/admin/users/${selected.id}/approve-registration`, token, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ sellerId, permissions }),
      })
      onChanged()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Não foi possível aprovar o cadastro.') }
    finally { setBusy(false) }
  }

  return <section className="portal-panel portal-pending" aria-label="Cadastros aguardando aprovação">
    <div className="portal-record-top"><h2>Cadastros aguardando aprovação</h2><span className="portal-badge">{pending.length}</span></div>
    <p className="portal-help">Confira nome e e-mail. Cada cadastro é liberado como Vendedor, vinculado a um único vendedor existente.</p>
    {pending.length === 0 ? <p className="portal-help">Nenhum cadastro pendente de aprovação.</p> : <div className="portal-list">{pending.map(user => <button type="button" className="portal-record" key={user.id} disabled={busy} aria-label={`Analisar cadastro de ${user.registrationName || user.email}`} onClick={() => select(user)}><strong>{user.registrationName || 'Nome não informado'}</strong><small>{user.email}</small><span className="portal-pending-status">Aguardando aprovação</span></button>)}</div>}
    {selected && <section className="portal-registration-review" aria-label={`Aprovar cadastro de ${selected.registrationName || selected.email}`}>
      <h3>{selected.registrationName || 'Solicitação de cadastro'}</h3><p className="portal-help">{selected.email} · Perfil após aprovação: Vendedor</p>
      <form className="portal-form" onSubmit={approve}>
        <label>Vendedor para este cadastro<select required value={sellerId} disabled={busy || availableSellers.length === 0} onChange={event => setSellerId(event.target.value)}><option value="">Selecione um vendedor existente</option>{availableSellers.map(seller => <option key={seller.id} value={seller.id}>{seller.externalId && `${seller.externalId} · `}{seller.name} · {seller.importedName}</option>)}</select></label>
        {availableSellers.length === 0 && <p className="portal-message">Cadastre ou ative um vendedor antes de aprovar esta solicitação.</p>}
        <fieldset disabled={busy}><legend>Indicadores autorizados</legend><div className="portal-permissions">{Object.entries(permissionLabels).map(([key, label]) => <label className="portal-check" key={key}><input type="checkbox" checked={permissions[key as keyof PortalPermissions]} onChange={event => setPermissions(current => ({ ...current, [key]: event.target.checked }))} />{label}</label>)}</div></fieldset>
        {error && <div className="portal-message" role="alert"><p>{error}</p><button type="button" className="portal-secondary" onClick={onChanged}>Atualizar lista</button></div>}
        <div className="portal-actions"><button disabled={busy || !sellerId}>{busy ? 'Aprovando cadastro...' : 'Aprovar cadastro'}</button><button type="button" className="portal-secondary" disabled={busy} onClick={() => setSelected(null)}>Cancelar análise</button></div>
      </form>
    </section>}
  </section>
}

type AccountProps = { token: string; onChanged?: () => void }
export default function PortalAccounts(props: AccountProps) {
  return <AccountWorkspace key={props.token} {...props} />
}
function AccountWorkspace({ token, onChanged }: AccountProps) {
  const [revision, setRevision] = useState(0)
  const [credential, setCredential] = useState<{ password: string; email: string } | null>(null)
  return <>{credential && <TemporaryPassword key={credential.password} credential={credential} onDismiss={() => setCredential(null)} />}<Resource<Seller[]> key={revision} token={token} path="/api/v1/admin/sellers">{sellers => <Resource<User[]> token={token} path="/api/v1/admin/users">{users => <AccountForms token={token} onCredential={setCredential} sellers={sellers} users={users} onChanged={() => { setRevision(value => value + 1); onChanged?.() }} />}</Resource>}</Resource></>
}
function AccountForms({ token, sellers, users, onChanged, onCredential }: { token: string; sellers: Seller[]; users: User[]; onChanged: () => void; onCredential: (credential: { password: string; email: string } | null) => void }) {
  const [name, setName] = useState('')
  const [externalId, setExternalId] = useState('')
  const [editedExternalId, setEditedExternalId] = useState('')
  const accountSelect = useRef<HTMLSelectElement>(null)
  const [sellerName, setSellerName] = useState('')
  const [importedName, setImportedName] = useState('')
  const [selectedSeller, setSelectedSeller] = useState<Seller | null>(null)
  const [selectedUser, setSelectedUser] = useState<User | null>(null)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('Vendedor')
  const [accesses, setAccesses] = useState<SellerAccess[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  async function mutate(path: string, method: string, body: unknown, credentialEmail?: string) {
    if (busy) return
    if (credentialEmail) onCredential(null)
    setBusy(true); setMessage('')
    try { const response = await apiRequest<{ temporaryPassword?: string } | undefined>(path, token, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); setPassword(''); if (credentialEmail && response?.temporaryPassword) onCredential({ email: credentialEmail, password: response.temporaryPassword }); onChanged() }
    catch (error) { setPassword(''); setMessage(error instanceof Error ? error.message : 'Não foi possível salvar.') }
    finally { setBusy(false) }
  }
  function selectUser(user: User | null) { setSelectedUser(user); setName(user?.registrationName ?? ''); setEmail(user?.email ?? ''); setPassword(''); setRole(user?.roles[0] ?? 'Vendedor'); setAccesses(user?.sellerAccesses ?? []); setMessage('') }
  function associatedAccount(seller: Seller) { return users.find(user => !user.isRegistrationPending && user.roles.includes('Vendedor') && user.sellerAccesses.some(access => access.sellerId === seller.id)) }
  function openAccess(seller: Seller) {
    const existing = associatedAccount(seller)
    selectUser(existing ?? null)
    if (!existing) setAccesses([{ sellerId: seller.id, isActive: true, permissions: { ...allPermissions } }])
    accountSelect.current?.focus()
    accountSelect.current?.scrollIntoView?.({ behavior: 'smooth', block: 'center' })
  }
  function toggleAccess(sellerId: string, enabled: boolean) { setAccesses(current => enabled ? [...current, { sellerId, isActive: true, permissions: { ...allPermissions } }] : current.filter(access => access.sellerId !== sellerId)) }
  function submitUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (selectedUser) void mutate(`/api/v1/admin/users/${selectedUser.id}/access`, 'PUT', { role, sellerAccesses: accesses })
    else {
      const generate = (event.nativeEvent as SubmitEvent).submitter?.getAttribute('value') === 'generate'
      if (!generate && password.length < 8) { setMessage('Informe uma senha inicial com pelo menos 8 caracteres ou use Gerar senha temporária.'); return }
      void mutate('/api/v1/admin/users', 'POST', { name, email, ...(generate ? { generateTemporaryPassword: true } : { password }), role, sellerAccesses: accesses }, email)
    }
  }
  return <><PendingRegistrations token={token} sellers={sellers} users={users} onChanged={onChanged} /><p className="portal-help">Cadastre identidades e vincule cada conta aos vendedores autorizados. O nome importado deve corresponder ao arquivo de origem.</p>{message && <p className="portal-message" role="alert">{message}</p>}<div className="portal-admin-grid"><section className="portal-panel"><h2>Vendedores</h2><form onSubmit={event => { event.preventDefault(); void mutate('/api/v1/admin/sellers', 'POST', { name: sellerName, importedName, externalId: externalId.trim() || null }) }} className="portal-form"><label>Nome do vendedor<input required value={sellerName} onChange={event => setSellerName(event.target.value)} /></label><label>Nome no arquivo importado<input required value={importedName} onChange={event => setImportedName(event.target.value)} /></label><label>Código do vendedor no ERP<input maxLength={64} value={externalId} onChange={event => setExternalId(event.target.value)} /></label><small>Opcional. Informe o código cadastrado na origem.</small><button disabled={busy}>Cadastrar vendedor</button></form><div className="portal-list">{sellers.map(seller => <button className="portal-record" key={seller.id} aria-label={`Editar ${seller.name}`} onClick={() => { setSelectedSeller(seller); setEditedExternalId(seller.externalId ?? '') }}><strong>{seller.name}</strong><small>{seller.importedName} · {seller.isActive ? 'Ativo' : 'Inativo'} · Código ERP: {seller.externalId || 'Não informado'}</small></button>)}</div>{selectedSeller && <div className="portal-panel"><h3>{selectedSeller.name}</h3><p>Nome importado: {selectedSeller.importedName}</p><form className="portal-form" onSubmit={event => { event.preventDefault(); void mutate(`/api/v1/admin/sellers/${selectedSeller.id}/external-id`, 'PUT', { externalId: editedExternalId.trim() || null }) }}><label>Editar código ERP<input maxLength={64} value={editedExternalId} onChange={event => setEditedExternalId(event.target.value)} /></label><button disabled={busy}>Salvar código ERP</button></form><div className="portal-actions"><button disabled={busy} onClick={() => openAccess(selectedSeller)}>{associatedAccount(selectedSeller) ? `Gerenciar acesso de ${selectedSeller.name}` : `Criar acesso para ${selectedSeller.name}`}</button></div><button disabled={busy} onClick={() => void mutate(`/api/v1/admin/sellers/${selectedSeller.id}/active`, 'PUT', { isActive: !selectedSeller.isActive })}>{selectedSeller.isActive ? 'Desativar vendedor' : 'Ativar vendedor'}</button></div>}</section><section className="portal-panel"><h2>Contas e permissões</h2><label>Conta para editar<select ref={accountSelect} value={selectedUser?.id ?? ''} onChange={event => selectUser(users.find(user => user.id === event.target.value) ?? null)}><option value="">Criar nova conta</option>{users.filter(user => !user.isRegistrationPending).map(user => <option key={user.id} value={user.id}>{user.email} · {user.isActive ? 'Ativa' : 'Inativa'}</option>)}</select></label><form onSubmit={submitUser} className="portal-form"><label>Nome da pessoa<input maxLength={120} disabled={!!selectedUser} autoComplete="off" value={name} onChange={event => setName(event.target.value)} /></label><label>E-mail da conta<input type="email" required disabled={!!selectedUser} autoComplete="off" value={email} onChange={event => setEmail(event.target.value)} /></label>{!selectedUser && <label>Senha inicial<input type="password" autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} /></label>}<label>Perfil de acesso<select value={role} onChange={event => setRole(event.target.value)}>{['Vendedor', 'Gestor', 'Gerente', 'Administrador', 'Diretoria'].map(item => <option key={item}>{item}</option>)}</select></label><fieldset><legend>Vendedores autorizados</legend>{sellers.length === 0 && <Empty>Cadastre um vendedor para criar vínculos.</Empty>}{sellers.map(seller => { const access = accesses.find(item => item.sellerId === seller.id); return <div className="portal-access" key={seller.id}><label className="portal-check"><input type="checkbox" checked={!!access} onChange={event => toggleAccess(seller.id, event.target.checked)} />{seller.externalId && `${seller.externalId} · `}{seller.name}</label>{access && <><label className="portal-check"><input type="checkbox" checked={access.isActive} onChange={event => setAccesses(current => current.map(item => item.sellerId === seller.id ? { ...item, isActive: event.target.checked } : item))} />Vínculo ativo</label><div className="portal-permissions">{Object.entries(permissionLabels).map(([key, label]) => <label className="portal-check" key={key}><input type="checkbox" checked={access.permissions[key as keyof PortalPermissions]} onChange={event => setAccesses(current => current.map(item => item.sellerId === seller.id ? { ...item, permissions: { ...item.permissions, [key]: event.target.checked } } : item))} />{label}</label>)}</div></>}</div> })}</fieldset><button disabled={busy}>{selectedUser ? 'Salvar vínculos e permissões' : 'Criar conta'}</button>{!selectedUser && <><button disabled={busy} type="submit" value="generate">Gerar senha temporária e criar conta</button><small>Será obrigatório alterar a senha no próximo acesso.</small></>}</form>{selectedUser && <div className="portal-form"><p className="portal-help">Último acesso: {selectedUser.lastLoginAtUtc ? dateTime(selectedUser.lastLoginAtUtc) : 'Nenhum acesso registrado'}.</p><p className="portal-help">{selectedUser.mustChangePassword ? 'Troca de senha obrigatória no próximo acesso.' : 'Sem troca de senha pendente.'}</p><button disabled={busy} onClick={() => void mutate(`/api/v1/admin/users/${selectedUser.id}/active`, 'PUT', { isActive: !selectedUser.isActive })}>{selectedUser.isActive ? 'Desativar conta' : 'Ativar conta'}</button><form className="portal-form" onSubmit={event => { event.preventDefault(); void mutate(`/api/v1/admin/users/${selectedUser.id}/reset-password`, 'POST', { newPassword: password }, selectedUser.email) }}><label>Nova senha da conta<input type="password" required minLength={8} autoComplete="new-password" value={password} onChange={event => setPassword(event.target.value)} /></label><button disabled={busy}>Redefinir senha</button><small>A redefinição encerra as sessões dessa conta e exige nova senha no próximo acesso.</small></form><button disabled={busy} onClick={() => void mutate(`/api/v1/admin/users/${selectedUser.id}/reset-password`, 'POST', { generateTemporaryPassword: true }, selectedUser.email)}>Gerar nova senha temporária</button></div>}</section></div></>
}

function TemporaryPassword({ credential, onDismiss }: { credential: { password: string; email: string }; onDismiss: () => void }) {
  const [revealed, setRevealed] = useState(false)
  const [message, setMessage] = useState('')
  async function copy() {
    try { await navigator.clipboard.writeText(credential.password); setMessage('Senha copiada. Compartilhe diretamente com a pessoa autorizada.') }
    catch { setMessage('Não foi possível copiar. Use Mostrar senha temporária para consultá-la.') }
  }
  return <section className="portal-panel portal-temporary" aria-label="Acesso temporário criado"><h2>Senha temporária de {credential.email}</h2><p className="portal-help">Esta senha só pode ser consultada agora. A pessoa deverá criar uma nova senha no próximo acesso.</p><label>Senha temporária gerada<input readOnly autoComplete="off" type={revealed ? 'text' : 'password'} value={credential.password} /></label><div className="portal-actions"><button type="button" onClick={() => setRevealed(value => !value)}>{revealed ? 'Ocultar senha temporária' : 'Mostrar senha temporária'}</button><button type="button" onClick={() => void copy()}>Copiar senha temporária</button><button type="button" className="portal-secondary" onClick={onDismiss}>Descartar senha temporária</button></div>{message && <p role="status">{message}</p>}</section>
}
