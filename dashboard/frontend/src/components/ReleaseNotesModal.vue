<template>
  <USlideover :open="true" @update:open="v => { if (!v) $emit('close') }" side="right" title="Release Notes" description="What's new" :overlay="true" :ui="{ width: 'max-w-3xl', overlay: 'bg-black/50 z-[100]', content: 'z-[101]' }">
    <template #body>
      <div class="px-6 py-5 overflow-y-auto leading-relaxed">
        <div v-if="loading" class="text-sm text-gray-500 dark:text-gray-300">Loading release notes...</div>
        <div v-else-if="loadError" class="text-center py-4">
          <p class="text-sm text-red-600 dark:text-red-400 mb-3">Failed to load release notes.</p>
          <UButton size="sm" icon="i-heroicons-arrow-path" @click="fetchNotes">Retry</UButton>
        </div>
        <div v-else-if="!releaseNotes.length" class="text-sm text-gray-500 dark:text-gray-300">No release notes available.</div>
        <div v-else class="space-y-6">
          <div v-for="entry in releaseNotes" :key="entry.version">
            <div class="release-notes-content text-sm text-gray-600 dark:text-gray-300 [&_h1]:text-lg [&_h1]:font-bold [&_h1]:text-primary-700 [&_h1]:dark:text-white [&_h1]:mb-3 [&_h2]:text-base [&_h2]:font-semibold [&_h2]:text-primary-600 [&_h2]:dark:text-gray-100 [&_h2]:mt-4 [&_h2]:mb-1 [&_h3]:text-sm [&_h3]:font-semibold [&_h3]:text-primary-500 [&_h3]:dark:text-gray-200 [&_strong]:text-gray-800 [&_strong]:dark:text-gray-100 [&_a]:text-primary-600 [&_a]:dark:text-primary-400 [&_p]:my-1.5 [&_p]:leading-relaxed [&_ul]:my-1 [&_ul]:pl-4 [&_ul]:list-disc [&_li]:my-0.5 max-w-none" v-html="renderMarkdown(entry.content)"></div>
            <hr v-if="entry !== releaseNotes[releaseNotes.length - 1]" class="mt-6 border-primary-100 dark:border-gray-700" />
          </div>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="text-xs text-gray-500 dark:text-gray-300">
        Press <UKbd>Esc</UKbd> to close
      </div>
    </template>
  </USlideover>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { useMonitorStore } from '../stores/monitor.js'

const emit = defineEmits(['close', 'loaded'])

const store = useMonitorStore()
const releaseNotes = ref([])
const loading = ref(true)
const loadError = ref(false)

function renderMarkdown(content) {
  return DOMPurify.sanitize(marked.parse(content))
}

const MAX_RETRIES = 3
const RETRY_DELAY = 1500

async function fetchNotes(attempt = 1) {
  loading.value = true
  loadError.value = false
  try {
    const notes = await store.fetchReleaseNotes()
    releaseNotes.value = notes
    if (notes.length > 0) {
      emit('loaded')
    }
  } catch {
    if (attempt < MAX_RETRIES) {
      await new Promise(r => setTimeout(r, RETRY_DELAY * attempt))
      return fetchNotes(attempt + 1)
    }
    releaseNotes.value = []
    loadError.value = true
  } finally {
    loading.value = false
  }
}

onMounted(fetchNotes)
</script>

<style>
/* Global (not scoped) — portal renders outside component DOM */
[data-side="right"].fixed {
  z-index: 100;
}
</style>
