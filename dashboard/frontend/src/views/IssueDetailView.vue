<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ checkData?.project_name || projectId }}</span>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ checkData?.check_label || checkType }}</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">{{ checkData?.check_label || checkType }}</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ checkData?.project_name || 'Unknown project' }}
          <span v-if="checkData" class="ml-2">· {{ checkData.flagged_items.length }} issue{{ checkData.flagged_items.length !== 1 ? 's' : '' }}</span>
        </p>
      </div>

      <div class="flex items-center gap-2">
        <template v-if="checkType === 'missing_estimate_check' && store.emailFromConfigured && assignees.length > 0 && !isDemoMode">
          <UButton
            v-for="a in assignees" :key="a.name"
            @click="openPersonMail(a.name, a.email)"
            :disabled="sendingMailFor === a.name"
            :loading="sendingMailFor === a.name"
            variant="outline" color="primary"
            :icon="sendingMailFor !== a.name ? 'i-heroicons-envelope' : undefined"
            :label="sendingMailFor === a.name ? 'Sending…' : a.firstName"
          />
        </template>

        <UButton
          v-if="checkType === 'orphan_check' && parentDoneItems.length > 0"
          @click="copyParentDoneItems"
          variant="outline"
          color="neutral"
          :icon="copied ? 'i-heroicons-check-circle' : 'i-heroicons-clipboard'"
          :title="`Copy ${parentDoneItems.length} parent-done item(s) to clipboard`"
        >
          {{ copied ? 'Copied!' : `Copy parent done (${parentDoneItems.length})` }}
        </UButton>

        <UButton
          v-if="checkType === 'orphan_check' && checkData?.flagged_items?.length > 0"
          @click="copyAllItems"
          variant="outline"
          color="neutral"
          :icon="copiedAll ? 'i-heroicons-check-circle' : 'i-heroicons-clipboard-document-list'"
          :title="`Copy all ${checkData.flagged_items.length} item(s) with links to clipboard`"
        >
          {{ copiedAll ? 'Copied!' : `Copy all (${checkData.flagged_items.length})` }}
        </UButton>

        <UButton
          v-if="checkData && checkData.flagged_items.length"
          @click="exportIssues"
          variant="outline"
          color="neutral"
          icon="i-heroicons-arrow-down-tray"
          title="Export to CSV"
        >
          CSV
        </UButton>

        <UButton to="/" variant="outline" color="neutral" icon="i-heroicons-arrow-left">
          Back
        </UButton>
      </div>
    </div>

    <!-- Operation status banner -->
    <div v-if="operationLoading" class="mb-4 flex items-center gap-2 px-4 py-3 bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-700 rounded-lg text-sm text-blue-700 dark:text-blue-300">
      <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin flex-shrink-0" />
      {{ operationMessage }}
    </div>
    <UAlert v-if="operationError" color="error" icon="i-heroicons-x-circle" :description="operationError" class="mb-4" />
    <UAlert v-if="operationSuccess" color="success" icon="i-heroicons-check-circle" :description="operationSuccess" class="mb-4" />

    <!-- Loading skeleton (store still running checks) -->
    <div v-if="!checkData && store.runningChecks" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden animate-pulse">
      <div class="px-4 py-3 border-b border-gray-200 dark:border-gray-700">
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-48"></div>
      </div>
      <div v-for="n in 5" :key="n" class="px-4 py-3 border-b border-gray-100 dark:border-gray-700 flex items-center gap-4">
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-12"></div>
        <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded flex-1"></div>
        <div class="h-4 bg-gray-100 dark:bg-gray-600 rounded w-28"></div>
        <div class="h-5 bg-gray-100 dark:bg-gray-600 rounded-full w-20"></div>
      </div>
    </div>

    <!-- No data -->
    <EmptyState v-else-if="!checkData" icon="archive" message="No data available. Run checks from the dashboard first." action-label="Go to Dashboard" action-to="/" />

    <!-- Issue table -->
    <div v-else class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
      <IssueTable
        :items="checkData.flagged_items"
        :check-type="checkType"
        :project-id="projectId"
        :all-tags="allTags"
        :all-tags-loading="allTagsLoading"
        :busy-item-id="busyItemId"
        @delete-tag="onDeleteTag"
        @remove-tag="onBulkRemoveTag"
        @rename-tag="onBulkRenameTag"
        @assign-parent="onAssignParent"
        @preview-item="onPreviewItem"
      />
    </div>

    <!-- Send mail dialog -->
    <div v-if="showMailDialog" class="fixed inset-0 bg-black/40 z-40 flex items-center justify-center" @click.self="closeMailDialog">
      <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-full max-w-sm mx-4 overflow-hidden">
        <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700">
          <h3 class="text-lg font-semibold text-gray-900 dark:text-gray-100">
            {{ mailAssignedTo ? `Send mail to ${mailAssignedTo}` : 'Send mail' }}
          </h3>
        </div>
        <div class="px-6 py-5 space-y-3">
          <div>
            <label for="mailTo" class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">To</label>
            <UInput
              id="mailTo"
              name="mail-to" v-model="mailTo"
              type="email"
              placeholder="recipient@example.com"
            />
          </div>
          <p v-if="mailAssignedTo" class="text-xs text-gray-500 dark:text-gray-400">
            Only {{ mailAssignedTo }}'s {{ personItemCount }} item(s) will be included.
          </p>
          <p v-if="store.emailFrom" class="text-xs text-gray-500 dark:text-gray-400">From: {{ store.emailFrom }}</p>
          <p v-else class="text-xs text-amber-600 dark:text-amber-400">No from address configured. Set it in Settings.</p>
          <p v-if="mailResult" class="text-xs" :class="mailSuccess ? 'text-green-600' : 'text-red-600'">{{ mailResult }}</p>
        </div>
        <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
          <UButton variant="outline" color="neutral" @click="closeMailDialog">
            Cancel
          </UButton>
          <UButton icon="i-heroicons-paper-airplane" @click="sendMail" :disabled="!mailTo || !store.emailFromConfigured || isDemoMode" :loading="sendingMail">
            {{ isDemoMode ? 'Disabled in demo mode' : 'Send' }}
          </UButton>
        </div>
      </div>
    </div>
    <!-- Assign parent dialog -->
    <div v-if="showParentDialog" class="fixed inset-0 bg-black/40 z-40 flex items-center justify-center" @click.self="closeParentDialog">
      <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-[75vw] max-h-[90vh] mx-4 overflow-hidden">
        <div class="px-6 py-4 border-b border-primary-200 dark:border-gray-700 bg-primary-50 dark:bg-transparent">
          <h3 class="text-lg font-semibold text-primary-900 dark:text-gray-100">
            {{ isParentDoneItem ? 'Change' : 'Assign' }} Parent for #{{ parentAssignItem?.id }}
            <span class="font-normal text-primary-600 dark:text-gray-100 ml-2">{{ parentAssignItem?.title }}</span>
          </h3>
        </div>

        <!-- Confirmation sub-view -->
        <div v-if="selectedParentCandidate" class="px-6 py-5 space-y-4">
          <p class="text-sm text-gray-400 dark:text-gray-400">Link the following parent:</p>
          <div class="rounded-lg border border-gray-200 dark:border-gray-600 p-4 space-y-3">
            <div>
              <span class="text-xs font-bold uppercase tracking-wider text-primary-500 dark:text-primary-400">Parent</span>
              <p class="text-base font-semibold text-gray-900 dark:text-gray-100 mt-0.5">{{ selectedParentCandidate.title }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ selectedParentCandidate.work_item_type }} #{{ selectedParentCandidate.id }}</p>
            </div>
            <div class="flex items-center gap-2 text-gray-400 dark:text-gray-300">
              <UIcon name="i-heroicons-link" class="w-4 h-4" />
            </div>
            <div>
              <span class="text-xs font-bold uppercase tracking-wider text-primary-500 dark:text-primary-400">Child</span>
              <p class="text-base font-semibold text-gray-900 dark:text-gray-100 mt-0.5">{{ parentAssignItem?.title }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ parentAssignItem?.work_item_type }} #{{ parentAssignItem?.id }}</p>
            </div>
          </div>
          <p v-if="parentAssignError" class="text-xs text-red-600 dark:text-red-400">{{ parentAssignError }}</p>
          <div class="flex justify-end gap-3 pt-1">
            <UButton variant="outline" color="neutral" @click="selectedParentCandidate = null; parentAssignError = ''">
              Back
            </UButton>
            <UButton @click="confirmAssignParent" :loading="assigningParent">
              Confirm
            </UButton>
          </div>
        </div>

        <!-- Candidate list -->
        <div v-else>
          <div class="px-6 py-3 border-b border-gray-100 dark:border-gray-700">
            <UInput
              ref="parentSearchInput"
              name="parent-search" v-model="parentSearch"
              type="text"
              placeholder="Filter candidates…"
              icon="i-heroicons-magnifying-glass"
              class="w-full"
            />
          </div>
          <div class="max-h-[30rem] overflow-y-auto">
            <div v-if="loadingCandidates" class="flex items-center gap-2 px-6 py-8 justify-center text-sm text-gray-500">
              <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
              Loading candidates…
            </div>
            <div v-else-if="filteredCandidates.length === 0" class="px-6 py-8 text-center text-sm text-gray-400 dark:text-gray-300">
              No candidate parents found.
            </div>
            <div v-else>
              <button
                v-for="c in filteredCandidates" :key="c.id"
                @click="selectedParentCandidate = c"
                class="w-full text-left px-5 py-2.5 border-b border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors grid items-center gap-x-3"
                style="grid-template-columns: 4.5rem 5.5rem 1fr auto auto"
              >
                <span class="text-xs text-gray-500 dark:text-gray-400">#{{ c.id }}</span>
                <span class="text-xs text-gray-500 dark:text-gray-400">{{ c.work_item_type }}</span>
                <span class="text-sm font-medium text-gray-800 dark:text-gray-200 truncate">{{ c.title }}</span>
                <span class="text-xs text-gray-500 dark:text-gray-400">{{ c.state }}</span>
                <span class="text-xs text-gray-400 dark:text-gray-400">{{ formatCandidateArea(c.area_path) }}</span>
              </button>
            </div>
          </div>
        </div>

        <div v-if="!selectedParentCandidate" class="px-6 py-3 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex items-center justify-between">
          <span class="text-xs text-gray-500 dark:text-gray-400">Press <kbd class="px-1 py-0.5 text-xs font-mono bg-gray-100 dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded">Esc</kbd> to close</span>
          <UButton variant="outline" color="neutral" @click="closeParentDialog">
            Cancel
          </UButton>
        </div>
      </div>
    </div>
    <!-- Work item preview dialog -->
    <div v-if="showPreviewDialog" class="fixed inset-0 bg-black/40 z-40 flex items-center justify-center" @click.self="closePreviewDialog">
      <div class="bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-[95vw] max-w-7xl max-h-[90vh] mx-4 overflow-hidden flex flex-col">
        <div class="px-6 py-4 border-b border-primary-200 dark:border-gray-700 bg-primary-50 dark:bg-transparent shrink-0">
          <h3 class="text-lg font-semibold text-primary-900 dark:text-gray-100">
            #{{ previewItem?.id }}
            <span class="font-normal text-primary-600 dark:text-gray-100 ml-2">{{ previewItem?.title }}</span>
          </h3>
          <div class="flex items-center gap-3 mt-2 text-xs text-gray-500 dark:text-gray-400">
            <span class="inline-block px-2 py-0.5 rounded-md font-medium bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">{{ previewData?.work_item_type || previewItem?.work_item_type }}</span>
            <span class="inline-block px-2 py-0.5 rounded-md font-medium bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">{{ previewData?.state || previewItem?.state || '—' }}</span>
            <span v-if="previewData?.assigned_to">{{ isDemoMode ? anonName(previewData.assigned_to) : previewData.assigned_to }}</span>
            <span v-if="previewData?.iteration_path">{{ isDemoMode ? anonIterationPath(previewData.iteration_path) : previewData.iteration_path }}</span>
          </div>
          <div v-if="previewTags.length" class="flex flex-wrap items-center gap-1.5 mt-2">
            <UIcon name="i-heroicons-tag" class="w-3.5 h-3.5 text-gray-400 dark:text-gray-500" />
            <span v-for="tag in previewTags" :key="tag" class="inline-block px-2 py-0.5 rounded-full text-xs font-medium bg-primary-100 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300">{{ tag }}</span>
          </div>
        </div>
        <div class="flex-1 overflow-y-auto px-6 py-5">
          <div v-if="loadingPreview" class="flex items-center gap-2 py-8 justify-center text-sm text-gray-500">
            <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
            Loading description…
          </div>
          <div v-else-if="previewError" class="text-sm text-red-600 dark:text-red-400">{{ previewError }}</div>
          <template v-else>
            <div v-if="!sanitizedDescription" class="text-sm text-gray-400 dark:text-gray-500 italic">No description available.</div>
            <div v-else class="prose dark:prose-invert max-w-none work-item-description" v-html="sanitizedDescription"></div>
            <!-- Comments -->
            <div v-if="previewData?.comments?.length" class="mt-6 border-t border-gray-200 dark:border-gray-700 pt-4">
              <h4 class="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-3 flex items-center gap-1.5">
                <UIcon name="i-heroicons-chat-bubble-left-right" class="w-4 h-4" />
                Comments ({{ previewData.comments.length }})
              </h4>
              <div class="space-y-3">
                <div v-for="(comment, idx) in previewData.comments" :key="idx" class="bg-gray-50 dark:bg-gray-700/50 rounded-lg px-4 py-3">
                  <div class="flex items-center gap-2 mb-1.5 text-xs text-gray-500 dark:text-gray-400">
                    <span class="font-medium text-gray-700 dark:text-gray-300">{{ comment.author || 'Unknown' }}</span>
                    <span v-if="comment.created_date">· {{ formatCommentDate(comment.created_date) }}</span>
                  </div>
                  <div class="prose dark:prose-invert prose-sm max-w-none work-item-description" v-html="sanitizeHtml(comment.text)"></div>
                </div>
              </div>
            </div>
          </template>
        </div>
        <div class="px-6 py-3 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex items-center justify-between shrink-0">
          <span class="text-xs text-gray-500 dark:text-gray-400">Press <kbd class="px-1 py-0.5 text-xs font-mono bg-gray-100 dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded">Esc</kbd> to close</span>
          <UButton variant="outline" color="neutral" @click="closePreviewDialog">
            Close
          </UButton>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import DOMPurify from 'dompurify'
