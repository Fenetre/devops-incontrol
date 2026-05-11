<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 flex items-center justify-center p-4">
    <div class="w-full max-w-2xl">
      <!-- Progress bar -->
      <div class="mb-8">
        <div class="flex items-center justify-between mb-2">
          <span class="text-xs font-medium text-gray-500 dark:text-gray-400">Step {{ step }} of {{ totalSteps }}</span>
          <span class="text-xs text-gray-400 dark:text-gray-500">{{ stepTitles[step - 1] }}</span>
        </div>
        <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1.5">
          <div
            class="bg-primary-600 h-1.5 rounded-full transition-all duration-300"
            :style="{ width: `${(step / totalSteps) * 100}%` }"
          />
        </div>
      </div>

      <!-- Card -->
      <div class="bg-white dark:bg-gray-800 rounded-2xl shadow-xl border border-gray-200 dark:border-gray-700 overflow-hidden">

        <!-- Step 1: Welcome -->
        <div v-if="step === 1" class="p-8">
          <div class="text-center mb-8">
            <div class="w-16 h-16 bg-primary-100 dark:bg-primary-900/50 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <svg class="w-8 h-8 text-primary-600 dark:text-primary-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z" />
              </svg>
            </div>
            <h1 class="text-2xl font-bold text-gray-900 dark:text-gray-100">Welcome to DevOps InControl</h1>
            <p class="text-gray-600 dark:text-gray-400 mt-2">Let's get you set up in a few quick steps.</p>
          </div>

          <div class="space-y-3 text-sm text-gray-600 dark:text-gray-400">
            <div class="flex items-start gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-700/50">
              <span class="flex-shrink-0 w-6 h-6 bg-primary-100 dark:bg-primary-900/50 text-primary-700 dark:text-primary-300 rounded-full flex items-center justify-center text-xs font-bold">1</span>
              <div><strong class="text-gray-900 dark:text-gray-100">Set a Password</strong> — protects your dashboard.</div>
            </div>
            <div class="flex items-start gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-700/50">
              <span class="flex-shrink-0 w-6 h-6 bg-primary-100 dark:bg-primary-900/50 text-primary-700 dark:text-primary-300 rounded-full flex items-center justify-center text-xs font-bold">2</span>
              <div><strong class="text-gray-900 dark:text-gray-100">Add a PAT</strong> — a Personal Access Token lets DevOps InControl read your Azure DevOps data.</div>
            </div>
            <div class="flex items-start gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-700/50">
              <span class="flex-shrink-0 w-6 h-6 bg-primary-100 dark:bg-primary-900/50 text-primary-700 dark:text-primary-300 rounded-full flex items-center justify-center text-xs font-bold">3</span>
              <div><strong class="text-gray-900 dark:text-gray-100">Add your first project</strong> — pick an Azure DevOps project to start monitoring.</div>
            </div>
          </div>
        </div>

        <!-- Step 2: Password -->
        <div v-if="step === 2" class="p-8">
          <h2 class="text-xl font-bold text-gray-900 dark:text-gray-100 mb-1">Set your Password</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400 mb-6">This password protects your dashboard. You'll need it every time you open DevOps InControl.</p>

          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Password</label>
              <input
                v-autofocus
                v-model="apiKey"
                type="password"
                placeholder="Choose a strong password"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none"
                @keydown.enter="saveApiKey"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Confirm Password</label>
              <input
                v-model="apiKeyConfirm"
                type="password"
                placeholder="Repeat your password"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none"
                @keydown.enter="saveApiKey"
              />
            </div>
            <p v-if="apiKeyError" class="text-sm text-red-600 dark:text-red-400">{{ apiKeyError }}</p>
            <p v-if="apiKeyDone" class="text-sm text-green-600 dark:text-green-400 flex items-center gap-1">
              <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              Password saved.
            </p>
          </div>
        </div>

        <!-- Step 3: PAT -->
        <div v-if="step === 3" class="p-8">
          <h2 class="text-xl font-bold text-gray-900 dark:text-gray-100 mb-1">Azure DevOps Personal Access Token</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
            Create a PAT in
            <a href="https://dev.azure.com" target="_blank" class="text-primary-600 dark:text-primary-400 underline">Azure DevOps</a>
            → User Settings → Personal Access Tokens with these scopes:
          </p>
          <div class="mb-6 text-sm text-gray-600 dark:text-gray-400">
            <ul class="ml-4 list-disc space-y-0.5">
              <li><strong>Work Items (Read)</strong> — backlog checks, sprint monitoring</li>
              <li><strong>Code (Read)</strong> — pull request monitoring</li>
              <li><strong>Build (Read)</strong> — pipeline runs overview</li>
              <li><strong>Release (Read)</strong> — release deployments overview</li>
              <li><strong>Project and Team (Read)</strong> — project and team listing</li>
              <li><strong>Identity (Read)</strong> — permission checks</li>
              <li><strong>Work Items (Read &amp; Write)</strong> — tag management</li>
              <li><strong>Security (Manage)</strong> — repo &amp; area permission audits</li>
              <li><strong>Graph (Read)</strong> — group membership resolution</li>
              <li><strong>Wiki (Read)</strong> — wiki permission checks</li>
            </ul>
            <p class="mt-3 px-3 py-2 text-xs font-medium text-amber-800 dark:text-amber-200 bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-700 rounded">💡 Click <strong>"Show all scopes"</strong> at the bottom of the PAT form to reveal all permissions.</p>
          </div>

          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Azure DevOps Organization name</label>
              <input
                v-model="projForm.organization"
                v-autofocus
                type="text"
                placeholder="e.g. MyOrganization"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none"
              />
              <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">The name from https://dev.azure.com/<strong>YourOrganization</strong></p>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Personal Access Token</label>
              <input
                v-model="pat"
                type="password"
                placeholder="Paste your PAT here"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none font-mono"
                @keydown.enter="savePat"
              />
              <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">Azure DevOps PATs are typically 52+ characters long.</p>
            </div>

            <!-- Validation progress -->
            <div v-if="patValidating" class="flex items-center gap-3 p-3 rounded-lg bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-800">
              <svg class="w-4 h-4 text-blue-500 animate-spin shrink-0" fill="none" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
              </svg>
              <span class="text-sm text-blue-700 dark:text-blue-300">{{ patValidationStep }}</span>
            </div>

            <p v-if="patError" class="text-sm text-red-600 dark:text-red-400">{{ patError }}</p>
            <p v-if="patDone" class="text-sm text-green-600 dark:text-green-400 flex items-center gap-1">
              <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              PAT saved &amp; validated — {{ store.orgProjects.length }} project{{ store.orgProjects.length === 1 ? '' : 's' }} found in {{ projForm.organization }}.
            </p>
          </div>
        </div>

        <!-- Step 4: First project -->
        <div v-if="step === 4" class="p-8">
          <h2 class="text-xl font-bold text-gray-900 dark:text-gray-100 mb-1">Add your first project</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400 mb-6">Pick an Azure DevOps project to start monitoring. You can add more later.</p>

          <div class="space-y-4">
            <!-- Organization (entered in step 3, shown read-only) -->
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Organization</label>
              <div class="px-3 py-2 border border-gray-200 dark:border-gray-600 rounded-lg text-sm bg-gray-50 dark:bg-gray-800 text-gray-700 dark:text-gray-300">
                {{ projForm.organization }}
              </div>
            </div>

            <!-- Project -->
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
              <div v-if="store.orgProjectsError" class="text-xs text-red-600 dark:text-red-400 mb-1">{{ store.orgProjectsError }}</div>
              <SelectMenu
                v-model="projForm.project"
                :options="projectOptions"
                :placeholder="projectPlaceholder"
                :disabled="!projForm.organization || store.orgProjects.length === 0"
                :loading="store.loadingOrgProjects"
              />
            </div>

            <!-- Area Path -->
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Area Path <span class="text-gray-500 font-normal">(optional)</span></label>
              <SelectMenu
                v-model="projForm.area_path"
                :options="areaPathOptions"
                placeholder="All areas (no filter)"
                :disabled="!projForm.project"
                :loading="store.loadingAreaPaths"
              />
              <label v-if="projForm.area_path" class="inline-flex items-center gap-2 mt-2 cursor-pointer">
                <input type="checkbox" v-model="projForm.include_child_areas" class="rounded border-gray-300 text-primary-600 focus:ring-primary-500" />
                <span class="text-sm text-gray-700 dark:text-gray-300">Include child areas</span>
              </label>
            </div>

            <!-- Checks -->
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Checks to enable</label>
              <p class="text-xs text-gray-500 dark:text-gray-400 mb-2">These can all be changed later in Configuration.</p>
              <div class="space-y-1.5 max-h-56 overflow-y-auto">
                <label
                  v-for="ct in store.checkTypes"
                  :key="ct.type_key"
                  class="flex items-center gap-2 p-2 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700/50 cursor-pointer"
                >
                  <input
                    type="checkbox"
                    :value="ct.type_key"
                    v-model="selectedChecks"
                    class="rounded border-gray-300 dark:border-gray-600 text-primary-600 focus:ring-primary-500"
                  />
                  <div>
                    <span class="text-sm text-gray-900 dark:text-gray-100">{{ ct.label }}</span>
                    <span class="block text-xs text-gray-500 dark:text-gray-400">{{ ct.description }}</span>
                  </div>
                </label>
              </div>
            </div>

            <p v-if="projectError" class="text-sm text-red-600 dark:text-red-400">{{ projectError }}</p>
            <p v-if="projectDone" class="text-sm text-green-600 dark:text-green-400 flex items-center gap-1">
              <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              Project added!
            </p>
          </div>
        </div>

        <!-- Step 5: Done -->
        <div v-if="step === 5" class="p-8 text-center">
          <div class="w-16 h-16 bg-green-100 dark:bg-green-900/50 rounded-2xl flex items-center justify-center mx-auto mb-4">
            <svg class="w-8 h-8 text-green-600 dark:text-green-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h2 class="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-2">You're all set!</h2>
          <p class="text-gray-600 dark:text-gray-400 mb-6">DevOps InControl will now start monitoring your project. You can add more projects and tweak settings from the Configuration page at any time.</p>
        </div>

        <!-- Footer buttons -->
        <div class="px-8 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <button
            v-if="step > 1 && step < 5"
            @click="step--"
            class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:text-gray-900 dark:hover:text-gray-100 transition-colors"
          >
            Back
          </button>
          <div v-else></div>

          <button
            v-if="step === 1"
            @click="step++"
            class="px-6 py-2.5 rounded-lg text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 transition-colors shadow-sm"
          >
            Get Started
          </button>

          <button
            v-else-if="step === 2"
            @click="saveApiKey"
            :disabled="saving"
            class="px-6 py-2.5 rounded-lg text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 transition-colors shadow-sm disabled:opacity-50"
          >
            {{ apiKeyDone ? 'Next' : saving ? 'Saving…' : 'Save & Continue' }}
          </button>

          <button
            v-else-if="step === 3"
            @click="savePat"
            :disabled="saving"
            class="px-6 py-2.5 rounded-lg text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 transition-colors shadow-sm disabled:opacity-50"
          >
            {{ patDone ? 'Next' : saving ? 'Saving…' : 'Save & Continue' }}
          </button>

          <button
            v-else-if="step === 4"
            @click="saveProject"
            :disabled="saving"
            class="px-6 py-2.5 rounded-lg text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 transition-colors shadow-sm disabled:opacity-50"
          >
            {{ projectDone ? 'Next' : saving ? 'Saving…' : 'Save & Finish' }}
          </button>

          <button
            v-else-if="step === 5"
            @click="finish"
            class="px-6 py-2.5 rounded-lg text-sm font-medium text-white bg-green-600 hover:bg-green-700 transition-colors shadow-sm"
          >
            Go to Dashboard
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import SelectMenu from '../components/SelectMenu.vue'

