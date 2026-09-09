'use strict'

const { test } = require('node:test')
const assert = require('node:assert/strict')
const http = require('node:http')
const { once } = require('node:events')
const { createGateway, validateOrigin } = require('../proxy')

async function fixture(t, handler, options = {}) {
  const received = []
  const server = http.createServer(async (req, res) => {
    const chunks = []
    for await (const chunk of req) chunks.push(chunk)
    received.push({ method: req.method, url: req.url, headers: req.headers, body: Buffer.concat(chunks) })
    handler(req, res)
  })
  server.listen(0, '127.0.0.1')
  await once(server, 'listening')
  t.after(() => { server.closeAllConnections(); return new Promise(resolve => server.close(resolve)) })
  const origin = `http://127.0.0.1:${server.address().port}`
  return { gateway: createGateway({ origin, allowLoopbackHttp: true, portalOrigin: 'https://bi.example', ...options }), received, origin }
}
const request = (url = '/api/me', overrides = {}) => ({ method: 'GET', url: `https://bi.example${url}`, headers: { origin: 'https://bi.example' }, ...overrides })

test('forwards only the application cookie and preserves anti-CSRF headers without trusting host/identity headers', async t => {
  const { gateway, received, origin } = await fixture(t, (_req, res) => res.end('{}'))
  const response = await gateway(request('/api/v1/me?month=2026-09&seller=A%2BB', { headers: {
    Cookie: 'StaticWebAppsAuthCookie=platform-secret; __Host-OroBI.Session=jwt.value.signature; ARRAffinity=private',
    Origin: 'https://bi.example', Referer: 'https://bi.example/portal', 'Sec-Fetch-Site': 'same-origin',
    'Content-Type': 'application/json', Host: 'evil.example', Forwarded: 'host=evil.example',
    'X-Forwarded-Host': 'evil.example', 'X-Forwarded-For': '127.0.0.1', 'X-Original-URL': '/admin',
    'X-MS-CLIENT-PRINCIPAL': 'forged-admin', Authorization: 'Bearer spoofed', 'Proxy-Authorization': 'secret',
    Connection: 'x-private', 'X-Private': 'secret',
  } }))
  assert.equal(response.status, 200)
  assert.equal(received[0].url, '/api/v1/me?month=2026-09&seller=A%2BB')
  assert.equal(received[0].headers.cookie, '__Host-OroBI.Session=jwt.value.signature')
  assert.equal(received[0].headers.host, new URL(origin).host)
  for (const name of ['origin', 'referer', 'sec-fetch-site']) assert.equal(received[0].headers[name], request().headers[name] ?? ({ origin: 'https://bi.example', referer: 'https://bi.example/portal', 'sec-fetch-site': 'same-origin' })[name])
  for (const name of ['forwarded', 'x-forwarded-host', 'x-forwarded-for', 'x-original-url', 'x-ms-client-principal', 'authorization', 'proxy-authorization', 'x-private']) assert.equal(received[0].headers[name], undefined)
  assert.equal(response.headers['cache-control'], 'no-store')
})

test('rejects hostile Origin or cross-site fetch before adding the backend cookie transport header', async t => {
  const { gateway, received } = await fixture(t, (_req, res) => res.end('unexpected'))
  for (const headers of [{ origin: 'https://attacker.example', 'sec-fetch-site': 'cross-site' }, { origin: 'https://bi.example', 'sec-fetch-site': 'cross-site' }, { origin: 'null' }, { referer: 'https://attacker.example/', 'sec-fetch-site': 'same-site' }, {}]) {
    assert.equal((await gateway(request('/api/auth/login', { method: 'POST', headers, bufferBody: Buffer.from('{}') }))).status, 403)
  }
  assert.equal(received.length, 0)
})

test('supplies the configured portal Origin for same-origin GET and sends the cookie transport marker', async t => {
  const { gateway, received } = await fixture(t, (_req, res) => res.end('{}'))
  await gateway(request('/api/me', { headers: { 'sec-fetch-site': 'same-origin', referer: 'https://bi.example/portal' } }))
  assert.equal(received[0].headers.origin, 'https://bi.example')
  assert.equal(received[0].headers['x-orobi-session'], 'cookie')
})

