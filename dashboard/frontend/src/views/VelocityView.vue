<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Velocity</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">Sprint capacity, velocity calculation, and metrics. <DataFreshness :timestamp="store.lastFetched.velocity" /></p>
      </div>
    </div>

    <!-- PAT Warning -->
    <UAlert v-if="!store.patConfigured" color="warning" icon="i-heroicons-exclamation-triangle" description="PAT not configured. Set it in Settings first." class="mb-6" />

    <!-- Tab bar -->
    <UTabs :items="tabs" v-model="activeTab" :content="false" variant="link" class="mb-6" />

    <!-- =============== CAPACITY TAB =============== -->
    <div v-if="activeTab === 'capacity'">
      <!-- Project + Team + Sprint selectors -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
          <!-- Project -->
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Project</label>
            <USelectMenu autofocus v-model="capProjectId" :items="projectOptions" value-key="value"
              placeholder="Choose a project…" class="w-full" />
          </div>
          <!-- Team -->
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Team</label>
            <USelectMenu v-model="capTeam" :items="capTeamOptions" value-key="value" placeholder="Choose a team…"
              :loading="capLoadingTeams" :disabled="!capProjectId || capLoadingTeams" class="w-full" />
          </div>
          <!-- Sprint -->
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Sprint</label>
            <USelectMenu v-model="capIterationId" :items="capIterationOptions" value-key="value" placeholder="Choose a sprint…"
              :loading="capLoadingIterations" :disabled="!capTeam || capLoadingIterations" class="w-full" />
          </div>
        </div>
      </div>

      <!-- Capacity table -->
      <div v-if="capData" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <div class="flex items-center justify-between mb-4">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300">
            Team Capacity — {{ isDemoMode ? anonIterationPath(capData.iteration_name) : capData.iteration_name }}
          </h3>
          <div class="flex items-center gap-2">
            <span class="text-xs text-gray-500 dark:text-gray-300">
              Total: <strong>{{ capTotalAll }}</strong>
              <template v-if="capTotalDev > 0"> · Dev: <strong>{{ capTotalDev }}</strong></template>
              <template v-if="capTotalTest > 0"> · Test: <strong>{{ capTotalTest }}</strong></template>
              <template v-if="capTotalUnassigned > 0"> · Unassigned: <strong>{{ capTotalUnassigned }}</strong></template>
              <template v-if="capTotalOther > 0"> · Other: <strong>{{ capTotalOther }}</strong></template>
            </span>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
            <thead>
              <tr class="border-b border-gray-200 dark:border-gray-700 text-left text-xs text-gray-500 dark:text-gray-300 uppercase tracking-wider">
                <th class="py-2 pr-4">Team Member</th>
                <th class="py-2 pr-2 text-right">Activity</th>
                <th class="py-2 pr-4">Capacity / Day</th>
                <th class="py-2">Days Off</th>
                <th class="py-2"></th>
              </tr>
            </thead>
            <tbody>
              <template v-for="(member, mi) in capMembers" :key="member.member_id">
                <tr v-for="(act, ai) in member.activities" :key="member.member_id + '-' + ai"
                  class="border-b border-gray-100 dark:border-gray-700/50 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
                  <td v-if="ai === 0" :rowspan="Math.max(member.activities.length, 1)" class="py-2 pr-4 font-medium text-gray-800 dark:text-gray-200 align-top">
                    {{ isDemoMode ? anonName(member.display_name) : cleanName(member.display_name) }}
                  </td>
                  <td class="py-2 pr-2 text-right">
                    <USelectMenu v-model="act.name" :items="activityOptions" value-key="value" size="sm" placeholder="Unassigned" />
                  </td>
                  <td class="py-2 pr-4">
                    <UInput type="number" v-model.number="act.capacity_per_day" :min="0" :max="24" :step="0.5" size="sm" class="w-24" />
                  </td>
                  <td v-if="ai === 0" :rowspan="Math.max(member.activities.length, 1)" class="py-2 align-top">
                    <UButton @click="toggleDaysOff(mi)" size="xs"
                      :variant="capExpandedDaysOff === mi ? 'soft' : 'ghost'"
                      :color="capExpandedDaysOff === mi ? 'primary' : 'neutral'"
                      icon="i-heroicons-calendar">
                      {{ countDaysOff(member) }} day{{ countDaysOff(member) !== 1 ? 's' : '' }}
                    </UButton>
                  </td>
                  <td v-if="ai === 0" :rowspan="Math.max(member.activities.length, 1)" class="py-2 align-top">
                    <div class="flex items-center gap-1">
                      <UButton v-if="member.activities.length < 2" @click="addActivity(mi)" variant="ghost" color="primary" size="xs" title="Add activity">+</UButton>
                      <UButton @click="removeMember(mi)" variant="ghost" color="error" size="xs" title="Remove member">✕</UButton>
                    </div>
                  </td>
                </tr>
                <!-- Days off panel (expanded) -->
                <tr v-if="capExpandedDaysOff === mi" class="bg-gray-50 dark:bg-gray-700/30">
                  <td colspan="5" class="px-4 py-3">
                    <div class="space-y-3">
                      <!-- Standard days off (weekday toggles) -->
                      <div>
                        <label class="text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1.5 block">Standard days off (weekly)</label>
                        <div class="flex gap-1">
                          <button v-for="(day, di) in weekdayLabels.slice(0, 5)" :key="di"
                            @click="toggleStandardDayOff(mi, di)"
                            class="w-9 h-8 text-xs font-medium rounded-md border transition-colors"
                            :class="isStandardDayOff(mi, di)
                              ? 'bg-red-100 dark:bg-red-900/40 border-red-300 dark:border-red-700 text-red-700 dark:text-red-300'
                              : 'bg-white dark:bg-gray-700 border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-600'">
                            {{ day }}
                          </button>
                        </div>
                      </div>
                      <!-- Sprint calendar -->
                      <div>
                        <label class="text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1.5 block">Sprint calendar (click to toggle)</label>
                        <div class="flex flex-wrap gap-1">
                          <div v-for="d in sprintDays" :key="d.iso" class="text-center">
                            <div class="text-[10px] text-gray-500 dark:text-gray-300 mb-0.5">{{ d.weekdayShort }}</div>
                            <button @click="toggleDateOff(mi, d.iso)"
                              class="w-9 h-8 text-xs font-medium rounded-md border transition-colors"
                              :class="isDayOff(mi, d.iso)
                                ? 'bg-red-100 dark:bg-red-900/40 border-red-300 dark:border-red-700 text-red-700 dark:text-red-300'
                                : d.isWeekend
                                  ? 'bg-gray-100 dark:bg-gray-600 border-gray-200 dark:border-gray-500 text-gray-500 dark:text-gray-300'
                                  : 'bg-white dark:bg-gray-700 border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-600'"
                              :title="d.iso">
                              {{ d.day }}
                            </button>
                          </div>
                        </div>
                      </div>
                      <!-- Summary -->
                      <div class="text-xs text-gray-500 dark:text-gray-300">
                        {{ countDaysOff(member) }} working day{{ countDaysOff(member) !== 1 ? 's' : '' }} off this sprint
                        <span v-if="getStandardDaysOff(mi).length > 0" class="ml-1">(incl. {{ getStandardDaysOff(mi).map(d => weekdayLabels[d]).join(', ') }} every week)</span>
                      </div>
                    </div>
                  </td>
                </tr>
                <tr v-if="member.activities.length === 0" class="border-b border-gray-100 dark:border-gray-700/50">
                  <td class="py-2 pr-4 font-medium text-gray-800 dark:text-gray-200">
                    {{ isDemoMode ? anonName(member.display_name) : cleanName(member.display_name) }}
                  </td>
                  <td colspan="3" class="py-2 text-gray-400 dark:text-gray-300 italic text-xs">
                    No activity set
                    <UButton @click="addActivity(mi)" variant="link" color="primary" size="xs" class="ml-2">+ Add</UButton>
                  </td>
                  <td class="py-2 align-top">
                    <UButton @click="removeMember(mi)" variant="ghost" color="error" size="xs" title="Remove member">✕</UButton>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>

        <!-- Add member + actions -->
        <div class="flex items-center gap-2 mt-3 flex-wrap">
          <USelectMenu v-model="capAddMemberId" :items="capAddMemberOptions" value-key="value"
            :loading="capLoadingAllMembers" :disabled="capAvailableMembers.length === 0 && !capLoadingAllMembers"
            placeholder="Add a team member…" class="flex-1 max-w-xs" />
          <UButton @click="addMember" :disabled="!capAddMemberId" size="sm">
            + Add
          </UButton>
          <span class="mx-1 text-gray-300 dark:text-gray-600">|</span>
          <UButton @click="copyFromLastSprint" :disabled="capPushing || capCopying" :loading="capCopying"
            variant="outline" color="neutral" size="sm" icon="i-heroicons-document-duplicate">
            {{ capCopying ? 'Copying…' : 'Copy capacity from last sprint' }}
          </UButton>
          <UButton @click="loadCapacity" :disabled="capPushing"
            variant="outline" color="neutral" size="sm" icon="i-heroicons-arrow-path">
            Reload from DevOps
          </UButton>
          <span v-if="capResult" class="text-sm" :class="capResult.success ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'">
            {{ capResult.message }}
          </span>
        </div>
        <div class="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
          <UButton @click="capShowConfirm = true" :disabled="capPushing" :loading="capPushing"
            icon="i-heroicons-cloud-arrow-up">
            {{ capPushing ? 'Sending…' : 'Send to DevOps' }}
          </UButton>

          <UModal :open="capShowConfirm" @update:open="v => { if (!v) capShowConfirm = false }" title="Are you sure?" description="Confirm sending capacity to DevOps">
            <template #body>
              <p class="text-sm text-gray-700 dark:text-gray-300" v-if="capMembers.length > 0">
                This will overwrite the capacity for <strong>{{ capMembers.length }} member{{ capMembers.length !== 1 ? 's' : '' }}</strong>
                in DevOps. <span class="text-red-500 font-medium">This replaces existing capacity data.</span>
              </p>
              <p class="text-sm text-gray-700 dark:text-gray-300" v-else>
                This will <strong>clear all capacity</strong> in DevOps for this sprint.
                <span class="text-red-500 font-medium">This removes all members.</span>
              </p>
              <p v-if="isDemoMode" class="text-sm text-amber-600 dark:text-amber-400 mt-2">Disabled in demo mode</p>
            </template>
            <template #footer>
              <div class="flex justify-end gap-2">
                <UButton variant="outline" color="neutral" label="Cancel" @click="capShowConfirm = false" />
                <UButton label="Yes, send to DevOps" :disabled="isDemoMode" @click="capShowConfirm = false; pushCapacity()" />
              </div>
            </template>
          </UModal>
        </div>
      </div>

      <!-- Loading capacity -->
      <div v-else-if="capLoadingData" class="flex items-center gap-2 text-sm text-gray-500 py-8 justify-center">
        <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 animate-spin" />
        Loading capacity…
      </div>
    </div>

    <!-- =============== CALCULATE VELOCITY TAB =============== -->
    <div v-if="activeTab === 'calculate'">
      <!-- Selectors -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
          <!-- Project -->
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Project</label>
            <USelectMenu v-model="calcProjectId" :items="projectOptions" value-key="value"
              placeholder="Choose a project…" class="w-full" />
          </div>
          <!-- Team -->
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Team</label>
            <USelectMenu v-model="calcTeam" :items="calcTeamOptions" value-key="value" placeholder="Choose a team…"
              :loading="calcLoadingTeams" :disabled="!calcProjectId || calcLoadingTeams" class="w-full" />
          </div>
        </div>
        <!-- Sprint selectors -->
        <div v-if="calcTeam" class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Last Sprint (completed)</label>
            <USelectMenu v-model="calcLastIterId" :items="calcIterationOptions" value-key="value" placeholder="Last sprint…"
              :loading="calcLoadingIterations" :disabled="calcLoadingIterations" class="w-full" />
          </div>
          <div>
            <label class="block text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase tracking-wider mb-1">Target Sprint</label>
            <USelectMenu v-model="calcTargetIterId" :items="calcIterationOptions" value-key="value" placeholder="Target sprint…"
              :disabled="calcLoadingIterations" class="w-full" />
          </div>
        </div>

        <!-- Calculate button + options -->
        <div v-if="calcLastIterId && calcTargetIterId" class="mt-4 flex items-center gap-4">
          <UButton @click="runCalc" :disabled="calcLoading" :loading="calcLoading"
            icon="i-heroicons-calculator">
            {{ calcLoading ? 'Calculating…' : 'Calculate Velocity' }}
          </UButton>
          <UCheckbox v-model="calcIncludeUnassigned" label="Include unassigned capacity" class="text-sm text-gray-600 dark:text-gray-300" />
        </div>
      </div>

      <!-- Velocity result -->
      <div v-if="calcResult" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm p-6 mb-4">
        <div class="flex items-center gap-2 mb-4">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wider">Velocity Calculation</h3>
          <span class="text-xs text-gray-500 dark:text-gray-300">{{ calcIncludeUnassigned ? '(including unassigned capacity)' : '(based on Development + Testing capacity only)' }}</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          <!-- Last sprint -->
          <div class="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <h4 class="text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase mb-2">Last Sprint</h4>
            <p class="text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">{{ isDemoMode ? anonIterationPath(calcResult.last_sprint.name) : calcResult.last_sprint.name }}</p>
            <div class="space-y-1 text-sm text-gray-600 dark:text-gray-300">
              <div class="flex justify-between"><span>Dev capacity:</span><span class="font-medium">{{ calcResult.last_sprint.capacity_dev }}</span></div>
              <div class="flex justify-between"><span>Test capacity:</span><span class="font-medium">{{ calcResult.last_sprint.capacity_test }}</span></div>
              <div v-if="calcIncludeUnassigned" class="flex justify-between"><span>Unassigned capacity:</span><span class="font-medium">{{ calcResult.last_sprint.capacity_unassigned }}</span></div>
              <div class="flex justify-between border-t border-gray-200 dark:border-gray-600 pt-1"><span>Total capacity:</span><span class="font-bold">{{ calcResult.last_sprint.capacity_total }}</span></div>
              <div class="flex justify-between items-center mt-2">
                <span>Story points:</span>
                <UInput type="number" v-model.number="calcOverridePoints" :min="0" :step="0.5" size="sm" class="w-20 text-right font-bold" />
              </div>
            </div>
          </div>

          <!-- Velocity ratio -->
          <div class="flex flex-col items-center justify-center">
            <div class="text-4xl font-bold text-primary-600 dark:text-primary-400">{{ displayRatio }}</div>
            <div class="text-xs text-gray-500 dark:text-gray-300 mt-1">velocity ratio</div>
            <div class="text-xs text-gray-500 dark:text-gray-300 mt-0.5">points / capacity</div>
            <div class="mt-4 text-center">
              <div class="text-3xl font-bold text-green-600 dark:text-green-400">{{ displayProjected }}</div>
              <div class="text-xs text-gray-500 dark:text-gray-300 mt-1">projected points</div>
            </div>
          </div>

          <!-- Target sprint -->
          <div class="bg-gray-50 dark:bg-gray-700/50 rounded-lg p-4">
            <h4 class="text-xs font-semibold text-gray-500 dark:text-gray-300 uppercase mb-2">Target Sprint</h4>
            <p class="text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">{{ isDemoMode ? anonIterationPath(calcResult.target_sprint.name) : calcResult.target_sprint.name }}</p>
            <div class="space-y-1 text-sm text-gray-600 dark:text-gray-300">
              <div class="flex justify-between"><span>Dev capacity:</span><span class="font-medium">{{ calcResult.target_sprint.capacity_dev }}</span></div>
              <div class="flex justify-between"><span>Test capacity:</span><span class="font-medium">{{ calcResult.target_sprint.capacity_test }}</span></div>
              <div v-if="calcIncludeUnassigned" class="flex justify-between"><span>Unassigned capacity:</span><span class="font-medium">{{ calcResult.target_sprint.capacity_unassigned }}</span></div>
              <div class="flex justify-between border-t border-gray-200 dark:border-gray-600 pt-1"><span>Total capacity:</span><span class="font-bold">{{ calcResult.target_sprint.capacity_total }}</span></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Loading calc -->
      <div v-else-if="calcLoading" class="flex items-center gap-2 text-sm text-gray-500 py-8 justify-center">
        <UIcon name="i-heroicons-arrow-path" class="w-5 h-5 animate-spin" />
        Calculating velocity…
      </div>

      <!-- Error -->
      <UAlert v-if="calcError" color="error" icon="i-heroicons-exclamation-circle" :description="calcError" class="mt-4" />
    </div>

    <!-- =============== METRICS TAB =============== -->
    <div v-if="activeTab === 'metrics'">
      <!-- Header -->
      <div class="flex items-center justify-between mb-6">
        <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300 uppercase tracking-wider">Velocity Metrics — Scope vs Burned Story Points</h3>
        <UButton icon="i-heroicons-arrow-path" :loading="metricsLoading" :disabled="metricsLoading" @click="loadMetrics(true)" :label="metricsLoading ? 'Loading...' : 'Refresh'" />
      </div>

      <!-- Project filter -->
      <div v-if="allMetricsProjectNames.length > 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 mb-4 overflow-hidden">
        <UButton @click="metricsFilterOpen = !metricsFilterOpen" variant="ghost" color="neutral"
          class="w-full !justify-between !px-4 !py-2.5">
          <span class="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-300">
            Projects
            <span v-if="metricsExcludedCount > 0" class="ml-1 text-amber-500 dark:text-amber-400">({{ metricsExcludedCount }} hidden)</span>
          </span>
          <UIcon name="i-heroicons-chevron-down" class="w-4 h-4 text-gray-400 dark:text-gray-300 transition-transform" :class="{ 'rotate-180': metricsFilterOpen }" />
        </UButton>
        <div v-if="metricsFilterOpen" class="px-4 pb-3 border-t border-gray-100 dark:border-gray-700">
          <div class="flex items-center justify-between py-2">
            <UButton @click="toggleAllMetricsProjects" variant="link" size="xs">
              {{ allMetricsProjectsEnabled ? 'Deselect all' : 'Select all' }}
            </UButton>
          </div>
          <div class="flex flex-wrap gap-2">
            <label v-for="name in allMetricsProjectNames" :key="name"
              class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium border cursor-pointer transition-colors select-none"
              :class="isMetricsProjectEnabled(name)
                ? 'bg-primary-50 dark:bg-primary-900/30 border-primary-300 dark:border-primary-700 text-primary-700 dark:text-primary-300'
                : 'bg-gray-50 dark:bg-gray-700 border-gray-200 dark:border-gray-600 text-gray-500 dark:text-gray-300 line-through'">
              <UCheckbox :model-value="isMetricsProjectEnabled(name)" @update:model-value="toggleMetricsProject(name)" class="w-3 h-3" />
              {{ isDemoMode ? anonProject(name) : name }}
            </label>
          </div>
        </div>
      </div>

      <!-- Loading -->
      <div v-if="metricsLoading && !metricsData" class="flex flex-col items-center gap-3 py-16">
        <UIcon name="i-heroicons-arrow-path" class="w-6 h-6 text-indigo-500 dark:text-indigo-400 animate-spin" />
        <span class="text-sm text-gray-500 dark:text-gray-300">Loading velocity metrics…</span>
      </div>

      <!-- Project cards with charts -->
      <div v-else-if="metricsData && visibleMetricsProjects.length > 0" class="space-y-3">
        <div v-for="proj in visibleMetricsProjects" :key="proj.project_id"
          class="bg-white dark:bg-gray-800/50 backdrop-blur rounded-2xl border border-gray-200 dark:border-gray-700/50 overflow-hidden transition-all duration-200 hover:border-gray-300 dark:hover:border-gray-600/60 hover:shadow-lg hover:shadow-indigo-500/5 max-w-4xl">
          <div class="px-5 pt-4 pb-2">
            <div class="flex items-center gap-3">
              <div class="w-1 h-7 rounded-full bg-gradient-to-b from-indigo-500 to-emerald-500 dark:from-indigo-400 dark:to-emerald-400"></div>
              <div>
                <h4 class="text-sm font-semibold text-gray-800 dark:text-gray-100 tracking-tight">
                  {{ isDemoMode ? anonProject(proj.project_name) : proj.project_name }}
                </h4>
                <p class="text-xs text-gray-500 dark:text-gray-500 mt-0.5">
                  {{ isDemoMode ? anonTeam(proj.team_name) : proj.team_name }}
                </p>
              </div>
              <UIcon v-if="metricsTeamLoading.has(proj.project_id)" name="i-heroicons-arrow-path" class="w-4 h-4 ml-auto text-indigo-500 animate-spin" />
            </div>
          </div>
          <!-- Team filter for this project -->
          <div v-if="proj.available_teams && proj.available_teams.length > 1" class="mx-5 mb-2 rounded-lg border border-primary-200 dark:border-primary-700/50 bg-primary-50/30 dark:bg-primary-900/10 overflow-hidden">
            <UButton @click="teamFilterOpenProjects.has(proj.project_id) ? teamFilterOpenProjects.delete(proj.project_id) : teamFilterOpenProjects.add(proj.project_id)"
              variant="ghost" color="neutral" class="w-full !justify-between !px-3 !py-2">
              <span class="text-xs font-semibold uppercase tracking-wider text-primary-600 dark:text-primary-400">
                Team
              </span>
              <UIcon name="i-heroicons-chevron-down" class="w-3.5 h-3.5 text-gray-400 dark:text-gray-300 transition-transform" :class="{ 'rotate-180': teamFilterOpenProjects.has(proj.project_id) }" />
            </UButton>
            <div v-if="teamFilterOpenProjects.has(proj.project_id)" class="px-3 pb-2.5 border-t border-gray-100 dark:border-gray-700/50">
              <div class="flex flex-wrap gap-1.5 pt-1.5">
                <UButton v-for="t in proj.available_teams" :key="t"
                  @click="onMetricsTeamChange(proj, t)"
                  :disabled="metricsTeamLoading.has(proj.project_id)"
                  size="xs"
                  :variant="t === proj.team_name ? 'soft' : 'outline'"
                  :color="t === proj.team_name ? 'primary' : 'neutral'">
                  {{ isDemoMode ? anonTeam(t) : t }}
                </UButton>
              </div>
            </div>
          </div>
          <!-- Sprint filter for this project -->
          <div class="mx-5 mb-2 rounded-lg border border-primary-200 dark:border-primary-700/50 bg-primary-50/30 dark:bg-primary-900/10 overflow-hidden">
            <UButton @click="sprintFilterOpenProjects.has(proj.project_id) ? sprintFilterOpenProjects.delete(proj.project_id) : sprintFilterOpenProjects.add(proj.project_id)"
              variant="ghost" color="neutral" class="w-full !justify-between !px-3 !py-2">
              <span class="text-xs font-semibold uppercase tracking-wider text-primary-600 dark:text-primary-400">
                Sprints
                <span v-if="sprintExcludedCount(proj) > 0" class="ml-1 text-amber-500 dark:text-amber-400">({{ sprintExcludedCount(proj) }} hidden)</span>
              </span>
              <UIcon name="i-heroicons-chevron-down" class="w-3.5 h-3.5 text-gray-400 dark:text-gray-300 transition-transform" :class="{ 'rotate-180': sprintFilterOpenProjects.has(proj.project_id) }" />
            </UButton>
            <div v-if="sprintFilterOpenProjects.has(proj.project_id)" class="px-3 pb-2.5 border-t border-gray-100 dark:border-gray-700/50">
              <div class="flex items-center justify-between py-1.5">
                <UButton @click="toggleAllSprints(proj)" variant="link" size="xs">
                  {{ allSprintsEnabled(proj) ? 'Deselect all' : 'Select all' }}
                </UButton>
              </div>
              <div class="flex flex-wrap gap-1.5">
                <label v-for="s in proj.sprints" :key="s.name"
                  class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium border cursor-pointer transition-colors select-none"
                  :class="isSprintEnabled(proj.project_id, s.name)
                    ? 'bg-primary-50 dark:bg-primary-900/30 border-primary-300 dark:border-primary-700 text-primary-700 dark:text-primary-300'
                    : 'bg-gray-50 dark:bg-gray-700 border-gray-200 dark:border-gray-600 text-gray-500 dark:text-gray-300 line-through'">
                  <UCheckbox :model-value="isSprintEnabled(proj.project_id, s.name)" @update:model-value="toggleSprint(proj.project_id, s.name)" class="w-3 h-3" />
                  {{ isDemoMode ? sprintLabel(anonIterationPath(s.name)) : sprintLabel(s.name) }}
                </label>
              </div>
            </div>
          </div>
          <div class="px-3 pb-4 h-56">
            <Bar :data="buildChartData(proj)" :options="computedChartOptions" />
          </div>
        </div>
      </div>

      <!-- Empty -->
      <div v-else-if="metricsData" class="flex flex-col items-center py-16 text-gray-500 dark:text-gray-300">
        <UIcon name="i-heroicons-chart-bar" class="w-10 h-10 mb-3 text-gray-300 dark:text-gray-600" />
        <span class="text-sm">No velocity data available</span>
        <span class="text-xs text-gray-400 dark:text-gray-600 mt-1">Ensure projects have past sprints with story points</span>
      </div>

      <!-- Error -->
      <UAlert v-if="metricsError" color="error" icon="i-heroicons-exclamation-circle" :description="metricsError" class="mt-4" />
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useMonitorStore } from '../stores/monitor.js'
import { useVelocityStore } from '../stores/velocity.js'
import { useDemoMode, anonName, anonProject, anonTeam, anonIterationPath } from '../composables/useDemoMode.js'
import DataFreshness from '../components/DataFreshness.vue'
import { useTheme } from '../composables/useTheme.js'
import { Bar } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, PointElement, LineElement, LineController, BarController, Title, Tooltip, Legend, Filler } from 'chart.js'

