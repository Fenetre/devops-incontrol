<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Manage Projects</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">Configure which Azure DevOps projects and checks to monitor.</p>
      </div>

      <UButton icon="i-heroicons-plus" @click="showForm = !showForm">
        {{ showForm ? 'Cancel' : 'Add Project' }}
      </UButton>
    </div>

    <!-- Add/Edit form -->
    <div v-if="showForm" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-6">
        <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700">
          <h3 class="font-semibold text-gray-900 dark:text-gray-100">{{ editing ? 'Edit Project' : 'Add New Project' }}</h3>
        </div>

        <div class="px-6 py-5 space-y-4">
          <!-- Organization & Project -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Organization</label>
              <div v-if="!store.patConfigured" class="text-xs text-amber-600 dark:text-amber-400 mb-1">Set a PAT in Settings first.</div>
              <div class="relative">
                <UInput
                  v-autofocus
                  name="organization" v-model="form.organization"
                  :list="store.organizations.length > 1 ? 'known-orgs' : undefined"
                  placeholder="e.g. MyOrganization"
                  :disabled="!store.patConfigured || store.organizations.length === 1"
                  @change="onOrgChange"
                  @blur="onOrgChange"
                  class="w-full"
                />
                <datalist v-if="store.organizations.length > 1" id="known-orgs">
                  <option v-for="o in store.organizations" :key="o.name" :value="o.name" />
                </datalist>
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
              <div v-if="store.orgProjectsError" class="text-xs text-red-600 dark:text-red-400 mb-1">{{ store.orgProjectsError }}</div>
              <USelectMenu
                v-model="form.project"
                :items="projectOptions"
                value-key="value"
                :placeholder="projectPlaceholder"
                :loading="store.loadingOrgProjects"
                :disabled="!form.organization || store.loadingOrgProjects || store.orgProjects.length === 0"
                class="w-full"
              />
            </div>
          </div>

          <!-- Area path -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Area Path <span class="text-gray-500 font-normal">(optional)</span></label>
              <USelectMenu
                v-model="form.area_path"
                :items="areaPathOptions"
                value-key="value"
                :loading="store.loadingAreaPaths"
                :disabled="!form.project || store.loadingAreaPaths"
                placeholder="All areas (no filter)"
                class="w-full"
              />
              <UCheckbox v-if="form.area_path" v-model="form.include_child_areas" label="Include child areas" class="mt-2" />
            </div>
          </div>

          <!-- Checks -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Enabled Checks</label>
            <div class="space-y-2">
              <div
                v-for="ct in store.checkTypes"
                :key="ct.type_key"
                class="border border-gray-200 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
              >
                <label class="flex items-start gap-3 p-3 cursor-pointer">
                  <UCheckbox
                    :model-value="selectedChecks.includes(ct.type_key)"
                    @update:model-value="v => toggleCheck(ct.type_key, v)"
                    class="mt-0.5"
                  />
                  <div class="flex-1">
                    <div class="flex items-center justify-between">
                      <span class="text-sm font-medium text-gray-800 dark:text-gray-200">{{ ct.label }}</span>
                      <UButton
                        v-if="selectedChecks.includes(ct.type_key) && hasCheckOptions(ct.type_key)"
                        @click.prevent.stop="toggleCheckOptions(ct.type_key)"
                        variant="ghost" color="neutral" size="xs"
                      >
                        <UIcon name="i-heroicons-chevron-right" class="w-3.5 h-3.5 transition-transform" :class="expandedChecks[ct.type_key] ? 'rotate-90' : ''" />
                        Options
                      </UButton>
                    </div>
                    <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">{{ ct.description }}</p>
                    <!-- Collapsible per-check options -->
                    <div v-if="selectedChecks.includes(ct.type_key) && expandedChecks[ct.type_key]" class="mt-3 pl-1 border-l-2 border-primary-200 dark:border-primary-800 space-y-3" @click.stop>
                      <!-- Repository filter -->
                      <div v-if="['release_pr_check', 'pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check'].includes(ct.type_key)">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Repository name <span class="text-gray-500">(leave empty to check all repos)</span></label>
                        <USelectMenu
                          :modelValue="checkRepositories[ct.type_key] || ''"
                          @update:modelValue="checkRepositories[ct.type_key] = $event"
                          :items="repoFilterOptions"
                          value-key="value"
                          :loading="store.loadingRepos"
                          :disabled="!form.project || store.loadingRepos"
                          placeholder="All repositories"
                          size="sm"
                          class="w-full mt-1"
                        />
                      </div>
                      <!-- Stale days -->
                      <div v-if="ct.type_key === 'stale_pr_check'">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Inactive days threshold</label>
                        <UInput
                          name="stale-days"
                          :model-value="checkStaleDays[ct.type_key] || 14"
                          @update:model-value="checkStaleDays[ct.type_key] = parseInt($event) || 14"
                          type="number"
                          size="xs"
                          placeholder="14"
                          class="w-20 mt-1"
                        />
                      </div>
                      <!-- Ignore reviewers -->
                      <div v-if="ct.type_key === 'unreviewed_pr_check'">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Ignore reviewers <span class="text-gray-500">(comma-separated, substring match)</span></label>
                        <UInput
                          name="field-input"
                          :model-value="checkIgnoreReviewers[ct.type_key] || ''"
                          @update:model-value="checkIgnoreReviewers[ct.type_key] = $event"
                          size="xs"
                          placeholder="e.g. Build Service, Project Collection"
                          class="w-full mt-1"
                        />
                      </div>
                      <!-- Estimate mode -->
                      <div v-if="ct.type_key === 'missing_estimate_check'">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Check for missing</label>
                        <URadioGroup
                          :model-value="checkEstimateMode[ct.type_key] || 'both'"
                          @update:model-value="checkEstimateMode[ct.type_key] = $event"
                          :items="estimateModeOptions"
                          size="sm"
                          class="mt-1.5"
                        />
                      </div>
                      <!-- Parent type mappings -->
                      <div v-if="ct.type_key === 'orphan_check'">
                        <div class="flex items-center gap-2">
                          <label class="text-xs text-gray-600 dark:text-gray-400">Parent type mappings</label>
                          <UButton
                            v-if="editing && form.project && !loadingParentHierarchy"
                            @click.stop="loadParentHierarchy"
                            variant="link" size="xs"
                          >Load from process</UButton>
                          <UIcon v-if="loadingParentHierarchy" name="i-heroicons-arrow-path" class="w-3 h-3 animate-spin text-gray-400 dark:text-gray-300" />
                        </div>
                        <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5 mb-2">Maps child type → allowed parent types. Leave empty to use process defaults at runtime.</p>
                        <div class="space-y-2">
                          <div v-for="(parents, childType) in checkParentMappings" :key="childType" class="flex items-center gap-2">
                            <span class="text-xs font-medium text-gray-700 dark:text-gray-300 w-28 shrink-0">{{ childType }}</span>
                            <UInput
                              name="parents"
                              :model-value="parents.join(', ')"
                              @change="updateParentMapping(childType, $event)"
                              size="xs"
                              placeholder="e.g. Feature, Epic"
                              class="flex-1"
                            />
                            <UButton type="button" icon="i-heroicons-x-mark" size="xs" variant="ghost" color="neutral" @click.stop="removeParentMapping(childType)" class="text-gray-400 dark:text-gray-300 hover:text-red-500" />
                          </div>
                        </div>
                        <div class="flex items-center gap-2 mt-2">
                          <UInput
                            name="new-mapping-child-type" v-model="newMappingChildType"
                            size="xs"
                            placeholder="Child type"
                            class="w-28"
                            @keydown.enter.prevent="addParentMapping"
                          />
                          <UInput
                            name="new-mapping-parent-types" v-model="newMappingParentTypes"
                            size="xs"
                            placeholder="Parent types (comma-separated)"
                            class="flex-1"
                            @keydown.enter.prevent="addParentMapping"
                          />
                          <UButton type="button" size="xs" variant="outline" @click.stop="addParentMapping" :disabled="!newMappingChildType.trim()">
                            Add
                          </UButton>
                        </div>
                      </div>
                    </div>
                  </div>
                </label>
              </div>
            </div>
          </div>

          <!-- Ignore patterns -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Ignore Title Contains <span class="text-gray-500 font-normal">(comma-separated)</span></label>
              <UInput name="ignore-titles" v-model="ignoreTitles" placeholder="In doc verwerken, FO change" class="w-full" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Ignore Parent Title Contains <span class="text-gray-500 font-normal">(comma-separated)</span></label>
              <UInput name="ignore-parent-titles" v-model="ignoreParentTitles" placeholder="Bugs to be discussed" class="w-full" />
            </div>
          </div>
        </div>

        <!-- Form footer -->
        <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
          <UButton variant="outline" color="neutral" @click="resetForm">
            Cancel
          </UButton>
          <UButton icon="i-heroicons-document-arrow-down" @click="saveProject" :disabled="!form.organization || !form.project">
            {{ editing ? 'Update' : 'Add Project' }}
          </UButton>
        </div>
      </div>

    <!-- Project list -->
    <div class="space-y-3">
      <EmptyState v-if="store.displayProjects.length === 0" icon="folder" message="No projects configured. Click &quot;Add Project&quot; above to get started." />

      <div
        v-for="p in store.displayProjects"
        :key="p.id"
        class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-5 py-4 flex items-center justify-between hover:shadow-md transition-shadow"
      >
        <div>
          <div class="flex items-center gap-2">
            <h3 class="font-semibold text-gray-900 dark:text-gray-100">{{ p.project }}</h3>
            <span class="text-xs text-gray-600 dark:text-gray-400">{{ p.organization }}</span>
          </div>
          <div class="flex flex-wrap gap-1.5 mt-2">
            <UBadge
              v-for="c in p.checks.filter(ch => ch.enabled)"
              :key="c.check_type"
              variant="subtle"
              size="sm"
            >
              {{ checkLabel(c.check_type) }}
            </UBadge>
            <span v-if="p.area_path" class="inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-gray-100 dark:bg-gray-700/50 text-gray-600 dark:text-gray-300">
              {{ p.area_path }}
            </span>
          </div>
        </div>

        <div class="flex items-center gap-2 shrink-0">
          <UButton icon="i-heroicons-pencil-square" size="sm" variant="ghost" color="neutral" title="Edit" @click="editProject(p)" />
          <template v-if="confirmingDeleteId === p.id">
            <span class="text-xs text-red-600 dark:text-red-400">Remove?</span>
            <UButton size="xs" color="error" @click="removeProject(p.id)">Yes</UButton>
            <UButton size="xs" variant="outline" color="neutral" @click="confirmingDeleteId = null">Cancel</UButton>
          </template>
          <UButton v-else icon="i-heroicons-trash" size="sm" variant="ghost" color="neutral" title="Delete" @click="removeProject(p.id)" class="hover:text-red-600" />
        </div>
      </div>
    </div>

    <!-- DB Projects Section -->
    <div class="mt-10">
      <div class="flex items-center justify-between mb-6">
        <div>
          <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Database Projects</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">Track database project names for monitoring.</p>
        </div>

        <UButton icon="i-heroicons-plus" color="info" @click="showDbForm = !showDbForm">
          {{ showDbForm ? 'Cancel' : 'Add DB Project' }}
        </UButton>
      </div>

      <!-- Add/Edit DB project form -->
      <div v-if="showDbForm" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-6">
        <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700">
          <h3 class="font-semibold text-gray-900 dark:text-gray-100">{{ editingDb ? 'Edit DB Project' : 'Add New DB Project' }}</h3>
        </div>
        <div class="px-6 py-5">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project Name</label>
              <UInput name="name" v-model="dbForm.name" placeholder="e.g. Customer Portal" class="w-full" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Database Name Filter</label>
              <UInput name="namefilter" v-model="dbForm.name_filter" placeholder="e.g. CustomerPortal" class="w-full" />
              <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">Filters databases whose name contains this text (case-insensitive). Leave empty to show all.</p>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Database Server</label>
              <USelectMenu
                v-model="dbForm.db_server_index"
                :items="dbServerOptions"
                value-key="value"
                placeholder="Select server…"
                class="w-full"
              />
            </div>
          </div>
          <div class="mt-4">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Allowlist <span class="text-gray-500 font-normal">(always green)</span></label>
            <UTextarea
              v-model="dbForm.db_allowlist"
              :rows="2"
              placeholder="One database name per line"
              class="w-full font-mono"
            />
            <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">Databases listed here will always be marked as OK, regardless of ticket status.</p>
          </div>
        </div>
        <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
          <UButton variant="outline" color="neutral" @click="resetDbForm">
            Cancel
          </UButton>
          <UButton color="info" icon="i-heroicons-document-arrow-down" @click="saveDbProject" :disabled="!dbForm.name.trim()">
            {{ editingDb ? 'Update' : 'Add' }}
          </UButton>
        </div>
      </div>

      <!-- DB project list -->
      <div class="space-y-3">
        <EmptyState v-if="store.displayDbProjects.length === 0 && !showDbForm" icon="database" message="No database projects yet. Click &quot;Add DB Project&quot; above to get started." />

        <div
          v-for="db in store.displayDbProjects"
          :key="db.id"
          class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-5 py-4 flex items-center justify-between hover:shadow-md transition-shadow"
        >
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center shrink-0">
              <UIcon name="i-heroicons-circle-stack" class="w-4 h-4 text-indigo-500" />
            </div>
            <div>
              <span class="font-semibold text-gray-800 dark:text-gray-100">{{ db.name }}</span>
              <span v-if="db.name_filter" class="ml-2 inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-indigo-50 dark:bg-indigo-900/30 text-indigo-700 dark:text-indigo-400">
                filter: {{ db.name_filter }}
              </span>
              <span class="ml-2 inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400">
                Server #{{ (db.db_server_index ?? 0) + 1 }}{{ store.dbServers[db.db_server_index ?? 0]?.server ? ` — ${store.dbServers[db.db_server_index ?? 0].server}` : '' }}
              </span>
            </div>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <UButton icon="i-heroicons-pencil-square" size="sm" variant="ghost" color="neutral" title="Edit" @click="editDbProject(db)" />
            <template v-if="confirmingDbDeleteId === db.id">
              <span class="text-xs text-red-600 dark:text-red-400">Remove?</span>
              <UButton size="xs" color="error" @click="removeDbProject(db.id)">Yes</UButton>
              <UButton size="xs" variant="outline" color="neutral" @click="confirmingDbDeleteId = null">Cancel</UButton>
            </template>
            <UButton v-else icon="i-heroicons-trash" size="sm" variant="ghost" color="neutral" title="Delete" @click="removeDbProject(db.id)" class="hover:text-red-600" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import EmptyState from '../components/EmptyState.vue'

