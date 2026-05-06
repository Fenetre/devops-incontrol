<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Sprint Populator</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Clone "sprint template" user stories (with tasks) into a selected sprint.
        </p>
      </div>
    </div>

    <!-- PAT Warning -->
    <div v-if="!store.patConfigured" class="mb-6 bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-amber-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-amber-800 dark:text-amber-200">PAT not configured. Set it in Settings first.</p>
    </div>

    <!-- Step 1: Select projects -->
    <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3 flex items-center gap-2">
        <span class="flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold" :class="step >= 1 ? 'bg-primary-600 text-white' : 'bg-gray-200 dark:bg-gray-600 text-gray-500'">1</span>
        Select Projects
      </h3>
      <label class="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-2">Target project (where work items are created)</label>
      <select v-model="selectedProjectId" @change="onProjectChange" :disabled="loading || applying"
        class="w-full max-w-md px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 outline-none">
        <option value="">Choose a project…</option>
        <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.project }} ({{ p.organization }})</option>
      </select>

      <div class="mt-4">
        <label class="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-2">Template source project (optional)</label>
        <select v-model="selectedSourceProjectId" @change="onSourceProjectChange" :disabled="!selectedProjectId || loading || applying"
          class="w-full max-w-md px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 outline-none">
          <option value="" disabled>Select source project…</option>
          <option v-for="p in projects" :key="`source-${p.id}`" :value="p.id">{{ p.project }} ({{ p.organization }})</option>
        </select>
        <p class="mt-2 text-xs text-gray-500 dark:text-gray-400">
          Templates are read from the source project and cloned into the selected target sprint.
        </p>
      </div>
    </div>

    <!-- Step 2: Select team -->
    <div v-if="step >= 2" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3 flex items-center gap-2">
        <span class="flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold" :class="step >= 2 ? 'bg-primary-600 text-white' : 'bg-gray-200 dark:bg-gray-600 text-gray-500'">2</span>
        Select Team
      </h3>
      <div v-if="loadingTeams" class="flex items-center gap-2 text-sm text-gray-500">
        <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        Loading teams…
      </div>
      <select v-else v-model="selectedTeam" @change="onTeamChange" :disabled="loading || applying"
        class="w-full max-w-md px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 outline-none">
        <option value="">Choose a team…</option>
        <option v-for="t in teams" :key="t.name" :value="t.name">{{ isDemoMode ? anonTeam(t.name) : t.name }}</option>
      </select>
    </div>

    <!-- Step 3: Select sprint -->
    <div v-if="step >= 3" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3 flex items-center gap-2">
        <span class="flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold" :class="step >= 3 ? 'bg-primary-600 text-white' : 'bg-gray-200 dark:bg-gray-600 text-gray-500'">3</span>
        Select Sprint
      </h3>
      <div v-if="loadingIterations" class="flex items-center gap-2 text-sm text-gray-500">
        <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        Loading sprints…
      </div>
      <select v-else v-model="selectedIterationIdx" @change="onSprintChange" :disabled="loading || applying"
        class="w-full max-w-md px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 outline-none">
        <option :value="-1">Choose a sprint…</option>
        <option v-for="(it, idx) in iterations" :key="idx" :value="idx">{{ isDemoMode ? anonIterationPath(it.name) : it.name }}</option>
      </select>
    </div>

    <!-- Step 4: Preview -->
    <div v-if="step >= 4" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3 flex items-center gap-2">
        <span class="flex items-center justify-center w-6 h-6 rounded-full text-xs font-bold bg-primary-600 text-white">4</span>
        Preview
      </h3>

      <div v-if="loadingPreview" class="flex items-center gap-2 text-sm text-gray-500">
        <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        Scanning for sprint templates…
      </div>

      <div v-else-if="preview && preview.templates.length === 0" class="text-sm text-gray-500 dark:text-gray-400 italic">
        No User Stories found with the tag "sprint template" in the selected source project.
      </div>

      <div v-else-if="preview" class="space-y-4">
        <div class="text-xs text-gray-500 dark:text-gray-400 mb-2">
          Source: <span class="font-medium text-gray-700 dark:text-gray-300">{{ sourceProjectLabel }}</span>
          <span class="mx-1">→</span>
          Target: <span class="font-medium text-gray-700 dark:text-gray-300">{{ targetProjectLabel }}</span>
        </div>
        <div v-if="preview.sprint_number" class="text-sm text-gray-600 dark:text-gray-400 mb-2">
          Detected sprint number: <span class="font-semibold text-gray-800 dark:text-gray-200">{{ preview.sprint_number }}</span>
          — title "X" placeholders will be replaced.
        </div>
        <div v-else class="text-sm text-amber-600 dark:text-amber-400 mb-2">
          Could not detect a sprint number from the sprint name. Title substitutions (X → sprint#) will be skipped.
        </div>

        <div v-for="tmpl in preview.templates" :key="tmpl.story_id"
          class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
          <div class="px-4 py-3 bg-gray-50 dark:bg-gray-700/50 flex items-center justify-between">
            <div>
              <span class="text-xs text-gray-500 dark:text-gray-400">User Story #{{ tmpl.story_id }}</span>
              <span class="mx-2 text-gray-300 dark:text-gray-600">→</span>
              <span class="font-medium text-gray-800 dark:text-gray-100">{{ isDemoMode ? anonPrTitle(tmpl.final_title) : tmpl.final_title }}</span>
            </div>
            <span class="text-xs bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 rounded-full px-2.5 py-0.5 font-medium">
              {{ tmpl.task_count }} task{{ tmpl.task_count !== 1 ? 's' : '' }}
            </span>
          </div>
          <div v-if="tmpl.original_title !== tmpl.final_title" class="px-4 py-2 text-xs text-gray-500 dark:text-gray-400 border-t border-gray-100 dark:border-gray-700">
            Original: <span class="italic">{{ isDemoMode ? anonPrTitle(tmpl.original_title) : tmpl.original_title }}</span>
          </div>
          <div v-if="tmpl.tasks.length" class="px-4 py-2 border-t border-gray-100 dark:border-gray-700">
            <div v-for="task in tmpl.tasks" :key="task.task_id" class="flex items-center gap-2 py-1 text-sm text-gray-600 dark:text-gray-400">
              <svg class="w-3.5 h-3.5 text-gray-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span class="text-xs text-gray-400">Task #{{ task.task_id }}:</span>
              {{ isDemoMode ? anonPrTitle(task.title) : task.title }}
            </div>
          </div>
        </div>

        <!-- Apply section -->
        <div class="mt-6 pt-4 border-t border-gray-200 dark:border-gray-700">
          <!-- Confirmation checkbox -->
          <label class="flex items-start gap-3 mb-4 cursor-pointer select-none">
            <input type="checkbox" v-model="confirmed" :disabled="applying || applied || isDemoMode"
              class="mt-0.5 w-4 h-4 text-primary-600 border-gray-300 dark:border-gray-600 rounded focus:ring-primary-500" />
            <span class="text-sm text-gray-700 dark:text-gray-300">
              I confirm I want to create <strong>{{ preview.templates.length }} user stor{{ preview.templates.length !== 1 ? 'ies' : 'y' }}</strong>
              and <strong>{{ totalTasks }} task{{ totalTasks !== 1 ? 's' : '' }}</strong>
              in sprint <strong>{{ selectedSprintName }}</strong>.
              <span class="text-red-500 font-medium">This action cannot be undone.</span>
            </span>
          </label>

          <div v-if="isDemoMode" class="text-sm text-amber-600 dark:text-amber-400 mb-3">Disabled in demo mode</div>

          <div class="flex items-center gap-3">
            <button @click="applyClone" :disabled="!confirmed || applying || applied || isDemoMode"
              class="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-medium text-white transition-colors shadow-sm"
              :class="applied
                ? 'bg-green-600 cursor-default'
                : confirmed && !applying
                  ? 'bg-red-600 hover:bg-red-700'
                  : 'bg-gray-400 cursor-not-allowed'">
              <svg v-if="applying" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
              <svg v-else-if="applied" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.59 14.37a6 6 0 01-5.84 7.38v-4.8m5.84-2.58a14.98 14.98 0 006.16-12.12A14.98 14.98 0 009.631 8.41m5.96 5.96a14.926 14.926 0 01-5.841 2.58m-.119-8.54a6 6 0 00-7.381 5.84h4.8m2.58-5.84a14.927 14.927 0 00-2.58 5.84m2.699 2.7c-.103.021-.207.041-.311.06a15.09 15.09 0 01-2.448-2.448 14.9 14.9 0 01.06-.312m-2.24 2.39a4.493 4.493 0 00-1.757 4.306 4.493 4.493 0 004.306-1.758M16.5 9a1.5 1.5 0 11-3 0 1.5 1.5 0 013 0z" /></svg>
              {{ applying ? 'Creating work items…' : applied ? 'Done!' : 'Create Work Items' }}
            </button>

            <button v-if="applied" @click="resetAll"
              class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors">
              Start Over
            </button>
          </div>
        </div>

        <!-- Results after apply -->
        <div v-if="applyResult" class="mt-4 space-y-3">
          <div v-for="item in applyResult.created" :key="item.from_story_id || item.error"
            class="rounded-lg border px-4 py-3"
            :class="item.error
              ? 'border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-900/20'
              : 'border-green-200 dark:border-green-700 bg-green-50 dark:bg-green-900/20'">
            <div v-if="item.error" class="text-sm text-red-700 dark:text-red-300">
              Failed to clone story #{{ item.from_story_id }}: {{ item.error }}
            </div>
            <div v-else>
              <div class="text-sm font-medium text-green-800 dark:text-green-200">
                Created User Story #{{ item.new_story_id }}: {{ isDemoMode ? anonPrTitle(item.title) : item.title }}
              </div>
              <div v-for="t in item.tasks" :key="t.task_id" class="text-sm text-green-700 dark:text-green-300 ml-4 mt-1">
                ↳ Task #{{ t.task_id }}: {{ isDemoMode ? anonPrTitle(t.title) : t.title }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Error -->
    <div v-if="error" class="mt-4 bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 rounded-lg px-4 py-3 text-sm text-red-800 dark:text-red-200">
      {{ error }}
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useApi } from '../composables/useApi.js'
import { transformSprintProjects } from '../composables/demoTransform.js'
import { useDemoMode, anonTeam, anonIterationPath, anonPrTitle } from '../composables/useDemoMode.js'

