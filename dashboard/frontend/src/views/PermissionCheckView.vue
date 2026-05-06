<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Permission Overview</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">{{ projectLabel }}</p>
      </div>
      <!-- Simple / Advanced toggle -->
      <div class="flex items-center gap-2">
        <span class="text-xs font-medium" :class="simpleMode ? 'text-primary-600 dark:text-primary-400' : 'text-gray-400 dark:text-gray-500'">Simple</span>
        <button @click="simpleMode = !simpleMode" class="relative w-10 h-5 rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-1"
          :class="simpleMode ? 'bg-gray-300 dark:bg-gray-600' : 'bg-primary-500'">
          <span class="absolute top-0.5 left-0.5 w-4 h-4 bg-white rounded-full shadow transition-transform"
            :class="simpleMode ? 'translate-x-0' : 'translate-x-5'"></span>
        </button>
        <span class="text-xs font-medium" :class="simpleMode ? 'text-gray-400 dark:text-gray-500' : 'text-primary-600 dark:text-primary-400'">Advanced</span>
      </div>
    </div>

    <!-- PAT warning -->
    <div v-if="!store.patConfigured" class="mb-6 bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-amber-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-amber-800 dark:text-amber-200">PAT not configured. Set it in Settings first.</p>
    </div>

    <!-- Error -->
    <div v-if="tabError" class="mb-6 bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-red-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-red-800 dark:text-red-200">{{ tabError }}</p>
    </div>

    <!-- ================================================================ -->
    <!-- SIMPLE MODE -->
    <!-- ================================================================ -->
    <div v-if="simpleMode">
      <div class="flex items-center gap-3 mb-4 flex-wrap">
        <button @click="loadSimpleData(true)" :disabled="store.loadingPermissions || store.loadingRepoPermissions || store.loadingAreaPermissions"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-white shadow-sm transition-colors"
          :class="(store.loadingPermissions || store.loadingRepoPermissions || store.loadingAreaPermissions) ? 'bg-primary-400 cursor-not-allowed' : 'bg-primary-600 hover:bg-primary-700'">
          <svg v-if="store.loadingPermissions || store.loadingRepoPermissions || store.loadingAreaPermissions" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182M2.985 19.644l3.181-3.182" /></svg>
          {{ (store.loadingPermissions || store.loadingRepoPermissions || store.loadingAreaPermissions) ? 'Loading…' : 'Refresh' }}
        </button>

        <!-- Repo selector -->
        <div class="flex items-center gap-1.5">
          <label class="text-xs font-medium text-gray-500 dark:text-gray-400 whitespace-nowrap">Repo:</label>
          <select v-model="selectedRepoId"
            class="text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 dark:text-gray-200 px-2 py-1.5 focus:ring-2 focus:ring-primary-500 outline-none max-w-[220px]">
            <option v-for="r in repoOptions" :key="r.id" :value="r.id">{{ r.name }}</option>
          </select>
        </div>

        <!-- Area selector -->
        <div class="flex items-center gap-1.5">
          <label class="text-xs font-medium text-gray-500 dark:text-gray-400 whitespace-nowrap">Area:</label>
          <select v-model="selectedAreaId"
            class="text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 dark:text-gray-200 px-2 py-1.5 focus:ring-2 focus:ring-primary-500 outline-none max-w-[280px]">
            <option v-for="a in areaOptions" :key="a.id" :value="a.id">{{ a.name }}</option>
          </select>
        </div>

        <input v-model="simpleSearch" type="text" placeholder="Search members…"
          class="text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 dark:text-gray-200 px-3 py-1.5 focus:ring-2 focus:ring-primary-500 outline-none w-64" />
        <span v-if="teamData?.fetched_at" class="text-xs text-gray-400 dark:text-gray-500 ml-auto">
          Fetched: {{ new Date(teamData.fetched_at).toLocaleString() }}
        </span>
      </div>

      <div v-if="(store.loadingPermissions || store.loadingRepoPermissions || store.loadingAreaPermissions) && !teamData" class="flex flex-col items-center py-20">
        <svg class="animate-spin w-8 h-8 text-primary-500 mb-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        <p class="text-sm text-gray-500 dark:text-gray-400">Fetching permissions…</p>
      </div>

      <div v-else-if="teamData && simpleFilteredMembers.length" class="overflow-x-auto">
        <table class="text-xs table-fixed w-full">
          <thead>
            <!-- Group header row -->
            <tr class="bg-gray-50 dark:bg-gray-800/40">
              <th class="sticky left-0 z-10 bg-gray-50 dark:bg-gray-800/40 w-[200px]" rowspan="2"></th>
              <template v-for="group in SIMPLE_CAPABILITIES" :key="group.group">
                <th :colspan="group.items.length"
                  class="px-1 pt-2 pb-0.5 text-center text-[10px] font-bold uppercase tracking-wider border-l border-gray-200 dark:border-gray-700"
                  :class="groupColorClass(group.group)">
                  {{ group.group }}
                </th>
              </template>
            </tr>
            <!-- Capability label row -->
            <tr class="bg-gray-100 dark:bg-gray-800/60">
              <template v-for="group in SIMPLE_CAPABILITIES" :key="'h-' + group.group">
                <th v-for="(cap, ci) in group.items" :key="cap.label"
                  class="w-[52px] px-1 py-2 font-medium text-gray-600 dark:text-gray-300 text-center whitespace-nowrap"
                  :class="ci === 0 ? 'border-l border-gray-200 dark:border-gray-700' : ''"
                  :title="cap.source === 'repo' ? 'Repo: ' + cap.repoPerm : cap.source === 'area' ? 'Area: ' + cap.areaPerm : (cap.checks || []).map(c => c.ns + ' → ' + c.perm).join(', ')">
                  <span class="inline-block max-w-[48px] truncate text-[10px]">{{ cap.label }}</span>
                </th>
              </template>
            </tr>
          </thead>
          <tbody>
            <tr v-for="member in simpleFilteredMembers" :key="member.id"
              class="border-t border-gray-100 dark:border-gray-700/50 hover:bg-gray-50 dark:hover:bg-gray-800/40">
              <td class="sticky left-0 z-10 bg-white dark:bg-gray-900 px-3 py-2 font-medium text-gray-700 dark:text-gray-200 whitespace-nowrap w-[200px]">
                <div class="truncate">{{ member.display_name }}</div>
                <div class="text-[10px] text-gray-400 dark:text-gray-500 font-normal truncate">{{ member.team_names.join(', ') }}</div>
              </td>
              <template v-for="group in SIMPLE_CAPABILITIES" :key="'c-' + group.group">
                <td v-for="(cap, ci) in group.items" :key="cap.label"
                  class="w-[52px] px-1 py-2 text-center"
                  :class="ci === 0 ? 'border-l border-gray-100 dark:border-gray-700/50' : ''">
                  <span :class="simpleCellClass(getSimpleEffective(member.id, cap))"
                    :title="cap.label + ': ' + getSimpleEffective(member.id, cap)"
                    class="inline-flex items-center justify-center w-6 h-6 rounded text-[11px] font-bold">
                    {{ simpleCellIcon(getSimpleEffective(member.id, cap)) }}
                  </span>
                </td>
              </template>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-else-if="teamData && !simpleFilteredMembers.length" class="text-center py-12 text-sm text-gray-400">No members match the search.</div>
      <div v-else-if="!store.loadingPermissions" class="text-center py-20 text-sm text-gray-400 dark:text-gray-500">No data available. Click Refresh.</div>
    </div>

    <!-- ================================================================ -->
    <!-- ADVANCED MODE -->
    <!-- ================================================================ -->
    <template v-else>

    <!-- Tab bar -->
    <div class="border-b border-gray-200 dark:border-gray-700 mb-6">
      <nav class="flex gap-6 -mb-px">
        <button v-for="tab in tabs" :key="tab.id" @click="activeTab = tab.id"
          class="pb-3 text-sm font-medium border-b-2 transition-colors whitespace-nowrap"
          :class="activeTab === tab.id
            ? 'border-primary-500 text-primary-600 dark:text-primary-400'
            : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300 hover:border-gray-300'">
          {{ tab.label }}
        </button>
      </nav>
    </div>

    <!-- ================================================================ -->
    <!-- TAB: Teams & Permissions -->
    <!-- ================================================================ -->
    <div v-if="activeTab === 'teams'">
      <div class="flex items-center gap-3 mb-4">
        <button @click="loadTeams(true)" :disabled="store.loadingPermissions"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-white shadow-sm transition-colors"
          :class="store.loadingPermissions ? 'bg-primary-400 cursor-not-allowed' : 'bg-primary-600 hover:bg-primary-700'">
          <svg v-if="store.loadingPermissions" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182M2.985 19.644l3.181-3.182" /></svg>
          {{ store.loadingPermissions ? 'Loading…' : 'Refresh' }}
        </button>
        <input v-model="teamSearch" type="text" placeholder="Search members…"
          class="text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 dark:text-gray-200 px-3 py-1.5 focus:ring-2 focus:ring-primary-500 outline-none w-64" />
        <span v-if="teamData?.fetched_at" class="text-xs text-gray-400 dark:text-gray-500 ml-auto">
          Fetched: {{ new Date(teamData.fetched_at).toLocaleString() }}
        </span>
      </div>

      <div v-if="store.loadingPermissions && !teamData" class="flex flex-col items-center py-20">
        <svg class="animate-spin w-8 h-8 text-primary-500 mb-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        <p class="text-sm text-gray-500 dark:text-gray-400">Fetching teams & permissions…</p>
      </div>

      <!-- Tree: Team → Category → Permission → Members -->
      <div v-else-if="teamData && teamData.teams?.length" class="space-y-3">
        <div v-for="team in teamData.teams" :key="team.id" class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
          <button @click="toggle('team', team.id)" class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-750 transition-colors text-left">
            <span class="text-sm font-semibold text-gray-700 dark:text-gray-200 flex items-center gap-2">
              <svg class="w-4 h-4 transition-transform" :class="{ 'rotate-90': opened.team?.has(team.id) }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              {{ team.name }}
            </span>
            <span class="text-xs text-gray-400">{{ teamMemberCount(team.name) }} members</span>
          </button>

          <div v-if="opened.team?.has(team.id)" class="pl-4">
            <div v-for="cat in teamData.categories" :key="cat.namespace_id" class="border-t border-gray-100 dark:border-gray-700/50">
              <button @click="toggle('cat', team.id + '|' + cat.namespace_id)" class="w-full flex items-center justify-between px-4 py-2 hover:bg-gray-50 dark:hover:bg-gray-800/40 transition-colors text-left">
                <span class="text-xs font-medium text-gray-600 dark:text-gray-300 flex items-center gap-2">
                  <svg class="w-3 h-3 transition-transform" :class="{ 'rotate-90': opened.cat?.has(team.id + '|' + cat.namespace_id) }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
                  {{ cat.display_name || cat.name }}
                </span>
                <span class="text-[10px] text-gray-400">{{ cat.permissions.length }} permissions</span>
              </button>

              <div v-if="opened.cat?.has(team.id + '|' + cat.namespace_id)" class="pl-6 pb-2">
                <div v-for="perm in cat.permissions" :key="perm.name" class="py-1.5 px-3">
                  <div class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">{{ perm.display_name || perm.name }}</div>
                  <div class="flex flex-wrap gap-1">
                    <template v-for="member in teamMembersOf(team.name)" :key="member.id">
                      <span v-if="matchesMemberSearch(member) && getEffective(member.id, cat.namespace_id, perm.name) !== 'not_set'"
                        class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium"
                        :class="getEffective(member.id, cat.namespace_id, perm.name) === 'allow'
                          ? 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300'
                          : 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300'">
                        {{ getEffective(member.id, cat.namespace_id, perm.name) === 'allow' ? '✓' : '✕' }}
                        {{ member.display_name }}
                      </span>
                    </template>
                    <span v-if="noVisibleMembers(team.name, cat.namespace_id, perm.name)" class="text-[10px] text-gray-400 italic">No explicit permissions</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="!store.loadingPermissions" class="text-center py-20 text-sm text-gray-400 dark:text-gray-500">No team data available.</div>
    </div>

    <!-- ================================================================ -->
    <!-- TAB 2: Repositories -->
    <!-- ================================================================ -->
    <div v-if="activeTab === 'repos'">
      <div class="flex items-center gap-3 mb-4">
        <button @click="loadRepos(true)" :disabled="store.loadingRepoPermissions"
          class="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-white shadow-sm transition-colors"
          :class="store.loadingRepoPermissions ? 'bg-primary-400 cursor-not-allowed' : 'bg-primary-600 hover:bg-primary-700'">
          <svg v-if="store.loadingRepoPermissions" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182M2.985 19.644l3.181-3.182" /></svg>
          {{ store.loadingRepoPermissions ? 'Loading…' : 'Refresh' }}
        </button>
        <input v-model="repoSearch" type="text" placeholder="Search repos…"
          class="text-sm border border-gray-300 dark:border-gray-600 rounded-md bg-white dark:bg-gray-700 dark:text-gray-200 px-3 py-1.5 focus:ring-2 focus:ring-primary-500 outline-none w-64" />
        <span v-if="repoData?.fetched_at" class="text-xs text-gray-400 dark:text-gray-500 ml-auto">
          Fetched: {{ new Date(repoData.fetched_at).toLocaleString() }}
        </span>
      </div>

      <div v-if="store.loadingRepoPermissions && !repoData" class="flex flex-col items-center py-20">
        <svg class="animate-spin w-8 h-8 text-primary-500 mb-4" fill="none" viewBox="0 0 24 24"><circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/><path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/></svg>
        <p class="text-sm text-gray-500 dark:text-gray-400">Fetching repository permissions…</p>
      </div>

      <div v-else-if="repoData && filteredRepos.length" class="space-y-3">
        <div v-for="repo in filteredRepos" :key="repo.repo_id" class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden">
          <button @click="toggle('repo', repo.repo_id)" class="w-full flex items-center justify-between px-4 py-3 bg-gray-50 dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-750 transition-colors text-left">
            <span class="text-sm font-semibold text-gray-700 dark:text-gray-200 flex items-center gap-2">
              <svg class="w-4 h-4 transition-transform" :class="{ 'rotate-90': opened.repo?.has(repo.repo_id) }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" /></svg>
              <svg class="w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M17.25 6.75L22.5 12l-5.25 5.25m-10.5 0L1.5 12l5.25-5.25m7.5-3l-4.5 16.5" /></svg>
              {{ repo.repo_name }}
            </span>
            <span class="text-xs text-gray-400">{{ repo.permissions?.length || 0 }} permissions</span>
          </button>

          <div v-show="opened.repo?.has(repo.repo_id)" class="pl-6 pb-3">
            <div v-for="perm in repo.permissions" :key="perm.name" class="py-1.5 px-3">
              <div class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">{{ perm.display_name || perm.name }}</div>
              <div class="flex flex-wrap gap-1">
                <span v-for="name in perm.members_allowed" :key="'a-' + name"
                  class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300">
                  ✓ {{ name }}
                </span>
                <span v-for="name in perm.members_denied" :key="'d-' + name"
                  class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300">
                  ✕ {{ name }}
                </span>
                <span v-if="!perm.members_allowed?.length && !perm.members_denied?.length" class="text-[10px] text-gray-400 italic">No explicit permissions</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="!store.loadingRepoPermissions" class="text-center py-20 text-sm text-gray-400 dark:text-gray-500">No repositories found.</div>
    </div>

    </template>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useDemoMode, anonAreaPath, anonRepo } from '../composables/useDemoMode.js'