const store = useMonitorStore()
const router = useRouter()

const totalSteps = 5
const stepTitles = ['Welcome', 'Password', 'Personal Access Token', 'First Project', 'Done']
const step = ref(1)
const saving = ref(false)

// Step 2 — Password
const apiKey = ref('')
const apiKeyConfirm = ref('')
const apiKeyError = ref('')
const apiKeyDone = ref(false)

// Step 3 — PAT
const pat = ref('')
const patError = ref('')
const patDone = ref(false)
const patValidating = ref(false)
const patValidationStep = ref('')

// Step 4 — First Project
const projForm = reactive({ organization: '', project: '', area_path: '', include_child_areas: true })
const selectedChecks = ref([])
const projectError = ref('')
const projectDone = ref(false)

const projectOptions = computed(() =>
  store.orgProjects.map(p => ({ value: p.name, label: p.name }))
)
const projectPlaceholder = computed(() => {
  if (store.orgProjectsError) return 'Failed — check org name & PAT'
  if (projForm.organization) return 'Select project…'
  return 'Enter organization first'
})
const areaPathOptions = computed(() => [
  { value: '', label: 'All areas (no filter)' },
  ...store.areaPaths.map(a => ({ value: a.path, label: a.path }))
])

// When entering step 4, projects are already loaded (validated in step 3)
// Just load check types
watch(step, async (newStep) => {
  if (newStep === 4) {
    await store.fetchCheckTypes()
    if (!selectedChecks.value.length) {
      selectedChecks.value = store.checkTypes.map(ct => ct.type_key)
    }
  }
})

