<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Check Permissions</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Look up person groups and audit project permission setup across all organizations.
        </p>
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

    <!-- ======================================================= -->
    <!-- Tab 1: Person Groups                                      -->
    <!-- ======================================================= -->
    <div v-if="activeTab === 'person'">
      <!-- Loading people list -->
      <div v-if="store.loadingPeople" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-300 mb-6">
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
        Loading people from all organizations…
      </div>

      <!-- Person error -->
      <UAlert v-if="personError" color="error" icon="i-heroicons-exclamation-circle" :description="personError" class="mb-4" />

      <!-- Person dropdown -->
      <div v-if="store.peopleList" class="mb-6">
        <div class="relative max-w-lg">
          <UInput
            v-autofocus
            name="person-filter" v-model="personFilter"
            @focus="handleDropdownFocus"
            placeholder="Search for a person…"
            icon="i-heroicons-magnifying-glass"
            size="lg"
            class="w-full"
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
                <span v-if="p.unique_name" class="ml-2 text-xs text-gray-500 dark:text-gray-300">{{ p.unique_name }}</span>
              </div>
              <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 shrink-0">{{ p.organization }}</span>
            </button>
          </div>
          <div v-else-if="dropdownOpen && personFilter.length >= 1 && filteredPeople.length === 0"
            class="absolute z-20 mt-1 w-full bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg px-4 py-3 text-sm text-gray-400 dark:text-gray-300 italic">
            No matching people
          </div>
        </div>
        <p class="text-xs text-gray-500 dark:text-gray-300 mt-1">{{ displayPeopleList?.people?.length ?? 0 }} people loaded across all organizations</p>
      </div>

      <!-- Click-outside overlay to close dropdown -->
      <div v-if="dropdownOpen" class="fixed inset-0 z-10" @click="dropdownOpen = false"></div>

      <!-- Selected person's groups -->
      <div v-if="selectedPerson" class="bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
        <div class="px-5 py-4 border-b border-gray-200 dark:border-gray-700">
          <span class="font-semibold text-gray-900 dark:text-gray-100 text-lg">{{ displaySelectedPerson.display_name }}</span>
          <span v-if="displaySelectedPerson.unique_name" class="ml-2 text-sm text-gray-500 dark:text-gray-300">{{ displaySelectedPerson.unique_name }}</span>
          <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">{{ displaySelectedPerson.organization }}</span>
        </div>
        <div class="px-5 py-4">
          <!-- Loading groups -->
          <div v-if="store.personGroups[selectedPerson.descriptor]?.loading" class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-300">
            <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
            Loading groups…
          </div>
          <!-- Groups loaded -->
          <div v-else-if="store.personGroups[selectedPerson.descriptor]?.groups">
            <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-3">Permission Groups ({{ store.personGroups[selectedPerson.descriptor].groups.length }})</h4>
            <div v-if="store.personGroups[selectedPerson.descriptor].groups.length === 0" class="text-sm text-gray-400 dark:text-gray-300 italic">No group memberships found</div>
            <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              <div v-for="pg in groupedPersonGroups" :key="pg.project"
                class="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
                <div class="px-3 py-2 bg-primary-50 dark:bg-primary-900/20 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
                  <span class="text-xs font-semibold text-gray-800 dark:text-gray-200 truncate">{{ pg.project }}</span>
                  <span class="ml-2 px-1.5 py-0.5 text-xs rounded-full bg-primary-100 dark:bg-primary-900/40 text-primary-700 dark:text-primary-300 font-medium shrink-0">{{ pg.groups.length }}</span>
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
        <UButton @click="doAudit" :disabled="store.loadingPermissionAudit" :loading="store.loadingPermissionAudit"
          icon="i-heroicons-check-circle">
          {{ store.loadingPermissionAudit ? 'Running Audit…' : 'Run Audit' }}
        </UButton>
        <UButton v-if="displayAuditResults" @click="doAudit(true)"
          :disabled="store.loadingPermissionAudit"
          icon="i-heroicons-arrow-path"
          variant="outline" color="neutral">
          Force Refresh
        </UButton>
        <UButton @click="toggleScopePanel"
          :variant="showScopePanel ? 'soft' : 'outline'"
          :color="showScopePanel ? 'primary' : 'neutral'"
          icon="i-heroicons-adjustments-horizontal">
          Manage Scope
          <span v-if="store.auditDenylist.length" class="px-1.5 py-0.5 text-xs rounded-full bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300 font-medium">{{ store.auditDenylist.length }}</span>
        </UButton>
        <UButton @click="showConfigPanel = !showConfigPanel"
          :variant="showConfigPanel ? 'soft' : 'outline'"
          :color="showConfigPanel ? 'primary' : 'neutral'"
          icon="i-heroicons-cog-6-tooth">
          Configure Audit
        </UButton>
        <span v-if="displayAuditResults" class="text-xs text-gray-500 dark:text-gray-300">
          Last run: {{ formatTime(displayAuditResults.fetched_at) }}
        </span>
      </div>

      <!-- Scope Management Panel -->
      <div v-if="showScopePanel" class="mb-6 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm overflow-hidden">
        <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700 flex items-center justify-between">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200">Audit Scope</h3>
          <div v-if="store.loadingAuditProjects" class="flex items-center gap-2 text-xs text-gray-400 dark:text-gray-300">
            <UIcon name="i-heroicons-arrow-path" class="w-3.5 h-3.5 animate-spin" />
            Loading projects…
          </div>
        </div>
        <div class="grid grid-cols-2 divide-x divide-gray-200 dark:divide-gray-700">
          <!-- In Scope -->
          <div class="p-4">
            <h4 class="text-xs font-semibold uppercase text-green-600 dark:text-green-400 mb-2">In Scope ({{ scopeProjects.length }})</h4>
            <div class="space-y-1 max-h-64 overflow-y-auto">
              <div v-if="scopeProjects.length === 0" class="text-xs text-gray-400 dark:text-gray-300 italic py-2">No projects in scope</div>
              <button v-for="p in scopeProjects" :key="p.project_id"
                @click="denylistAuditProject(p._realOrg || p.organization, p.project_id)"
                class="w-full flex items-center justify-between px-3 py-2 text-sm rounded-lg hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors group text-left"
                title="Move to denylist">
                <div class="truncate">
                  <span class="text-gray-800 dark:text-gray-200">{{ p.project }}</span>
                  <span class="ml-1.5 text-xs text-gray-400 dark:text-gray-300">{{ p.organization }}</span>
                </div>
                <UIcon name="i-heroicons-arrow-right" class="w-4 h-4 text-gray-300 group-hover:text-red-500 shrink-0 ml-2 transition-colors" />
              </button>
            </div>
          </div>
          <!-- Denylisted -->
          <div class="p-4">
            <h4 class="text-xs font-semibold uppercase text-red-600 dark:text-red-400 mb-2">Denylisted ({{ denylistProjects.length }})</h4>
            <div class="space-y-1 max-h-64 overflow-y-auto">
              <div v-if="denylistProjects.length === 0" class="text-xs text-gray-400 dark:text-gray-300 italic py-2">No denylisted projects</div>
              <button v-for="p in denylistProjects" :key="p.project_id"
                @click="removeAuditProjectFromDenylist(p._realOrg || p.organization, p.project_id)"
                class="w-full flex items-center justify-between px-3 py-2 text-sm rounded-lg hover:bg-green-50 dark:hover:bg-green-900/20 transition-colors group text-left"
                title="Move back to scope">
                <UIcon name="i-heroicons-arrow-left" class="w-4 h-4 text-gray-300 group-hover:text-green-500 shrink-0 mr-2 transition-colors" />
                <div class="truncate text-right">
                  <span class="text-gray-800 dark:text-gray-200">{{ p.project }}</span>
                  <span class="ml-1.5 text-xs text-gray-400 dark:text-gray-300">{{ p.organization }}</span>
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
          <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-3">Group Connections</h4>
          <div v-if="localGroupConfig.length" class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase">
                  <th class="pb-2 pr-4">Permission Group</th>
                  <th class="pb-2 px-4 text-center w-20">Area</th>
                  <th class="pb-2 px-4 text-center w-20">Repo</th>
                  <th class="pb-2 px-4 text-center w-20">Wiki</th>
                  <th class="pb-2 pl-4 w-10"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(gc, idx) in localGroupConfig" :key="gc.group_name"
                  class="border-t border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
                  <td class="py-2 pr-4 text-gray-800 dark:text-gray-200">{{ gc.group_name }}</td>
                  <td class="py-2 px-4 text-center">
                    <UCheckbox v-model="gc.area_connected" @update:model-value="persistConfig" />
                  </td>
                  <td class="py-2 px-4 text-center">
                    <UCheckbox v-model="gc.repo_connected" @update:model-value="persistConfig" />
                  </td>
                  <td class="py-2 px-4 text-center">
                    <UCheckbox v-model="gc.wiki_connected" @update:model-value="persistConfig" />
                  </td>
                  <td class="py-2 pl-4 text-center">
                    <UButton @click="removeGroupFromMatrix(idx)" variant="ghost" color="error" size="xs" icon="i-heroicons-x-mark" title="Remove group" />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <p v-else class="text-xs text-gray-400 dark:text-gray-300 italic">No group connections configured yet.</p>
          <div v-if="availableGroupsForMatrix.length" class="mt-3 flex items-center gap-2">
            <USelectMenu v-model="newGroupForMatrix" :items="matrixGroupOptions" value-key="value" placeholder="Add group…" size="sm" class="w-64" />
            <UButton v-if="newGroupForMatrix" @click="addGroupToMatrix" size="sm">Add</UButton>
          </div>
        </div>

        <!-- Audit Rules -->
        <div class="px-5 pb-5 border-t border-gray-200 dark:border-gray-700 pt-4">
          <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-3">Audit Rules</h4>

          <!-- Team deny (single toggle) -->
          <div class="flex items-center justify-between py-2 px-3 rounded-lg bg-gray-100/60 dark:bg-gray-700/50">
            <span class="text-sm text-gray-700 dark:text-gray-300">Teams are not allowed in permission groups</span>
            <USwitch :model-value="teamDenyEnabled" @update:model-value="toggleTeamDeny" />
          </div>

          <!-- Customer deny rules -->
          <div class="mt-4">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-300 mb-2">Customer deny rules</div>
            <div v-for="(r, idx) in customerDenyRules" :key="'cd-' + idx"
              class="flex items-center gap-3 py-2 px-3 rounded-lg hover:bg-gray-100/60 dark:hover:bg-gray-700/40 transition-colors">
              <USwitch :model-value="r.enabled" @update:model-value="toggleRuleEnabled(r)" />
              <span class="text-sm text-gray-700 dark:text-gray-300 flex-1">Customer not allowed in <span class="font-semibold text-gray-900 dark:text-gray-100">{{ r.group_name }}</span></span>
              <UButton @click="removeRule('customer_deny', idx)" variant="ghost" color="error" size="xs" icon="i-heroicons-x-mark" title="Remove rule" />
            </div>
            <div v-if="!customerDenyRules.length" class="text-xs text-gray-400 dark:text-gray-300 italic px-3 py-1">No customer deny rules configured.</div>
            <div class="flex items-center gap-2 mt-2">
              <USelectMenu v-model="newCustomerDenyGroup" :items="customerDenyGroupOptions" value-key="value" placeholder="Add customer deny rule…" size="sm" class="w-64" />
              <UButton v-if="newCustomerDenyGroup" @click="addRule('customer_deny', newCustomerDenyGroup); newCustomerDenyGroup = ''" size="sm">Add</UButton>
            </div>
          </div>

          <!-- Mandatory group rules -->
          <div class="mt-4">
            <div class="text-xs font-medium text-gray-500 dark:text-gray-300 mb-2">Mandatory group rules</div>
            <div v-for="(r, idx) in mandatoryGroupRules" :key="'mg-' + idx"
              class="flex items-center gap-3 py-2 px-3 rounded-lg hover:bg-gray-100/60 dark:hover:bg-gray-700/40 transition-colors">
              <USwitch :model-value="r.enabled" @update:model-value="toggleRuleEnabled(r)" />
              <span class="text-sm text-gray-700 dark:text-gray-300 flex-1">Group <span class="font-semibold text-gray-900 dark:text-gray-100">{{ r.group_name }}</span> is mandatory for every project</span>
              <UButton @click="removeRule('mandatory_group', idx)" variant="ghost" color="error" size="xs" icon="i-heroicons-x-mark" title="Remove rule" />
            </div>
            <div v-if="!mandatoryGroupRules.length" class="text-xs text-gray-400 dark:text-gray-300 italic px-3 py-1">No mandatory group rules configured.</div>
            <div class="flex items-center gap-2 mt-2">
              <USelectMenu v-model="newMandatoryGroup" :items="mandatoryGroupOptions" value-key="value" placeholder="Add mandatory group rule…" size="sm" class="w-64" />
              <UButton v-if="newMandatoryGroup" @click="addRule('mandatory_group', newMandatoryGroup); newMandatoryGroup = ''" size="sm">Add</UButton>
            </div>
          </div>
        </div>
      </div>

      <!-- Audit error -->
      <UAlert v-if="auditError" color="error" icon="i-heroicons-exclamation-circle" :description="auditError" class="mb-4" />

      <!-- Audit summary + filter -->
      <div v-if="displayAuditResults && displayAuditResults.projects.length > 0" class="flex items-center gap-3 mb-4">
        <USelectMenu v-model="auditFilter" :items="auditFilterOptions" value-key="value" size="sm" class="w-40" />
        <span class="text-xs text-gray-500 dark:text-gray-300">
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
        <div v-if="filteredAuditProjects.length === 0" class="text-center py-12 text-gray-500 dark:text-gray-300">
          <p class="text-sm">No projects match the current filter.</p>
        </div>

        <div v-for="project in filteredAuditProjects" :key="project.project_id"
          class="bg-white dark:bg-gray-800 rounded-lg border overflow-hidden"
          :class="hasIssues(project) ? 'border-red-200 dark:border-red-800' : 'border-green-200 dark:border-green-800'">
          <button @click="toggleProject(project.project_id)"
            class="w-full flex items-center justify-between px-5 py-3.5 text-left hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
            <div class="flex items-center gap-3">
              <!-- Status icon -->
              <span v-if="hasIssues(project)" class="flex items-center justify-center w-6 h-6 rounded-full bg-red-100 dark:bg-red-900/40">
                <UIcon name="i-heroicons-x-mark" class="w-4 h-4 text-red-500" />
              </span>
              <span v-else class="flex items-center justify-center w-6 h-6 rounded-full bg-green-100 dark:bg-green-900/40">
                <UIcon name="i-heroicons-check" class="w-4 h-4 text-green-500" />
              </span>
              <div>
                <span class="font-semibold text-gray-900 dark:text-gray-100">{{ project.project }}</span>
                <span class="ml-2 px-2 py-0.5 text-xs rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300">{{ project.organization }}</span>
              </div>
            </div>
            <div class="flex items-center gap-3">
              <!-- Denylist project -->
              <UButton @click.stop="denylistAuditProject(project._realOrg || project.organization, project.project_id)"
                variant="ghost" color="error" size="xs"
                icon="i-heroicons-no-symbol"
                title="Denylist this project" />
              <!-- Refresh single project -->
              <UButton @click.stop="refreshProject(project)"
                :disabled="refreshingProjects.has(project.project_id)"
                :loading="refreshingProjects.has(project.project_id)"
                variant="ghost" color="neutral" size="xs"
                icon="i-heroicons-arrow-path"
                title="Refresh this project" />
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
              <span v-else-if="wikiSkipped(project)" class="px-2 py-0.5 text-xs rounded-full bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-300">
                wiki skipped
              </span>
              <span v-if="project.checks.areas_missing_groups?.length" class="px-2 py-0.5 text-xs rounded-full bg-teal-100 dark:bg-teal-900/40 text-teal-700 dark:text-teal-300">
                {{ project.checks.areas_missing_groups.length }} area{{ project.checks.areas_missing_groups.length !== 1 ? 's' : '' }}
              </span>
              <UIcon name="i-heroicons-chevron-down" class="w-4 h-4 transition-transform text-gray-400 dark:text-gray-300" :class="{ 'rotate-180': expandedProjects.has(project.project_id) }" />
            </div>
          </button>

          <!-- Expanded detail -->
          <div v-if="expandedProjects.has(project.project_id)" class="border-t border-gray-200 dark:border-gray-700 divide-y divide-gray-100 dark:divide-gray-700">

            <!-- Check 1: Missing Groups -->
            <div class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Mandatory Permission Groups</h4>
              <div v-if="mandatoryGroupNames.length" class="flex flex-wrap gap-2">
                <span v-for="g in mandatoryGroupNames" :key="g"
                  class="inline-flex items-center gap-1.5 px-2.5 py-1 text-xs rounded-full"
                  :class="project.checks.missing_groups.includes(g)
                    ? 'bg-red-100 dark:bg-red-900/40 text-red-700 dark:text-red-300'
                    : 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300'">
                  <UIcon v-if="!project.checks.missing_groups.includes(g)" name="i-heroicons-check" class="w-3 h-3" />
                  <UIcon v-else name="i-heroicons-x-mark" class="w-3 h-3" />
                  {{ g }}
                </span>
              </div>
              <p v-else class="text-xs text-gray-400 dark:text-gray-300 italic">No mandatory group rules configured. Set up rules in <UButton @click="showConfigPanel = true" variant="link" size="xs">Configure Audit</UButton>.</p>
            </div>

            <!-- Available Groups -->
            <div v-if="project.checks.available_groups?.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">All Available Permission Groups ({{ project.checks.available_groups.length }})</h4>
              <div class="flex flex-wrap gap-1.5">
                <span v-for="g in project.checks.available_groups" :key="g"
                  class="px-2 py-0.5 text-xs rounded-full bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300">
                  {{ g }}
                </span>
              </div>
            </div>

            <!-- Check 2: Teams in Groups -->
            <div v-if="project.checks.teams_in_groups.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Teams Found in Groups (not allowed)</h4>
              <ul class="space-y-1">
                <li v-for="t in project.checks.teams_in_groups" :key="t.group + t.team_name" class="text-sm text-gray-700 dark:text-gray-300 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-amber-400 shrink-0"></span>
                  <span class="font-medium">{{ t.group }}</span>
                  <span class="text-gray-400 dark:text-gray-300">contains team</span>
                  <span class="font-medium text-amber-600 dark:text-amber-400">{{ t.team_name }}</span>
                </li>
              </ul>
            </div>

            <!-- Check 3: Repos Missing Groups -->
            <div v-if="project.checks.repos_missing_groups.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Repositories Missing Permission Groups</h4>
              <div class="space-y-2 max-h-60 overflow-y-auto">
                <div v-for="r in project.checks.repos_missing_groups" :key="r.repo_name" class="text-sm">
                  <span class="font-medium text-gray-900 dark:text-gray-100">{{ r.repo_name }}</span>
                  <span class="text-gray-400 dark:text-gray-300 mx-1">—</span>
                  <span v-for="(mg, mi) in r.missing_groups" :key="mg" class="text-orange-600 dark:text-orange-400 text-xs">
                    {{ mg }}<span v-if="mi < r.missing_groups.length - 1">, </span>
                  </span>
                </div>
              </div>
            </div>

            <!-- Check 4: Wiki Missing Groups -->
            <div v-if="wikiIssues(project).length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Wiki Security — Missing Board Groups</h4>
              <ul class="space-y-1">
                <li v-for="w in wikiIssues(project)" :key="w" class="text-sm text-purple-600 dark:text-purple-400 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-purple-400 shrink-0"></span>
                  {{ w }}
                </li>
              </ul>
            </div>
            <div v-if="project.checks.wiki_unwanted_groups?.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Wiki Security — Unwanted Repository Groups</h4>
              <ul class="space-y-1">
                <li v-for="w in project.checks.wiki_unwanted_groups" :key="w" class="text-sm text-red-600 dark:text-red-400 flex items-center gap-2">
                  <span class="w-1.5 h-1.5 rounded-full bg-red-400 shrink-0"></span>
                  {{ w }}
                </li>
              </ul>
            </div>
            <div v-else-if="wikiSkipped(project)" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Wiki Security</h4>
              <p class="text-sm text-gray-500 dark:text-gray-300 italic">Skipped — PAT requires 'vso.wiki' scope</p>
            </div>

            <!-- Check 5: Areas Missing Board Groups -->
            <div v-if="project.checks.areas_missing_groups?.length" class="px-5 py-3">
              <h4 class="text-xs font-semibold uppercase text-gray-500 dark:text-gray-300 mb-2">Area Paths Missing Board Groups</h4>
              <div class="space-y-2 max-h-60 overflow-y-auto">
                <div v-for="a in project.checks.areas_missing_groups" :key="a.area_path" class="text-sm">
                  <span class="font-medium text-gray-900 dark:text-gray-100">{{ a.area_path }}</span>
                  <span class="text-gray-400 dark:text-gray-300 mx-1">—</span>
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

const store = useMonitorStore()
const { isDemoMode } = useDemoMode()

const activeTab = ref('person')
const tabItems = [
  { label: 'Person Groups', value: 'person', icon: 'i-heroicons-user-group' },
  { label: 'Permission Audit', value: 'audit', icon: 'i-heroicons-shield-check' },
]
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
