'use strict'

const http = require('node:http')
const https = require('node:https')

const DEFAULT_API_ORIGIN = 'https://orobi-api.ashymoss-e2dce47a.eastus2.azurecontainerapps.io'
const DEFAULT_PORTAL_ORIGIN = 'https://lively-sea-0776c9a0f.6.azurestaticapps.net'
const SESSION_COOKIE = '__Host-OroBI.Session'
const METHODS = new Set(['GET', 'HEAD', 'POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS'])
const REQUEST_HEADERS = new Set([
  'accept', 'accept-language', 'accept-encoding', 'content-type', 'content-encoding',
  'origin', 'referer', 'sec-fetch-site', 'sec-fetch-mode', 'sec-fetch-dest', 'user-agent',
  'if-match', 'if-none-match', 'if-modified-since', 'if-unmodified-since', 'range', 'if-range',
  'access-control-request-method', 'access-control-request-headers',
])
const RESPONSE_HEADERS = new Set([
  'content-type', 'content-disposition', 'content-encoding', 'content-language', 'content-range',
  'accept-ranges', 'etag', 'last-modified', 'retry-after', 'www-authenticate', 'location',
  'vary', 'x-content-type-options', 'content-security-policy', 'referrer-policy',
])

class RequestError extends Error {
  constructor(status) { super('Invalid gateway request'); this.status = status }
}