import { useMonitorStore } from '../stores/monitor.js'
import { useApi } from '../composables/useApi.js'
import { useDemoMode, anonIterationPath, anonName } from '../composables/useDemoMode.js'
import { useCsvExport } from '../composables/useCsvExport.js'
import IssueTable from '../components/IssueTable.vue'
import EmptyState from '../components/EmptyState.vue'

const props = defineProps({
  projectId: { type: String, required: true },
  checkType: { type: String, required: true },
})

const store = useMonitorStore()
const api = useApi()
const { isDemoMode } = useDemoMode()
const { exportCsv } = useCsvExport()

const checkData = computed(() => {
  return store.issuesByProjectAndType(props.projectId, props.checkType)
})

const parentDoneItems = computed(() => {
  if (!checkData.value) return []
  return checkData.value.flagged_items.filter(i =>
    (i.work_item_type || '').toLowerCase().includes('[parent done]')
  )
})

const copied = ref(false)
function copyParentDoneItems() {
  const lines = parentDoneItems.value.map(i => i.id).join(',')
  navigator.clipboard.writeText(lines).then(() => {
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  })
}

const copiedAll = ref(false)

function copyAllItems() {
  const lines = checkData.value.flagged_items.map(i =>
    `${i.id} - ${i.title} (${i.work_item_type}) ${i.url}`
  ).join('\n')
  navigator.clipboard.writeText(lines).then(() => {
    copiedAll.value = true
    setTimeout(() => { copiedAll.value = false }, 2000)
  })
}