ChartJS.register(CategoryScale, LinearScale, BarElement, PointElement, LineElement, LineController, BarController, Title, Tooltip, Legend, Filler)

const { dark: isDark } = useTheme()
const store = useMonitorStore()
const velocityStore = useVelocityStore()
const { isDemoMode } = useDemoMode()

function cleanName(name) {
  return name ? name.replace(/<[^>]+>$/, '').trim() : ''
}

// Tabs
const tabs = [
  { value: 'capacity', label: 'Capacity', icon: 'i-heroicons-users' },
  { value: 'calculate', label: 'Calculate Velocity', icon: 'i-heroicons-calculator' },
  { value: 'metrics', label: 'Velocity Metrics', icon: 'i-heroicons-chart-bar' },
]
const activeTab = ref('capacity')

// Projects (shared)
const projects = ref([])

const projectOptions = computed(() =>
  projects.value.map(p => ({ value: p.id, label: isDemoMode.value ? anonProject(p.project) : p.project }))
)
const activityOptions = [
  { value: 'Development', label: 'Development' },
  { value: 'Testing', label: 'Testing' },
  { value: 'Design', label: 'Design' },
  { value: 'Documentation', label: 'Documentation' },
  { value: 'Deployment', label: 'Deployment' },
  { value: 'Requirements', label: 'Requirements' },
]

