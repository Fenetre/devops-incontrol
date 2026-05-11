<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-3">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Check Permissions</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Look up person groups and audit project permission setup across all organizations.
        </p>
      </div>
    </div>

    <!-- PAT warning -->
    <div v-if="!store.patConfigured" class="mb-6 p-4 rounded-lg bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-700">
      <p class="text-sm text-amber-800 dark:text-amber-200">
        A Personal Access Token (PAT) with <strong>Identity (Read)</strong>, <strong>Security (Manage)</strong>, and <strong>Graph (Read)</strong> scopes is required.
        Configure it in <router-link to="/config" class="underline font-medium">Settings</router-link>.
      </p>
    </div>

    <!-- Tab bar -->
    <div class="flex border-b border-gray-200 dark:border-gray-700 mb-6">
      <button @click="activeTab = 'person'" class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px"
        :class="activeTab === 'person' ? 'border-primary-500 text-primary-600 dark:text-primary-400' : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'">
        Person Groups
      </button>
      <button @click="activeTab = 'audit'" class="px-4 py-2.5 text-sm font-medium border-b-2 transition-colors -mb-px"
        :class="activeTab === 'audit' ? 'border-primary-500 text-primary-600 dark:text-primary-400' : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'">
        Permission Audit
      </button>
    </div>

    <!-- ======================================================= -->
    <!-- Tab 1: Person Groups                                      -->
    <!-- ======================================================= -->
    <div v-if="activeTab === 'person'">
      <!-- Loading people list -->
      <div v-if="store.loadingPeople" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-6">
        <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
        </svg>
        Loading people from all organizations…
      </div>

      <!-- Person error -->
      <div v-if="personError" class="mb-4 p-3 rounded-lg bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 text-sm text-red-700 dark:text-red-300">
        {{ personError }}
      </div>

      <!-- Person dropdown -->
      <div v-if="store.peopleList" class="mb-6">
        <div class="relative max-w-lg">
          <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
          </svg>
          <input
            v-autofocus
            v-model="personFilter"
            @focus="handleDropdownFocus"
            type="text"
            placeholder="Search for a person…"
            class="w-full pl-10 pr-4 py-2.5 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-200 focus:ring-2 focus:ring-primary-500 outline-none"
          />
          <!-- Dropdown list -->
          <div v-if="dropdownOpen && filteredPeople.length > 0"
            class="absolute z-20 mt-1 w-full max-h-64 overflow-y-auto bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg">
            <button v-for="p in filteredPeople" :key="p.descriptor"
              @click="selectPerson(p)"
              class="w-full flex items-center justify-between px-4 py-2.5 text-sm text-left hover:bg-primary-50 dark:hover:bg-primary-800/60 transition-colors"
              :class="selectedPerson?.descriptor === p.descriptor ? 'bg-primary-100 dark:bg-primary-800/80 font-semibold border-l-3 border-primary-500' : ''">
              <div class="truncate">
                <span class="font-medium text-gray-900 dark:text-gray-100">{{ p.display_name }}</span>
                <span v-if="p.unique_name" class="ml-2 text-xs text-gray-500 dark:text-gray-400">{{ p.unique_name }}</span>
              </div>
              <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 shrink-0">{{ p.organization }}</span>
            </button>
          </div>
          <div v-else-if="dropdownOpen && personFilter.length >= 1 && filteredPeople.length === 0"
            class="absolute z-20 mt-1 w-full bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg px-4 py-3 text-sm text-gray-400 italic">
            No matching people
          </div>
        </div>
        <p class="text-xs text-gray-400 dark:text-gray-500 mt-1">{{ displayPeopleList?.people?.length ?? 0 }} people loaded across all organizations</p>
      </div>

      <!-- Click-outside overlay to close dropdown -->
      <div v-if="dropdownOpen" class="fixed inset-0 z-10" @click="dropdownOpen = false"></div>

      <!-- Selected person's groups -->
      <div v-if="selectedPerson" class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-200 dark:border-gray-700">
          <span class="font-semibold text-gray-900 dark:text-gray-100 text-lg">{{ displaySelectedPerson.display_name }}</span>
          <span v-if="displaySelectedPerson.unique_name" class="ml-2 text-sm text-gray-500 dark:text-gray-400">{{ displaySelectedPerson.unique_name }}</span>
          <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">{{ displaySelectedPerson.organization }}</span>
        </div>
        <div class="px-5 py-4">
          <!-- Loading groups -->
          <div v-if="store.personGroups[selectedPerson.descriptor]?.loading" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
            <svg class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
            </svg>
            Loading groups…
          </div>
          <!-- Groups loaded -->
          <div v-else-if="store.personGroups[selectedPerson.descriptor]?.groups">
            <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-3">Permission Groups ({{ store.personGroups[selectedPerson.descriptor].groups.length }})</h4>
            <div v-if="store.personGroups[selectedPerson.descriptor].groups.length === 0" class="text-sm text-gray-400 italic">No group memberships found</div>
            <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              <div v-for="pg in groupedPersonGroups" :key="pg.project"
                class="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                <div class="px-3 py-2 bg-primary-50 dark:bg-primary-900/20 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
                  <span class="text-xs font-semibold text-gray-800 dark:text-gray-200 truncate">{{ pg.project }}</span>
                  <span class="ml-2 px-1.5 py-0.5 text-[10px] rounded-full bg-primary-100 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 font-medium shrink-0">{{ pg.groups.length }}</span>
                </div>
                <ul class="px-3 py-2 space-y-1 bg-white dark:bg-gray-800">
                  <li v-for="g in pg.groups" :key="g" class="text-sm text-gray-700 dark:text-gray-300 flex items-center gap-2">
                    <span class="w-1.5 h-1.5 rounded-full bg-primary-400 shrink-0"></span>
                    {{ g }}
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ======================================================= -->
    <!-- Tab 2: Permission Audit                                   -->
    <!-- ======================================================= -->
    <div v-if="activeTab === 'audit'">
      <div class="flex items-center gap-3 mb-6">
        <button @click="doAudit" :disabled="store.loadingPermissionAudit"
          class="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-semibold text-white shadow-sm transition-colors"
          :class="store.loadingPermissionAudit ? 'bg-primary-400 cursor-not-allowed' : 'bg-primary-600 hover:bg-primary-700'">
          <svg v-if="store.loadingPermissionAudit" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
          </svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          {{ store.loadingPermissionAudit ? 'Running Audit…' : 'Run Audit' }}
        </button>
        <button v-if="displayAuditResults" @click="doAudit(true)"
          :disabled="store.loadingPermissionAudit"
          class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium border transition-colors"
          :class="store.loadingPermissionAudit ? 'border-gray-200 dark:border-gray-700 text-gray-400 cursor-not-allowed' : 'border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-750'">
          Force Refresh
        </button>
        <button @click="toggleScopePanel"
          class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium border transition-colors"
          :class="showScopePanel
            ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-400 border-primary-300 dark:border-primary-600'
            : 'border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-750'">
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 6h9.75M10.5 6a1.5 1.5 0 11-3 0m3 0a1.5 1.5 0 10-3 0M3.75 6H7.5m3 12h9.75m-9.75 0a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m-3.75 0H7.5m9-6h3.75m-3.75 0a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m-9.75 0h9.75" />
          </svg>
          Manage Scope
          <span v-if="store.auditDenylist.length" class="px-1.5 py-0.5 text-[10px] rounded-full bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300 font-medium">{{ store.auditDenylist.length }}</span>
        </button>
        <button @click="showConfigPanel = !showConfigPanel"
          class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium border transition-colors"
          :class="showConfigPanel
            ? 'bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-400 border-primary-300 dark:border-primary-600'
            : 'border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-750'">
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
          </svg>
          Configure Audit
        </button>
        <span v-if="displayAuditResults" class="text-xs text-gray-400 dark:text-gray-500">
          Last run: {{ formatTime(displayAuditResults.fetched_at) }}
        </span>
      </div>

      <!-- Scope Management Panel -->
      <div v-if="showScopePanel" class="mb-6 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
        <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200">Audit Scope</h3>
          <div v-if="store.loadingAuditProjects" class="flex items-center gap-2 text-xs text-gray-400">
            <svg class="animate-spin w-3.5 h-3.5" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
            </svg>
            Loading projects…
          </div>
        </div>
        <div class="grid grid-cols-2 divide-x divide-gray-200 dark:divide-gray-700">
          <!-- In Scope -->
          <div class="p-4">
            <h4 class="text-xs font-semibold uppercase text-green-600 dark:text-green-400 mb-2">In Scope ({{ scopeProjects.length }})</h4>
            <div class="space-y-1 max-h-64 overflow-y-auto">
              <div v-if="scopeProjects.length === 0" class="text-xs text-gray-400 italic py-2">No projects in scope</div>
              <button v-for="p in scopeProjects" :key="p.project_id"
                @click="denylistAuditProject(p._realOrg || p.organization, p.project_id)"
                class="w-full flex items-center justify-between px-3 py-2 text-sm rounded-lg hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors group text-left"
                title="Move to denylist">
                <div class="truncate">
                  <span class="text-gray-800 dark:text-gray-200">{{ p.project }}</span>
                  <span class="ml-1.5 text-xs text-gray-400">{{ p.organization }}</span>
                </div>
                <svg class="w-4 h-4 text-gray-300 group-hover:text-red-500 shrink-0 ml-2 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 4.5L21 12m0 0l-7.5 7.5M21 12H3" />
                </svg>
              </button>
            </div>
          </div>
          <!-- Denylisted -->
          <div class="p-4">
            <h4 class="text-xs font-semibold uppercase text-red-600 dark:text-red-400 mb-2">Denylisted ({{ denylistProjects.length }})</h4>
            <div class="space-y-1 max-h-64 overflow-y-auto">
              <div v-if="denylistProjects.length === 0" class="text-xs text-gray-400 italic py-2">No denylisted projects</div>
              <button v-for="p in denylistProjects" :key="p.project_id"
                @click="removeAuditProjectFromDenylist(p._realOrg || p.organization, p.project_id)"
                class="w-full flex items-center justify-between px-3 py-2 text-sm rounded-lg hover:bg-green-50 dark:hover:bg-green-900/20 transition-colors group text-left"
                title="Move back to scope">
                <svg class="w-4 h-4 text-gray-300 group-hover:text-green-500 shrink-0 mr-2 transition-colors" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 19.5L3 12m0 0l7.5-7.5M3 12h18" />
                </svg>
                <div class="truncate text-right">
                  <span class="text-gray-800 dark:text-gray-200">{{ p.project }}</span>
                  <span class="ml-1.5 text-xs text-gray-400">{{ p.organization }}</span>
                </div>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Audit Configuration Panel -->
      <div v-if="showConfigPanel" class="mb-6 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
        <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200">Audit Configuration</h3>
        </div>

        <!-- Group Connections Matrix -->
        <div class="p-5">
          <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-3">Group Connections</h4>
          <div v-if="localGroupConfig.length" class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase">
                  <th class="pb-2 pr-4">Permission Group</th>
                  <th class="pb-2 px-4 text-center w-20">Area</th>
                  <th class="pb-2 px-4 text-center w-20">Repo</th>
                  <th class="pb-2 px-4 text-center w-20">Wiki</th>
                  <th class="pb-2 pl-4 w-10"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(gc, idx) in localGroupConfig" :key="gc.group_name"
                  class="border-t border-gray-100 dark:border-gray-750">
                  <td class="py-2 pr-4 text-gray-800 dark:text-gray-200">{{ gc.group_name }}</td>
                  <td class="py-2 px-4 text-center">
                    <input type="checkbox" v-model="gc.area_connected" @change="persistConfig"
                      class="w-4 h-4 text-primary-600 bg-gray-100 border-gray-300 rounded focus:ring-primary-500 dark:bg-gray-700 dark:border-gray-600" />
                  </td>
                  <td class="py-2 px-4 text-center">
                    <input type="checkbox" v-model="gc.repo_connected" @change="persistConfig"
                      class="w-4 h-4 text-primary-600 bg-gray-100 border-gray-300 rounded focus:ring-primary-500 dark:bg-gray-700 dark:border-gray-600" />
                  </td>
                  <td class="py-2 px-4 text-center">
                    <input type="checkbox" v-model="gc.wiki_connected" @change="persistConfig"
                      class="w-4 h-4 text-primary-600 bg-gray-100 border-gray-300 rounded focus:ring-primary-500 dark:bg-gray-700 dark:border-gray-600" />
                  </td>
                  <td class="py-2 pl-4 text-center">
                    <button @click="removeGroupFromMatrix(idx)"
                      class="text-gray-400 hover:text-red-500 transition-colors text-lg leading-none" title="Remove group">&times;</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p v-else class="text-xs text-gray-400 italic">No group connections configured yet.</p>
          <div v-if="availableGroupsForMatrix.length" class="mt-3 flex items-center gap-2">
            <SelectMenu v-model="newGroupForMatrix" :options="matrixGroupOptions" placeholder="Add group…" size="sm" class="w-64" />
            <button v-if="newGroupForMatrix" @click="addGroupToMatrix"
              class="px-3 py-1.5 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg transition-colors">Add</button>
          </div>
        </div>

        <!-- Audit Rules -->
        <div class="px-5 pb-5 border-t border-gray-200 dark:border-gray-700 pt-4">
          <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-3">Audit Rules</h4>

          <!-- Team deny (single toggle) -->
          <div class="flex items-center justify-between py-2 px-3 rounded-lg bg-gray-100/60 dark:bg-gray-750">
            <span class="text-sm text-gray-700 dark:text-gray-300">Teams are not allowed in permission groups</span>
            <button @click="toggleTeamDeny"
              class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500"
              :class="teamDenyEnabled ? 'bg-primary-500' : 'bg-gray-300 dark:bg-gray-600'"
              role="switch" :aria-checked="teamDenyEnabled">
              <span class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out"
                :class="teamDenyEnabled ? 'translate-x-4' : 'translate-x-0'"></span>
            </button>
          </div>

          <!-- Customer deny rules -->
          <div class="mt-4">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2">Customer deny rules</div>
            <div v-for="(r, idx) in customerDenyRules" :key="'cd-' + idx"
              class="flex items-center gap-3 py-2 px-3 rounded-lg hover:bg-gray-100/60 dark:hover:bg-gray-750 transition-colors">
              <button @click="toggleRuleEnabled(r)"
                class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500"
                :class="r.enabled ? 'bg-primary-500' : 'bg-gray-300 dark:bg-gray-600'"
                role="switch" :aria-checked="r.enabled">
                <span class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out"
                  :class="r.enabled ? 'translate-x-4' : 'translate-x-0'"></span>
              </button>
              <span class="text-sm text-gray-700 dark:text-gray-300 flex-1">Customer not allowed in <span class="font-semibold text-gray-900 dark:text-gray-100">{{ r.group_name }}</span></span>
              <button @click="removeRule('customer_deny', idx)" class="text-gray-400 hover:text-red-500 transition-colors text-lg leading-none" title="Remove rule">&times;</button>
            </div>
            <div v-if="!customerDenyRules.length" class="text-xs text-gray-400 italic px-3 py-1">No customer deny rules configured.</div>
            <div class="flex items-center gap-2 mt-2">
              <SelectMenu v-model="newCustomerDenyGroup" :options="customerDenyGroupOptions" placeholder="Add customer deny rule…" size="sm" class="w-64" />
              <button v-if="newCustomerDenyGroup" @click="addRule('customer_deny', newCustomerDenyGroup); newCustomerDenyGroup = ''"
                class="px-3 py-1.5 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg transition-colors">Add</button>
            </div>
          </div>

          <!-- Mandatory group rules -->
          <div class="mt-4">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2">Mandatory group rules</div>
            <div v-for="(r, idx) in mandatoryGroupRules" :key="'mg-' + idx"
              class="flex items-center gap-3 py-2 px-3 rounded-lg hover:bg-gray-100/60 dark:hover:bg-gray-750 transition-colors">
              <button @click="toggleRuleEnabled(r)"
                class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-primary-500"
                :class="r.enabled ? 'bg-primary-500' : 'bg-gray-300 dark:bg-gray-600'"
                role="switch" :aria-checked="r.enabled">
                <span class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out"
                  :class="r.enabled ? 'translate-x-4' : 'translate-x-0'"></span>
              </button>
              <span class="text-sm text-gray-700 dark:text-gray-300 flex-1">Group <span class="font-semibold text-gray-900 dark:text-gray-100">{{ r.group_name }}</span> is mandatory for every project</span>
              <button @click="removeRule('mandatory_group', idx)" class="text-gray-400 hover:text-red-500 transition-colors text-lg leading-none" title="Remove rule">&times;</button>
            </div>
            <div v-if="!mandatoryGroupRules.length" class="text-xs text-gray-400 italic px-3 py-1">No mandatory group rules configured.</div>
            <div class="flex items-center gap-2 mt-2">
              <SelectMenu v-model="newMandatoryGroup" :options="mandatoryGroupOptions" placeholder="Add mandatory group rule…" size="sm" class="w-64" />
              <button v-if="newMandatoryGroup" @click="addRule('mandatory_group', newMandatoryGroup); newMandatoryGroup = ''"
                class="px-3 py-1.5 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 rounded-lg transition-colors">Add</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Audit error -->
      <div v-if="auditError" class="mb-4 p-3 rounded-lg bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 text-sm text-red-700 dark:text-red-300">
        {{ auditError }}
      </div>

      <!-- Audit summary + filter -->
      <div v-if="displayAuditResults && displayAuditResults.projects.length > 0" class="flex items-center gap-3 mb-4">
        <SelectMenu v-model="auditFilter" :options="auditFilterOptions" size="sm" class="w-40" />
        <span class="text-xs text-gray-500 dark:text-gray-400">
          {{ filteredAuditProjects.length }} of {{ displayAuditResults.projects.length }} projects
        </span>
        <span v-if="issueCount > 0" class="px-2 py-0.5 text-xs rounded-full bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300 font-medium">
          {{ issueCount }} project{{ issueCount !== 1 ? 's' : '' }} with issues
        </span>
        <span v-else class="px-2 py-0.5 text-xs rounded-full bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300 font-medium">
          All projects OK
        </span>
      </div>

      <!-- Audit results -->
      <div v-if="displayAuditResults && !store.loadingPermissionAudit" class="space-y-3">
        <div v-if="filteredAuditProjects.length === 0" class="text-center py-12 text-gray-400 dark:text-gray-500">
          <p class="text-sm">No projects match the current filter.</p>
        </div>

        <div v-for="project in filteredAuditProjects" :key="project.project_id"
          class="bg-white dark:bg-gray-800 rounded-lg border overflow-hidden"
          :class="hasIssues(project) ? 'border-red-200 dark:border-red-800' : 'border-green-200 dark:border-green-800'">
          <button @click="toggleProject(project.project_id)"
            class="w-full flex items-center justify-between px-5 py-3.5 text-left hover:bg-gray-50 dark:hover:bg-gray-750 transition-colors">
            <div class="flex items-center gap-3">
              <!-- Status icon -->
              <span v-if="hasIssues(project)" class="flex items-center justify-center w-6 h-6 rounded-full bg-red-100 dark:bg-red-900/40">
                <svg class="w-4 h-4 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </span>
              <span v-else class="flex items-center justify-center w-6 h-6 rounded-full bg-green-100 dark:bg-green-900/40">
                <svg class="w-4 h-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                </svg>
              </span>
              <div>
                <span class="font-semibold text-gray-900 dark:text-gray-100">{{ project.project }}</span>
                <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">{{ project.organization }}</span>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <!-- Denylist project -->
              <button @click.stop="denylistAuditProject(project._realOrg || project.organization, project.project_id)"
                class="p-1 rounded-md text-gray-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors"
                title="Denylist this project">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
                </svg>
              </button>
              <!-- Refresh single project -->
              <button @click.stop="refreshProject(project)"
                :disabled="refreshingProjects.has(project.project_id)"
                class="p-1 rounded-md text-gray-400 hover:text-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/30 transition-colors disabled:opacity-50"
                title="Refresh this project">
                <svg class="w-4 h-4" :class="{ 'animate-spin': refreshingProjects.has(project.project_id) }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182M20.015 4.356v4.992" />
                </svg>
              </button>
              <!-- Check summary badges -->
              <span v-if="project.checks.missing_groups.length" class="px-2 py-0.5 text-xs rounded-full bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300">
                {{ project.checks.missing_groups.length }} missing group{{ project.checks.missing_groups.length !== 1 ? 's' : '' }}
              </span>
              <span v-if="project.checks.teams_in_groups.length" class="px-2 py-0.5 text-xs rounded-full bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300">
                {{ project.checks.teams_in_groups.length }} team{{ project.checks.teams_in_groups.length !== 1 ? 's' : '' }} in groups
              </span>
              <span v-if="project.checks.repos_missing_groups.length" class="px-2 py-0.5 text-xs rounded-full bg-orange-100 dark:bg-orange-900/40 text-orange-700 dark:text-orange-300">
                {{ project.checks.repos_missing_groups.length }} repo{{ project.checks.repos_missing_groups.length !== 1 ? 's' : '' }}
              </span>
              <span v-if="wikiIssues(project).length || project.checks.wiki_unwanted_groups?.length" class="px-2 py-0.5 text-xs rounded-full bg-purple-100 dark:bg-purple-900/40 text-purple-700 dark:text-purple-300">
                wiki
              </span>
              <span v-else-if="wikiSkipped(project)" class="px-2 py-0.5 text-xs rounded-full bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400">
                wiki skipped
              </span>
              <span v-if="project.checks.areas_missing_groups?.length" class="px-2 py-0.5 text-xs rounded-full bg-teal-100 dark:bg-teal-900/40 text-teal-700 dark:text-teal-300">
                {{ project.checks.areas_missing_groups.length }} area{{ project.checks.areas_missing_groups.length !== 1 ? 's' : '' }}
              </span>
              <svg class="w-4 h-4 transition-transform text-gray-400" :class="{ 'rotate-180': expandedProjects.has(project.project_id) }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
              </svg>
            </div>
          </button>

          <!-- Expanded detail -->
          <div v-if="expandedProjects.has(project.project_id)" class="border-t border-gray-200 dark:border-gray-700 divide-y divide-gray-100 dark:divide-gray-750">

            <!-- Check 1: Missing Groups -->
            <div class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Mandatory Permission Groups</h4>
              <div v-if="mandatoryGroupNames.length" class="flex flex-wrap gap-2">
                <span v-for="g in mandatoryGroupNames" :key="g"
                  class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs rounded-full"
                  :class="project.checks.missing_groups.includes(g)
                    ? 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300'
                    : 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300'">
                  <svg v-if="!project.checks.missing_groups.includes(g)" class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                  </svg>
                  <svg v-else class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                  {{ g }}
                </span>
              </div>
              <p v-else class="text-xs text-gray-400 italic">No mandatory group rules configured. Set up rules in <button @click="showConfigPanel = true" class="underline text-primary-500">Configure Audit</button>.</p>
            </div>

            <!-- Available Groups -->
            <div v-if="project.checks.available_groups?.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">All Available Permission Groups ({{ project.checks.available_groups.length }})</h4>
              <div class="flex flex-wrap gap-1.5">
                <span v-for="g in project.checks.available_groups" :key="g"
                  class="px-2 py-0.5 text-xs rounded-full bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300">
                  {{ g }}
                </span>
              </div>
            </div>

            <!-- Check 2: Teams in Groups -->
            <div v-if="project.checks.teams_in_groups.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Teams Found in Groups (not allowed)</h4>
              <ul class="space-y-1">
                <li v-for="t in project.checks.teams_in_groups" :key="t.group + t.team_name" class="text-sm text-gray-700 dark:text-gray-300 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-amber-400 shrink-0"></span>
                  <span class="font-medium">{{ t.group }}</span>
                  <span class="text-gray-400">contains team</span>
                  <span class="font-medium text-amber-600 dark:text-amber-400">{{ t.team_name }}</span>
                </li>
              </ul>
            </div>

            <!-- Check 3: Repos Missing Groups -->
            <div v-if="project.checks.repos_missing_groups.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Repositories Missing Permission Groups</h4>
              <div class="space-y-2 max-h-60 overflow-y-auto">
                <div v-for="r in project.checks.repos_missing_groups" :key="r.repo_name" class="text-sm">
                  <span class="font-medium text-gray-900 dark:text-gray-100">{{ r.repo_name }}</span>
                  <span class="text-gray-400 mx-1">—</span>
                  <span v-for="(mg, mi) in r.missing_groups" :key="mg" class="text-orange-600 dark:text-orange-400 text-xs">
                    {{ mg }}<span v-if="mi < r.missing_groups.length - 1">, </span>
                  </span>
                </div>
              </div>
            </div>

            <!-- Check 4: Wiki Missing Groups -->
            <div v-if="wikiIssues(project).length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Wiki Security — Missing Board Groups</h4>
              <ul class="space-y-1">
                <li v-for="w in wikiIssues(project)" :key="w" class="text-sm text-purple-600 dark:text-purple-400 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-purple-400 shrink-0"></span>
                  {{ w }}
                </li>
              </ul>
            </div>
            <div v-if="project.checks.wiki_unwanted_groups?.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Wiki Security — Unwanted Repository Groups</h4>
              <ul class="space-y-1">
                <li v-for="w in project.checks.wiki_unwanted_groups" :key="w" class="text-sm text-red-600 dark:text-red-400 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-red-400 shrink-0"></span>
                  {{ w }}
                </li>
              </ul>
            </div>
            <div v-else-if="wikiSkipped(project)" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Wiki Security</h4>
              <p class="text-sm text-gray-400 dark:text-gray-500 italic">Skipped — PAT requires 'vso.wiki' scope</p>
            </div>

            <!-- Check 5: Areas Missing Board Groups -->
            <div v-if="project.checks.areas_missing_groups?.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-2">Area Paths Missing Board Groups</h4>
              <div class="space-y-2 max-h-60 overflow-y-auto">
                <div v-for="a in project.checks.areas_missing_groups" :key="a.area_path" class="text-sm">
                  <span class="font-medium text-gray-900 dark:text-gray-100">{{ a.area_path }}</span>
                  <span class="text-gray-400 mx-1">—</span>
                  <span v-for="(mg, mi) in a.missing_groups" :key="mg" class="text-teal-600 dark:text-teal-400 text-xs">
                    {{ mg }}<span v-if="mi < a.missing_groups.length - 1">, </span>
                  </span>
                </div>
              </div>
            </div>

            <!-- All clear -->
            <div v-if="!hasIssues(project)" class="px-5 py-3 text-sm text-green-600 dark:text-green-400">
              All checks passed.
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, reactive, watch, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useDemoMode, anonName, anonEmail, anonOrg, anonProject } from '../composables/useDemoMode.js'
import { transformPeopleList, transformPersonGroups, transformPermissionAudit, transformAuditProjects } from '../composables/demoTransform.js'
import SelectMenu from '../components/SelectMenu.vue'

