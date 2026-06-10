<template>
  <UModal :open="open" @update:open="$emit('update:open', $event)" :ui="{ overlay: 'z-[9998]', content: 'z-[9999] w-[95vw] max-w-7xl !ring-0 !outline-none !shadow-xl focus-visible:!ring-0 focus-visible:!outline-none', header: 'hidden', close: 'hidden', footer: 'bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 px-6 py-3' }">
    <template #body>
      <div v-if="loading" class="flex items-center justify-center py-12">
        <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 animate-spin text-gray-400" />
        <span class="ml-2 text-sm text-gray-400">Loading…</span>
      </div>
      <div v-else-if="loadError" class="py-8 text-center">
        <UAlert color="error" icon="i-heroicons-exclamation-circle" :description="loadError" />
      </div>
      <div v-else-if="data">
        <!-- Header bar (matches orphan check preview style) -->
        <div class="-mx-6 -mt-6 px-6 py-4 border-b border-primary-200 dark:border-gray-700 bg-primary-50 dark:bg-transparent mb-5">
          <h3 class="text-lg font-semibold text-primary-900 dark:text-gray-100">
            #{{ data.id }}
            <span class="font-normal text-primary-600 dark:text-gray-100 ml-2">{{ data.title }}</span>
          </h3>
          <div class="flex items-center gap-3 mt-2 text-xs text-gray-500 dark:text-gray-400">
            <span class="inline-block px-2 py-0.5 rounded-md font-medium bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">{{ data.work_item_type }}</span>
            <span class="inline-block px-2 py-0.5 rounded-md font-medium bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">{{ data.state }}</span>
            <span v-if="data.assigned_to">{{ data.assigned_to }}</span>
            <span v-if="data.iteration_path">{{ data.iteration_path }}</span>
            <span v-if="data.changed_date" class="text-amber-600 dark:text-amber-400 flex items-center gap-1">
              <UIcon name="i-heroicons-clock" class="w-3 h-3" />
              Last changed: {{ formatDate(data.changed_date) }}
            </span>
            <span v-if="!editing" class="ml-auto">
              <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-pencil-square" label="Edit" @click="startEditing" />
            </span>
          </div>
          <div v-if="tags.length" class="flex flex-wrap items-center gap-1.5 mt-2">
            <UIcon name="i-heroicons-tag" class="w-3.5 h-3.5 text-gray-400 dark:text-gray-500" />
            <span v-for="tag in tags" :key="tag" class="inline-block px-2 py-0.5 rounded-full text-xs font-medium bg-primary-100 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300">{{ tag }}</span>
          </div>
        </div>

        <!-- View mode -->
        <div v-if="!editing" class="space-y-4">
          <!-- Description -->
          <div v-if="sanitizedDesc">
            <div class="prose dark:prose-invert max-w-none text-sm work-item-description" v-html="sanitizedDesc"></div>
          </div>
          <div v-else class="text-sm text-gray-400 dark:text-gray-500 italic">No description available.</div>

          <!-- Dependencies (view mode) -->
          <div class="border-t border-gray-100 dark:border-gray-700 pt-4">
            <h4 class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">Dependencies</h4>
            <div v-if="loadingLinks" class="flex items-center gap-2 text-xs text-gray-400 py-1">
              <UIcon name="i-heroicons-arrow-path" class="w-3 h-3 animate-spin" /> Loading links…
            </div>
            <div v-else-if="predecessors.length === 0 && successors.length === 0 && pendingLinks.length === 0 && pendingRemovals.length === 0" class="text-xs text-gray-400">No dependencies</div>
            <div v-else class="space-y-2">
              <div v-if="predecessors.length > 0">
                <span class="text-[10px] uppercase tracking-wider text-gray-400 font-medium">Predecessors</span>
                <div v-for="link in predecessors" :key="link.id" class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300 py-0.5">
                  <UIcon name="i-heroicons-arrow-left" class="w-3 h-3 text-gray-400 shrink-0" />
                  <span class="font-medium text-xs text-gray-500 dark:text-gray-400">#{{ link.id }}</span>
                  <span class="truncate flex-1">{{ link.title }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400">{{ link.state }}</span>
                  <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="removeDependency(link.id, 'predecessor')" title="Remove link" />
                </div>
              </div>
              <div v-if="successors.length > 0">
                <span class="text-[10px] uppercase tracking-wider text-gray-400 font-medium">Successors</span>
                <div v-for="link in successors" :key="link.id" class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300 py-0.5">
                  <UIcon name="i-heroicons-arrow-right" class="w-3 h-3 text-gray-400 shrink-0" />
                  <span class="font-medium text-xs text-gray-500 dark:text-gray-400">#{{ link.id }}</span>
                  <span class="truncate flex-1">{{ link.title }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400">{{ link.state }}</span>
                  <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="removeDependency(link.id, 'successor')" title="Remove link" />
                </div>
              </div>
              <!-- Pending (dirty) links -->
              <div v-if="pendingLinks.length > 0">
                <span class="text-[10px] uppercase tracking-wider text-amber-500 font-medium">Pending (not yet pushed)</span>
                <div v-for="link in pendingLinks" :key="link.target_id + link.link_type" class="flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400 py-0.5">
                  <UIcon :name="link.link_type === 'predecessor' ? 'i-heroicons-arrow-left' : 'i-heroicons-arrow-right'" class="w-3 h-3 shrink-0" />
                  <span class="font-medium text-xs">#{{ link.target_id }}</span>
                  <span class="text-[10px] uppercase">{{ link.link_type }}</span>
                  <span class="flex-1"></span>
                  <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="undoPendingLink(link.target_id, link.link_type)" title="Undo" />
                </div>
              </div>
              <!-- Pending removals -->
              <div v-if="pendingRemovals.length > 0">
                <span class="text-[10px] uppercase tracking-wider text-red-500 font-medium">Pending removal</span>
                <div v-for="link in pendingRemovals" :key="'r'+link.target_id + link.link_type" class="flex items-center gap-2 text-sm text-red-500 dark:text-red-400 py-0.5 line-through opacity-70">
                  <UIcon :name="link.link_type === 'predecessor' ? 'i-heroicons-arrow-left' : 'i-heroicons-arrow-right'" class="w-3 h-3 shrink-0" />
                  <span class="font-medium text-xs">#{{ link.target_id }}</span>
                  <span class="text-[10px] uppercase">{{ link.link_type }}</span>
                  <span class="flex-1"></span>
                  <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-arrow-uturn-left" @click="undoPendingRemoval(link.target_id, link.link_type)" title="Undo removal" />
                </div>
              </div>
            </div>
          </div>

          <!-- Comments -->
          <div class="border-t border-gray-100 dark:border-gray-700 pt-4">
            <h4 class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">Comments ({{ data.comments?.length || 0 }})</h4>
            <div v-if="data.comments && data.comments.length" class="space-y-3 max-h-[300px] overflow-y-auto mb-3">
              <div v-for="(c, i) in data.comments" :key="i" class="rounded-lg border border-gray-100 dark:border-gray-700 px-3 py-2">
                <div class="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400 mb-1">
                  <span class="font-medium">{{ c.author }}</span>
                  <span>{{ formatDate(c.created_date) }}</span>
                </div>
                <div class="prose dark:prose-invert max-w-none text-xs" v-html="sanitizeHtml(c.text)"></div>
              </div>
            </div>

            <!-- Add comment -->
            <div class="relative">
              <div
                ref="commentEditor"
                contenteditable="true"
                class="w-full min-h-[60px] max-h-[150px] overflow-y-auto rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 px-3 py-2 text-sm text-gray-800 dark:text-gray-200 focus:outline-none focus:ring-2 focus:ring-primary-500"
                :data-placeholder="'Add a comment… (use @ to mention people)'"
                @input="onMentionInput($event, 'comment')"
                @keydown="onMentionKeydown"
                @paste="onPaste($event, 'comment')"
                @blur="closeMentionDropdown"
              ></div>
              <!-- @mention dropdown -->
              <div v-if="mentionOpen && mentionContext === 'comment'" class="absolute left-0 bottom-full mb-1 w-64 max-h-48 overflow-y-auto bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg z-[10001]">
                <button
                  v-for="(person, idx) in filteredMentions" :key="person.value"
                  class="w-full text-left px-3 py-1.5 text-sm hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors"
                  :class="idx === mentionIdx ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300' : 'text-gray-700 dark:text-gray-300'"
                  @mousedown.prevent="selectMention(person)"
                >{{ person.label }}</button>
                <div v-if="filteredMentions.length === 0" class="px-3 py-2 text-xs text-gray-400">No matches</div>
              </div>
              <div class="flex justify-end mt-2">
                <UButton size="xs" label="Post Comment" icon="i-heroicons-paper-airplane" :loading="postingComment" :disabled="!commentHasContent" @click="postComment" />
              </div>
            </div>
          </div>
        </div>

        <!-- Edit mode -->
        <div v-else class="space-y-4">

          <!-- Title -->
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Title</label>
            <UInput name="title" v-model="form.title" class="w-full" />
          </div>

          <!-- State + Assigned To row -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">State</label>
              <USelectMenu v-model="form.state" :items="stateOptions" value-key="value" :loading="loadingStates" placeholder="Select state…" class="w-full" :search-input="false" :ui="{ content: 'z-[10000]' }" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Assigned To</label>
              <USelectMenu v-model="form.assignedTo" :items="memberOptions" value-key="value" :loading="loadingMembers" placeholder="Search people…" class="w-full" :ui="{ content: 'z-[10000]' }" />
            </div>
          </div>

          <!-- Iteration + Tags row -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Iteration Path</label>
              <USelectMenu v-model="form.iterationPath" :items="iterationOptions" value-key="value" :loading="loadingIterations" placeholder="Select iteration…" class="w-full" :ui="{ content: 'z-[10000]' }" />
            </div>
            <div>
              <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Tags</label>
              <USelectMenu v-model="form.tags" :items="tagOptions" value-key="value" :loading="loadingTags" :multiple="true" placeholder="Search tags…" class="w-full" :ui="{ content: 'z-[10000]' }" />
            </div>
          </div>

          <!-- Description -->
          <div class="relative">
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Description</label>
            <div
              ref="descEditor"
              contenteditable="true"
              class="w-full min-h-[200px] max-h-[400px] overflow-y-auto rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 px-3 py-2 text-sm text-gray-800 dark:text-gray-200 focus:outline-none focus:ring-2 focus:ring-primary-500"
              v-html="form.description"
              @blur="form.description = $event.target.innerHTML"
              @input="onMentionInput($event, 'desc')"
              @keydown="onMentionKeydown"
              @paste="onPaste($event, 'desc')"
            ></div>
            <!-- @mention dropdown for description -->
            <div v-if="mentionOpen && mentionContext === 'desc'" class="absolute left-0 bottom-full mb-1 w-64 max-h-48 overflow-y-auto bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg z-[10001]">
              <button
                v-for="(person, idx) in filteredMentions" :key="person.value"
                class="w-full text-left px-3 py-1.5 text-sm hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors"
                :class="idx === mentionIdx ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300' : 'text-gray-700 dark:text-gray-300'"
                @mousedown.prevent="selectMention(person)"
              >{{ person.label }}</button>
              <div v-if="filteredMentions.length === 0" class="px-3 py-2 text-xs text-gray-400">No matches</div>
            </div>
          </div>

          <!-- Custom fields -->
          <div v-if="visibleCustomFields.length > 0" class="space-y-3 pt-2 border-t border-gray-100 dark:border-gray-700">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Additional Fields</div>
            <!-- Dropdown / input fields side by side -->
            <div v-if="gridCustomFields.length > 0" class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div v-for="field in gridCustomFields" :key="field.referenceName">
                <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">{{ field.name }}</label>
                <USelectMenu v-if="field.allowedValues.length > 0"
                  :model-value="customFieldValues[field.referenceName] || undefined"
                  @update:model-value="customFieldValues[field.referenceName] = $event"
                  :items="fieldOptions(field)"
                  value-key="value"
                  :placeholder="field.defaultValue || 'Select…'"
                  :ui="{ content: 'z-[10000]' }"
                  class="w-full" />
                <UInput v-else
                  name="custom-field"
                  :model-value="customFieldValues[field.referenceName] || ''"
                  @update:model-value="customFieldValues[field.referenceName] = $event"
                  :placeholder="field.defaultValue || ''"
                  class="w-full" />
              </div>
            </div>
            <!-- Text fields full width -->
            <div v-for="field in textCustomFields" :key="field.referenceName">
              <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">{{ field.name }}</label>
              <div class="relative">
                <div
                  contenteditable="true"
                  class="w-full min-h-[80px] overflow-x-auto rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 px-3 py-2 text-sm text-gray-800 dark:text-gray-200 focus:outline-none focus:ring-2 focus:ring-primary-500 prose dark:prose-invert max-w-none"
                  v-html="customFieldValues[field.referenceName] || ''"
                  @blur="customFieldValues[field.referenceName] = $event.target.innerHTML"
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

          <!-- Dependencies (edit mode) -->
          <div v-if="predecessors.length > 0 || successors.length > 0 || pendingLinks.length > 0 || pendingRemovals.length > 0" class="space-y-3 pt-2 border-t border-gray-100 dark:border-gray-700">
            <div class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">Dependencies</div>
            <div class="space-y-1">
              <div v-for="link in predecessors" :key="'p'+link.id" class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
                <UIcon name="i-heroicons-arrow-left" class="w-3 h-3 text-gray-400 shrink-0" />
                <span class="text-[10px] uppercase text-gray-400">predecessor</span>
                <span class="font-medium text-xs text-gray-500 dark:text-gray-400">#{{ link.id }}</span>
                <span class="truncate flex-1">{{ link.title }}</span>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="removeDependency(link.id, 'predecessor')" title="Remove link" />
              </div>
              <div v-for="link in successors" :key="'s'+link.id" class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
                <UIcon name="i-heroicons-arrow-right" class="w-3 h-3 text-gray-400 shrink-0" />
                <span class="text-[10px] uppercase text-gray-400">successor</span>
                <span class="font-medium text-xs text-gray-500 dark:text-gray-400">#{{ link.id }}</span>
                <span class="truncate flex-1">{{ link.title }}</span>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="removeDependency(link.id, 'successor')" title="Remove link" />
              </div>
              <!-- Pending links -->
              <div v-for="link in pendingLinks" :key="'a'+link.target_id+link.link_type" class="flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400">
                <UIcon :name="link.link_type === 'predecessor' ? 'i-heroicons-arrow-left' : 'i-heroicons-arrow-right'" class="w-3 h-3 shrink-0" />
                <span class="text-[10px] uppercase">{{ link.link_type }} (pending)</span>
                <span class="font-medium text-xs">#{{ link.target_id }}</span>
                <span class="flex-1"></span>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="undoPendingLink(link.target_id, link.link_type)" title="Undo" />
              </div>
              <!-- Pending removals -->
              <div v-for="link in pendingRemovals" :key="'r'+link.target_id+link.link_type" class="flex items-center gap-2 text-sm text-red-500 dark:text-red-400 line-through opacity-70">
                <UIcon :name="link.link_type === 'predecessor' ? 'i-heroicons-arrow-left' : 'i-heroicons-arrow-right'" class="w-3 h-3 shrink-0" />
                <span class="text-[10px] uppercase">{{ link.link_type }} (removing)</span>
                <span class="font-medium text-xs">#{{ link.target_id }}</span>
                <span class="flex-1"></span>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-arrow-uturn-left" @click="undoPendingRemoval(link.target_id, link.link_type)" title="Undo removal" />
              </div>
            </div>
          </div>

          <!-- Save error -->
          <UAlert v-if="saveError" color="error" icon="i-heroicons-exclamation-circle" :description="saveError" />
        </div>
      </div>
    </template>
    <template #footer>
      <div class="flex items-center justify-between w-full">
        <span class="text-xs text-gray-500 dark:text-gray-400">Press <kbd class="px-1 py-0.5 text-xs font-mono bg-gray-100 dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded">Esc</kbd> to close</span>
        <div class="flex gap-2">
          <UButton v-if="editing" variant="outline" color="neutral" label="Cancel" @click="cancelEditing" :disabled="saving" />
          <UButton v-if="editing" label="Save" icon="i-heroicons-document-arrow-down" :loading="saving" @click="save" />
          <UButton v-if="!editing" variant="outline" color="neutral" @click="$emit('update:open', false)">Close</UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useApi } from '../composables/useApi.js'
import { useRoadmapStore } from '../stores/roadmap.js'
import DOMPurify from 'dompurify'

const props = defineProps({
  open: { type: Boolean, default: false },
  workItemId: { type: Number, default: null },
  projectId: { type: String, default: '' },
  organization: { type: String, default: '' },
  projectName: { type: String, default: '' },
})

const emit = defineEmits(['update:open', 'updated'])

const api = useApi()
const roadmapStore = useRoadmapStore()
const loading = ref(false)
const loadError = ref('')
const data = ref(null)
const editing = ref(false)
const saving = ref(false)
const saveError = ref('')
const descEditor = ref(null)

// Dependencies state
const predecessors = ref([])
const successors = ref([])
const loadingLinks = ref(false)

const form = ref({
  title: '',
  state: '',
  assignedTo: '',
  iterationPath: '',
  tags: '',
  description: '',
})

const stateOptions = ref([])
const loadingStates = ref(false)
const memberOptions = ref([])
const loadingMembers = ref(false)
const iterationOptions = ref([])
const loadingIterations = ref(false)
const tagOptions = ref([])
const loadingTags = ref(false)

// Custom fields state
const customFields = ref([])
const customFieldValues = ref({})
const loadingFields = ref(false)

// Comment state
const commentEditor = ref(null)
const postingComment = ref(false)
const commentHasContent = ref(false)

// @mention state
const mentionOpen = ref(false)
const mentionContext = ref('') // 'comment' or 'desc'
const mentionQuery = ref('')
const mentionIdx = ref(0)
const mentionRange = ref(null) // saved selection range

const filteredMentions = computed(() => {
  if (!mentionQuery.value) return memberOptions.value.slice(0, 20)
  const q = mentionQuery.value.toLowerCase()
  return memberOptions.value.filter(m => m.label.toLowerCase().includes(q)).slice(0, 20)
})

const tags = computed(() => {
  if (!data.value?.tags) return []
  return data.value.tags.split(';').map(t => t.trim()).filter(Boolean)
})

const sanitizedDesc = computed(() => {
  if (!data.value?.description) return ''
  return DOMPurify.sanitize(data.value.description, {
    ADD_TAGS: ['img'],
    ADD_ATTR: ['src', 'alt', 'width', 'height', 'style'],
  })
})

function sanitizeHtml(html) {
  const clean = DOMPurify.sanitize(html || '', {
    ADD_TAGS: ['img'],
    ADD_ATTR: ['src', 'alt', 'width', 'height', 'style'],
  })
  // Rewrite Azure DevOps attachment URLs to use the proxy
  return clean.replace(/src="(https:\/\/[^"]*(?:\.visualstudio\.com|dev\.azure\.com)[^"]*\/wit\/attachments[^"]*)"/g,
    (_, url) => `src="/api/roadmap/attachment-proxy?url=${encodeURIComponent(url)}"`)
}