onMounted(async () => {
  loadSavedExcludedProjects()
  loadSavedExcludedSprints()
  loadSavedSelectedTeams()
  await velocityStore.fetchVelocityProjects()
  projects.value = velocityStore.velocityProjects
  if (projects.value.length === 1) {
    capProjectId.value = projects.value[0].id
    calcProjectId.value = projects.value[0].id
  }
})

// =============== CAPACITY TAB STATE ===============
const capProjectId = ref('')
const capTeam = ref('')
const capIterationId = ref('')
const capTeams = ref([])
const capIterations = ref([])
const capData = ref(null)
const capMembers = ref([])
const capLoadingTeams = ref(false)
const capLoadingIterations = ref(false)
const capLoadingData = ref(false)
const capPushing = ref(false)
const capCopying = ref(false)
const capResult = ref(null)
const capShowConfirm = ref(false)
const capAllMembers = ref([])
const capLoadingAllMembers = ref(false)
const capAddMemberId = ref('')
const capExpandedDaysOff = ref(null) // member index or null
const capStandardDaysOff = ref({}) // { member_id: Set<weekdayIndex> }

const capTeamOptions = computed(() =>
  capTeams.value.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
)
const capIterationOptions = computed(() =>
  capIterations.value.map(it => ({
    value: it.id,
    label: `${isDemoMode.value ? anonIterationPath(it.name) : it.name}${it.timeframe ? ` (${it.timeframe})` : ''}`
  }))
)

