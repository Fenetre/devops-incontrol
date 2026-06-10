<template>
  <div>
    <!-- Breadcrumb -->
    <nav class="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400 mb-4">
      <router-link to="/" class="hover:text-primary-600 transition-colors">Dashboard</router-link>
      <UIcon name="i-heroicons-chevron-right" class="w-3 h-3" />
      <span class="text-gray-700 dark:text-gray-200 font-medium">Permissions</span>
    </nav>

    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Permissions</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">Permission overview, person audit, and project audit.</p>
      </div>
    </div>

    <!-- PAT warning -->
    <UAlert v-if="!store.patConfigured" color="warning" icon="i-heroicons-exclamation-triangle" class="mb-6">
      <template #description>
        A Personal Access Token (PAT) with <strong>Identity (Read)</strong>, <strong>Security (Manage)</strong>, and <strong>Graph (Read)</strong> scopes is required.
        Configure it in <router-link to="/config" class="underline font-medium">Settings</router-link>.
      </template>
    </UAlert>

    <!-- Tab bar -->
    <UTabs :items="tabItems" v-model="activeTab" :content="false" variant="link" class="mb-6" />

    <!-- Overview tab -->
    <div v-if="activeTab === 'overview'">
      <div class="flex gap-4">
        <!-- Side menu: Project / Repo / Area selectors -->
        <div v-if="store.displayProjects.length" class="shrink-0 space-y-3">
          <!-- Project selector -->
          <nav class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
            <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Projects</div>
            <button
              v-for="proj in store.displayProjects" :key="proj.id"
              @click="router.push({ name: 'permissions', params: { projectId: proj.id } })"
              class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
              :class="proj.id === projectId
                ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
                : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
            >
              <UIcon name="i-heroicons-folder" class="w-4 h-4 shrink-0" />
              <span class="whitespace-nowrap">{{ proj.project }}</span>
            </button>
          </nav>

          <!-- Repo selector -->
          <nav v-if="repoOptions.length" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
            <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Repo</div>
            <div class="max-h-[200px] overflow-y-auto">
              <button
                v-for="repo in repoOptions" :key="repo.id"
                @click="selectedRepoId = repo.id"
                class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
                :class="repo.id === selectedRepoId
                  ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
                  : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
              >
                <UIcon name="i-heroicons-code-bracket" class="w-4 h-4 shrink-0" />
                <span class="whitespace-nowrap">{{ repo.name }}</span>
              </button>
            </div>
          </nav>

          <!-- Area selector -->
          <nav v-if="areaOptions.length" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
            <div class="px-4 py-2 text-xs font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-700/30">Area</div>
            <div class="max-h-[200px] overflow-y-auto">
              <button
                v-for="area in areaOptions" :key="area.id"
                @click="selectedAreaId = area.id"
                class="w-full flex items-center gap-2 px-4 py-2.5 text-sm text-left border-t border-gray-100 dark:border-gray-700 transition-colors"
                :class="area.id === selectedAreaId
                  ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-300 font-medium'
                  : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/40'"
              >
                <UIcon name="i-heroicons-rectangle-stack" class="w-4 h-4 shrink-0" />
                <span class="whitespace-nowrap">{{ area.name }}</span>
              </button>
            </div>
          </nav>
        </div>

        <!-- Overview content -->
        <div class="flex-1 min-w-0">
          <PermissionsOverview :project-id="projectId" :selected-repo-id="selectedRepoId" :selected-area-id="selectedAreaId" />
        </div>
      </div>
    </div>

    <!-- Person Audit tab -->
    <div v-if="activeTab === 'person-audit'">
      <PersonAuditTab />
    </div>

    <!-- Project Audit tab -->
    <div v-if="activeTab === 'project-audit'">
      <ProjectAuditTab />
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import { usePermissionStore } from '../stores/permissions.js'
import { useDemoMode, anonRepo, anonAreaPath } from '../composables/useDemoMode.js'
import PermissionsOverview from '../components/permissions/PermissionsOverview.vue'
import PersonAuditTab from '../components/permissions/PersonAuditTab.vue'
import ProjectAuditTab from '../components/permissions/ProjectAuditTab.vue'

const props = defineProps({ projectId: { type: String, required: true } })

const store = useMonitorStore()
const permStore = usePermissionStore()
const router = useRouter()
const { isDemoMode } = useDemoMode()

const activeTab = ref('overview')
const tabItems = [
  { label: 'Overview', value: 'overview', icon: 'i-heroicons-table-cells' },
  { label: 'Person Audit', value: 'person-audit', icon: 'i-heroicons-user-group' },
  { label: 'Project Audit', value: 'project-audit', icon: 'i-heroicons-clipboard-document-check' },
]

// --- Repo / Area selection (sidebar nav) ---
function savedSelection(type) {
  try { return localStorage.getItem(`perm_${props.projectId}_${type}`) || '' } catch { return '' }
}
function saveSelection(type, value) {
  try { if (value) localStorage.setItem(`perm_${props.projectId}_${type}`, value); } catch {}
}

const selectedRepoId = ref(savedSelection('repo'))
const selectedAreaId = ref(savedSelection('area'))

watch(selectedRepoId, v => saveSelection('repo', v))
watch(selectedAreaId, v => saveSelection('area', v))

const repoOptions = computed(() => {
  const list = permStore.repoList[props.projectId]
  if (!list) return []
  return list.map(r => ({ id: r.id, name: isDemoMode.value ? anonRepo(r.name) : r.name }))
})

const areaOptions = computed(() => {
  const list = permStore.areaList[props.projectId]
  if (!list) return []
  return list.map(a => ({ id: a.id, name: isDemoMode.value ? anonAreaPath(a.name) : a.name }))
})

watch(repoOptions, (opts) => {
  if (opts.length && !selectedRepoId.value) selectedRepoId.value = opts[0].id
}, { immediate: true })

watch(areaOptions, (opts) => {
  if (opts.length && !selectedAreaId.value) selectedAreaId.value = opts[0].id
}, { immediate: true })

watch(() => props.projectId, () => {
  selectedRepoId.value = savedSelection('repo')
  selectedAreaId.value = savedSelection('area')
})

onMounted(() => {
  permStore.fetchRepoList(props.projectId)
  permStore.fetchAreaList(props.projectId)
})
</script>