test('preserves multipart bytes, JSON whitespace and binary download bytes with relevant headers', async t => {
  const download = Buffer.from([0, 255, 128, 80, 75, 13, 10, 0])
  const { gateway, received } = await fixture(t, (_req, res) => { res.writeHead(200, { 'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="folha.pdf"', 'Cache-Control': 'public, max-age=300', 'Content-Length': download.length }); res.end(download) })
  const multipart = Buffer.concat([Buffer.from('--boundary\r\nContent-Disposition: form-data; name="file"; filename="power.csv"\r\n\r\n'), Buffer.from([0, 255, 128, 13, 10]), Buffer.from('\r\n--boundary--\r\n')])
  const response = await gateway(request('/api/imports', { method: 'POST', headers: { origin: 'https://bi.example', 'content-type': 'multipart/form-data; boundary=boundary', 'content-length': '1' }, bufferBody: multipart, body: { should: 'never reserialize' }, rawBody: 'corrupt' }))
  assert.deepEqual(received[0].body, multipart)
  assert.equal(received[0].headers['content-length'], String(multipart.length))
  assert.equal(received[0].headers['content-type'], 'multipart/form-data; boundary=boundary')
  assert.deepEqual(response.body, download)
  assert.equal(response.isRaw, true)
  assert.equal(response.headers['content-disposition'], 'attachment; filename="folha.pdf"')
  assert.equal(response.headers['content-type'], 'application/pdf')
  assert.equal(response.headers['cache-control'], 'no-store')
  const json = ' { "email" : "ana@example.com" } \n'
  await gateway(request('/api/auth/login', { method: 'POST', rawBody: json, body: { email: 'ana@example.com' } }))
  assert.equal(received[1].body.toString(), json)
})

test('returns multiple cookies as separate Azure cookie bindings with all session and expiry flags', async t => {
  const { gateway } = await fixture(t, (_req, res) => {
    res.setHeader('Set-Cookie', [
      '__Host-OroBI.Session=abc.def.ghi; Path=/; HttpOnly; Secure; SameSite=Strict; Expires=Wed, 09 Sep 2026 18:00:00 GMT',
      'old.session=; Path=/api; HttpOnly; Secure; SameSite=Strict; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT',
    ])
    res.end('{"roles":["Vendedor"]}')
  })
  const response = await gateway(request('/api/auth/login', { method: 'POST' }))
  assert.equal(response.cookies.length, 2)
  assert.deepEqual(response.cookies[0], { name: '__Host-OroBI.Session', value: 'abc.def.ghi', path: '/', httpOnly: true, secure: true, sameSite: 'Strict', expires: new Date('2026-09-09T18:00:00Z') })
  assert.equal(response.cookies[1].value, '')
  assert.equal(response.cookies[1].maxAge, 0)
  assert.equal(response.cookies[1].expires.getTime(), 0)
  assert.equal(response.cookies[1].sameSite, 'Strict')
  assert.equal(response.headers['set-cookie'], undefined, 'cookie values must not be joined by comma in a scalar header')
})

for (const status of [204, 401, 403, 429]) test(`preserves upstream ${status} without retrying a mutation`, async t => {
  const { gateway, received } = await fixture(t, (_req, res) => { res.writeHead(status, { 'Retry-After': '60', 'WWW-Authenticate': 'Bearer', 'Connection': 'close' }); res.end(status === 204 ? undefined : 'upstream response') })
  const response = await gateway(request('/api/v1/auth/logout', { method: 'POST' }))
  assert.equal(response.status, status)
  assert.equal(response.body.toString(), status === 204 ? '' : 'upstream response')
  assert.equal(response.headers['retry-after'], '60')
  assert.equal(response.headers['connection'], undefined)
  assert.equal(response.headers['cache-control'], 'no-store')
  assert.equal(received.length, 1)
})

test('does not follow redirects or use URL query/header values to select an upstream', async t => {
  const { gateway, received } = await fixture(t, (_req, res) => { res.writeHead(307, { Location: 'http://169.254.169.254/metadata/identity' }); res.end('redirect') })
  const response = await gateway(request('/api/me?url=http://169.254.169.254/metadata', { headers: { 'x-upstream-url': 'http://169.254.169.254' } }))
  assert.equal(response.status, 307)
  assert.equal(received.length, 1)
  assert.equal(received[0].headers['x-upstream-url'], undefined)
})

for (const path of ['/admin', '//evil.example/api/me', '/api/../admin', '/api/%2e%2e/admin', '/api/%252e%252e/admin', '/api/foo%2f..%2fadmin', '/api/%5cadmin', '/api//evil.example', '/api/%00', '/api/me#fragment']) test(`rejects path escape ${path} without contacting the backend`, async t => {
  const { gateway, received } = await fixture(t, (_req, res) => res.end('unexpected'))
  const response = await gateway(request(path))
  assert.equal(response.status, 400)
  assert.equal(received.length, 0)
})