import { transformPermissionMatrix, transformRepoPermissions, transformAreaPermissions } from '../composables/demoTransform.js'

const props = defineProps({ projectId: { type: String, required: true } })

const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

// --- Simple / Advanced toggle ---
const simpleMode = ref(true)

// --- Computed data (used by both modes) ---
const teamData = computed(() => {
  const raw = store.permissionData[props.projectId]
  return raw ? transformPermissionMatrix(raw) : null
})

const repoData = computed(() => {
  const raw = store.repoPermissionData[props.projectId]
  return raw ? transformRepoPermissions(raw) : null
})

const projectLabel = computed(() => {
  const proj = store.displayProjects.find(p => p.id === props.projectId)
  return proj ? proj.project : props.projectId
})

// --- Matrix index for teams tab ---
const matrixIndex = computed(() => {
  if (!teamData.value?.matrix) return {}
  const idx = {}
  for (const e of teamData.value.matrix) {
    idx[`${e.member_id}|${e.namespace_id}|${e.permission_name}`] = e.effective
  }
  return idx
})

// --- Simple Mode: Capability mapping ---
const SIMPLE_CAPABILITIES = [
  { group: 'Code', items: [
    { label: 'View', source: 'repo', repoPerm: 'GenericRead' },
    { label: 'Push', source: 'repo', repoPerm: 'GenericContribute' },
    { label: 'Branches', source: 'repo', repoPerm: 'CreateBranch' },
    { label: 'PRs', source: 'repo', repoPerm: 'PullRequestContribute' },
  ]},
  { group: 'Work Items', items: [
    { label: 'View', source: 'area', areaPerm: 'WORK_ITEM_READ' },
    { label: 'Edit', source: 'area', areaPerm: 'WORK_ITEM_WRITE' },
    { label: 'Delete', checks: [{ ns: 'Project', perm: 'WORK_ITEM_DELETE' }] },
  ]},
  { group: 'Boards', items: [
    { label: 'View', checks: [{ ns: 'Project', perm: 'GENERIC_READ' }] },
  ]},
  { group: 'Pipelines', items: [
    { label: 'View', checks: [{ ns: 'Build', perm: 'ViewBuilds' }] },
    { label: 'Run', checks: [{ ns: 'Build', perm: 'QueueBuilds' }] },
    { label: 'Edit', checks: [{ ns: 'Build', perm: 'EditBuildDefinition' }] },
  ]},
  { group: 'Releases', items: [
    { label: 'View', checks: [{ ns: 'ReleaseManagement', perm: 'ViewReleaseDefinition' }] },
    { label: 'Edit', checks: [{ ns: 'ReleaseManagement', perm: 'EditReleaseDefinition' }] },
    { label: 'Create', checks: [{ ns: 'ReleaseManagement', perm: 'CreateReleases' }] },
    { label: 'Delete', checks: [{ ns: 'ReleaseManagement', perm: 'DeleteReleaseDefinition' }] },
    { label: 'Deploy', checks: [{ ns: 'ReleaseManagement', perm: 'ManageDeployments' }] },
    { label: 'Manage', checks: [{ ns: 'ReleaseManagement', perm: 'ManageReleases' }] },
  ]},
  { group: 'Dashboards', items: [
    { label: 'View', checks: [{ ns: 'DashboardsPrivileges', perm: 'Read' }] },
    { label: 'Edit', checks: [{ ns: 'DashboardsPrivileges', perm: 'Edit' }] },
  ]},
  { group: 'Analytics', items: [
    { label: 'View', checks: [{ ns: 'Analytics', perm: 'Read' }] },
  ]},
  { group: 'Tags', items: [
    { label: 'Create', checks: [{ ns: 'Tagging', perm: 'Create' }] },
  ]},
]

