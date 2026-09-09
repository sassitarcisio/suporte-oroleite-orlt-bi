// Dependency-free browser checks against local synthetic API fixtures only. Build OroBI.Web before running.
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import { once } from 'node:events'
import { readFile, writeFile, mkdir, mkdtemp, rm, stat } from 'node:fs/promises'
import { dirname, resolve, extname, sep, basename } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'


const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const dist = resolve(root, "src/OroBI.Web/dist");
const evidence = resolve(root, "docs/audits/login-evidence");
const chromePath = process.env.PORTAL_TEST_CHROME ?? "C:/Program Files/Google/Chrome/Application/chrome.exe";
const delay = ms => new Promise(done => setTimeout(done, ms));
const permissions = { canViewRevenue: true, canViewCommission: true, canViewPrize: true, canViewPPP: true, canViewGoals: true, canViewTrades: true, canViewCustomers: true }
const period = { grossSales: 132456.78, netRevenue: 129876.54, negativeMovements: -2580.24, saleQuantity: 456, movementCount: 37, customerCount: 18, documentCount: 29, averageTicket: 4478.50 }
const sale = { id: 'synthetic-sale', date: '2026-09-05', documentNumber: 'SYNTHETIC-001', movementType: 'VENDA', customerCode: 'SYN001', customerName: 'Mercado Sintético Jardim das Flores', productName: 'Produto sintético de demonstração com descrição extensa para inspeção da tela', brand: 'Marca Sintética', quantity: 12, totalValue: 1234.56 }
const customer = { customerCode: sale.customerCode, customerName: sale.customerName, city: 'Cidade Sintética', grossSales: 1234.56, netRevenue: 1200, documentCount: 2, lastPurchaseDate: sale.date, averageTicket: 600, purchasedQuantity: 12 }
const closing = { year: 2026, month: 9, status: 'Aprovado', isEstimated: false, approvedAtUtc: '2026-09-05T16:00:00Z', revenue: 129876.54, commissionableRevenue: 129876.54, commission: 1298.7654, commissionPercent: 1, pppPercent: 90, pppAward: 200, revenueAward: 300, positivityAward: 100, tradeAward: 50, totalAwards: 650, tradeValue: 700, tradePercent: 0.54, commissionAndAwards: 1948.7654 }
const requests = []
let simulateNetworkFailure = false
const fixture = pathname => {
  if (pathname === '/api/v1/me' || pathname === '/api/me') return { expiresAtUtc: new Date(Date.now()+3600000).toISOString(), mustChangePassword: false, userId: 'synthetic-browser-user', email: 'synthetic@example.invalid', userName: 'Conta sintética', roles: ['Vendedor'], sellerId: '00000000-0000-0000-0000-000000000001', seller: 'Vendedora Sintética', permissions, sellerAccesses: [] }
  if (pathname.endsWith('/dashboard')) return { startDate: '2026-09-01', endDate: '2026-09-30', referenceDate: '2026-09-05', period, today: period, month: period, dailyTrend: [{ date: sale.date, grossSales: period.grossSales, netRevenue: period.netRevenue, negativeMovements: period.negativeMovements }], freshness: { source: 'csv', updatedAtUtc: '2026-09-05T15:00:00Z', timestampKind: 'import-started' } }
  if (pathname.endsWith('/sales')) return { items: [sale, { ...sale, id: 'synthetic-sale-2', documentNumber: 'SYNTHETIC-002' }], page: 1, pageSize: 20, totalCount: 2 }
  if (pathname.endsWith('/customers')) return { observedBuyersOnly: true, items: [customer], totalCount: 1, hasMore: false }
  if (pathname.includes('/customers/')) return { customer, sales: [{ ...sale, physicalTrades: 61.728, tradeToSalesPercent: 5 }], totalCount: 1, hasMore: false }
  if (pathname.endsWith('/goals')) return { year: 2026, month: 9, available: true, unavailableReason: null, isEstimated: false, status: 'Aprovado', items: [{ brand: 'Marca Sintética', type: 'FATURAMENTO', target: 140000, actual: 129876.54, achievedPercent: 92.77, maximumPrize: 400, currentPrize: 300, nextTierPercent: 100, amountToNextTier: 10123.46, nextTierPrize: 400 }] }
  if (pathname.endsWith('/ppp')) return { year: 2026, month: 9, available: true, unavailableReason: null, isEstimated: false, status: 'Aprovado', achievementPercent: 90, award: 200, segments: [{ segment: 'Mercados sintéticos', customerCount: 18, itemsPerSegment: 4, groupsPlaced: 3, achievementPercent: 90 }] }
  if (pathname.endsWith('/trades')) return { physicalTrades: 700, tradeToSalesPercent: 0.54, movementCount: 1, items: [{ ...sale, movementType: 'TROCA' }], hasMore: false }
  if (pathname.endsWith('/closings/history')) return [{ month: '2026-09', status: 'Aprovado' }]
  if (pathname.endsWith('/closings') || pathname.endsWith('/commission')) return closing
  if (pathname.endsWith('/products') || pathname.endsWith('/brands')) return { items: [{ label: 'Resultado sintético', grossSales: 1234, netRevenue: 1200, quantity: 12, movementCount: 2, customerCount: 1, revenueSharePercent: 100 }], totalCount: 1, hasMore: false }
  return undefined
}

