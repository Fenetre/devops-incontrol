<template>
  <aside
    class="bg-sidebar-light dark:bg-sidebar text-gray-300 flex flex-col shrink-0 overflow-hidden transition-all duration-200"
    :class="collapsed ? 'w-16' : 'w-64'"
  >
    <!-- Brand area -->
    <div class="px-3 py-4 border-b border-white/15 flex items-center" :class="collapsed ? 'justify-center' : 'justify-between'">
      <router-link v-if="!collapsed" to="/" class="flex items-center transition-opacity hover:opacity-90">
        <img src="/fenetre-logo.svg" alt="Fenetre logo" class="w-36 h-auto rounded-sm" />
      </router-link>
      <UButton
        @click="collapsed = !collapsed"
        variant="ghost" color="neutral" size="xs"
        icon="i-heroicons-chevron-left"
        :ui="{ base: 'text-white/60 hover:text-white hover:bg-white/10' }"
        :class="{ '[&_svg]:rotate-180': collapsed }"
        :title="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
      />
    </div>

    <!-- Search bar -->
    <div v-if="!collapsed" class="px-3 pt-3 pb-1">
      <UInput
        name="menu-search" v-model="menuSearch"
        placeholder="Search menu…"
        data-sidebar-search
        size="sm"
        icon="i-heroicons-magnifying-glass"
        class="w-full sidebar-search"
      >
        <template v-if="menuSearch" #trailing>
          <UButton @click="menuSearch = ''" variant="link" color="neutral" size="xs" icon="i-heroicons-x-mark" :padded="false" class="text-white/70 hover:text-white" />
        </template>
      </UInput>
    </div>

    <nav class="flex-1 py-2 overflow-y-auto min-h-0">
      <!-- DevOps Monitor section -->
      <div v-show="!isSearching || hasDevopsMatches">
      <button
        @click="collapsed ? (collapsed = false, devopsOpen = true) : (devopsOpen = !devopsOpen)"
        class="w-full flex items-center px-4 py-2.5 text-sm font-semibold text-white/90 hover:bg-white/10 transition-colors"
        :class="collapsed ? 'justify-center' : 'justify-between'"
        :title="collapsed ? 'DevOps Monitor' : ''"
      >
        <span class="flex items-center gap-2">
          <UIcon name="i-heroicons-computer-desktop" class="w-4 h-4 shrink-0" />
          <span v-if="!collapsed">DevOps Monitor</span>
        </span>
        <UIcon v-if="!collapsed" name="i-heroicons-chevron-right" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': devopsOpen }" />
      </button>

      <div v-show="(devopsOpen || isSearching) && !collapsed" class="ml-2 border-l border-white/15">
        <div class="px-4 pt-3 pb-1">
          <span class="text-xs font-semibold uppercase tracking-wider text-white/70 dark:text-white/50">Projects with Issues</span>
        </div>

        <div v-if="filteredSidebarItems.length === 0 && !isSearching" class="px-4 py-6 text-sm text-white/50 text-center">
          <template v-if="!store.results">Run checks to see results</template>
          <template v-else>All clear — no issues found</template>
        </div>
        <div v-else-if="filteredSidebarItems.length === 0 && isSearching" class="px-4 py-3 text-xs text-white/50 text-center italic">
          No matches
        </div>

        <div
          v-for="item in filteredSidebarItems"
          :key="item.projectId"
          class="group"
        >
          <!-- Project name -->
          <div class="flex items-center justify-between px-4 py-2.5 cursor-default hover:bg-white/10 transition-colors">
            <span class="text-sm font-bold text-white truncate">{{ item.projectName }}</span>
            <div class="flex items-center gap-1.5">
              <span v-if="item.hasErrors" class="text-xs text-amber-400" title="Some checks failed">
                <UIcon name="i-heroicons-exclamation-triangle" class="w-3.5 h-3.5" />
              </span>
              <span v-if="item.totalIssues > 0" class="text-xs bg-white/90 text-red-600 dark:bg-red-500/20 dark:text-red-400 rounded-full px-2 py-0.5 font-bold">
                {{ item.totalIssues }}
              </span>
            </div>
          </div>

          <!-- Submenu — always visible -->
          <div class="dark:bg-black/10">
              <router-link
                v-for="check in filteredChecks(item)"
                :key="check.checkType"
                :to="{ name: 'issues', params: { projectId: item.projectId, checkType: check.checkType } }"
                class="flex items-center justify-between pl-8 pr-4 py-2 text-sm hover:bg-white/20 hover:text-white transition-colors"
                :class="isActive(item.projectId, check.checkType) ? 'text-white bg-white/25' : 'text-white/80 dark:text-white/60'"
              >
                <span class="flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full ring-1 ring-white/30" :class="checkColor(check.checkType)"></span>
                  {{ check.label }}
                </span>
                <span class="text-xs opacity-75">{{ check.count }}</span>
              </router-link>
            </div>
        </div>
      </div>
      </div>

      <!-- PR Monitor link -->
      <div v-show="!isSearching || matchesSearch('PR Monitor')">
        <router-link
          :to="prProjects.length ? { name: 'pr-project', params: { projectId: prProjects[0].id } } : '/pr-monitor'"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'pr-project' || route.name === 'pr-monitor' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'PR Monitor' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-arrows-right-left" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">PR Monitor</span>
          </span>
        </router-link>
      </div>

      <!-- DB Monitor link -->
      <div v-show="!isSearching || matchesSearch('DB Monitor')">
        <router-link
          :to="store.displayDbProjects.length ? { name: 'db-project', params: { projectId: store.displayDbProjects[0].id } } : '/db-monitor'"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'db-project' || route.name === 'db-monitor' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'DB Monitor' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-circle-stack" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">DB Monitor</span>
          </span>
        </router-link>
      </div>

      <!-- Sprint Manager link -->
      <div v-show="!isSearching || matchesSearch('Sprint Manager')">
        <router-link
          to="/sprint-populator"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'sprint-populator' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Sprint Manager' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-rocket-launch" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">Sprint Manager</span>
          </span>
        </router-link>
      </div>

      <!-- Template Manager link -->
      <div v-show="!isSearching || matchesSearch('Template Manager')">
        <router-link
          to="/template-manager"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'template-manager' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Template Manager' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-document-duplicate" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">Template Manager</span>
          </span>
        </router-link>
      </div>

      <!-- Capacity & Velocity link -->
      <div v-show="!isSearching || matchesSearch('Capacity & Velocity')">
        <router-link
          to="/velocity"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'velocity' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Capacity & Velocity' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-presentation-chart-line" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">Capacity & Velocity</span>
          </span>
        </router-link>
      </div>

      <!-- Roadmap link -->
      <div v-show="!isSearching || matchesSearch('Roadmap')">
        <router-link
          to="/roadmap"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'roadmap' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Roadmap' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-map" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">Roadmap</span>
          </span>
          <span v-if="!collapsed && roadmapStore.hasDirty" class="text-[10px] font-semibold px-1.5 py-0.5 rounded-full bg-amber-400/20 text-amber-400 animate-pulse">unsaved</span>
        </router-link>
      </div>

      <!-- Permissions link -->
      <div v-show="!isSearching || matchesSearch('Permissions')">
        <router-link
          :to="permCheckProjects.length ? { name: 'permissions', params: { projectId: permCheckProjects[0].id } } : '/'"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'permissions' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Permissions' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-shield-check" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">Permissions</span>
          </span>
        </router-link>
      </div>

      <!-- Pipelines & Releases link -->
      <div v-show="!isSearching || matchesSearch('Pipelines') || matchesSearch('Releases')">
        <router-link
          :to="pipelinesProjects.length ? { name: 'pipelines', params: { projectId: pipelinesProjects[0].id } } : '/'"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'pipelines' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Pipelines & Releases' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-play" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">Pipelines & Releases</span>
          </span>
        </router-link>
      </div>

      <!-- DEV Assessment link -->
      <div v-show="!isSearching || matchesSearch('DEV Assessment')">
        <router-link
          to="/dev-assessment"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'dev-assessment' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'DEV Assessment' : ''"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-heroicons-chart-bar" class="w-4 h-4 shrink-0" />
            <span v-if="!collapsed">DEV Assessment</span>
          </span>
        </router-link>
      </div>
    </nav>

    <!-- Bottom links (pinned) -->
    <div class="border-t border-white/15 px-3 py-3 shrink-0" :class="collapsed ? 'text-center' : ''">
      <router-link
        to="/config"
        class="flex items-center gap-2 text-sm text-white/60 hover:text-white transition-colors"
        :class="collapsed ? 'justify-center' : ''"
        :title="collapsed ? 'Manage Projects' : ''"
      >
        <UIcon name="i-heroicons-cog-6-tooth" class="w-4 h-4 shrink-0" />
        <span v-if="!collapsed">Manage Projects</span>
      </router-link>

      <UButton
        variant="outline" color="neutral" size="xs"
        class="mt-3 w-full"
        :class="collapsed ? 'justify-center' : ''"
        :ui="{ base: 'border-white/15 bg-white/5 text-white/75 hover:bg-white/10 hover:text-white' }"
        title="View release notes"
        @click="$emit('show-release-notes')"
      >
        <span class="text-xs font-semibold tracking-wide">Version ID: {{ appVersion }}</span>
      </UButton>
    </div>
  </aside>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { useRoadmapStore } from '../stores/roadmap.js'
