<template>
  <UModal :open="open" @update:open="$emit('update:open', $event)" :ui="{ overlay: 'z-[9998]', content: 'z-[9999] w-[95vw] max-w-7xl !ring-0 !outline-none !shadow-xl focus-visible:!ring-0 focus-visible:!outline-none', header: 'hidden', close: 'hidden', footer: 'bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 px-6 py-3' }">
    <template #body>
      <!-- Header bar -->
      <div class="-mx-6 -mt-6 px-6 py-4 border-b border-primary-200 dark:border-gray-700 bg-primary-50 dark:bg-transparent mb-5">
        <h3 class="text-lg font-semibold text-primary-900 dark:text-gray-100">
          Create {{ workItemType }}
        </h3>
      </div>

      <div class="space-y-4">
        <!-- Step 1: Source choice -->
        <div v-if="step === 'source'" class="space-y-3">
          <p class="text-sm text-gray-600 dark:text-gray-300">How would you like to start?</p>
          <div class="grid grid-cols-2 gap-3">
            <button @click="chooseSource('clean')"
              class="p-4 rounded-lg border border-gray-200 dark:border-gray-700 hover:border-primary-400 dark:hover:border-primary-500 hover:bg-primary-50 dark:hover:bg-primary-900/20 transition-colors text-left">
              <UIcon name="i-heroicons-document-plus" class="w-6 h-6 text-primary-500 mb-2" />
              <div class="text-sm font-medium text-gray-700 dark:text-gray-200">Start clean</div>
              <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Empty work item with just a title</div>
            </button>
            <button @click="chooseSource('template')"
              class="p-4 rounded-lg border border-gray-200 dark:border-gray-700 hover:border-primary-400 dark:hover:border-primary-500 hover:bg-primary-50 dark:hover:bg-primary-900/20 transition-colors text-left">
              <UIcon name="i-heroicons-document-duplicate" class="w-6 h-6 text-violet-500 mb-2" />
              <div class="text-sm font-medium text-gray-700 dark:text-gray-200">Use template</div>
              <div class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Pre-fill from a project template</div>
            </button>
          </div>
        </div>

        <!-- Step 2: Template selection -->
        <div v-if="step === 'template'" class="space-y-4">
          <UButton variant="link" color="neutral" size="xs" icon="i-heroicons-arrow-left" label="Back" @click="step = 'source'" />
          <div v-if="projectOptions.length > 1">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
            <USelectMenu v-model="selectedProjectId" :items="projectOptions" value-key="value" placeholder="Choose a project…"
              :ui="{ content: 'z-[10000]' }" class="w-full" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Team</label>
            <USelectMenu v-model="templateTeam" :items="teamOptions" value-key="value" placeholder="Choose a team…"
              :loading="loadingTeams" :disabled="!selectedProjectId" :ui="{ content: 'z-[10000]' }" class="w-full" />
          </div>
          <div v-if="templateTeam">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Template</label>
            <div v-if="loadingTemplates" class="flex items-center gap-2 text-xs text-gray-400 py-2">
              <UIcon name="i-heroicons-arrow-path" class="w-3 h-3 animate-spin" /> Loading templates…
            </div>
            <div v-else-if="filteredTemplates.length === 0" class="text-xs text-gray-400 py-2">No templates found for {{ workItemType }}.</div>
            <div v-else class="space-y-1 max-h-[200px] overflow-y-auto">
              <button v-for="tmpl in filteredTemplates" :key="tmpl.id" @click="selectTemplate(tmpl)"
                class="w-full text-left px-3 py-2 rounded-lg border border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
                <div class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ tmpl.name }}</div>
                <div v-if="tmpl.description" class="text-xs text-gray-400 mt-0.5 truncate">{{ tmpl.description }}</div>
              </button>
            </div>
          </div>
        </div>

        <!-- Step 3: Form -->
        <div v-if="step === 'form'" class="space-y-4">
          <UButton variant="link" color="neutral" size="xs" icon="i-heroicons-arrow-left" label="Back" @click="step = 'source'" />

          <!-- Project -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
            <USelectMenu v-model="selectedProjectId" :items="projectOptions" value-key="value" placeholder="Choose a project…"
              :ui="{ content: 'z-[10000]' }" class="w-full" />
          </div>

          <!-- Title -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Title *</label>
            <UInput name="title" v-model="form.title" placeholder="Enter title…" class="w-full" />
          </div>

          <!-- Iteration path -->
          <div v-if="iterationOptions.length > 0">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Iteration</label>
            <USelectMenu v-model="form.iterationPath" :items="iterationOptions" value-key="value" placeholder="Default iteration" :ui="{ content: 'z-[10000]' }" class="w-full" />
          </div>

          <!-- Description -->
          <div class="relative">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Description</label>
            <div
              ref="editorRef"
              contenteditable="true"
              class="min-h-[120px] max-h-[250px] overflow-y-auto px-3 py-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-sm text-gray-700 dark:text-gray-200 focus:outline-none focus:ring-2 focus:ring-primary-400 prose dark:prose-invert max-w-none"
              @paste="onPaste"
              @input="onEditorInput"
              @keydown="onMentionKeydown"
              @blur="closeMentionDropdown"
            ></div>
            <!-- @mention dropdown -->
            <div v-if="mentionOpen && mentionContext === 'desc'" class="absolute left-0 bottom-full mb-1 w-64 max-h-48 overflow-y-auto bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg z-[10001]">
              <button
                v-for="(person, idx) in filteredMentions" :key="person.value"
                class="w-full text-left px-3 py-1.5 text-sm hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors"
                :class="idx === mentionIdx ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300' : 'text-gray-700 dark:text-gray-300'"
                @mousedown.prevent="selectMention(person)"
              >{{ person.label }}</button>
              <div v-if="filteredMentions.length === 0" class="px-3 py-2 text-xs text-gray-400">No matches</div>
            </div>
            <p class="text-[10px] text-gray-400 mt-1">Paste images directly · Type @ to mention people</p>
          </div>

          <!-- Custom fields for this work item type -->
          <div v-if="visibleCustomFields.length > 0" class="pt-2 border-t border-gray-100 dark:border-gray-700 space-y-4">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Custom Fields</div>
            <!-- Dropdown / input fields side by side -->
            <div v-if="gridCustomFields.length > 0" class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div v-for="field in gridCustomFields" :key="field.referenceName">
                <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">{{ field.name }}</label>
                <USelectMenu v-if="field.allowedValues.length > 0"
                  :model-value="form.customFields[field.referenceName] || undefined"
                  @update:model-value="form.customFields[field.referenceName] = $event"
                  :items="fieldOptions(field)"
                  value-key="value"
                  :placeholder="field.defaultValue || 'Select…'"
                  :ui="{ content: 'z-[10000]' }"
                  class="w-full" />
                <UInput v-else
                  name="custom-field"
                  :model-value="form.customFields[field.referenceName] || ''"
                  @update:model-value="form.customFields[field.referenceName] = $event"
                  :placeholder="field.defaultValue || ''"
                  class="w-full" />
              </div>
            </div>
            <!-- Text fields full width -->
            <div v-for="field in textCustomFields" :key="field.referenceName">
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">{{ field.name }}</label>
              <div class="relative">
                <div
                  contenteditable="true"
                  class="w-full min-h-[80px] overflow-x-auto px-3 py-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-sm text-gray-700 dark:text-gray-200 focus:outline-none focus:ring-2 focus:ring-primary-400 prose dark:prose-invert max-w-none"
                  v-html="DOMPurify.sanitize(form.customFields[field.referenceName] || '')"
                  @blur="form.customFields[field.referenceName] = $event.target.innerHTML"
                  @input="onCustomFieldMentionInput($event, field.referenceName)"
                  @keydown="onMentionKeydown"
                  @paste="onPaste($event, 'cf-' + field.referenceName)"
                ></div>
                <!-- @mention dropdown for this custom field -->
                <div v-if="mentionOpen && mentionContext === 'cf-' + field.referenceName" class="absolute left-0 bottom-full mb-1 w-64 max-h-48 overflow-y-auto bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg z-[10001]">
                  <button
                    v-for="(person, idx) in filteredMentions" :key="person.value"
                    class="w-full text-left px-3 py-1.5 text-sm hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors"
                    :class="idx === mentionIdx ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300' : 'text-gray-700 dark:text-gray-300'"
                    @mousedown.prevent="selectMention(person)"
                  >{{ person.label }}</button>
                  <div v-if="filteredMentions.length === 0" class="px-3 py-2 text-xs text-gray-400">No matches</div>
                </div>
              </div>
            </div>
          </div>
          <div v-if="loadingFields" class="flex items-center gap-2 text-xs text-gray-400 py-1">
            <UIcon name="i-heroicons-arrow-path" class="w-3 h-3 animate-spin" /> Loading fields…
          </div>

          <!-- Parent info (read-only) -->
          <div v-if="props.parentId" class="text-xs text-gray-500 dark:text-gray-400 px-3 py-2 rounded-lg bg-gray-50 dark:bg-gray-700/50 border border-gray-100 dark:border-gray-700">
            <span class="font-medium">Parent:</span> #{{ props.parentId }} {{ props.parentTitle || '' }}
          </div>

          <!-- Template fields applied -->
          <div v-if="Object.keys(templateFields).length > 0" class="text-xs text-gray-500 dark:text-gray-400 px-3 py-2 rounded-lg bg-violet-50 dark:bg-violet-900/20 border border-violet-100 dark:border-violet-800">
            <span class="font-medium">Template fields:</span> {{ Object.keys(templateFields).length }} field(s) will be applied
          </div>

          <!-- Error -->
          <UAlert v-if="createError" color="error" icon="i-heroicons-exclamation-circle" :description="createError" />
        </div>
      </div>
    </template>
    <template #footer>
      <div class="flex items-center justify-between w-full">
        <span class="text-xs text-gray-500 dark:text-gray-400">Press <kbd class="px-1 py-0.5 text-xs font-mono bg-gray-100 dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded">Esc</kbd> to close</span>
        <div class="flex gap-2">
          <UButton variant="outline" color="neutral" label="Cancel" @click="$emit('update:open', false)" />
          <UButton v-if="step === 'form'" label="Create" :loading="creating" :disabled="!canCreate" @click="doCreate" />
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue'
import DOMPurify from 'dompurify'
import { useApi } from '../composables/useApi.js'
import { useRoadmapStore } from '../stores/roadmap.js'