// Auto-select project when only one available
watch(() => store.orgProjects, (projs) => {
  if (projs.length === 1 && !projForm.project) {
    projForm.project = projs[0].name
  }
})

// Fetch area paths when project changes
watch(() => projForm.project, (newProject) => {
  projForm.area_path = ''
  store.areaPaths = []
  if (newProject && projForm.organization) {
    store.fetchAreaPaths(projForm.organization.trim(), newProject)
  }
})

async function saveApiKey() {
  if (apiKeyDone.value) { if (step.value === 2) step.value++; return }
  apiKeyError.value = ''
  if (!apiKey.value.trim()) { apiKeyError.value = 'Please enter a password.'; return }
  if (apiKey.value.length < 6) { apiKeyError.value = 'Password must be at least 6 characters.'; return }
  if (apiKey.value !== apiKeyConfirm.value) { apiKeyError.value = 'Keys do not match.'; return }
  saving.value = true
  try {
    await store.saveApiKey(apiKey.value.trim())
    apiKeyDone.value = true
    step.value++
  } catch (e) {
    apiKeyError.value = e.message || 'Failed to save password.'
  } finally {
    saving.value = false
  }
}

async function savePat() {
  if (patDone.value) { step.value++; return }
  patError.value = ''
  patValidating.value = false
  if (!projForm.organization.trim()) { patError.value = 'Please enter your organization name.'; return }
  if (!pat.value.trim()) { patError.value = 'Please enter a PAT.'; return }
  if (pat.value.trim().length < 20) { patError.value = 'This doesn\'t look like a valid PAT. Azure DevOps tokens are typically 52+ characters.'; return }
  saving.value = true
  patValidating.value = true
  try {
    patValidationStep.value = 'Saving PAT…'
    await store.savePat(pat.value.trim())

    patValidationStep.value = 'Validating connection to ' + projForm.organization.trim() + '…'
    // Clear any cached projects for this org so we get a fresh validation
    delete store._orgProjectsCache[projForm.organization.trim()]
    await store.fetchOrgProjects(projForm.organization.trim())
    patValidating.value = false

    if (store.orgProjectsError) {
      patError.value = `Could not reach organization "${projForm.organization}". Check the org name and PAT scopes (needs Work Items Read, Code Read, Project and Team Read).`
      saving.value = false
      return
    }
    if (store.orgProjects.length === 0) {
      patError.value = `Connected to "${projForm.organization}" but no projects were found. Check that the PAT has access to at least one project.`
      saving.value = false
      return
    }
    patDone.value = true
    setTimeout(() => step.value++, 600)
  } catch (e) {
    patValidating.value = false
    patError.value = e.message || 'Failed to save PAT.'
  } finally {
    saving.value = false
  }
}

