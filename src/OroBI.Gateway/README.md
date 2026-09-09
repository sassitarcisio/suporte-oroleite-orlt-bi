# Oroleite same-origin gateway

Azure Functions programming model v3 on Node 22, using only built-in Node modules.
Deploy this directory as the managed API of **orobi-web**, not the legacy orlt-bi site.

Server-only configuration:

- `OROBI_API_ORIGIN`: HTTPS origin of the API; defaults to the current Oroleite Container App.
- `OROBI_PORTAL_ORIGIN`: exact permitted browser origin; defaults to the current orobi-web Static Web App.

The backend must enable its existing BrowserSession transport and permit the configured portal origin. Requests use `X-OroBI-Session: cookie` and forward only `__Host-OroBI.Session` from the browser Cookie header. Never pass platform cookies, client identity headers, Authorization, or forwarded host/IP headers upstream.

The proxy validates the raw API path before URL normalization, rejects foreign Origin/Fetch Metadata/Referer values, and supplies the configured portal Origin when a same-origin browser request omits it. It preserves byte buffers and separate response cookies via the Functions cookies binding. API responses are never cached. Network errors are generic and requests have a 40-second total deadline with no retries.

Functions v3 buffers requests and responses. Large/slow imports remain subject to Static Web Apps request-size and execution limits; a timeout does not prove an upstream mutation was rolled back. Unknown response cookie attributes fail closed rather than silently dropping a security flag.

Run `npm test` (or `node --test --experimental-test-isolation=none test/proxy.test.js`). Tests use a real loopback HTTP upstream; the production factory permits HTTP only through an explicit test-only option restricted to loopback IPs.

The v3 HTTP binding documents raw `bufferBody`, scalar response headers and separate `cookies[]`: [Microsoft Node.js Functions reference](https://learn.microsoft.com/en-us/azure/azure-functions/functions-reference-node?pivots=nodejs-model-v3).
