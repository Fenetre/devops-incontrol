<template>
  <div>
    <!-- Search bar -->
    <div class="px-4 py-3 border-b border-gray-200 dark:border-gray-700">
      <UInput v-autofocus name="search" v-model="search" icon="i-heroicons-magnifying-glass" placeholder="Filter by ID, title, type, or assignee…" size="sm" class="w-full app-search" />
    </div>

    <!-- Table -->
    <div class="overflow-x-auto">
      <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
        <thead>
          <tr class="text-left border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-700/30">
            <th class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('id')">
              ID
              <span v-if="sortKey === 'id'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('title')">
              {{ isTagOverview ? 'Tag' : 'Title' }}
              <span v-if="sortKey === 'title'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('work_item_type')">
              {{ isTagOverview ? 'Count' : 'Type' }}
              <span v-if="sortKey === 'work_item_type'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasAssignedTo" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('assigned_to')">
              Assigned To
              <span v-if="sortKey === 'assigned_to'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasState" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('state')">
              State
              <span v-if="sortKey === 'state'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasIterationPath" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('iteration_path')">
              Iteration
              <span v-if="sortKey === 'iteration_path'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasCreatedDate" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider cursor-pointer select-none" @click="toggleSort('created_date')">
              Created
              <span v-if="sortKey === 'created_date'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="isOrphanCheck" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider text-right">Actions</th>
            <th v-if="isTagOverview" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider text-right">Actions</th>
            <th v-if="isTagDetail" class="px-4 py-3 text-xs font-medium text-gray-500 dark:text-gray-300 uppercase tracking-wider text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
            <tr
              v-for="item in paginatedItems" :key="item.id"
              class="border-b border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
            >
              <td class="px-4 py-3">
                <span v-if="isTagOverview" class="text-gray-500 dark:text-gray-300">#{{ item.id }}</span>
                <a
                  v-else
                  :href="item.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="text-primary-600 hover:text-primary-800 font-medium hover:underline"
                >
                  #{{ item.id }}
                </a>
              </td>
              <td class="px-4 py-3 text-gray-800 dark:text-gray-200 !whitespace-normal">
                <div class="flex items-center gap-1.5">
                  <router-link
                  v-if="isTagOverview && !isZeroCount(item)"
                  :to="{ name: 'tag-detail', params: { projectId: props.projectId, tagName: item.title } }"
                  class="text-primary-600 hover:text-primary-800 font-medium hover:underline"
                >
                  {{ item.title }}
                </router-link>
                <span v-else-if="isTagOverview" class="text-gray-500 dark:text-gray-300 italic">{{ item.title }}</span>
                <a
                  v-else
                  :href="item.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="hover:text-primary-600 hover:underline"
                >
                  {{ item.title }}
                </a>
                </div>
              </td>
              <td class="px-4 py-3 text-gray-600 dark:text-gray-300">
                {{ item.work_item_type || '—' }}
              </td>
              <td v-if="hasAssignedTo" class="px-4 py-3 text-gray-700 dark:text-gray-300">
                {{ item.assigned_to || '—' }}
              </td>
              <td v-if="hasState" class="px-4 py-3 whitespace-nowrap">
                <span class="inline-block px-2 py-0.5 rounded-md text-xs font-medium" :class="stateClass(item.state)">{{ item.state || '—' }}</span>
              </td>
              <td v-if="hasIterationPath" class="px-4 py-3 text-gray-600 dark:text-gray-300 whitespace-nowrap">
                {{ formatIterationPath(item.iteration_path) }}
              </td>
              <td v-if="hasCreatedDate" class="px-4 py-3 text-gray-600 dark:text-gray-300 whitespace-nowrap">
                {{ formatDate(item.created_date) }}
              </td>
              <td v-if="isOrphanCheck" class="px-4 py-3 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <UButton size="xs" variant="outline" icon="i-heroicons-eye" @click="emit('preview-item', item)" title="Preview work item">Preview</UButton>
                  <UButton size="xs" variant="outline" icon="i-heroicons-link" @click="emit('assign-parent', item)" :title="isParentDone(item) ? 'Change the parent work item' : 'Assign a parent work item'">{{ isParentDone(item) ? 'Change Parent' : 'Assign Parent' }}</UButton>
                </div>
              </td>
              <td v-if="isTagOverview" class="px-4 py-3 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <!-- Inline confirmation for remove/rename/delete -->
                  <template v-if="confirmingRemoveId === item.id && confirmAction === 'remove'">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Remove from all items?</span>
                    <UButton size="xs" color="error" icon="i-heroicons-check" @click="confirmBulkRemove(item.title)">Yes</UButton>
                    <UButton size="xs" variant="outline" color="neutral" @click="confirmingRemoveId = null; confirmAction = null">Cancel</UButton>
                  </template>
                  <template v-else-if="confirmingRemoveId === item.id && confirmAction === 'delete'">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Delete unused tag?</span>
                    <UButton size="xs" color="error" icon="i-heroicons-check" @click="confirmDelete(item.title)">Yes</UButton>
                    <UButton size="xs" variant="outline" color="neutral" @click="confirmingRemoveId = null; confirmAction = null">Cancel</UButton>
                  </template>
                  <template v-else-if="confirmingRemoveId === item.id && confirmAction === 'rename'">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Rename to "{{ pendingRenameTag?.newTag }}"?</span>
                    <UButton size="xs" color="warning" icon="i-heroicons-check" @click="confirmBulkRename()">Yes</UButton>
                    <UButton size="xs" variant="outline" color="neutral" @click="confirmingRemoveId = null; confirmAction = null; pendingRenameTag = null">Cancel</UButton>
                  </template>
                  <!-- Normal actions -->
                  <template v-else>
                    <!-- Rename (in-use tags only) -->
                    <div v-if="!isZeroCount(item)" class="relative" :ref="el => setRenameRef(item.id, el)">
                      <UButton size="xs" variant="outline" color="warning" icon="i-heroicons-pencil-square" :disabled="busyItemId === item.id" @click.stop="toggleItemRename(item.id)" title="Rename this tag on all work items">Rename</UButton>
                      <div v-if="renameOpenId === item.id" class="absolute right-0 top-full mt-1 z-50 w-56">
                        <USelectMenu
                          :items="renameFilteredTagsForOverview(item.title).map(t => ({ value: t, label: t }))"
                          value-key="value"
                          placeholder="Search tags…"
                          :loading="allTagsLoading"
                          size="xs"
                          class="w-full"
                          :open="true"
                          @update:model-value="val => { renameOpenId = null; selectBulkRenameTag(item, val) }"
                        />
                      </div>
                    </div>
                    <!-- Remove (in-use tags only) -->
                    <UButton
                      v-if="!isZeroCount(item)"
                      size="xs" variant="outline" color="error" icon="i-heroicons-x-circle"
                      :disabled="busyItemId === item.id"
                      @click="confirmingRemoveId = item.id; confirmAction = 'remove'"
                      title="Remove this tag from all work items"
                    >Remove</UButton>
                    <!-- Delete (zero-count tags only) -->
                    <UButton
                      v-if="isZeroCount(item)"
                      size="xs" variant="outline" color="error" icon="i-heroicons-trash"
                      @click="confirmingRemoveId = item.id; confirmAction = 'delete'"
                      title="Delete this unused tag"
                    >Delete</UButton>
                  </template>
                </div>
              </td>
              <!-- Per-item tag actions (tag detail view) -->
              <td v-if="isTagDetail" class="px-4 py-3 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <!-- Inline remove confirmation -->
                  <template v-if="confirmingRemoveId === item.id">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Remove tag?</span>
                    <UButton size="xs" color="error" icon="i-heroicons-check" @click="confirmRemove(item.id)">Yes</UButton>
                    <UButton size="xs" variant="outline" color="neutral" @click="confirmingRemoveId = null">Cancel</UButton>
                  </template>
                  <!-- Normal actions -->
                  <template v-else>
                    <!-- Rename -->
                    <div class="relative" :ref="el => setRenameRef(item.id, el)">
                      <UButton size="xs" variant="outline" color="warning" icon="i-heroicons-pencil-square" :disabled="busyItemId === item.id" @click.stop="toggleItemRename(item.id)" title="Rename tag on this work item">Rename</UButton>
                      <div v-if="renameOpenId === item.id" class="absolute right-0 top-full mt-1 z-50 w-56">
                        <USelectMenu
                          :items="renameFilteredTags.map(t => ({ value: t, label: t }))"
                          value-key="value"
                          placeholder="Search tags…"
                          :loading="allTagsLoading"
                          size="xs"
                          class="w-full"
                          :open="true"
                          @update:model-value="val => { renameOpenId = null; selectRenameTag(item.id, val) }"
                        />
                      </div>
                    </div>
                    <!-- Remove -->
                    <UButton
                      size="xs" variant="outline" color="error" icon="i-heroicons-x-circle"
                      :disabled="busyItemId === item.id"
                      @click="confirmingRemoveId = item.id"
                      title="Remove tag from this work item"
                    >Remove</UButton>
                  </template>
                </div>
              </td>
            </tr>
        </tbody>
      </table>

      <div v-if="filteredItems.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-300 text-sm">
        {{ debouncedSearch ? 'No matching items.' : 'No items to display.' }}
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="totalPages > 1" class="flex items-center justify-between px-4 py-3 border-t border-gray-200 dark:border-gray-700">
      <span class="text-xs text-gray-500 dark:text-gray-300">
        {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, filteredItems.length) }} of {{ filteredItems.length }}
      </span>
      <div class="flex items-center gap-1">
        <UButton size="sm" variant="outline" color="neutral" icon="i-heroicons-chevron-left" :disabled="page <= 1" @click="page = page - 1">Prev</UButton>
        <template v-for="p in visiblePages" :key="p">
          <span v-if="p === '…'" class="px-1.5 text-xs text-gray-400 dark:text-gray-300">…</span>
          <UButton v-else size="sm" :variant="p === page ? 'solid' : 'outline'" :color="p === page ? 'primary' : 'neutral'" @click="page = p">{{ p }}</UButton>
        </template>
        <UButton size="sm" variant="outline" color="neutral" icon="i-heroicons-chevron-right" :disabled="page >= totalPages" @click="page = page + 1">Next</UButton>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { useDebouncedRef } from '../composables/useDebounce.js'