let orgDebounce = null
function onOrgChange() {
  projForm.project = ''
  store.orgProjects = []
  store.orgProjectsError = null
  clearTimeout(orgDebounce)
  if (!projForm.organization.trim()) return
  orgDebounce = setTimeout(() => {
    store.fetchOrgProjects(projForm.organization.trim())
  }, 300)
}

async function saveProject() {
  if (projectDone.value) { step.value++; return }
  projectError.value = ''
  if (!projForm.organization.trim()) { projectError.value = 'Please select an organization.'; return }
  if (!projForm.project.trim()) { projectError.value = 'Please select a project.'; return }
  saving.value = true
  try {
    const payload = {
      organization: projForm.organization,
      project: projForm.project,
      area_path: projForm.area_path,
      include_child_areas: projForm.include_child_areas,
      ignore_title_contains: [],
      ignore_parent_title_contains: [],
      checks: selectedChecks.value.map(ct => ({
        check_type: ct,
        enabled: true,
        api_version: '7.1',
        exclude_types: [],
        custom_wiql: '',
        repository: '',
        stale_days: 14,
        ignore_reviewers: [],
      })),
    }
    await store.addProject(payload)
    projectDone.value = true
    setTimeout(() => step.value++, 600)
  } catch (e) {
    projectError.value = e.message || 'Failed to add project.'
  } finally {
    saving.value = false
  }
}

async function finish() {
  try {
    await store.completeSetup()
    await store.fetchProjects()
    store.runChecks()
  } catch { /* ignore */ }
  router.replace('/')
}

// Skip wizard if already set up
onMounted(async () => {
  const status = await store.fetchSetupStatus()
  if (status?.setup_complete) {
    router.replace('/')
  }
})
</script>
