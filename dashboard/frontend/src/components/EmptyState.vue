<template>
  <div class="bg-white dark:bg-gray-800 rounded-xl border shadow-sm px-6 py-12 text-center"
       :class="variant === 'warning' ? 'border-amber-200 dark:border-amber-800' : 'border-gray-200 dark:border-gray-700'">
    <!-- Icon -->
    <div v-if="icon || $slots.icon" class="flex justify-center mb-3">
      <slot name="icon">
        <!-- folder -->
        <svg v-if="icon === 'folder'" class="w-12 h-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
          <path stroke-linecap="round" stroke-linejoin="round" d="M2.25 12.75V12A2.25 2.25 0 014.5 9.75h15A2.25 2.25 0 0121.75 12v.75m-8.69-6.44l-2.12-2.12a1.5 1.5 0 00-1.061-.44H4.5A2.25 2.25 0 002.25 6v12a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9a2.25 2.25 0 00-2.25-2.25h-5.379a1.5 1.5 0 01-1.06-.44z" />
        </svg>
        <!-- database -->
        <svg v-else-if="icon === 'database'" class="w-12 h-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
          <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375m16.5 0v3.75m-16.5-3.75v3.75m16.5 0v3.75C20.25 16.153 16.556 18 12 18s-8.25-1.847-8.25-4.125v-3.75m16.5 0c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125" />
        </svg>
        <!-- archive / box -->
        <svg v-else-if="icon === 'archive'" class="w-12 h-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
          <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 7.5l-.625 10.632a2.25 2.25 0 01-2.247 2.118H6.622a2.25 2.25 0 01-2.247-2.118L3.75 7.5m8.25 3v6.75m0 0l-3-3m3 3l3-3M3.375 7.5h17.25c.621 0 1.125-.504 1.125-1.125v-1.5c0-.621-.504-1.125-1.125-1.125H3.375c-.621 0-1.125.504-1.125 1.125v1.5c0 .621.504 1.125 1.125 1.125z" />
        </svg>
        <!-- git-merge / PR -->
        <svg v-else-if="icon === 'git-merge'" class="w-12 h-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
          <path stroke-linecap="round" stroke-linejoin="round" d="M7.5 21L3 16.5m0 0L7.5 12M3 16.5h13.5m0-13.5L21 7.5m0 0L16.5 12M21 7.5H7.5" />
        </svg>
        <!-- check-circle (positive empty = all clear) -->
        <svg v-else-if="icon === 'check-circle'" class="w-12 h-12 text-green-300 dark:text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
          <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <!-- warning (for "not configured" states) -->
        <svg v-else-if="icon === 'warning'" class="w-10 h-10 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
        </svg>
        <!-- search (no matches) -->
        <svg v-else-if="icon === 'search'" class="w-12 h-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
        </svg>
      </slot>
    </div>
    <!-- Message -->
    <p class="text-gray-500 dark:text-gray-400 text-sm">{{ message }}</p>
    <p v-if="hint" class="text-gray-400 dark:text-gray-500 text-xs mt-1">{{ hint }}</p>
    <!-- Action -->
    <router-link
      v-if="actionLabel && actionTo"
      :to="actionTo"
      class="inline-flex items-center gap-1.5 mt-4 px-4 py-2 text-sm font-medium text-primary-600 dark:text-primary-400 bg-primary-50 dark:bg-primary-900/30 border border-primary-200 dark:border-primary-700 rounded-lg hover:bg-primary-100 dark:hover:bg-primary-800/40 transition-colors"
    >
      {{ actionLabel }}
    </router-link>
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
