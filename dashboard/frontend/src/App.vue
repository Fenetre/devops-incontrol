<template>
  <UApp>
  <!-- Setup wizard (no auth gate, no sidebar) -->
  <router-view v-if="isSetupRoute" />

  <!-- Password login gate -->
  <div v-else-if="!store.authenticated" class="flex items-center justify-center h-screen bg-gray-100 dark:bg-gray-900">
    <div class="bg-white dark:bg-gray-800 dark:border dark:border-gray-700 rounded-xl shadow-lg p-8 w-full max-w-sm mx-4">
      <div class="flex items-center gap-2 mb-6">
        <UIcon name="i-heroicons-lock-closed" class="w-7 h-7 text-primary-600" />
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
        <UInput
          name="login-key" v-model="loginKey"
          type="password"
          placeholder="Password…"
          autofocus
          class="w-full"
        />
        <p v-if="loginError" class="mt-2 text-sm text-red-600 dark:text-red-400">
          {{ store.apiKeyConfigured ? 'Invalid password' : 'Password is required' }}
        </p>
        <UButton
          type="submit"
          :disabled="unlocking"
          :loading="unlocking"
          block
          class="mt-4"
        >
          {{ store.apiKeyConfigured ? 'Unlock' : 'Save & Unlock' }}
        </UButton>
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
          <UButton
            variant="ghost"
            color="neutral"
            :icon="theme.dark.value ? 'i-heroicons-sun' : 'i-heroicons-moon'"
            :title="theme.dark.value ? 'Switch to light mode' : 'Switch to dark mode'"
            @click="theme.toggle()"
          />
          <!-- Help link -->
          <a
            :href="getHelpUrl(route.name)"
            target="_blank"
            rel="noopener noreferrer"
            class="p-2 rounded-lg text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title="Help"
          >
            <UIcon name="i-heroicons-question-mark-circle" class="w-5 h-5" />
          </a>
          <!-- Settings gear -->
          <UButton
            variant="ghost"
            color="neutral"
            icon="i-heroicons-cog-6-tooth"
            title="Settings"
            @click="showSettings = true"
          />
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
    <ReleaseNotesModal v-if="showReleaseNotes" @close="showReleaseNotes = false" @loaded="markReleaseNotesSeen" />

    <!-- Toast notifications (provided by UApp) -->
  </div>
  </UApp>

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
const toast = useToast()
const theme = useTheme()
const { isDemoMode } = useDemoMode()

const toastColorMap = { error: 'error', success: 'success', warning: 'warning' }
const toastIconMap = { error: 'i-heroicons-exclamation-circle', success: 'i-heroicons-check-circle', warning: 'i-heroicons-exclamation-triangle' }
store._toastFn = (message, type) => {
  toast.add({
    title: message,
    color: toastColorMap[type] || 'neutral',
    icon: toastIconMap[type],
    duration: type === 'error' ? 8000 : 3000,
  })
}
const router = useRouter()
const route = useRoute()
const showSettings = ref(false)
const showShortcutsHelp = ref(false)
const showReleaseNotes = ref(false)
const loginKey = ref('')

useKeyboardShortcuts({ showSettings, showShortcutsHelp })

function markReleaseNotesSeen() {
  localStorage.setItem('devops-incontrol-last-seen-version', appPackage.version)
}
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
    }
  }
})
</script>

