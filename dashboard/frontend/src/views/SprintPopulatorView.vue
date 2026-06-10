<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Sprint Manager</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Create sprints and populate them with template work items.
        </p>
      </div>
    </div>

    <!-- PAT Warning -->
    <UAlert v-if="!store.patConfigured" color="warning" icon="i-heroicons-exclamation-triangle" description="PAT not configured. Set it in Settings first." class="mb-6" />

    <!-- Tabs -->
    <UTabs :items="viewTabs" v-model="activeViewTab" :content="false" variant="link" class="mb-6" />

    <!-- ==================== TAB 1: Create Sprint ==================== -->
    <template v-if="activeViewTab === 'create'">
      <!-- Project & Team selector -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Project</label>
            <USelectMenu v-model="csProjectId" :items="projectOptions" value-key="value"
              :disabled="csCreating" placeholder="Choose a project…" class="w-full" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Team</label>
            <USelectMenu v-model="csTeam" :items="csTeamOptions" value-key="value" placeholder="Choose a team…"
              :loading="csLoadingTeams" :disabled="!csProjectId || csLoadingTeams || csCreating" class="w-full" />
          </div>
        </div>
      </div>

      <!-- Existing sprints -->
      <div v-if="csIterations.length && !csLoadingIterations" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden mb-4">
        <button @click="csShowExisting = !csShowExisting"
          class="w-full px-5 py-3 flex items-center justify-between text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-queue-list" class="w-4 h-4 text-gray-400" />
            Existing sprints ({{ csIterations.length }})
          </span>
          <UIcon :name="csShowExisting ? 'i-heroicons-chevron-up' : 'i-heroicons-chevron-down'" class="w-4 h-4 text-gray-400" />
        </button>
        <div v-if="csShowExisting" class="border-t border-gray-100 dark:border-gray-700 max-h-[300px] overflow-y-auto">
          <table class="w-full text-sm">
            <thead>
              <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider bg-gray-50 dark:bg-gray-700/30">
                <th class="px-4 py-2">Name</th>
                <th class="px-4 py-2">Parent Path</th>
                <th class="px-4 py-2">Start</th>
                <th class="px-4 py-2">End</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="it in csIterations" :key="it.path"
                class="border-b border-gray-100 dark:border-gray-700 last:border-0">
                <td class="px-4 py-2 text-gray-800 dark:text-gray-200">{{ isDemoMode ? anonIterationPath(it.name) : it.name }}</td>
                <td class="px-4 py-2 text-gray-500 dark:text-gray-400">{{ isDemoMode ? anonIterationPath(iterationParentPath(it.path)) : iterationParentPath(it.path) }}</td>
                <td class="px-4 py-2 text-gray-500 dark:text-gray-400">{{ formatIterationDate(it.start_date) }}</td>
                <td class="px-4 py-2 text-gray-500 dark:text-gray-400">{{ formatIterationDate(it.finish_date) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Sprint details -->
      <div v-if="csTeam" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3">
          Sprint Details
        </h3>

        <div v-if="csLoadingIterations" class="flex items-center gap-2 text-sm text-gray-500">
          <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
          Analyzing existing sprints…
        </div>

        <div v-else class="space-y-4">
          <p v-if="csInference && csInference.suggestedName" class="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1">
            <UIcon name="i-heroicons-light-bulb" class="w-3.5 h-3.5 text-amber-500" />
            Suggested based on {{ csInference.sprintCount }} existing sprint{{ csInference.sprintCount !== 1 ? 's' : '' }}
          </p>

          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Sprint name</label>
            <UInput name="cs-name" v-model="csName" placeholder="e.g. Sprint 42" :disabled="csCreating" class="w-full max-w-md" />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Parent path</label>
            <USelectMenu v-model="csParentPath" :items="csParentPathOptions" value-key="value" :disabled="csCreating" class="w-full max-w-md" />
            <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Iteration path under which the sprint is created.</p>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4 max-w-md">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Start date</label>
              <UInput name="cs-start-date" v-model="csStartDate" type="date" :disabled="csCreating" class="w-full" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">End date</label>
              <UInput name="cs-finish-date" v-model="csFinishDate" type="date" :disabled="csCreating" class="w-full" />
            </div>
          </div>
        </div>
      </div>

      <!-- Create buttons -->
      <div v-if="csTeam && csName" class="flex items-center gap-3">
        <UButton
          @click="createSprint(false)"
          :disabled="csCreating || csCreated"
          :loading="csCreating && !csAddToTeam"
          :icon="csCreated && !csAddToTeam ? 'i-heroicons-check-circle' : 'i-heroicons-plus'"
          :label="csCreating && !csAddToTeam ? 'Creating…' : csCreated && !csAddToTeam ? 'Created!' : 'Create Sprint'"
          :color="csCreated && !csAddToTeam ? 'success' : 'primary'"
        />
        <UButton
          @click="createSprint(true)"
          :disabled="csCreating || csCreated"
          :loading="csCreating && csAddToTeam"
          :icon="csCreated && csAddToTeam ? 'i-heroicons-check-circle' : 'i-heroicons-plus'"
          :label="csCreating && csAddToTeam ? 'Creating…' : csCreated && csAddToTeam ? 'Created & Assigned!' : 'Create & Add to Team'"
          :color="csCreated && csAddToTeam ? 'success' : 'primary'"
        />
        <UButton v-if="csCreated" @click="resetCreateSprint" icon="i-heroicons-plus" variant="outline" color="neutral" label="Create another" />
      </div>

      <!-- Success result -->
      <div v-if="csResult" class="mt-4 rounded-lg border border-green-200 dark:border-green-700 bg-green-50 dark:bg-green-900/30 px-4 py-3">
        <p class="text-sm font-medium text-green-800 dark:text-green-200">
          Created iteration: {{ isDemoMode ? anonIterationPath(csResult.name) : csResult.name }}
        </p>
        <p class="text-xs text-green-700 dark:text-green-300 mt-1">
          Path: {{ isDemoMode ? anonIterationPath(csResult.iteration_path) : csResult.iteration_path }}
          <span v-if="csResult.assigned_to_team" class="ml-2">· Assigned to team</span>
          <span v-else class="ml-2 text-amber-600 dark:text-amber-400">· Could not assign to team (do it manually)</span>
        </p>
      </div>

      <UAlert v-if="csError" color="error" icon="i-heroicons-exclamation-circle" :description="csError" class="mt-4" />
    </template>

    <!-- ==================== TAB 2: Sprint Populator ==================== -->
    <template v-if="activeViewTab === 'populate'">

    <!-- Project, Team & Sprint selector -->
    <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Template Source Project</label>
          <USelectMenu autofocus v-model="selectedSourceProjectId" :items="projectOptions" value-key="value"
            :disabled="loading || applying" placeholder="Choose a project…" class="w-full" />
        </div>
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Target Project</label>
          <USelectMenu v-model="selectedProjectId" :items="projectOptions" value-key="value"
            :disabled="!selectedSourceProjectId || loading || applying" placeholder="Choose a project…" class="w-full" />
        </div>
      </div>
      <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Team</label>
          <USelectMenu v-model="selectedTeam" :items="teamOptions" value-key="value" placeholder="Choose a team…"
            :loading="loadingTeams" :disabled="!selectedProjectId || loading || applying || loadingTeams" class="w-full" />
        </div>
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Sprint</label>
          <USelectMenu v-model="selectedIterationIdx" :items="iterationOptions" value-key="value" placeholder="Choose a sprint…"
            :loading="loadingIterations" :disabled="!selectedTeam || loading || applying || loadingIterations" class="w-full" />
        </div>
      </div>
    </div>

    <!-- Preview -->
    <div v-if="step >= 4" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
      <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider mb-3">
        Preview
      </h3>

      <div v-if="loadingPreview" class="flex items-center gap-2 text-sm text-gray-500">
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
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
          class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden"
          :class="{ 'opacity-50': !selectedTemplateIds.has(tmpl.story_id) }">
          <div class="px-4 py-3 bg-gray-50 dark:bg-gray-700/50 flex items-center justify-between">
            <div class="flex items-center gap-3">
              <input type="checkbox" :checked="selectedTemplateIds.has(tmpl.story_id)" @change="toggleTemplate(tmpl.story_id)"
                class="w-4 h-4 rounded border-gray-300 dark:border-gray-600 text-primary-600 focus:ring-primary-500" />
              <div>
                <span class="text-xs text-gray-500 dark:text-gray-400">User Story #{{ tmpl.story_id }}</span>
                <span class="mx-2 text-gray-300 dark:text-gray-600">→</span>
                <span class="font-medium text-gray-800 dark:text-gray-100">{{ isDemoMode ? anonPrTitle(tmpl.final_title) : tmpl.final_title }}</span>
              </div>
            </div>
            <span class="text-xs bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 rounded-full px-2.5 py-0.5 font-medium">
              {{ tmpl.task_count }} task{{ tmpl.task_count !== 1 ? 's' : '' }}
            </span>
          </div>
          <div v-if="tmpl.original_title !== tmpl.final_title" class="px-4 py-2 text-xs text-gray-500 dark:text-gray-400 border-t border-gray-100 dark:border-gray-700">
            Original: <span class="italic">{{ isDemoMode ? anonPrTitle(tmpl.original_title) : tmpl.original_title }}</span>
          </div>
          <div v-if="tmpl.tasks.length" class="px-4 py-2 border-t border-gray-100 dark:border-gray-700">
            <div v-for="task in tmpl.tasks" :key="task.task_id"
              class="flex items-center gap-2 py-1 text-sm text-gray-600 dark:text-gray-400"
              :class="{ 'opacity-50': !isTaskSelected(tmpl.story_id, task.task_id) }">
              <input type="checkbox"
                :checked="isTaskSelected(tmpl.story_id, task.task_id)"
                :disabled="!selectedTemplateIds.has(tmpl.story_id)"
                @change="toggleTask(tmpl.story_id, task.task_id)"
                class="w-3.5 h-3.5 rounded border-gray-300 dark:border-gray-600 text-primary-600 focus:ring-primary-500 shrink-0" />
              <span class="text-xs text-gray-400 dark:text-gray-300">Task #{{ task.task_id }}:</span>
              {{ isDemoMode ? anonPrTitle(task.title) : task.title }}
            </div>
          </div>
        </div>

        <!-- Apply section -->
        <div class="mt-6 pt-4 border-t border-gray-200 dark:border-gray-700">
          <div class="flex items-center gap-3">
            <UButton
              @click="showConfirm = true"
              :disabled="applying || applied || selectedTemplateCount === 0"
              :loading="applying"
              :icon="applied ? 'i-heroicons-check-circle' : 'i-heroicons-rocket-launch'"
              :label="applying ? 'Creating work items…' : applied ? 'Done!' : `Create Work Items (${selectedTemplateCount})`"
              :color="applied ? 'success' : 'primary'"
            />

            <UButton v-if="applied" @click="resetAll" variant="outline" color="neutral" label="Reset overview" />
          </div>

          <UModal :open="showConfirm" @update:open="v => { if (!v) showConfirm = false }" title="Are you sure?" description="Confirm creating work items">
            <template #body>
              <p class="text-sm text-gray-700 dark:text-gray-300">
                This will create <strong>{{ selectedTemplateCount }} user stor{{ selectedTemplateCount !== 1 ? 'ies' : 'y' }}</strong>
                and <strong>{{ totalTasks }} task{{ totalTasks !== 1 ? 's' : '' }}</strong>
                in sprint <strong>{{ selectedSprintName }}</strong>.
                <span class="text-red-500 font-medium">This action cannot be undone.</span>
              </p>
              <p v-if="isDemoMode" class="text-sm text-amber-600 dark:text-amber-400 mt-2">Disabled in demo mode</p>
            </template>
            <template #footer>
              <div class="flex justify-end gap-2">
                <UButton variant="outline" color="neutral" label="Cancel" @click="showConfirm = false" />
                <UButton label="Yes, create work items" :disabled="isDemoMode" @click="showConfirm = false; applyClone()" />
              </div>
            </template>
          </UModal>
        </div>

        <!-- Results after apply -->
        <div v-if="applyResult" class="mt-4 space-y-3">
          <div v-for="item in applyResult.created" :key="item.from_story_id || item.error"
            class="rounded-lg border px-4 py-3"
            :class="item.error
              ? 'border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-900/30'
              : 'border-green-200 dark:border-green-700 bg-green-50 dark:bg-green-900/30'">
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
    <UAlert v-if="error" color="error" icon="i-heroicons-exclamation-triangle" :description="error" class="mt-4" />
    </template>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useApi } from '../composables/useApi.js'
import { transformSprintProjects } from '../composables/demoTransform.js'
import { useDemoMode, anonTeam, anonIterationPath, anonPrTitle } from '../composables/useDemoMode.js'
import { inferNextSprint } from '../composables/useSprintInference.js'
const api = useApi()
const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

// ---------- Tab state ----------
const viewTabs = [
  { label: 'Sprint Creator', value: 'create', icon: 'i-heroicons-plus' },
  { label: 'Sprint Populator', value: 'populate', icon: 'i-heroicons-clipboard-document-list' },
]
const activeViewTab = ref('create')

// When switching to populate tab, sync project/team from creator and refresh iterations
watch(activeViewTab, async (tab) => {
  if (tab === 'populate') {
    // Sync project selection from creator if populator hasn't been configured yet
    if (csProjectId.value && !selectedProjectId.value) {
      selectedSourceProjectId.value = csProjectId.value
      selectedProjectId.value = csProjectId.value
    }
    // Always refresh iterations if team is selected
    if (selectedTeam.value && selectedProjectId.value) {
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
  }
})

const rawProjects = ref([])
const projects = computed(() => transformSprintProjects(rawProjects.value))
const teams = ref([])
const iterations = ref([])
const preview = ref(null)
const applyResult = ref(null)
const selectedTemplateIds = ref(new Set())
const excludedTaskIds = ref(new Set())

const selectedProjectId = ref('')
const selectedSourceProjectId = ref('')
const previousSourceProjectId = ref('')
const selectedTeam = ref('')
const selectedIterationIdx = ref(null)
const showConfirm = ref(false)

const loading = ref(false)
const loadingTeams = ref(false)
const loadingIterations = ref(false)
const loadingPreview = ref(false)
const applying = ref(false)
const applied = ref(false)
const error = ref(null)

const projectOptions = computed(() =>
  projects.value.map(p => ({ value: p.id, label: `${p.project} (${p.organization})` }))
)
const teamOptions = computed(() =>
  teams.value.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
)
const iterationOptions = computed(() =>
  iterations.value.map((it, idx) => ({ value: idx, label: isDemoMode.value ? anonIterationPath(it.name) : it.name }))
)

const step = computed(() => {
  if (!selectedSourceProjectId.value || !selectedProjectId.value) return 1
  if (!selectedTeam.value) return 2
  if (selectedIterationIdx.value == null) return 3
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
  if (selectedIterationIdx.value != null && iterations.value[selectedIterationIdx.value]) {
    const name = iterations.value[selectedIterationIdx.value].name
    return isDemoMode.value ? anonIterationPath(name) : name
  }
  return ''
})

const totalTasks = computed(() => {
  if (!preview.value) return 0
  return preview.value.templates
    .filter(t => selectedTemplateIds.value.has(t.story_id))
    .reduce((sum, t) => sum + t.tasks.filter(task => !excludedTaskIds.value.has(task.task_id)).length, 0)
})

const selectedTemplateCount = computed(() => {
  if (!preview.value) return 0
  return preview.value.templates.filter(t => selectedTemplateIds.value.has(t.story_id)).length
})

function toggleTemplate(storyId) {
  const s = new Set(selectedTemplateIds.value)
  if (s.has(storyId)) s.delete(storyId)
  else s.add(storyId)
  selectedTemplateIds.value = s
}

function isTaskSelected(storyId, taskId) {
  return selectedTemplateIds.value.has(storyId) && !excludedTaskIds.value.has(taskId)
}

function toggleTask(storyId, taskId) {
  if (!selectedTemplateIds.value.has(storyId)) return
  const s = new Set(excludedTaskIds.value)
  if (s.has(taskId)) s.delete(taskId)
  else s.add(taskId)
  excludedTaskIds.value = s
}

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
  if (list.length === 1 && !selectedSourceProjectId.value) {
    selectedSourceProjectId.value = list[0].id
  }
})

