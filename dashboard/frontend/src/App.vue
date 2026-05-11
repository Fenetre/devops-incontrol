<template>
  <!-- Setup wizard (no auth gate, no sidebar) -->
  <router-view v-if="isSetupRoute" />

  <!-- Password login gate -->
  <div v-else-if="!store.authenticated" class="flex items-center justify-center h-screen bg-gray-100 dark:bg-gray-900">
    <div class="bg-white dark:bg-gray-800 rounded-xl shadow-lg p-8 w-full max-w-sm mx-4">
      <div class="flex items-center gap-2 mb-6">
        <svg class="w-7 h-7 text-primary-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z" />
        </svg>
        <h2 class="text-lg font-semibold text-gray-900 dark:text-gray-100">
          {{ store.apiKeyConfigured ? 'Enter Password' : 'Create Password' }}
        </h2>
      </div>
      <p class="mb-4 text-sm text-gray-600 dark:text-gray-300">
        {{ store.apiKeyConfigured ? 'Authentication is required to unlock the dashboard.' : 'No password is configured yet. Enter one to secure and unlock this dashboard.' }}
      </p>
      <p class="mb-3 text-xs text-gray-500 dark:text-gray-400">
        Once verified, this browser is remembered with a secure HttpOnly session cookie.
      </p>
      <form @submit.prevent="unlock">
        <input
          v-model="loginKey"
          type="password"
          placeholder="Password…"
          autofocus
          class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
        />
        <p v-if="loginError" class="mt-2 text-sm text-red-600 dark:text-red-400">
          {{ store.apiKeyConfigured ? 'Invalid password' : 'Password is required' }}
        </p>
        <button
          type="submit"
          :disabled="unlocking"
          class="mt-4 w-full px-4 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 disabled:opacity-50 transition-colors"
        >
          {{ unlocking ? (store.apiKeyConfigured ? 'Verifying…' : 'Saving…') : (store.apiKeyConfigured ? 'Unlock' : 'Save & Unlock') }}
        </button>
      </form>
    </div>
  </div>

  <div v-else class="flex h-screen bg-gray-100 dark:bg-gray-900">
    <!-- Sidebar -->
    <SidebarMenu @show-release-notes="showReleaseNotes = true" />

    <!-- Main content -->
    <div class="flex-1 flex flex-col overflow-hidden">
      <!-- Top bar -->
      <header class="bg-gray-50 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 px-6 py-3 flex items-center justify-between shrink-0">
        <div class="flex items-center gap-3">
          <router-link to="/" class="flex items-center gap-2 text-gray-800 dark:text-gray-200 hover:text-primary-600 transition-colors">
            <img src="/favicon.svg" alt="DevOps InControl" class="w-7 h-7" />
            <h1 class="text-lg font-semibold dark:text-gray-100">DevOps InControl Dashboard</h1>
          </router-link>
        </div>

        <div class="flex items-center gap-3">
          <!-- Demo mode indicator -->
          <span
            v-if="isDemoMode"
            class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-100 dark:bg-amber-900/50 text-amber-700 dark:text-amber-300 border border-amber-300 dark:border-amber-600"
          >DEMO</span>
          <!-- PAT status dot -->
          <span
            class="w-2.5 h-2.5 rounded-full"
            :class="store.patConfigured ? 'bg-green-500' : 'bg-red-400'"
            :title="store.patConfigured ? 'PAT configured' : 'PAT not set'"
          ></span>
          <span
            class="text-xs font-semibold"
            :class="store.patConfigured ? 'text-green-700 dark:text-green-300' : 'text-red-600 dark:text-red-300'"
          >
            {{ store.patConfigured ? 'PAT OK' : 'PAT Missing' }}
          </span>
          <!-- Theme toggle -->
          <button
            @click="theme.toggle()"
            class="p-2 rounded-lg text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            :title="theme.dark.value ? 'Switch to light mode' : 'Switch to dark mode'"
          >
            <!-- Sun icon (shown in dark mode) -->
            <svg v-if="theme.dark.value" class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 3v2.25m6.364.386l-1.591 1.591M21 12h-2.25m-.386 6.364l-1.591-1.591M12 18.75V21m-4.773-4.227l-1.591 1.591M5.25 12H3m4.227-4.773L5.636 5.636M15.75 12a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0z" />
            </svg>
            <!-- Moon icon (shown in light mode) -->
            <svg v-else class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M21.752 15.002A9.718 9.718 0 0118 15.75c-5.385 0-9.75-4.365-9.75-9.75 0-1.33.266-2.597.748-3.752A9.753 9.753 0 003 11.25C3 16.635 7.365 21 12.75 21a9.753 9.753 0 009.002-5.998z" />
            </svg>
          </button>
          <!-- Help link -->
          <a
            :href="getHelpUrl(route.name)"
            target="_blank"
            rel="noopener noreferrer"
            class="p-2 rounded-lg text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title="Help"
          >
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9.879 7.519c1.171-1.025 3.071-1.025 4.242 0 1.172 1.025 1.172 2.687 0 3.712-.203.179-.43.326-.67.442-.745.361-1.45.999-1.45 1.827v.75M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9 5.25h.008v.008H12v-.008z" />
            </svg>
          </a>
          <!-- Settings gear -->
          <button
            @click="showSettings = true"
            class="p-2 rounded-lg text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title="Settings"
          >
            <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 011.37.49l1.296 2.247a1.125 1.125 0 01-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 010 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 01-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 01-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 01-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 01-1.369-.49l-1.297-2.247a1.125 1.125 0 01.26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 010-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 01-.26-1.43l1.297-2.247a1.125 1.125 0 011.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28z" />
              <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </button>
        </div>
      </header>

      <!-- Page content -->
      <main class="flex-1 overflow-y-auto p-6">
        <router-view :key="$route.fullPath" />
      </main>
    </div>

    <!-- Settings modal -->
    <SettingsModal v-if="showSettings" @close="showSettings = false" />

    <!-- Keyboard shortcuts help -->
    <KeyboardShortcutsHelp v-if="showShortcutsHelp" @close="showShortcutsHelp = false" />

    <!-- Release notes modal -->
    <ReleaseNotesModal v-if="showReleaseNotes" @close="showReleaseNotes = false" />

    <!-- Toast notifications -->
    <div class="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm">
      <transition-group name="toast">
        <div
          v-for="t in store.toasts"
          :key="t.id"
          class="flex items-center gap-2 px-4 py-3 rounded-lg shadow-lg text-sm font-medium border cursor-pointer"
          :class="{
            'bg-red-50 dark:bg-red-900/80 text-red-800 dark:text-red-200 border-red-200 dark:border-red-700': t.type === 'error',
            'bg-green-50 dark:bg-green-900/80 text-green-800 dark:text-green-200 border-green-200 dark:border-green-700': t.type === 'success',
            'bg-amber-50 dark:bg-amber-900/80 text-amber-800 dark:text-amber-200 border-amber-200 dark:border-amber-700': t.type === 'warning',
          }"
          title="Click to copy"
          @click="copyToast(t.message)"
        >
          <svg v-if="t.type === 'error'" class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" /></svg>
          <svg v-else-if="t.type === 'success'" class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          <span class="truncate">{{ t.message }}</span>
          <button @click.stop="store.toasts = store.toasts.filter(x => x.id !== t.id)" class="ml-auto shrink-0 opacity-60 hover:opacity-100">&times;</button>
        </div>
      </transition-group>
    </div>
  </div>