const store = useMonitorStore()

const showForm = ref(false)
const editing = ref(null)  // project id or null

// DB project form state
const showDbForm = ref(false)
const editingDb = ref(null)
const dbForm = reactive({ name: '', name_filter: '', db_server_index: 0, db_allowlist: '' })
const confirmingDeleteId = ref(null)   // project id pending delete confirmation
const confirmingDbDeleteId = ref(null) // db-project id pending delete confirmation

const form = reactive({
  organization: '',
  project: '',
  area_path: '',
  include_child_areas: true,
})
const selectedChecks = ref([])
function toggleCheck(key, checked) {
  const idx = selectedChecks.value.indexOf(key)
  if (checked && idx === -1) selectedChecks.value.push(key)
  else if (!checked && idx !== -1) selectedChecks.value.splice(idx, 1)
}
const checkApiVersions = reactive({})  // { check_type: "7.1" }
const checkRepositories = reactive({})  // { check_type: "repo-name" }
const checkStaleDays = reactive({})  // { check_type: 14 }
const checkIgnoreReviewers = reactive({})  // { check_type: "name1, name2" }
const checkEstimateMode = reactive({})  // { check_type: "both" | "original_estimate" | "remaining_work" }
const estimateModeOptions = [
  { label: 'Both Original Estimate & Remaining Work', value: 'both' },
  { label: 'Original Estimate only', value: 'original_estimate' },
  { label: 'Remaining Work only', value: 'remaining_work' },
]
const expandedChecks = reactive({})  // { check_type: true/false }
const ignoreTitles = ref('')
const ignoreParentTitles = ref('')