function formatDate(dateStr) {
  if (!dateStr) return ''
  return new Date(dateStr).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}

// --- Dependency link helpers ---

const pendingLinks = computed(() => {
  // Touch version counter to ensure reactivity
  void roadmapStore.dirtyLinksVersion
  const results = []
  // Links stored under this item's ID
  const entry = roadmapStore.dirtyLinks.get(props.workItemId)
  if (entry?.added) {
    for (const link of entry.added) {
      results.push({ target_id: link.target_id, link_type: link.link_type })
    }
  }
  // Links stored under OTHER items where this item is the target
  for (const [itemId, otherEntry] of roadmapStore.dirtyLinks) {
    if (itemId === props.workItemId) continue
    if (otherEntry?.added) {
      for (const link of otherEntry.added) {
        if (link.target_id === props.workItemId) {
          // Reverse the perspective: if other item has us as predecessor, we are their predecessor → they are our successor
          const reversedType = link.link_type === 'predecessor' ? 'successor' : 'predecessor'
          results.push({ target_id: itemId, link_type: reversedType })
        }
      }
    }
  }
  return results
})

const pendingRemovals = computed(() => {
  void roadmapStore.dirtyLinksVersion
  const results = []
  // Links stored under this item's ID in removed list
  const entry = roadmapStore.dirtyLinks.get(props.workItemId)
  if (entry?.removed) {
    for (const link of entry.removed) {
      results.push({ target_id: link.target_id, link_type: link.link_type })
    }
  }
  // Links stored under OTHER items where this item is the target in removed list
  for (const [itemId, otherEntry] of roadmapStore.dirtyLinks) {
    if (itemId === props.workItemId) continue
    if (otherEntry?.removed) {
      for (const link of otherEntry.removed) {
        if (link.target_id === props.workItemId) {
          const reversedType = link.link_type === 'predecessor' ? 'successor' : 'predecessor'
          results.push({ target_id: itemId, link_type: reversedType })
        }
      }
    }
  }
  return results
})

