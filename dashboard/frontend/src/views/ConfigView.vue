<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-gray-900 dark:text-gray-100">Manage Projects</h2>
        <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">Configure which Azure DevOps projects and checks to monitor.</p>
      </div>

      <button
        @click="showForm = !showForm"
        class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 transition-colors shadow-sm"
      >
        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
        </svg>
        {{ showForm ? 'Cancel' : 'Add Project' }}
      </button>
    </div>

    <!-- Add/Edit form -->
    <div v-if="showForm" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-6">
        <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700">
          <h3 class="font-semibold text-gray-900 dark:text-gray-100">{{ editing ? 'Edit Project' : 'Add New Project' }}</h3>
        </div>

        <div class="px-6 py-5 space-y-4">
          <!-- Organization & Project -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Organization</label>
              <div v-if="!store.patConfigured" class="text-xs text-amber-600 dark:text-amber-400 mb-1">Set a PAT in Settings first.</div>
              <div class="relative">
                <input
                  v-autofocus
                  v-model="form.organization"
                  :list="store.organizations.length > 1 ? 'known-orgs' : undefined"
                  type="text"
                  placeholder="e.g. MyOrganization"
                  :disabled="!store.patConfigured || store.organizations.length === 1"
                  @change="onOrgChange"
                  @blur="onOrgChange"
                  class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none disabled:bg-gray-50 dark:disabled:bg-gray-800 disabled:text-gray-400"
                />
                <datalist v-if="store.organizations.length > 1" id="known-orgs">
                  <option v-for="o in store.organizations" :key="o.name" :value="o.name" />
                </datalist>
              </div>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
              <div v-if="store.orgProjectsError" class="text-xs text-red-600 dark:text-red-400 mb-1">{{ store.orgProjectsError }}</div>
              <SelectMenu
                v-model="form.project"
                :options="projectOptions"
                :placeholder="projectPlaceholder"
                :loading="store.loadingOrgProjects"
                :disabled="!form.organization || store.loadingOrgProjects || store.orgProjects.length === 0"
                @change="onProjectChange"
                class="w-full"
              />
            </div>
          </div>

          <!-- Area path -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Area Path <span class="text-gray-500 font-normal">(optional)</span></label>
              <SelectMenu
                v-model="form.area_path"
                :options="areaPathOptions"
                :loading="store.loadingAreaPaths"
                :disabled="!form.project || store.loadingAreaPaths"
                placeholder="Select area path…"
                class="w-full"
              />
              <label v-if="form.area_path" class="inline-flex items-center gap-2 mt-2 cursor-pointer">
                <input type="checkbox" v-model="form.include_child_areas" class="rounded border-gray-300 text-primary-600 focus:ring-primary-500" />
                <span class="text-sm text-gray-700 dark:text-gray-300">Include child areas</span>
              </label>
            </div>
          </div>

          <!-- Checks -->
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Enabled Checks</label>
            <div class="space-y-2">
              <div
                v-for="ct in store.checkTypes"
                :key="ct.type_key"
                class="border border-gray-200 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
              >
                <label class="flex items-start gap-3 p-3 cursor-pointer">
                  <input
                    type="checkbox"
                    :value="ct.type_key"
                    v-model="selectedChecks"
                    class="mt-0.5 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
                  />
                  <div class="flex-1">
                    <div class="flex items-center justify-between">
                      <span class="text-sm font-medium text-gray-800 dark:text-gray-200">{{ ct.label }}</span>
                      <button
                        v-if="selectedChecks.includes(ct.type_key) && hasCheckOptions(ct.type_key)"
                        type="button"
                        @click.prevent.stop="toggleCheckOptions(ct.type_key)"
                        class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium text-gray-500 dark:text-gray-400 hover:text-primary-600 dark:hover:text-primary-400 hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors"
                      >
                        <svg class="w-3.5 h-3.5 transition-transform" :class="expandedChecks[ct.type_key] ? 'rotate-90' : ''" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
                        </svg>
                        Options
                      </button>
                    </div>
                    <p class="text-xs text-gray-600 dark:text-gray-400 mt-0.5">{{ ct.description }}</p>
                    <!-- Collapsible per-check options -->
                    <div v-if="selectedChecks.includes(ct.type_key) && expandedChecks[ct.type_key]" class="mt-3 pl-1 border-l-2 border-primary-200 dark:border-primary-800 space-y-3" @click.stop>
                      <!-- Repository filter -->
                      <div v-if="['release_pr_check', 'pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check'].includes(ct.type_key)">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Repository name <span class="text-gray-500">(leave empty to check all repos)</span></label>
                        <SelectMenu
                          :modelValue="checkRepositories[ct.type_key] || ''"
                          @update:modelValue="checkRepositories[ct.type_key] = $event"
                          :options="repoFilterOptions"
                          :loading="store.loadingRepos"
                          :disabled="!form.project || store.loadingRepos"
                          placeholder="All repositories"
                          size="sm"
                          class="w-full mt-1"
                        />
                      </div>
                      <!-- Stale days -->
                      <div v-if="ct.type_key === 'stale_pr_check'">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Inactive days threshold</label>
                        <input
                          :value="checkStaleDays[ct.type_key] || 14"
                          @input="checkStaleDays[ct.type_key] = parseInt($event.target.value) || 14"
                          type="number"
                          min="1"
                          class="w-20 mt-1 px-2 py-1 border border-gray-300 dark:border-gray-600 rounded text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-1 focus:ring-primary-500 focus:border-primary-500 outline-none"
                          placeholder="14"
                        />
                      </div>
                      <!-- Ignore reviewers -->
                      <div v-if="ct.type_key === 'unreviewed_pr_check'">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Ignore reviewers <span class="text-gray-500">(comma-separated, substring match)</span></label>
                        <input
                          :value="checkIgnoreReviewers[ct.type_key] || ''"
                          @input="checkIgnoreReviewers[ct.type_key] = $event.target.value"
                          type="text"
                          class="w-full mt-1 px-2 py-1 border border-gray-300 dark:border-gray-600 rounded text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-1 focus:ring-primary-500 focus:border-primary-500 outline-none"
                          placeholder="e.g. Build Service, Project Collection"
                        />
                      </div>
                      <!-- Estimate mode -->
                      <div v-if="ct.type_key === 'missing_estimate_check'">
                        <label class="text-xs text-gray-600 dark:text-gray-400">Check for missing</label>
                        <div class="flex flex-col gap-1.5 mt-1.5">
                          <label class="inline-flex items-center gap-1.5 cursor-pointer">
                            <input type="radio" :name="'estimate_mode_' + ct.type_key" value="both" :checked="(checkEstimateMode[ct.type_key] || 'both') === 'both'" @change="checkEstimateMode[ct.type_key] = 'both'" class="text-primary-600 focus:ring-primary-500" />
                            <span class="text-xs text-gray-700 dark:text-gray-300">Both Original Estimate &amp; Remaining Work</span>
                          </label>
                          <label class="inline-flex items-center gap-1.5 cursor-pointer">
                            <input type="radio" :name="'estimate_mode_' + ct.type_key" value="original_estimate" :checked="(checkEstimateMode[ct.type_key] || 'both') === 'original_estimate'" @change="checkEstimateMode[ct.type_key] = 'original_estimate'" class="text-primary-600 focus:ring-primary-500" />
                            <span class="text-xs text-gray-700 dark:text-gray-300">Original Estimate only</span>
                          </label>
                          <label class="inline-flex items-center gap-1.5 cursor-pointer">
                            <input type="radio" :name="'estimate_mode_' + ct.type_key" value="remaining_work" :checked="(checkEstimateMode[ct.type_key] || 'both') === 'remaining_work'" @change="checkEstimateMode[ct.type_key] = 'remaining_work'" class="text-primary-600 focus:ring-primary-500" />
                            <span class="text-xs text-gray-700 dark:text-gray-300">Remaining Work only</span>
                          </label>
                        </div>
                      </div>
                      <!-- Parent type mappings -->
                      <div v-if="ct.type_key === 'orphan_check'">
                        <div class="flex items-center gap-2">
                          <label class="text-xs text-gray-600 dark:text-gray-400">Parent type mappings</label>
                          <button
                            v-if="editing && form.project && !loadingParentHierarchy"
                            type="button"
                            @click.stop="loadParentHierarchy"
                            class="text-xs text-primary-600 dark:text-primary-400 hover:underline"
                          >Load from process</button>
                          <svg v-if="loadingParentHierarchy" class="w-3 h-3 animate-spin text-gray-400" fill="none" viewBox="0 0 24 24">
                            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                          </svg>
                        </div>
                        <p class="text-xs text-gray-500 dark:text-gray-500 mt-0.5 mb-2">Maps child type → allowed parent types. Leave empty to use process defaults at runtime.</p>
                        <div class="space-y-2">
                          <div v-for="(parents, childType) in checkParentMappings" :key="childType" class="flex items-center gap-2">
                            <span class="text-xs font-medium text-gray-700 dark:text-gray-300 w-28 shrink-0">{{ childType }}</span>
                            <input
                              :value="parents.join(', ')"
                              @change="updateParentMapping(childType, $event.target.value)"
                              type="text"
                              class="flex-1 px-2 py-1 border border-gray-300 dark:border-gray-600 rounded text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-1 focus:ring-primary-500 focus:border-primary-500 outline-none"
                              placeholder="e.g. Feature, Epic"
                            />
                            <button type="button" @click.stop="removeParentMapping(childType)" class="text-gray-400 hover:text-red-500 transition-colors">
                              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                              </svg>
                            </button>
                          </div>
                        </div>
                        <div class="flex items-center gap-2 mt-2">
                          <input
                            v-model="newMappingChildType"
                            type="text"
                            class="w-28 px-2 py-1 border border-gray-300 dark:border-gray-600 rounded text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-1 focus:ring-primary-500 focus:border-primary-500 outline-none"
                            placeholder="Child type"
                            @keydown.enter.prevent="addParentMapping"
                          />
                          <input
                            v-model="newMappingParentTypes"
                            type="text"
                            class="flex-1 px-2 py-1 border border-gray-300 dark:border-gray-600 rounded text-xs bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-1 focus:ring-primary-500 focus:border-primary-500 outline-none"
                            placeholder="Parent types (comma-separated)"
                            @keydown.enter.prevent="addParentMapping"
                          />
                          <button type="button" @click.stop="addParentMapping" :disabled="!newMappingChildType.trim()" class="px-2 py-1 text-xs font-medium text-primary-600 dark:text-primary-400 border border-primary-300 dark:border-primary-600 rounded hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors disabled:opacity-40">
                            Add
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </label>
              </div>
            </div>
          </div>

          <!-- Ignore patterns -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Ignore Title Contains <span class="text-gray-500 font-normal">(comma-separated)</span></label>
              <input v-model="ignoreTitles" type="text" placeholder="In doc verwerken, FO change"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Ignore Parent Title Contains <span class="text-gray-500 font-normal">(comma-separated)</span></label>
              <input v-model="ignoreParentTitles" type="text" placeholder="Bugs to be discussed"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none" />
            </div>
          </div>
        </div>

        <!-- Form footer -->
        <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
          <button @click="resetForm" class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-600 transition-colors">
            Cancel
          </button>
          <button @click="saveProject" :disabled="!form.organization || !form.project" class="px-4 py-2 text-sm font-medium text-white bg-primary-600 rounded-lg hover:bg-primary-700 disabled:opacity-50 transition-colors">
            {{ editing ? 'Update' : 'Add Project' }}
          </button>
        </div>
      </div>

    <!-- Project list -->
    <div class="space-y-3">
      <EmptyState v-if="store.displayProjects.length === 0" icon="folder" message="No projects configured. Click &quot;Add Project&quot; above to get started." />

      <div
        v-for="p in store.displayProjects"
        :key="p.id"
        class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-5 py-4 flex items-center justify-between hover:shadow-md transition-shadow"
      >
        <div>
          <div class="flex items-center gap-2">
            <h3 class="font-semibold text-gray-900 dark:text-gray-100">{{ p.project }}</h3>
            <span class="text-xs text-gray-600">{{ p.organization }}</span>
          </div>
          <div class="flex flex-wrap gap-1.5 mt-2">
            <span
              v-for="c in p.checks.filter(ch => ch.enabled)"
              :key="c.check_type"
              class="inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-primary-50 dark:bg-primary-900/30 text-primary-700 dark:text-primary-400"
            >
              {{ checkLabel(c.check_type) }}
            </span>
            <span v-if="p.area_path" class="inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-gray-100 text-gray-600">
              {{ p.area_path }}
            </span>
          </div>
        </div>

        <div class="flex items-center gap-2 shrink-0">
          <button @click="editProject(p)" class="p-2 text-gray-500 hover:text-primary-700 hover:bg-gray-100 rounded-lg transition-colors" title="Edit">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
            </svg>
          </button>
          <template v-if="confirmingDeleteId === p.id">
            <span class="text-xs text-red-600 dark:text-red-400">Remove?</span>
            <button @click="removeProject(p.id)" class="px-2 py-1 text-xs font-medium text-white bg-red-600 hover:bg-red-700 rounded-md transition-colors">Yes</button>
            <button @click="confirmingDeleteId = null" class="px-2 py-1 text-xs font-medium text-gray-600 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-md transition-colors">Cancel</button>
          </template>
          <button v-else @click="removeProject(p.id)" class="p-2 text-gray-500 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors" title="Delete">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
            </svg>
          </button>
        </div>
      </div>
    </div>

    <!-- DB Projects Section -->
    <div class="mt-10">
      <div class="flex items-center justify-between mb-6">
        <div>
          <h2 class="text-2xl font-bold text-gray-900 dark:text-gray-100">Database Projects</h2>
          <p class="text-sm text-gray-600 dark:text-gray-400 mt-1">Track database project names for monitoring.</p>
        </div>

        <button
          @click="showDbForm = !showDbForm"
          class="inline-flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 transition-colors shadow-sm"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          {{ showDbForm ? 'Cancel' : 'Add DB Project' }}
        </button>
      </div>

      <!-- Add/Edit DB project form -->
      <div v-if="showDbForm" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mb-6">
        <div class="px-6 py-4 border-b border-gray-100 dark:border-gray-700">
          <h3 class="font-semibold text-gray-900 dark:text-gray-100">{{ editingDb ? 'Edit DB Project' : 'Add New DB Project' }}</h3>
        </div>
        <div class="px-6 py-5">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project Name</label>
              <input
                v-model="dbForm.name"
                type="text"
                placeholder="e.g. Customer Portal"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Database Name Filter</label>
              <input
                v-model="dbForm.name_filter"
                type="text"
                placeholder="e.g. CustomerPortal"
                class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              />
              <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">Filters databases whose name contains this text (case-insensitive). Leave empty to show all.</p>
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Database Server</label>
              <SelectMenu
                v-model="dbForm.db_server_index"
                :options="dbServerOptions"
                placeholder="Select server…"
                class="w-full"
              />
            </div>
          </div>
          <div class="mt-4">
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Allowlist <span class="text-gray-500 font-normal">(always green)</span></label>
            <textarea
              v-model="dbForm.db_allowlist"
              rows="2"
              placeholder="One database name per line"
              class="w-full px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg text-sm bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none font-mono"
            />
            <p class="mt-1 text-xs text-gray-600 dark:text-gray-400">Databases listed here will always be marked as OK, regardless of ticket status.</p>
          </div>
        </div>
        <div class="px-6 py-4 bg-gray-50 dark:bg-gray-800/50 border-t border-gray-100 dark:border-gray-700 flex justify-end gap-3">
          <button @click="resetDbForm" class="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 bg-white dark:bg-gray-700 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-600 transition-colors">
            Cancel
          </button>
          <button @click="saveDbProject" :disabled="!dbForm.name.trim()" class="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 disabled:opacity-50 transition-colors">
            {{ editingDb ? 'Update' : 'Add' }}
          </button>
        </div>
      </div>

      <!-- DB project list -->
      <div class="space-y-3">
        <EmptyState v-if="store.displayDbProjects.length === 0 && !showDbForm" icon="database" message="No database projects yet. Click &quot;Add DB Project&quot; above to get started." />

        <div
          v-for="db in store.displayDbProjects"
          :key="db.id"
          class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-5 py-4 flex items-center justify-between hover:shadow-md transition-shadow"
        >
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center shrink-0">
              <svg class="w-4 h-4 text-indigo-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
                <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375" />
              </svg>
            </div>
            <div>
              <span class="font-semibold text-gray-800 dark:text-gray-100">{{ db.name }}</span>
              <span v-if="db.name_filter" class="ml-2 inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-indigo-50 dark:bg-indigo-900/30 text-indigo-700 dark:text-indigo-400">
                filter: {{ db.name_filter }}
              </span>
              <span class="ml-2 inline-block px-2 py-0.5 rounded-md text-xs font-medium bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400">
                Server #{{ (db.db_server_index ?? 0) + 1 }}{{ store.dbServers[db.db_server_index ?? 0]?.server ? ` — ${store.dbServers[db.db_server_index ?? 0].server}` : '' }}
              </span>
            </div>
          </div>

          <div class="flex items-center gap-2 shrink-0">
            <button @click="editDbProject(db)" class="p-2 text-gray-500 hover:text-indigo-600 hover:bg-gray-100 rounded-lg transition-colors" title="Edit">
              <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
              </svg>
            </button>
            <template v-if="confirmingDbDeleteId === db.id">
              <span class="text-xs text-red-600 dark:text-red-400">Remove?</span>
              <button @click="removeDbProject(db.id)" class="px-2 py-1 text-xs font-medium text-white bg-red-600 hover:bg-red-700 rounded-md transition-colors">Yes</button>
              <button @click="confirmingDbDeleteId = null" class="px-2 py-1 text-xs font-medium text-gray-600 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-md transition-colors">Cancel</button>
            </template>
            <button v-else @click="removeDbProject(db.id)" class="p-2 text-gray-500 hover:text-red-600 hover:bg-red-50 rounded-lg transition-colors" title="Delete">
              <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import EmptyState from '../components/EmptyState.vue'