const weekdayLabels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']

// Build array of dates in the selected sprint
const sprintDays = computed(() => {
  const iter = capIterations.value.find(i => i.id === capIterationId.value)
  if (!iter?.start_date || !iter?.finish_date) return []
  const days = []
  const start = new Date(iter.start_date)
  const end = new Date(iter.finish_date)
  for (let d = new Date(start); d <= end; d.setUTCDate(d.getUTCDate() + 1)) {
    const iso = d.toISOString().slice(0, 10)
    const jsDay = d.getUTCDay() // 0=Sun
    const weekdayIdx = jsDay === 0 ? 6 : jsDay - 1 // 0=Mon
    days.push({
      iso,
      day: d.getUTCDate(),
      weekdayShort: weekdayLabels[weekdayIdx],
      weekdayIdx,
      isWeekend: jsDay === 0 || jsDay === 6,
    })
  }
  return days
})

function toggleDaysOff(mi) {
  capExpandedDaysOff.value = capExpandedDaysOff.value === mi ? null : mi
}

function getStandardDaysOff(mi) {
  const id = capMembers.value[mi]?.member_id
  return id && capStandardDaysOff.value[id] ? [...capStandardDaysOff.value[id]] : []
}

function isStandardDayOff(mi, weekdayIdx) {
  const id = capMembers.value[mi]?.member_id
  return !!capStandardDaysOff.value[id]?.has(weekdayIdx)
}

function toggleStandardDayOff(mi, weekdayIdx) {
  const member = capMembers.value[mi]
  if (!member) return
  const id = member.member_id
  if (!capStandardDaysOff.value[id]) capStandardDaysOff.value[id] = new Set()
  const set = capStandardDaysOff.value[id]
  if (set.has(weekdayIdx)) set.delete(weekdayIdx)
  else set.add(weekdayIdx)
  // Rebuild days_off for this member based on standard + individual
  rebuildDaysOff(mi)
}