function removeDependency(targetId, linkType) {
  roadmapStore.removeLink(props.workItemId, targetId, linkType)
  // Remove from local display
  if (linkType === 'predecessor') {
    predecessors.value = predecessors.value.filter(l => l.id !== targetId)
  } else {
    successors.value = successors.value.filter(l => l.id !== targetId)
  }
}

function undoPendingRemoval(targetId, linkType) {
  // Check if the removal is stored under this item
  const entry = roadmapStore.dirtyLinks.get(props.workItemId)
  if (entry?.removed?.some(l => l.target_id === targetId && l.link_type === linkType)) {
    roadmapStore.addLink(props.workItemId, targetId, linkType)
  } else {
    // Removal is stored under the other item (reversed perspective)
    const reversedType = linkType === 'predecessor' ? 'successor' : 'predecessor'
    roadmapStore.addLink(targetId, props.workItemId, reversedType)
  }
}

function undoPendingLink(targetId, linkType) {
  // Check if the link is stored under this item
  const entry = roadmapStore.dirtyLinks.get(props.workItemId)
  if (entry?.added?.some(l => l.target_id === targetId && l.link_type === linkType)) {
    roadmapStore.removeLink(props.workItemId, targetId, linkType)
  } else {
    // Link is stored under the other item (reversed perspective)
    const reversedType = linkType === 'predecessor' ? 'successor' : 'predecessor'
    roadmapStore.removeLink(targetId, props.workItemId, reversedType)
  }
}

