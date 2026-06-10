<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ projectData?.project_name || projectId }}</span>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">PR Monitor</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">PR Monitor</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ projectData?.project_name || '' }}
          <span v-if="projectData && activeTab === 'prs'" class="ml-2">· {{ projectData.prs.length }} active PR{{ projectData.prs.length !== 1 ? 's' : '' }}</span>
          <span v-if="projectData && activeTab === 'prs'" class="ml-2">· {{ filteredItems.length }} search result{{ filteredItems.length !== 1 ? 's' : '' }}</span>
          <span v-if="projectData && activeTab === 'reviewers'" class="ml-2">· {{ reviewerGroups.length }} reviewer{{ reviewerGroups.length !== 1 ? 's' : '' }}</span>
        </p>
      </div>
      <div class="flex items-center gap-2">
        <UButton
          @click="refresh"
          :disabled="loading"
          :loading="loading"
          icon="i-heroicons-arrow-path"
          label="Refresh"
        />
        <UButton
          v-if="filteredItems.length"
          @click="exportPrs"
          variant="outline"
          color="neutral"
          icon="i-heroicons-arrow-down-tray"
          label="CSV"
          title="Export to CSV"
        />
      </div>
    </div>

    <!-- Error -->
    <UAlert v-if="projectData?.error" color="error" icon="i-heroicons-exclamation-circle" :description="projectData.error" class="mb-4" />

    <!-- Tabs -->
    <UTabs :items="tabItems" v-model="activeTab" :content="false" variant="link" class="mb-6" />

    <!-- Filter bar (Pull Requests tab) -->
    <div v-if="activeTab === 'prs'" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-4">
      <div class="px-4 py-3 flex items-center gap-3">
        <div class="flex-1">
          <UInput
            v-autofocus
            name="search" v-model="search"
            placeholder="Filter by title, repo, author…"
            icon="i-heroicons-magnifying-glass"
            size="sm"
            class="w-full app-search"
          />
        </div>
        <div class="flex items-center gap-2 shrink-0">
          <UButton
            v-for="f in filterOptions"
            :key="f.key"
            @click="toggleFilter(f.key)"
            :variant="activeFilters.includes(f.key) ? 'soft' : 'outline'"
            :color="activeFilters.includes(f.key) ? 'primary' : 'neutral'"
          >
            <span class="w-1.5 h-1.5 rounded-full" :class="f.color"></span>
            {{ f.label }}
          </UButton>
        </div>
      </div>
    </div>

    <!-- Reviewer search bar -->
    <div v-if="activeTab === 'reviewers'" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-4">
      <div class="px-4 py-3">
        <UInput
          name="reviewer-search" v-model="reviewerSearch"
          placeholder="Filter by reviewer name…"
          icon="i-heroicons-magnifying-glass"
          size="sm"
          class="w-full app-search"
        />
      </div>
    </div>

    <!-- Side menu + content -->
    <div class="flex gap-4">
      <!-- Project selector -->
      <div v-if="prProjects.length" class="shrink-0">
        <nav class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Projects</div>
          <button
            v-for="proj in prProjects" :key="proj.id"
            @click="router.push({ name: 'pr-project', params: { projectId: proj.id } })"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="proj.id === projectId
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-folder" class="w-4 h-4 shrink-0" />
            <span class="whitespace-nowrap">{{ proj.project }}</span>
          </button>
        </nav>
      </div>

      <!-- Content -->
      <div class="flex-1 min-w-0">
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

        <!-- ===== Pull Requests tab ===== -->
        <template v-if="activeTab === 'prs'">
          <!-- PR Table -->
          <div v-if="projectData && projectData.prs.length > 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
              <UTable :data="paginatedItems" :columns="prColumns" />

              <div v-if="filteredItems.length === 0" class="text-center py-8 text-gray-400 dark:text-gray-500 text-sm">
                {{ search || activeFilters.length ? 'No matching PRs.' : 'No active PRs.' }}
              </div>
            </div>

            <!-- Pagination -->
            <div v-if="totalPages > 1" class="flex items-center justify-between px-4 py-3 border-t border-gray-200 dark:border-gray-700">
              <span class="text-xs text-gray-500 dark:text-gray-400">
                Showing {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, filteredItems.length) }} of {{ filteredItems.length }}
              </span>
              <div class="flex items-center gap-1">
                <UButton size="sm" variant="outline" color="neutral" icon="i-heroicons-chevron-left" :disabled="page <= 1" @click="page = page - 1">Prev</UButton>
                <template v-for="p in visiblePages" :key="p">
                  <UButton size="sm" :variant="p === page ? 'solid' : 'outline'" :color="p === page ? 'primary' : 'neutral'" @click="page = p">{{ p }}</UButton>
                </template>
                <UButton size="sm" variant="outline" color="neutral" icon="i-heroicons-chevron-right" :disabled="page >= totalPages" @click="page = page + 1">Next</UButton>
              </div>
            </div>
          </div>

          <!-- Empty state -->
          <EmptyState v-if="projectData && projectData.prs.length === 0 && !projectData.error" icon="check-circle" message="No active pull requests." />
        </template>

        <!-- ===== Reviewer Monitor tab ===== -->
        <template v-if="activeTab === 'reviewers'">
          <EmptyState v-if="filteredReviewerGroups.length === 0" icon="users" message="No reviewers found." />

          <div v-else class="space-y-3">
            <div
              v-for="group in filteredReviewerGroups"
              :key="group.reviewer"
              class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden"
            >
              <!-- Reviewer header -->
              <button
                @click="toggleReviewerExpand(group.reviewer)"
                class="w-full flex items-center gap-3 px-5 py-4 text-left hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors"
              >
                <UIcon
                  :name="expandedReviewers.has(group.reviewer) ? 'i-heroicons-chevron-down' : 'i-heroicons-chevron-right'"
                  class="w-4 h-4 text-gray-400 shrink-0"
                />
                <UIcon name="i-heroicons-user-circle" class="w-5 h-5 text-gray-400 shrink-0" />
                <span class="font-medium text-gray-900 dark:text-gray-100">{{ group.reviewer }}</span>
                <span class="ml-auto text-sm text-gray-500 dark:text-gray-400">
                  {{ group.prs.length }} PR{{ group.prs.length !== 1 ? 's' : '' }}
                </span>
                <div class="flex items-center gap-1.5">
                  <span v-if="group.pendingCount > 0" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-50 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400">
                    <span class="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
                    {{ group.pendingCount }} pending
                  </span>
                  <span v-if="group.approvedCount > 0" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-400">
                    <span class="w-1.5 h-1.5 rounded-full bg-green-500"></span>
                    {{ group.approvedCount }} approved
                  </span>
                  <span v-if="group.rejectedCount > 0" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400">
                    <span class="w-1.5 h-1.5 rounded-full bg-red-500"></span>
                    {{ group.rejectedCount }} rejected
                  </span>
                </div>
              </button>

              <!-- PR list (expanded) -->
              <div v-if="expandedReviewers.has(group.reviewer)">
                <div class="border-t border-gray-200 dark:border-gray-700">
                  <div class="overflow-x-auto">
                    <table class="w-full text-sm">
                      <thead>
                        <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider bg-gray-50 dark:bg-gray-700/30">
                          <th class="px-4 py-2.5">ID</th>
                          <th class="px-4 py-2.5">Title</th>
                          <th class="px-4 py-2.5">Repository</th>
                          <th class="px-4 py-2.5">Author</th>
                          <th class="px-4 py-2.5">Vote</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr
                          v-for="item in group.prs"
                          :key="item.pr.pr_id"
                          class="border-b border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors"
                        >
                          <td class="px-4 py-2.5">
                            <a :href="item.pr.url" target="_blank" rel="noopener noreferrer" class="text-primary-600 hover:text-primary-800 font-medium hover:underline">#{{ item.pr.pr_id }}</a>
                          </td>
                          <td class="px-4 py-2.5">
                            <a :href="item.pr.url" target="_blank" rel="noopener noreferrer" class="hover:text-primary-600 hover:underline">
                              <span v-if="item.pr.is_draft" class="inline-block mr-1.5 px-1.5 py-0.5 rounded text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400">Draft</span>
                              {{ item.pr.title }}
                            </a>
                          </td>
                          <td class="px-4 py-2.5 text-gray-600 dark:text-gray-400">{{ item.pr.repository }}</td>
                          <td class="px-4 py-2.5 text-gray-600 dark:text-gray-400">{{ item.pr.created_by }}</td>
                          <td class="px-4 py-2.5">
                            <span :class="voteBadgeClass(item.vote)" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium">
                              {{ voteLabel(item.vote) }}
                            </span>
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script>
// Module-level state — survives component re-mounts from :key="$route.fullPath"
let _persistedTab = 'prs'
</script>