function isDayOff(mi, iso) {
  const member = capMembers.value[mi]
  if (!member) return false
  return member.days_off.some(r => {
    const s = r.start.slice(0, 10)
    const e = r.end.slice(0, 10)
    return iso >= s && iso <= e
  })
}

function toggleDateOff(mi, iso) {
  const member = capMembers.value[mi]
  if (!member) return
  if (isDayOff(mi, iso)) {
    // Remove this date from days_off ranges (split ranges if needed)
    const newRanges = []
    for (const r of member.days_off) {
      const s = r.start.slice(0, 10)
      const e = r.end.slice(0, 10)
      if (iso < s || iso > e) { newRanges.push(r); continue }
      // Split: keep days before iso and after iso
      if (s < iso) newRanges.push({ start: r.start, end: prevDay(iso) + 'T00:00:00Z' })
      if (e > iso) newRanges.push({ start: nextDay(iso) + 'T00:00:00Z', end: r.end })
    }
    member.days_off = newRanges
  } else {
    // Add this date — try to merge with adjacent ranges
    member.days_off.push({ start: iso + 'T00:00:00Z', end: iso + 'T00:00:00Z' })
    member.days_off = mergeRanges(member.days_off)
  }
}

function rebuildDaysOff(mi) {
  const member = capMembers.value[mi]
  if (!member) return
  const id = member.member_id
  const standardSet = capStandardDaysOff.value[id] || new Set()
  // Collect all individual off-dates already set
  const offDates = new Set()
  for (const r of member.days_off) {
    const s = new Date(r.start)
    const e = new Date(r.end)
    for (let d = new Date(s); d <= e; d.setUTCDate(d.getUTCDate() + 1)) {
      offDates.add(d.toISOString().slice(0, 10))
    }
  }
  // Add/remove standard weekday dates in the sprint
  for (const sd of sprintDays.value) {
    if (standardSet.has(sd.weekdayIdx)) offDates.add(sd.iso)
    // Don't remove dates that happen to match an un-toggled weekday — only standard toggling adds
  }
  // Rebuild ranges from the set of dates
  const sorted = [...offDates].sort()
  const ranges = []
  for (const iso of sorted) {
    if (ranges.length > 0) {
      const lastEnd = ranges[ranges.length - 1].end.slice(0, 10)
      if (iso === nextDay(lastEnd)) {
        ranges[ranges.length - 1].end = iso + 'T00:00:00Z'
        continue
      }
    }
    ranges.push({ start: iso + 'T00:00:00Z', end: iso + 'T00:00:00Z' })
  }
  member.days_off = ranges
}

function countDaysOff(member) {
  let count = 0
  for (const r of member.days_off) {
    const s = new Date(r.start)
    const e = new Date(r.end)
    for (let d = new Date(s); d <= e; d.setUTCDate(d.getUTCDate() + 1)) {
      const jsDay = d.getUTCDay()
      if (jsDay !== 0 && jsDay !== 6) count++ // only count working days
    }
  }
  return count
}

function prevDay(iso) {
  const d = new Date(iso); d.setUTCDate(d.getUTCDate() - 1); return d.toISOString().slice(0, 10)
}
function nextDay(iso) {
  const d = new Date(iso); d.setUTCDate(d.getUTCDate() + 1); return d.toISOString().slice(0, 10)
}
function mergeRanges(ranges) {
  if (ranges.length <= 1) return ranges
  const sorted = ranges.map(r => ({ start: r.start.slice(0, 10), end: r.end.slice(0, 10) })).sort((a, b) => a.start.localeCompare(b.start))
  const merged = [sorted[0]]
  for (let i = 1; i < sorted.length; i++) {
    const last = merged[merged.length - 1]
    if (sorted[i].start <= nextDay(last.end)) {
      last.end = sorted[i].end > last.end ? sorted[i].end : last.end
    } else merged.push(sorted[i])
  }
  return merged.map(r => ({ start: r.start + 'T00:00:00Z', end: r.end + 'T00:00:00Z' }))
}

// Detect standard days off from existing days_off data
function detectStandardDaysOff(mi) {
  const member = capMembers.value[mi]
  if (!member || sprintDays.value.length === 0) return
  const id = member.member_id
  const offDates = new Set()
  for (const r of member.days_off) {
    const s = new Date(r.start)
    const e = new Date(r.end)
    for (let d = new Date(s); d <= e; d.setUTCDate(d.getUTCDate() + 1)) {
      offDates.add(d.toISOString().slice(0, 10))
    }
  }
  // For each weekday (Mon-Fri), check if ALL occurrences in sprint are off
  const weekdayCounts = Array(7).fill(0)
  const weekdayOff = Array(7).fill(0)
  for (const sd of sprintDays.value) {
    if (sd.isWeekend) continue
    weekdayCounts[sd.weekdayIdx]++
    if (offDates.has(sd.iso)) weekdayOff[sd.weekdayIdx]++
  }
  const standardSet = new Set()
  for (let i = 0; i < 5; i++) { // Mon-Fri only
    if (weekdayCounts[i] > 0 && weekdayOff[i] === weekdayCounts[i]) standardSet.add(i)
  }
  if (standardSet.size > 0) capStandardDaysOff.value[id] = standardSet
}

const capAvailableMembers = computed(() => {
  const existingIds = new Set(capMembers.value.map(m => m.member_id))
  return capAllMembers.value.filter(m => !existingIds.has(m.id))
})
const capAddMemberOptions = computed(() =>
  capAvailableMembers.value.map(m => ({ value: m.id, label: isDemoMode.value ? anonName(m.display_name) : cleanName(m.display_name) }))
)

const capTotalDev = computed(() => {
  let total = 0
  for (const m of capMembers.value) {
    for (const a of m.activities) {
      if (a.name === 'Development') total += (a.capacity_per_day || 0)
    }
  }
  return Math.round(total * 10) / 10
})
const capTotalTest = computed(() => {
  let total = 0
  for (const m of capMembers.value) {
    for (const a of m.activities) {
      if (a.name === 'Testing') total += (a.capacity_per_day || 0)
    }
  }
  return Math.round(total * 10) / 10
})
const capTotalAll = computed(() => {
  let total = 0
  for (const m of capMembers.value) {
    for (const a of m.activities) total += (a.capacity_per_day || 0)
  }
  return Math.round(total * 10) / 10
})
const capTotalOther = computed(() => {
  let total = 0
  for (const m of capMembers.value) {
    for (const a of m.activities) {
      if (a.name !== 'Development' && a.name !== 'Testing' && a.name) total += (a.capacity_per_day || 0)
    }
  }
  return Math.round(total * 10) / 10
})
const capTotalUnassigned = computed(() => {
  let total = 0
  for (const m of capMembers.value) {
    for (const a of m.activities) {
      if (!a.name) total += (a.capacity_per_day || 0)
    }
  }
  return Math.round(total * 10) / 10
})

async function onCapProjectChange() {
  capTeam.value = ''
  capIterationId.value = ''
  capTeams.value = []
  capIterations.value = []
  capData.value = null
  capMembers.value = []
  capResult.value = null
  if (!capProjectId.value) return
  capLoadingTeams.value = true
  try {
    capTeams.value = await velocityStore.fetchVelocityTeams(capProjectId.value)
    if (capTeams.value.length === 1) {
      capTeam.value = capTeams.value[0].name
    }
  } finally { capLoadingTeams.value = false }
}