import appPackage from '../../package.json'

defineEmits(['show-release-notes'])

const store = useMonitorStore()
const roadmapStore = useRoadmapStore()
const route = useRoute()
const collapsed = ref(false)
const devopsOpen = ref(true)
const menuSearch = ref('')
const appVersion = appPackage.version

const isSearching = computed(() => menuSearch.value.trim().length > 0)

function matchesSearch(text) {
  if (!isSearching.value) return true
  return text.toLowerCase().includes(menuSearch.value.trim().toLowerCase())
}

const prCheckTypes = ['pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check']

const prProjects = computed(() => store.displayProjects)

// --- Filtered items for search ---
const filteredSidebarItems = computed(() => {
  if (!isSearching.value) return store.sidebarItems
  const q = menuSearch.value.trim().toLowerCase()
  return store.sidebarItems.filter(item => {
    if (item.projectName.toLowerCase().includes(q)) return true
    return item.checks.some(c => !prCheckTypes.includes(c.checkType) && c.label.toLowerCase().includes(q))
  })
})

function filteredChecks(item) {
  const nonPr = item.checks.filter(c => !prCheckTypes.includes(c.checkType))
  if (!isSearching.value) return nonPr
  const q = menuSearch.value.trim().toLowerCase()
  // If project name matches, show all its checks
  if (item.projectName.toLowerCase().includes(q)) return nonPr
  // Otherwise only show matching checks
  return nonPr.filter(c => c.label.toLowerCase().includes(q))
}