const api = useApi()
const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

const rawProjects = ref([])
const projects = computed(() => transformSprintProjects(rawProjects.value))
const teams = ref([])
const iterations = ref([])
const preview = ref(null)
const applyResult = ref(null)

const selectedProjectId = ref('')
const selectedSourceProjectId = ref('')
const previousTargetProjectId = ref('')
const selectedTeam = ref('')
const selectedIterationIdx = ref(-1)
const confirmed = ref(false)

const loading = ref(false)
const loadingTeams = ref(false)
const loadingIterations = ref(false)
const loadingPreview = ref(false)
const applying = ref(false)
const applied = ref(false)
const error = ref(null)

const step = computed(() => {
  if (!selectedProjectId.value) return 1
  if (!selectedTeam.value) return 2
  if (selectedIterationIdx.value < 0) return 3
  return 4
})

const effectiveSourceProjectId = computed(() => selectedSourceProjectId.value || selectedProjectId.value)

const targetProjectLabel = computed(() => {
  const project = projects.value.find(p => p.id === selectedProjectId.value)
  return project ? `${project.project} (${project.organization})` : '-'
})

const sourceProjectLabel = computed(() => {
  const project = projects.value.find(p => p.id === effectiveSourceProjectId.value)
  return project ? `${project.project} (${project.organization})` : '-'
})