import SelectMenu from '../components/SelectMenu.vue'

const store = useMonitorStore()

const showForm = ref(false)
const editing = ref(null)  // project id or null

// DB project form state
const showDbForm = ref(false)
const editingDb = ref(null)
const dbForm = reactive({ name: '', name_filter: '', db_server_index: 0, db_allowlist: '' })
const confirmingDeleteId = ref(null)   // project id pending delete confirmation
const confirmingDbDeleteId = ref(null) // db-project id pending delete confirmation

const form = reactive({
  organization: '',
  project: '',
  area_path: '',
  include_child_areas: true,
})
const selectedChecks = ref([])
const checkApiVersions = reactive({})  // { check_type: "7.1" }
const checkRepositories = reactive({})  // { check_type: "repo-name" }
const checkStaleDays = reactive({})  // { check_type: 14 }
const checkIgnoreReviewers = reactive({})  // { check_type: "name1, name2" }
const checkEstimateMode = reactive({})  // { check_type: "both" | "original_estimate" | "remaining_work" }
const expandedChecks = reactive({})  // { check_type: true/false }
const ignoreTitles = ref('')
const ignoreParentTitles = ref('')

const projectOptions = computed(() =>
  store.orgProjects.map(p => ({ value: p.name, label: p.name }))
)
const projectPlaceholder = computed(() =>
  store.loadingOrgProjects ? 'Loading projects…'
    : store.orgProjectsError ? 'Failed — check org name & PAT'
    : form.organization ? 'Select project…'
    : 'Enter organization first'
)
const areaPathOptions = computed(() => [
  { value: '', label: 'All areas (no filter)' },
  ...store.areaPaths.map(a => ({ value: a.path, label: a.path }))
])
const repoFilterOptions = computed(() => [
  { value: '', label: 'All repositories' },
  ...store.repos.map(r => ({ value: r.name, label: r.name }))
])
const dbServerOptions = computed(() =>
  [0, 1, 2].map(i => ({
    value: i,
    label: `Server #${i + 1}${store.dbServers[i]?.configured ? ` — ${store.dbServers[i].server}` : ' (not configured)'}`
  }))
)