// --- Send mail ---
const showMailDialog = ref(false)
const mailTo = ref('')
const mailAssignedTo = ref(null)
const sendingMail = ref(false)
const sendingMailFor = ref(null)
const mailResult = ref('')
const mailSuccess = ref(false)

const assignees = computed(() => {
  if (!checkData.value) return []
  const map = {}
  for (const item of checkData.value.flagged_items) {
    const name = item.assigned_to || ''
    if (!name) continue
    if (!map[name]) {
      map[name] = {
        name,
        firstName: name.split(' ')[0],
        email: item.assigned_to_email || '',
        count: 0,
      }
    }
    map[name].count++
  }
  return Object.values(map).sort((a, b) => a.name.localeCompare(b.name))
})

const personItemCount = computed(() => {
  if (!mailAssignedTo.value || !checkData.value) return 0
  return checkData.value.flagged_items.filter(i => i.assigned_to === mailAssignedTo.value).length
})

function openPersonMail(assignedTo, email) {
  mailAssignedTo.value = assignedTo
  mailTo.value = email || ''
  mailResult.value = ''
  mailSuccess.value = false
  showMailDialog.value = true
}

function closeMailDialog() {
  showMailDialog.value = false
  mailAssignedTo.value = null
  mailResult.value = ''
}

async function sendMail() {
  sendingMail.value = true
  if (mailAssignedTo.value) sendingMailFor.value = mailAssignedTo.value
  mailResult.value = ''
  try {
    const res = await store.sendCheckMail(props.projectId, props.checkType, mailTo.value, mailAssignedTo.value)
    mailResult.value = res.message || 'Sent!'
    mailSuccess.value = true
    setTimeout(() => { closeMailDialog(); sendingMailFor.value = null }, 1500)
  } catch (e) {
    mailResult.value = e.message || 'Failed to send'
    mailSuccess.value = false
    sendingMailFor.value = null
  } finally {
    sendingMail.value = false
  }
}