const props = defineProps({
  items: { type: Array, default: () => [] },
  checkType: { type: String, default: '' },
  projectId: { type: String, default: '' },
  tagName: { type: String, default: '' },
  allTags: { type: Array, default: () => [] },
  allTagsLoading: { type: Boolean, default: false },
  busyItemId: { type: Number, default: null },
})

const emit = defineEmits(['delete-tag', 'remove-item-tag', 'rename-item-tag', 'remove-tag', 'rename-tag', 'assign-parent', 'preview-item'])

const isOrphanCheck = computed(() => props.checkType === 'orphan_check')
const isTagOverview = computed(() => props.checkType === 'tag_overview_check')
const isTagDetail = computed(() => props.checkType === 'tag_detail')

function isParentDone(item) {
  return (item.work_item_type || '').toLowerCase().includes('[parent done]')
}

function isZeroCount(item) {
  return (item.work_item_type || '').startsWith('0 ')
}

// --- Per-item rename dropdown ---
const confirmingRemoveId = ref(null)
const confirmAction = ref(null)
const pendingRenameTag = ref(null)
const renameOpenId = ref(null)
const renameSearchText = ref('')
const renameRefs = {}
const searchRefs = {}

function setRenameRef(id, el) { if (el) renameRefs[id] = el }
function setSearchRef(id, el) { if (el) searchRefs[id] = el }

