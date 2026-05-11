<template>
  <div>
    <!-- Header row -->
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
      <div>
        <h2 class="text-2xl font-bold text-gray-900 dark:text-gray-100">Dashboard</h2>
        <p class="text-sm mt-1" :class="isStale ? 'text-amber-600 dark:text-amber-400' : 'text-gray-500 dark:text-gray-400'">
          <template v-if="store.results?.ran_at">
            <span v-if="isStale" class="inline-flex items-center gap-1">
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" /></svg>
              Stale —
            </span>
            Last run: <span :title="new Date(store.results.ran_at).toLocaleString()">{{ formatTime(store.results.ran_at) }}</span>
            · {{ store.results.total_issues }} issue{{ store.results.total_issues !== 1 ? 's' : '' }} found
          </template>
          <template v-else>No checks have been run yet.</template>
        </p>
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <!-- Hide all-clear toggle -->
        <button
          v-if="store.displayResults?.projects?.length"
          @click="hideAllClear = !hideAllClear"
          class="inline-flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium transition-colors border"
          :class="hideAllClear
            ? 'bg-primary-50 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 border-primary-200 dark:border-primary-700'
            : 'bg-white dark:bg-gray-700 text-gray-600 dark:text-gray-300 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-600'"
        >
          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path v-if="hideAllClear" stroke-linecap="round" stroke-linejoin="round" d="M3.98 8.223A10.477 10.477 0 001.934 12C3.226 16.338 7.244 19.5 12 19.5c.993 0 1.953-.138 2.863-.395M6.228 6.228A10.45 10.45 0 0112 4.5c4.756 0 8.773 3.162 10.065 7.498a10.523 10.523 0 01-4.293 5.774M6.228 6.228L3 3m3.228 3.228l3.65 3.65m7.894 7.894L21 21m-3.228-3.228l-3.65-3.65m0 0a3 3 0 10-4.243-4.243m4.242 4.242L9.88 9.88" />
            <path v-else stroke-linecap="round" stroke-linejoin="round" d="M2.036 12.322a1.012 1.012 0 010-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178zM15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          {{ hideAllClear ? 'Show all projects' : 'Hide projects without issues' }}
        </button>

        <!-- Auto-refresh selector -->
        <div class="flex items-center gap-1.5">
          <svg class="w-4 h-4 text-gray-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182" />
          </svg>
          <SelectMenu
            v-model="refreshInterval"
            :options="refreshIntervalOptions"
            placeholder="Auto-refresh: Off"
            size="sm"
            class="w-44"
          />
        </div>

        <button
          @click="run"
          :disabled="store.runningChecks || !store.patConfigured || store.projects.length === 0"
          class="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors shadow-sm"
        >
          <svg v-if="store.runningChecks" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
          </svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-.856.917-1.398 1.667-.986l11.54 6.347a1.125 1.125 0 010 1.972l-11.54 6.347a1.125 1.125 0 01-1.667-.986V5.653z" />
          </svg>
          {{ store.runningChecks ? 'Running…' : 'Run Checks' }}
        </button>
      </div>
    </div>

    <!-- PAT warning -->
    <div v-if="!store.patConfigured" class="mb-6 bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-amber-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-amber-800 dark:text-amber-200">
        No PAT configured. Click the
        <svg class="inline w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 011.37.49l1.296 2.247a1.125 1.125 0 01-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 010 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 01-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 01-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 01-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 01-1.369-.49l-1.297-2.247a1.125 1.125 0 01.26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 010-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 01-.26-1.43l1.297-2.247a1.125 1.125 0 011.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28z" /><path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /></svg>
        settings icon in the top-right corner to add your Azure DevOps PAT.
      </p>
    </div>

    <!-- No projects configured -->
    <div v-if="store.projects.length === 0" class="mb-6 bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-blue-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M11.25 11.25l.041-.02a.75.75 0 011.063.852l-.708 2.836a.75.75 0 001.063.853l.041-.021M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9-3.75h.008v.008H12V8.25z" />
      </svg>
      <p class="text-sm text-blue-800 dark:text-blue-200">
        No projects configured yet.
        <router-link to="/config" class="underline font-medium hover:text-blue-900">Add a project</router-link>
        to get started.
      </p>
    </div>

    <!-- Error -->
    <div v-if="store.error" class="mb-6 bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-red-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-red-800 dark:text-red-200">{{ store.error }}</p>
    </div>

    <!-- Loading skeleton (only when no results exist at all) -->
    <div v-if="store.runningChecks && !store.displayResults?.projects?.length" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <div v-for="n in store.projects.length || 3" :key="n" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-5 animate-pulse">
        <div class="h-5 bg-gray-200 dark:bg-gray-700 rounded w-3/4 mb-4"></div>
        <div class="flex gap-2">
          <div class="h-7 bg-gray-100 dark:bg-gray-600 rounded-full w-28"></div>
          <div class="h-7 bg-gray-100 dark:bg-gray-600 rounded-full w-24"></div>
        </div>
      </div>
    </div>

    <!-- Project cards -->
    <div v-else-if="store.displayResults || store.runningChecks" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <!-- Projects with results -->
      <ProjectCard
        v-for="pr in visibleProjects"
        :key="pr.project_id"
        :project="pr"
        :project-id="pr.project_id"
        :checks="pr.checks"
        :pr-flag-counts="prFlagCountsByProject[pr.project_id] || null"
        :info-checks="['tag_overview_check']"
        :running="!!store.runningProjects[pr.project_id]"
        @run-project="runProject(pr.project_id)"
      />

      <!-- Configured projects with no results yet (not run) -->
      <div
        v-for="p in hideAllClear ? [] : projectsNotInResults"
        :key="p.id"
        class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden"
      >
        <div class="px-5 py-4 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <h3 class="font-semibold text-gray-900 dark:text-gray-100 truncate">{{ p.project }}</h3>
          <button
            @click="runProject(p.id)"
            :disabled="!!store.runningProjects[p.id]"
            class="inline-flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium text-gray-600 hover:text-primary-700 hover:bg-primary-50 disabled:opacity-50 transition-colors"
            title="Run checks for this project"
          >
            <svg v-if="store.runningProjects[p.id]" class="animate-spin w-3.5 h-3.5" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
            </svg>
            <svg v-else class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-.856.917-1.398 1.667-.986l11.54 6.347a1.125 1.125 0 010 1.972l-11.54 6.347a1.125 1.125 0 01-1.667-.986V5.653z" />
            </svg>
          </button>
        </div>
        <div class="px-5 py-4 text-gray-600 dark:text-gray-400 text-sm">
          <template v-if="store.runningProjects[p.id]">
            <div class="flex items-center gap-2">
              <svg class="animate-spin w-3.5 h-3.5" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
              </svg>
              Checking…
            </div>
          </template>
          <template v-else>Not yet checked</template>
        </div>
      </div>
    </div>

    <!-- ================================================================ -->
    <!-- DB Monitor Summary                                                -->
    <!-- ================================================================ -->
    <div v-if="store.dbProjects.length > 0" class="mt-10">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-bold text-gray-900 dark:text-gray-100">DB Monitor</h3>
          <p v-if="store.runningDbRules" class="text-sm text-gray-500 dark:text-gray-400">Checking…</p>
          <p v-else-if="totalDbTotal > 0" class="text-sm text-gray-500 dark:text-gray-400 flex items-center gap-3">
            <span>{{ totalDbTotal }} total</span>
            <span class="text-green-600 dark:text-green-400 font-medium">{{ totalDbOk }} OK</span>
            <span v-if="totalDbNok > 0" class="text-red-600 dark:text-red-400 font-medium">{{ totalDbNok }} NOK</span>
            <span v-else class="text-green-600 dark:text-green-400">All clear</span>
          </p>
          <p v-else class="text-sm text-gray-500 dark:text-gray-400">Not yet checked</p>
        </div>
        <button
          @click="runDbRules"
          :disabled="store.runningDbRules || !store.dbCredentialsConfigured"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium text-white bg-amber-600 hover:bg-amber-700 disabled:opacity-50 transition-colors shadow-sm"
        >
          <svg v-if="store.runningDbRules" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
          </svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
          </svg>
          {{ store.runningDbRules ? 'Running…' : 'Run DB Rules' }}
        </button>
      </div>

      <!-- DB project cards -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <router-link
          v-for="proj in dbSummaryProjects"
          :key="proj.id"
          :to="`/db-monitor/${proj.id}`"
          class="bg-white dark:bg-gray-800 rounded-xl border shadow-sm overflow-hidden hover:shadow-md transition-shadow"
          :class="proj.nok > 0 ? 'border-red-200 dark:border-red-800' : 'border-gray-200 dark:border-gray-700'"
        >
          <div class="px-5 py-4 border-b" :class="proj.nok > 0 ? 'border-red-100 bg-red-50 dark:bg-red-900/30 dark:border-red-800' : 'border-gray-200 dark:border-gray-700'">
            <div class="flex items-center justify-between">
              <h4 class="font-semibold text-gray-900 dark:text-gray-100 truncate">{{ proj.name }}</h4>
              <span v-if="proj.total > 0" class="text-xs bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 rounded-full px-2.5 py-0.5 font-medium">{{ proj.total }} db{{ proj.total !== 1 ? 's' : '' }}</span>
            </div>
            <p v-if="proj.name_filter" class="text-xs text-gray-600 mt-0.5">{{ proj.name_filter }}</p>
          </div>
          <div class="px-5 py-3 flex items-center gap-3">
            <template v-if="proj.total > 0">
              <span class="text-xs text-gray-600 font-medium">{{ proj.total }} total</span>
              <span class="text-xs text-green-600 dark:text-green-400 font-medium">{{ proj.ok }} OK</span>
              <span v-if="proj.nok > 0" class="inline-flex items-center gap-1 text-xs font-medium text-red-600 dark:text-red-400">
                <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
                </svg>
                {{ proj.nok }} NOK
              </span>
            </template>
            <span v-else class="text-xs text-gray-600">
              {{ store.runningDbRules ? 'Checking…' : 'Not yet checked' }}
            </span>
          </div>
        </router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref, watch, onMounted, onUnmounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import ProjectCard from '../components/ProjectCard.vue'
