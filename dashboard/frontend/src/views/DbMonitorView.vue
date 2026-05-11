<template>
  <div>
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">All Databases</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">All user databases discovered on configured SQL Servers. <DataFreshness :timestamp="store.lastFetched.dbMonitor" /></p>
      </div>
      <button
        @click="refresh"
        :disabled="loading"
        class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 transition-colors shadow-sm"
      >
        <svg class="w-4 h-4" :class="{ 'animate-spin': loading }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182" />
        </svg>
        {{ loading ? 'Loading…' : 'Refresh' }}
      </button>
    </div>

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
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { transformDbDatabases } from '../composables/demoTransform.js'
import DataFreshness from '../components/DataFreshness.vue'
import EmptyState from '../components/EmptyState.vue'

const store = useMonitorStore()
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
