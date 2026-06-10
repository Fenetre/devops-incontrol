<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">DB Monitor</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">All user databases discovered on configured SQL Servers. <DataFreshness :timestamp="store.lastFetched.dbMonitor" /></p>
      </div>
      <UButton
        :loading="loading"
        :disabled="loading"
        icon="i-heroicons-arrow-path"
        @click="refresh"
      >
        {{ loading ? 'Loading…' : 'Refresh' }}
      </UButton>
    </div>

    <!-- Side menu + content -->
    <div class="flex gap-4">
      <!-- Project selector -->
      <div v-if="store.displayDbProjects.length" class="shrink-0">
        <nav class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Projects</div>
          <button
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium"
          >
            <UIcon name="i-heroicons-squares-2x2" class="w-4 h-4 shrink-0" />
            All databases
          </button>
          <button
            v-for="dbProj in store.displayDbProjects" :key="dbProj.id"
            @click="router.push({ name: 'db-project', params: { projectId: dbProj.id } })"
            class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40"
          >
            <UIcon name="i-heroicons-circle-stack" class="w-4 h-4 shrink-0" />
            <span class="whitespace-nowrap">{{ dbProj.name }}</span>
          </button>
        </nav>
      </div>

      <!-- Content -->
      <div class="flex-1 min-w-0">
        <EmptyState v-if="!store.dbCredentialsConfigured" icon="warning" variant="warning" message="Database credentials not configured." hint="Set them in Settings (gear icon top-right)." />

        <div v-else-if="loading && !databases.length" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2">
          <div v-for="n in 9" :key="n" class="flex items-center gap-2 px-3 py-2 rounded-lg bg-gray-50 dark:bg-gray-700 animate-pulse">
            <div class="w-2 h-2 rounded-full bg-gray-300 dark:bg-gray-500 shrink-0"></div>
            <div class="h-4 bg-gray-200 dark:bg-gray-600 rounded flex-1"></div>
          </div>
        </div>

        <div v-else-if="error" class="bg-white dark:bg-gray-800 rounded-xl border border-red-200 dark:border-red-700 shadow-sm px-6 py-8 text-center">
          <p class="text-red-500 text-sm">{{ error }}</p>
        </div>

        <EmptyState v-else-if="databases.length === 0 && !loading" icon="database" message="No databases found on the server." />

        <div v-else class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
          <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center gap-3">
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300 shrink-0">{{ filteredDatabases.length }} of {{ databases.length }} database{{ databases.length !== 1 ? 's' : '' }}</span>
            <UInput
              v-autofocus
              name="search" v-model="search"
              icon="i-heroicons-magnifying-glass"
              placeholder="Search databases…"
              size="sm"
              class="w-64 ml-auto app-search"
            />
          </div>
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 p-5">
            <div
              v-for="dbName in filteredDatabases"
              :key="dbName"
              class="flex items-center gap-2 px-3 py-2 rounded-lg bg-gray-50 dark:bg-gray-700 hover:bg-indigo-50 dark:hover:bg-indigo-900/30 transition-colors"
            >
              <span class="w-2 h-2 rounded-full bg-indigo-400 shrink-0"></span>
              <span class="text-sm text-gray-700 dark:text-gray-300 truncate">{{ dbName }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { transformDbDatabases } from '../composables/demoTransform.js'
import DataFreshness from '../components/DataFreshness.vue'
import EmptyState from '../components/EmptyState.vue'

const store = useMonitorStore()
const router = useRouter()
const databases = ref([])
const loading = ref(false)
const error = ref(null)
const search = ref('')

const filteredDatabases = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return databases.value
  return databases.value.filter(name => name.toLowerCase().includes(q))
})

async function refresh() {
  loading.value = true
  error.value = null
  try {
    await store.fetchAllDatabases()
    databases.value = transformDbDatabases(store.allDatabases)
  } catch (e) {
    error.value = e.message || 'Failed to load databases'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loading.value = true
  store.fetchDbCredentialsStatus().then(() => {
    if (store.dbCredentialsConfigured) {
      refresh()
    } else {
      loading.value = false
    }
  })
})
</script>