const _CHECKS_WITH_OPTIONS = new Set([
  'release_pr_check', 'pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check', 'missing_estimate_check', 'orphan_check'
])
function hasCheckOptions(typeKey) {
  return _CHECKS_WITH_OPTIONS.has(typeKey)
}

const checkParentMappings = reactive({})
const loadingParentHierarchy = ref(false)
const newMappingChildType = ref('')
const newMappingParentTypes = ref('')

async function loadParentHierarchy() {
  if (!editing.value) return
  loadingParentHierarchy.value = true
  try {
    const res = await store.fetchParentTypeHierarchy(editing.value)
    Object.keys(checkParentMappings).forEach(k => delete checkParentMappings[k])
    if (res.hierarchy) {
      for (const [child, parents] of Object.entries(res.hierarchy)) {
        checkParentMappings[child] = [...parents]
      }
    }
  } catch (e) {
    store._toast('Failed to load hierarchy: ' + (e.message || 'Unknown error'), 'error')
  } finally {
    loadingParentHierarchy.value = false
  }
}

function updateParentMapping(childType, value) {
  checkParentMappings[childType] = value.split(',').map(s => s.trim()).filter(Boolean)
}

function removeParentMapping(childType) {
  delete checkParentMappings[childType]
}

function addParentMapping() {
  const child = newMappingChildType.value.trim()
  if (!child) return
  checkParentMappings[child] = newMappingParentTypes.value.split(',').map(s => s.trim()).filter(Boolean)
  newMappingChildType.value = ''
  newMappingParentTypes.value = ''
}
function toggleCheckOptions(typeKey) {
  expandedChecks[typeKey] = !expandedChecks[typeKey]
}

