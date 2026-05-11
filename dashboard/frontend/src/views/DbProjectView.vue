<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
      <router-link to="/db-monitor" class="hover:text-primary-600 transition-colors">DB Monitor</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ project?.name || 'DB Project' }}</span>
    </nav>

    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-gray-900 dark:text-gray-100">{{ project?.name || 'DB Project' }}</h2>
        <p v-if="project?.name_filter" class="text-sm text-gray-600 dark:text-gray-400 mt-1">
          Filter: <span class="font-medium text-indigo-600">{{ project.name_filter }}</span>
        </p>
      </div>
      <div class="flex items-center gap-2">
        <button
          v-if="closedTicketCount"
          @click="copyAllClosed"
          class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium text-red-700 bg-red-50 hover:bg-red-100 border border-red-200 transition-colors shadow-sm"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15.666 3.888A2.25 2.25 0 0013.5 2.25h-3c-1.03 0-1.9.693-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 01-.75.75H9.75a.75.75 0 01-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 01-2.25 2.25H6.75A2.25 2.25 0 014.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 011.927-.184" />
          </svg>
          {{ copyAllLabel }}
        </button>
        <button
          @click="refresh"
          :disabled="loading || checkingRules"
          class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 transition-colors shadow-sm"
        >
          <svg class="w-4 h-4" :class="{ 'animate-spin': loading || checkingRules }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182" />
          </svg>
          {{ loading ? 'Loading…' : checkingRules ? 'Checking rules…' : 'Refresh' }}
        </button>
      </div>
    </div>

    <!-- Legend -->
    <div v-if="ruleResults.length" class="flex flex-wrap items-center gap-5 mb-4 text-xs text-gray-600 dark:text-gray-400">
      <span class="flex items-center gap-1.5">
        <svg class="w-4 h-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        OK
      </span>
      <span class="flex items-center gap-1.5">
        <svg class="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" /></svg>
        Allowlisted
      </span>
      <span class="flex items-center gap-1.5">
        <svg class="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9.879 7.519c1.171-1.025 3.071-1.025 4.242 0 1.172 1.025 1.172 2.687 0 3.712-.203.179-.43.326-.67.442-.745.361-1.45.999-1.45 1.827v.75M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9 5.25h.008v.008H12v-.008z" /></svg>
        No ticket number
      </span>
      <span class="flex items-center gap-1.5">
        <svg class="w-4 h-4 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" /></svg>
        Ticket closed/done
      </span>
    </div>

    <EmptyState v-if="!store.dbCredentialsConfigured" icon="warning" variant="warning" message="Database credentials not configured." hint="Set them in Settings (gear icon top-right)." />

    <div v-else-if="(loading || checkingRules) && !displayItems.length" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden animate-pulse">
      <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700">
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-32"></div>
      </div>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 p-5">
        <div v-for="n in 6" :key="n" class="flex items-center gap-2 px-3 py-2 rounded-lg bg-gray-50 dark:bg-gray-700">
          <div class="w-2 h-2 rounded-full bg-gray-300 dark:bg-gray-500 shrink-0"></div>
          <div class="h-4 bg-gray-200 dark:bg-gray-600 rounded flex-1"></div>
        </div>
      </div>
    </div>

    <div v-else-if="error" class="bg-white dark:bg-gray-800 rounded-xl border border-red-200 dark:border-red-700 shadow-sm px-6 py-8 text-center">
      <p class="text-red-500 text-sm">{{ error }}</p>
    </div>

    <EmptyState v-else-if="displayItems.length === 0 && !loading" icon="search" message="No databases found matching the filter." />

    <div v-else class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
      <div v-if="rulesWarning" class="mx-5 mt-5 mb-1 bg-amber-50 dark:bg-amber-900/20 rounded-lg border border-amber-200 dark:border-amber-700 px-4 py-2">
        <p class="text-amber-800 dark:text-amber-300 text-sm">{{ rulesWarning }}</p>
      </div>
      <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center gap-3">
        <span class="text-sm font-medium text-gray-700 dark:text-gray-300 shrink-0">{{ filteredItems.length }} of {{ displayItems.length }} database{{ displayItems.length !== 1 ? 's' : '' }}</span>
        <div class="relative w-64 ml-auto">
          <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
          </svg>
          <input
            v-autofocus
            v-model="search"
            type="text"
            placeholder="Search databases…"
            class="w-full pl-8 pr-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-200 placeholder-gray-400 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none transition-shadow"
          />
        </div>
        <div v-if="ruleResults.length" class="flex items-center gap-3 text-xs shrink-0">
          <span v-if="noTicketCount" class="text-gray-600 font-medium">{{ noTicketCount }} no ticket</span>
          <span v-if="closedTicketCount" class="text-red-600 font-medium">{{ closedTicketCount }} closed</span>
        </div>
      </div>
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 p-5">
        <div
          v-for="item in filteredItems"
          :key="item.name"
          class="group flex items-center gap-2 px-3 py-2 rounded-lg transition-colors"
          :class="itemBgClass(item)"
        >
          <!-- Exclamation icon for closed tickets -->
          <svg v-if="item.status === 'closed_ticket'" class="w-4 h-4 text-red-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
          </svg>
          <!-- Checkmark for OK -->
          <svg v-else-if="item.status === 'ok'" class="w-4 h-4 text-green-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <!-- Shield for allowlisted -->
          <svg v-else-if="item.status === 'allowlisted'" class="w-4 h-4 text-gray-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
          </svg>
          <!-- Question mark for no ticket -->
          <svg v-else-if="item.status === 'no_ticket'" class="w-4 h-4 text-gray-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9.879 7.519c1.171-1.025 3.071-1.025 4.242 0 1.172 1.025 1.172 2.687 0 3.712-.203.179-.43.326-.67.442-.745.361-1.45.999-1.45 1.827v.75M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9 5.25h.008v.008H12v-.008z" />
          </svg>
          <!-- Fallback dot -->
          <span v-else class="w-2 h-2 rounded-full shrink-0" :class="itemDotClass(item)"></span>
          <span class="text-sm truncate" :class="itemTextClass(item)">{{ item.name }}</span>
          <span v-if="item.ticket_id" class="text-xs opacity-60">#{{ item.ticket_id }}</span>
          <button
            @click.stop="copyName(item.name)"
            class="ml-auto p-1.5 rounded-md transition-all shrink-0 opacity-0 group-hover:opacity-100 focus:opacity-100"
            :class="copiedName === item.name ? 'text-green-500' : 'text-gray-500 hover:text-gray-700 hover:bg-gray-100'"
            title="Copy database name"
          >
            <svg v-if="copiedName === item.name" class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
            </svg>
            <svg v-else class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15.666 3.888A2.25 2.25 0 0013.5 2.25h-3c-1.03 0-1.9.693-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 01-.75.75H9.75a.75.75 0 01-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 01-2.25 2.25H6.75A2.25 2.25 0 014.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 011.927-.184" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { transformDbDatabases, transformDbRulesResponse } from '../composables/demoTransform.js'