const props = defineProps({
  open: { type: Boolean, default: false },
  workItemType: { type: String, default: 'Epic' },
  parentId: { type: Number, default: null },
  parentTitle: { type: String, default: '' },
  project: { type: String, default: '' },
})

const emit = defineEmits(['update:open', 'created'])

const api = useApi()
const store = useRoadmapStore()

const step = ref('source')
const selectedProjectId = ref('')
const templateTeam = ref('')
const loadingTeams = ref(false)
const teamOptions = ref([])
const loadingTemplates = ref(false)
const templates = ref([])
const templateFields = ref({})
const iterations = ref([])
const customFields = ref([])
const loadingFields = ref(false)
const creating = ref(false)
const createError = ref('')
const editorRef = ref(null)

// @mention state
const mentionMembers = ref([])
const loadingMentionMembers = ref(false)
const mentionOpen = ref(false)
const mentionQuery = ref('')
const mentionIdx = ref(0)
const mentionContext = ref('')  // 'desc' or 'cf-{referenceName}'

const filteredMentions = computed(() => {
  if (!mentionQuery.value) return mentionMembers.value.slice(0, 20)
  const q = mentionQuery.value.toLowerCase()
  return mentionMembers.value.filter(m => m.label.toLowerCase().includes(q)).slice(0, 20)
})