const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

const activeTab = ref('person')
const personFilter = ref('')
const dropdownOpen = ref(false)
const selectedPerson = ref(null)
const personError = ref('')
const auditError = ref('')
const auditFilter = ref('issues')
const expandedProjects = reactive(new Set())
const refreshingProjects = reactive(new Set())
const showScopePanel = ref(true)
const showConfigPanel = ref(false)
const localGroupConfig = ref([])
const localRules = ref([])
const newGroupForMatrix = ref('')
const newCustomerDenyGroup = ref('')
const newMandatoryGroup = ref('')
const auditFilterOptions = [
  { value: 'all', label: 'All projects' },
  { value: 'issues', label: 'With issues only' },
  { value: 'ok', label: 'Passed only' },
]

const mandatoryGroupNames = computed(() =>
  localRules.value
    .filter(r => r.rule_type === 'mandatory_group' && r.enabled)
    .map(r => r.group_name)
)

// Load people + audit scope data on mount (all in parallel so scope panel is ready quickly)
onMounted(async () => {
  await Promise.all([
    store.fetchPeople().catch(e => {
      personError.value = e?.message || 'Failed to load people. Check PAT configuration and Graph scope.'
    }),
    store.fetchAuditProjects(),
    store.fetchAuditDenylist().catch(() => {}),
    store.fetchAuditConfig(),
  ])
  // Initialize local config state from loaded data
  localGroupConfig.value = JSON.parse(JSON.stringify(store.auditConfig.group_config || []))
  localRules.value = JSON.parse(JSON.stringify(store.auditConfig.rules || []))
})