watch(teams, (list) => {
  if (list.length === 1 && !selectedTeam.value) {
    selectedTeam.value = list[0].name
  }
})

watch(iterations, (list) => {
  if (list.length === 1 && selectedIterationIdx.value == null) {
    selectedIterationIdx.value = 0
  }
})

// React to selection changes (watch guarantees ref is updated before callback)
watch(selectedSourceProjectId, () => onSourceProjectChange())
watch(selectedProjectId, () => onProjectChange())
watch(selectedTeam, () => onTeamChange())
watch(selectedIterationIdx, () => onSprintChange())

async function onProjectChange() {
  teams.value = []
  iterations.value = []
  preview.value = null
  applyResult.value = null
  selectedTeam.value = ''
  selectedIterationIdx.value = null
  showConfirm.value = false
  applied.value = false
  error.value = null

  if (!selectedProjectId.value) {
    selectedProjectId.value = selectedSourceProjectId.value
    if (!selectedProjectId.value) return
  }

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
  const previousSource = previousSourceProjectId.value

  teams.value = []
  iterations.value = []
  preview.value = null
  applyResult.value = null
  selectedTeam.value = ''
  selectedIterationIdx.value = null
  showConfirm.value = false
  applied.value = false
  error.value = null

  if (!selectedSourceProjectId.value) {
    selectedProjectId.value = ''
    previousSourceProjectId.value = ''
    return
  }

  if (!selectedProjectId.value || selectedProjectId.value === previousSource) {
    selectedProjectId.value = selectedSourceProjectId.value
  }
  previousSourceProjectId.value = selectedSourceProjectId.value

  loadingTeams.value = true
  try {
    teams.value = await api.get(`/api/sprint-populator/${encodeURIComponent(selectedProjectId.value)}/teams`)
  } catch (e) {
    error.value = e.message
  } finally {
    loadingTeams.value = false
  }
}