class Cdp {
  constructor(socket) {
    this.socket = socket; this.pending = new Map(); this.handlers = new Map(); this.counter = 0
    socket.addEventListener('message', event => {
      const message = JSON.parse(event.data)
      if (message.id) {
        const pending = this.pending.get(message.id)
        if (!pending) return
        clearTimeout(pending.timer); this.pending.delete(message.id)
        if (message.error) pending.reject(new Error(JSON.stringify(message.error)))
        else pending.resolve(message.result)
      } else for (const handler of this.handlers.get(message.method) ?? []) handler(message.params)
    })
  }
  on(method, handler) { this.handlers.set(method, [...(this.handlers.get(method) ?? []), handler]) }
  send(method, params = {}, sessionId = this.sessionId) {
    const id = ++this.counter
    return new Promise((resolveResult, reject) => {
      const timer = setTimeout(() => { this.pending.delete(id); reject(new Error(`CDP timeout: ${method}`)) }, 15000)
      this.pending.set(id, { resolve: resolveResult, reject, timer })
      this.socket.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }))
    })
  }
  async evaluate(expression) {
    const result = await this.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.text)
    return result.result.value
  }
}

const viewports = [[360,800], [390,844], [430,932], [768,1024], [1366,768], [1920,1080]]
let loginMode = 'invalid', identityDelay = 0, activeSession = false
const cookieName = '__Host-OroBI.Session'
const mimeTypes = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.webmanifest': 'application/manifest+json', '.woff2': 'font/woff2' }
const server = createServer(async (request, response) => {
  try {
    const pathname = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname)
    if (simulateNetworkFailure) { request.socket.destroy(); return }
    if (pathname.startsWith('/api/')) {
      let raw = ''; for await (const chunk of request) raw += chunk
      const body = raw ? JSON.parse(raw) : null
      requests.push({ method: request.method, path: request.url, hasCookie: request.headers.cookie?.includes(`${cookieName}=`) ?? false, hasAuthorization: !!request.headers.authorization, cookieMode: request.headers['x-orobi-session'] === 'cookie', ...(body ? { submittedEmail: body.email, rememberDevice: body.rememberDevice } : {}) })
      response.setHeader('Cache-Control', 'no-store')
      response.setHeader('Content-Type', 'application/json')
      if (pathname.endsWith('/auth/login')) {
        if (loginMode === 'network') { request.socket.destroy(); return }
        if (loginMode === 'loading') await delay(1200)
        if (loginMode === 'invalid') { response.writeHead(401).end(JSON.stringify({ error: 'Usuário ou senha incorretos.' })); return }
        activeSession = true
        response.setHeader('Set-Cookie', `${cookieName}=synthetic-session; HttpOnly; Secure; SameSite=Lax; Path=/${body?.rememberDevice ? '; Max-Age=2592000' : ''}`)
        response.end(JSON.stringify({ sessionMode: 'cookie', roles: ['Vendedor'], expiresAtUtc: new Date(Date.now()+3600000).toISOString() })); return
      }
      if (pathname.endsWith('/auth/logout')) {
        activeSession = false; response.setHeader('Set-Cookie', `${cookieName}=; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age=0`)
        response.writeHead(204).end(); return
      }
      if (identityDelay && (pathname === '/api/me' || pathname === '/api/v1/me')) await delay(identityDelay)
      if (!activeSession || !request.headers.cookie?.includes(`${cookieName}=`)) { response.writeHead(401).end(JSON.stringify({ error: 'Sessão expirada.' })); return }
      const payload = fixture(pathname)
      response.writeHead(payload === undefined ? 404 : 200).end(JSON.stringify(payload ?? { error: 'Synthetic route not configured' })); return
    }
    const path = pathname === '/' || pathname === '/portal' || pathname.startsWith('/portal/') ? resolve(dist, 'index.html') : resolve(dist, `.${pathname}`)
    if (!path.startsWith(`${dist}${sep}`)) { response.writeHead(403).end(); return }
    const payload = await readFile(path)
    response.writeHead(200, { 'Content-Type': mimeTypes[extname(path)] ?? 'application/octet-stream' }).end(payload)
  } catch { if (!response.headersSent) response.writeHead(404); response.end() }
})