async function startEditing() {
  // Parse tags string into array
  const tagsArr = (data.value.tags || '').split(';').map(t => t.trim()).filter(Boolean)
  form.value = {
    title: data.value.title || '',
    state: data.value.state || '',
    assignedTo: data.value.assigned_to || '',
    iterationPath: data.value.iteration_path || '',
    tags: tagsArr,
    description: data.value.description || '',
  }
  saveError.value = ''
  editing.value = true

  // Load dropdown data in parallel
  const promises = []
  const org = encodeURIComponent(props.organization)
  const proj = encodeURIComponent(props.projectName)
  const wit = data.value.work_item_type ? encodeURIComponent(data.value.work_item_type) : null

  if (stateOptions.value.length === 0 && wit) {
    loadingStates.value = true
    promises.push(
      api.get(`/api/roadmap/wit-states?organization=${org}&project=${proj}&type=${wit}`)
        .then(states => { stateOptions.value = states.map(s => ({ value: s, label: s })) })
        .catch(() => {})
        .finally(() => { loadingStates.value = false })
    )
  }

  if (memberOptions.value.length === 0) {
    loadingMembers.value = true
    promises.push(
      api.get(`/api/roadmap/project-members?organization=${org}&project=${proj}`)
        .then(members => { memberOptions.value = members })
        .catch(() => {})
        .finally(() => { loadingMembers.value = false })
    )
  }

  if (iterationOptions.value.length === 0) {
    loadingIterations.value = true
    promises.push(
      api.get(`/api/roadmap/project-iterations?organization=${org}&project=${proj}`)
        .then(iters => { iterationOptions.value = iters.map(i => ({ value: i, label: i })) })
        .catch(() => {})
        .finally(() => { loadingIterations.value = false })
    )
  }

  if (tagOptions.value.length === 0) {
    loadingTags.value = true
    promises.push(
      api.get(`/api/roadmap/project-tags?organization=${org}&project=${proj}`)
        .then(tags => { tagOptions.value = tags.map(t => ({ value: t, label: t })) })
        .catch(() => {})
        .finally(() => { loadingTags.value = false })
    )
  }

  if (customFields.value.length === 0 && wit) {
    loadingFields.value = true
    promises.push(
      Promise.all([
        api.get(`/api/roadmap/wit-fields?organization=${org}&project=${proj}&type=${wit}`),
        api.get(`/api/roadmap/work-item-field-values?organization=${org}&project=${proj}&work_item_id=${props.workItemId}`)
      ]).then(([fields, values]) => {
        customFields.value = (fields || []).filter(f => f.referenceName.startsWith('Custom.') || f.referenceName.startsWith('Microsoft.VSTS.Common.') || f.allowedValues?.length > 0)
        const vals = {}
        for (const field of customFields.value) {
          const current = values[field.referenceName]
          vals[field.referenceName] = current != null ? String(current) : ''
        }
        customFieldValues.value = vals
      }).catch(() => {})
        .finally(() => { loadingFields.value = false })
    )
  }

  await Promise.all(promises)
}

