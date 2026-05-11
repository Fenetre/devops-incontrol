<template>
  <div class="fixed inset-0 z-[60] flex items-center justify-center" @click.self="$emit('close')">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/40 dark:bg-black/60"></div>

    <!-- Card -->
    <div class="relative bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-full max-w-4xl mx-4 overflow-hidden">
      <!-- Header -->
      <div class="flex items-center justify-between px-5 py-4 bg-primary-50 border-b border-primary-200 dark:bg-gray-800 dark:border-gray-700">
        <h3 class="text-base font-semibold text-primary-700 dark:text-white">Release Notes</h3>
        <button @click="$emit('close')" class="text-primary-400 hover:text-primary-600 dark:text-gray-400 dark:hover:text-gray-200 transition-colors">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
        </button>
      </div>

      <!-- Body -->
      <div class="px-6 py-5 max-h-[70vh] overflow-y-auto leading-relaxed">
        <div v-if="loading" class="text-sm text-gray-500 dark:text-gray-400">Loading release notes...</div>
        <div v-else-if="!releaseNotes.length" class="text-sm text-gray-500 dark:text-gray-400">No release notes available.</div>
        <div v-else class="space-y-6">
          <div v-for="entry in releaseNotes" :key="entry.version">
            <div class="release-notes-content text-sm text-gray-600 dark:text-gray-300 [&_h1]:text-lg [&_h1]:font-bold [&_h1]:text-primary-700 [&_h1]:dark:text-white [&_h1]:mb-3 [&_h2]:text-base [&_h2]:font-semibold [&_h2]:text-primary-600 [&_h2]:dark:text-gray-100 [&_h2]:mt-4 [&_h2]:mb-1 [&_h3]:text-sm [&_h3]:font-semibold [&_h3]:text-primary-500 [&_h3]:dark:text-gray-200 [&_strong]:text-gray-800 [&_strong]:dark:text-gray-100 [&_a]:text-primary-600 [&_a]:dark:text-primary-400 [&_p]:my-1.5 [&_p]:leading-relaxed [&_ul]:my-1 [&_ul]:pl-4 [&_ul]:list-disc [&_li]:my-0.5 max-w-none" v-html="renderMarkdown(entry.content)"></div>
            <hr v-if="entry !== releaseNotes[releaseNotes.length - 1]" class="mt-6 border-primary-100 dark:border-gray-700" />
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="px-5 py-3 border-t border-primary-100 dark:border-gray-700 bg-primary-50/50 dark:bg-gray-800 text-xs text-gray-500 dark:text-gray-400">
        Press <kbd class="px-1 py-0.5 text-xs font-mono bg-white dark:bg-gray-700 border border-primary-200 dark:border-gray-600 rounded text-primary-600 dark:text-gray-300">Esc</kbd> to close
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { marked } from 'marked'
import { useMonitorStore } from '../stores/monitor.js'

const emit = defineEmits(['close'])

const store = useMonitorStore()
const releaseNotes = ref([])
const loading = ref(true)

function renderMarkdown(content) {
  return marked.parse(content)
}

function onKeydown(e) {
  if (e.key === 'Escape') emit('close')
}

onMounted(async () => {
  document.addEventListener('keydown', onKeydown)
  try {
    releaseNotes.value = await store.fetchReleaseNotes()
  } catch {
    releaseNotes.value = []
  } finally {
    loading.value = false
  }
})

onUnmounted(() => {
  document.removeEventListener('keydown', onKeydown)
})
</script>