async function onCapTeamChange() {
  capIterationId.value = ''
  capIterations.value = []
  capData.value = null
  capMembers.value = []
  capResult.value = null
  capAllMembers.value = []
  capAddMemberId.value = ''
  if (!capTeam.value) return
  capLoadingIterations.value = true
  capLoadingAllMembers.value = true
  try {
    const [iterations] = await Promise.all([
      velocityStore.fetchVelocityIterations(capProjectId.value, capTeam.value),
      velocityStore.fetchVelocityTeamMembers(capProjectId.value, capTeam.value).then(m => { capAllMembers.value = m; capLoadingAllMembers.value = false }),
    ])
    capIterations.value = iterations
    // Auto-select current or next future sprint
    const current = capIterations.value.find(i => i.timeframe === 'current')
    const future = capIterations.value.find(i => i.timeframe === 'future')
    if (current) capIterationId.value = current.id
    else if (future) capIterationId.value = future.id
  } finally { capLoadingIterations.value = false; capLoadingAllMembers.value = false }
}

async function onCapSprintChange() {
  capData.value = null
  capMembers.value = []
  capResult.value = null
  if (!capIterationId.value) return
  await loadCapacity()
}

watch(capProjectId, () => onCapProjectChange())
watch(capTeam, () => onCapTeamChange())
watch(capIterationId, () => onCapSprintChange())

async function loadCapacity() {
  capLoadingData.value = true
  capExpandedDaysOff.value = null
  capResult.value = null
  capShowConfirm.value = false
  try {
    const data = await velocityStore.fetchVelocityCapacity(capProjectId.value, capTeam.value, capIterationId.value)
    capData.value = data
    // Deep-copy members for editing
    capMembers.value = JSON.parse(JSON.stringify(data.members))
    // Ensure every member has at least one activity slot
    for (const m of capMembers.value) {
      if (m.activities.length === 0) m.activities.push({ name: null, capacity_per_day: 0 })
      for (const a of m.activities) { if (a.name === '') a.name = null }
    }
    // Detect standard days off from existing data
    capStandardDaysOff.value = {}
    for (let i = 0; i < capMembers.value.length; i++) detectStandardDaysOff(i)
  } catch (e) {
    capResult.value = { success: false, message: e?.message || 'Failed to load capacity' }
  } finally { capLoadingData.value = false }
}

function addActivity(memberIdx) {
  capMembers.value[memberIdx].activities.push({ name: null, capacity_per_day: 0 })
}

function removeMember(memberIdx) {
  if (capExpandedDaysOff.value === memberIdx) capExpandedDaysOff.value = null
  else if (capExpandedDaysOff.value > memberIdx) capExpandedDaysOff.value--
  const id = capMembers.value[memberIdx].member_id
  capMembers.value.splice(memberIdx, 1)
  delete capStandardDaysOff.value[id]
}

function addMember() {
  const m = capAllMembers.value.find(m => m.id === capAddMemberId.value)
  if (!m) return
  capMembers.value.push({
    member_id: m.id,
    display_name: m.display_name,
    activities: [{ name: null, capacity_per_day: 0 }],
    days_off: [],
  })
  capAddMemberId.value = ''
}

async function copyFromLastSprint() {
  capCopying.value = true
  capResult.value = null
  capShowConfirm.value = false
  try {
    const data = await velocityStore.fetchPreviousCapacity(capProjectId.value, capTeam.value, capIterationId.value)
    if (!data.members || data.members.length === 0) {
      capResult.value = { success: false, message: 'No previous sprint with capacity found.' }
      return
    }
    // Replace current members with copied members (people + activity + capacity, no days off)
    capMembers.value = data.members.map(m => ({
      member_id: m.member_id,
      display_name: m.display_name,
      activities: (m.activities || []).map(a => ({ name: a.name, capacity_per_day: a.capacity_per_day })),
      days_off: [],
    }))
    // Ensure every member has at least one activity slot
    for (const m of capMembers.value) {
      if (m.activities.length === 0) m.activities.push({ name: null, capacity_per_day: 0 })
    }
    capStandardDaysOff.value = {}
    capExpandedDaysOff.value = null
    capResult.value = { success: true, message: `Copied ${data.members.length} members from previous sprint.` }
  } catch (e) {
    capResult.value = { success: false, message: e?.message || 'Failed to copy from last sprint' }
  } finally { capCopying.value = false }
}

async function pushCapacity() {
  // Validate all members have activity set and capacity > 0
  const invalid = []
  for (const m of capMembers.value) {
    const name = cleanName(m.display_name)
    for (const a of m.activities) {
      if (!a.capacity_per_day || a.capacity_per_day <= 0) invalid.push(`${name}: capacity/day must be > 0`)
    }
    if (m.activities.length === 0) invalid.push(`${name}: no activity`)
  }
  if (invalid.length > 0) {
    capResult.value = { success: false, message: `Validation failed: ${invalid[0]}${invalid.length > 1 ? ` (+${invalid.length - 1} more)` : ''}` }
    return
  }

  capPushing.value = true
  capResult.value = null
  try {
    const body = {
      team: capTeam.value,
      iteration_id: capIterationId.value,
      members: capMembers.value.map(m => ({
        member_id: m.member_id,
        display_name: m.display_name,
        activities: m.activities.filter(a => a.name || a.capacity_per_day > 0),
        days_off: m.days_off,
      })),
    }
    const resp = await velocityStore.pushVelocityCapacity(capProjectId.value, body)
    // Reload from DevOps to sync UI with actual state
    // KNOWN BUG: DevOps re-populates capacity after clearing via empty PUT.
    // The API confirms 0 members, but DevOps restores them server-side.
    await loadCapacity()
    capResult.value = { success: true, message: resp.detail || 'Capacity updated!' }
  } catch (e) {
    capResult.value = { success: false, message: e?.message || 'Failed to push capacity' }
  } finally { capPushing.value = false }
}

// =============== CALCULATE VELOCITY TAB STATE ===============
const calcProjectId = ref('')
const calcTeam = ref('')
const calcTeams = ref([])
const calcIterations = ref([])
const calcLastIterId = ref('')
const calcTargetIterId = ref('')
const calcLoadingTeams = ref(false)
const calcLoadingIterations = ref(false)
const calcLoading = ref(false)
const calcResult = ref(null)
const calcError = ref('')
const calcOverridePoints = ref(0)
const calcIncludeUnassigned = ref(localStorage.getItem('velocity_includeUnassigned') === 'true')
watch(calcIncludeUnassigned, v => {
  if (v) localStorage.setItem('velocity_includeUnassigned', 'true')
  else localStorage.removeItem('velocity_includeUnassigned')
})

const calcTeamOptions = computed(() =>
  calcTeams.value.map(t => ({ value: t.name, label: isDemoMode.value ? anonTeam(t.name) : t.name }))
)
const calcIterationOptions = computed(() =>
  calcIterations.value.map(it => ({
    value: it.id,
    label: `${isDemoMode.value ? anonIterationPath(it.name) : it.name}${it.timeframe ? ` (${it.timeframe})` : ''}`
  }))
)

const displayRatio = computed(() => {
  if (!calcResult.value) return '—'
  const targetTotal = calcResult.value.target_sprint.capacity_total
  const pts = calcOverridePoints.value
  const lastTotal = calcResult.value.last_sprint.capacity_total
  if (lastTotal > 0) return (pts / lastTotal).toFixed(2)
  return calcResult.value.velocity_ratio.toFixed(2)
})

const displayProjected = computed(() => {
  if (!calcResult.value) return '—'
  const targetTotal = calcResult.value.target_sprint.capacity_total
  const lastTotal = calcResult.value.last_sprint.capacity_total
  const pts = calcOverridePoints.value
  if (lastTotal > 0) return ((pts / lastTotal) * targetTotal).toFixed(1)
  return calcResult.value.projected_points.toFixed(1)
})

