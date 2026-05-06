<template>
  <div class="fixed inset-0 z-[60] flex items-center justify-center" @click.self="$emit('close')">
    <!-- Backdrop -->
    <div class="absolute inset-0 bg-black/40 dark:bg-black/60"></div>

    <!-- Card -->
    <div class="relative bg-white dark:bg-gray-800 rounded-xl shadow-2xl w-full max-w-md mx-4 overflow-hidden">
      <!-- Header -->
      <div class="flex items-center justify-between px-5 py-4 border-b border-gray-200 dark:border-gray-700">
        <h3 class="text-base font-semibold text-gray-900 dark:text-gray-100">Keyboard Shortcuts</h3>
        <button @click="$emit('close')" class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
        </button>
      </div>

      <!-- Shortcuts list -->
      <div class="px-5 py-4 space-y-4 max-h-[60vh] overflow-y-auto">
        <div v-for="(section, i) in sections" :key="i">
          <h4 class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-2">{{ section.title }}</h4>
          <div class="space-y-1.5">
            <div v-for="(shortcut, j) in section.items" :key="j" class="flex items-center justify-between text-sm">
              <span class="text-gray-700 dark:text-gray-300">{{ shortcut.label }}</span>
              <span class="flex items-center gap-1">
                <kbd
                  v-for="(key, k) in shortcut.keys"
                  :key="k"
                  class="min-w-[1.5rem] px-1.5 py-0.5 text-center text-xs font-mono bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-600 rounded"
                >{{ key }}</kbd>
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="px-5 py-3 border-t border-gray-200 dark:border-gray-700 text-xs text-gray-500 dark:text-gray-400">
        Press <kbd class="px-1 py-0.5 text-xs font-mono bg-gray-100 dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded">Esc</kbd> to close
      </div>
    </div>
  </div>
</template>

<script setup>
defineEmits(['close'])

const sections = [
  {
    title: 'General',
    items: [
      { label: 'Show shortcuts', keys: ['?'] },
      { label: 'Open settings', keys: [','] },
      { label: 'Focus sidebar search', keys: ['/'] },
      { label: 'Close modal / overlay', keys: ['Esc'] },
    ],
  },
  {
    title: 'Navigation',
    items: [
      { label: 'Go to Dashboard', keys: ['g', 'h'] },
      { label: 'Go to PR Monitor', keys: ['g', 'p'] },
      { label: 'Go to DB Monitor', keys: ['g', 'd'] },
      { label: 'Go to Velocity', keys: ['g', 'v'] },
      { label: 'Go to Configuration', keys: ['g', 'c'] },
      { label: 'Go to Sprint Populator', keys: ['g', 's'] },
      { label: 'Go to DEV Assessment', keys: ['g', 'a'] },
    ],
  },
]
</script>
