<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">PR Monitor</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">Active pull requests across all configured projects <DataFreshness :timestamp="store.lastFetched.prMonitor" /></p>
      </div>
      <UButton
        @click="refresh"
        :disabled="loading"
        :loading="loading"
        icon="i-heroicons-arrow-path"
        :label="loading ? 'Loading...' : 'Refresh'"
      />
    </div>

    <!-- Loading skeleton -->
    <div v-if="loading && prProjects.length === 0" class="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
      <div v-for="n in 3" :key="n" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-5 animate-pulse">
        <div class="flex items-center justify-between mb-3">
          <div class="h-5 bg-gray-200 dark:bg-gray-700 rounded w-3/4"></div>
          <div class="h-5 bg-gray-100 dark:bg-gray-600 rounded-full w-16"></div>
        </div>
        <div class="h-3 bg-gray-100 dark:bg-gray-700 rounded w-1/3 mb-3"></div>
        <div class="flex gap-2">
          <div class="h-6 bg-gray-100 dark:bg-gray-600 rounded-full w-24"></div>
          <div class="h-6 bg-gray-100 dark:bg-gray-600 rounded-full w-20"></div>
        </div>
      </div>
    </div>

    <!-- No projects -->
    <EmptyState v-else-if="!loading && prProjects.length === 0" icon="git-merge" message="No projects with PR checks enabled." hint="Enable a PR check in the configuration for a project." action-label="Configure Projects" action-to="/config" />

    <!-- Project cards -->
    <div class="grid grid-cols-1 lg:grid-cols-2 xl:grid-cols-3 gap-4">
      <router-link
        v-for="proj in prProjects"
        :key="proj.project_id"
        :to="{ name: 'pr-project', params: { projectId: proj.project_id } }"
        class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm hover:shadow-md transition-shadow p-5 block"
      >
        <div class="flex items-center justify-between mb-3">
          <h3 class="text-base font-semibold text-primary-500 dark:text-gray-100 truncate">{{ proj.project_name }}</h3>
          <span class="text-xs bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 rounded-full px-2.5 py-0.5 font-medium shrink-0 ml-2">
            {{ proj.prs.length }} PR{{ proj.prs.length !== 1 ? 's' : '' }}
          </span>
        </div>
        <p class="text-xs text-gray-500 dark:text-gray-400 mb-3">{{ proj.organization }}</p>

        <div v-if="proj.error" class="text-xs text-red-500 mb-2">{{ proj.error }}</div>

        <!-- Flag summary -->
        <div class="flex flex-wrap gap-1.5">
          <span v-if="flagCount(proj, 'unreviewed')" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400">
            <span class="w-1.5 h-1.5 rounded-full bg-red-500"></span>
            {{ flagCount(proj, 'unreviewed') }} no reviewer
          </span>
          <span v-if="flagCount(proj, 'rejected')" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-red-50 dark:bg-red-900/30 text-red-700 dark:text-red-400">
            <span class="w-1.5 h-1.5 rounded-full bg-red-500"></span>
            {{ flagCount(proj, 'rejected') }} rejected
          </span>
          <span v-if="flagCount(proj, 'stale')" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-50 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400">
            <span class="w-1.5 h-1.5 rounded-full bg-amber-500"></span>
            {{ flagCount(proj, 'stale') }} stale
          </span>
          <span v-if="flagCount(proj, 'approval_ready')" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400">
            <span class="w-1.5 h-1.5 rounded-full bg-blue-500"></span>
            {{ flagCount(proj, 'approval_ready') }} ready
          </span>
          <span v-if="proj.prs.length > 0 && totalFlags(proj) === 0" class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-400">
            <span class="w-1.5 h-1.5 rounded-full bg-green-500"></span>
            All clear
          </span>
        </div>
      </router-link>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import DataFreshness from '../components/DataFreshness.vue'
import EmptyState from '../components/EmptyState.vue'

const store = useMonitorStore()
const loading = computed(() => store.loadingPrMonitor)
const prProjects = computed(() => store.displayPrProjects)

async function refresh() {
  await store.fetchPrProjects(true)
}

function flagCount(proj, key) {
  return proj.prs.filter(pr => pr.flags.some(f => f.key === key)).length
}

function totalFlags(proj) {
  return proj.prs.filter(pr => pr.flags.length > 0).length
}

onMounted(() => store.fetchPrProjects())
</script>
