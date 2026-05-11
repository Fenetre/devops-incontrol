<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-4">
      <router-link to="/pr-monitor" class="hover:text-primary-600 transition-colors">PR Monitor</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
      </svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ projectData?.project_name || projectId }}</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">{{ projectData?.project_name || 'Project' }}</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ projectData?.organization || '' }}
          <span v-if="projectData" class="ml-2">· {{ projectData.prs.length }} active PR{{ projectData.prs.length !== 1 ? 's' : '' }}</span>
          <span v-if="projectData" class="ml-2">· {{ filteredItems.length }} search result{{ filteredItems.length !== 1 ? 's' : '' }}</span>
        </p>
      </div>
      <div class="flex items-center gap-2">
        <button
          @click="refresh"
          :disabled="loading"
          class="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50 transition-colors"
        >
          <svg v-if="loading" class="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
          </svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182" />
          </svg>
          Refresh
        </button>
        <button
          v-if="filteredItems.length"
          @click="exportPrs"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          title="Export to CSV"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" /></svg>
          CSV
        </button>
        <router-link
          to="/pr-monitor"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
          </svg>
          Back
        </router-link>
      </div>
    </div>

    <!-- Error -->
    <div v-if="projectData?.error" class="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg p-4 mb-4 text-sm text-red-700 dark:text-red-400">
      {{ projectData.error }}
    </div>

    <!-- Filter bar -->
    <div v-if="projectData && projectData.prs.length > 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-4">
      <div class="px-4 py-3 flex flex-wrap items-center gap-3">
        <div class="relative flex-1 min-w-[200px]">
          <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
          </svg>
          <input
            v-autofocus
            v-model="search"
            type="text"
            placeholder="Filter by title, repo, author…"
            class="w-full pl-9 pr-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
          />
        </div>
        <div class="flex items-center gap-2">
          <button
            v-for="f in filterOptions"
            :key="f.key"
            @click="toggleFilter(f.key)"
            class="inline-flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg border transition-colors"
            :class="activeFilters.includes(f.key)
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-400 border-primary-300 dark:border-primary-600'
              : 'text-gray-600 dark:text-gray-400 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700'"
          >
            <span class="w-1.5 h-1.5 rounded-full" :class="f.color"></span>
            {{ f.label }}
          </button>
        </div>
      </div>
    </div>

    <!-- Loading skeleton -->
    <div v-if="loading && !projectData" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden animate-pulse">
      <div class="px-4 py-3 border-b border-gray-200 dark:border-gray-700">
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-48"></div>
      </div>
      <div v-for="n in 5" :key="n" class="px-4 py-3 border-b border-gray-100 dark:border-gray-700 flex items-center gap-4">
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-12"></div>
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded flex-1"></div>
        <div class="h-4 bg-gray-100 dark:bg-gray-600 rounded w-24"></div>
        <div class="h-4 bg-gray-100 dark:bg-gray-600 rounded w-28"></div>
        <div class="h-5 bg-gray-100 dark:bg-gray-600 rounded-full w-20"></div>
      </div>
    </div>

    <!-- No data (after loading finishes with nothing) -->
    <EmptyState v-else-if="!projectData && !loading" icon="git-merge" message="No PR data found for this project." />

    <!-- PR Table -->
    <div v-if="projectData && projectData.prs.length > 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
      <div class="overflow-x-auto">
        <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
          <thead>
            <tr class="text-left border-b border-gray-200 dark:border-gray-700">
              <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('pr_id')">
                ID
                <span v-if="sortKey === 'pr_id'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
              </th>
              <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('title')">
                Title
                <span v-if="sortKey === 'title'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
              </th>
              <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('repository')">
                Repository
                <span v-if="sortKey === 'repository'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
              </th>
              <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('created_by')">
                Author
                <span v-if="sortKey === 'created_by'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
              </th>
              <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('days_inactive')">
                Stale
                <span v-if="sortKey === 'days_inactive'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
              </th>
              <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300">
                Flags
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="pr in paginatedItems"
              :key="pr.pr_id"
              class="border-b border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
            >
              <td class="px-4 py-3">
                <a :href="pr.url" target="_blank" rel="noopener noreferrer" class="text-primary-600 hover:text-primary-800 font-medium hover:underline">
                  #{{ pr.pr_id }}
                </a>
              </td>
              <td class="px-4 py-3 text-gray-800 dark:text-gray-200 !whitespace-normal">
                <a :href="pr.url" target="_blank" rel="noopener noreferrer" class="hover:text-primary-600 hover:underline">
                  <span v-if="pr.is_draft" class="inline-block mr-1.5 px-1.5 py-0.5 rounded text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400">Draft</span>
                  {{ pr.title }}
                </a>
              </td>
              <td class="px-4 py-3 text-gray-600 dark:text-gray-400 text-xs">{{ pr.repository }}</td>
              <td class="px-4 py-3 text-gray-700 dark:text-gray-300">{{ pr.created_by }}</td>
              <td class="px-4 py-3 text-center">
                <span v-if="pr.days_inactive != null" class="inline-block px-2 py-0.5 rounded-md text-xs font-medium" :class="staleBadgeClass(pr.days_inactive)">
                  {{ pr.days_inactive }}d
                </span>
                <span v-else class="text-xs text-gray-400">—</span>
              </td>
              <td class="px-4 py-3">
                <div class="flex gap-1">
                  <span
                    v-for="flag in pr.flags"
                    :key="flag.key"
                    class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium"
                    :class="flagStyle(flag)"
                  >
                    <span class="w-1.5 h-1.5 rounded-full" :class="flagDot(flag)"></span>
                    {{ flag.label }}
                  </span>
                  <span v-if="pr.flags.length === 0" class="text-xs text-gray-400">—</span>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <div v-if="filteredItems.length === 0" class="text-center py-8 text-gray-400 dark:text-gray-500 text-sm">
          {{ search || activeFilters.length ? 'No matching PRs.' : 'No active PRs.' }}
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="flex items-center justify-between px-4 py-3 border-t border-gray-200 dark:border-gray-700">
        <span class="text-xs text-gray-500">
          Showing {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, filteredItems.length) }} of {{ filteredItems.length }}
        </span>
        <div class="flex items-center gap-1">
          <button @click="page = Math.max(1, page - 1)" :disabled="page === 1" class="px-2 py-1 text-xs rounded border border-gray-300 dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-40 transition-colors">&laquo;</button>
          <button
            v-for="p in visiblePages"
            :key="p"
            @click="page = p"
            class="px-2.5 py-1 text-xs rounded border transition-colors"
            :class="p === page ? 'bg-primary-600 text-white border-primary-600' : 'border-gray-300 dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700'"
          >{{ p }}</button>
          <button @click="page = Math.min(totalPages, page + 1)" :disabled="page === totalPages" class="px-2 py-1 text-xs rounded border border-gray-300 dark:border-gray-600 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-40 transition-colors">&raquo;</button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <EmptyState v-if="projectData && projectData.prs.length === 0 && !projectData.error" icon="check-circle" message="No active pull requests." />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { transformPrProject } from '../composables/demoTransform.js'
