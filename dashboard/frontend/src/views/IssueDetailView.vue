<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
      </svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ checkData?.project_name || projectId }}</span>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
      </svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ checkData?.check_label || checkType }}</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-gray-900 dark:text-gray-100">{{ checkData?.check_label || checkType }}</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ checkData?.project_name || 'Unknown project' }}
          <span v-if="checkData" class="ml-2">· {{ checkData.flagged_items.length }} issue{{ checkData.flagged_items.length !== 1 ? 's' : '' }}</span>
        </p>
      </div>

      <div class="flex items-center gap-2">
        <template v-if="checkType === 'missing_estimate_check' && store.emailFromConfigured && assignees.length > 0 && !isDemoMode">
          <button
            v-for="a in assignees" :key="a.name"
            @click="openPersonMail(a.name, a.email)"
            :disabled="sendingMailFor === a.name"
            class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-primary-600 dark:text-primary-400 bg-white dark:bg-gray-800 border border-primary-300 dark:border-primary-600 rounded-lg hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors disabled:opacity-50"
          >
            <svg v-if="sendingMailFor !== a.name" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M21.75 6.75v10.5a2.25 2.25 0 01-2.25 2.25h-15a2.25 2.25 0 01-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0019.5 4.5h-15a2.25 2.25 0 00-2.25 2.25m19.5 0v.243a2.25 2.25 0 01-1.07 1.916l-7.5 4.615a2.25 2.25 0 01-2.36 0L3.32 8.91a2.25 2.25 0 01-1.07-1.916V6.75" />
            </svg>
            {{ sendingMailFor === a.name ? 'Sending…' : a.firstName }}
          </button>
        </template>

        <button
          v-if="checkType === 'orphan_check' && parentDoneItems.length > 0"
          @click="copyParentDoneItems"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          :title="`Copy ${parentDoneItems.length} parent-done item(s) to clipboard`"
        >
          <svg v-if="!copied" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15.666 3.888A2.25 2.25 0 0013.5 2.25h-3c-1.03 0-1.9.693-2.166 1.638m7.332 0c.055.194.084.4.084.612v0a.75.75 0 01-.75.75H9.75a.75.75 0 01-.75-.75v0c0-.212.03-.418.084-.612m7.332 0c.646.049 1.288.11 1.927.184 1.1.128 1.907 1.077 1.907 2.185V19.5a2.25 2.25 0 01-2.25 2.25H6.75A2.25 2.25 0 014.5 19.5V6.257c0-1.108.806-2.057 1.907-2.185a48.208 48.208 0 011.927-.184" />
          </svg>
          <svg v-else class="w-4 h-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
          </svg>
          {{ copied ? 'Copied!' : `Copy parent done (${parentDoneItems.length})` }}
        </button>

        <button
          v-if="checkType === 'orphan_check' && checkData?.flagged_items?.length > 0"
          @click="copyAllItems"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          :title="`Copy all ${checkData.flagged_items.length} item(s) with links to clipboard`"
        >
          <svg v-if="!copiedAll" class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 7.5V6.108c0-1.135.845-2.098 1.976-2.192.373-.03.748-.057 1.123-.08M15.75 18H18a2.25 2.25 0 002.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 00-1.123-.08M15.75 18.75v-1.875a3.375 3.375 0 00-3.375-3.375h-1.5a1.125 1.125 0 01-1.125-1.125v-1.5A3.375 3.375 0 006.375 7.5H5.25m11.9-3.664A2.251 2.251 0 0015 2.25h-1.5a2.251 2.251 0 00-2.15 1.586m5.8 0c.065.21.1.433.1.664v.75h-6V4.5c0-.231.035-.454.1-.664M6.75 7.5H4.875c-.621 0-1.125.504-1.125 1.125v12c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V16.5a9 9 0 00-9-9z" />
          </svg>
          <svg v-else class="w-4 h-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
          </svg>
          {{ copiedAll ? 'Copied!' : `Copy all (${checkData.flagged_items.length})` }}
        </button>

        <button
          v-if="checkData && checkData.flagged_items.length"
          @click="exportIssues"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          title="Export to CSV"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" /></svg>
          CSV
        </button>

        <router-link
          to="/"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
          </svg>
          Back
        </router-link>
      </div>
    </div>

    <!-- Operation status banner -->
    <div v-if="operationLoading" class="mb-4 flex items-center gap-2 px-4 py-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-700 rounded-lg text-sm text-blue-700 dark:text-blue-300">
      <svg class="w-4 h-4 animate-spin flex-shrink-0" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
      </svg>
      {{ operationMessage }}
    </div>
    <div v-if="operationError" class="mb-4 px-4 py-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-700 rounded-lg text-sm text-red-700 dark:text-red-300">
      {{ operationError }}
    </div>
    <div v-if="operationSuccess" class="mb-4 px-4 py-3 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-700 rounded-lg text-sm text-green-700 dark:text-green-300">
      {{ operationSuccess }}
    </div>

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
            <input
              id="mailTo"
              v-model="mailTo"
              type="email"
              placeholder="recipient@example.com"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
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
          <button @click="closeMailDialog" class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-600 transition-colors">
            Cancel
          </button>
          <button @click="sendMail" :disabled="sendingMail || !mailTo || !store.emailFromConfigured || isDemoMode" class="px-4 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 disabled:opacity-50 transition-colors">
            {{ isDemoMode ? 'Disabled in demo mode' : sendingMail ? 'Sending…' : 'Send' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useApi } from '../composables/useApi.js'
import { useDemoMode } from '../composables/useDemoMode.js'
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