// Load known org names for autocomplete
watch(showForm, (open) => {
  if (open) store.fetchKnownOrganizations()
})

// Auto-fill organization when only one is known
watch(() => store.organizations, (orgs) => {
  if (orgs.length === 1 && !form.organization && !editing.value) {
    form.organization = orgs[0].name
    onOrgChange()
  }
})

// Auto-select project when only one available
watch(() => store.orgProjects, (projs) => {
  if (projs.length === 1 && !form.project) {
    form.project = projs[0].name
    onProjectChange()
  }
})

// Auto-select area path when only one available
watch(() => store.areaPaths, (paths) => {
  if (paths.length === 1 && !form.area_path) {
    form.area_path = paths[0].path
  }
})

// Auto-select repo for PR checks when only one available
watch(() => store.repos, (repos) => {
  if (repos.length === 1) {
    for (const ct of ['release_pr_check', 'pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check']) {
      if (selectedChecks.value.includes(ct) && !checkRepositories[ct]) {
        checkRepositories[ct] = repos[0].name
      }
    }
  }
})

// Auto-load parent type hierarchy when orphan_check is toggled on and no mappings exist
watch(selectedChecks, (checks, oldChecks) => {
  if (checks.includes('orphan_check') && !(oldChecks || []).includes('orphan_check') &&
      Object.keys(checkParentMappings).length === 0 && editing.value) {
    loadParentHierarchy()
  }
})