// Demo-aware people list
const displayPeopleList = computed(() => {
  void isDemoMode.value
  return transformPeopleList(store.peopleList)
})

// Filtered people list for the dropdown
const filteredPeople = computed(() => {
  if (!displayPeopleList.value?.people) return []
  const q = personFilter.value.trim().toLowerCase()
  if (!q) return displayPeopleList.value.people.slice(0, 50) // show first 50 when no filter
  return displayPeopleList.value.people.filter(p =>
    p.display_name.toLowerCase().includes(q) ||
    (p.unique_name && p.unique_name.toLowerCase().includes(q))
  ).slice(0, 50)
})

// The selected person's display fields (anonymised if demo mode)
const displaySelectedPerson = computed(() => {
  if (!selectedPerson.value) return null
  void isDemoMode.value
  if (!isDemoMode.value) return selectedPerson.value
  return {
    ...selectedPerson.value,
    display_name: anonName(selectedPerson.value.display_name),
    unique_name: anonEmail(selectedPerson.value.unique_name),
    organization: anonOrg(selectedPerson.value.organization),
  }
})

function handleDropdownFocus() {
  if (selectedPerson.value) {
    personFilter.value = ''
  }
  dropdownOpen.value = true
}

async function selectPerson(person) {
  selectedPerson.value = person
  personFilter.value = isDemoMode.value ? anonName(person.display_name) : person.display_name
  dropdownOpen.value = false
  try {
    await store.fetchPersonGroups(person.organization, person.descriptor)
  } catch (e) {
    // Error is handled in the template via store state
  }
}

