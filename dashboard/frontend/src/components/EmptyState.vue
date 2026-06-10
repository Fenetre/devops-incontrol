<template>
  <div class="bg-white dark:bg-gray-800 rounded-xl border shadow-sm px-6 py-12 text-center"
       :class="variant === 'warning' ? 'border-amber-200 dark:border-amber-800' : 'border-gray-200 dark:border-gray-700'">
    <!-- Icon -->
    <div v-if="icon || $slots.icon" class="flex justify-center mb-3">
      <slot name="icon">
        <UIcon v-if="icon === 'folder'" name="i-heroicons-folder" class="w-12 h-12 text-gray-400 dark:text-gray-300" />
        <UIcon v-else-if="icon === 'database'" name="i-heroicons-circle-stack" class="w-12 h-12 text-gray-400 dark:text-gray-300" />
        <UIcon v-else-if="icon === 'archive'" name="i-heroicons-archive-box-arrow-down" class="w-12 h-12 text-gray-400 dark:text-gray-300" />
        <UIcon v-else-if="icon === 'git-merge'" name="i-heroicons-arrows-right-left" class="w-12 h-12 text-gray-400 dark:text-gray-300" />
        <UIcon v-else-if="icon === 'check-circle'" name="i-heroicons-check-circle" class="w-12 h-12 text-green-500 dark:text-green-400" />
        <UIcon v-else-if="icon === 'warning'" name="i-heroicons-exclamation-triangle" class="w-10 h-10 text-amber-500 dark:text-amber-400" />
        <UIcon v-else-if="icon === 'search'" name="i-heroicons-magnifying-glass" class="w-12 h-12 text-gray-400 dark:text-gray-300" />
      </slot>
    </div>
    <!-- Message -->
    <p class="text-gray-500 dark:text-gray-300 text-sm">{{ message }}</p>
    <p v-if="hint" class="text-gray-500 dark:text-gray-300 text-xs mt-1">{{ hint }}</p>
    <!-- Action -->
    <UButton v-if="actionLabel && actionTo" :to="actionTo" variant="soft" color="primary" class="mt-4">
      {{ actionLabel }}
    </UButton>
  </div>
</template>

<script setup>
defineProps({
  icon: { type: String, default: null },
  message: { type: String, required: true },
  hint: { type: String, default: null },
  actionLabel: { type: String, default: null },
  actionTo: { type: String, default: null },
  variant: { type: String, default: 'default' },
})
</script>