const form = ref({ title: '', description: '', iterationPath: '', customFields: {} })

const projectOptions = computed(() =>
  store.config.projects.map(p => ({ value: p.project_id, label: p.project }))
)

const defaultIterationPath = ref('')

const iterationOptions = computed(() => {
  const dated = iterations.value.filter(i => i.startDate && i.finishDate)
  const undated = iterations.value.filter(i => !i.startDate || !i.finishDate)
  // Dated: newest first
  dated.sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime())
  // Undated: alphabetical
  undated.sort((a, b) => (a.path || '').localeCompare(b.path || ''))
  const all = [...dated, ...undated]
  const opts = all.map(i => ({ value: i.path, label: i.path.split('\\').pop() || i.path }))
  // Ensure the default iteration is always in the list (in case it wasn't returned by the tree API)
  if (defaultIterationPath.value && !opts.some(o => o.value === defaultIterationPath.value)) {
    const label = defaultIterationPath.value.split('\\').pop() || defaultIterationPath.value
    opts.unshift({ value: defaultIterationPath.value, label: label + ' (default)' })
  }
  return opts
})

const filteredTemplates = computed(() =>
  templates.value.filter(t => t.workItemTypeName === props.workItemType)
)

const canCreate = computed(() => !!form.value.title.trim() && !!selectedProjectId.value)