onMounted(() => {
  store.fetchKnownOrganizations()
  store.fetchDbProjects()
  store.fetchDbCredentialsStatus()
})

let orgDebounce = null
async function onOrgChange() {
  form.project = ''
  form.area_path = ''
  store.orgProjects = []
  store.orgProjectsError = null
  store.areaPaths = []
  store.repos = []
  clearTimeout(orgDebounce)
  if (!form.organization.trim()) return
  orgDebounce = setTimeout(async () => {
    await store.fetchOrgProjects(form.organization.trim())
  }, 300)
}

function onProjectChange() {
  form.area_path = ''
  store.areaPaths = []
  store.repos = []
  if (form.organization && form.project) {
    store.fetchAreaPaths(form.organization.trim(), form.project)
    store.fetchRepos(form.organization.trim(), form.project)
  }
}

function checkLabel(checkType) {
  const ct = store.checkTypes.find(c => c.type_key === checkType)
  return ct ? ct.label : checkType
}

function resetForm() {
  form.organization = ''
  form.project = ''
  form.area_path = ''
  form.include_child_areas = true
  selectedChecks.value = []
  Object.keys(checkApiVersions).forEach(k => delete checkApiVersions[k])
  Object.keys(checkRepositories).forEach(k => delete checkRepositories[k])
  Object.keys(checkStaleDays).forEach(k => delete checkStaleDays[k])
  Object.keys(checkIgnoreReviewers).forEach(k => delete checkIgnoreReviewers[k])
  Object.keys(checkEstimateMode).forEach(k => delete checkEstimateMode[k])
  Object.keys(checkParentMappings).forEach(k => delete checkParentMappings[k])
  ignoreTitles.value = ''
  ignoreParentTitles.value = ''
  editing.value = null
  showForm.value = false
}

