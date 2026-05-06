import { describe, it, expect } from 'vitest'
import { useApi } from '../composables/useApi.js'

describe('smoke', () => {
  it('useApi returns expected shape', () => {
    const api = useApi()
    expect(api).toHaveProperty('get')
    expect(api).toHaveProperty('post')
    expect(api).toHaveProperty('put')
    expect(api).toHaveProperty('del')
    expect(typeof api.get).toBe('function')
    expect(typeof api.post).toBe('function')
  })
})
