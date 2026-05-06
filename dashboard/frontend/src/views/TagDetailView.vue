<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
      </svg>
      <router-link
        :to="{ name: 'issues', params: { projectId, checkType: 'tag_overview_check' } }"
        class="hover:text-primary-600 transition-colors"
      >{{ projectName }}</router-link>
      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
      </svg>
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ tagName }}</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">
          <span class="inline-flex items-center gap-2">
            <svg class="w-6 h-6 text-sky-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M9.568 3H5.25A2.25 2.25 0 003 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 005.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 009.568 3z" />
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 6h.008v.008H6V6z" />
            </svg>
            {{ tagName }}
          </span>
        </h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ projectName }}
          <span v-if="items.length" class="ml-2">· {{ items.length }} work item{{ items.length !== 1 ? 's' : '' }}</span>
        </p>
      </div>

      <div class="flex items-center gap-2">
        <button
          @click="$router.back()"
          class="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
          </svg>
          Back
        </button>
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

    <!-- Loading -->
    <div v-if="loading" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-6 py-12 text-center">
      <svg class="w-8 h-8 mx-auto text-primary-500 animate-spin mb-3" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
      </svg>
      <p class="text-gray-500 text-sm">Loading work items…</p>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="bg-white dark:bg-gray-800 rounded-xl border border-red-200 dark:border-red-700 shadow-sm px-6 py-12 text-center">
      <svg class="w-12 h-12 mx-auto text-red-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
      </svg>
      <p class="text-red-600 dark:text-red-400 text-sm">{{ error }}</p>
    </div>

    <!-- No items -->
    <div v-else-if="items.length === 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-6 py-12 text-center">
      <svg class="w-12 h-12 mx-auto text-gray-300 mb-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
        <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 7.5l-.625 10.632a2.25 2.25 0 01-2.247 2.118H6.622a2.25 2.25 0 01-2.247-2.118L3.75 7.5M10 11.25h4M3.375 7.5h17.25c.621 0 1.125-.504 1.125-1.125v-1.5c0-.621-.504-1.125-1.125-1.125H3.375c-.621 0-1.125.504-1.125 1.125v1.5c0 .621.504 1.125 1.125 1.125z" />
      </svg>
      <p class="text-gray-500 text-sm">No work items found with this tag.</p>
    </div>

    <!-- Work items table -->
    <div v-else class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
      <IssueTable
        :items="items"
        check-type="tag_detail"
        :project-id="projectId"
        :tag-name="tagName"
        :all-tags="allTags"
        :all-tags-loading="allTagsLoading"
        :busy-item-id="busyItemId"
        @remove-item-tag="handleRemoveTag"
        @rename-item-tag="handleRenameTag"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useApi } from '../composables/useApi.js'
import { useMonitorStore } from '../stores/monitor.js'
import { transformTagItems } from '../composables/demoTransform.js'
import { anonProject } from '../composables/useDemoMode.js'
import { useDemoMode } from '../composables/useDemoMode.js'
import IssueTable from '../components/IssueTable.vue'

const props = defineProps({
  projectId: { type: String, required: true },
  tagName: { type: String, required: true },
})

const api = useApi()
const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

const rawItems = ref([])
const items = ref([])
const loading = ref(true)
const error = ref('')

const projectName = ref(props.projectId)
const projectData = store.results?.projects?.find(p => p.project_id === props.projectId)
if (projectData) projectName.value = isDemoMode.value ? anonProject(projectData.project_name) : projectData.project_name

// --- Tag list for rename dropdowns ---
const allTags = ref([])
const allTagsLoading = ref(false)

async function loadTags() {
  if (allTags.value.length > 0) return
  allTagsLoading.value = true
  try {
    const data = await api.get(`/api/checks/tags/${props.projectId}`)
    allTags.value = (data.tags || []).sort((a, b) => a.localeCompare(b))
  } catch { /* ignore */ }
  finally { allTagsLoading.value = false }
}

// --- Per-item operations ---
const busyItemId = ref(null)
const operationLoading = ref(false)
const operationMessage = ref('')
const operationError = ref('')
const operationSuccess = ref('')

function clearStatus() {
  operationError.value = ''
  operationSuccess.value = ''
}

async function handleRemoveTag(workItemId) {
  const item = items.value.find(i => i.id === workItemId)
  if (!item) return
  clearStatus()
  busyItemId.value = workItemId
  operationLoading.value = true
  operationMessage.value = `Removing tag from #${workItemId}…`
  try {
    await api.post(`/api/checks/tag-remove/${props.projectId}`, {
      tag: props.tagName,
      work_item_id: workItemId,
    })
    items.value = items.value.filter(i => i.id !== workItemId)
    operationSuccess.value = `Tag removed from #${workItemId}.`
  } catch (e) {
    operationError.value = e.message || 'Failed to remove tag'
  } finally {
    operationLoading.value = false
    operationMessage.value = ''
    busyItemId.value = null
  }
}

async function handleRenameTag(workItemId, newTag) {
  const item = items.value.find(i => i.id === workItemId)
  if (!item) return
  clearStatus()
  busyItemId.value = workItemId
  operationLoading.value = true
  operationMessage.value = `Renaming tag to "${newTag}" on #${workItemId}…`
  try {
    await api.post(`/api/checks/tag-rename/${props.projectId}`, {
      tag: props.tagName,
      new_tag: newTag,
      work_item_id: workItemId,
    })
    items.value = items.value.filter(i => i.id !== workItemId)
    operationSuccess.value = `Tag renamed to "${newTag}" on #${workItemId}.`
  } catch (e) {
    operationError.value = e.message || 'Failed to rename tag'
  } finally {
    operationLoading.value = false
    operationMessage.value = ''
    busyItemId.value = null
  }
}

// --- Load items ---
onMounted(async () => {
  loadTags()
  try {
    const data = await api.get(`/api/checks/tag-items/${props.projectId}?tag=${encodeURIComponent(props.tagName)}`)
    rawItems.value = (data.items || []).map((wi) => ({
      id: wi.id,
      title: wi.title,
      url: wi.url,
      work_item_type: wi.work_item_type,
      assigned_to: wi.assigned_to,
      state: wi.state,
    }))
    items.value = transformTagItems(rawItems.value)
  } catch (e) {
    error.value = e.message || 'Failed to load work items'
  } finally {
    loading.value = false
  }
})
</script>