test('rejects duplicate authentication cookies, unsupported methods and unsafe connection nominations', async t => {
  const { gateway, received } = await fixture(t, (_req, res) => res.end('unexpected'))
  assert.equal((await gateway(request('/api/me', { headers: { cookie: '__Host-OroBI.Session=a; __Host-OroBI.Session=b' } }))).status, 400)
  assert.equal((await gateway(request('/api/me', { method: 'TRACE' }))).status, 405)
  assert.equal((await gateway(request('/api/me', { headers: { connection: 'origin', origin: 'https://bi.example' } }))).status, 400)
  assert.equal(received.length, 0)
})

test('network failure is generic and never echoes credentials, target host, or stack', async t => {
  const { gateway, received } = await fixture(t, (req) => req.socket.destroy())
  const response = await gateway(request('/api/auth/login?secret=query-secret', { method: 'POST', headers: { origin: 'https://bi.example', cookie: '__Host-OroBI.Session=cookie-secret' }, bufferBody: Buffer.from('password-secret') }))
  assert.equal(response.status, 502)
  assert.equal(JSON.parse(response.body).error, 'Não foi possível acessar o sistema. Tente novamente em alguns instantes.')
  assert.equal(response.headers['cache-control'], 'no-store')
  assert.doesNotMatch(response.body.toString(), /secret|127\.0\.0\.1|Error:|stack|ECONN/)
  assert.equal(received.length, 1)
})

test('uses a total timeout even when the upstream slowly streams data', async t => {
  const { gateway, received } = await fixture(t, (_req, res) => {
    res.writeHead(200)
    const interval = setInterval(() => res.write('still working'), 10)
    res.on('close', () => clearInterval(interval))
  }, { timeoutMs: 80 })
  const started = Date.now()
  const response = await gateway(request('/api/imports', { method: 'POST', bufferBody: Buffer.from('private upload') }))
  assert.equal(response.status, 504)
  assert.equal(JSON.parse(response.body).error, 'Não foi possível acessar o sistema. Tente novamente em alguns instantes.')
  assert.ok(Date.now() - started < 1500)
  assert.doesNotMatch(response.body.toString(), /private|127\.0\.0\.1|Error:|stack/)
  assert.equal(received.length, 1)
})

test('production configuration accepts only a fixed HTTPS origin without path, userinfo, or query', () => {
  assert.equal(validateOrigin('https://api.example').origin, 'https://api.example')
  for (const origin of ['http://localhost:3000', 'https://user:pass@api.example', 'https://api.example/api', 'https://api.example?target=other', 'https://api.example#fragment', 'file:///secret']) assert.throws(() => validateOrigin(origin))
  assert.throws(() => createGateway({ timeoutMs: 45000 }))
})

test('treats malformed raw header bytes as a generic client error instead of exposing a Node exception', async t => {
  const { gateway, received } = await fixture(t, (_req, res) => res.end('unexpected'))
  const response = await gateway(request('/api/me', { headers: { 'user-agent': '\u0001' } }))
  assert.equal(response.status, 400)
  assert.doesNotMatch(response.body.toString(), /ERR_|TypeError|stack/)
  assert.equal(received.length, 0)
})

for (const [attribute, expected] of [['strict', 'Strict'], ['LAX', 'Lax'], ['None', 'None']]) test(`normalizes SameSite=${attribute} to the documented Azure cookie enum`, async t => {
  const { gateway } = await fixture(t, (_req, res) => {
    res.setHeader('Set-Cookie', `__Host-OroBI.Session=value; Path=/; Secure; HttpOnly; SameSite=${attribute}`)
    res.end('{}')
  })
  assert.equal((await gateway(request())).cookies[0].sameSite, expected)
})

test('returns generic Portuguese forbidden and invalid-request messages', async t => {
  const { gateway } = await fixture(t, (_req, res) => res.end('unexpected'))
  const forbidden = await gateway(request('/api/me', { headers: { origin: 'https://attacker.example' } }))
  assert.equal(JSON.parse(forbidden.body).error, 'Acesso não permitido.')
  const invalid = await gateway(request('/outside-api'))
  assert.equal(JSON.parse(invalid.body).error, 'Solicitação inválida.')
})
