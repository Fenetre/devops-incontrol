import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useApi } from '../composables/useApi.js'

describe('useApi SWR cache', () => {
  let fetchCount

  beforeEach(() => {
    fetchCount = 0
    vi.stubGlobal('fetch', vi.fn(async () => {
      fetchCount++
      return {
        ok: true,
        json: async () => ({ result: 'data' }),
      }
    }))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('serves cached data on second GET within TTL', async () => {
    const api = useApi()
    const r1 = await api.get('/api/test-swr-' + Math.random())
    expect(r1).toEqual({ result: 'data' })
    expect(fetchCount).toBe(1)

    const r2 = await api.get('/api/test-swr-' + (Math.random() - 1)) // different url → new fetch
    expect(fetchCount).toBe(2)
  })

  it('returns cached result for same URL within 30s', async () => {
    const api = useApi()
    const url = '/api/swr-same-' + Date.now()
    await api.get(url)
    expect(fetchCount).toBe(1)

    await api.get(url)
    expect(fetchCount).toBe(1) // no new fetch — served from cache
  })

  it('POST invalidates cache for same URL prefix', async () => {
    const api = useApi()
    const url = '/api/swr-inv-' + Date.now()
    await api.get(url)
    expect(fetchCount).toBe(1)

    await api.post(url, { x: 1 })
    expect(fetchCount).toBe(2)

    await api.get(url)
    expect(fetchCount).toBe(3) // cache was invalidated
  })
})
