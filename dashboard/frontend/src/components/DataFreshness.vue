<template>
  <span v-if="timestamp" class="inline-flex items-center gap-1 text-xs" :class="isStale ? 'text-amber-600 dark:text-amber-400' : 'text-gray-500 dark:text-gray-400'">
    <UIcon name="i-heroicons-clock" class="w-3 h-3" />
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
