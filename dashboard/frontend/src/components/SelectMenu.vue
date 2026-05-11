<template>
  <div ref="containerRef" class="relative" @keydown="onKeydown">
    <button
      ref="triggerRef"
      type="button"
      @click="toggle"
      :disabled="disabled || loading"
      class="w-full flex items-center justify-between border rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 border-gray-300 dark:border-gray-600 focus:ring-2 focus:ring-primary-500 outline-none disabled:opacity-50 disabled:cursor-not-allowed text-left transition-shadow"
      :class="triggerSizeClass"
    >
      <span v-if="loading" class="flex items-center gap-2 text-gray-400 dark:text-gray-500 truncate">
        <svg class="animate-spin w-3.5 h-3.5 shrink-0" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"></path>
        </svg>
        Loading…
      </span>
      <span v-else class="truncate" :class="selectedLabel ? 'text-gray-900 dark:text-gray-100' : 'text-gray-400 dark:text-gray-500'">
        {{ selectedLabel || placeholder }}
      </span>
      <svg class="shrink-0 ml-2 text-gray-400 transition-transform" :class="[isOpen ? 'rotate-180' : '', size === 'sm' ? 'w-3.5 h-3.5' : 'w-4 h-4']" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
      </svg>
    </button>

    <Teleport to="body">
      <div v-if="isOpen" ref="dropdownRef" class="fixed z-[9999] bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-lg shadow-lg overflow-hidden"
        :style="dropdownStyle">
        <div v-if="showSearch" class="p-2 border-b border-gray-200 dark:border-gray-600">
          <input
            ref="searchRef"
            v-model="searchQuery"
            type="text"
            placeholder="Search…"
            class="w-full px-2.5 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-600 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 outline-none"
          />
        </div>

        <ul ref="listRef" class="max-h-60 overflow-y-auto py-1">
          <li
            v-for="(opt, idx) in filteredOptions"
            :key="String(opt.value)"
            @click="selectOption(opt)"
            @mouseenter="highlightedIndex = idx"
            class="cursor-pointer flex items-center justify-between text-gray-900 dark:text-gray-100"
            :class="[
              idx === highlightedIndex ? 'bg-primary-50 dark:bg-primary-900/30' : 'hover:bg-gray-50 dark:hover:bg-gray-600',
              optionSizeClass
            ]"
          >
            <span class="truncate" :class="{ 'font-medium text-primary-600 dark:text-primary-400': isSelected(opt) }">{{ opt.label }}</span>
            <svg v-if="isSelected(opt)" class="w-4 h-4 text-primary-500 shrink-0 ml-2" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
            </svg>
          </li>
          <li v-if="filteredOptions.length === 0" class="px-3 py-2 text-gray-400 dark:text-gray-500" :class="size === 'sm' ? 'text-xs' : 'text-sm'">
            No results
          </li>
        </ul>
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  modelValue: { type: [String, Number], default: '' },
  options: { type: Array, default: () => [] },
  placeholder: { type: String, default: 'Select…' },
  disabled: { type: Boolean, default: false },
  loading: { type: Boolean, default: false },
  searchable: { type: Boolean, default: undefined },
  size: { type: String, default: 'base' },
  autofocus: { type: Boolean, default: false },
})

const emit = defineEmits(['update:modelValue', 'change'])

const containerRef = ref(null)
const triggerRef = ref(null)
const searchRef = ref(null)
const listRef = ref(null)
const dropdownRef = ref(null)
const isOpen = ref(false)
const searchQuery = ref('')
const highlightedIndex = ref(0)
const dropdownStyle = ref({})

const showSearch = computed(() => {
  if (props.searchable !== undefined) return props.searchable
  return props.options.length > 5
})

const selectedLabel = computed(() => {
  if (props.modelValue == null) return ''
  const opt = props.options.find(o => o.value == props.modelValue)
  return opt ? opt.label : ''
})

const filteredOptions = computed(() => {
  if (!searchQuery.value) return props.options
  const q = searchQuery.value.toLowerCase()
  return props.options.filter(o => o.label.toLowerCase().includes(q))
})

const triggerSizeClass = computed(() =>
  props.size === 'sm' ? 'px-2 py-1.5 text-xs' : 'px-3 py-2 text-sm'
)

const optionSizeClass = computed(() =>
  props.size === 'sm' ? 'px-2 py-1.5 text-xs' : 'px-3 py-2 text-sm'
)

function isSelected(opt) {
  if (props.modelValue == null) return false
  return opt.value == props.modelValue
}

function toggle() {
  isOpen.value ? close() : open()
}

function updateDropdownPosition() {
  if (!triggerRef.value) return
  const rect = triggerRef.value.getBoundingClientRect()
  dropdownStyle.value = {
    top: `${rect.bottom + 4}px`,
    left: `${rect.left}px`,
    width: `${rect.width}px`,
  }
}

function open() {
  isOpen.value = true
  searchQuery.value = ''
  const idx = filteredOptions.value.findIndex(o => isSelected(o))
  highlightedIndex.value = Math.max(0, idx)
  updateDropdownPosition()
  nextTick(() => {
    if (showSearch.value && searchRef.value) {
      searchRef.value.focus()
    }
    scrollToHighlighted()
  })
}

function close() {
  isOpen.value = false
  searchQuery.value = ''
  nextTick(() => triggerRef.value?.focus())
}

function selectOption(opt) {
  emit('update:modelValue', opt.value)
  emit('change', opt.value)
  close()
}

function scrollToHighlighted() {
  nextTick(() => {
    if (listRef.value && listRef.value.children[highlightedIndex.value]) {
      listRef.value.children[highlightedIndex.value].scrollIntoView({ block: 'nearest' })
    }
  })
}

function onKeydown(e) {
  if (!isOpen.value) {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault()
      open()
    }
    return
  }

  const len = filteredOptions.value.length
  if (!len) return

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    highlightedIndex.value = (highlightedIndex.value + 1) % len
    scrollToHighlighted()
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    highlightedIndex.value = (highlightedIndex.value - 1 + len) % len
    scrollToHighlighted()
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (filteredOptions.value[highlightedIndex.value]) {
      selectOption(filteredOptions.value[highlightedIndex.value])
    }
  } else if (e.key === 'Escape') {
    e.preventDefault()
    close()
  }
}

function onClickOutside(e) {
  if (isOpen.value && containerRef.value && !containerRef.value.contains(e.target)
      && (!dropdownRef.value || !dropdownRef.value.contains(e.target))) {
    close()
  }
}

onMounted(() => {
  document.addEventListener('mousedown', onClickOutside)
  if (props.autofocus && !props.disabled && !props.loading) {
    setTimeout(() => open(), 50)
  }
})

onUnmounted(() => {
  document.removeEventListener('mousedown', onClickOutside)
})

watch(searchQuery, () => {
  highlightedIndex.value = 0
})

watch(() => props.options, () => {
  if (isOpen.value) {
    highlightedIndex.value = 0
  }
})

defineExpose({ open })
</script>