const renameFilteredTags = computed(() => {
  const q = renameSearchText.value.toLowerCase()
  const current = (props.tagName || '').toLowerCase()
  return props.allTags
    .filter(t => t.toLowerCase() !== current)
    .filter(t => !q || t.toLowerCase().includes(q))
})

function confirmRemove(id) {
  confirmingRemoveId.value = null
  confirmAction.value = null
  emit('remove-item-tag', id)
}

function selectRenameTag(itemId, tag) {
  renameOpenId.value = null
  emit('rename-item-tag', itemId, tag)
}

// --- Tag overview bulk operations ---
function renameFilteredTagsForOverview(currentTag) {
  const q = renameSearchText.value.toLowerCase()
  const current = currentTag.toLowerCase()
  return props.allTags
    .filter(t => t.toLowerCase() !== current)
    .filter(t => !q || t.toLowerCase().includes(q))
}

function confirmBulkRemove(tagName) {
  confirmingRemoveId.value = null
  confirmAction.value = null
  emit('remove-tag', tagName)
}

function confirmDelete(tagName) {
  confirmingRemoveId.value = null
  confirmAction.value = null
  emit('delete-tag', tagName)
}

function selectBulkRenameTag(item, newTag) {
  renameOpenId.value = null
  // Set confirm state for rename
  confirmingRemoveId.value = item.id
  confirmAction.value = 'rename'
  // Stash the newTag so confirmation can use it
  pendingRenameTag.value = { tagName: item.title, newTag }
}

