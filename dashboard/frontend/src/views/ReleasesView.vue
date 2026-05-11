<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ projectLabel }}</span>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">Releases</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Releases</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">{{ projectLabel }} — Release deployments.</p>
      </div>
    </div>

    <!-- Error -->
    <div v-if="tabError" class="mb-6 bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-red-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-red-800 dark:text-red-200">{{ tabError }}</p>
    </div>

    <!-- Toolbar -->
    <div class="flex flex-wrap items-center gap-3 mb-4">
      <button @click="load(true)" :disabled="store.loadingReleaseRuns"
        class="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-white shadow-sm transition-colors"
        :class="store.loadingReleaseRuns ? 'bg-primary-400 cursor-not-allowed' : 'bg-primary-600 hover:bg-primary-700'">
        <svg v-if="store.loadingReleaseRuns" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182M2.985 19.644l3.181-3.182" /></svg>
        {{ store.loadingReleaseRuns ? 'Loading…' : 'Refresh' }}
      </button>
      <input v-autofocus v-model="search" type="text" placeholder="Search…"
        class="text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 dark:text-gray-200 px-3 py-1.5 focus:ring-2 focus:ring-primary-500 outline-none w-48" />
      <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
        <span>From:</span>
        <input type="date" v-model="minDate" @change="load()"
          class="border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 dark:text-gray-200 px-2 py-1 text-xs focus:ring-2 focus:ring-primary-500 outline-none" />
        <span>To:</span>
        <input type="date" v-model="maxDate" @change="load()"
          class="border border-gray-300 dark:border-gray-600 rounded bg-white dark:bg-gray-700 dark:text-gray-200 px-2 py-1 text-xs focus:ring-2 focus:ring-primary-500 outline-none" />
      </div>
      <span v-if="data?.fetched_at" class="text-xs text-gray-400 dark:text-gray-500 ml-auto">
        Fetched: {{ new Date(data.fetched_at).toLocaleString() }}
      </span>
    </div>

    <!-- Definition tabs -->
    <div v-if="definitionNames.length" class="flex flex-wrap gap-1 mb-4 border-b border-gray-200 dark:border-gray-700">
      <button
        @click="activeTab = 'all'"
        class="px-3 py-1.5 text-sm font-medium rounded-t-md transition-colors"
        :class="activeTab === 'all'
          ? 'bg-primary-100 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 border border-b-0 border-gray-200 dark:border-gray-700'
          : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-800'"
      >All</button>
      <button
        v-for="name in definitionNames"
        :key="name"
        @click="activeTab = name"
        class="px-3 py-1.5 text-sm font-medium rounded-t-md transition-colors"
        :class="activeTab === name
          ? 'bg-primary-100 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 border border-b-0 border-gray-200 dark:border-gray-700'
          : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-800'"
      >{{ name }}</button>
    </div>

    <!-- Loading -->
    <div v-if="store.loadingReleaseRuns && !data" class="flex flex-col items-center py-20">
      <svg class="animate-spin w-8 h-8 text-primary-500 mb-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
      <p class="text-sm text-gray-500 dark:text-gray-400">Fetching releases…</p>
    </div>

    <!-- Table -->
    <div v-else-if="data && filtered.length" class="overflow-x-auto">
      <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
        <thead>
          <tr class="bg-gray-100 dark:bg-gray-800/60">
            <th class="text-left px-3 py-2 font-medium text-gray-600 dark:text-gray-300">Release</th>
            <th class="text-left px-3 py-2 font-medium text-gray-600 dark:text-gray-300">Definition</th>
            <th class="text-left px-3 py-2 font-medium text-gray-600 dark:text-gray-300">Status</th>
            <th class="text-left px-3 py-2 font-medium text-gray-600 dark:text-gray-300">Environments</th>
            <th class="text-left px-3 py-2 font-medium text-gray-600 dark:text-gray-300">Created by</th>
            <th class="text-left px-3 py-2 font-medium text-gray-600 dark:text-gray-300">Date/Time</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(run, i) in filtered" :key="i" class="border-t border-gray-100 dark:border-gray-700/50 hover:bg-gray-50 dark:hover:bg-gray-800/40">
            <td class="px-3 py-2 text-gray-700 dark:text-gray-200">{{ run.release_name }}</td>
            <td class="px-3 py-2 text-gray-600 dark:text-gray-300">{{ run.definition_name }}</td>
            <td class="px-3 py-2">
              <span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium" :class="resultClass(run.status)">
                {{ run.status }}
              </span>
            </td>
            <td class="px-3 py-2">
              <div class="flex gap-1">
                <span v-for="env in run.environments" :key="env.name"
                  class="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium" :class="resultClass(env.status)">
                  {{ env.name }}: {{ env.status }}
                </span>
              </div>
            </td>
            <td class="px-3 py-2 text-gray-600 dark:text-gray-300">{{ run.created_by }}</td>
            <td class="px-3 py-2 text-gray-500 dark:text-gray-400 whitespace-nowrap">{{ formatDateTime(run.created_on) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-else-if="data && !filtered.length" class="text-center py-12 text-sm text-gray-400">No releases match the filter.</div>
    <div v-else-if="!store.loadingReleaseRuns" class="text-center py-20 text-sm text-gray-400 dark:text-gray-500">No release data available. Click Refresh to fetch.</div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { transformReleaseRuns } from '../composables/demoTransform.js'

const props = defineProps({ projectId: { type: String, required: true } })

const store = useMonitorStore()
const tabError = ref('')
const search = ref('')
const minDate = ref('')
const maxDate = ref('')
const activeTab = ref('all')

const cacheKey = computed(() => `${props.projectId}:${minDate.value}:${maxDate.value}`)
const data = computed(() => {
  const raw = store.releaseRunData[cacheKey.value]
  return raw ? transformReleaseRuns(raw) : null
})

const projectLabel = computed(() => {
  const proj = store.displayProjects.find(p => p.id === props.projectId)
  return proj ? proj.project : props.projectId
})

const definitionNames = computed(() => {
  if (!data.value?.runs) return []
  return [...new Set(data.value.runs.map(r => r.definition_name).filter(Boolean))].sort()
})

const filtered = computed(() => {
  if (!data.value?.runs) return []
  let runs = data.value.runs
  if (activeTab.value !== 'all') {
    runs = runs.filter(r => r.definition_name === activeTab.value)
  }
  if (!search.value.trim()) return runs
  const q = search.value.trim().toLowerCase()
  return runs.filter(r =>
    r.release_name.toLowerCase().includes(q) ||
    r.definition_name?.toLowerCase().includes(q) ||
    r.status?.toLowerCase().includes(q) ||
    r.created_by?.toLowerCase().includes(q)
  )
})

function formatDateTime(iso) {
  if (!iso) return ''
  return new Date(iso).toLocaleString()
}

function resultClass(status) {
  const s = (status || '').toLowerCase()
  if (s === 'succeeded' || s === 'active' || s === 'completed') return 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300'
  if (s === 'failed' || s === 'rejected') return 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300'
  if (s === 'partiallysucceeded') return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
  if (s === 'canceled' || s === 'cancelled' || s === 'abandoned') return 'bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400'
  if (s === 'inprogress' || s === 'notstarted' || s === 'queued' || s === 'notdeployed') return 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300'
  return 'bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400'
}

async function load(force = false) {
  tabError.value = ''
  try { await store.fetchReleaseRuns(props.projectId, minDate.value, maxDate.value, force) }
  catch (e) { tabError.value = e?.message || 'Failed to fetch releases' }
}

onMounted(() => load())
watch(() => props.projectId, () => load())
</script>