// --- Tag overview bulk operations ---
const allTags = ref([])
const allTagsLoading = ref(false)
const busyItemId = ref(null)
const operationLoading = ref(false)
const operationMessage = ref('')
const operationError = ref('')
const operationSuccess = ref('')

function clearStatus() {
  operationError.value = ''
  operationSuccess.value = ''
}

const isTagOverview = computed(() => props.checkType === 'tag_overview_check')

async function loadTags() {
  if (!isTagOverview.value || allTags.value.length > 0) return
  allTagsLoading.value = true
  try {
    const data = await api.get(`/api/checks/tags/${props.projectId}`)
    allTags.value = (data.tags || []).sort((a, b) => a.localeCompare(b))
  } catch { /* ignore */ }
  finally { allTagsLoading.value = false }
}

onMounted(() => { loadTags() })

function exportIssues() {
  const items = checkData.value?.flagged_items
  if (!items?.length) return
  const cols = [
    { key: 'id', label: 'ID' },
    { key: 'title', label: 'Title' },
    { key: 'work_item_type', label: 'Type' },
    { key: 'assigned_to', label: 'Assigned To' },
    { key: 'state', label: 'State' },
    { key: 'iteration_path', label: 'Iteration' },
    { key: 'created_date', label: 'Created' },
    { key: 'url', label: 'URL' },
  ]
  const name = `${checkData.value.project_name}_${props.checkType}`
  exportCsv(items, cols, name)
}

