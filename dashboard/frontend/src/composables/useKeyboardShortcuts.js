import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'

/**
 * Global keyboard shortcuts.
 * Accepts reactive refs for toggles the parent controls (settings modal, help overlay).
 */
export function useKeyboardShortcuts({ showSettings, showShortcutsHelp }) {
  const router = useRouter()
  const pendingG = ref(false)
  let gTimer = null

  function isInputFocused() {
    const el = document.activeElement
    if (!el) return false
    const tag = el.tagName.toLowerCase()
    return tag === 'input' || tag === 'textarea' || tag === 'select' || el.isContentEditable
  }

  function handleKeydown(e) {
    // Escape always works — close modals
    if (e.key === 'Escape') {
      if (showShortcutsHelp.value) { showShortcutsHelp.value = false; return }
      if (showSettings.value) { showSettings.value = false; return }
      return
    }

    // All other shortcuts are suppressed when inside an input
    if (isInputFocused()) return

    // ? — toggle shortcuts help
    if (e.key === '?') {
      e.preventDefault()
      showShortcutsHelp.value = !showShortcutsHelp.value
      return
    }

    // Don't process navigation shortcuts while a modal is open
    if (showSettings.value || showShortcutsHelp.value) return

    // / — focus sidebar search
    if (e.key === '/') {
      e.preventDefault()
      const searchInput = document.querySelector('[data-sidebar-search]')
      if (searchInput) searchInput.focus()
      return
    }

    // , — open settings
    if (e.key === ',') {
      e.preventDefault()
      showSettings.value = true
      return
    }

    // g + key — navigation sequences
    if (e.key === 'g' && !pendingG.value) {
      pendingG.value = true
      gTimer = setTimeout(() => { pendingG.value = false }, 600)
      return
    }

    if (pendingG.value) {
      pendingG.value = false
      clearTimeout(gTimer)
      const routes = {
        h: '/',           // home / dashboard
        p: '/pr-monitor',
        d: '/db-monitor',
        v: '/velocity',
        c: '/config',
        s: '/sprint-populator',
        t: '/template-manager',
        a: '/dev-assessment',
      }
      const target = routes[e.key]
      if (target) {
        e.preventDefault()
        router.push(target)
      }
      return
    }
  }

  onMounted(() => document.addEventListener('keydown', handleKeydown))
  onUnmounted(() => {
    document.removeEventListener('keydown', handleKeydown)
    clearTimeout(gTimer)
  })
}