function cancelEditing() {
  editing.value = false
  saveError.value = ''
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
  onMentionInput(event, 'cf-' + referenceName)
}

function isHtmlContent(val) {
  return val && /<[a-z][\s\S]*>/i.test(val)
}

// --- @mention logic ---

function getMentionTriggerWord() {
  const sel = window.getSelection()
  if (!sel || sel.rangeCount === 0) return null
  const range = sel.getRangeAt(0)
  const textNode = range.startContainer
  if (textNode.nodeType !== Node.TEXT_NODE) return null
  const text = textNode.textContent || ''
  const cursorPos = range.startOffset
  // Find the @ before cursor
  const beforeCursor = text.slice(0, cursorPos)
  const atIdx = beforeCursor.lastIndexOf('@')
  if (atIdx === -1) return null
  // Make sure there's no space between @ and cursor (allow spaces in names)
  const query = beforeCursor.slice(atIdx + 1)
  // If there's a line break or it's too long, don't trigger
  if (query.length > 40 || query.includes('\n')) return null
  return { query, atIdx, textNode, cursorPos }
}

function onMentionInput(event, context) {
  if (context === 'comment') {
    commentHasContent.value = !!(commentEditor.value && commentEditor.value.textContent.trim())
  }
  const trigger = getMentionTriggerWord()
  if (trigger) {
    mentionOpen.value = true
    mentionContext.value = context
    mentionQuery.value = trigger.query
    mentionIdx.value = 0
    // Save range for later insertion
    const sel = window.getSelection()
    if (sel && sel.rangeCount > 0) mentionRange.value = sel.getRangeAt(0).cloneRange()
  } else {
    mentionOpen.value = false
  }

  // Load members if not yet loaded
  if (mentionOpen.value && memberOptions.value.length === 0 && !loadingMembers.value) {
    loadingMembers.value = true
    api.get(`/api/roadmap/project-members?organization=${encodeURIComponent(props.organization)}&project=${encodeURIComponent(props.projectName)}`)
      .then(members => { memberOptions.value = members })
      .catch(() => {})
      .finally(() => { loadingMembers.value = false })
  }
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

  // Replace @query with a mention span
  const before = text.slice(0, atIdx)
  const after = text.slice(cursorPos)

  // Create mention element
  const mentionEl = document.createElement('a')
  mentionEl.href = `mailto:${person.value}`
  mentionEl.textContent = `@${person.label}`
  mentionEl.style.cssText = 'color: #0078d4; font-weight: 500; text-decoration: none;'
  mentionEl.contentEditable = 'false'

  // Replace text node content
  textNode.textContent = before
  const afterNode = document.createTextNode(after || '\u00A0')
  const parent = textNode.parentNode
  parent.insertBefore(mentionEl, textNode.nextSibling)
  parent.insertBefore(afterNode, mentionEl.nextSibling)

  // Move cursor after mention
  const sel = window.getSelection()
  const newRange = document.createRange()
  newRange.setStart(afterNode, after ? 0 : 1)
  newRange.collapse(true)
  sel.removeAllRanges()
  sel.addRange(newRange)

  mentionOpen.value = false

  // Sync custom field value if mention was in a custom field editor
  if (mentionContext.value && mentionContext.value.startsWith('cf-')) {
    const refName = mentionContext.value.slice(3)
    let el = textNode.parentNode
    while (el && !el.hasAttribute?.('contenteditable')) el = el.parentNode
    if (el) customFieldValues.value[refName] = el.innerHTML
  }
}

