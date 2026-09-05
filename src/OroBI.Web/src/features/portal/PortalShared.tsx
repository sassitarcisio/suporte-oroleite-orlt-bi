import { date, money, number } from './portalFormatting'
import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { apiRequest } from '../../api/client'
import type { PortalSale } from './portalTypes'

export function Empty({ children }: { children: ReactNode }) { return <div className="portal-empty"><i className="fa-regular fa-folder-open" aria-hidden="true" /><p>{children}</p></div> }
export function Metric({ label, value, icon, hint, primary = false }: { label: string; value: string; icon: string; hint?: string; primary?: boolean }) { return <article className={`portal-metric ${primary ? 'portal-metric-primary' : ''}`}><span className="portal-metric-icon" aria-hidden="true"><i className={`fa-solid ${icon}`} /></span><p>{label}</p><strong>{value}</strong>{hint && <small>{hint}</small>}</article> }
export function Resource<T>({ token, path, children }: { token: string; path: string; children: (data: T) => ReactNode }) {
  return <ResourceRequest<T> key={`${token}:${path}`} token={token} path={path}>{children}</ResourceRequest>
}
function ResourceRequest<T>({ token, path, children }: { token: string; path: string; children: (data: T) => ReactNode }) {
  const [state, setState] = useState<{ path: string; token: string; data?: T; error?: string }>({ path: '', token: '' })
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    const abort = new AbortController()
    void apiRequest<T>(path, token, { signal: abort.signal, cache: 'no-store' })
      .then(data => { if (!abort.signal.aborted) setState({ path, token, data }) })
      .catch(error => { if (!abort.signal.aborted) setState({ path, token, error: error instanceof Error ? error.message : 'Não foi possível carregar os dados.' }) })
    return () => abort.abort()
  }, [token, path, retry])
  if (state.path !== path || state.token !== token || (!state.error && state.data === undefined)) return <div className="portal-loading" role="status"><span />Carregando seus resultados...</div>
  if (state.error) return <div className="portal-message" role="alert"><p>{state.error}</p><button onClick={() => { setState({ path: '', token: '' }); setRetry(value => value + 1) }}>Tentar novamente</button></div>
  return children(state.data!)
}
export function SalesList({ items }: { items: PortalSale[] }) {
  return <div className="portal-list">{items.map((sale, index) => <article className="portal-record" key={`${sale.id}-${index}`}><div className="portal-record-top"><strong>{sale.customerName}</strong><span>{money(sale.totalValue)}</span></div><p>{sale.productName}</p><div className="portal-record-meta"><span>{date(sale.date)} · Doc. {sale.documentNumber}</span><span>{sale.brand} · {number(sale.quantity)} un. · {sale.movementType}</span></div></article>)}</div>
}