function editProject(p) {
  editing.value = p.id
  form.organization = p.organization
  form.project = p.project
  form.area_path = p.area_path
  form.include_child_areas = p.include_child_areas ?? true
  selectedChecks.value = p.checks.filter(c => c.enabled).map(c => c.check_type)
  // Populate per-check api versions
  Object.keys(checkApiVersions).forEach(k => delete checkApiVersions[k])
  Object.keys(checkRepositories).forEach(k => delete checkRepositories[k])
  Object.keys(checkStaleDays).forEach(k => delete checkStaleDays[k])
  Object.keys(checkIgnoreReviewers).forEach(k => delete checkIgnoreReviewers[k])
  Object.keys(checkEstimateMode).forEach(k => delete checkEstimateMode[k])
  Object.keys(checkParentMappings).forEach(k => delete checkParentMappings[k])
  for (const c of p.checks) {
    if (c.enabled) {
      checkApiVersions[c.check_type] = c.api_version || '7.1'
      if (c.repository) checkRepositories[c.check_type] = c.repository
      if (c.stale_days) checkStaleDays[c.check_type] = c.stale_days
      if (c.ignore_reviewers && c.ignore_reviewers.length) checkIgnoreReviewers[c.check_type] = c.ignore_reviewers.join(', ')
      if (c.estimate_mode) checkEstimateMode[c.check_type] = c.estimate_mode
      if (c.parent_type_mappings && Object.keys(c.parent_type_mappings).length > 0) {
        for (const [child, parents] of Object.entries(c.parent_type_mappings)) {
          checkParentMappings[child] = [...parents]
        }
      }
    }
  }
  ignoreTitles.value = (p.ignore_title_contains || []).join(', ')
  ignoreParentTitles.value = (p.ignore_parent_title_contains || []).join(', ')
  showForm.value = true
  // Auto-load parent hierarchy if orphan_check is enabled but no mappings configured
  if (selectedChecks.value.includes('orphan_check') && Object.keys(checkParentMappings).length === 0) {
    loadParentHierarchy()
  }
  // Load projects and areas for the existing org/project
  if (p.organization) {
    const savedProject = p.project
    const savedArea = p.area_path
    store.fetchOrgProjects(p.organization).then(() => {
      form.project = savedProject
      if (savedProject) {
        store.fetchAreaPaths(p.organization, savedProject).then(() => {
          form.area_path = savedArea
        })
        store.fetchRepos(p.organization, savedProject)
      }
    })
  }
}