// Group the person's permission groups by project (parsed from "[Project]\Group" format)
const groupedPersonGroups = computed(() => {
  if (!selectedPerson.value) return []
  void isDemoMode.value
  const raw = store.personGroups[selectedPerson.value.descriptor]
  const data = transformPersonGroups(raw)
  if (!data?.groups) return []

  const map = {}
  for (const g of data.groups) {
    const name = g.display_name || ''
    // Azure DevOps groups use format: [ProjectName]\GroupName or just GroupName
    const match = name.match(/^\[(.+?)\]\\(.+)$/)
    const project = match ? match[1] : 'Organization-level'
    const groupName = match ? match[2] : name
    if (!map[project]) map[project] = []
    map[project].push(groupName)
  }

  // Sort projects alphabetically, but put Organization-level last
  return Object.entries(map)
    .sort(([a], [b]) => {
      if (a === 'Organization-level') return 1
      if (b === 'Organization-level') return -1
      return a.localeCompare(b)
    })
    .map(([project, groups]) => ({ project, groups: groups.sort() }))
})

// Demo-aware audit results
const displayAuditResults = computed(() => {
  void isDemoMode.value
  return transformPermissionAudit(store.permissionAuditResults)
})

// Demo-aware audit projects (scope panel)
const displayAuditProjects = computed(() => {
  void isDemoMode.value
  return transformAuditProjects(store.auditProjects)
})

