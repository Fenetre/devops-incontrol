<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ projectLabel }}</span>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
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
    <UAlert v-if="tabError" color="error" icon="i-heroicons-exclamation-circle" :description="tabError" class="mb-6" />

    <!-- Toolbar -->
    <div class="flex flex-wrap items-center gap-3 mb-4">
      <UInput v-autofocus name="search" v-model="search" type="text" placeholder="Search…" icon="i-heroicons-magnifying-glass" size="sm" class="w-48 app-search" />
      <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
        <span>From:</span>
        <UInput type="date" name="min-date" v-model="minDate" @change="load()" size="sm" />
        <span>To:</span>
        <UInput type="date" name="max-date" v-model="maxDate" @change="load()" size="sm" />
      </div>
      <UButton @click="load(true)" :disabled="store.loadingReleaseRuns" :loading="store.loadingReleaseRuns" icon="i-heroicons-arrow-path">
        {{ store.loadingReleaseRuns ? 'Loading…' : 'Refresh' }}
      </UButton>
      <span v-if="data?.fetched_at" class="text-xs text-gray-400 dark:text-gray-500 ml-auto">
        Fetched: {{ new Date(data.fetched_at).toLocaleString() }}
      </span>
    </div>

    <!-- Definition filter + table -->
    <div class="flex gap-4">
      <!-- Side menu -->
      <div v-if="definitionNames.length" class="shrink-0">
        <nav class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <button
            @click="activeTab = 'all'"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left transition-colors"
            :class="activeTab === 'all'
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-squares-2x2" class="w-4 h-4 shrink-0" />
            All definitions
          </button>
          <button
            v-for="name in definitionNames" :key="name"
            @click="activeTab = name"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="activeTab === name
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-rocket-launch" class="w-4 h-4 shrink-0" />
            <span class="whitespace-nowrap">{{ name }}</span>
          </button>
        </nav>
      </div>

      <!-- Content -->
      <div class="flex-1 min-w-0">
        <!-- Loading -->
        <div v-if="store.loadingReleaseRuns && !data" class="flex flex-col items-center py-8">
          <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 text-primary-500 mb-4 animate-spin" />
          <p class="text-sm text-gray-500 dark:text-gray-400">Fetching releases…</p>
        </div>

        <!-- Table -->
        <div v-else-if="data && filtered.length" class="overflow-x-auto">
          <UTable :data="filtered" :columns="releaseColumns" />
        </div>

        <div v-else-if="data && !filtered.length" class="text-center py-12 text-sm text-gray-400 dark:text-gray-300">No releases match the filter.</div>
        <div v-else-if="!store.loadingReleaseRuns" class="text-center py-20 text-sm text-gray-400 dark:text-gray-500">No release data available. Click Refresh to fetch.</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, h } from 'vue'
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

const releaseColumns = [
  { accessorKey: 'release_name', header: 'Release' },
  { accessorKey: 'definition_name', header: 'Definition' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h('span', {
      class: ['inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium', resultClass(row.original.status)]
    }, row.original.status)
  },
  {
    accessorKey: 'environments',
    header: 'Environments',
    cell: ({ row }) => h('div', { class: 'flex gap-1' },
      (row.original.environments || []).map(env =>
        h('span', {
          key: env.name,
          class: ['inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium', resultClass(env.status)]
        }, `${env.name}: ${env.status}`)
      )
    )
  },
  { accessorKey: 'created_by', header: 'Created by' },
  {
    accessorKey: 'created_on',
    header: 'Date/Time',
    cell: ({ row }) => formatDateTime(row.original.created_on)
  },
]

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