async function onDeleteTag(tagName) {
  const project = store.results?.projects?.find(p => p.project_id === props.projectId)
  if (!project) return
  clearStatus()
  operationLoading.value = true
  operationMessage.value = `Deleting tag "${tagName}"…`
  try {
    await api.post('/api/devops/tags/delete', {
      organization: project.organization,
      project: project.project_name,
      tag_name: tagName,
    })
    if (checkData.value) {
      const idx = checkData.value.flagged_items.findIndex(i => i.title === tagName)
      if (idx !== -1) checkData.value.flagged_items.splice(idx, 1)
    }
    operationSuccess.value = `Tag "${tagName}" deleted.`
  } catch (e) {
    operationError.value = 'Failed to delete tag: ' + (e.message || 'Unknown error')
  } finally {
    operationLoading.value = false
    operationMessage.value = ''
  }
}

async function onBulkRemoveTag(tagName) {
  clearStatus()
  operationLoading.value = true
  operationMessage.value = `Removing tag "${tagName}" from all work items…`
  try {
    const res = await api.post(`/api/checks/tag-remove/${props.projectId}`, { tag: tagName })
    if (checkData.value) {
      const item = checkData.value.flagged_items.find(i => i.title === tagName)
      if (item) item.work_item_type = '0 work items'
    }
    operationSuccess.value = `Tag "${tagName}" removed from ${res.updated || 0} work item(s).`
  } catch (e) {
    operationError.value = 'Failed to remove tag: ' + (e.message || 'Unknown error')
  } finally {
    operationLoading.value = false
    operationMessage.value = ''
  }
}