// Get the project name from selectedProjectId
const selectedProject = computed(() => store.config.projects.find(p => p.project_id === selectedProjectId.value))

// Reset state when modal opens
watch(() => props.open, (isOpen) => {
  if (isOpen) {
    step.value = 'source'
    form.value = { title: '', description: '', iterationPath: '', customFields: {} }
    templateFields.value = {}
    createError.value = ''
    templateTeam.value = ''
    templates.value = []
    customFields.value = []
    defaultIterationPath.value = ''
    if (props.project) {
      const proj = store.config.projects.find(p => p.project === props.project)
      if (proj) selectedProjectId.value = proj.project_id
    } else if (projectOptions.value.length === 1) {
      selectedProjectId.value = projectOptions.value[0].value
    } else {
      selectedProjectId.value = ''
    }
    // Always reload teams for the selected project
    loadTeamsForProject(selectedProjectId.value)
    loadCustomFields(selectedProjectId.value)
    nextTick(() => { if (editorRef.value) editorRef.value.innerHTML = '' })
  }
})

async function loadTeamsForProject(val) {
  templateTeam.value = ''
  teamOptions.value = []
  iterations.value = []
  if (!val) return
  const proj = store.config.projects.find(p => p.project_id === val)
  if (!proj) return
  loadingTeams.value = true
  try {
    const data = await api.get(`/api/devops/organizations/${encodeURIComponent(proj.organization)}/projects/${encodeURIComponent(proj.project)}/teams`)
    teamOptions.value = (data || []).map(t => ({ value: t.name || t, label: t.name || t }))
  } catch (e) { createError.value = e.message || 'Failed to load teams' }
  finally { loadingTeams.value = false }

  // Load iterations
  try {
    const resp = await api.get(`/api/roadmap/iterations?organization=${encodeURIComponent(proj.organization)}&project=${encodeURIComponent(proj.project)}&team=${encodeURIComponent(teamOptions.value[0]?.value || proj.project + ' Team')}`)
    iterations.value = resp.iterations || []
    // Store and auto-select the team's default iteration from Azure DevOps
    if (resp.defaultIteration) {
      defaultIterationPath.value = resp.defaultIteration
      form.value.iterationPath = resp.defaultIteration
    }
  } catch { iterations.value = [] }
}

// Load teams when project changes
watch(selectedProjectId, (val, oldVal) => {
  if (val !== oldVal) {
    templateTeam.value = ''
    templates.value = []
  }
  loadTeamsForProject(val)
  loadCustomFields(val)
})