async function onCalcProjectChange() {
  calcTeam.value = ''
  calcTeams.value = []
  calcIterations.value = []
  calcLastIterId.value = ''
  calcTargetIterId.value = ''
  calcResult.value = null
  calcError.value = ''
  if (!calcProjectId.value) return
  calcLoadingTeams.value = true
  try {
    calcTeams.value = await velocityStore.fetchVelocityTeams(calcProjectId.value)
    if (calcTeams.value.length === 1) {
      calcTeam.value = calcTeams.value[0].name
    }
  } finally { calcLoadingTeams.value = false }
}

async function onCalcTeamChange() {
  calcIterations.value = []
  calcLastIterId.value = ''
  calcTargetIterId.value = ''
  calcResult.value = null
  calcError.value = ''
  if (!calcTeam.value) return
  calcLoadingIterations.value = true
  try {
    calcIterations.value = await velocityStore.fetchVelocityIterations(calcProjectId.value, calcTeam.value)
    // Auto-detect: current sprint as last, first future sprint as target
    const current = calcIterations.value.find(i => i.timeframe === 'current')
    const future = calcIterations.value.find(i => i.timeframe === 'future')
    if (current) calcLastIterId.value = current.id
    if (future) calcTargetIterId.value = future.id
  } finally { calcLoadingIterations.value = false }
}

watch(calcProjectId, () => onCalcProjectChange())
watch(calcTeam, () => onCalcTeamChange())

async function runCalc() {
  calcLoading.value = true
  calcError.value = ''
  try {
    const override = calcResult.value && calcOverridePoints.value !== calcResult.value.last_sprint.story_points
      ? calcOverridePoints.value : null
    const data = await velocityStore.calculateVelocity(
      calcProjectId.value, calcTeam.value, calcLastIterId.value, calcTargetIterId.value, override, calcIncludeUnassigned.value)
    calcResult.value = data
    calcOverridePoints.value = data.last_sprint.story_points
  } catch (e) {
    calcError.value = e?.message || 'Failed to calculate velocity'
  } finally { calcLoading.value = false }
}

// =============== METRICS TAB STATE ===============
const metricsData = ref(null)
const metricsLoading = ref(false)
const metricsError = ref('')
const metricsFilterOpen = ref(false)
const metricsTeamLoading = reactive(new Set())

const LS_SELECTED_TEAMS = 'velocity_selectedTeams'
const metricsSelectedTeams = reactive(new Map()) // projectId -> teamName

function loadSavedSelectedTeams() {
  const saved = localStorage.getItem(LS_SELECTED_TEAMS)
  if (!saved) return
  try {
    const obj = JSON.parse(saved)
    for (const [pid, team] of Object.entries(obj)) {
      if (typeof team === 'string' && team) metricsSelectedTeams.set(pid, team)
    }
  } catch { /* ignore */ }
}

function saveSelectedTeams() {
  const obj = Object.fromEntries(metricsSelectedTeams)
  if (Object.keys(obj).length === 0) localStorage.removeItem(LS_SELECTED_TEAMS)
  else localStorage.setItem(LS_SELECTED_TEAMS, JSON.stringify(obj))
}

async function onMetricsTeamChange(proj, newTeam) {
  if (newTeam === proj.team_name) return
  metricsTeamLoading.add(proj.project_id)
  try {
    const updated = await velocityStore.fetchVelocityProjectMetrics(proj.project_id, newTeam, 10)
    // Clear sprint exclusions since sprints differ per team
    metricsExcludedSprints.delete(proj.project_id)
    saveExcludedSprints()
    // Save selected team
    metricsSelectedTeams.set(proj.project_id, newTeam)
    saveSelectedTeams()
    // Replace the project data in metricsData
    const idx = metricsData.value.projects.findIndex(p => p.project_id === proj.project_id)
    if (idx !== -1) metricsData.value.projects.splice(idx, 1, updated)
  } catch (e) {
    console.error('Failed to switch team', e)
  } finally {
    metricsTeamLoading.delete(proj.project_id)
  }
}

const LS_EXCLUDED_VELOCITY_PROJECTS = 'velocity_excludedProjects'
const metricsExcludedProjects = reactive(new Set())

function loadSavedExcludedProjects() {
  const saved = localStorage.getItem(LS_EXCLUDED_VELOCITY_PROJECTS)
  if (!saved) return
  try {
    const arr = JSON.parse(saved)
    if (Array.isArray(arr)) arr.forEach(n => metricsExcludedProjects.add(n))
  } catch { /* ignore */ }
}

function saveExcludedProjects() {
  const arr = [...metricsExcludedProjects]
  if (arr.length === 0) localStorage.removeItem(LS_EXCLUDED_VELOCITY_PROJECTS)
  else localStorage.setItem(LS_EXCLUDED_VELOCITY_PROJECTS, JSON.stringify(arr))
}

const allMetricsProjectNames = computed(() => {
  if (!metricsData.value?.projects) return []
  return metricsData.value.projects.map(p => p.project_name).sort()
})

function isMetricsProjectEnabled(name) {
  return !metricsExcludedProjects.has(name)
}

function toggleMetricsProject(name) {
  if (metricsExcludedProjects.has(name)) metricsExcludedProjects.delete(name)
  else metricsExcludedProjects.add(name)
  saveExcludedProjects()
}

function toggleAllMetricsProjects() {
  if (allMetricsProjectsEnabled.value) {
    allMetricsProjectNames.value.forEach(n => metricsExcludedProjects.add(n))
  } else {
    metricsExcludedProjects.clear()
  }
  saveExcludedProjects()
}

const allMetricsProjectsEnabled = computed(() => {
  return allMetricsProjectNames.value.length > 0 && allMetricsProjectNames.value.every(n => !metricsExcludedProjects.has(n))
})

const metricsExcludedCount = computed(() => {
  return allMetricsProjectNames.value.filter(n => metricsExcludedProjects.has(n)).length
})

const visibleMetricsProjects = computed(() => {
  if (!metricsData.value?.projects) return []
  return metricsData.value.projects.filter(p => !metricsExcludedProjects.has(p.project_name))
})

// =============== TEAM FILTER (per-project) ===============
const teamFilterOpenProjects = reactive(new Set())

// =============== SPRINT FILTER (per-project) ===============
const LS_EXCLUDED_SPRINTS = 'velocity_excludedSprints'
const metricsExcludedSprints = reactive(new Map())
const sprintFilterOpenProjects = reactive(new Set())

function loadSavedExcludedSprints() {
  const saved = localStorage.getItem(LS_EXCLUDED_SPRINTS)
  if (!saved) return
  try {
    const obj = JSON.parse(saved)
    for (const [pid, arr] of Object.entries(obj)) {
      if (Array.isArray(arr) && arr.length > 0) metricsExcludedSprints.set(pid, reactive(new Set(arr)))
    }
  } catch { /* ignore */ }
}

function saveExcludedSprints() {
  const obj = {}
  for (const [pid, set] of metricsExcludedSprints.entries()) {
    if (set.size > 0) obj[pid] = [...set]
  }
  if (Object.keys(obj).length === 0) localStorage.removeItem(LS_EXCLUDED_SPRINTS)
  else localStorage.setItem(LS_EXCLUDED_SPRINTS, JSON.stringify(obj))
}

function isSprintEnabled(projectId, sprintName) {
  return !metricsExcludedSprints.get(projectId)?.has(sprintName)
}

function toggleSprint(projectId, sprintName) {
  if (!metricsExcludedSprints.has(projectId)) metricsExcludedSprints.set(projectId, reactive(new Set()))
  const set = metricsExcludedSprints.get(projectId)
  if (set.has(sprintName)) set.delete(sprintName)
  else set.add(sprintName)
  if (set.size === 0) metricsExcludedSprints.delete(projectId)
  saveExcludedSprints()
}