// Audit
const denylistSet = computed(() => new Set(store.auditDenylist.map(k => k.toLowerCase())))

function auditKey(org, projectId) {
  return `${org}/${projectId}`
}

const issueCount = computed(() => {
  if (!displayAuditResults.value) return 0
  return displayAuditResults.value.projects.filter(p =>
    !denylistSet.value.has(auditKey(p._realOrg || p.organization, p.project_id).toLowerCase()) && hasIssues(p)
  ).length
})

async function doAudit(force = false) {
  auditError.value = ''
  expandedProjects.clear()
  try {
    await store.runPermissionAudit(force)
    showScopePanel.value = false
    // Auto-expand projects with issues (exclude denylisted)
    if (displayAuditResults.value) {
      for (const p of displayAuditResults.value.projects) {
        if (hasIssues(p) && !denylistSet.value.has(auditKey(p._realOrg || p.organization, p.project_id).toLowerCase())) {
          expandedProjects.add(p.project_id)
        }
      }
    }
  } catch (e) {
    auditError.value = e?.message || 'Failed to run audit. Check PAT configuration and Graph API scope.'
  }
}

function wikiSkipped(project) {
  return project.checks.wiki_missing_groups.some(w => w.startsWith('Skipped:'))
}
function wikiIssues(project) {
  return project.checks.wiki_missing_groups.filter(w => !w.startsWith('Skipped:'))
}

