<template>
  <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm hover:shadow-md transition-shadow overflow-hidden">
    <!-- Header -->
    <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700">
      <div class="flex items-center justify-between">
        <h3 class="font-semibold text-primary-500 dark:text-gray-100 truncate">{{ project.project_name || project.project }}</h3>
        <div class="flex items-center gap-2">
          <span class="text-xs text-gray-400">{{ project.organization || '' }}</span>
          <button
            @click="$emit('run-project')"
            :disabled="running"
            class="inline-flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium text-gray-500 hover:text-primary-600 hover:bg-primary-50 disabled:opacity-50 transition-colors"
            title="Run checks for this project"
          >
            <svg v-if="running" class="animate-spin w-3.5 h-3.5" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
            </svg>
            <svg v-else class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-.856.917-1.398 1.667-.986l11.54 6.347a1.125 1.125 0 010 1.972l-11.54 6.347a1.125 1.125 0 01-1.667-.986V5.653z" />
            </svg>
          </button>
        </div>
      </div>
    </div>

    <!-- Body -->
    <div class="px-5 py-4">
      <!-- Error state -->
      <div v-if="errorChecks.length > 0 && issueChecks.length === 0" class="flex items-center gap-2 text-amber-600 dark:text-amber-400">
        <svg class="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
        </svg>
        <span class="text-sm font-medium">{{ errorChecks.length }} check{{ errorChecks.length !== 1 ? 's' : '' }} failed</span>
      </div>

      <!-- All clear state (may still have informational badges) -->
      <div v-else-if="issueChecks.length === 0 && errorChecks.length === 0" class="space-y-2">
        <div class="flex items-center gap-2 text-green-600 dark:text-green-400">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span class="text-sm font-medium">All clear — no issues</span>
        </div>
        <div v-if="infoOnlyChecks.length > 0" class="flex flex-wrap gap-2">
          <router-link
            v-for="check in infoOnlyChecks"
            :key="'info-' + check.check_type"
            :to="{ name: 'issues', params: { projectId: projectId, checkType: check.check_type } }"
            class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium transition-all hover:scale-105 hover:shadow-sm cursor-pointer bg-sky-50 text-sky-700 hover:bg-sky-100 dark:bg-sky-900/30 dark:text-sky-400 dark:hover:bg-sky-900/50"
          >
            <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9.568 3H5.25A2.25 2.25 0 003 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 005.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 009.568 3z" />
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 6h.008v.008H6V6z" />
            </svg>
            {{ check.check_label }}
            <span class="ml-0.5 font-bold">{{ getCheckCount(check) }}</span>
          </router-link>
        </div>
      </div>

      <!-- Issue badges -->
      <div v-else class="flex flex-wrap gap-2">
        <router-link
          v-for="check in issueChecks"
          :key="check.check_type"
          :to="checkRoute(check, projectId)"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium transition-all hover:scale-105 hover:shadow-sm cursor-pointer"
          :class="badgeClass(check.check_type)"
        >
          <span class="w-1.5 h-1.5 rounded-full" :class="dotClass(check.check_type)"></span>
          {{ check.check_label }}
          <span class="ml-0.5 font-bold">{{ getCheckCount(check) }}</span>
        </router-link>
        <span
          v-for="check in errorChecks"
          :key="'err-' + check.check_type"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400"
          :title="check.error"
        >
          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
          </svg>
          {{ check.check_label }}
        </span>
        <router-link
          v-for="check in infoOnlyChecks"
          :key="'info-' + check.check_type"
          :to="{ name: 'issues', params: { projectId: projectId, checkType: check.check_type } }"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium transition-all hover:scale-105 hover:shadow-sm cursor-pointer bg-sky-50 text-sky-700 hover:bg-sky-100 dark:bg-sky-900/30 dark:text-sky-400 dark:hover:bg-sky-900/50"
        >
          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9.568 3H5.25A2.25 2.25 0 003 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 005.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 009.568 3z" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 6h.008v.008H6V6z" />
          </svg>
          {{ check.check_label }}
          <span class="ml-0.5 font-bold">{{ getCheckCount(check) }}</span>
        </router-link>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const PR_CHECK_TYPES = ['pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check']