import { useCsvExport } from '../composables/useCsvExport.js'
import EmptyState from '../components/EmptyState.vue'

const props = defineProps({
  projectId: { type: String, required: true },
})

const route = useRoute()
const store = useMonitorStore()
const { exportCsv } = useCsvExport()
const loading = computed(() => store.loadingPrProject)
const projectData = computed(() => transformPrProject(store.prProjectData[props.projectId] || null))
const search = ref('')
const activeFilters = ref(route.query.filter ? [route.query.filter] : [])
const sortKey = ref('pr_id')
const sortDir = ref('desc')
const page = ref(1)
const pageSize = 50

const filterOptions = [
  { key: 'unreviewed', label: 'No reviewer', color: 'bg-red-500' },
  { key: 'rejected', label: 'Rejected', color: 'bg-red-500' },
  { key: 'stale', label: 'Stale', color: 'bg-amber-500' },
  { key: 'approval_ready', label: 'Ready', color: 'bg-blue-500' },
  { key: 'draft', label: 'Draft', color: 'bg-gray-400' },
  { key: 'clean', label: 'No flags', color: 'bg-green-500' },
]

function toggleFilter(key) {
  const idx = activeFilters.value.indexOf(key)
  if (idx >= 0) activeFilters.value.splice(idx, 1)
  else activeFilters.value.push(key)
  page.value = 1
}