import SelectMenu from '../components/SelectMenu.vue'

const store = useMonitorStore()
const refreshInterval = ref(0)
const hideAllClear = ref(false)
const refreshIntervalOptions = [
  { value: 0, label: 'Auto-refresh: Off' },
  { value: 60, label: 'Every 1 min' },
  { value: 300, label: 'Every 5 min' },
  { value: 600, label: 'Every 10 min' },
  { value: 1800, label: 'Every 30 min' },
]
let timerId = null

watch(refreshInterval, (seconds) => {
  if (timerId) { clearInterval(timerId); timerId = null }
  if (seconds > 0) {
    timerId = setInterval(() => {
      if (document.visibilityState !== 'visible') return
      if (!store.runningChecks && store.patConfigured && store.projects.length > 0) {
        store.runChecks()
      }
    }, seconds * 1000)
  }
})

function onVisibilityChange() {
  // When tab becomes visible again and we have a refresh interval, trigger an immediate check
  if (document.visibilityState === 'visible' && refreshInterval.value > 0) {
    if (!store.runningChecks && store.patConfigured && store.projects.length > 0) {
      store.runChecks()
    }
  }
}

onMounted(() => {
  document.addEventListener('visibilitychange', onVisibilityChange)
})

onUnmounted(() => {
  if (timerId) clearInterval(timerId)
  document.removeEventListener('visibilitychange', onVisibilityChange)
})

