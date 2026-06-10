<template>
  <div
    class="roadmap-card px-3 py-2 rounded-lg border text-xs cursor-grab active:cursor-grabbing transition-shadow hover:shadow-md select-none"
    :class="[
      isDirty ? 'ring-2 ring-amber-400 dark:ring-amber-500' : '',
      isDragging ? 'opacity-30 pointer-events-none' : '',
      'border-gray-200 dark:border-gray-600 bg-gray-50 dark:bg-gray-700'
    ]"
    :data-item-id="item.id"
    @pointerdown="onPointerDown"
  >
    <div class="flex items-start gap-2">
      <span class="w-2 h-2 rounded-full mt-1 shrink-0" :class="colorClass"></span>
      <div class="min-w-0 flex-1">
        <div class="font-medium text-gray-800 dark:text-gray-200 line-clamp-3 leading-tight hover:text-primary-600 dark:hover:text-primary-400 cursor-pointer" :title="dTitle(item.title)" @click.stop="$emit('open-detail', item)">{{ dTitle(item.title) }}</div>
        <div class="flex items-center gap-1.5 mt-0.5 flex-wrap">
          <span class="text-gray-400">#{{ item.id }}</span>
          <span class="px-1.5 py-0.5 rounded-full" :class="badgeClass">{{ item.work_item_type }}</span>
          <span v-if="item.state" class="text-gray-400">{{ item.state }}</span>
        </div>
        <div v-if="item.assigned_to" class="text-gray-400 dark:text-gray-500 mt-0.5 truncate">
          {{ dAssigned(item.assigned_to) }}
        </div>
      </div>
      <span v-if="isDirty" class="w-2 h-2 rounded-full bg-amber-400 shrink-0 mt-1" title="Unsaved changes"></span>
    </div>
  </div>
</template>

<script setup>
import { inject, computed } from 'vue'
import { useDemoMode, anonPrTitle, anonName } from '../composables/useDemoMode.js'

const { isDemoMode } = useDemoMode()
function dTitle(v) { return isDemoMode.value ? anonPrTitle(v) : v }
function dAssigned(v) { return isDemoMode.value ? anonName(v) : v }

const props = defineProps({
  item: { type: Object, required: true },
  colorClass: { type: String, default: 'bg-gray-400' },
  badgeClass: { type: String, default: 'bg-gray-200 text-gray-600 dark:bg-gray-600 dark:text-gray-300' },
  isDirty: { type: Boolean, default: false },
})

defineEmits(['open-detail'])

const startDrag = inject('roadmapStartDrag', null)
const draggingId = inject('roadmapDraggingId', null)
const isDragging = computed(() => draggingId?.value === props.item.id)

function onPointerDown(evt) {
  if (evt.button !== 0) return
  evt.preventDefault()
  if (startDrag) startDrag(evt, props.item)
}
</script>