function closeMentionDropdown() {
  // Delay to allow mousedown on dropdown items to fire
  setTimeout(() => { mentionOpen.value = false }, 200)
}

// --- Image paste ---

async function onPaste(evt, context) {
  const items = evt.clipboardData?.items
  if (!items) return
  for (let i = 0; i < items.length; i++) {
    if (items[i].type.startsWith('image/')) {
      evt.preventDefault()
      const file = items[i].getAsFile()
      if (!file) return
      let editorEl
      if (context === 'desc') editorEl = descEditor.value
      else if (context === 'comment') editorEl = commentEditor.value
      else editorEl = evt.target  // custom field contenteditable
      await uploadAndInsertImage(file, editorEl, context)
      return
    }
  }
}

async function uploadAndInsertImage(file, editorEl, context) {
  if (!props.organization || !props.projectName) return

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
    formData.append('organization', props.organization)
    formData.append('project', props.projectName)
    const resp = await api.postForm('/api/roadmap/upload-attachment', formData)
    const img = document.createElement('img')
    img.src = `/api/roadmap/attachment-proxy?url=${encodeURIComponent(resp.url)}`
    img.style.maxWidth = '100%'
    img.alt = file.name || 'pasted image'
    placeholder.replaceWith(img)
  } catch (err) {
    console.error('Image upload failed:', err)
    placeholder.textContent = `❌ upload failed: ${err.message || 'unknown error'}`
  }

  // Sync description form value if editing description
  if (editorEl === descEditor.value) {
    form.value.description = descEditor.value.innerHTML
  }
  // Update comment content flag
  if (editorEl === commentEditor.value) {
    commentHasContent.value = !!(commentEditor.value && commentEditor.value.textContent.trim())
  }
  // Sync custom field value after image insert
  if (context && context.startsWith('cf-')) {
    const refName = context.slice(3)
    customFieldValues.value[refName] = editorEl.innerHTML
  }
}

