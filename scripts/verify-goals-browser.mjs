// Local synthetic browser verification; no installed browser automation dependency or production API.
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import { once } from 'node:events'
import { readFile, writeFile, mkdir, mkdtemp, rm, stat } from 'node:fs/promises'
import { dirname, resolve, extname, sep, basename } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const dist = resolve(root, 'src/OroBI.Web/dist')
const evidence = resolve(root, 'docs/audits/evidence/goals-ui')
const chromePath = process.env.PORTAL_TEST_CHROME ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe'
const widths = [360, 390, 430, 768, 1366, 1920]
const delay = ms => new Promise(done => setTimeout(done, ms))
const permissions = { canViewRevenue: true, canViewCommission: true, canViewPrize: true, canViewPPP: true, canViewGoals: true, canViewTrades: true, canViewCustomers: true }
const period = { grossSales: 132456.78, netRevenue: 129876.54, negativeMovements: -2580.24, saleQuantity: 456, movementCount: 37, customerCount: 18, documentCount: 29, averageTicket: 4478.50 }
const sale = { id: 'synthetic-sale', date: '2026-09-05', documentNumber: 'SYNTHETIC-001', movementType: 'VENDA', customerCode: 'SYN001', customerName: 'Mercado Sintético Jardim das Flores', productName: 'Produto sintético de demonstração com descrição extensa para inspeção da tela', brand: 'Marca Sintética', quantity: 12, totalValue: 1234.56 }
const customer = { customerCode: sale.customerCode, customerName: sale.customerName, city: 'Cidade Sintética', grossSales: 1234.56, netRevenue: 1200, documentCount: 2, lastPurchaseDate: sale.date, averageTicket: 600, purchasedQuantity: 12 }
const closing = { year: 2026, month: 9, status: 'Aprovado', isEstimated: false, approvedAtUtc: '2026-09-05T16:00:00Z', revenue: 129876.54, commissionableRevenue: 129876.54, commission: 1298.7654, commissionPercent: 1, pppPercent: 90, pppAward: 200, revenueAward: 300, positivityAward: 100, tradeAward: 50, totalAwards: 650, tradeValue: 700, tradePercent: 0.54, commissionAndAwards: 1948.7654 }
const revenue = { brand: 'AÇAÍ FUTURO', type: 'FATURAMENTO', target: 1500, actual: 330.8, achievedPercent: 22.05333333333333, maximumPrize: 100, currentPrize: 0, nextTierPercent: 80, amountToNextTier: 869.2, nextTierPrize: 50 };
const positivity = { ...revenue, type: 'POSITIVACAO', target: 15, actual: 4, achievedPercent: 26.666666666666668, nextTierPercent: 100, amountToNextTier: 11, nextTierPrize: 100 };
let goals = { year: 2026, month: 8, available: true, unavailableReason: null, isApproved: false, items: [positivity, revenue] };
const requests = []
let simulateNetworkFailure = false
const brandRows = [
 {label:'TIA SONIA',netRevenue:51634.15,quantity:28,customerCount:11,revenueSharePercent:16.38},
 {label:'ZZZ - INATIVO F',netRevenue:-384.52,quantity:-2,customerCount:1,revenueSharePercent:-0.12},
 {label:'SEM INFORMACAO',netRevenue:157.86,quantity:1,customerCount:1,revenueSharePercent:0.05},
 {label:'MARCA ATIVA NEGATIVA',netRevenue:-890.12,quantity:-3,customerCount:2,revenueSharePercent:-0.28},
 {label:'MARCA ATIVA ZERO',netRevenue:0,quantity:0,customerCount:0,revenueSharePercent:0}
 ].map(row=>({...row,grossSales:row.netRevenue,movementCount:3}));