function confirmBulkRename() {
  if (!pendingRenameTag.value) return
  const { tagName, newTag } = pendingRenameTag.value
  confirmingRemoveId.value = null
  confirmAction.value = null
  pendingRenameTag.value = null
  emit('rename-tag', tagName, newTag)
}

async function toggleItemRename(itemId) {
  if (renameOpenId.value === itemId) {
    renameOpenId.value = null
    return
  }
  renameOpenId.value = itemId
  renameSearchText.value = ''
  await nextTick()
  searchRefs[itemId]?.focus()
}

function onDocClick(e) {
  if (renameOpenId.value != null) {
    const ref = renameRefs[renameOpenId.value]
    if (ref && !ref.contains(e.target)) {
      renameOpenId.value = null
    }
  }
}
onMounted(() => document.addEventListener('click', onDocClick))
onBeforeUnmount(() => document.removeEventListener('click', onDocClick))

const search = ref('')
const debouncedSearch = useDebouncedRef(search, 250)
const sortKey = ref('id')
const sortDir = ref('asc')
const page = ref(1)
const pageSize = 50

// Reset to page 1 when search or items change
watch([debouncedSearch, () => props.items], () => { page.value = 1 })

const hasAssignedTo = computed(() => props.items.some(i => i.assigned_to))
const hasCreatedDate = computed(() => props.items.some(i => i.created_date))
const hasIterationPath = computed(() => props.items.some(i => i.iteration_path))
const hasState = computed(() => props.items.some(i => i.state))

function formatDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  if (isNaN(d)) return '—'
  return d.toLocaleDateString('nl-NL', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function formatIterationPath(path) {
  if (!path) return '—'
  const parts = path.split('\\')
  return parts[parts.length - 1]
}

function stateClass(state) {
  if (!state) return 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-300'
  const s = state.toLowerCase()
  if (s.includes('progress') || s === 'active' || s === 'committed') return 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300'
  if (s.includes('refine') || s === 'new' || s === 'proposed') return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
  if (s.includes('review') || s.includes('test')) return 'bg-purple-100 dark:bg-purple-900/40 text-purple-700 dark:text-purple-300'
  if (s === 'resolved') return 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300'
  return 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300'
}

const filteredItems = computed(() => {
  let items = props.items
  if (!debouncedSearch.value) return items
  const q = debouncedSearch.value.toLowerCase()
  return items.filter(i =>
    String(i.id).includes(q) ||
    (i.title || '').toLowerCase().includes(q) ||
    (i.work_item_type || '').toLowerCase().includes(q) ||
    (i.assigned_to || '').toLowerCase().includes(q) ||
    (i.iteration_path || '').toLowerCase().includes(q) ||
    (i.state || '').toLowerCase().includes(q)
  )
})

function toggleSort(key) {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortDir.value = 'asc'
  }
}

const sortedItems = computed(() => {
  const copy = [...filteredItems.value]
  const dir = sortDir.value === 'asc' ? 1 : -1
  copy.sort((a, b) => {
    const va = a[sortKey.value]
    const vb = b[sortKey.value]
    if (typeof va === 'number') return (va - vb) * dir
    return String(va || '').localeCompare(String(vb || '')) * dir
  })
  return copy
})

const totalPages = computed(() => Math.ceil(sortedItems.value.length / pageSize) || 1)

const paginatedItems = computed(() => {
  const start = (page.value - 1) * pageSize
  return sortedItems.value.slice(start, start + pageSize)
})

const visiblePages = computed(() => {
  const total = totalPages.value
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const cur = page.value
  const pages = [1]
  if (cur > 3) pages.push('…')
  for (let i = Math.max(2, cur - 1); i <= Math.min(total - 1, cur + 1); i++) pages.push(i)
  if (cur < total - 2) pages.push('…')
  pages.push(total)
  return pages
})
</script>