function hasIssues(project) {
  const c = project.checks
  return c.missing_groups.length > 0 ||
    c.teams_in_groups.length > 0 ||
    c.repos_missing_groups.length > 0 ||
    wikiIssues(project).length > 0 ||
    (c.wiki_unwanted_groups?.length ?? 0) > 0 ||
    (c.areas_missing_groups?.length ?? 0) > 0
}

async function refreshProject(project) {
  refreshingProjects.add(project.project_id)
  try {
    await store.refreshSingleProjectAudit(project._realOrg || project.organization, project.project_id)
  } catch (e) {
    console.error('Failed to refresh project', e)
  } finally {
    refreshingProjects.delete(project.project_id)
  }
}

const filteredAuditProjects = computed(() => {
  if (!displayAuditResults.value) return []
  const projects = displayAuditResults.value.projects.filter(p =>
    !denylistSet.value.has(auditKey(p._realOrg || p.organization, p.project_id).toLowerCase())
  )
  if (auditFilter.value === 'issues') return projects.filter(hasIssues)
  if (auditFilter.value === 'ok') return projects.filter(p => !hasIssues(p))
  return projects
})

// Scope panel: projects split into scope vs denylisted
const scopeProjects = computed(() => {
  if (!displayAuditProjects.value.length) return []
  return displayAuditProjects.value.filter(p =>
    !denylistSet.value.has(auditKey(p._realOrg || p.organization, p.project_id).toLowerCase())
  )
})

