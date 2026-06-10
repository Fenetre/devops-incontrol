import { ref, watchEffect } from 'vue'

const stored = localStorage.getItem('theme')
const systemDark = window.matchMedia('(prefers-color-scheme: dark)')
const dark = ref(stored ? stored === 'dark' : systemDark.matches)

watchEffect(() => {
  document.documentElement.classList.toggle('dark', dark.value)
  localStorage.setItem('theme', dark.value ? 'dark' : 'light')
})

// Sync when the OS theme changes (only when user hasn't explicitly toggled)
let userToggled = !!stored
systemDark.addEventListener('change', (e) => {
  if (!userToggled) {
    dark.value = e.matches
  }
})

export function useTheme() {
  function toggle() {
    userToggled = true
    dark.value = !dark.value
  }
  return { dark, toggle }
}