const PR_CHECK_FILTER_MAP = {
  stale_pr_check: 'stale',
  unreviewed_pr_check: 'unreviewed',
  pr_approval_check: 'approval_ready',
}

const PR_CHECK_COUNT_OVERRIDE_MAP = {
  stale_pr_check: 'stale',
  unreviewed_pr_check: 'unreviewed',
  pr_approval_check: 'approval_ready',
}

function checkRoute(check, projectId) {
  if (PR_CHECK_TYPES.includes(check.check_type)) {
    const route = { name: 'pr-project', params: { projectId } }
    const filter = PR_CHECK_FILTER_MAP[check.check_type]
    if (filter) route.query = { filter }
    return route
  }
  return { name: 'issues', params: { projectId, checkType: check.check_type } }
}

const props = defineProps({
  project: { type: Object, required: true },
  projectId: { type: String, required: true },
  checks: { type: Array, default: () => [] },
  infoChecks: { type: Array, default: () => [] },
  running: { type: Boolean, default: false },
  prFlagCounts: { type: Object, default: null },
})

const emit = defineEmits(['run-project'])

function getCheckCount(check) {
  const flagKey = PR_CHECK_COUNT_OVERRIDE_MAP[check.check_type]
  if (flagKey && props.prFlagCounts && typeof props.prFlagCounts[flagKey] === 'number') {
    return props.prFlagCounts[flagKey]
  }
  return check.flagged_items.length
}

const issueChecks = computed(() => props.checks.filter(c => getCheckCount(c) > 0 && !props.infoChecks.includes(c.check_type)))
const infoOnlyChecks = computed(() => props.checks.filter(c => getCheckCount(c) > 0 && props.infoChecks.includes(c.check_type)))
const errorChecks = computed(() => props.checks.filter(c => c.error))

function badgeClass(checkType) {
  const map = {
    orphan_check: 'bg-red-50 text-red-700 hover:bg-red-100 dark:bg-red-900/30 dark:text-red-400 dark:hover:bg-red-900/50',
    unassigned_check: 'bg-red-50 text-red-700 hover:bg-red-100 dark:bg-red-900/30 dark:text-red-400 dark:hover:bg-red-900/50',
    stale_sprint_check: 'bg-amber-50 text-amber-700 hover:bg-amber-100 dark:bg-amber-900/30 dark:text-amber-400 dark:hover:bg-amber-900/50',
    missing_estimate_check: 'bg-amber-50 text-amber-700 hover:bg-amber-100 dark:bg-amber-900/30 dark:text-amber-400 dark:hover:bg-amber-900/50',
    release_pr_check: 'bg-red-50 text-red-700 hover:bg-red-100 dark:bg-red-900/30 dark:text-red-400 dark:hover:bg-red-900/50',
    resolved_pr_check: 'bg-purple-50 text-purple-700 hover:bg-purple-100 dark:bg-purple-900/30 dark:text-purple-400 dark:hover:bg-purple-900/50',
    tag_overview_check: 'bg-sky-50 text-sky-700 hover:bg-sky-100 dark:bg-sky-900/30 dark:text-sky-400 dark:hover:bg-sky-900/50',
  }
  return map[checkType] || 'bg-gray-50 text-gray-700 hover:bg-gray-100 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600'
}

function dotClass(checkType) {
  const map = {
    orphan_check: 'bg-red-500',
    unassigned_check: 'bg-red-500',
    stale_sprint_check: 'bg-amber-500',
    missing_estimate_check: 'bg-amber-500',
    release_pr_check: 'bg-red-500',
    resolved_pr_check: 'bg-purple-500',
    tag_overview_check: 'bg-sky-500',
  }
  return map[checkType] || 'bg-gray-500'
}
</script>
