<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ projectLabel }}</span>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ mode === 'pipelines' ? 'Pipelines' : 'Releases' }}</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Pipelines & Releases</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">{{ projectLabel }} — {{ mode === 'pipelines' ? 'Build pipeline runs' : 'Release deployments' }}.</p>
      </div>
    </div>

    <!-- Pipelines / Releases tabs -->
    <UTabs :items="tabItems" v-model="mode" :content="false" variant="link" class="mb-6" />

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
      <UButton @click="load(true)" :disabled="isLoading" :loading="isLoading" icon="i-heroicons-arrow-path">
        {{ isLoading ? 'Loading…' : 'Refresh' }}
      </UButton>
      <span v-if="currentData?.fetched_at" class="text-xs text-gray-400 dark:text-gray-400 ml-auto">
        Fetched: {{ new Date(currentData.fetched_at).toLocaleString() }}
      </span>
    </div>

    <!-- Side menus + table -->
    <div class="flex gap-4">
      <!-- Side menus -->
      <div v-if="store.displayProjects.length" class="shrink-0 space-y-4">
        <!-- Project selector -->
        <nav class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Projects</div>
          <button
            v-for="proj in store.displayProjects" :key="proj.id"
            @click="router.push({ name: 'pipelines', params: { projectId: proj.id } })"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="proj.id === projectId
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-folder" class="w-4 h-4 shrink-0" />
            <span class="whitespace-nowrap">{{ proj.project }}</span>
          </button>
        </nav>

        <!-- Pipeline filter -->
        <nav v-if="mode === 'pipelines' && pipelineNames.length" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <button
            @click="activeFilter = 'all'"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left transition-colors"
            :class="activeFilter === 'all'
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-squares-2x2" class="w-4 h-4 shrink-0" />
            All pipelines
          </button>
          <button
            v-for="name in pipelineNames" :key="name"
            @click="activeFilter = name"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="activeFilter === name
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-play-circle" class="w-4 h-4 shrink-0" />
            <span class="whitespace-nowrap">{{ name }}</span>
          </button>
        </nav>

        <!-- Release definition filter -->
        <nav v-if="mode === 'releases' && definitionNames.length" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <button
            @click="activeFilter = 'all'"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left transition-colors"
            :class="activeFilter === 'all'
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-squares-2x2" class="w-4 h-4 shrink-0" />
            All definitions
          </button>
          <button
            v-for="name in definitionNames" :key="name"
            @click="activeFilter = name"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="activeFilter === name
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
        <!-- Pipelines content -->
        <template v-if="mode === 'pipelines'">
          <div v-if="store.loadingPipelineRuns && !pipelineData" class="flex flex-col items-center py-8">
            <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 text-primary-500 mb-4 animate-spin" />
            <p class="text-sm text-gray-500 dark:text-gray-400">Fetching pipeline runs…</p>
          </div>
          <div v-else-if="pipelineData && filteredPipelines.length" class="overflow-x-auto">
            <UTable :data="filteredPipelines" :columns="pipelineColumns" />
          </div>
          <div v-else-if="pipelineData && !filteredPipelines.length" class="text-center py-12 text-sm text-gray-400 dark:text-gray-300">No pipeline runs match the filter.</div>
          <div v-else-if="!store.loadingPipelineRuns" class="text-center py-20 text-sm text-gray-400 dark:text-gray-400">No pipeline data available. Click Refresh to fetch.</div>
        </template>

        <!-- Releases content -->
        <template v-else>
          <div v-if="store.loadingReleaseRuns && !releaseData" class="flex flex-col items-center py-8">
            <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 text-primary-500 mb-4 animate-spin" />
            <p class="text-sm text-gray-500 dark:text-gray-400">Fetching releases…</p>
          </div>
          <div v-else-if="releaseData && filteredReleases.length" class="overflow-x-auto">
            <UTable :data="filteredReleases" :columns="releaseColumns" />
          </div>
          <div v-else-if="releaseData && !filteredReleases.length" class="text-center py-12 text-sm text-gray-400 dark:text-gray-300">No releases match the filter.</div>
          <div v-else-if="!store.loadingReleaseRuns" class="text-center py-20 text-sm text-gray-400 dark:text-gray-400">No release data available. Click Refresh to fetch.</div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, h } from 'vue'
import { useRouter } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { transformPipelineRuns, transformReleaseRuns } from '../composables/demoTransform.js'

const props = defineProps({ projectId: { type: String, required: true } })