const fixture = pathname => {
  if(pathname.endsWith('/brands') || pathname.endsWith('/products')) return {items:brandRows,totalCount:brandRows.length,hasMore:true};
  if (pathname === '/api/v1/me' || pathname === '/api/me') return { userId: 'synthetic-browser-user', email: 'synthetic@example.invalid', userName: 'Conta sintética', roles: ['Vendedor'], sellerId: '00000000-0000-0000-0000-000000000001', seller: 'Vendedora Sintética', permissions, sellerAccesses: [] }
  if (pathname.endsWith('/dashboard')) return { startDate: '2026-09-01', endDate: '2026-09-30', referenceDate: '2026-09-05', period, today: period, month: period, dailyTrend: [{ date: sale.date, grossSales: period.grossSales, netRevenue: period.netRevenue, negativeMovements: period.negativeMovements }], freshness: { source: 'csv', updatedAtUtc: '2026-09-05T15:00:00Z', timestampKind: 'import-started' } }
  if (pathname.endsWith('/sales')) return { items: [sale, { ...sale, id: 'synthetic-sale-2', documentNumber: 'SYNTHETIC-002' }], page: 1, pageSize: 20, totalCount: 2 }
  if (pathname.endsWith('/customers')) return { observedBuyersOnly: true, items: [customer], totalCount: 1, hasMore: false }
  if (pathname.includes('/customers/')) return { customer, sales: [{ ...sale, physicalTrades: 61.728, tradeToSalesPercent: 5 }], totalCount: 1, hasMore: false }
  if (pathname.endsWith('/goals')) return goals
  if (pathname.endsWith('/ppp')) return { year: 2026, month: 9, available: true, unavailableReason: null, isEstimated: false, status: 'Aprovado', achievementPercent: 90, award: 200, segments: [{ segment: 'Mercados sintéticos', customerCount: 18, itemsPerSegment: 4, groupsPlaced: 3, achievementPercent: 90 }] }
  if (pathname.endsWith('/trades')) return { physicalTrades: 700, tradeToSalesPercent: 0.54, movementCount: 1, items: [{ ...sale, movementType: 'TROCA' }], hasMore: false }
  if (pathname.endsWith('/closings/history')) return [{ month: '2026-09', status: 'Aprovado' }]
  if (pathname.endsWith('/closings') || pathname.endsWith('/commission')) return closing
  if (pathname.endsWith('/products') || pathname.endsWith('/brands')) return { items: [{ label: 'Resultado sintético', grossSales: 1234, netRevenue: 1200, quantity: 12, movementCount: 2, customerCount: 1, revenueSharePercent: 100 }], totalCount: 1, hasMore: false }
  return undefined
}
const mimeTypes = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.webmanifest': 'application/manifest+json', '.woff2': 'font/woff2' }
const server = createServer(async (request, response) => {
  if (simulateNetworkFailure) { request.socket.destroy(); return }
  try {
    const pathname = decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname)
    if (pathname.startsWith('/api/')) {
      requests.push({ method: request.method, path: request.url })
      response.setHeader('Cache-Control', 'no-store')
      if (pathname.endsWith('/auth/logout')) { response.writeHead(204).end(); return }
      const body = fixture(pathname)
      response.writeHead(body === undefined ? 404 : 200, { 'Content-Type': 'application/json' })
      response.end(JSON.stringify(body ?? { error: 'Synthetic route is not configured.' })); return
    }
    const path = pathname === '/' || pathname === '/portal' || pathname.startsWith('/portal/') ? resolve(dist, 'index.html') : resolve(dist, `.${pathname}`)
    if (!path.startsWith(`${dist}${sep}`)) { response.writeHead(403).end(); return }
    response.writeHead(200, { 'Content-Type': mimeTypes[extname(path)] ?? 'application/octet-stream' })
    response.end(await readFile(path))
  } catch { if (!response.headersSent) response.writeHead(404); response.end() }
})

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