const denylistProjects = computed(() => {
  if (!displayAuditProjects.value.length) return []
  return displayAuditProjects.value.filter(p =>
    denylistSet.value.has(auditKey(p._realOrg || p.organization, p.project_id).toLowerCase())
  )
})

function toggleScopePanel() {
  showScopePanel.value = !showScopePanel.value
}

// --- Audit Configuration ---
const allKnownGroups = computed(() => {
  const set = new Set()
  if (store.permissionAuditResults) {
    for (const p of store.permissionAuditResults.projects) {
      if (p.checks.available_groups) {
        for (const g of p.checks.available_groups) set.add(g)
      }
    }
  }
  for (const gc of localGroupConfig.value) set.add(gc.group_name)
  for (const r of localRules.value) {
    if (r.group_name) set.add(r.group_name)
  }
  return [...set].sort()
})

const availableGroupsForMatrix = computed(() => {
  const existing = new Set(localGroupConfig.value.map(gc => gc.group_name))
  return allKnownGroups.value.filter(g => !existing.has(g))
})

const teamDenyEnabled = computed(() =>
  localRules.value.some(r => r.rule_type === 'team_deny' && r.enabled)
)

const customerDenyRules = computed(() =>
  localRules.value.filter(r => r.rule_type === 'customer_deny')
)