const projectsNotInResults = computed(() => {
  if (!store.displayResults) return []
  const inResults = new Set(store.displayResults.projects.map(p => p.project_id))
  return store.displayProjects.filter(p => !inResults.has(p.id))
})

const _STALE_MS = 60 * 60 * 1000  // 1 hour
const isStale = computed(() => {
  if (!store.displayResults?.ran_at) return false
  return Date.now() - new Date(store.displayResults.ran_at).getTime() > _STALE_MS
})

const _INFO_ONLY = new Set(['tag_overview_check'])
const visibleProjects = computed(() => {
  if (!store.displayResults) return []
  if (!hideAllClear.value) return store.displayResults.projects
  return store.displayResults.projects.filter(p =>
    p.total_issues > 0 || p.checks.some(c => c.error) || !p.checks.length
  )
})

const prFlagCountsByProject = computed(() => {
  const map = {}
  for (const proj of store.displayPrProjects || []) {
    const counts = { approval_ready: 0, stale: 0, unreviewed: 0 }
    for (const pr of proj.prs || []) {
      const flagKeys = new Set((pr.flags || []).map(f => f.key))
      if (flagKeys.has('approval_ready')) counts.approval_ready++
      if (flagKeys.has('stale')) counts.stale++
      if (flagKeys.has('unreviewed')) counts.unreviewed++
    }
    map[proj.project_id] = counts
  }
  return map
})

function formatTime(iso) {
  try {
    const date = new Date(iso)
    const now = new Date()
    const diff = Math.floor((now - date) / 1000)
    if (diff < 60) return 'just now'
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`
    return `${Math.floor(diff / 86400)}d ago`
  } catch {
    return iso
  }
}

async function run() {
  try {
    await store.runChecks()
  } catch {
    // error already stored in store.error
  } finally {
    store.fetchPrProjects(true).catch(() => {})
  }
}

async function runProject(projectId) {
  try {
    await store.runChecksForProject(projectId)
  } catch {
    // error already stored in store.error
  } finally {
    store.fetchPrProjects(true).catch(() => {})
  }
}

async function runDbRules() {
  await store.runAllDbRules()
}

// DB summary helpers
const dbSummaryProjects = computed(() => {
  return store.displayDbProjects.map(proj => {
    const result = store.dbRuleResults[proj.id]
    const results = result?.results || []
    const closed = results.filter(r => r.status === 'closed_ticket').length
    const noTicket = results.filter(r => r.status === 'no_ticket').length
    const ok = results.filter(r => r.status === 'ok' || r.status === 'allowlisted').length
    const nok = closed + noTicket
    return { ...proj, results, closed, noTicket, ok, nok, total: results.length }
  })
})

const totalDbClosed = computed(() => dbSummaryProjects.value.reduce((s, p) => s + p.closed, 0))
const totalDbTotal = computed(() => dbSummaryProjects.value.reduce((s, p) => s + p.total, 0))
const totalDbOk = computed(() => dbSummaryProjects.value.reduce((s, p) => s + p.ok, 0))
const totalDbNok = computed(() => dbSummaryProjects.value.reduce((s, p) => s + p.nok, 0))

onMounted(() => {
  store.runAllDbRules()
  store.fetchPrProjects().catch(() => {})
})
</script>