function failure(status) {
  const message = status === 502 || status === 504
    ? 'Não foi possível acessar o sistema. Tente novamente em alguns instantes.'
    : status === 403 ? 'Acesso não permitido.' : 'Solicitação inválida.'
  return { status, headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' }, body: Buffer.from(JSON.stringify({ error: message })), isRaw: true }
}

function validateOrigin(value, allowLoopbackHttp = false) {
  const origin = new URL(value)
  const loopback = allowLoopbackHttp && origin.protocol === 'http:' && ['127.0.0.1', '[::1]'].includes(origin.hostname)
  if ((!loopback && origin.protocol !== 'https:') || origin.username || origin.password || origin.pathname !== '/' || origin.search || origin.hash) throw new Error('Gateway requires an HTTPS origin without credentials, path, query, or fragment')
  return origin
}

// Validate the raw request target before URL normalization can erase dot segments.
function apiPath(requestUrl) {
  if (typeof requestUrl !== 'string' || /[^\x21-\x7e]|[\\#]/.test(requestUrl)) throw new RequestError(400)
  const raw = requestUrl.replace(/^https?:\/\/[^/]+(?=\/)/i, '')
  if (!raw.startsWith('/') || raw.startsWith('//')) throw new RequestError(400)
  const rawPath = raw.split('?')[0]
  if (rawPath !== '/api' && !rawPath.startsWith('/api/')) throw new RequestError(400)
  const parts = rawPath.split('/').slice(1)
  for (let index = 0; index < parts.length; index++) {
    let segment = parts[index]
    if (!segment && index !== parts.length - 1) throw new RequestError(400)
    for (let pass = 0; pass < 5; pass++) {
      if (segment === '.' || segment === '..' || /[/\\?#\u0000-\u001f\u007f]/.test(segment)) throw new RequestError(400)
      if (!segment.includes('%')) break
      try { segment = decodeURIComponent(segment) } catch { throw new RequestError(400) }
      if (pass === 4) throw new RequestError(400)
    }
  }
  return raw
}

function normalizedHeaders(headers) {
  const result = Object.create(null)
  for (const [name, value] of Object.entries(headers || {})) {
    const key = name.toLowerCase()
    if (Object.hasOwn(result, key) || typeof value !== 'string' || /[\r\n\u0000]/.test(value)) throw new RequestError(400)
    try { http.validateHeaderName(name); http.validateHeaderValue(name, value) }
    catch { throw new RequestError(400) }
    result[key] = value
  }
  return result
}

function forwardHeaders(input, method, portalOrigin) {
  // Never replace an untrusted browser Origin with our trusted configured origin.
  if (input.origin !== undefined && input.origin !== portalOrigin) throw new RequestError(403)
  if (input['sec-fetch-site'] !== undefined && !['same-origin', 'none'].includes(input['sec-fetch-site'])) throw new RequestError(403)
  if (input.referer) {
    try { if (new URL(input.referer).origin !== portalOrigin) throw new RequestError(403) }
    catch { throw new RequestError(403) }
  }
  if (input.origin === undefined && !['GET', 'HEAD', 'OPTIONS'].includes(method) && input['sec-fetch-site'] !== 'same-origin') throw new RequestError(403)
  const nominated = (input.connection || '').toLowerCase().split(',').map(value => value.trim())
  if (nominated.some(name => REQUEST_HEADERS.has(name) || name === 'cookie' || name === 'x-orobi-session')) throw new RequestError(400)
  const headers = Object.create(null)
  for (const [name, value] of Object.entries(input)) if (REQUEST_HEADERS.has(name)) headers[name] = value
  const cookies = (input.cookie || '').split(';').map(value => value.trim()).filter(value => value.slice(0, value.indexOf('=')) === SESSION_COOKIE)
  if (cookies.length > 1) throw new RequestError(400)
  if (cookies.length === 1) headers.cookie = cookies[0]
  headers.origin = input.origin || portalOrigin
  headers['x-orobi-session'] = 'cookie'
  return headers
}

function bodyBytes(req, headers) {
  if (Buffer.isBuffer(req.bufferBody)) return req.bufferBody
  if (Buffer.isBuffer(req.body)) return req.body
  if (typeof req.rawBody === 'string') return Buffer.from(req.rawBody)
  if (typeof req.body === 'string') return Buffer.from(req.body)
  if (req.body != null || Number(headers['content-length'] || 0) > 0) throw new RequestError(400)
  return Buffer.alloc(0)
}

// Functions v3 uses a scalar header map. Its cookies[] binding preserves separate
// Set-Cookie fields, including Expires dates containing commas and logout expiry.
function responseCookie(value) {
  const [pair, ...attributes] = value.split(';')
  const separator = pair.indexOf('=')
  if (separator < 1) throw new Error('Invalid upstream cookie')
  const cookie = { name: pair.slice(0, separator).trim(), value: pair.slice(separator + 1).trim() }
  for (const attribute of attributes) {
    const index = attribute.indexOf('=')
    const name = (index < 0 ? attribute : attribute.slice(0, index)).trim().toLowerCase()
    const content = index < 0 ? '' : attribute.slice(index + 1).trim()
    switch (name) {
      case 'httponly': cookie.httpOnly = true; break
      case 'secure': cookie.secure = true; break
      case 'path': cookie.path = content; break
      case 'domain': cookie.domain = content; break
      case 'samesite':
        if (!['lax', 'strict', 'none'].includes(content.toLowerCase())) throw new Error('Invalid upstream SameSite')
        cookie.sameSite = { lax: 'Lax', strict: 'Strict', none: 'None' }[content.toLowerCase()]; break
      case 'expires':
        cookie.expires = new Date(content)
        if (!Number.isFinite(cookie.expires.getTime())) throw new Error('Invalid upstream expiry')
        break
      case 'max-age':
        if (!/^-?\d+$/.test(content)) throw new Error('Invalid upstream max-age')
        cookie.maxAge = Number(content); break
      case '': break
      default: throw new Error('Unsupported upstream cookie attribute')
    }
  }
  return cookie
}

function upstreamResponse(response, body) {
  const headers = Object.create(null)
  const nominated = (response.headers.connection || '').toLowerCase().split(',').map(value => value.trim())
  for (const [name, value] of Object.entries(response.headers)) {
    if (RESPONSE_HEADERS.has(name) && !nominated.includes(name) && typeof value === 'string') headers[name] = value
  }
  headers['cache-control'] = 'no-store'
  return { status: response.statusCode || 502, headers, cookies: (response.headers['set-cookie'] || []).map(responseCookie), body, isRaw: true }
}

function createGateway({ origin = process.env.OROBI_API_ORIGIN || DEFAULT_API_ORIGIN, portalOrigin = process.env.OROBI_PORTAL_ORIGIN || DEFAULT_PORTAL_ORIGIN, timeoutMs = 40000, allowLoopbackHttp = false } = {}) {
  const upstream = validateOrigin(origin, allowLoopbackHttp)
  const allowedPortalOrigin = validateOrigin(portalOrigin).origin
  if (!Number.isInteger(timeoutMs) || timeoutMs < 1 || timeoutMs > 40000) throw new Error('Gateway timeout must be between 1 and 40000ms')
  const transport = upstream.protocol === 'https:' ? https : http
  return async function gateway(req) {
    let path, method, headers, body
    try {
      path = apiPath(req.originalUrl || req.url)
      method = String(req.method || '').toUpperCase()
      if (!METHODS.has(method)) throw new RequestError(405)
      const input = normalizedHeaders(req.headers)
      headers = forwardHeaders(input, method, allowedPortalOrigin)
      body = bodyBytes(req, input)
      if (body.length || !['GET', 'HEAD'].includes(method)) headers['content-length'] = String(body.length)
    } catch (error) { return failure(error instanceof RequestError ? error.status : 400) }

    return new Promise(resolve => {
      let completed = false
      let timer
      const finish = result => { if (completed) return; completed = true; clearTimeout(timer); resolve(result) }
      // Host, protocol and port come exclusively from trusted server configuration.
      // Node HTTP does not follow redirects and this gateway never retries requests.
      const outgoing = transport.request({ protocol: upstream.protocol, hostname: upstream.hostname, port: upstream.port || undefined, method, path, headers }, incoming => {
        const chunks = []
        incoming.on('data', chunk => chunks.push(chunk))
        incoming.on('error', () => finish(failure(502)))
        incoming.on('end', () => {
          try { finish(upstreamResponse(incoming, Buffer.concat(chunks))) }
          catch { finish(failure(502)) }
        })
      })
      outgoing.on('error', () => finish(failure(502)))
      // A total deadline also bounds DNS/connect/TLS and slow streaming responses.
      timer = setTimeout(() => { finish(failure(504)); outgoing.destroy() }, timeoutMs)
      outgoing.end(body)
    }).catch(() => failure(502))
  }
}

module.exports = { createGateway, validateOrigin }