// --- Simple Mode: Selection dropdowns (persisted per project) ---
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

const areaData = computed(() => {
  const raw = store.areaPermissionData[props.projectId]
  return raw ? transformAreaPermissions(raw) : null
})

// Dropdown options: prefer full permission data if available, otherwise use lightweight lists
const repoOptions = computed(() => {
  if (repoData.value?.repos) return repoData.value.repos.map(r => ({ id: r.repo_id, name: r.repo_name }))
  const list = store.repoList[props.projectId]
  if (!list) return []
  return list.map(r => ({ id: r.id, name: isDemoMode.value ? anonRepo(r.name) : r.name }))
})

const areaOptions = computed(() => {
  if (areaData.value?.areas) return areaData.value.areas.map(a => ({ id: a.area_id, name: a.area_path }))
  const list = store.areaList[props.projectId]
  if (!list) return []
  return list.map(a => ({ id: a.id, name: isDemoMode.value ? anonAreaPath(a.name) : a.name }))
})

// Auto-select first repo/area when data arrives
watch(repoOptions, (opts) => {
  if (opts.length && !selectedRepoId.value) selectedRepoId.value = opts[0].id
}, { immediate: true })

watch(areaOptions, (opts) => {
  if (opts.length && !selectedAreaId.value) selectedAreaId.value = opts[0].id
}, { immediate: true })