const selectedSprintName = computed(() => {
  if (selectedIterationIdx.value >= 0 && iterations.value[selectedIterationIdx.value]) {
    const name = iterations.value[selectedIterationIdx.value].name
    return isDemoMode.value ? anonIterationPath(name) : name
  }
  return ''
})

const totalTasks = computed(() => {
  if (!preview.value) return 0
  return preview.value.templates.reduce((sum, t) => sum + t.task_count, 0)
})

// Load projects on mount
;(async () => {
  try {
    rawProjects.value = await api.get('/api/sprint-populator/projects')
  } catch (e) {
    error.value = e.message
  }
})()

// Auto-select when only one option is available
watch(projects, (list) => {
  if (list.length === 1 && !selectedProjectId.value) {
    selectedProjectId.value = list[0].id
    onProjectChange()
  }
})

watch(teams, (list) => {
  if (list.length === 1 && !selectedTeam.value) {
    selectedTeam.value = list[0].name
    onTeamChange()
  }
})

watch(iterations, (list) => {
  if (list.length === 1 && selectedIterationIdx.value < 0) {
    selectedIterationIdx.value = 0
    onSprintChange()
  }
})

async function onProjectChange() {
  const previousTarget = previousTargetProjectId.value

  // Reset downstream
  teams.value = []
  iterations.value = []
  preview.value = null
  applyResult.value = null
  selectedTeam.value = ''
  selectedIterationIdx.value = -1
  confirmed.value = false
  applied.value = false
  error.value = null

  if (!selectedProjectId.value) {
    selectedSourceProjectId.value = ''
    previousTargetProjectId.value = ''
    return
  }

  if (!selectedSourceProjectId.value || selectedSourceProjectId.value === previousTarget) {
    selectedSourceProjectId.value = selectedProjectId.value
  }
  previousTargetProjectId.value = selectedProjectId.value

  loadingTeams.value = true
  try {
    teams.value = await api.get(`/api/sprint-populator/${encodeURIComponent(selectedProjectId.value)}/teams`)
  } catch (e) {
    error.value = e.message
  } finally {
    loadingTeams.value = false
  }
}