function toggleSort(key) {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortDir.value = 'asc'
  }
}

const filteredItems = computed(() => {
  if (!projectData.value) return []
  let items = projectData.value.prs

  if (search.value) {
    const q = search.value.toLowerCase()
    items = items.filter(pr =>
      String(pr.pr_id).includes(q) ||
      pr.title.toLowerCase().includes(q) ||
      pr.repository.toLowerCase().includes(q) ||
      pr.created_by.toLowerCase().includes(q)
    )
  }

  if (activeFilters.value.length > 0) {
    items = items.filter(pr => {
      return activeFilters.value.some(f => {
        if (f === 'draft') return pr.is_draft
        if (f === 'clean') return pr.flags.length === 0
        return pr.flags.some(fl => fl.key === f)
      })
    })
  }

  const key = sortKey.value
  const dir = sortDir.value === 'asc' ? 1 : -1
  items = [...items].sort((a, b) => {
    const av = a[key] ?? ''
    const bv = b[key] ?? ''
    if (typeof av === 'number' && typeof bv === 'number') return (av - bv) * dir
    return String(av).localeCompare(String(bv)) * dir
  })

  return items
})

const totalPages = computed(() => Math.ceil(filteredItems.value.length / pageSize))
const paginatedItems = computed(() => {
  const start = (page.value - 1) * pageSize
  return filteredItems.value.slice(start, start + pageSize)
})

function exportPrs() {
  const items = filteredItems.value
  if (!items.length) return
  const cols = [
    { key: 'pr_id', label: 'PR ID' },
    { key: 'title', label: 'Title' },
    { key: 'repository', label: 'Repository' },
    { key: 'created_by', label: 'Author' },
    { key: 'created_date', label: 'Created' },
    { label: 'Stale Days', format: r => r.stale_days ?? '' },
    { label: 'Flags', format: r => (r.flags || []).map(f => f.label || f.key).join('; ') },
    { label: 'Draft', format: r => r.is_draft ? 'Yes' : 'No' },
    { label: 'URL', format: r => r.url || '' },
  ]
  exportCsv(items, cols, `PRs_${projectData.value?.project_name || props.projectId}`)
}
const visiblePages = computed(() => {
  const pages = []
  const start = Math.max(1, page.value - 2)
  const end = Math.min(totalPages.value, page.value + 2)
  for (let i = start; i <= end; i++) pages.push(i)
  return pages
})

watch(search, () => { page.value = 1 })

function flagStyle(flag) {
  if (flag.severity === 'error') return 'bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400'
  if (flag.severity === 'warning') return 'bg-amber-50 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400'
  return 'bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400'
}

function flagDot(flag) {
  if (flag.severity === 'error') return 'bg-red-500'
  if (flag.severity === 'warning') return 'bg-amber-500'
  return 'bg-blue-500'
}

function staleBadgeClass(days) {
  if (days >= 14) return 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-400'
  if (days >= 7) return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-400'
  if (days >= 3) return 'bg-yellow-100 dark:bg-yellow-900/40 text-yellow-700 dark:text-yellow-400'
  return 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-400'
}

async function refresh() {
  await store.fetchPrProjectData(props.projectId, true)
}

onMounted(() => store.fetchPrProjectData(props.projectId))
</script>