// --- Simple Mode: Selected item data ---
const selectedRepo = computed(() => {
  if (!repoData.value?.repos || !selectedRepoId.value) return null
  return repoData.value.repos.find(r => r.repo_id === selectedRepoId.value) ?? null
})

const selectedArea = computed(() => {
  if (!areaData.value?.areas || !selectedAreaId.value) return null
  return areaData.value.areas.find(a => a.area_id === selectedAreaId.value) ?? null
})

// Map member ID → display_name for lookup in repo/area data (which uses display names)
const memberNameById = computed(() => {
  if (!teamData.value?.members) return {}
  const map = {}
  for (const m of teamData.value.members) map[m.id] = m.display_name
  return map
})

// Map namespace name → namespace_id from fetched categories
const nsNameToId = computed(() => {
  if (!teamData.value?.categories) return {}
  const map = {}
  for (const cat of teamData.value.categories) {
    map[cat.name] = cat.namespace_id
  }
  return map
})

function getSimpleEffective(memberId, cap) {
  // Repo-based capability (Code columns)
  if (cap.source === 'repo') {
    const repo = selectedRepo.value
    if (!repo) return 'not_set'
    const name = memberNameById.value[memberId]
    if (!name) return 'not_set'
    const perm = repo.permissions.find(p => p.name === cap.repoPerm)
    if (!perm) return 'not_set'
    if (perm.members_denied?.includes(name)) return 'deny'
    if (perm.members_allowed?.includes(name)) return 'allow'
    return 'not_set'
  }
  // Area-based capability (Work Items columns)
  if (cap.source === 'area') {
    const area = selectedArea.value
    if (!area) return 'not_set'
    const name = memberNameById.value[memberId]
    if (!name) return 'not_set'
    const perm = area.permissions.find(p => p.name === cap.areaPerm)
    if (!perm) return 'not_set'
    if (perm.members_denied?.includes(name)) return 'deny'
    if (perm.members_allowed?.includes(name)) return 'allow'
    return 'not_set'
  }
  // Matrix-based capability (everything else)
  const checks = cap.checks || []
  let hasAllow = false
  for (const check of checks) {
    const nsId = nsNameToId.value[check.ns]
    if (!nsId) continue
    const eff = matrixIndex.value[`${memberId}|${nsId}|${check.perm}`]
    if (eff === 'deny') return 'deny'
    if (eff === 'allow') hasAllow = true
  }
  return hasAllow ? 'allow' : 'not_set'
}