<script setup>
import { ref, computed, onMounted, watch, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { usePrStore } from '../stores/pr.js'
import { transformPrProject } from '../composables/demoTransform.js'
import { useCsvExport } from '../composables/useCsvExport.js'
import EmptyState from '../components/EmptyState.vue'

const props = defineProps({
  projectId: { type: String, required: true },
})

const route = useRoute()
const router = useRouter()
const store = useMonitorStore()
const prStore = usePrStore()
const { exportCsv } = useCsvExport()
const loading = computed(() => prStore.loadingPrProject)
const projectData = computed(() => transformPrProject(prStore.prProjectData[props.projectId] || null))

const activeTab = ref(_persistedTab)
watch(activeTab, (tab) => { _persistedTab = tab })
const tabItems = [
  { label: 'Pull Requests', value: 'prs', icon: 'i-heroicons-code-bracket-square' },
  { label: 'Reviewers', value: 'reviewers', icon: 'i-heroicons-user-group' },
]

const prProjects = computed(() => store.displayProjects)
const search = ref('')
const activeFilters = ref(route.query.filter ? [route.query.filter] : [])
const sortKey = ref('pr_id')
const sortDir = ref('desc')
const page = ref(1)
const pageSize = 50

const prColumns = [
  {
    accessorKey: 'pr_id',
    header: ({ column }) => h('span', {
      class: 'cursor-pointer select-none',
      onClick: () => toggleSort('pr_id')
    }, ['ID', sortKey.value === 'pr_id' ? h('span', { class: 'ml-1 text-xs' }, sortDir.value === 'asc' ? '▲' : '▼') : null]),
    cell: ({ row }) => h('a', {
      href: row.original.url,
      target: '_blank',
      rel: 'noopener noreferrer',
      class: 'text-primary-600 hover:text-primary-800 font-medium hover:underline'
    }, `#${row.original.pr_id}`)
  },
  {
    accessorKey: 'title',
    header: ({ column }) => h('span', {
      class: 'cursor-pointer select-none',
      onClick: () => toggleSort('title')
    }, ['Title', sortKey.value === 'title' ? h('span', { class: 'ml-1 text-xs' }, sortDir.value === 'asc' ? '▲' : '▼') : null]),
    cell: ({ row }) => h('a', {
      href: row.original.url,
      target: '_blank',
      rel: 'noopener noreferrer',
      class: 'hover:text-primary-600 hover:underline'
    }, [
      row.original.is_draft ? h('span', { class: 'inline-block mr-1.5 px-1.5 py-0.5 rounded text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400' }, 'Draft') : null,
      row.original.title
    ])
  },
  {
    accessorKey: 'repository',
    header: ({ column }) => h('span', {
      class: 'cursor-pointer select-none',
      onClick: () => toggleSort('repository')
    }, ['Repository', sortKey.value === 'repository' ? h('span', { class: 'ml-1 text-xs' }, sortDir.value === 'asc' ? '▲' : '▼') : null]),
  },
  {
    accessorKey: 'created_by',
    header: ({ column }) => h('span', {
      class: 'cursor-pointer select-none',
      onClick: () => toggleSort('created_by')
    }, ['Author', sortKey.value === 'created_by' ? h('span', { class: 'ml-1 text-xs' }, sortDir.value === 'asc' ? '▲' : '▼') : null]),
  },
  {
    accessorKey: 'days_inactive',
    header: ({ column }) => h('span', {
      class: 'cursor-pointer select-none',
      onClick: () => toggleSort('days_inactive')
    }, ['Stale', sortKey.value === 'days_inactive' ? h('span', { class: 'ml-1 text-xs' }, sortDir.value === 'asc' ? '▲' : '▼') : null]),
    cell: ({ row }) => {
      const d = row.original.days_inactive
      if (d != null) return h('span', { class: ['inline-block px-2 py-0.5 rounded-md text-xs font-medium', staleBadgeClass(d)] }, `${d}d`)
      return h('span', { class: 'text-xs text-gray-400 dark:text-gray-300' }, '—')
    }
  },
  {
    id: 'flags',
    header: 'Flags',
    cell: ({ row }) => {
      const flags = row.original.flags || []
      if (flags.length === 0) return h('span', { class: 'text-xs text-gray-400 dark:text-gray-300' }, '—')
      return h('div', { class: 'flex gap-1' },
        flags.map(flag => h('span', {
          key: flag.key,
          class: ['inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium', flagStyle(flag)]
        }, [
          h('span', { class: ['w-1.5 h-1.5 rounded-full', flagDot(flag)] }),
          flag.label
        ]))
      )
    }
  },
]

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
      return activeFilters.value.every(f => {
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

// --- Reviewer Monitor ---
const reviewerSearch = ref('')
const expandedReviewers = ref(new Set())

const reviewerGroups = computed(() => {
  if (!projectData.value) return []
  const map = new Map()
  for (const pr of projectData.value.prs) {
    for (const reviewer of (pr.reviewers || [])) {
      const name = reviewer.name || reviewer
      if (!map.has(name)) map.set(name, [])
      map.get(name).push({ pr, vote: reviewer.vote ?? 0 })
    }
  }
  return Array.from(map.entries())
    .map(([reviewer, prs]) => ({
      reviewer,
      prs,
      pendingCount: prs.filter(p => p.vote === 0).length,
      approvedCount: prs.filter(p => p.vote > 0).length,
      rejectedCount: prs.filter(p => p.vote < 0).length,
    }))
    .sort((a, b) => b.prs.length - a.prs.length)
})

const filteredReviewerGroups = computed(() => {
  if (!reviewerSearch.value) return reviewerGroups.value
  const q = reviewerSearch.value.toLowerCase()
  return reviewerGroups.value.filter(g => g.reviewer.toLowerCase().includes(q))
})

function toggleReviewerExpand(reviewer) {
  if (expandedReviewers.value.has(reviewer)) expandedReviewers.value.delete(reviewer)
  else expandedReviewers.value.add(reviewer)
}

function voteLabel(vote) {
  if (vote > 0) return 'Approved'
  if (vote < 0) return 'Rejected'
  return 'Pending'
}

function voteBadgeClass(vote) {
  if (vote > 0) return 'bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-400'
  if (vote < 0) return 'bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400'
  return 'bg-amber-50 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400'
}

async function refresh() {
  await prStore.fetchPrProjectData(props.projectId, true)
}

onMounted(() => prStore.fetchPrProjectData(props.projectId))
</script>