async function loadCustomFields(val) {
  customFields.value = []
  if (!val) return
  const proj = store.config.projects.find(p => p.project_id === val)
  if (!proj) return
  loadingFields.value = true
  try {
    const resp = await api.get(`/api/roadmap/wit-fields?organization=${encodeURIComponent(proj.organization)}&project=${encodeURIComponent(proj.project)}&type=${encodeURIComponent(props.workItemType)}`)
    customFields.value = (resp || []).filter(f => f.referenceName.startsWith('Custom.') || f.referenceName.startsWith('Microsoft.VSTS.Common.') || f.allowedValues?.length > 0)
  } catch { customFields.value = [] }
  finally { loadingFields.value = false }
}

function fieldOptions(field) {
  return field.allowedValues.map(v => ({ value: v, label: v }))
}

// Fields to hide from custom fields section (system-managed, not useful for user input)
const HIDDEN_FIELDS = [
  'Microsoft.VSTS.Common.StateChangeDate',
  'Microsoft.VSTS.Common.ActivatedDate',
  'Microsoft.VSTS.Common.ResolvedDate',
  'Microsoft.VSTS.Common.ResolvedBy',
  'Microsoft.VSTS.Common.ResolvedReason',
  'Microsoft.VSTS.Common.ActivatedBy',
  'Microsoft.VSTS.Common.StackRank',
  'Microsoft.VSTS.Common.ClosedDate',
  'Microsoft.VSTS.Common.ClosedBy',
]

const visibleCustomFields = computed(() =>
  customFields.value.filter(f => !HIDDEN_FIELDS.includes(f.referenceName))
)

const gridCustomFields = computed(() =>
  visibleCustomFields.value.filter(f => !isTextField(f))
)

const textCustomFields = computed(() =>
  visibleCustomFields.value.filter(f => isTextField(f))
)

function isTextField(field) {
  if (field.allowedValues?.length > 0) return false
  const t = (field.type || '').toLowerCase()
  return t === 'html' || t === 'plaintext' || t === 'string'
}

function onCustomFieldMentionInput(event, referenceName) {
  // Only handle @mention detection — value syncs on blur
  const trigger = getMentionTriggerWord()
  if (trigger) {
    mentionOpen.value = true
    mentionContext.value = 'cf-' + referenceName
    mentionQuery.value = trigger.query
    mentionIdx.value = 0
    if (mentionMembers.value.length === 0 && !loadingMentionMembers.value) {
      const proj = store.config.projects.find(p => p.project_id === selectedProjectId.value)
      if (proj) {
        loadingMentionMembers.value = true
        api.get(`/api/roadmap/project-members?organization=${encodeURIComponent(proj.organization)}&project=${encodeURIComponent(proj.project)}`)
          .then(members => { mentionMembers.value = members })
          .catch(() => {})
          .finally(() => { loadingMentionMembers.value = false })
      }
    }
  } else {
    mentionOpen.value = false
  }
}

function isHtmlContent(val) {
  return val && /<[a-z][\s\S]*>/i.test(val)
}

// Load templates when team changes
watch(templateTeam, async (val) => {
  templates.value = []
  if (!val || !selectedProjectId.value) return
  const proj = store.config.projects.find(p => p.project_id === selectedProjectId.value)
  if (!proj) return
  loadingTemplates.value = true
  try {
    templates.value = await api.get(
      `/api/roadmap/templates?organization=${encodeURIComponent(proj.organization)}&project=${encodeURIComponent(proj.project)}&team=${encodeURIComponent(val)}`
    )
  } catch { templates.value = [] }
  finally { loadingTemplates.value = false }
})

function chooseSource(source) {
  if (source === 'clean') {
    step.value = 'form'
  } else {
    step.value = 'template'
  }
}