import EmptyState from '../components/EmptyState.vue'

const props = defineProps({ projectId: String })
const store = useMonitorStore()

const loading = ref(false)
const checkingRules = ref(false)
const error = ref(null)
const rulesWarning = ref(null)
const databases = ref([])
const ruleResults = ref([])
const copyAllLabel = ref('Copy All Closed')
const copiedName = ref(null)
const search = ref('')

const filteredItems = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return displayItems.value
  return displayItems.value.filter(i => i.name.toLowerCase().includes(q))
})

const closedItems = computed(() => displayItems.value.filter(i => i.status === 'closed_ticket'))

let copyTimer = null
async function copyName(name) {
  await navigator.clipboard.writeText(name)
  copiedName.value = name
  clearTimeout(copyTimer)
  copyTimer = setTimeout(() => { copiedName.value = null }, 1200)
}

async function copyAllClosed() {
  const text = closedItems.value.map(i => i.name).join('\n')
  await navigator.clipboard.writeText(text)
  copyAllLabel.value = 'Copied!'
  setTimeout(() => { copyAllLabel.value = 'Copy All Closed' }, 1500)
}

const project = computed(() => store.displayDbProjects.find(p => p.id === props.projectId))

watch(() => props.projectId, () => {
  ruleResults.value = []
  rulesWarning.value = null
  if (store.dbCredentialsConfigured) {
    refresh()
  }
})

const displayItems = computed(() => {
  if (ruleResults.value.length) return ruleResults.value
  return databases.value.map(name => ({ name, ticket_id: null, status: 'ok' }))
})

const noTicketCount = computed(() => ruleResults.value.filter(r => r.status === 'no_ticket').length)
const closedTicketCount = computed(() => ruleResults.value.filter(r => r.status === 'closed_ticket').length)

function itemBgClass(item) {
  if (item.status === 'closed_ticket') return 'bg-red-50 hover:bg-red-100 dark:bg-red-900/30 dark:hover:bg-red-900/50'
  if (item.status === 'no_ticket') return 'bg-gray-100 hover:bg-gray-200 dark:bg-gray-700 dark:hover:bg-gray-600'
  return 'bg-green-50 hover:bg-green-100 dark:bg-green-900/30 dark:hover:bg-green-900/50'
}

function itemDotClass(item) {
  if (item.status === 'no_ticket') return 'bg-gray-400'
  if (item.status === 'allowlisted') return 'bg-white border border-gray-300 dark:bg-gray-300 dark:border-gray-500'
  return 'bg-green-400'
}

function itemTextClass(item) {
  if (item.status === 'closed_ticket') return 'text-red-700 dark:text-red-400 font-medium'
  if (item.status === 'no_ticket') return 'text-gray-700 dark:text-gray-300'
  return 'text-green-700 dark:text-green-400'
}

async function refresh() {
  loading.value = true
  error.value = null
  rulesWarning.value = null
  ruleResults.value = []
  try {
    const result = await store.fetchDbProjectDatabases(props.projectId)
    databases.value = transformDbDatabases(result.databases || [])
    store.dbProjectDatabases[props.projectId] = result
  } catch (e) {
    error.value = e.message || 'Failed to load databases'
  } finally {
    loading.value = false
  }
  if (!error.value) {
    await runRules()
  }
}

async function runRules() {
  checkingRules.value = true
  rulesWarning.value = null
  try {
    const result = await store.checkDbProjectRules(props.projectId)
    const transformed = transformDbRulesResponse(result)
    ruleResults.value = transformed.results || []
    databases.value = ruleResults.value.map(r => r.name)
  } catch (e) {
    rulesWarning.value = e.message || 'Failed to check rules'
    if (!databases.value.length) {
      error.value = rulesWarning.value
    }
  } finally {
    checkingRules.value = false
  }
}

onMounted(() => {
  Promise.all([
    store.fetchDbProjects(),
    store.fetchDbCredentialsStatus(),
  ]).then(() => {
    if (store.dbCredentialsConfigured) {
      refresh()
    }
  })
})
</script>