// --- Post comment ---

async function postComment() {
  if (!commentEditor.value) return
  const html = commentEditor.value.innerHTML.trim()
  if (!html || html === '<br>') return

  postingComment.value = true
  try {
    const result = await api.post('/api/roadmap/add-comment', {
      organization: props.organization,
      project: props.projectName,
      work_item_id: props.workItemId,
      text: html,
    })
    // Add to local comments list
    if (!data.value.comments) data.value.comments = []
    data.value.comments.push(result)
    commentEditor.value.innerHTML = ''
    commentHasContent.value = false
  } catch (e) {
    // Show error inline — could improve
    alert(e.message || 'Failed to post comment')
  } finally {
    postingComment.value = false
  }
}

async function save() {
  saving.value = true
  saveError.value = ''

  // Read latest content from contenteditable
  if (descEditor.value) {
    form.value.description = descEditor.value.innerHTML
  }

  // Convert tags array to semicolon string
  const tagsString = Array.isArray(form.value.tags) ? form.value.tags.join('; ') : form.value.tags

  // Build payload — only send changed fields
  const payload = {
    organization: props.organization,
    project: props.projectName,
    work_item_id: props.workItemId,
  }

  if (form.value.title !== (data.value.title || '')) payload.title = form.value.title
  if (form.value.state !== (data.value.state || '')) payload.state = form.value.state
  if (form.value.assignedTo !== (data.value.assigned_to || '')) payload.assigned_to = form.value.assignedTo
  if (form.value.iterationPath !== (data.value.iteration_path || '')) payload.iteration_path = form.value.iterationPath
  if (tagsString !== (data.value.tags || '')) payload.tags = tagsString
  if (form.value.description !== (data.value.description || '')) payload.description = form.value.description

  // Collect changed custom fields
  const changedFields = {}
  for (const field of customFields.value) {
    const newVal = customFieldValues.value[field.referenceName] || ''
    changedFields[field.referenceName] = newVal
  }
  if (Object.keys(changedFields).length > 0) {
    payload.fields = changedFields
  }

  // Check if anything changed
  const hasChanges = payload.title !== undefined || payload.state !== undefined ||
    payload.assigned_to !== undefined || payload.iteration_path !== undefined ||
    payload.tags !== undefined || payload.description !== undefined ||
    payload.fields !== undefined

  if (!hasChanges) {
    editing.value = false
    saving.value = false
    return
  }

  try {
    await api.patch('/api/roadmap/update-work-item', payload)
    // Update local data
    if (payload.title !== undefined) data.value.title = form.value.title
    if (payload.state !== undefined) data.value.state = form.value.state
    if (payload.assigned_to !== undefined) data.value.assigned_to = form.value.assignedTo
    if (payload.iteration_path !== undefined) data.value.iteration_path = form.value.iterationPath
    if (payload.tags !== undefined) data.value.tags = tagsString
    if (payload.description !== undefined) data.value.description = form.value.description
    editing.value = false
    emit('updated', { id: props.workItemId })
  } catch (e) {
    saveError.value = e.message || 'Failed to save changes'
  } finally {
    saving.value = false
  }
}