function simpleCellIcon(eff) {
  if (eff === 'allow') return '✓'
  if (eff === 'deny') return '✕'
  return '–'
}

function simpleCellClass(eff) {
  if (eff === 'allow') return 'bg-emerald-100 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-300'
  if (eff === 'deny') return 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300'
  return 'bg-gray-100 dark:bg-gray-800 text-gray-400 dark:text-gray-500'
}

function groupColorClass(group) {
  const colors = {
    'Code': 'text-blue-600 dark:text-blue-400',
    'Work Items': 'text-purple-600 dark:text-purple-400',
    'Boards': 'text-indigo-600 dark:text-indigo-400',
    'Pipelines': 'text-cyan-600 dark:text-cyan-400',
    'Releases': 'text-orange-600 dark:text-orange-400',
    'Dashboards': 'text-pink-600 dark:text-pink-400',
    'Analytics': 'text-emerald-600 dark:text-emerald-400',
    'Tags': 'text-amber-600 dark:text-amber-400',
  }
  return colors[group] || 'text-gray-600 dark:text-gray-400'
}

const simpleSearch = ref('')

const simpleFilteredMembers = computed(() => {
  if (!teamData.value?.members) return []
  if (!simpleSearch.value.trim()) return teamData.value.members
  const q = simpleSearch.value.trim().toLowerCase()
  return teamData.value.members.filter(m =>
    m.display_name.toLowerCase().includes(q) || m.unique_name?.toLowerCase().includes(q) ||
    m.team_names.some(t => t.toLowerCase().includes(q))
  )
})