// --- Assign parent ---
const showParentDialog = ref(false)
const parentAssignItem = ref(null)
const parentCandidates = ref([])
const loadingCandidates = ref(false)
const parentSearch = ref('')
const parentSearchInput = ref(null)
const selectedParentCandidate = ref(null)
const assigningParent = ref(false)
const parentAssignError = ref('')

function onEsc(e) {
  if (e.key === 'Escape' && showParentDialog.value) closeParentDialog()
}
watch(showParentDialog, (open) => {
  if (open) document.addEventListener('keydown', onEsc)
  else document.removeEventListener('keydown', onEsc)
})
onUnmounted(() => document.removeEventListener('keydown', onEsc))

const filteredCandidates = computed(() => {
  if (!parentSearch.value) return parentCandidates.value
  const q = parentSearch.value.toLowerCase()
  return parentCandidates.value.filter(c =>
    String(c.id).includes(q) ||
    c.title.toLowerCase().includes(q) ||
    c.work_item_type.toLowerCase().includes(q)
  )
})

const isParentDoneItem = computed(() =>
  (parentAssignItem.value?.work_item_type || '').toLowerCase().includes('[parent done]')
)

function formatCandidateArea(areaPath) {
  if (!areaPath) return ''
  const parts = areaPath.split('\\')
  return parts[parts.length - 1]
}

async function onAssignParent(item) {
  parentAssignItem.value = item
  parentCandidates.value = []
  parentSearch.value = ''
  selectedParentCandidate.value = null
  parentAssignError.value = ''
  showParentDialog.value = true
  loadingCandidates.value = true
  try {
    const cleanType = (item.work_item_type || '').replace(/\s*\[.*\]$/, '')
    const res = await store.fetchCandidateParents(props.projectId, item.id, cleanType)
    parentCandidates.value = res.candidates || []
  } catch (e) {
    parentCandidates.value = []
    parentAssignError.value = e.message || 'Failed to load candidates'
  } finally {
    loadingCandidates.value = false
    nextTick(() => {
      const el = parentSearchInput.value?.$el?.querySelector('input') || parentSearchInput.value
      el?.focus()
    })
  }
}

