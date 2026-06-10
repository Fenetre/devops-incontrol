<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <router-link
        :to="{ name: 'issues', params: { projectId, checkType: 'tag_overview_check' } }"
        class="hover:text-primary-600 transition-colors"
      >{{ projectName }}</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">{{ tagName }}</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">
          <span class="inline-flex items-center gap-2">
            <UIcon name="i-heroicons-tag" class="w-6 h-6 text-sky-500" />
            {{ tagName }}
          </span>
        </h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          {{ projectName }}
          <span v-if="items.length" class="ml-2">· {{ items.length }} work item{{ items.length !== 1 ? 's' : '' }}</span>
        </p>
      </div>

      <div class="flex items-center gap-2">
        <UButton
          @click="$router.back()"
          variant="outline"
          color="neutral"
          icon="i-heroicons-arrow-left"
        >
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

    <!-- Loading -->
    <div v-if="loading" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-6 py-8 text-center">
      <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 mx-auto text-primary-500 animate-spin mb-3" />
      <p class="text-gray-500 text-sm">Loading work items…</p>
    </div>

    <!-- Error -->
    <div v-else-if="error" class="bg-white dark:bg-gray-800 rounded-xl border border-red-200 dark:border-red-700 shadow-sm px-6 py-12 text-center">
      <UIcon name="i-heroicons-exclamation-triangle" class="w-12 h-12 mx-auto text-red-300 mb-3" />
      <p class="text-red-600 dark:text-red-400 text-sm">{{ error }}</p>
    </div>

    <!-- No items -->
    <div v-else-if="items.length === 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-6 py-12 text-center">
      <UIcon name="i-heroicons-archive-box" class="w-12 h-12 mx-auto text-gray-300 mb-3" />
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