const mandatoryGroupRules = computed(() =>
  localRules.value.filter(r => r.rule_type === 'mandatory_group')
)

const availableCustomerDenyGroups = computed(() => {
  const existing = new Set(customerDenyRules.value.map(r => r.group_name))
  return allKnownGroups.value.filter(g => !existing.has(g))
})

const availableMandatoryGroups = computed(() => {
  const existing = new Set(mandatoryGroupRules.value.map(r => r.group_name))
  return allKnownGroups.value.filter(g => !existing.has(g))
})

const matrixGroupOptions = computed(() =>
  availableGroupsForMatrix.value.map(g => ({ value: g, label: g }))
)
const customerDenyGroupOptions = computed(() =>
  availableCustomerDenyGroups.value.map(g => ({ value: g, label: g }))
)
const mandatoryGroupOptions = computed(() =>
  availableMandatoryGroups.value.map(g => ({ value: g, label: g }))
)

async function persistConfig() {
  await store.saveAuditConfig({
    group_config: localGroupConfig.value,
    rules: localRules.value,
  })
}

function addGroupToMatrix() {
  if (!newGroupForMatrix.value) return
  localGroupConfig.value.push({
    group_name: newGroupForMatrix.value,
    area_connected: false,
    repo_connected: false,
    wiki_connected: false,
  })
  newGroupForMatrix.value = ''
  persistConfig()
}

function removeGroupFromMatrix(idx) {
  localGroupConfig.value.splice(idx, 1)
  persistConfig()
}

function toggleTeamDeny() {
  const existing = localRules.value.find(r => r.rule_type === 'team_deny')
  if (existing) {
    existing.enabled = !existing.enabled
  } else {
    localRules.value.push({ rule_type: 'team_deny', group_name: '', enabled: true })
  }
  persistConfig()
}

function toggleRuleEnabled(rule) {
  rule.enabled = !rule.enabled
  persistConfig()
}

function addRule(type, groupName) {
  localRules.value.push({ rule_type: type, group_name: groupName, enabled: true })
  persistConfig()
}

function removeRule(type, idx) {
  const filtered = localRules.value.filter(r => r.rule_type === type)
  const rule = filtered[idx]
  const actualIdx = localRules.value.indexOf(rule)
  if (actualIdx !== -1) localRules.value.splice(actualIdx, 1)
  persistConfig()
}

async function denylistAuditProject(org, projectId) {
  await store.addToAuditDenylist(auditKey(org, projectId))
}

async function removeAuditProjectFromDenylist(org, projectId) {
  await store.removeFromAuditDenylist(auditKey(org, projectId))
}

function toggleProject(id) {
  if (expandedProjects.has(id)) expandedProjects.delete(id)
  else expandedProjects.add(id)
}

function formatTime(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}
</script>