const projectOptions = computed(() =>
  store.orgProjects.map(p => ({ value: p.name, label: p.name }))
)
const projectPlaceholder = computed(() =>
  store.loadingOrgProjects ? 'Loading projects…'
    : store.orgProjectsError ? 'Failed — check org name & PAT'
    : form.organization ? 'Select project…'
    : 'Enter organization first'
)
const areaPathOptions = computed(() =>
  store.areaPaths.map(a => ({ value: a.path, label: a.path }))
)
const repoFilterOptions = computed(() =>
  store.repos.map(r => ({ value: r.name, label: r.name }))
)
const dbServerOptions = computed(() =>
  [0, 1, 2].map(i => ({
    value: i,
    label: `Server #${i + 1}${store.dbServers[i]?.configured ? ` — ${store.dbServers[i].server}` : ' (not configured)'}`
  }))
)

const _CHECKS_WITH_OPTIONS = new Set([
  'release_pr_check', 'pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check', 'missing_estimate_check', 'orphan_check'
])
function hasCheckOptions(typeKey) {
  return _CHECKS_WITH_OPTIONS.has(typeKey)
}

const checkParentMappings = reactive({})
const loadingParentHierarchy = ref(false)
const newMappingChildType = ref('')
const newMappingParentTypes = ref('')