let browser, cdp, profile
const result = { generatedAtUtc: new Date().toISOString(), environment: 'Local Chrome headless, synthetic API fixtures only', limitations: ['No production endpoint or account was contacted.', 'Viewport emulation does not certify physical Android/iOS installation, touch input or device rendering.', 'HTTP server emulates SPA fallback; actual Azure deployment is not exercised.'], browserVersion: null, viewports: [], browserErrors: [], blockedExternalRequests: [], assertions: [], apiRequests: requests }
const check = (name, passed, details = null) => { result.assertions.push({ name, passed: !!passed, details }); if (!passed) console.error(`FAIL: ${name}`) }
async function waitFor(expression) {
  for (let attempt = 0; attempt < 100; attempt++) { if (await cdp.evaluate(expression)) return; await delay(100) }
  throw new Error(`UI did not settle: ${expression}`)
}
const layoutExpression = `(() => { const visible = element => element && element.getBoundingClientRect().width > 0; return { viewportWidth: innerWidth, documentWidth: document.documentElement.scrollWidth, heading: document.querySelector('h1')?.textContent, headerVisible: visible(document.querySelector('.portal-header')), bottomNavigationVisible: visible(document.querySelector('.portal-bottom-nav')), desktopNavigationVisible: visible(document.querySelector('.portal-desktop-nav')), alerts: [...document.querySelectorAll('[role="alert"]')].map(item => item.textContent), outlyingElements: [...document.querySelectorAll('.portal-content *')].filter(item => { const box = item.getBoundingClientRect(); return box.width > 0 && (box.right > innerWidth + 1 || box.left < -1) && !item.closest('.portal-table-scroll'); }).slice(0,8).map(item => ({ tag: item.tagName, className: item.className, right: item.getBoundingClientRect().right })) }; })()`
async function capture(width, view) {
  await delay(350)
  const layout = await cdp.evaluate(layoutExpression)
  const screenshot = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false })
  const filename = `portal-ui-${width}-${view}.png`
  await writeFile(resolve(evidence, filename), Buffer.from(screenshot.data, 'base64'))
  check(`${width}px ${view}: no document horizontal overflow`, layout.documentWidth <= width + 1, layout)
  check(`${width}px ${view}: header and correct navigation visible`, layout.headerVisible && (width < 1100 ? layout.bottomNavigationVisible : layout.desktopNavigationVisible))
  check(`${width}px ${view}: no error alert`, layout.alerts.length === 0, layout.alerts)
  return { view, screenshot: `docs/audits/evidence/goals-ui/${filename}`, ...layout }
}
async function clickLabel(label) {
  const clicked = await cdp.evaluate(`(() => { const item = [...document.querySelectorAll('button')].find(button => button.textContent.trim() === ${JSON.stringify(label)} && button.getBoundingClientRect().width > 0); item?.click(); return !!item; })()`)
  if (!clicked) throw new Error(`Visible button not found: ${label}`)
}

