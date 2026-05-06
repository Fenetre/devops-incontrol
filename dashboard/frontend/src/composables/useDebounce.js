import { ref, watch } from 'vue'

/**
 * Returns a debounced ref that updates `delay` ms after the source stops changing.
 * @param {import('vue').Ref<string>} source - reactive source ref
 * @param {number} delay - debounce delay in ms (default 250)
 * @returns {import('vue').Ref<string>}
 */
export function useDebouncedRef(source, delay = 250) {
  const debounced = ref(source.value)
  let timer = null
  watch(source, (val) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => { debounced.value = val }, delay)
  })
  return debounced
}
