<template>
  <UModal :open="true" @update:open="v => { if (!v) $emit('close') }" :ui="{ width: 'sm:max-w-md' }">
    <template #header>
      <h3 class="text-base font-semibold text-gray-900 dark:text-gray-100">Keyboard Shortcuts</h3>
    </template>

    <div class="px-5 py-4 space-y-4 max-h-[60vh] overflow-y-auto">
      <div v-for="(section, i) in sections" :key="i">
        <h4 class="text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-300 mb-2">{{ section.title }}</h4>
        <div class="space-y-1.5">
          <div v-for="(shortcut, j) in section.items" :key="j" class="flex items-center justify-between text-sm">
            <span class="text-gray-700 dark:text-gray-300">{{ shortcut.label }}</span>
            <span class="flex items-center gap-1">
              <UKbd v-for="(key, k) in shortcut.keys" :key="k">{{ key }}</UKbd>
            </span>
          </div>
        </div>
      </div>
    </div>

    <template #footer>
      <div class="text-xs text-gray-500 dark:text-gray-300">
        Press <UKbd>Esc</UKbd> to close
      </div>
    </template>
  </UModal>
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
      { label: 'Go to Sprint Manager', keys: ['g', 's'] },
      { label: 'Go to Template Manager', keys: ['g', 't'] },
      { label: 'Go to DEV Assessment', keys: ['g', 'a'] },
    ],
  },
]
</script>