const store = useMonitorStore()
const router = useRouter()
const tabError = ref('')
const search = ref('')
const minDate = ref('')
const maxDate = ref('')
const mode = ref('pipelines')
const tabItems = [{ label: 'Pipelines', value: 'pipelines', icon: 'i-heroicons-play-circle' }, { label: 'Releases', value: 'releases', icon: 'i-heroicons-rocket-launch' }]
const activeFilter = ref('all')

const isLoading = computed(() => mode.value === 'pipelines' ? store.loadingPipelineRuns : store.loadingReleaseRuns)

// --- Pipeline data ---
const pipelineCacheKey = computed(() => `${props.projectId}:${minDate.value}:${maxDate.value}`)
const pipelineData = computed(() => {
  const raw = store.pipelineRunData[pipelineCacheKey.value]
  return raw ? transformPipelineRuns(raw) : null
})

const pipelineNames = computed(() => {
  if (!pipelineData.value?.runs) return []
  return [...new Set(pipelineData.value.runs.map(r => r.pipeline_name))].sort()
})

const filteredPipelines = computed(() => {
  if (!pipelineData.value?.runs) return []
  let runs = pipelineData.value.runs
  if (activeFilter.value !== 'all') {
    runs = runs.filter(r => r.pipeline_name === activeFilter.value)
  }
  if (!search.value.trim()) return runs
  const q = search.value.trim().toLowerCase()
  return runs.filter(r =>
    r.pipeline_name.toLowerCase().includes(q) ||
    r.result?.toLowerCase().includes(q) ||
    r.status?.toLowerCase().includes(q) ||
    r.branch?.toLowerCase().includes(q) ||
    r.requested_by?.toLowerCase().includes(q)
  )
})

const pipelineColumns = [
  { accessorKey: 'pipeline_name', header: 'Pipeline' },
  {
    accessorKey: 'result',
    header: 'Result',
    cell: ({ row }) => h('span', {
      class: ['inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium', resultClass(row.original.result || row.original.status)]
    }, row.original.result || row.original.status)
  },
  { accessorKey: 'branch', header: 'Branch' },
  { accessorKey: 'requested_by', header: 'Triggered by' },
  {
    accessorKey: 'finished_at',
    header: 'Date/Time',
    cell: ({ row }) => formatDateTime(row.original.finished_at)
  },
]

// --- Release data ---
const releaseCacheKey = computed(() => `${props.projectId}:${minDate.value}:${maxDate.value}`)
const releaseData = computed(() => {
  const raw = store.releaseRunData[releaseCacheKey.value]
  return raw ? transformReleaseRuns(raw) : null
})

const definitionNames = computed(() => {
  if (!releaseData.value?.runs) return []
  return [...new Set(releaseData.value.runs.map(r => r.definition_name).filter(Boolean))].sort()
})

const filteredReleases = computed(() => {
  if (!releaseData.value?.runs) return []
  let runs = releaseData.value.runs
  if (activeFilter.value !== 'all') {
    runs = runs.filter(r => r.definition_name === activeFilter.value)
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

// --- Shared ---
const currentData = computed(() => mode.value === 'pipelines' ? pipelineData.value : releaseData.value)

const projectLabel = computed(() => {
  const proj = store.displayProjects.find(p => p.id === props.projectId)
  return proj ? proj.project : props.projectId
})

watch(mode, () => {
  activeFilter.value = 'all'
  search.value = ''
  load()
})

function formatDateTime(iso) {
  if (!iso) return ''
  return new Date(iso).toLocaleString()
}

function resultClass(status) {
  const s = (status || '').toLowerCase()
  if (s === 'succeeded' || s === 'completed' || s === 'active') return 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300'
  if (s === 'failed' || s === 'rejected') return 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300'
  if (s === 'partiallysucceeded') return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
  if (s === 'canceled' || s === 'cancelled' || s === 'abandoned') return 'bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400'
  if (s === 'inprogress' || s === 'notstarted' || s === 'queued' || s === 'notdeployed') return 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300'
  return 'bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400'
}

async function load(force = false) {
  tabError.value = ''
  try {
    if (mode.value === 'pipelines') {
      await store.fetchPipelineRuns(props.projectId, minDate.value, maxDate.value, force)
    } else {
      await store.fetchReleaseRuns(props.projectId, minDate.value, maxDate.value, force)
    }
  } catch (e) {
    tabError.value = e?.message || `Failed to fetch ${mode.value}`
  }
}

onMounted(() => load())
watch(() => props.projectId, () => { activeFilter.value = 'all'; load() })
</script>