let browser, cdp, profile
const result = { generatedAtUtc: new Date().toISOString(), environment: 'Local Chrome headless; production build; synthetic localhost API', limitations: ['No production endpoint, account, password or token was used.', 'Cookie and API behavior is simulated; these checks do not establish backend authentication security.', 'Local loopback uses the same Secure HttpOnly __Host cookie; the browser fixture build changes only the configured cookie portal origin to http://127.0.0.1:55331.', 'Viewport and standalone emulation do not certify installation, physical device rendering, virtual keyboards, or operating system browser restart behavior.'], assertions: [], viewports: [], runtimeErrors: [], consoleErrors: [], failedRequests: [], blockedExternalRequests: [], apiRequests: requests }
const check = (name, passed, details = null) => { result.assertions.push({ name, passed: !!passed, details }); if (!passed) console.error(`FAIL: ${name}`) }
async function waitFor(expression) { for (let attempt = 0; attempt < 120; attempt++) { if (await cdp.evaluate(expression)) return; await delay(100) }; throw new Error(`UI did not settle: ${expression}`) }
async function click(selector) { const found = await cdp.evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); el?.click(); return !!el })()`); if (!found) throw new Error(`Missing selector ${selector}`) }
async function fill(selector, value) { await cdp.evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, ${JSON.stringify(value)}); el.dispatchEvent(new Event('input', { bubbles: true })); })()`) }
async function screenshot(name) { const capture = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }); await writeFile(resolve(evidence, name), Buffer.from(capture.data, 'base64')) }
async function loginFields() { await fill('input[type="email"]', 'synthetic@example.invalid'); await fill('input[autocomplete="current-password"]', 'Synthetic-only-password!') }
async function submit() { await cdp.evaluate(`document.querySelector('.login-screen form').requestSubmit()`) }
async function cookie() { return (await cdp.send('Network.getCookies', { urls: [`http://127.0.0.1:${server.address().port}/api/me`] })).cookies.find(item => item.name === cookieName) }
async function logout() { await click('button[aria-label="Sair"]'); await waitFor(`!!document.querySelector('.login-submit')`); await waitFor(`!document.querySelector('.seller-portal')`) }
async function assertNoStorageCredentials(name) { const storage = await cdp.evaluate(`({local: {...localStorage},session: {...sessionStorage},cookie:document.cookie})`); check(name, !JSON.stringify(storage).includes('synthetic-session') && !JSON.stringify(storage).includes('Synthetic-only-password') && !storage.local['orobi.access-token'] && !storage.session['orobi.access-token'], storage) }