async function onSourceProjectChange() {
  preview.value = null
  applyResult.value = null
  confirmed.value = false
  applied.value = false
  error.value = null

  if (!selectedSourceProjectId.value)
    selectedSourceProjectId.value = selectedProjectId.value

  if (selectedIterationIdx.value >= 0)
    await onSprintChange()
}

async function onTeamChange() {
  iterations.value = []
  preview.value = null
  applyResult.value = null
  selectedIterationIdx.value = -1
  confirmed.value = false
  applied.value = false
  error.value = null

  if (!selectedTeam.value) return

  loadingIterations.value = true
  try {
    iterations.value = await api.get(
      `/api/sprint-populator/${encodeURIComponent(selectedProjectId.value)}/iterations?team=${encodeURIComponent(selectedTeam.value)}`
    )
  } catch (e) {
    error.value = e.message
  } finally {
    loadingIterations.value = false
  }
}

async function onSprintChange() {
  preview.value = null
  applyResult.value = null
  confirmed.value = false
  applied.value = false
  error.value = null

  if (selectedIterationIdx.value < 0) return

  const sprint = iterations.value[selectedIterationIdx.value]
  loadingPreview.value = true
  try {
    preview.value = await api.post(
      `/api/sprint-populator/${encodeURIComponent(selectedProjectId.value)}/preview`,
      {
        iteration_path: sprint.path,
        sprint_name: sprint.name,
        source_project_id: effectiveSourceProjectId.value,
      }
    )
  } catch (e) {
    error.value = e.message
  } finally {
    loadingPreview.value = false
  }
}

async function applyClone() {
  if (!confirmed.value || applying.value || isDemoMode.value) return

  const sprint = iterations.value[selectedIterationIdx.value]
  applying.value = true
  error.value = null
  try {
    applyResult.value = await api.post(
      `/api/sprint-populator/${encodeURIComponent(selectedProjectId.value)}/apply`,
      {
        iteration_path: sprint.path,
        sprint_name: sprint.name,
        source_project_id: effectiveSourceProjectId.value,
      }
    )
    applied.value = true
  } catch (e) {
    error.value = e.message
  } finally {
    applying.value = false
  }
}

function resetAll() {
  selectedProjectId.value = ''
  selectedSourceProjectId.value = ''
  previousTargetProjectId.value = ''
  selectedTeam.value = ''
  selectedIterationIdx.value = -1
  teams.value = []
  iterations.value = []
  preview.value = null
  applyResult.value = null
  confirmed.value = false
  applied.value = false
  applying.value = false
  error.value = null
}
</script>
