const PUBLIC_CACHE = 'orobi-portal-public-v1'
const PUBLIC_FILES = ['/offline.html', '/portal-icon.svg', '/portal-icon-192.png', '/portal-icon-512.png', '/apple-touch-icon.png', '/logoOroleite.png', '/manifest.webmanifest']
self.addEventListener('install', event => {
  event.waitUntil(caches.open(PUBLIC_CACHE).then(cache => cache.addAll(PUBLIC_FILES)))
})
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('orobi-portal-public-') && key !== PUBLIC_CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim()))
})
self.addEventListener('fetch', event => {
  const request = event.request
  const url = new URL(request.url)
  // Only explicitly public files are stored. Never intercept API, cross-origin or authenticated requests.
  if (url.origin !== self.location.origin || request.method !== 'GET' || url.pathname === '/api' || url.pathname.startsWith('/api/') || request.headers.has('Authorization')) return
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => caches.match('/offline.html')))
    return
  }
  if (PUBLIC_FILES.includes(url.pathname) && !url.search) {
    event.respondWith(caches.match(url.pathname).then(cached => cached || fetch(request)))
  }
})