async function loadParentHierarchy() {
  if (!editing.value) return
  loadingParentHierarchy.value = true
  try {
    const res = await store.fetchParentTypeHierarchy(editing.value)
    Object.keys(checkParentMappings).forEach(k => delete checkParentMappings[k])
    if (res.hierarchy) {
      for (const [child, parents] of Object.entries(res.hierarchy)) {
        checkParentMappings[child] = [...parents]
      }
    }
  } catch (e) {
    store._toast('Failed to load hierarchy: ' + (e.message || 'Unknown error'), 'error')
  } finally {
    loadingParentHierarchy.value = false
  }
}

function updateParentMapping(childType, value) {
  checkParentMappings[childType] = value.split(',').map(s => s.trim()).filter(Boolean)
}

function removeParentMapping(childType) {
  delete checkParentMappings[childType]
}

function addParentMapping() {
  const child = newMappingChildType.value.trim()
  if (!child) return
  checkParentMappings[child] = newMappingParentTypes.value.split(',').map(s => s.trim()).filter(Boolean)
  newMappingChildType.value = ''
  newMappingParentTypes.value = ''
}
function toggleCheckOptions(typeKey) {
  expandedChecks[typeKey] = !expandedChecks[typeKey]
}

// Load known org names for autocomplete
watch(showForm, (open) => {
  if (open) store.fetchKnownOrganizations()
})

