<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">DB Monitor</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ project?.name || '' }}
          <span v-if="project?.name_filter" class="ml-2">· Filter: <span class="font-medium text-indigo-600 dark:text-indigo-400">{{ project.name_filter }}</span></span>
        </p>
      </div>
      <div class="flex items-center gap-2">
        <UButton
          v-if="closedTicketCount"
          icon="i-heroicons-clipboard"
          variant="soft"
          color="error"
          @click="copyAllClosed"
        >
          {{ copyAllLabel }}
        </UButton>
        <UButton
          :loading="loading || checkingRules"
          :disabled="loading || checkingRules"
          icon="i-heroicons-arrow-path"
          @click="refresh"
        >
          {{ loading ? 'Loading…' : checkingRules ? 'Checking rules…' : 'Refresh' }}
        </UButton>
      </div>
    </div>

    <!-- Side menu + content -->
    <div class="flex gap-4">
      <!-- Project selector -->
      <div v-if="store.displayDbProjects.length" class="shrink-0">
        <nav class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Projects</div>
          <button
            @click="router.push({ name: 'db-monitor' })"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="!projectId
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-squares-2x2" class="w-4 h-4 shrink-0" />
            All databases
          </button>
          <button
            v-for="dbProj in store.displayDbProjects" :key="dbProj.id"
            @click="router.push({ name: 'db-project', params: { projectId: dbProj.id } })"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
            :class="dbProj.id === projectId
              ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
          >
            <UIcon name="i-heroicons-circle-stack" class="w-4 h-4 shrink-0" />
            <span class="whitespace-nowrap">{{ dbProj.name }}</span>
          </button>
        </nav>
      </div>

      <!-- Content -->
      <div class="flex-1 min-w-0">
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
          <div v-if="rulesWarning" class="mx-5 mt-5 mb-1 bg-amber-50 dark:bg-amber-900/30 rounded-lg border border-amber-200 dark:border-amber-700 px-4 py-2">
            <p class="text-amber-800 dark:text-amber-300 text-sm">{{ rulesWarning }}</p>
          </div>
          <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center gap-3">
            <UInput
              v-autofocus
              name="search" v-model="search"
              icon="i-heroicons-magnifying-glass"
              placeholder="Search databases…"
              size="sm"
              class="w-64 shrink-0"
            />
            <div class="flex items-center gap-3 text-xs shrink-0" :class="{ 'invisible': !ruleResults.length }">
              <span class="text-gray-600 dark:text-gray-400 font-medium">{{ noTicketCount || 0 }} no ticket</span>
              <span class="text-red-600 dark:text-red-400 font-medium">{{ closedTicketCount || 0 }} closed</span>
            </div>
            <div class="flex items-center gap-4 mx-auto text-xs text-gray-600 dark:text-gray-400" :class="{ 'invisible': !ruleResults.length }">
              <span class="flex items-center gap-1.5">
                <UIcon name="i-heroicons-check-circle" class="w-4 h-4 text-green-500" />
                OK
              </span>
              <span class="flex items-center gap-1.5">
                <UIcon name="i-heroicons-shield-check" class="w-4 h-4 text-gray-400 dark:text-gray-300" />
                Allowlisted
              </span>
              <span class="flex items-center gap-1.5">
                <UIcon name="i-heroicons-question-mark-circle" class="w-4 h-4 text-gray-400 dark:text-gray-300" />
                No ticket number
              </span>
              <span class="flex items-center gap-1.5">
                <UIcon name="i-heroicons-exclamation-circle" class="w-4 h-4 text-red-500" />
                Ticket closed/done
              </span>
            </div>
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300 shrink-0 ml-auto">{{ filteredItems.length }}/{{ displayItems.length }} databases</span>
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 p-5">
            <div
              v-for="item in filteredItems"
              :key="item.name"
              class="group flex items-center gap-2 px-3 py-2 rounded-lg transition-colors"
              :class="itemBgClass(item)"
            >
              <UIcon v-if="item.status === 'closed_ticket'" name="i-heroicons-exclamation-circle" class="w-4 h-4 text-red-500 shrink-0" />
              <UIcon v-else-if="item.status === 'ok'" name="i-heroicons-check-circle" class="w-4 h-4 text-green-500 shrink-0" />
              <UIcon v-else-if="item.status === 'allowlisted'" name="i-heroicons-shield-check" class="w-4 h-4 text-gray-400 dark:text-gray-300 shrink-0" />
              <UIcon v-else-if="item.status === 'no_ticket'" name="i-heroicons-question-mark-circle" class="w-4 h-4 text-gray-400 dark:text-gray-300 shrink-0" />
              <span v-else class="w-2 h-2 rounded-full shrink-0" :class="itemDotClass(item)"></span>
              <span class="text-sm truncate" :class="itemTextClass(item)">{{ item.name }}</span>
              <span v-if="item.ticket_id" class="text-xs opacity-60">#{{ item.ticket_id }}</span>
              <div class="ml-auto shrink-0 flex items-center gap-0.5 opacity-0 group-hover:opacity-100 focus-within:opacity-100">
                <UButton
                  :icon="item.status === 'allowlisted' ? 'i-heroicons-shield-exclamation' : 'i-heroicons-shield-check'"
                  size="xs"
                  variant="ghost"
                  :color="item.status === 'allowlisted' ? 'warning' : 'neutral'"
                  :title="item.status === 'allowlisted' ? 'Remove from allowlist' : 'Add to allowlist'"
                  :loading="togglingName === item.name"
                  @click.stop="toggleAllowlist(item.name)"
                />
                <UButton
                  :icon="copiedName === item.name ? 'i-heroicons-check' : 'i-heroicons-clipboard'"
                  size="xs"
                  variant="ghost"
                  :color="copiedName === item.name ? 'success' : 'neutral'"
                  title="Copy database name"
                  @click.stop="copyName(item.name)"
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { transformDbDatabases, transformDbRulesResponse } from '../composables/demoTransform.js'
import EmptyState from '../components/EmptyState.vue'

const props = defineProps({ projectId: String })
const router = useRouter()
const store = useMonitorStore()

const loading = ref(false)
const checkingRules = ref(false)
const error = ref(null)
const rulesWarning = ref(null)
const databases = ref([])
const ruleResults = ref([])
const copyAllLabel = ref('Copy All Closed')
const copiedName = ref(null)
const togglingName = ref(null)
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

async function toggleAllowlist(name) {
  togglingName.value = name
  try {
    await store.toggleDbAllowlist(props.projectId, name)
    // Re-run rules to update statuses
    await runRules()
  } catch (e) {
    rulesWarning.value = e.message || 'Failed to toggle allowlist'
  } finally {
    togglingName.value = null
  }
}

const project = computed(() => store.displayDbProjects.find(p => p.id === props.projectId))

watch(() => props.projectId, () => {
  ruleResults.value = []
  rulesWarning.value = null
  loadFromCacheOrFetch()
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

function loadFromCacheOrFetch() {
  if (!store.dbCredentialsConfigured) return
  const cached = store.dbProjectDatabases[props.projectId]
  if (cached) {
    databases.value = transformDbDatabases(cached.databases || [])
    error.value = null
    runRules()
  } else {
    refresh()
  }
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
    loadFromCacheOrFetch()
  })
})
</script>