async function selectTemplate(tmpl) {
  // Load full template with fields
  const proj = store.config.projects.find(p => p.project_id === selectedProjectId.value)
  if (!proj) return
  try {
    const full = await api.get(
      `/api/roadmap/templates/${encodeURIComponent(tmpl.id)}?organization=${encodeURIComponent(proj.organization)}&project=${encodeURIComponent(proj.project)}&team=${encodeURIComponent(templateTeam.value)}`
    )
    templateFields.value = full.fields || {}
    // Pre-fill title/description from template fields if present
    if (templateFields.value['System.Title']) {
      form.value.title = templateFields.value['System.Title']
    }
    if (templateFields.value['System.Description'] && editorRef.value) {
      form.value.description = templateFields.value['System.Description']
      editorRef.value.innerHTML = templateFields.value['System.Description']
    }
    // Pre-fill custom fields from template
    for (const [key, val] of Object.entries(templateFields.value)) {
      if (key.startsWith('System.')) continue
      form.value.customFields[key] = val
    }
    step.value = 'form'
  } catch (e) {
    createError.value = e.message || 'Failed to load template'
    step.value = 'form'
  }
}

function onEditorInput() {
  if (editorRef.value) {
    form.value.description = editorRef.value.innerHTML
  }
  // Check for @mention trigger
  const trigger = getMentionTriggerWord()
  if (trigger) {
    mentionOpen.value = true
    mentionContext.value = 'desc'
    mentionQuery.value = trigger.query
    mentionIdx.value = 0
    // Load members if needed
    if (mentionMembers.value.length === 0 && !loadingMentionMembers.value) {
      const proj = store.config.projects.find(p => p.project_id === selectedProjectId.value)
      if (proj) {
        loadingMentionMembers.value = true
        api.get(`/api/roadmap/project-members?organization=${encodeURIComponent(proj.organization)}&project=${encodeURIComponent(proj.project)}`)
          .then(members => { mentionMembers.value = members })
          .catch(() => {})
          .finally(() => { loadingMentionMembers.value = false })
      }
    }
  } else {
    mentionOpen.value = false
  }
}

function getMentionTriggerWord() {
  const sel = window.getSelection()
  if (!sel || sel.rangeCount === 0) return null
  const range = sel.getRangeAt(0)
  const textNode = range.startContainer
  if (textNode.nodeType !== Node.TEXT_NODE) return null
  const text = textNode.textContent || ''
  const cursorPos = range.startOffset
  const beforeCursor = text.slice(0, cursorPos)
  const atIdx = beforeCursor.lastIndexOf('@')
  if (atIdx === -1) return null
  const query = beforeCursor.slice(atIdx + 1)
  if (query.length > 40 || query.includes('\n')) return null
  return { query, atIdx, textNode, cursorPos }
}

function onMentionKeydown(event) {
  if (!mentionOpen.value) return
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    mentionIdx.value = Math.min(mentionIdx.value + 1, filteredMentions.value.length - 1)
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    mentionIdx.value = Math.max(mentionIdx.value - 1, 0)
  } else if (event.key === 'Enter' && filteredMentions.value.length > 0) {
    event.preventDefault()
    selectMention(filteredMentions.value[mentionIdx.value])
  } else if (event.key === 'Escape') {
    mentionOpen.value = false
  }
}

function selectMention(person) {
  const trigger = getMentionTriggerWord()
  if (!trigger) { mentionOpen.value = false; return }
  const { atIdx, textNode, cursorPos } = trigger
  const text = textNode.textContent || ''
  const before = text.slice(0, atIdx)
  const after = text.slice(cursorPos)
  const mentionEl = document.createElement('a')
  mentionEl.href = `mailto:${person.value}`
  mentionEl.textContent = `@${person.label}`
  mentionEl.style.cssText = 'color: #0078d4; font-weight: 500; text-decoration: none;'
  mentionEl.contentEditable = 'false'
  textNode.textContent = before
  const afterNode = document.createTextNode(after || '\u00A0')
  const parent = textNode.parentNode
  parent.insertBefore(mentionEl, textNode.nextSibling)
  parent.insertBefore(afterNode, mentionEl.nextSibling)
  const sel = window.getSelection()
  const newRange = document.createRange()
  newRange.setStart(afterNode, after ? 0 : 1)
  newRange.collapse(true)
  sel.removeAllRanges()
  sel.addRange(newRange)
  mentionOpen.value = false
  // Update form - sync the correct editor
  if (mentionContext.value && mentionContext.value.startsWith('cf-')) {
    const refName = mentionContext.value.slice(3)
    // Find the contenteditable parent of the textNode
    let el = textNode.parentNode
    while (el && !el.hasAttribute?.('contenteditable')) el = el.parentNode
    if (el) form.value.customFields[refName] = el.innerHTML
  } else {
    if (editorRef.value) form.value.description = editorRef.value.innerHTML
  }
}