// Auto-fill organization when only one is known
watch(() => store.organizations, (orgs) => {
  if (orgs.length === 1 && !form.organization && !editing.value) {
    form.organization = orgs[0].name
    onOrgChange()
  }
})

// Auto-select project when only one available
watch(() => store.orgProjects, (projs) => {
  if (projs.length === 1 && !form.project) {
    form.project = projs[0].name
    onProjectChange()
  }
})

// Auto-select area path when only one available
watch(() => store.areaPaths, (paths) => {
  if (paths.length === 1 && !form.area_path) {
    form.area_path = paths[0].path
  }
})

// Auto-select repo for PR checks when only one available
watch(() => store.repos, (repos) => {
  if (repos.length === 1) {
    for (const ct of ['release_pr_check', 'pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check']) {
      if (selectedChecks.value.includes(ct) && !checkRepositories[ct]) {
        checkRepositories[ct] = repos[0].name
      }
    }
  }
})

// Auto-load parent type hierarchy when orphan_check is toggled on and no mappings exist
watch(selectedChecks, (checks, oldChecks) => {
  if (checks.includes('orphan_check') && !(oldChecks || []).includes('orphan_check') &&
      Object.keys(checkParentMappings).length === 0 && editing.value) {
    loadParentHierarchy()
  }
})