// --- Advanced mode state ---
const tabs = [
  { id: 'teams', label: 'Teams & Permissions' },
  { id: 'repos', label: 'Repositories' },
]
const activeTab = ref('teams')
const tabError = ref('')

const opened = reactive({
  team: new Set(),
  cat: new Set(),
  repo: new Set(),
})

function toggle(type, key) {
  if (opened[type].has(key)) opened[type].delete(key)
  else opened[type].add(key)
  opened[type] = new Set(opened[type])
}

// --- Computed data per tab (advanced mode helpers) ---

function getEffective(memberId, nsId, permName) {
  return matrixIndex.value[`${memberId}|${nsId}|${permName}`] || 'not_set'
}

function teamMembersOf(teamName) {
  if (!teamData.value?.members) return []
  return teamData.value.members.filter(m => m.team_names.includes(teamName))
}

function teamMemberCount(teamName) {
  return teamMembersOf(teamName).length
}

// --- Search & filter ---
const teamSearch = ref('')
const repoSearch = ref('')

function matchesMemberSearch(member) {
  if (!teamSearch.value.trim()) return true
  const q = teamSearch.value.trim().toLowerCase()
  return member.display_name.toLowerCase().includes(q) || member.unique_name?.toLowerCase().includes(q)
}

function noVisibleMembers(teamName, nsId, permName) {
  return teamMembersOf(teamName).filter(m => matchesMemberSearch(m) && getEffective(m.id, nsId, permName) !== 'not_set').length === 0
}