try {
  await stat(resolve(dist, 'index.html')); await mkdir(evidence, { recursive: true })
  await rm(resolve(evidence, 'login-failure.png'), { force: true })
  await new Promise(done => server.listen(Number(process.env.PORTAL_TEST_PORT ?? 55331), '127.0.0.1', done))
  const origin = `http://127.0.0.1:${server.address().port}`
  profile = await mkdtemp(resolve(tmpdir(), 'orobi-login-ui-'))
  browser = spawn(chromePath, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--disable-background-networking', '--disable-component-update', '--disable-sync', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] })
  let launchError; browser.on('error', error => { launchError = error }); browser.stderr.on('data', () => {})
  let debugging
  for (let attempt = 0; attempt < 150; attempt++) { if (launchError) throw launchError; try { debugging = (await readFile(resolve(profile, 'DevToolsActivePort'), 'utf8')).trim().split('\n'); break } catch { await delay(100) } }
  if (!debugging) throw new Error('Chrome debugging endpoint did not start.')
  const socket = new WebSocket(`ws://127.0.0.1:${debugging[0]}${debugging[1].trim()}`)
  await once(socket, 'open'); cdp = new Cdp(socket); result.browserVersion = await cdp.send('Browser.getVersion')
  const target = await cdp.send('Target.createTarget', { url: 'about:blank' })
  cdp.sessionId = (await cdp.send('Target.attachToTarget', { targetId: target.targetId, flatten: true })).sessionId
  await cdp.send('Page.enable'); await cdp.send('Runtime.enable'); await cdp.send('Network.enable'); await cdp.send('Log.enable')
  cdp.on('Runtime.exceptionThrown', event => result.runtimeErrors.push(event.exceptionDetails.exception?.description ?? event.exceptionDetails.text))
  cdp.on('Runtime.consoleAPICalled', event => { if (event.type === 'error') result.consoleErrors.push(event.args.map(arg => arg.value ?? arg.description).join(' ')) })
  cdp.on('Network.loadingFailed', event => result.failedRequests.push({ errorText: event.errorText, type: event.type, canceled: !!event.canceled }))
  result.httpErrors = []
  cdp.on('Network.responseReceived', event => { if (event.response.status >= 400) result.httpErrors.push({ url: event.response.url, status: event.response.status }) })
  await cdp.send('Fetch.enable', { patterns: [{ urlPattern: '*' }] })
  cdp.on('Fetch.requestPaused', event => { const url = new URL(event.request.url); if (url.origin === origin || ['data:', 'blob:'].includes(url.protocol)) void cdp.send('Fetch.continueRequest', { requestId: event.requestId }); else { result.blockedExternalRequests.push(event.request.url); void cdp.send('Fetch.failRequest', { requestId: event.requestId, errorReason: 'BlockedByClient' }) } })
  for (const [width,height] of viewports) {
    await cdp.send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: width < 768 })
    await cdp.send('Page.navigate', { url: `${origin}/portal` }); await waitFor(`!!document.querySelector('.login-submit')`); await cdp.evaluate('document.fonts.ready.then(() => true)'); await delay(150)
    const layout = await cdp.evaluate(`(() => { const visible = el => el.getBoundingClientRect().width > 0 && el.getBoundingClientRect().height > 0; const targets = [...document.querySelectorAll('.login-screen button,.login-screen input,.login-screen label:has(input[type="checkbox"])')].filter(visible).map(el => { const rect = (el.type === 'checkbox' ? el.closest('label') : el).getBoundingClientRect(); return {name:el.getAttribute('aria-label') || el.textContent?.trim() || el.type,width:rect.width,height:rect.height}; }); return { width:innerWidth,height:innerHeight,documentWidth:document.documentElement.scrollWidth,documentHeight:document.documentElement.scrollHeight,targets,heading:document.querySelector('.login-screen h1')?.textContent,mobileIdentityVisible:!!document.querySelector('.login-mobile-identity') && visible(document.querySelector('.login-mobile-identity')),bodyText:document.body.innerText, fonts:document.fonts.status } })()`)
    check(`${width}x${height}: no horizontal overflow`, layout.documentWidth <= width + 1, { documentWidth: layout.documentWidth })
    check(`${width}x${height}: visible controls at least 44px`, layout.targets.every(item => item.width >= 43.9 && item.height >= 43.9), layout.targets)
    const rememberLayout = await cdp.evaluate(`(() => {const input=document.querySelector('input[type="checkbox"]'),label=input.closest('label'),email=document.querySelector('input[type="email"]'),box=input.getBoundingClientRect(),first=[...label.childNodes].find(node=>node.nodeType===Node.TEXT_NODE && node.textContent.trim()),range=document.createRange();range.selectNodeContents(first);const text=range.getBoundingClientRect();return {leftGap:box.left-email.getBoundingClientRect().left,width:box.width,textGap:text.left-box.right,labelWidth:label.getBoundingClientRect().width}})()`)
    check(`${width}x${height}: checkbox aligns with fields and adjacent label text`, Math.abs(rememberLayout.leftGap)<=3 && rememberLayout.width<=24 && rememberLayout.textGap>=6 && rememberLayout.textGap<=16, rememberLayout)
    check(`${width}x${height}: no public registration`, !/Criar minha conta|Cadastre-se/i.test(layout.bodyText))
    if (width < 768) check(`${width}x${height}: mobile identity visible`, layout.mobileIdentityVisible)
    const filename = `login-${width}x${height}.png`; await screenshot(filename)
    result.viewports.push({ width,height,screenshot:filename,...layout })
  }
  await cdp.send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true })
  await cdp.send('Page.navigate', { url: `${origin}/portal` }); await waitFor(`!!document.querySelector('.login-submit')`)
  const beforeEmpty = requests.filter(item => item.path.endsWith('/auth/login')).length
  await submit(); await delay(150)
  check('Empty required fields prevent HTTP submission', requests.filter(item => item.path.endsWith('/auth/login')).length === beforeEmpty)
  await cdp.evaluate(`document.querySelector('input[type="email"]').focus()`)
  await cdp.send('Input.dispatchKeyEvent', { type:'keyDown', key:'Tab', code:'Tab', windowsVirtualKeyCode:9 }); await cdp.send('Input.dispatchKeyEvent', { type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9 })
  check('Keyboard Tab reaches password in natural field order', await cdp.evaluate(`document.activeElement.getAttribute('autocomplete') === 'current-password'`))
  const focusedStyle = await cdp.evaluate(`(() => { const el=document.activeElement,s=getComputedStyle(el); return {focusVisible:el.matches(':focus-visible'),outline:s.outline,boxShadow:s.boxShadow,borderColor:s.borderColor} })()`)
  check('Keyboard field focus is visibly indicated', focusedStyle.focusVisible && (focusedStyle.outline.includes('solid') || focusedStyle.boxShadow !== 'none'), focusedStyle)
  await loginFields(); await click('button[aria-label="Mostrar senha"]')
  check('Password reveal updates input type and accessible name', await cdp.evaluate(`document.querySelector('input[autocomplete="current-password"]').type === 'text' && !!document.querySelector('button[aria-label="Ocultar senha"]')`))
  await click('button[aria-label="Ocultar senha"]')
  await cdp.evaluate(`[...document.querySelectorAll('button')].find(el => el.textContent.trim() === 'Esqueci minha senha').click()`)
  await waitFor(`!!document.querySelector('[role="dialog"]') || !!document.querySelector('dialog[open]')`)
  check('Password helper receives keyboard focus', await cdp.evaluate(`!!document.activeElement.closest('[role="dialog"],dialog')`))
  check('Helper close action has a 44px touch target', await cdp.evaluate(`(() => {const r=document.querySelector('[role="dialog"] button,dialog button').getBoundingClientRect();return r.width>=44 && r.height>=44})()`))
  await screenshot('login-password-help-390x844.png')
  await cdp.send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await cdp.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 })
  await waitFor(`!document.querySelector('[role="dialog"]') && !document.querySelector('dialog[open]')`)
  check('Escape closes helper and restores opener focus', await cdp.evaluate(`document.activeElement.textContent.trim() === 'Esqueci minha senha'`))
  loginMode = 'invalid'; await submit(); await waitFor(`!!document.querySelector('[role="alert"]')`)
  check('Invalid credentials receive explicit accessible feedback', await cdp.evaluate(`/Usuário ou senha incorretos/.test(document.querySelector('[role="alert"]').textContent)`))
  await screenshot('login-invalid-390x844.png')
  loginMode = 'network'; await loginFields(); await submit(); await waitFor(`!!document.querySelector('[role="alert"]') && !document.querySelector('.login-submit').disabled`)
  check('Network error receives distinct retryable feedback', await cdp.evaluate(`!/Usuário ou senha incorretos/.test(document.querySelector('[role="alert"]').textContent)`))
  await screenshot('login-network-390x844.png')
  loginMode = 'loading'; await loginFields(); await click('input[type="checkbox"]')
  const beforeDouble = requests.filter(item => item.path.endsWith('/auth/login')).length
  await cdp.evaluate(`(() => { const form = document.querySelector('.login-screen form'); form.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})); form.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})); })()`)
  await waitFor(`document.querySelector('.login-submit')?.disabled === true`)
  check('Loading state provides progress label', await cdp.evaluate(`/ENTRANDO/i.test(document.querySelector('.login-submit').textContent)`))
  await screenshot('login-loading-390x844.png')
  await waitFor(`!!document.querySelector('.seller-portal h1')`)
  check('Repeated login submits send exactly one request', requests.filter(item => item.path.endsWith('/auth/login')).length === beforeDouble + 1)
  check('Remember device is submitted explicitly', requests.filter(item => item.path.endsWith('/auth/login')).at(-1).rememberDevice === true)
  const persistentCookie = await cookie()
  check('Remembered synthetic session is HttpOnly with expiry', persistentCookie?.httpOnly && !persistentCookie.session && persistentCookie.expires > Date.now()/1000, persistentCookie ? { httpOnly:persistentCookie.httpOnly,session:persistentCookie.session,sameSite:persistentCookie.sameSite,expires:persistentCookie.expires } : null)
  await assertNoStorageCredentials('Login keeps credentials out of browser readable storage')
  identityDelay = 700
  await cdp.send('Page.navigate', { url: `${origin}/` }); await delay(200)
  check('Startup validates session without briefly rendering login', await cdp.evaluate(`!document.querySelector('.login-submit')`))
  await waitFor(`!!document.querySelector('.seller-portal h1')`); identityDelay = 0
  check('Valid cookie restores portal without new login', await cdp.evaluate(`location.pathname === '/portal' && !document.querySelector('.login-screen')`))
  await logout(); check('Logout revokes and deletes synthetic cookie', !activeSession && !(await cookie()))
  await assertNoStorageCredentials('Logout leaves no browser readable token or password')
  loginMode = 'success'; await loginFields(); await submit(); await waitFor(`!!document.querySelector('.seller-portal h1')`)
  const sessionCookie = await cookie(); check('Unremembered login receives a session cookie', sessionCookie?.httpOnly && sessionCookie.session, sessionCookie ? { httpOnly:sessionCookie.httpOnly,session:sessionCookie.session } : null)
  activeSession = false
  await cdp.send('Page.navigate', { url: `${origin}/portal` }); await waitFor(`!!document.querySelector('.login-submit')`)
  check('Expired cookie startup returns login and no commercial view', await cdp.evaluate(`!document.querySelector('.seller-portal')`))
  await cdp.send('Emulation.setEmulatedMedia', { features: [{ name: 'display-mode', value: 'standalone' }] })
  result.standaloneMedia = await cdp.evaluate(`matchMedia('(display-mode: standalone)').matches`)
  if (result.standaloneMedia) { await screenshot('login-standalone-390x844.png'); check('Standalone emulation retains usable login', await cdp.evaluate(`!!document.querySelector('.login-submit') && document.documentElement.scrollWidth <= innerWidth`)) }
  else result.limitations.push('Chrome did not emulate display-mode: standalone; real PWA standalone remains a manual check.')
  await waitFor(`!!navigator.serviceWorker.controller`)
  result.cachedUrls = await cdp.evaluate(`(async () => { const keys = await caches.keys(); return (await Promise.all(keys.map(async key => (await (await caches.open(key)).keys()).map(request => request.url)))).flat() })()`)
  check('PWA caches public offline fallback', result.cachedUrls.some(url => new URL(url).pathname === '/offline.html'))
  check('PWA cache excludes all API responses', result.cachedUrls.every(url => !new URL(url).pathname.startsWith('/api')))
  // A server outage also covers Service Worker fetches, which page-only CDP network emulation misses.
  simulateNetworkFailure = true
  await cdp.send('Network.emulateNetworkConditions', { offline:true,latency:0,downloadThroughput:0,uploadThroughput:0 })
  await cdp.send('Page.navigate', { url: `${origin}/portal` })
  await waitFor(`document.body.textContent.includes('Você está offline.')`)
  check('Offline portal navigation shows public fallback without commercial data', await cdp.evaluate(`document.body.textContent.includes('Você está offline.') && !document.querySelector('.seller-portal') && !document.body.textContent.includes('Mercado Sintético')`))
  await screenshot('login-offline-390x844.png')
  simulateNetworkFailure = false
  await cdp.send('Network.emulateNetworkConditions', { offline:false,latency:0,downloadThroughput:-1,uploadThroughput:-1 })
  check('API requests use cookie mode and never send a readable bearer token', requests.every(item => item.cookieMode && !item.hasAuthorization))
  check('No JavaScript runtime exceptions', result.runtimeErrors.length === 0, result.runtimeErrors)
  check('No JavaScript console errors', result.consoleErrors.length === 0, result.consoleErrors)
  check('No unexpected HTTP errors', result.httpErrors.every(item => item.status === 401 && new URL(item.url).pathname.startsWith('/api/')), result.httpErrors)
  const unexpectedExternal = result.blockedExternalRequests.filter(url => !['fonts.googleapis.com','fonts.gstatic.com'].includes(new URL(url).hostname))
  check('No unexpected external requests attempted', unexpectedExternal.length === 0, unexpectedExternal)
  if (result.blockedExternalRequests.length) result.limitations.push('Existing optional Google Fonts requests were blocked before external network access; screenshots use the local fallback font.')
} catch (error) {
  if (cdp) try { result.lastPageState = await cdp.evaluate(`({url:location.href,text:document.body?.innerText?.slice(0,1200)})`); await screenshot('login-failure.png') } catch {}
  result.failure = error.stack ?? String(error); check('Browser verification completed', false, result.failure)
} finally {
  if (cdp) { try { await cdp.send('Browser.close', {}, null) } catch {} cdp.socket.close() }
  if (browser && browser.exitCode === null) { await Promise.race([once(browser, 'exit').catch(() => {}), delay(3000)]); if (browser.exitCode === null) browser.kill() }
  server.closeAllConnections(); server.close()
  if (profile) { const absoluteProfile = resolve(profile); if (absoluteProfile.startsWith(`${resolve(tmpdir())}${sep}`) && basename(absoluteProfile).startsWith('orobi-login-ui-')) try { await rm(absoluteProfile, { recursive: true,force: true,maxRetries: 5,retryDelay: 300 }) } catch(error) { result.cleanupNote = error.code } }
  result.passed = result.assertions.length > 0 && result.assertions.every(item => item.passed)
  await mkdir(evidence, { recursive: true }); await writeFile(resolve(evidence, 'login-browser-verification.json'), `${JSON.stringify(result,null,2)}\n`)
  console.log(JSON.stringify({ passed:result.passed,assertions:result.assertions.length,failures:result.assertions.filter(item => !item.passed).map(item => item.name),report:'docs/audits/login-evidence/login-browser-verification.json' }))
  if (!result.passed) process.exitCode = 1
}