onMounted(() => {
  store.fetchKnownOrganizations()
  store.fetchDbProjects()
  store.fetchDbCredentialsStatus()
})

let orgDebounce = null
async function onOrgChange() {
  form.project = ''
  form.area_path = ''
  store.orgProjects = []
  store.orgProjectsError = null
  store.areaPaths = []
  store.repos = []
  clearTimeout(orgDebounce)
  if (!form.organization.trim()) return
  orgDebounce = setTimeout(async () => {
    await store.fetchOrgProjects(form.organization.trim())
  }, 300)
}

function onProjectChange() {
  form.area_path = ''
  store.areaPaths = []
  store.repos = []
  if (form.organization && form.project) {
    store.fetchAreaPaths(form.organization.trim(), form.project)
    store.fetchRepos(form.organization.trim(), form.project)
  }
}

watch(() => form.project, () => onProjectChange())

function checkLabel(checkType) {
  const ct = store.checkTypes.find(c => c.type_key === checkType)
  return ct ? ct.label : checkType
}

function resetForm() {
  form.organization = ''
  form.project = ''
  form.area_path = ''
  form.include_child_areas = true
  selectedChecks.value = []
  Object.keys(checkApiVersions).forEach(k => delete checkApiVersions[k])
  Object.keys(checkRepositories).forEach(k => delete checkRepositories[k])
  Object.keys(checkStaleDays).forEach(k => delete checkStaleDays[k])
  Object.keys(checkIgnoreReviewers).forEach(k => delete checkIgnoreReviewers[k])
  Object.keys(checkEstimateMode).forEach(k => delete checkEstimateMode[k])
  Object.keys(checkParentMappings).forEach(k => delete checkParentMappings[k])
  ignoreTitles.value = ''
  ignoreParentTitles.value = ''
  editing.value = null
  showForm.value = false
}

function editProject(p) {
  editing.value = p.id
  form.organization = p.organization
  form.project = p.project
  form.area_path = p.area_path
  form.include_child_areas = p.include_child_areas ?? true
  selectedChecks.value = p.checks.filter(c => c.enabled).map(c => c.check_type)
  // Populate per-check api versions
  Object.keys(checkApiVersions).forEach(k => delete checkApiVersions[k])
  Object.keys(checkRepositories).forEach(k => delete checkRepositories[k])
  Object.keys(checkStaleDays).forEach(k => delete checkStaleDays[k])
  Object.keys(checkIgnoreReviewers).forEach(k => delete checkIgnoreReviewers[k])
  Object.keys(checkEstimateMode).forEach(k => delete checkEstimateMode[k])
  Object.keys(checkParentMappings).forEach(k => delete checkParentMappings[k])
  for (const c of p.checks) {
    if (c.enabled) {
      checkApiVersions[c.check_type] = c.api_version || '7.1'
      if (c.repository) checkRepositories[c.check_type] = c.repository
      if (c.stale_days) checkStaleDays[c.check_type] = c.stale_days
      if (c.ignore_reviewers && c.ignore_reviewers.length) checkIgnoreReviewers[c.check_type] = c.ignore_reviewers.join(', ')
      if (c.estimate_mode) checkEstimateMode[c.check_type] = c.estimate_mode
      if (c.parent_type_mappings && Object.keys(c.parent_type_mappings).length > 0) {
        for (const [child, parents] of Object.entries(c.parent_type_mappings)) {
          checkParentMappings[child] = [...parents]
        }
      }
    }
  }
  ignoreTitles.value = (p.ignore_title_contains || []).join(', ')
  ignoreParentTitles.value = (p.ignore_parent_title_contains || []).join(', ')
  showForm.value = true
  // Auto-load parent hierarchy if orphan_check is enabled but no mappings configured
  if (selectedChecks.value.includes('orphan_check') && Object.keys(checkParentMappings).length === 0) {
    loadParentHierarchy()
  }
  // Load projects and areas for the existing org/project
  if (p.organization) {
    const savedProject = p.project
    const savedArea = p.area_path
    store.fetchOrgProjects(p.organization).then(() => {
      form.project = savedProject
      if (savedProject) {
        store.fetchAreaPaths(p.organization, savedProject).then(() => {
          form.area_path = savedArea
        })
        store.fetchRepos(p.organization, savedProject)
      }
    })
  }
}