watch(() => props.open, async (isOpen) => {
  if (!isOpen) {
    editing.value = false
    mentionOpen.value = false
    return
  }
  if (!props.workItemId || !props.projectId) return
  // Reset cached dropdowns when project/type might change
  stateOptions.value = []
  memberOptions.value = []
  iterationOptions.value = []
  tagOptions.value = []
  customFields.value = []
  customFieldValues.value = {}
  commentHasContent.value = false
  predecessors.value = []
  successors.value = []
  loading.value = true
  loadError.value = ''
  data.value = null
  try {
    const resp = await api.get(`/api/checks/work-item-preview/${encodeURIComponent(props.projectId)}?work_item_id=${props.workItemId}`)
    data.value = resp
  } catch (e) {
    loadError.value = e.message || 'Failed to load work item details'
  } finally {
    loading.value = false
  }
  // Load dependency links
  if (props.organization && props.projectName && props.workItemId) {
    loadingLinks.value = true
    try {
      const links = await api.get(`/api/roadmap/work-item-links?organization=${encodeURIComponent(props.organization)}&project=${encodeURIComponent(props.projectName)}&work_item_id=${props.workItemId}`)
      predecessors.value = links.predecessors || []
      successors.value = links.successors || []
    } catch { /* ignore */ }
    // Merge in locally-saved links (pushed this session but not yet re-fetched from API)
    const saved = roadmapStore.savedLinks.get(props.workItemId)
    if (saved) {
      const existingPredIds = new Set(predecessors.value.map(l => l.id))
      for (const predId of saved.predecessors) {
        if (!existingPredIds.has(predId)) {
          const item = roadmapStore.items.find(i => i.id === predId)
          predecessors.value.push({ id: predId, title: item?.title || `Work Item #${predId}`, state: item?.state || '', work_item_type: item?.work_item_type || '' })
        }
      }
      const existingSuccIds = new Set(successors.value.map(l => l.id))
      for (const succId of saved.successors) {
        if (!existingSuccIds.has(succId)) {
          const item = roadmapStore.items.find(i => i.id === succId)
          successors.value.push({ id: succId, title: item?.title || `Work Item #${succId}`, state: item?.state || '', work_item_type: item?.work_item_type || '' })
        }
      }
    }
    loadingLinks.value = false
  }
  // Preload members for @mentions in comment box
  if (memberOptions.value.length === 0 && props.organization && props.projectName) {
    loadingMembers.value = true
    api.get(`/api/roadmap/project-members?organization=${encodeURIComponent(props.organization)}&project=${encodeURIComponent(props.projectName)}`)
      .then(members => { memberOptions.value = members })
      .catch(() => {})
      .finally(() => { loadingMembers.value = false })
  }
})
</script>

<style scoped>
[contenteditable][data-placeholder]:empty::before {
  content: attr(data-placeholder);
  color: #9ca3af;
  pointer-events: none;
}
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