const filteredRepos = computed(() => {
  if (!repoData.value?.repos) return []
  if (!repoSearch.value.trim()) return repoData.value.repos
  const q = repoSearch.value.trim().toLowerCase()
  return repoData.value.repos.filter(r => r.repo_name.toLowerCase().includes(q))
})

// --- Loading ---
async function loadTeams(force = false) {
  tabError.value = ''
  try { await store.fetchPermissions(props.projectId, force) }
  catch (e) { tabError.value = e?.message || 'Failed to fetch team permissions' }
}

async function loadRepos(force = false) {
  tabError.value = ''
  try { await store.fetchRepoPermissions(props.projectId, force) }
  catch (e) { tabError.value = e?.message || 'Failed to fetch repo permissions' }
}

async function loadAreas(force = false) {
  try { await store.fetchAreaPermissions(props.projectId, force) }
  catch (e) { console.error('Failed to fetch area permissions', e) }
}

async function loadSimpleData(force = false) {
  // Instantly populate repo/area dropdowns with lightweight lists,
  // then load full permissions (slow) in parallel
  if (!force) {
    store.fetchRepoList(props.projectId)
    store.fetchAreaList(props.projectId)
  }
  await Promise.all([loadTeams(force), loadRepos(force), loadAreas(force)])
}

watch(activeTab, (tab) => {
  tabError.value = ''
  if (tab === 'teams' && !teamData.value) loadTeams()
  if (tab === 'repos' && !repoData.value) loadRepos()
})

onMounted(() => {
  if (simpleMode.value) loadSimpleData()
  else loadTeams()
})

watch(() => props.projectId, () => {
  tabError.value = ''
  selectedRepoId.value = savedSelection('repo')
  selectedAreaId.value = savedSelection('area')
  if (simpleMode.value) loadSimpleData()
  else if (activeTab.value === 'teams') loadTeams()
  else if (activeTab.value === 'repos') loadRepos()
})

watch(simpleMode, (isSimple) => {
  if (isSimple && !repoData.value) {
    store.fetchRepoList(props.projectId)
    loadRepos()
  }
  if (isSimple && !areaData.value) {
    store.fetchAreaList(props.projectId)
    loadAreas()
  }
})
</script>
