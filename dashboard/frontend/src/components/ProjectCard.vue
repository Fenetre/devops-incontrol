<template>
  <UCard class="hover:shadow-md transition-shadow">
    <template #header>
      <div class="flex items-center justify-between">
        <h3 class="font-semibold text-primary-500 dark:text-gray-100 truncate">{{ project.project_name || project.project }}</h3>
        <div class="flex items-center gap-2">
          <span class="text-xs text-gray-400 dark:text-gray-300">{{ project.organization || '' }}</span>
          <UButton
            size="xs"
            variant="ghost"
            color="neutral"
            :loading="running"
            icon="i-heroicons-play"
            title="Run checks for this project"
            @click="$emit('run-project')"
          />
        </div>
      </div>
    </template>

    <!-- Error state -->
    <div v-if="errorChecks.length > 0 && issueChecks.length === 0" class="flex items-center gap-2 text-amber-600 dark:text-amber-400">
      <UIcon name="i-heroicons-exclamation-triangle" class="w-5 h-5 shrink-0" />
      <span class="text-sm font-medium">{{ errorChecks.length }} check{{ errorChecks.length !== 1 ? 's' : '' }} failed</span>
    </div>

    <!-- All clear state -->
    <div v-else-if="issueChecks.length === 0 && errorChecks.length === 0" class="space-y-2">
      <div class="flex items-center gap-2 text-green-600 dark:text-green-400">
        <UIcon name="i-heroicons-check-circle" class="w-5 h-5" />
        <span class="text-sm font-medium">All clear — no issues</span>
      </div>
      <div v-if="infoOnlyChecks.length > 0" class="flex flex-wrap gap-2">
        <router-link
          v-for="check in infoOnlyChecks"
          :key="'info-' + check.check_type"
          :to="{ name: 'issues', params: { projectId: projectId, checkType: check.check_type } }"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium transition-all hover:scale-105 hover:shadow-sm cursor-pointer bg-sky-50 text-sky-700 hover:bg-sky-100 dark:bg-sky-900/30 dark:text-sky-400 dark:hover:bg-sky-900/50"
        >
          <UIcon name="i-heroicons-tag" class="w-3.5 h-3.5" />
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
        <UIcon name="i-heroicons-exclamation-triangle" class="w-3.5 h-3.5" />
        {{ check.check_label }}
      </span>
      <router-link
        v-for="check in infoOnlyChecks"
        :key="'info-' + check.check_type"
        :to="{ name: 'issues', params: { projectId: projectId, checkType: check.check_type } }"
        class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium transition-all hover:scale-105 hover:shadow-sm cursor-pointer bg-sky-50 text-sky-700 hover:bg-sky-100 dark:bg-sky-900/30 dark:text-sky-400 dark:hover:bg-sky-900/50"
      >
        <UIcon name="i-heroicons-tag" class="w-3.5 h-3.5" />
        {{ check.check_label }}
        <span class="ml-0.5 font-bold">{{ getCheckCount(check) }}</span>
      </router-link>
    </div>
  </UCard>
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