try {
  await stat(resolve(dist, 'index.html'))
  await mkdir(evidence, { recursive: true })
  await new Promise(done => server.listen(0, '127.0.0.1', done))
  const origin = `http://127.0.0.1:${server.address().port}`
  profile = await mkdtemp(resolve(tmpdir(), 'orobi-portal-ui-'))
  browser = spawn(chromePath, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--disable-background-networking', '--disable-component-update', '--disable-sync', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] })
  let launchError
  browser.on('error', error => { launchError = error })
  browser.stderr.on('data', () => {})
  let debugging
  for (let attempt = 0; attempt < 150; attempt++) {
    if (launchError) throw launchError
    try { debugging = (await readFile(resolve(profile, 'DevToolsActivePort'), 'utf8')).trim().split('\n'); break } catch { await delay(100) }
  }
  if (!debugging) throw new Error('Chrome debugging endpoint did not start.')
  const socket = new WebSocket(`ws://127.0.0.1:${debugging[0]}${debugging[1].trim()}`)
  await once(socket, 'open'); cdp = new Cdp(socket)
  result.browserVersion = await cdp.send('Browser.getVersion')
  const target = await cdp.send('Target.createTarget', { url: 'about:blank' })
  cdp.sessionId = (await cdp.send('Target.attachToTarget', { targetId: target.targetId, flatten: true })).sessionId
  await cdp.send('Page.enable'); await cdp.send('Runtime.enable'); await cdp.send('Network.enable')
  cdp.on('Runtime.exceptionThrown', event => result.browserErrors.push(event.exceptionDetails.exception?.description ?? event.exceptionDetails.text))
  cdp.on('Runtime.consoleAPICalled', event=>{if(event.type==='error')result.browserErrors.push(event.args.map(arg=>arg.value??arg.description).join(' '))})
  await cdp.send('Fetch.enable', { patterns: [{ urlPattern: '*' }] })
  cdp.on('Fetch.requestPaused', event => {
    const url = new URL(event.request.url)
    if (url.origin === origin || url.protocol === 'data:') void cdp.send('Fetch.continueRequest', { requestId: event.requestId })
    else { result.blockedExternalRequests.push(event.request.url); void cdp.send('Fetch.failRequest', { requestId: event.requestId, errorReason: 'BlockedByClient' }) }
  })
  await cdp.send('Page.addScriptToEvaluateOnNewDocument', { source: `if (location.origin === ${JSON.stringify(origin)}) sessionStorage.setItem('orobi.access-token', 'synthetic-browser-token');` })
  async function openGoals(width) {
    const height = width < 768 ? 900 : 1000;
    await cdp.send('Emulation.setDeviceMetricsOverride', {width,height,deviceScaleFactor:1,mobile:width<768});
    await cdp.send('Page.navigate', {url:origin+'/portal'});
    await waitFor("document.querySelector('h1')?.textContent === 'Meu desempenho' && !document.querySelector('.portal-loading')");
    await clickLabel(width<1100?'Metas':'Metas e prêmios');
    await waitFor("document.querySelectorAll('.goal-card').length > 0 && !document.querySelector('.portal-loading')");
    await cdp.evaluate(`(() => {
      const input = document.querySelector('input[type=month]');
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(input, '2026-08');
      input.dispatchEvent(new Event('input', {bubbles:true}));
      input.dispatchEvent(new Event('change', {bubbles:true}));
    })()`);
    await waitFor("document.querySelectorAll('.goal-card').length > 0 && !document.querySelector('.portal-loading')");
    await cdp.evaluate('document.fonts.ready.then(()=>true)');
    return height;
  }
  for (const width of widths) {
    const height=await openGoals(width);
    const actual=await cdp.evaluate(`(() => {
      const grid=document.querySelector('.goal-grid'), cards=[...grid.children];
      const heading=document.querySelector('.portal-heading');
      const label=heading.querySelector('label'), title=heading.querySelector('h1');
      return {columns:getComputedStyle(grid).gridTemplateColumns.split(' ').length,
        cards:cards.map(card=>({text:card.textContent.replace(/\\s+/g,' '),margin:getComputedStyle(card).marginTop,padding:getComputedStyle(card).paddingTop,barHeight:card.querySelector('[role=progressbar]').getBoundingClientRect().height,aria:card.querySelector('[role=progressbar]').getAttribute('aria-valuenow')})),
        monthBelow:label.getBoundingClientRect().top>title.getBoundingClientRect().bottom,
        subtitle:!!heading.querySelector('.goal-subtitle')};
    })()`);
    check(width+'px correct columns',actual.columns===(width<1100?1:2),actual.columns);
    check(width+'px bar thickness and card spacing',actual.cards.every(card=>card.barHeight===10&&card.margin==='0px'&&parseFloat(card.padding)>=20),actual.cards);
    check(width+'px reference values unchanged',actual.cards[0].text.includes('26,67%')&&actual.cards[0].text.includes('4 de 15 clientes')&&actual.cards[0].text.includes('11 clientes')&&actual.cards[0].text.includes('100,00')&&actual.cards[1].text.includes('22,05%')&&actual.cards[1].text.includes('330,80')&&actual.cards[1].text.includes('1.500,00')&&actual.cards[1].text.includes('869,20')&&actual.cards[1].text.includes('50,00'),actual.cards);
    check(width+'px exact accessible percentages',actual.cards[0].aria===String(positivity.achievedPercent)&&actual.cards[1].aria===String(revenue.achievedPercent));
    check(width+'px subtitle and mobile month layout',actual.subtitle&&(width>=768||actual.monthBelow));
    check(width+'px month selector remains functional',requests.some(request=>request.path.includes('/goals?month=2026-08')));
    const layout=await capture(width,'goals');
    check(width+'px no overflowing card content',layout.outlyingElements.length===0,layout.outlyingElements);
    if(width<768){ await cdp.evaluate("document.querySelectorAll('.goal-card')[1].scrollIntoView({block:'center',behavior:'instant'})"); await capture(width,'goals-revenue'); }
    result.viewports.push({width,height,actual,layout});
  }
  goals = {...goals,isApproved:true,items:[
    {...revenue,achievedPercent:87.4,actual:1311,currentPrize:50,nextTierPercent:90,amountToNextTier:39,nextTierPrize:75},
    {...positivity,achievedPercent:100,actual:15,currentPrize:91.23,nextTierPercent:null,amountToNextTier:null,nextTierPrize:null},
    {...revenue,brand:'ACIMA DA META',achievedPercent:125.6,actual:1884,currentPrize:100,nextTierPercent:null,amountToNextTier:null,nextTierPrize:null},
    {...positivity,brand:'PRÊMIO RESTRITO',currentPrize:null,maximumPrize:null,nextTierPrize:null},
    {...revenue,brand:'SEM CONFIGURAÇÃO',target:0,achievedPercent:null,currentPrize:null,maximumPrize:null,nextTierPercent:null,amountToNextTier:null,nextTierPrize:null}
  ]};
  await openGoals(390);
  const edges=await cdp.evaluate("[...document.querySelectorAll('.goal-card')].map(card=>card.textContent)");
  check('Between tiers and frozen official prize',edges[0].includes('Faixa atual: 80%')&&edges[0].includes('para atingir 90%')&&edges[0].includes('75,00')&&edges[1].includes('Prêmio conquistado')&&edges[1].includes('91,23'),edges);
  check('Exceeded and restricted and missing values',edges[2].includes('Acima da meta')&&!edges[2].includes('Faltam')&&!edges[3].includes('R$')&&edges[4].includes('Sem percentual')&&!edges[4].includes('Faltam'));
  await capture(390,'goals-edge-cases');
  const accessibility = await cdp.evaluate(`(() => {
    const rgb = color => color.match(/[\\d.]+/g).slice(0,3).map(Number);
    const luminance = color => rgb(color).map(value => { value/=255; return value<=.04045?value/12.92:((value+.055)/1.055)**2.4; }).reduce((sum,value,index)=>sum+value*[.2126,.7152,.0722][index],0);
    const contrasts = [...document.querySelectorAll('.goal-card strong,.goal-card p,.goal-card dt,.goal-card dd,.goal-status,.goal-type,.goal-prize-kind,.goal-tiers li')].map(element=>{
      let ancestor=element;
      while(ancestor.parentElement && getComputedStyle(ancestor).backgroundColor==='rgba(0, 0, 0, 0)') ancestor=ancestor.parentElement;
      const style=getComputedStyle(element), values=[luminance(style.color),luminance(getComputedStyle(ancestor).backgroundColor)].sort((a,b)=>b-a);
      return {text:element.textContent,ratio:(values[0]+.05)/(values[1]+.05),minimum:parseFloat(style.fontSize)>=24?3:4.5};
    });
    const input=document.querySelector('input[type=month]'); input.focus();
    return {contrasts,focused:document.activeElement===input,focusBorder:getComputedStyle(input).borderColor};
  })()`);
  check('Goal text contrast meets AA thresholds',accessibility.contrasts.every(item=>item.ratio>=item.minimum),accessibility.contrasts);
  check('Month selector retains visible focus',accessibility.focused&&accessibility.focusBorder==='rgb(242, 201, 76)',accessibility.focusBorder);
  check('No browser runtime exception or console error',result.browserErrors.length===0,result.browserErrors);
  const unexpected=result.blockedExternalRequests.filter(url=>!['fonts.googleapis.com','fonts.gstatic.com'].includes(new URL(url).hostname));
  check('No external API or unexpected asset requests',unexpected.length===0,unexpected);
  result.limitations.push('This run uses local goal fixtures from the user reference and invented edge cases and no production credentials; existing optional Google Fonts requests were blocked.');
} catch (error) {
  if (cdp) { try { result.lastPageState = await cdp.evaluate(`({url:location.href,title:document.title,text:document.body?.innerText?.slice(0,1000),serviceWorkerControlled:!!navigator.serviceWorker?.controller})`) } catch {} }
  result.failure = error.stack ?? String(error)
  check('Browser verification completed', false, result.failure)
} finally {
  if (cdp) { try { await cdp.send('Browser.close', {}, null) } catch {} cdp.socket.close() }
  if (browser && browser.exitCode === null) { await Promise.race([once(browser, 'exit').catch(() => {}), delay(3000)]); if (browser.exitCode === null) browser.kill() }
  server.closeAllConnections(); server.close()
  if (profile) {
    const absoluteProfile = resolve(profile)
    const expectedParent = `${resolve(tmpdir())}${sep}`
    if (absoluteProfile.startsWith(expectedParent) && basename(absoluteProfile).startsWith('orobi-portal-ui-')) {
      try { await rm(absoluteProfile, { recursive: true, force: true, maxRetries: 5, retryDelay: 300 }) } catch (error) { result.cleanupNote = `Temporary profile cleanup: ${error.code}` }
    }
  }
  result.passed = result.assertions.length > 0 && result.assertions.every(item => item.passed)
  await mkdir(evidence, { recursive: true })
  await writeFile(resolve(evidence, 'goals-ui-verification.json'), `${JSON.stringify(result, null, 2)}\n`)
  console.log(JSON.stringify({ passed: result.passed, viewports: result.viewports.map(item => item.width), assertions: result.assertions.length, failures: result.assertions.filter(item => !item.passed).map(item => item.name), report: 'docs/audits/evidence/goals-ui/goals-ui-verification.json' }))
  if (!result.passed) process.exitCode = 1
}