function buildPayload() {
  return {
    organization: form.organization,
    project: form.project,
    area_path: form.area_path,
    include_child_areas: form.include_child_areas,
    ignore_title_contains: ignoreTitles.value ? ignoreTitles.value.split(',').map(s => s.trim()).filter(Boolean) : [],
    ignore_parent_title_contains: ignoreParentTitles.value ? ignoreParentTitles.value.split(',').map(s => s.trim()).filter(Boolean) : [],
    checks: selectedChecks.value.map(ct => ({
      check_type: ct,
      enabled: true,
      api_version: checkApiVersions[ct] || '7.1',
      exclude_types: [],
      custom_wiql: '',
      repository: checkRepositories[ct] || '',
      stale_days: checkStaleDays[ct] || 14,
      ignore_reviewers: checkIgnoreReviewers[ct] ? checkIgnoreReviewers[ct].split(',').map(s => s.trim()).filter(Boolean) : [],
      estimate_mode: checkEstimateMode[ct] || 'both',
      parent_type_mappings: ct === 'orphan_check' && Object.keys(checkParentMappings).length > 0 ? { ...checkParentMappings } : {},
    })),
  }
}

async function saveProject() {
  try {
    const payload = buildPayload()
    if (editing.value) {
      await store.updateProject(editing.value, payload)
    } else {
      await store.addProject(payload)
    }
    resetForm()
  } catch (e) {
    store._toast('Failed to save project: ' + e.message, 'error')
  }
}

