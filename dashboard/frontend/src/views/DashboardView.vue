<template>
  <div>
    <!-- Header row -->
    <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Dashboard</h2>
        <p class="text-sm mt-1" :class="isStale ? 'text-amber-600 dark:text-amber-400' : 'text-gray-500 dark:text-gray-400'">
          <template v-if="store.results?.ran_at">
            <span v-if="isStale" class="inline-flex items-center gap-1">
              <UIcon name="i-heroicons-exclamation-triangle" class="w-3.5 h-3.5" />
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
        <UButton v-if="store.displayResults?.projects?.length" size="xs" :variant="hideAllClear ? 'soft' : 'outline'" :color="hideAllClear ? 'primary' : 'neutral'" :icon="hideAllClear ? 'i-heroicons-eye-slash' : 'i-heroicons-eye'" @click="hideAllClear = !hideAllClear">
          {{ hideAllClear ? 'Show all projects' : 'Hide projects without issues' }}
        </UButton>

        <!-- Auto-refresh selector -->
        <div class="flex items-center gap-1.5">
          <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 text-gray-500" />
          <USelectMenu v-model="refreshInterval" :items="refreshIntervalOptions" value-key="value" placeholder="Auto-refresh: Off" size="sm" class="w-44" />
        </div>

        <UButton :loading="store.runningChecks" :disabled="!store.patConfigured || store.projects.length === 0" icon="i-heroicons-play" @click="run">
          {{ store.runningChecks ? 'Running…' : 'Run Checks' }}
        </UButton>
      </div>
    </div>

    <!-- PAT warning -->
    <UAlert v-if="!store.patConfigured" color="warning" icon="i-heroicons-exclamation-triangle" class="mb-6">
      <template #description>
        No PAT configured. Click the
        <UIcon name="i-heroicons-cog-6-tooth" class="inline w-4 h-4" />
        settings icon in the top-right corner to add your Azure DevOps PAT.
      </template>
    </UAlert>

    <!-- No projects configured -->
    <UAlert v-if="store.projects.length === 0" color="info" icon="i-heroicons-information-circle" class="mb-6">
      <template #description>
        No projects configured yet.
        <router-link to="/config" class="underline font-medium hover:text-blue-900">Add a project</router-link>
        to get started.
      </template>
    </UAlert>

    <!-- Error -->
    <UAlert v-if="store.error" color="error" icon="i-heroicons-exclamation-circle" class="mb-6" :description="store.error" />

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
          <UButton size="xs" variant="ghost" color="neutral" :loading="!!store.runningProjects[p.id]" icon="i-heroicons-play" title="Run checks for this project" @click="runProject(p.id)" />
        </div>
        <div class="px-5 py-4 text-gray-600 dark:text-gray-400 text-sm">
          <template v-if="store.runningProjects[p.id]">
            <div class="flex items-center gap-2">
              <UIcon name="i-heroicons-arrow-path" class="animate-spin w-3.5 h-3.5" />
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
        <UButton :loading="store.runningDbRules" :disabled="!store.dbCredentialsConfigured" color="warning" icon="i-heroicons-shield-check" @click="runDbRules">
          {{ store.runningDbRules ? 'Running…' : 'Run DB Rules' }}
        </UButton>
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
                <UIcon name="i-heroicons-exclamation-circle" class="w-3 h-3" />
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