function buildPayload() {
  return {
    organization: form.organization,
    project: form.project,
    area_path: form.area_path,
    include_child_areas: form.include_child_areas,
    ignore_title_contains: ignoreTitles.value ? ignoreTitles.value.split(',').map(s => s.trim()).filter(Boolean) : [],
    ignore_parent_title_contains: ignoreParentTitles.value ? ignoreParentTitles.value.split(',').map(s => s.trim()).filter(Boolean) : [],
    checks: selectedChecks.value.map(ct => ({
      check_type: ct,
      enabled: true,
      api_version: checkApiVersions[ct] || '7.1',
      exclude_types: [],
      custom_wiql: '',
      repository: checkRepositories[ct] || '',
      stale_days: checkStaleDays[ct] || 14,
      ignore_reviewers: checkIgnoreReviewers[ct] ? checkIgnoreReviewers[ct].split(',').map(s => s.trim()).filter(Boolean) : [],
      estimate_mode: checkEstimateMode[ct] || 'both',
      parent_type_mappings: ct === 'orphan_check' && Object.keys(checkParentMappings).length > 0 ? { ...checkParentMappings } : {},
    })),
  }
}

async function saveProject() {
  try {
    const payload = buildPayload()
    if (editing.value) {
      await store.updateProject(editing.value, payload)
    } else {
      await store.addProject(payload)
    }
    resetForm()
  } catch (e) {
    store._toast('Failed to save project: ' + e.message, 'error')
  }
}

function removeProject(id) {
  if (confirmingDeleteId.value === id) {
    // second click = confirmed
    confirmingDeleteId.value = null
    store.deleteProject(id).catch(e => store._toast('Failed to delete project: ' + e.message, 'error'))
  } else {
    confirmingDeleteId.value = id
    // auto-cancel after 3 seconds
    setTimeout(() => { if (confirmingDeleteId.value === id) confirmingDeleteId.value = null }, 3000)
  }
}

// DB project helpers
function resetDbForm() {
  dbForm.name = ''
  dbForm.name_filter = ''
  dbForm.db_server_index = 0
  dbForm.db_allowlist = ''
  editingDb.value = null
  showDbForm.value = false
}

function editDbProject(db) {
  editingDb.value = db.id
  dbForm.name = db.name
  dbForm.name_filter = db.name_filter || ''
  dbForm.db_server_index = db.db_server_index ?? 0
  dbForm.db_allowlist = (db.db_allowlist || []).join('\n')
  showDbForm.value = true
}

async function saveDbProject() {
  try {
    const payload = {
      name: dbForm.name.trim(),
      name_filter: dbForm.name_filter.trim(),
      db_server_index: dbForm.db_server_index,
      db_allowlist: dbForm.db_allowlist.split('\n').map(s => s.trim()).filter(Boolean),
    }
    if (editingDb.value) {
      await store.updateDbProject(editingDb.value, payload)
    } else {
      await store.addDbProject(payload)
    }
    resetDbForm()
  } catch (e) {
    store._toast('Failed to save DB project: ' + e.message, 'error')
  }
}

function removeDbProject(id) {
  if (confirmingDbDeleteId.value === id) {
    confirmingDbDeleteId.value = null
    store.deleteDbProject(id).catch(e => store._toast('Failed to delete DB project: ' + e.message, 'error'))
  } else {
    confirmingDbDeleteId.value = id
    setTimeout(() => { if (confirmingDbDeleteId.value === id) confirmingDbDeleteId.value = null }, 3000)
  }
}
</script>
