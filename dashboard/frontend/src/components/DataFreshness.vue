<template>
  <span v-if="timestamp" class="inline-flex items-center gap-1 text-xs" :class="isStale ? 'text-amber-600 dark:text-amber-400' : 'text-gray-400 dark:text-gray-500'">
    <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
      <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
    {{ label }}
  </span>
</template>

<script setup>
import { computed, ref, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  timestamp: { type: String, default: null },
  staleMinutes: { type: Number, default: 60 },
})

const now = ref(Date.now())
let timer = null

onMounted(() => {
  timer = setInterval(() => { now.value = Date.now() }, 30_000)
})
onUnmounted(() => { clearInterval(timer) })

const diffMs = computed(() => props.timestamp ? now.value - new Date(props.timestamp).getTime() : 0)
const isStale = computed(() => diffMs.value > props.staleMinutes * 60_000)

const label = computed(() => {
  if (!props.timestamp) return ''
  const secs = Math.floor(diffMs.value / 1000)
  if (secs < 60) return 'just now'
  if (secs < 3600) return `${Math.floor(secs / 60)}m ago`
  if (secs < 86400) return `${Math.floor(secs / 3600)}h ago`
  return `${Math.floor(secs / 86400)}d ago`
})
</script>