function toggleAllSprints(proj) {
  if (allSprintsEnabled(proj)) {
    metricsExcludedSprints.set(proj.project_id, reactive(new Set(proj.sprints.map(s => s.name))))
  } else {
    metricsExcludedSprints.delete(proj.project_id)
  }
  saveExcludedSprints()
}

function allSprintsEnabled(proj) {
  const set = metricsExcludedSprints.get(proj.project_id)
  if (!set || set.size === 0) return true
  return proj.sprints.every(s => !set.has(s.name))
}

function sprintExcludedCount(proj) {
  const set = metricsExcludedSprints.get(proj.project_id)
  if (!set) return 0
  return proj.sprints.filter(s => set.has(s.name)).length
}

const computedChartOptions = computed(() => {
  const d = isDark.value
  const textColor = d ? '#94a3b8' : '#475569'
  const gridColor = d ? 'rgba(148, 163, 184, 0.08)' : 'rgba(148, 163, 184, 0.2)'
  const tooltipBg = d ? 'rgba(15, 23, 42, 0.9)' : 'rgba(255, 255, 255, 0.95)'
  const tooltipTitleColor = d ? '#f1f5f9' : '#1e293b'
  const tooltipBodyColor = d ? '#cbd5e1' : '#475569'
  const tooltipBorder = d ? 'rgba(51, 65, 85, 0.5)' : 'rgba(203, 213, 225, 0.8)'
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: {
        position: 'top',
        align: 'end',
        labels: {
          color: textColor,
          font: { size: 11, family: 'Inter, system-ui, sans-serif', weight: '500' },
          usePointStyle: true,
          padding: 16,
        },
      },
      tooltip: {
        backgroundColor: tooltipBg,
        titleColor: tooltipTitleColor,
        bodyColor: tooltipBodyColor,
        borderColor: tooltipBorder,
        borderWidth: d ? 0 : 1,
        titleFont: { size: 12, weight: '600' },
        bodyFont: { size: 12 },
        padding: 12,
        cornerRadius: 8,
        displayColors: true,
        usePointStyle: true,
        callbacks: {
          label: (ctx) => ` ${ctx.dataset.label}: ${Number(ctx.parsed.y).toFixed(1)} SP`,
        },
      },
    },
    scales: {
      x: {
        ticks: {
          color: textColor,
          font: { size: 10, family: 'Inter, system-ui, sans-serif' },
          maxRotation: 45,
          minRotation: 25,
        },
        grid: { display: false },
        border: { display: false },
      },
      y: {
        beginAtZero: true,
        ticks: {
          color: textColor,
          font: { size: 10, family: 'Inter, system-ui, sans-serif' },
          padding: 8,
        },
        grid: { color: gridColor, drawTicks: false },
        border: { display: false, dash: [4, 4] },
      },
    },
  }
})

function sprintLabel(name) {
  // Extract sprint number: "S50 - 0.50 - description" → "S50", "Iteration 3" → "3", "4.4.16" → "4.4.16"
  const sMatch = name.match(/^(S\d+)/i)
  if (sMatch) return sMatch[1]
  const iterMatch = name.match(/^Iteration\s+(\d+)/i)
  if (iterMatch) return `#${iterMatch[1]}`
  // If short enough already, keep as-is; otherwise take first token
  if (name.length <= 10) return name
  return name.split(/\s+-\s+/)[0] || name
}

function buildChartData(proj) {
  const d = isDark.value
  const pointBorder = d ? '#1e293b' : '#ffffff'
  const scopeFill = d ? 'rgba(99, 102, 241, 0.06)' : 'rgba(99, 102, 241, 0.1)'
  const burnedFill = d ? 'rgba(52, 211, 153, 0.08)' : 'rgba(16, 185, 129, 0.1)'

  const sprints = proj.sprints.filter(s => isSprintEnabled(proj.project_id, s.name))

  const labels = sprints.map(s => {
    const raw = isDemoMode.value ? anonIterationPath(s.name) : s.name
    const short = sprintLabel(raw)
    if (s.timeframe === 'current') return `${short} ●`
    if (s.timeframe === 'future') return `${short} ◇`
    return short
  })

  // Compute average burned SP from past sprints (exclude current & future)
  const pastSprints = sprints.filter(s => s.timeframe === 'past')
  const avgBurned = pastSprints.length > 0
    ? pastSprints.reduce((sum, s) => sum + s.burned_points, 0) / pastSprints.length
    : 0

  // Build prognosis dataset: null for all past, then connect from last actual to future
  const lastActualIdx = sprints.findLastIndex(s => s.timeframe !== 'future')
  const prognosisData = sprints.map((s, i) => {
    if (i === lastActualIdx) return s.burned_points // anchor to last real value
    if (s.timeframe === 'future') return Math.round(avgBurned)
    return null
  })

  // Actual lines: null out future sprints so lines stop at current
  const scopeData = sprints.map(s => s.timeframe === 'future' ? null : s.initial_scope)
  const burnedData = sprints.map(s => s.timeframe === 'future' ? null : s.burned_points)

  return {
    labels,
    datasets: [
      {
        type: 'line',
        label: 'Initial Scope',
        data: scopeData,
        borderColor: d ? '#818cf8' : '#6366f1',
        backgroundColor: scopeFill,
        borderWidth: 2.5,
        pointBackgroundColor: d ? '#818cf8' : '#6366f1',
        pointBorderColor: pointBorder,
        pointBorderWidth: 2,
        pointRadius: 5,
        pointHoverRadius: 7,
        tension: 0.3,
        fill: true,
        spanGaps: false,
        order: 3,
      },
      {
        type: 'line',
        label: 'Burned SP',
        data: burnedData,
        borderColor: d ? '#34d399' : '#10b981',
        backgroundColor: burnedFill,
        borderWidth: 2.5,
        pointBackgroundColor: d ? '#34d399' : '#10b981',
        pointBorderColor: pointBorder,
        pointBorderWidth: 2,
        pointRadius: 5,
        pointHoverRadius: 7,
        tension: 0.3,
        fill: true,
        spanGaps: false,
        order: 2,
      },
      {
        type: 'line',
        label: 'Prognosis',
        data: prognosisData,
        borderColor: d ? '#fbbf24' : '#d97706',
        backgroundColor: 'rgba(251, 191, 36, 0.04)',
        borderWidth: 2,
        borderDash: [6, 4],
        pointBackgroundColor: d ? '#fbbf24' : '#d97706',
        pointBorderColor: pointBorder,
        pointBorderWidth: 2,
        pointRadius: 4,
        pointHoverRadius: 6,
        tension: 0.3,
        fill: false,
        spanGaps: true,
        order: 1,
      },
    ],
  }
}

async function loadMetrics(force = false) {
  metricsLoading.value = true
  metricsError.value = ''
  try {
    const data = await velocityStore.fetchVelocityMetrics(10)
    metricsData.value = data
    // Apply saved team selections
    if (data?.projects) {
      const teamSwitches = data.projects
        .filter(p => metricsSelectedTeams.has(p.project_id) && metricsSelectedTeams.get(p.project_id) !== p.team_name
          && p.available_teams?.includes(metricsSelectedTeams.get(p.project_id)))
        .map(p => onMetricsTeamChange(p, metricsSelectedTeams.get(p.project_id)))
      await Promise.allSettled(teamSwitches)
    }
  } catch (e) {
    metricsError.value = e?.message || 'Failed to load velocity metrics'
  } finally { metricsLoading.value = false }
}

// Auto-load metrics when switching to that tab
watch(activeTab, (tab) => {
  if (tab === 'metrics' && !metricsData.value && !metricsLoading.value) loadMetrics()
})
</script>
