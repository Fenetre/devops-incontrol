const BASE = ''  // Vite proxy handles /api → backend
const SWR_TTL = 30_000 // 30 seconds stale-while-revalidate window
const swrCache = new Map() // key → { data, timestamp }
let apiKeyMemory = ''

// Keep password in memory only; do not persist to browser storage.
function getApiKey() {
  return apiKeyMemory
}

export function setApiKey(key) {
  apiKeyMemory = key || ''
}

async function request(url, options = {}) {
  const apiKey = getApiKey()
  const headers = { 'Content-Type': 'application/json', ...options.headers }
  if (apiKey) {
    headers['X-API-Key'] = apiKey
  }
  const fetchOpts = { headers, ...options }
  // Bypass the browser HTTP cache for all API requests.
  // Our own SWR layer handles caching; the browser cache can serve stale
  // HTML responses from before the SPA-fallback fix was deployed.
  fetchOpts.cache = 'no-store'

  const method = options.method || 'GET'
  const maxRetries = method === 'GET' ? 2 : 0
  let lastError

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    if (attempt > 0) await new Promise(r => setTimeout(r, 1000 * attempt))

    const res = await fetch(BASE + url, fetchOpts)
    if (!res.ok) {
      const body = await res.json().catch(() => ({}))
      const detail = typeof body.detail === 'string' ? body.detail : body.detail ? JSON.stringify(body.detail) : null
      throw new Error(detail || `${method} ${url} failed (${res.status})`)
    }
    // Guard against HTML responses (e.g. proxy error page when backend is temporarily unreachable)
    const ct = res.headers?.get?.('content-type') || ''
    if (ct.includes('html')) {
      await res.text().catch(() => '') // consume body before retry
      lastError = new Error(`${method} ${url} returned an HTML page instead of JSON. The backend may be temporarily unreachable.`)
      if (attempt < maxRetries) continue // retry
      throw lastError
    }
    return res.json()
  }
}

function getCached(url) {
  const entry = swrCache.get(url)
  if (!entry) return null
  if (Date.now() - entry.timestamp < SWR_TTL) return entry.data
  return null
}

async function getWithSwr(url) {
  const cached = getCached(url)
  if (cached !== null) return cached
  const data = await request(url)
  swrCache.set(url, { data, timestamp: Date.now() })
  return data
}

function invalidateCache(url) {
  // Invalidate exact match and any keys starting with the url (for prefixes)
  for (const key of swrCache.keys()) {
    if (key === url || key.startsWith(url)) swrCache.delete(key)
  }
}

export function useApi() {
  return {
    get: (url) => getWithSwr(url),
    invalidate: (url) => invalidateCache(url),
    post: (url, data) => {
      invalidateCache(url)
      return request(url, { method: 'POST', body: JSON.stringify(data) })
    },
    postForm: (url, formData) => {
      invalidateCache(url)
      const apiKey = getApiKey()
      const headers = {}
      if (apiKey) headers['X-API-Key'] = apiKey
      return fetch(BASE + url, { method: 'POST', headers, body: formData, cache: 'no-store' })
        .then(async res => {
          if (!res.ok) {
            const body = await res.json().catch(() => ({}))
            throw new Error(body.detail || `POST ${url} failed (${res.status})`)
          }
          return res.json()
        })
    },
    put: (url, data) => {
      invalidateCache(url)
      return request(url, { method: 'PUT', body: JSON.stringify(data) })
    },
    patch: (url, data) => {
      return request(url, { method: 'PATCH', body: JSON.stringify(data) })
    },
    del: (url, data) => {
      invalidateCache(url)
      return request(url, { method: 'DELETE', ...(data ? { body: JSON.stringify(data) } : {}) })
    },
  }
}