function closeMentionDropdown() {
  setTimeout(() => { mentionOpen.value = false }, 200)
}

async function onPaste(evt, context) {
  const items = evt.clipboardData?.items
  if (!items) return
  for (let i = 0; i < items.length; i++) {
    if (items[i].type.startsWith('image/')) {
      evt.preventDefault()
      const file = items[i].getAsFile()
      if (!file) return
      const editorEl = context && context.startsWith('cf-') ? evt.target : editorRef.value
      await uploadAndInsertImage(file, editorEl, context)
      return
    }
  }
}

async function uploadAndInsertImage(file, editorEl, context) {
  const proj = selectedProject.value
  if (!proj) { createError.value = 'Select a project first to upload images'; return }

  // Save selection to insert at cursor
  const sel = window.getSelection()
  const range = sel?.rangeCount > 0 ? sel.getRangeAt(0) : null

  // Insert placeholder at cursor or end
  const placeholder = document.createElement('span')
  placeholder.textContent = '⏳ uploading image…'
  placeholder.className = 'text-gray-400 italic'
  if (range && editorEl?.contains(range.startContainer)) {
    range.deleteContents()
    range.insertNode(placeholder)
  } else {
    editorEl?.appendChild(placeholder)
  }

  try {
    const formData = new FormData()
    formData.append('file', file)
    formData.append('organization', proj.organization)
    formData.append('project', proj.project)
    const resp = await api.postForm('/api/roadmap/upload-attachment', formData)
    const img = document.createElement('img')
    img.src = `/api/roadmap/attachment-proxy?url=${encodeURIComponent(resp.url)}`
    img.style.maxWidth = '100%'
    img.alt = file.name
    placeholder.replaceWith(img)
  } catch {
    placeholder.textContent = '❌ upload failed'
  }
  // Sync value
  if (context && context.startsWith('cf-')) {
    const refName = context.slice(3)
    form.value.customFields[refName] = editorEl.innerHTML
  } else {
    onEditorInput()
  }
}

function mergedFields() {
  const merged = { ...templateFields.value }
  // Add custom field values (non-empty only)
  for (const [key, val] of Object.entries(form.value.customFields)) {
    if (val) merged[key] = val
  }
  return Object.keys(merged).length > 0 ? merged : null
}

async function doCreate() {
  creating.value = true
  createError.value = ''
  const proj = selectedProject.value
  if (!proj) { createError.value = 'No project selected'; creating.value = false; return }

  try {
    const payload = {
      organization: proj.organization,
      project: proj.project,
      work_item_type: props.workItemType,
      title: form.value.title.trim(),
      description: form.value.description || null,
      parent_id: props.parentId || null,
      iteration_path: form.value.iterationPath || null,
      fields: mergedFields(),
    }
    const result = await api.post('/api/roadmap/create-work-item', payload)
    emit('created', result)
    emit('update:open', false)
  } catch (e) {
    createError.value = e.message || 'Failed to create work item'
  } finally {
    creating.value = false
  }
}
</script>

<style scoped>
:deep([role="dialog"]),
:deep([role="dialog"] > *),
:deep([data-headlessui-state]),
:deep(.focus-visible),
:deep(:focus-visible) {
  outline: none !important;
  box-shadow: none !important;
  --tw-ring-shadow: none !important;
  --tw-ring-color: transparent !important;
  --tw-ring-offset-shadow: none !important;
}
</style>