function removeProject(id) {
  if (confirmingDeleteId.value === id) {
    // second click = confirmed
    confirmingDeleteId.value = null
    store.deleteProject(id).catch(e => store._toast('Failed to delete project: ' + e.message, 'error'))
  } else {
    confirmingDeleteId.value = id
    // auto-cancel after 3 seconds
    setTimeout(() => { if (confirmingDeleteId.value === id) confirmingDeleteId.value = null }, 3000)
  }
}

// DB project helpers
function resetDbForm() {
  dbForm.name = ''
  dbForm.name_filter = ''
  dbForm.db_server_index = 0
  dbForm.db_allowlist = ''
  editingDb.value = null
  showDbForm.value = false
}

function editDbProject(db) {
  editingDb.value = db.id
  dbForm.name = db.name
  dbForm.name_filter = db.name_filter || ''
  dbForm.db_server_index = db.db_server_index ?? 0
  dbForm.db_allowlist = (db.db_allowlist || []).join('\n')
  showDbForm.value = true
}

async function saveDbProject() {
  try {
    const payload = {
      name: dbForm.name.trim(),
      name_filter: dbForm.name_filter.trim(),
      db_server_index: dbForm.db_server_index,
      db_allowlist: dbForm.db_allowlist.split('\n').map(s => s.trim()).filter(Boolean),
    }
    if (editingDb.value) {
      await store.updateDbProject(editingDb.value, payload)
    } else {
      await store.addDbProject(payload)
    }
    resetDbForm()
  } catch (e) {
    store._toast('Failed to save DB project: ' + e.message, 'error')
  }
}

function removeDbProject(id) {
  if (confirmingDbDeleteId.value === id) {
    confirmingDbDeleteId.value = null
    store.deleteDbProject(id).catch(e => store._toast('Failed to delete DB project: ' + e.message, 'error'))
  } else {
    confirmingDbDeleteId.value = id
    setTimeout(() => { if (confirmingDbDeleteId.value === id) confirmingDbDeleteId.value = null }, 3000)
  }
}
</script>
