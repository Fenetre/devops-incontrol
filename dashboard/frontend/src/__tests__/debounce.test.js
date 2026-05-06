import { describe, it, expect, vi } from 'vitest'
import { ref, nextTick } from 'vue'
import { useDebouncedRef } from '../composables/useDebounce.js'

describe('useDebouncedRef', () => {
  it('does not update before delay elapses', async () => {
    vi.useFakeTimers()
    const source = ref('')
    const debounced = useDebouncedRef(source, 250)

    source.value = 'a'
    await nextTick()
    expect(debounced.value).toBe('')

    source.value = 'ab'
    await nextTick()
    expect(debounced.value).toBe('')

    vi.advanceTimersByTime(250)
    expect(debounced.value).toBe('ab')
    vi.useRealTimers()
  })

  it('fires only once after rapid changes', async () => {
    vi.useFakeTimers()
    const source = ref('')
    const debounced = useDebouncedRef(source, 250)

    for (let i = 0; i < 10; i++) {
      source.value = 'x'.repeat(i + 1)
      await nextTick()
    }

    vi.advanceTimersByTime(250)
    expect(debounced.value).toBe('xxxxxxxxxx')
    vi.useRealTimers()
  })
})