const permCheckProjects = computed(() => store.displayProjects)

const pipelinesProjects = computed(() => store.displayProjects)

// Section visibility when searching
const hasDevopsMatches = computed(() => {
  if (!isSearching.value) return true
  return filteredSidebarItems.value.length > 0 || matchesSearch('DevOps Monitor')
})

onMounted(async () => {
  const initTasks = [store.fetchDbProjects()]
  if (!store.projects.length) initTasks.push(store.fetchProjects())
  await Promise.all(initTasks)
  if (store.dbCredentialsConfigured) {
    store.refreshDbSidebar()
  }
  // Prefetch roadmap config & chunk so navigation feels instant
  roadmapStore.loadConfig()
  import('../views/RoadmapView.vue')
})

watch(() => store.dbCredentialsConfigured, (configured) => {
  if (configured && !store.allDatabases.length) {
    store.refreshDbSidebar()
  }
})

function isActive(projectId, checkType) {
  return route.params.projectId === projectId && route.params.checkType === checkType
}

function checkColor(checkType) {
  const colors = {
    orphan_check: 'bg-red-300',
    unassigned_check: 'bg-red-300',
    stale_sprint_check: 'bg-amber-300',
    missing_estimate_check: 'bg-amber-300',
    release_pr_check: 'bg-red-300',
    resolved_pr_check: 'bg-purple-300',
    pr_approval_check: 'bg-teal-300',
    stale_pr_check: 'bg-amber-300',
    unreviewed_pr_check: 'bg-red-300',
  }
  return colors[checkType] || 'bg-gray-300'
}
</script>

<style scoped>
.sidebar-search :deep(input) {
  background-color: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
  color: #fff;
}
.sidebar-search :deep(input::placeholder) {
  color: rgba(255, 255, 255, 0.6);
}
.sidebar-search :deep(input:focus) {
  ring-color: rgba(255, 255, 255, 0.4);
}
.sidebar-search :deep(.iconify),
.sidebar-search :deep(svg) {
  color: rgba(255, 255, 255, 0.6);
}
</style>