async function onTeamChange() {
  iterations.value = []
  preview.value = null
  applyResult.value = null
  selectedIterationIdx.value = null
  showConfirm.value = false
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
  showConfirm.value = false
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
    // Select all templates and tasks by default
    selectedTemplateIds.value = new Set(preview.value?.templates?.map(t => t.story_id) || [])
    excludedTaskIds.value = new Set()
  } catch (e) {
    error.value = e.message
  } finally {
    loadingPreview.value = false
  }
}

async function applyClone() {
  if (applying.value || isDemoMode.value) return

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
        template_ids: [...selectedTemplateIds.value],
        excluded_task_ids: [...excludedTaskIds.value],
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
  previousSourceProjectId.value = ''
  selectedTeam.value = ''
  selectedIterationIdx.value = null
  teams.value = []
  iterations.value = []
  preview.value = null
  applyResult.value = null
  showConfirm.value = false
  applied.value = false
  applying.value = false
  error.value = null
}

// ---------- Sprint Creator (Tab 1) ----------
const csProjectId = ref('')
const csTeam = ref('')
const csTeams = ref([])
const csIterations = ref([])
const csLoadingTeams = ref(false)
const csLoadingIterations = ref(false)
const csName = ref('')
const csParentPath = ref('root')
const csStartDate = ref('')
const csFinishDate = ref('')
const csCreating = ref(false)
const csCreated = ref(false)
const csResult = ref(null)
const csError = ref(null)
const csInference = ref(null)
const csShowExisting = ref(false)
const csAddToTeam = ref(false)

function formatIterationDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  return d.toLocaleDateString('nl-NL', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function iterationParentPath(path) {
  if (!path) return '—'
  const segments = path.split('\\').filter(Boolean)
  if (segments.length <= 2) return '(root)'
  return segments.slice(1, -1).join('\\')
}

const csTeamOptions = computed(() =>
  csTeams.value.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
)

const csIterationPaths = ref([])
const csParentPathOptions = computed(() => [
  { value: 'root', label: '(root)' },
  ...csIterationPaths.value.map(p => ({ value: p, label: p })),
])

watch(csProjectId, async (pid) => {
  csTeam.value = ''
  csTeams.value = []
  csIterations.value = []
  csIterationPaths.value = []
  csInference.value = null
  csResult.value = null
  csError.value = null
  csCreated.value = false
  if (!pid) return

  csLoadingTeams.value = true
  try {
    const [teams, paths] = await Promise.all([
      api.get(`/api/sprint-populator/${encodeURIComponent(pid)}/teams`),
      api.get(`/api/sprint-populator/${encodeURIComponent(pid)}/iteration-paths`),
    ])
    csTeams.value = teams
    csIterationPaths.value = paths
    if (csTeams.value.length === 1) csTeam.value = csTeams.value[0].name
  } catch (e) {
    csError.value = e.message
  } finally {
    csLoadingTeams.value = false
  }
})

watch(csTeam, async (team) => {
  csIterations.value = []
  csInference.value = null
  csName.value = ''
  csParentPath.value = 'root'
  csStartDate.value = ''
  csFinishDate.value = ''
  csCreated.value = false
  csResult.value = null
  csError.value = null
  if (!team || !csProjectId.value) return

  csLoadingIterations.value = true
  try {
    csIterations.value = await api.get(
      `/api/sprint-populator/${encodeURIComponent(csProjectId.value)}/iterations?team=${encodeURIComponent(team)}`
    )
    const suggestion = inferNextSprint(csIterations.value)
    csInference.value = suggestion
    if (suggestion.suggestedName) csName.value = suggestion.suggestedName
    csParentPath.value = suggestion.suggestedParentPath || 'root'
    if (suggestion.suggestedStartDate) csStartDate.value = suggestion.suggestedStartDate
    if (suggestion.suggestedFinishDate) csFinishDate.value = suggestion.suggestedFinishDate
  } catch (e) {
    csError.value = e.message
  } finally {
    csLoadingIterations.value = false
  }
})

async function createSprint(addToTeam) {
  if (csCreating.value || isDemoMode.value) return
  csAddToTeam.value = addToTeam
  csCreating.value = true
  csError.value = null
  csResult.value = null
  try {
    csResult.value = await api.post(
      `/api/sprint-populator/${encodeURIComponent(csProjectId.value)}/create-sprint`,
      {
        name: csName.value,
        team: addToTeam ? csTeam.value : null,
        parent_path: csParentPath.value && csParentPath.value !== 'root' ? csParentPath.value : null,
        start_date: csStartDate.value || null,
        finish_date: csFinishDate.value || null,
      }
    )
    csCreated.value = true
    // Invalidate cached iterations so the populator tab picks up the new sprint
    api.invalidate(`/api/sprint-populator/${encodeURIComponent(csProjectId.value)}/iterations`)
  } catch (e) {
    csError.value = e.message
  } finally {
    csCreating.value = false
  }
}

function resetCreateSprint() {
  csCreated.value = false
  csResult.value = null
  csError.value = null
  // Re-run inference to prefill next sprint
  if (csIterations.value.length >= 2) {
    const suggestion = inferNextSprint(csIterations.value)
    csInference.value = suggestion
    csName.value = suggestion.suggestedName || ''
    csParentPath.value = suggestion.suggestedParentPath || 'root'
    csStartDate.value = suggestion.suggestedStartDate || ''
    csFinishDate.value = suggestion.suggestedFinishDate || ''
  } else {
    csName.value = ''
    csParentPath.value = 'root'
    csStartDate.value = ''
    csFinishDate.value = ''
  }
}
</script>