function closeParentDialog() {
  showParentDialog.value = false
  parentAssignItem.value = null
  selectedParentCandidate.value = null
  parentAssignError.value = ''
}

// --- Work item preview ---
const showPreviewDialog = ref(false)
const previewItem = ref(null)
const previewData = ref(null)
const loadingPreview = ref(false)
const previewError = ref('')

const sanitizedDescription = computed(() => {
  if (!previewData.value?.description) return ''
  return DOMPurify.sanitize(previewData.value.description, {
    ADD_TAGS: ['img'],
    ADD_ATTR: ['src', 'alt', 'width', 'height', 'style'],
  })
})

const previewTags = computed(() => {
  if (!previewData.value?.tags) return []
  return previewData.value.tags.split(';').map(t => t.trim()).filter(Boolean)
})

function sanitizeHtml(html) {
  return DOMPurify.sanitize(html || '', {
    ADD_TAGS: ['img'],
    ADD_ATTR: ['src', 'alt', 'width', 'height', 'style'],
  })
}

function formatCommentDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d)) return dateStr
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) +
    ' ' + d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
}

function onEscPreview(e) {
  if (e.key === 'Escape' && showPreviewDialog.value) closePreviewDialog()
}
watch(showPreviewDialog, (open) => {
  if (open) document.addEventListener('keydown', onEscPreview)
  else document.removeEventListener('keydown', onEscPreview)
})

async function onPreviewItem(item) {
  previewItem.value = item
  previewData.value = null
  previewError.value = ''
  showPreviewDialog.value = true
  loadingPreview.value = true
  try {
    previewData.value = await store.fetchWorkItemPreview(props.projectId, item.id)
  } catch (e) {
    previewError.value = e.message || 'Failed to load work item preview'
  } finally {
    loadingPreview.value = false
  }
}

function closePreviewDialog() {
  showPreviewDialog.value = false
  previewItem.value = null
  previewData.value = null
  previewError.value = ''
}

async function confirmAssignParent() {
  if (!parentAssignItem.value || !selectedParentCandidate.value) return
  assigningParent.value = true
  parentAssignError.value = ''
  try {
    await store.assignParent(props.projectId, parentAssignItem.value.id, selectedParentCandidate.value.id)
    if (checkData.value) {
      const idx = checkData.value.flagged_items.findIndex(i => i.id === parentAssignItem.value.id)
      if (idx !== -1) checkData.value.flagged_items.splice(idx, 1)
    }
    closeParentDialog()
    operationSuccess.value = `Parent assigned: #${selectedParentCandidate.value.id} → #${parentAssignItem.value.id}`
  } catch (e) {
    parentAssignError.value = e.message || 'Failed to assign parent'
  } finally {
    assigningParent.value = false
  }
}

async function onBulkRenameTag(tagName, newTag) {
  clearStatus()
  operationLoading.value = true
  operationMessage.value = `Renaming tag "${tagName}" to "${newTag}"…`
  try {
    const res = await api.post(`/api/checks/tag-rename/${props.projectId}`, { tag: tagName, new_tag: newTag })
    if (checkData.value) {
      const idx = checkData.value.flagged_items.findIndex(i => i.title === tagName)
      if (idx !== -1) checkData.value.flagged_items.splice(idx, 1)
      // Update the target tag count if it exists in the list
      const target = checkData.value.flagged_items.find(i => i.title === newTag)
      if (target) {
        const count = parseInt(target.work_item_type) || 0
        target.work_item_type = `${count + (res.updated || 0)} work items`
      }
    }
    operationSuccess.value = `Tag "${tagName}" renamed to "${newTag}" on ${res.updated || 0} work item(s).`
  } catch (e) {
    operationError.value = 'Failed to rename tag: ' + (e.message || 'Unknown error')
  } finally {
    operationLoading.value = false
    operationMessage.value = ''
  }
}
</script>