</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import { useMonitorStore } from './stores/monitor.js'
import { useTheme } from './composables/useTheme.js'
import { useDemoMode } from './composables/useDemoMode.js'
import { useRouter, useRoute } from 'vue-router'
import SidebarMenu from './components/SidebarMenu.vue'
import SettingsModal from './components/SettingsModal.vue'
import KeyboardShortcutsHelp from './components/KeyboardShortcutsHelp.vue'
import ReleaseNotesModal from './components/ReleaseNotesModal.vue'
import appPackage from '../package.json'
import { getHelpUrl } from './config/helpUrls.js'
import { useKeyboardShortcuts } from './composables/useKeyboardShortcuts.js'

const store = useMonitorStore()
const theme = useTheme()
const { isDemoMode } = useDemoMode()
const router = useRouter()
const route = useRoute()
const showSettings = ref(false)
const showShortcutsHelp = ref(false)
const showReleaseNotes = ref(false)
const loginKey = ref('')

useKeyboardShortcuts({ showSettings, showShortcutsHelp })
const loginError = ref(false)
const unlocking = ref(false)

const isSetupRoute = computed(() => route.path === '/setup')

async function unlock() {
  unlocking.value = true
  loginError.value = false
  const key = loginKey.value.trim()
  try {
    if (!key) {
      loginError.value = true
      return
    }

    if (!store.apiKeyConfigured) {
      await store.saveApiKey(key)
      loginKey.value = ''
      return
    }

    const valid = await store.verifyApiKey(key)
    if (!valid) {
      loginError.value = true
      return
    }

    loginKey.value = ''
  } catch {
    loginError.value = true
  } finally {
    unlocking.value = false
  }
}

function copyToast(message) {
  navigator.clipboard.writeText(message).catch(() => {})
}

async function loadData() {
  await Promise.all([
    store.fetchPatStatus(),
    store.fetchDbCredentialsStatus(),
    store.fetchEmailFromStatus(),
    store.fetchProjects(),
    store.fetchAuditDenylist().catch(() => {}),
    store.fetchCheckTypes(),
    store.fetchCachedResults(),
  ])
  if (store.patConfigured && store.projects.length > 0) {
    const staleMinutes = 5
    let shouldRun = !store.results || !store.results.ran_at
    if (!shouldRun) {
      const age = (Date.now() - new Date(store.results.ran_at).getTime()) / 60000
      shouldRun = age > staleMinutes
    }
    if (shouldRun) store.runChecks()
  }
}

// Load data once authenticated
watch(() => store.authenticated, (authed) => {
  if (authed) {
    loadData()
    const lastSeen = localStorage.getItem('devops-incontrol-last-seen-version')
    if (lastSeen !== appPackage.version) {
      showReleaseNotes.value = true
      localStorage.setItem('devops-incontrol-last-seen-version', appPackage.version)
    }
  }
})

onMounted(async () => {
  await store.fetchApiKeyStatus()

  // Redirect to setup wizard if first run
  if (!store.apiKeyConfigured) {
    const status = await store.fetchSetupStatus()
    if (status && !status.setup_complete) {
      router.replace('/setup')
      return
    }
  }

  if (store.authenticated) {
    loadData()
    const lastSeen = localStorage.getItem('devops-incontrol-last-seen-version')
    if (lastSeen !== appPackage.version) {
      showReleaseNotes.value = true
      localStorage.setItem('devops-incontrol-last-seen-version', appPackage.version)
    }
  }
})
</script>

<style scoped>
.toast-enter-active { transition: all 0.3s ease-out; }
.toast-leave-active { transition: all 0.2s ease-in; }
.toast-enter-from { opacity: 0; transform: translateY(10px); }
.toast-leave-to { opacity: 0; transform: translateX(20px); }
</style>
