<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-3">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">DEV Assessment</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Analyse completed PRs, work item flow, and developer performance. <DataFreshness :timestamp="lastFetchedAt" />
        </p>
      </div>
      <div class="flex items-center gap-3">
        <!-- CSV export -->
        <button v-if="currentDevs.length" @click="exportTeamOverview"
          class="inline-flex items-center gap-1.5 px-3 py-2.5 text-sm font-medium text-gray-600 dark:text-gray-300 bg-white dark:bg-gray-800 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
          title="Export team overview to CSV"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" /></svg>
          CSV
        </button>
        <!-- Time mode toggle -->
        <div class="flex rounded-md border border-gray-300 dark:border-gray-600 overflow-hidden text-xs">
          <button @click="timeMode = 'months'"
            class="px-3 py-2 font-medium transition-colors"
            :class="timeMode === 'months' ? 'bg-primary-600 text-white' : 'bg-white dark:bg-gray-700 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-600'">
            Months
          </button>
          <button @click="timeMode = 'sprint'"
            class="px-3 py-2 font-medium transition-colors border-l border-gray-300 dark:border-gray-600"
            :class="timeMode === 'sprint' ? 'bg-primary-600 text-white' : 'bg-white dark:bg-gray-700 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-600'">
            Sprint
          </button>
        </div>
        <button @click="runAssessment" :disabled="loading || runDisabled"
          class="inline-flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-semibold text-white shadow-sm transition-colors"
          :class="loading || runDisabled ? 'bg-primary-400 cursor-not-allowed' : 'bg-primary-600 hover:bg-primary-700'">
          <svg v-if="loading" class="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"/>
          </svg>
          <svg v-else class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-.856.917-1.398 1.667-.986l11.54 6.347a1.125 1.125 0 010 1.972l-11.54 6.347a1.125 1.125 0 01-1.667-.986V5.653z" />
          </svg>
          {{ loading ? 'Running…' : 'Run Assessment' }}
        </button>
      </div>
    </div>

    <!-- Filter row -->
    <div class="flex items-center gap-3 mb-6 flex-wrap justify-end">
      <!-- Months selector -->
      <SelectMenu v-if="timeMode === 'months'" v-model="months" :options="monthOptions" size="sm" class="w-40" />

      <!-- Project selector (always visible) -->
      <SelectMenu autofocus v-model="sprintProjectId" :options="projectSelectOptions" @change="onSprintProjectChange" size="sm" class="w-44" />

      <!-- Team & Sprint selectors (sprint mode only) -->
      <template v-if="timeMode === 'sprint'">
        <SelectMenu v-if="sprintTeams.length" v-model="sprintTeam" :options="teamSelectOptions" @change="onSprintTeamChange" size="sm" class="w-44" />
        <SelectMenu v-if="sprintIterations.length" v-model="sprintIterationId" :options="sprintSelectOptions" @change="onSprintIterationChange" size="sm" class="w-52" />
        <span v-if="sprintDateLabel" class="text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">{{ sprintDateLabel }}</span>
      </template>
    </div>

    <!-- PAT warning -->
    <div v-if="!store.patConfigured" class="mb-6 bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-700 rounded-lg px-4 py-3 flex items-center gap-3">
      <svg class="w-5 h-5 text-amber-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
      </svg>
      <p class="text-sm text-amber-800 dark:text-amber-200">PAT not configured. Set it in Settings first.</p>
    </div>

    <!-- Error -->
    <div v-if="error" class="mb-6 bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-700 rounded-lg px-4 py-3 text-sm text-red-700 dark:text-red-300">{{ error }}</div>

    <!-- Empty state -->
    <div v-if="!loading && !data" class="text-center text-gray-400 dark:text-gray-500 py-16">
      <svg class="mx-auto w-12 h-12 mb-3 opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1">
        <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 013 19.875v-6.75zM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V8.625zM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V4.125z" />
      </svg>
      <p class="text-sm">Click <strong>Run Assessment</strong> to analyse developer performance.</p>
    </div>

    <template v-if="data && (data.prs.length || data.work_items.length)">

      <!-- ================================================================= -->
      <!-- Project / Team Buttons -->
      <!-- ================================================================= -->
      <div class="mb-4 flex flex-wrap gap-2">
        <button v-for="proj in projectNames" :key="proj" @click="selectProject(proj)"
          class="px-4 py-2 rounded-lg text-sm font-medium transition-colors border"
          :class="selectedProject === proj
            ? 'bg-primary-600 text-white border-primary-600 shadow-sm'
            : 'bg-white dark:bg-gray-800 text-gray-700 dark:text-gray-300 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700'">
          {{ dProject(proj) }}
          <span class="ml-1 text-xs opacity-75">({{ projectPrCount(proj) }})</span>
        </button>
      </div>

      <!-- ================================================================= -->
      <!-- Selected project content -->
      <!-- ================================================================= -->
      <template v-if="selectedProject">
        <!-- Member filter (foldable) -->
        <div class="mb-4 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm">
          <button @click="memberFilterOpen = !memberFilterOpen"
            class="w-full px-4 py-3 flex items-center justify-between text-left hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors rounded-xl">
            <span class="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Team members
              <span v-if="excludedCount > 0" class="ml-1 text-amber-500">({{ excludedCount }} hidden)</span>
            </span>
            <svg class="w-4 h-4 text-gray-400 transition-transform" :class="{ 'rotate-180': memberFilterOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
            </svg>
          </button>
          <div v-if="memberFilterOpen" class="px-4 pb-3 border-t border-gray-100 dark:border-gray-700 pt-3">
            <div class="flex justify-end mb-2">
              <button @click="toggleAllMembers" class="text-xs text-primary-600 dark:text-primary-400 hover:underline">
                {{ allMembersEnabled ? 'Deselect all' : 'Select all' }}
              </button>
            </div>
            <div class="flex flex-wrap gap-2">
              <label v-for="dev in allProjectDevNames" :key="dev"
                class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs border cursor-pointer select-none transition-colors"
                :class="isDevEnabled(dev)
                  ? 'bg-primary-50 dark:bg-primary-900/30 border-primary-300 dark:border-primary-600 text-primary-700 dark:text-primary-300'
                  : 'bg-gray-50 dark:bg-gray-900 border-gray-200 dark:border-gray-700 text-gray-400 dark:text-gray-500 line-through'">
                <input type="checkbox" :checked="isDevEnabled(dev)" @change="toggleDev(dev)"
                  class="w-3 h-3 rounded border-gray-300 text-primary-600 focus:ring-primary-500" />
                {{ dName(dev) }}
              </label>
            </div>
          </div>
        </div>

        <!-- Dev member buttons -->
        <div class="mb-5">
          <div v-if="projectDevNames.length > 6" class="relative w-64 mb-3">
            <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 pointer-events-none" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
            </svg>
            <input
              v-model="devSearch"
              type="text"
              placeholder="Search developers…"
              class="w-full pl-8 pr-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-200 placeholder-gray-400 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
            />
          </div>
          <div class="flex flex-wrap gap-2">
          <button @click="selectedDev = null"
            class="px-3 py-1.5 rounded-md text-xs font-medium transition-colors border"
            :class="!selectedDev
              ? 'bg-gray-700 dark:bg-gray-200 text-white dark:text-gray-800 border-gray-700 dark:border-gray-200'
              : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700'">
            All Team
          </button>
          <button v-for="dev in filteredDevNames" :key="dev" @click="selectedDev = dev"
            class="px-3 py-1.5 rounded-md text-xs font-medium transition-colors border"
            :class="selectedDev === dev
              ? 'bg-primary-600 text-white border-primary-600'
              : 'bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-400 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700'">
            {{ dName(dev) }}
          </button>
          </div>
        </div>

        <!-- ============================================================= -->
        <!-- TEAM VIEW (no dev selected) -->
        <!-- ============================================================= -->
        <template v-if="!selectedDev">
          <!-- Team stat graphs -->
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6 mb-6">
            <!-- PRs per dev -->
            <div class="chart-card">
              <div class="chart-header">
                <span class="chart-title" title="Total number of completed PRs created by each developer in the selected time window.">PRs per Developer</span>
                <div class="sort-group">
                  <button class="sort-btn" :class="{ active: chartSorts.prs === 'alpha' }" @click="chartSorts.prs = 'alpha'" title="Sort A–Z">A–Z</button>
                  <button class="sort-btn" :class="{ active: chartSorts.prs === 'desc' }" @click="chartSorts.prs = 'desc'" title="Sort descending">↓</button>
                  <button class="sort-btn" :class="{ active: chartSorts.prs === 'asc' }" @click="chartSorts.prs = 'asc'" title="Sort ascending">↑</button>
                </div>
              </div>
              <div class="space-y-3">
                <div v-for="dev in sortedChart('prs', 'prCount')" :key="'prs-' + dev.name" class="flex items-center gap-2">
                  <span class="text-xs text-gray-600 dark:text-gray-400 w-40 truncate shrink-0" :title="dev.displayName">{{ dev.displayName }}</span>
                  <div class="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-6 overflow-hidden">
                    <div class="h-full bg-primary-500 rounded-full flex items-center justify-end pr-2 text-[10px] text-white font-bold transition-all"
                      :style="{ width: barWidth(dev.prCount, maxPrCount) }">
                      {{ dev.prCount }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- PRs Reviewed per dev -->
            <div class="chart-card">
              <div class="chart-header">
                <span class="chart-title" title="Number of PRs reviewed (approved or wait-for-author vote) by each developer.">PRs Reviewed</span>
                <div class="sort-group">
                  <button class="sort-btn" :class="{ active: chartSorts.reviewed === 'alpha' }" @click="chartSorts.reviewed = 'alpha'" title="Sort A–Z">A–Z</button>
                  <button class="sort-btn" :class="{ active: chartSorts.reviewed === 'desc' }" @click="chartSorts.reviewed = 'desc'" title="Sort descending">↓</button>
                  <button class="sort-btn" :class="{ active: chartSorts.reviewed === 'asc' }" @click="chartSorts.reviewed = 'asc'" title="Sort ascending">↑</button>
                </div>
              </div>
              <div class="space-y-3">
                <div v-for="dev in sortedChart('reviewed', 'prsReviewedCount')" :key="'reviewed-' + dev.name" class="flex items-center gap-2">
                  <span class="text-xs text-gray-600 dark:text-gray-400 w-40 truncate shrink-0" :title="dev.displayName">{{ dev.displayName }}</span>
                  <div class="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-6 overflow-hidden">
                    <div class="h-full bg-indigo-500 rounded-full flex items-center justify-end pr-2 text-[10px] text-white font-bold transition-all"
                      :style="{ width: barWidth(dev.prsReviewedCount, maxReviewedCount) }">
                      {{ dev.prsReviewedCount }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Avg Hours per dev -->
            <div class="chart-card">
              <div class="chart-header">
                <span class="chart-title" title="Average business hours (Mon–Fri, 9–17) from PR creation to completion per developer.">Avg Hours to Complete</span>
                <div class="sort-group">
                  <button class="sort-btn" :class="{ active: chartSorts.hours === 'alpha' }" @click="chartSorts.hours = 'alpha'" title="Sort A–Z">A–Z</button>
                  <button class="sort-btn" :class="{ active: chartSorts.hours === 'desc' }" @click="chartSorts.hours = 'desc'" title="Sort descending">↓</button>
                  <button class="sort-btn" :class="{ active: chartSorts.hours === 'asc' }" @click="chartSorts.hours = 'asc'" title="Sort ascending">↑</button>
                </div>
              </div>
              <div class="space-y-3">
                <div v-for="dev in sortedChart('hours', 'avgHours')" :key="'hours-' + dev.name" class="flex items-center gap-2">
                  <span class="text-xs text-gray-600 dark:text-gray-400 w-40 truncate shrink-0" :title="dev.displayName">{{ dev.displayName }}</span>
                  <div class="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-6 overflow-hidden">
                    <div class="h-full rounded-full flex items-center justify-end pr-2 text-[10px] text-white font-bold transition-all"
                      :class="barColor(parseFloat(dev.avgHours), 24, 72)"
                      :style="{ width: barWidth(parseFloat(dev.avgHours) || 0, maxAvgHours) }">
                      {{ dev.avgHours }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Avg Iterations per dev -->
            <div class="chart-card">
              <div class="chart-header">
                <span class="chart-title" title="Average review iterations per PR. Counts ‘Wait for author’ votes plus final approval from PR threads.">Avg Iterations</span>
                <div class="sort-group">
                  <button class="sort-btn" :class="{ active: chartSorts.iters === 'alpha' }" @click="chartSorts.iters = 'alpha'" title="Sort A–Z">A–Z</button>
                  <button class="sort-btn" :class="{ active: chartSorts.iters === 'desc' }" @click="chartSorts.iters = 'desc'" title="Sort descending">↓</button>
                  <button class="sort-btn" :class="{ active: chartSorts.iters === 'asc' }" @click="chartSorts.iters = 'asc'" title="Sort ascending">↑</button>
                </div>
              </div>
              <div class="space-y-3">
                <div v-for="dev in sortedChart('iters', 'avgIters')" :key="'iter-' + dev.name" class="flex items-center gap-2">
                  <span class="text-xs text-gray-600 dark:text-gray-400 w-40 truncate shrink-0" :title="dev.displayName">{{ dev.displayName }}</span>
                  <div class="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-6 overflow-hidden">
                    <div class="h-full rounded-full flex items-center justify-end pr-2 text-[10px] text-white font-bold transition-all"
                      :class="iterBarColor(parseFloat(dev.avgIters))"
                      :style="{ width: barWidth(parseFloat(dev.avgIters) || 0, maxAvgIters) }">
                      {{ dev.avgIters }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Avg First Review per dev -->
            <div class="chart-card">
              <div class="chart-header">
                <span class="chart-title" title="Average business hours (Mon–Fri, 9–17) from PR creation to the first non-author comment or review.">Avg First Review (h)</span>
                <div class="sort-group">
                  <button class="sort-btn" :class="{ active: chartSorts.review === 'alpha' }" @click="chartSorts.review = 'alpha'" title="Sort A–Z">A–Z</button>
                  <button class="sort-btn" :class="{ active: chartSorts.review === 'desc' }" @click="chartSorts.review = 'desc'" title="Sort descending">↓</button>
                  <button class="sort-btn" :class="{ active: chartSorts.review === 'asc' }" @click="chartSorts.review = 'asc'" title="Sort ascending">↑</button>
                </div>
              </div>
              <div class="space-y-3">
                <div v-for="dev in sortedChart('review', 'avgFirstReview')" :key="'review-' + dev.name" class="flex items-center gap-2">
                  <span class="text-xs text-gray-600 dark:text-gray-400 w-40 truncate shrink-0" :title="dev.displayName">{{ dev.displayName }}</span>
                  <div class="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-6 overflow-hidden">
                    <div class="h-full rounded-full flex items-center justify-end pr-2 text-[10px] text-white font-bold transition-all"
                      :class="barColor(parseFloat(dev.avgFirstReview), 8, 24)"
                      :style="{ width: barWidth(parseFloat(dev.avgFirstReview) || 0, maxFirstReview) }">
                      {{ dev.avgFirstReview }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- Est/Actual Ratio per dev -->
            <div class="chart-card">
              <div class="chart-header">
                <span class="chart-title" title="Ratio of actual completed work hours to original estimate across work items. 1.0x = on target, >1x = over estimate.">Estimate vs Actual Ratio</span>
                <div class="sort-group">
                  <button class="sort-btn" :class="{ active: chartSorts.est === 'alpha' }" @click="chartSorts.est = 'alpha'" title="Sort A–Z">A–Z</button>
                  <button class="sort-btn" :class="{ active: chartSorts.est === 'desc' }" @click="chartSorts.est = 'desc'" title="Sort descending">↓</button>
                  <button class="sort-btn" :class="{ active: chartSorts.est === 'asc' }" @click="chartSorts.est = 'asc'" title="Sort ascending">↑</button>
                </div>
              </div>
              <div class="space-y-3">
                <div v-for="dev in sortedChart('est', 'estActualRatio')" :key="'est-' + dev.name" class="flex items-center gap-2">
                  <span class="text-xs text-gray-600 dark:text-gray-400 w-40 truncate shrink-0" :title="dev.displayName">{{ dev.displayName }}</span>
                  <div class="flex-1 bg-gray-100 dark:bg-gray-700 rounded-full h-6 overflow-hidden">
                    <div class="h-full rounded-full flex items-center justify-end pr-2 text-[10px] text-white font-bold transition-all"
                      :class="ratioBarColor(dev.estActualRatio)"
                      :style="{ width: barWidth(parseFloat(dev.estActualRatio) || 0, maxEstRatio) }">
                      {{ dev.estActualRatio === '—' ? '—' : dev.estActualRatio + 'x' }}
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Developer summary table -->
          <div class="bg-white dark:bg-gray-800 box-rounded border border-gray-200 dark:border-gray-700 shadow-sm mb-6 overflow-hidden">
            <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700">
              <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider">Team Overview — {{ selectedProject }}</h3>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
                <thead>
                  <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider bg-gray-50 dark:bg-gray-700/30">
                    <th class="px-4 py-2.5">Developer</th>
                    <th class="px-4 py-2.5 text-right">PRs</th>
                    <th class="px-4 py-2.5 text-right">Reviewed</th>
                    <th class="px-4 py-2.5 text-right">Avg Files</th>
                    <th class="px-4 py-2.5 text-right">Avg Hours</th>
                    <th class="px-4 py-2.5 text-right">Avg 1st Review (h)</th>
                    <th class="px-4 py-2.5 text-right">Avg Iters</th>
                    <th class="px-4 py-2.5 text-right">Unresolved %</th>
                    <th class="px-4 py-2.5 text-right">WI Count</th>
                    <th class="px-4 py-2.5 text-right">Avg Cycle (h)</th>
                    <th class="px-4 py-2.5 text-right">WIP</th>
                    <th class="px-4 py-2.5 text-right">Est/Actual</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                  <tr v-for="dev in currentDevs" :key="dev.name"
                    class="hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors cursor-pointer"
                    @click="selectedDev = dev.name">
                    <td class="px-4 py-2.5 font-medium text-primary-600 dark:text-primary-400 hover:underline">{{ dev.displayName }}</td>
                    <td class="px-4 py-2.5 text-right text-gray-700 dark:text-gray-300">{{ dev.prCount }}</td>
                    <td class="px-4 py-2.5 text-right text-gray-700 dark:text-gray-300">{{ dev.prsReviewedCount }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.avgFiles, 10, 20)">{{ dev.avgFiles }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.avgHours, 24, 72)">{{ dev.avgHours }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.avgFirstReview, 8, 24)">{{ dev.avgFirstReview }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.avgIters, 3, 6)">{{ dev.avgIters }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.unresolvedPct, 10, 25)">{{ dev.unresolvedPct }}%</td>
                    <td class="px-4 py-2.5 text-right text-gray-700 dark:text-gray-300">{{ dev.wiCount }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.avgCycleTime, 120, 336)">{{ dev.avgCycleTime }}</td>
                    <td class="px-4 py-2.5 text-right" :class="metricColor(dev.wip, 5, 10)">
                      <span v-if="dev.wip > 0" class="cursor-pointer hover:underline" @click.stop="selectedDev = dev.name; devPanel = 'wip'">{{ dev.wip }}</span>
                      <span v-else>{{ dev.wip }}</span>
                    </td>
                    <td class="px-4 py-2.5 text-right" :class="ratioColor(dev.estActualRatio)">{{ dev.estActualRatio === '—' ? '—' : dev.estActualRatio + 'x' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </template>

        <!-- ============================================================= -->
        <!-- INDIVIDUAL DEV VIEW -->
        <!-- ============================================================= -->
        <template v-if="selectedDev && activeDev">
          <!-- Personal stat cards - PR row -->
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-4 mb-6">
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'prs' }" title="Total completed PRs. Click to show PR list." @click="devPanel = devPanel === 'prs' ? null : 'prs'"><p class="stat-val stat-val-default">{{ activeDev.prCount }}</p><p class="stat-label">PRs</p></div>
            <div class="card-stat" title="PRs reviewed (approved or wait-for-author)."><p class="stat-val stat-val-default">{{ activeDev.prsReviewedCount }}</p><p class="stat-label">Reviewed</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'prs' }" title="Avg business hours from PR published to completion." @click="devPanel = devPanel === 'prs' ? null : 'prs'"><p class="stat-val" :class="metricColor(activeDev.avgHours, 24, 72)">{{ activeDev.avgHours }}</p><p class="stat-label">Avg Hours</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'prs' }" title="Avg review iterations per PR." @click="devPanel = devPanel === 'prs' ? null : 'prs'"><p class="stat-val" :class="iterMetricColor(activeDev.avgIters)">{{ activeDev.avgIters }}</p><p class="stat-label">Avg Iterations</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'prs' }" title="Avg business hours to first review." @click="devPanel = devPanel === 'prs' ? null : 'prs'"><p class="stat-val" :class="metricColor(activeDev.avgFirstReview, 8, 24)">{{ activeDev.avgFirstReview }}</p><p class="stat-label">Avg 1st Review (h)</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'prs' }" title="Avg files changed per PR." @click="devPanel = devPanel === 'prs' ? null : 'prs'"><p class="stat-val" :class="metricColor(activeDev.avgFiles, 10, 20)">{{ activeDev.avgFiles }}</p><p class="stat-label">Avg Files</p></div>
          </div>

          <!-- Work Item Flow stats -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'wi' }" title="Total work items assigned to this developer. Click to show details." @click="devPanel = devPanel === 'wi' ? null : 'wi'"><p class="stat-val stat-val-default">{{ activeDev.wiCount }}</p><p class="stat-label">Work Items</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'wi' }" title="Avg business hours from work start to closure. Click to show work items." @click="devPanel = devPanel === 'wi' ? null : 'wi'"><p class="stat-val" :class="metricColor(activeDev.avgCycleTime, 120, 336)">{{ activeDev.avgCycleTime }}</p><p class="stat-label">Avg Cycle Time (h)</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'est' }" title="Ratio of completed vs estimated hours. Click to show details." @click="devPanel = devPanel === 'est' ? null : 'est'"><p class="stat-val" :class="ratioColor(activeDev.estActualRatio)">{{ activeDev.estActualRatio === '\u2014' ? '\u2014' : activeDev.estActualRatio + 'x' }}</p><p class="stat-label">Est/Actual</p></div>
            <div class="card-stat card-stat-clickable" :class="{ 'card-stat-active': devPanel === 'wip' }" title="Work items currently in progress or resolved. Click to show details." @click="devPanel = devPanel === 'wip' ? null : 'wip'"><p class="stat-val" :class="metricColor(activeDev.wip, 5, 10)">{{ activeDev.wip }}</p><p class="stat-label">WIP Items</p></div>
          </div>

          <!-- PR overview panel -->
          <div v-if="devPanel === 'prs'" class="bg-white dark:bg-gray-800 box-rounded border border-gray-200 dark:border-gray-700 shadow-sm mb-6 overflow-hidden">
            <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700">
              <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider">Pull Requests ({{ activeDev.prs.length }})</h3>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
                <thead>
                  <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase bg-gray-50 dark:bg-gray-700/30">
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('prs', 'pr_id')">PR{{ sortIcon('prs', 'pr_id') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('prs', 'title')">Title{{ sortIcon('prs', 'title') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('prs', 'repository')">Repo{{ sortIcon('prs', 'repository') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('prs', 'hours_to_complete')">Hours{{ sortIcon('prs', 'hours_to_complete') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('prs', 'iterations')">Iters{{ sortIcon('prs', 'iterations') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('prs', 'files_changed')">Files{{ sortIcon('prs', 'files_changed') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('prs', 'hours_to_first_review')">1st Review (h){{ sortIcon('prs', 'hours_to_first_review') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('prs', 'unresolved_thread_count')">Unresolved{{ sortIcon('prs', 'unresolved_thread_count') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                  <tr v-for="pr in sorted(activeDev.prs, 'prs')" :key="pr.pr_id" class="hover:bg-gray-50 dark:hover:bg-gray-700/40">
                    <td class="px-4 py-2 text-gray-500 dark:text-gray-400">#{{ pr.pr_id }}</td>
                    <td class="px-4 py-2 font-medium text-gray-800 dark:text-gray-200 max-w-xs truncate" :title="dTitle(pr.title)">{{ dTitle(pr.title) }}</td>
                    <td class="px-4 py-2 text-gray-500 dark:text-gray-400 text-xs">{{ dRepo(pr.repository) }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="hoursColor(pr.hours_to_complete)">{{ pr.hours_to_complete ?? '\u2014' }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="iterColor(pr.iterations)">{{ pr.iterations }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="metricColor(pr.files_changed, 10, 20)">{{ pr.files_changed }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="hoursColor24(pr.hours_to_first_review)">{{ pr.hours_to_first_review ?? '\u2014' }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="pr.unresolved_thread_count > 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-400'">{{ pr.unresolved_thread_count }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Est vs Completed panel -->
          <div v-if="devPanel === 'est'" class="bg-white dark:bg-gray-800 box-rounded border border-gray-200 dark:border-gray-700 shadow-sm mb-6 overflow-hidden">
            <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700">
              <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider">Estimated vs Completed ({{ activeDev.estItems.length }})</h3>
            </div>
            <div v-if="activeDev.estItems.length" class="overflow-x-auto">
              <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
                <thead>
                  <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase bg-gray-50 dark:bg-gray-700/30">
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('est', 'id')">ID{{ sortIcon('est', 'id') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('est', 'title')">Title{{ sortIcon('est', 'title') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('est', 'original_estimate')">Estimated{{ sortIcon('est', 'original_estimate') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('est', 'completed_work')">Completed{{ sortIcon('est', 'completed_work') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('est', '_ratio')">Ratio{{ sortIcon('est', '_ratio') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                  <tr v-for="wi in sorted(activeDev.estItems, 'est')" :key="wi.id" class="hover:bg-gray-50 dark:hover:bg-gray-700/40">
                    <td class="px-4 py-2"><a :href="wiUrl(wi)" target="_blank" class="text-primary-600 dark:text-primary-400 hover:underline">#{{ wi.id }}</a></td>
                    <td class="px-4 py-2 font-medium text-gray-800 dark:text-gray-200 max-w-xs truncate">{{ dTitle(wi.title) }}</td>
                    <td class="px-4 py-2 text-right font-mono text-gray-700 dark:text-gray-300">{{ wi.original_estimate }}h</td>
                    <td class="px-4 py-2 text-right font-mono text-gray-700 dark:text-gray-300">{{ wi.completed_work }}h</td>
                    <td class="px-4 py-2 text-right font-mono" :class="ratioColor((wi.completed_work / wi.original_estimate).toFixed(2))">
                      {{ (wi.completed_work / wi.original_estimate).toFixed(2) }}x
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div v-else class="px-5 py-4 text-sm text-gray-400 italic">No tickets with both estimate and completed hours.</div>
          </div>

          <!-- WIP Items panel -->
          <div v-if="devPanel === 'wip'" class="bg-white dark:bg-gray-800 box-rounded border border-gray-200 dark:border-gray-700 shadow-sm mb-6 overflow-hidden">
            <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700">
              <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider">WIP Items ({{ activeDev.wipItems.length }})</h3>
            </div>
            <div v-if="activeDev.wipItems.length" class="overflow-x-auto">
              <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
                <thead>
                  <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase bg-gray-50 dark:bg-gray-700/30">
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wip', 'id')">ID{{ sortIcon('wip', 'id') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wip', 'title')">Title{{ sortIcon('wip', 'title') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wip', 'work_item_type')">Type{{ sortIcon('wip', 'work_item_type') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wip', 'state')">State{{ sortIcon('wip', 'state') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wip', 'active_age_hours')">Active (h){{ sortIcon('wip', 'active_age_hours') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wip', 'original_estimate')">Est{{ sortIcon('wip', 'original_estimate') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wip', 'remaining_work')">Remaining{{ sortIcon('wip', 'remaining_work') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                  <tr v-for="wi in sorted(activeDev.wipItems, 'wip')" :key="wi.id" class="hover:bg-gray-50 dark:hover:bg-gray-700/40">
                    <td class="px-4 py-2"><a :href="wiUrl(wi)" target="_blank" class="text-primary-600 dark:text-primary-400 hover:underline">#{{ wi.id }}</a></td>
                    <td class="px-4 py-2 font-medium text-gray-800 dark:text-gray-200 max-w-xs truncate">{{ dTitle(wi.title) }}</td>
                    <td class="px-4 py-2 text-gray-500 dark:text-gray-400">{{ wi.work_item_type }}</td>
                    <td class="px-4 py-2">
                      <span class="px-1.5 py-0.5 rounded text-[10px] font-medium"
                        :class="wi.state === 'Active' || wi.state === 'In Progress' ? 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-400'
                          : wi.state === 'Resolved' ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400'
                          : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400'">{{ wi.state }}</span>
                    </td>
                    <td class="px-4 py-2 text-right font-mono" :class="wi.active_age_hours > 336 ? 'text-red-600 dark:text-red-400' : 'text-gray-700 dark:text-gray-300'">{{ Math.round(wi.active_age_hours) }}</td>
                    <td class="px-4 py-2 text-right font-mono text-gray-500 dark:text-gray-400">{{ wi.original_estimate ? wi.original_estimate + 'h' : '\u2014' }}</td>
                    <td class="px-4 py-2 text-right font-mono text-gray-500 dark:text-gray-400">{{ wi.remaining_work ? wi.remaining_work + 'h' : '\u2014' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div v-else class="px-5 py-4 text-sm text-gray-400 italic">No WIP items.</div>
          </div>

          <!-- Work Items panel -->
          <div v-if="devPanel === 'wi'" class="bg-white dark:bg-gray-800 box-rounded border border-gray-200 dark:border-gray-700 shadow-sm mb-6 overflow-hidden">
            <div class="px-5 py-3 border-b border-gray-200 dark:border-gray-700">
              <h3 class="text-sm font-semibold text-primary-500 dark:text-gray-300 uppercase tracking-wider">Work Items ({{ activeDev.workItems.length }})</h3>
            </div>
            <div v-if="activeDev.workItems.length" class="overflow-x-auto">
              <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
                <thead>
                  <tr class="text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase bg-gray-50 dark:bg-gray-700/30">
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wi', 'id')">ID{{ sortIcon('wi', 'id') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wi', 'title')">Title{{ sortIcon('wi', 'title') }}</th>
                    <th class="px-4 py-2 th-sort" @click="toggleTableSort('wi', 'state')">State{{ sortIcon('wi', 'state') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wi', 'cycle_time_hours')">Cycle (h){{ sortIcon('wi', 'cycle_time_hours') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wi', 'state_changes')">State Chgs{{ sortIcon('wi', 'state_changes') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wi', 'reopen_count')">Reopens{{ sortIcon('wi', 'reopen_count') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wi', 'original_estimate')">Est{{ sortIcon('wi', 'original_estimate') }}</th>
                    <th class="px-4 py-2 text-right th-sort" @click="toggleTableSort('wi', 'completed_work')">Actual{{ sortIcon('wi', 'completed_work') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 dark:divide-gray-700">
                  <tr v-for="wi in sorted(activeDev.workItems, 'wi')" :key="wi.id" class="hover:bg-gray-50 dark:hover:bg-gray-700/40">
                    <td class="px-4 py-2"><a :href="wiUrl(wi)" target="_blank" class="text-primary-600 dark:text-primary-400 hover:underline">#{{ wi.id }}</a></td>
                    <td class="px-4 py-2 font-medium text-gray-800 dark:text-gray-200 max-w-xs truncate">{{ dTitle(wi.title) }}</td>
                    <td class="px-4 py-2">
                      <span class="px-1.5 py-0.5 rounded text-[10px] font-medium"
                        :class="wi.state === 'Closed' || wi.state === 'Done' ? 'bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-400'
                          : wi.state === 'Active' || wi.state === 'In Progress' ? 'bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-400'
                          : wi.state === 'Resolved' ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400'
                          : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400'">{{ wi.state }}</span>
                    </td>
                    <td class="px-4 py-2 text-right font-mono" :class="wi.cycle_time_hours > 336 ? 'text-red-600 dark:text-red-400' : 'text-gray-700 dark:text-gray-300'">{{ wi.cycle_time_hours ?? '—' }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="wi.state_changes > 5 ? 'text-amber-600 dark:text-amber-400' : 'text-gray-700 dark:text-gray-300'">{{ wi.state_changes }}</td>
                    <td class="px-4 py-2 text-right font-mono" :class="wi.reopen_count > 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-700 dark:text-gray-300'">{{ wi.reopen_count }}</td>
                    <td class="px-4 py-2 text-right font-mono text-gray-500 dark:text-gray-400">{{ wi.original_estimate ? wi.original_estimate + 'h' : '—' }}</td>
                    <td class="px-4 py-2 text-right font-mono text-gray-500 dark:text-gray-400">{{ wi.completed_work ? wi.completed_work + 'h' : '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div v-else class="px-5 py-4 text-sm text-gray-400 italic">No work items found for this developer.</div>
          </div>
        </template>
      </template>

    </template>

    <!-- No results after run -->
    <div v-if="data && !data.prs.length && !data.work_items.length && !loading" class="text-center text-gray-400 dark:text-gray-500 py-16">
      <p class="text-sm">No data found in the last {{ months }} month{{ months > 1 ? 's' : '' }}.</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, reactive, watch, onMounted } from 'vue'
import { useApi } from '../composables/useApi.js'
import { useMonitorStore } from '../stores/monitor.js'
import { transformDevAssessment } from '../composables/demoTransform.js'
import { useCsvExport } from '../composables/useCsvExport.js'
import { useDemoMode, anonName, anonProject, anonPrTitle, anonRepo, anonOrg, anonTeam, anonSprint } from '../composables/useDemoMode.js'
import DataFreshness from '../components/DataFreshness.vue'
import SelectMenu from '../components/SelectMenu.vue'

const { isDemoMode } = useDemoMode()
function dName(v) { return isDemoMode.value ? anonName(v) : v }
function dProject(v) { return isDemoMode.value ? anonProject(v) : v }
function dTitle(v) { return isDemoMode.value ? anonPrTitle(v) : v }
function dRepo(v) { return isDemoMode.value ? anonRepo(v) : v }
function dOrg(v) { return isDemoMode.value ? anonOrg(v) : v }
function dTeam(v) { return isDemoMode.value ? anonTeam(v) : v }
function dSprint(v) { return isDemoMode.value ? anonSprint(v) : v }

const api = useApi()
const store = useMonitorStore()
const { exportCsv } = useCsvExport()

// --- Persistence keys ---
const LS_EXCLUDED_DEVS = 'da_excludedDevs'

// --- Project list ---
const availableProjects = ref([])

async function loadProjects() {
  try {
    availableProjects.value = await api.get('/api/projects')
  } catch { /* ignore */ }
}

const monthOptions = [
  { value: 1, label: 'Last 1 month' },
  { value: 3, label: 'Last 3 months' },
  { value: 6, label: 'Last 6 months' },
  { value: 12, label: 'Last 12 months' },
]
const projectSelectOptions = computed(() => [
  { value: '', label: 'All projects' },
  ...availableProjects.value.map(p => ({ value: p.id, label: dProject(p.project) }))
])
const teamSelectOptions = computed(() => [
  { value: '', label: 'Team…' },
  ...sprintTeams.value.map(t => ({ value: t.name, label: dTeam(t.name) }))
])
const sprintSelectOptions = computed(() => [
  { value: '', label: 'Sprint…' },
  ...sprintIterations.value.map(it => ({ value: it.id, label: dSprint(it.name) }))
])

// --- Member selection (persistent) ---
const excludedDevs = reactive({}) // { projectName: Set<devName> }

function loadSavedExcludedDevs() {
  const saved = localStorage.getItem(LS_EXCLUDED_DEVS)
  if (!saved) return
  try {
    const obj = JSON.parse(saved)
    for (const [proj, devs] of Object.entries(obj)) {
      excludedDevs[proj] = new Set(devs)
    }
  } catch { /* ignore */ }
}

function saveExcludedDevs() {
  const obj = {}
  for (const [proj, devSet] of Object.entries(excludedDevs)) {
    if (devSet.size > 0) obj[proj] = [...devSet]
  }
  localStorage.setItem(LS_EXCLUDED_DEVS, JSON.stringify(obj))
}

function isDevEnabled(dev) {
  const excl = excludedDevs[selectedProject.value]
  return !excl || !excl.has(dev)
}

function toggleDev(dev) {
  const proj = selectedProject.value
  if (!excludedDevs[proj]) excludedDevs[proj] = new Set()
  if (excludedDevs[proj].has(dev)) excludedDevs[proj].delete(dev)
  else excludedDevs[proj].add(dev)
  saveExcludedDevs()
  // If the currently selected dev was just excluded, deselect
  if (selectedDev.value === dev && excludedDevs[proj].has(dev)) {
    selectedDev.value = null
  }
}

function toggleAllMembers() {
  const proj = selectedProject.value
  if (allMembersEnabled.value) {
    excludedDevs[proj] = new Set(allProjectDevNames.value)
  } else {
    excludedDevs[proj] = new Set()
  }
  saveExcludedDevs()
  if (selectedDev.value && !isDevEnabled(selectedDev.value)) selectedDev.value = null
}

onMounted(() => { loadProjects(); loadSavedExcludedDevs() })

const loading = ref(false)
const error = ref('')
const rawData = ref(store.devAssessmentData)
const lastFetchedAt = ref(null)
const data = computed(() => rawData.value)
const months = ref(store.devAssessmentMonths || 3)
const selectedProject = ref(store.devAssessmentSelectedProject)
const selectedDev = ref(store.devAssessmentSelectedDev)
const memberFilterOpen = ref(false)
const devPanel = ref(null)
const chartSorts = reactive({ prs: 'desc', reviewed: 'desc', hours: 'asc', iters: 'asc', review: 'asc', est: 'asc' })
const tableSort = reactive({ prs: { key: null, dir: 'asc' }, est: { key: null, dir: 'asc' }, wip: { key: null, dir: 'asc' }, wi: { key: null, dir: 'asc' } })

// --- Sprint time-box state ---
const timeMode = ref('months') // 'months' | 'sprint'
const sprintProjectId = ref('')
const sprintTeams = ref([])
const sprintTeam = ref('')
const sprintIterations = ref([])
const sprintIterationId = ref('')
const sprintSince = ref('')
const sprintUntil = ref('')

const sprintDateLabel = computed(() => {
  if (!sprintSince.value || !sprintUntil.value) return ''
  const fmt = d => new Date(d).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
  return `${fmt(sprintSince.value)} – ${fmt(sprintUntil.value)}`
})

async function onSprintProjectChange() {
  sprintTeam.value = ''
  sprintTeams.value = []
  sprintIterationId.value = ''
  sprintIterations.value = []
  sprintSince.value = ''
  sprintUntil.value = ''
  if (!sprintProjectId.value) return
  sprintTeams.value = await store.fetchVelocityTeams(sprintProjectId.value)
  if (sprintTeams.value.length === 1) {
    sprintTeam.value = sprintTeams.value[0].name
    await onSprintTeamChange()
  }
}

async function onSprintTeamChange() {
  sprintIterationId.value = ''
  sprintIterations.value = []
  sprintSince.value = ''
  sprintUntil.value = ''
  if (!sprintTeam.value) return
  sprintIterations.value = await store.fetchVelocityIterations(sprintProjectId.value, sprintTeam.value)
  // Auto-select current sprint
  const current = sprintIterations.value.find(i => i.timeframe === 'current')
  if (current) {
    sprintIterationId.value = current.id
    onSprintIterationChange()
  }
}

function onSprintIterationChange() {
  const it = sprintIterations.value.find(i => i.id === sprintIterationId.value)
  if (it?.start_date && it?.finish_date) {
    sprintSince.value = it.start_date
    sprintUntil.value = it.finish_date
  } else {
    sprintSince.value = ''
    sprintUntil.value = ''
  }
}

function toggleTableSort(table, key) {
  const s = tableSort[table]
  if (s.key === key) { s.dir = s.dir === 'asc' ? 'desc' : 'asc' }
  else { s.key = key; s.dir = 'asc' }
}

function sortIcon(table, key) {
  const s = tableSort[table]
  return s.key === key ? (s.dir === 'asc' ? ' \u25B2' : ' \u25BC') : ''
}

function sorted(items, table) {
  const s = tableSort[table]
  if (!s.key) return items
  return [...items].sort((a, b) => {
    let va, vb
    if (s.key === '_ratio') {
      va = a.original_estimate ? a.completed_work / a.original_estimate : null
      vb = b.original_estimate ? b.completed_work / b.original_estimate : null
    } else { va = a[s.key]; vb = b[s.key] }
    if (va == null) return 1
    if (vb == null) return -1
    if (typeof va === 'string') return s.dir === 'asc' ? va.localeCompare(vb) : vb.localeCompare(va)
    return s.dir === 'asc' ? va - vb : vb - va
  })
}

const runDisabled = computed(() =>
  timeMode.value === 'sprint' && (!sprintProjectId.value || !sprintTeam.value || !sprintIterationId.value)
)

async function runAssessment() {
  loading.value = true
  error.value = ''
  selectedProject.value = null
  selectedDev.value = null
  try {
    const body = { months: months.value }
    if (sprintProjectId.value) {
      body.project_ids = [sprintProjectId.value]
    }
    if (timeMode.value === 'sprint') {
      body.since = sprintSince.value
      body.until = sprintUntil.value
    }
    rawData.value = await api.post('/api/dev-assessment/run', body)
    store.devAssessmentData = rawData.value
    store.devAssessmentMonths = months.value
    lastFetchedAt.value = new Date().toISOString()
    if (projectNames.value.length) selectedProject.value = projectNames.value[0]
  } catch (e) {
    error.value = e.message || 'Assessment failed'
  } finally {
    loading.value = false
  }
}

function selectProject(proj) {
  selectedProject.value = proj
  selectedDev.value = null
  devPanel.value = null
}

watch(selectedProject, v => { store.devAssessmentSelectedProject = v })
watch(selectedDev, v => { store.devAssessmentSelectedDev = v })

// ================================================================
// Project names
// ================================================================

const projectNames = computed(() => {
  if (!data.value) return []
  const set = new Set()
  for (const pr of data.value.prs) set.add(pr.project)
  for (const wi of data.value.work_items) set.add(wi.project)
  return [...set].sort()
})

function projectPrCount(proj) {
  return data.value?.prs.filter(p => p.project === proj).length ?? 0
}

// ================================================================
// Devs for selected project (from work item assignees)
// ================================================================

// All devs discovered in the data (unfiltered) — used for the member checkboxes
const allProjectDevNames = computed(() => {
  if (!data.value || !selectedProject.value) return []
  const set = new Set()
  for (const wi of data.value.work_items) {
    if (wi.project === selectedProject.value) {
      const name = stripOrg(wi.assigned_to || '')
      if (name && name !== 'Unassigned') set.add(name)
    }
  }
  return [...set].sort()
})

const allMembersEnabled = computed(() => {
  const excl = excludedDevs[selectedProject.value]
  return !excl || allProjectDevNames.value.every(d => !excl.has(d))
})

const excludedCount = computed(() => {
  const excl = excludedDevs[selectedProject.value]
  if (!excl) return 0
  return allProjectDevNames.value.filter(d => excl.has(d)).length
})

// Filtered devs — only enabled members
const projectDevNames = computed(() => {
  return allProjectDevNames.value.filter(d => isDevEnabled(d))
})

const devSearch = ref('')
const filteredDevNames = computed(() => {
  const q = devSearch.value.trim().toLowerCase()
  if (!q) return projectDevNames.value
  return projectDevNames.value.filter(d => d.toLowerCase().includes(q))
})

// ================================================================
// Per-developer aggregation for the selected project
// ================================================================

const currentDevs = computed(() => {
  if (!data.value || !selectedProject.value) return []
  const devNames = new Set(projectDevNames.value)
  return devsForProject(selectedProject.value).filter(d => devNames.has(d.name))
})

const activeDev = computed(() => {
  if (!selectedDev.value) return null
  return currentDevs.value.find(d => d.name === selectedDev.value) || null
})

function stripOrg(name) {
  return name.replace(/\s+Fen[eê]tre\s+bv$/i, '').trim()
}

function wiUrl(wi) {
  return `https://dev.azure.com/${encodeURIComponent(wi.organization)}/${encodeURIComponent(wi.project)}/_workitems/edit/${wi.id}`
}

function devsForProject(proj) {
  const prs = data.value.prs.filter(p => p.project === proj)
  const wis = data.value.work_items.filter(w => w.project === proj)
  const openPrs = (data.value.open_prs || []).filter(p => p.project === proj)

  const devMap = {}
  for (const pr of prs) {
    const key = stripOrg(pr.created_by || 'Unknown')
    if (!devMap[key]) devMap[key] = { name: key, prs: [], workItems: [], openPrs: [] }
    devMap[key].prs.push(pr)
  }
  for (const wi of wis) {
    const key = stripOrg(wi.assigned_to || 'Unassigned')
    if (!devMap[key]) devMap[key] = { name: key, prs: [], workItems: [], openPrs: [] }
    devMap[key].workItems.push(wi)
  }
  for (const op of openPrs) {
    const key = stripOrg(op.created_by || 'Unknown')
    if (!devMap[key]) devMap[key] = { name: key, prs: [], workItems: [], openPrs: [] }
    devMap[key].openPrs.push(op)
  }

  return Object.values(devMap).map(d => {
    const prCount = d.prs.length
    const avgFiles = avg(d.prs, 'files_changed')
    const avgHours = avg(d.prs, 'hours_to_complete')
    const avgFirstReview = avg(d.prs, 'hours_to_first_review')
    const avgIters = avg(d.prs, 'iterations')
    const unresolvedPct = d.openPrs.length ? Math.round(d.openPrs.filter(p => p.unresolved_thread_count > 0).length / d.openPrs.length * 100) : 0
    const wiCount = d.workItems.length
    const closedWis = d.workItems.filter(w => w.cycle_time_hours != null)
    const avgCycleTime = closedWis.length ? (closedWis.reduce((s, w) => s + w.cycle_time_hours, 0) / closedWis.length).toFixed(0) : '—'
    const wipItems = d.workItems.filter(w => w.active_age_hours != null && (w.work_item_type === 'Task' || w.work_item_type === 'Bug') && (w.state === 'In Progress' || w.state === 'Active' || w.state === 'Resolved'))
    const wip = wipItems.length
    const estItems = d.workItems.filter(w => w.original_estimate && w.completed_work && (w.state === 'Closed' || w.state === 'Done'))
    const estActualRatio = estItems.length
      ? (estItems.reduce((s, w) => s + w.completed_work, 0) / estItems.reduce((s, w) => s + w.original_estimate, 0)).toFixed(2)
      : '—'

    // PRs reviewed: count PRs in this project where this dev voted approve (10/5) or wait-for-author (-5)
    const allProjectPrs = data.value.prs.filter(p => p.project === proj)
    const prsReviewedCount = allProjectPrs.filter(pr =>
      (pr.reviewers || []).some(r => stripOrg(r.name) === d.name && (r.vote === 10 || r.vote === 5 || r.vote === -5))
    ).length

    return { ...d, displayName: dName(d.name), prCount, prsReviewedCount, avgFiles, avgHours, avgFirstReview, avgIters, unresolvedPct, wiCount, avgCycleTime, wip, wipItems, estActualRatio, estItems }
  }).sort((a, b) => a.name.localeCompare(b.name))
}

function avg(items, field) {
  const valid = items.filter(i => i[field] != null)
  if (!valid.length) return '—'
  return (valid.reduce((s, i) => s + i[field], 0) / valid.length).toFixed(1)
}

// ================================================================
// Chart sorting
// ================================================================

function sortedChart(chart, field) {
  const devs = [...currentDevs.value]
  const mode = chartSorts[chart]
  if (mode === 'desc') return devs.sort((a, b) => (parseFloat(b[field]) || 0) - (parseFloat(a[field]) || 0))
  if (mode === 'asc') return devs.sort((a, b) => (parseFloat(a[field]) || 0) - (parseFloat(b[field]) || 0))
  return devs
}

// ================================================================
// Bar chart helpers
// ================================================================

const maxPrCount = computed(() => Math.max(...currentDevs.value.map(d => d.prCount), 1))
const maxReviewedCount = computed(() => Math.max(...currentDevs.value.map(d => d.prsReviewedCount), 1))
const maxAvgHours = computed(() => Math.max(...currentDevs.value.map(d => parseFloat(d.avgHours) || 0), 1))
const maxAvgIters = computed(() => Math.max(...currentDevs.value.map(d => parseFloat(d.avgIters) || 0), 1))
const maxFirstReview = computed(() => Math.max(...currentDevs.value.map(d => parseFloat(d.avgFirstReview) || 0), 1))
const maxEstRatio = computed(() => Math.max(...currentDevs.value.map(d => parseFloat(d.estActualRatio) || 0), 2))

function barWidth(val, max) {
  if (!val || !max) return '4%'
  return Math.max(4, Math.min(100, (val / max) * 100)) + '%'
}

function barColor(val, warnThreshold, badThreshold) {
  if (val == null || isNaN(val)) return 'bg-gray-400'
  if (val <= warnThreshold) return 'bg-green-500'
  if (val <= badThreshold) return 'bg-amber-500'
  return 'bg-red-500'
}

function iterBarColor(val) {
  if (val == null || isNaN(val)) return 'bg-gray-400'
  if (val < 1) return 'bg-red-500'
  if (val <= 1.5) return 'bg-green-500'
  if (val <= 2) return 'bg-amber-500'
  return 'bg-red-500'
}

function iterMetricColor(val) {
  const n = parseFloat(val)
  if (isNaN(n)) return ''
  if (n < 1) return 'text-red-500'
  if (n <= 1.5) return 'text-green-500'
  if (n <= 2) return 'text-amber-500'
  return 'text-red-500'
}

function ratioBarColor(val) {
  if (val === '—' || val == null) return 'bg-gray-400'
  const n = parseFloat(val)
  if (isNaN(n)) return 'bg-gray-400'
  if (n >= 0.8 && n <= 1.2) return 'bg-green-500'
  if (n >= 0.5 && n <= 1.5) return 'bg-amber-500'
  return 'bg-red-500'
}

// ================================================================
// Color helpers
// ================================================================

function metricColor(val, warnThreshold, badThreshold) {
  if (val === '—' || val == null) return 'text-gray-400 dark:text-gray-500'
  const n = parseFloat(val)
  if (isNaN(n)) return 'text-gray-400'
  if (n <= warnThreshold) return 'text-green-600 dark:text-green-400'
  if (n <= badThreshold) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

function ratioColor(val) {
  if (val === '—' || val == null) return 'text-gray-400 dark:text-gray-500'
  const n = parseFloat(val)
  if (isNaN(n)) return 'text-gray-400'
  if (n >= 0.8 && n <= 1.2) return 'text-green-600 dark:text-green-400'
  if (n >= 0.5 && n <= 1.5) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

function hoursColor(h) {
  if (h == null) return 'text-gray-400'
  if (h < 24) return 'text-green-600 dark:text-green-400'
  if (h < 72) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

function hoursColor24(h) {
  if (h == null) return 'text-gray-400'
  if (h < 8) return 'text-green-600 dark:text-green-400'
  if (h < 24) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

function iterColor(n) {
  if (n <= 2) return 'text-green-600 dark:text-green-400'
  if (n <= 5) return 'text-amber-600 dark:text-amber-400'
  return 'text-red-600 dark:text-red-400'
}

function exportTeamOverview() {
  const devs = currentDevs.value
  if (!devs.length) return
  const cols = [
    { label: 'Developer', format: d => d.displayName },
    { label: 'PRs', format: d => d.prCount },
    { label: 'Reviewed', format: d => d.prsReviewedCount },
    { label: 'Avg Files', format: d => d.avgFiles },
    { label: 'Avg Hours', format: d => d.avgHours },
    { label: 'Avg 1st Review (h)', format: d => d.avgFirstReview },
    { label: 'Avg Iterations', format: d => d.avgIters },
    { label: 'Unresolved %', format: d => d.unresolvedPct },
    { label: 'WI Count', format: d => d.wiCount },
    { label: 'Avg Cycle (h)', format: d => d.avgCycleTime },
    { label: 'WIP', format: d => d.wip },
    { label: 'Est/Actual Ratio', format: d => d.estActualRatio },
  ]
  exportCsv(devs, cols, `DevAssessment_${selectedProject.value || 'team'}`)
}
</script>

<style scoped>
.box-rounded {
  border-radius: 1rem;
}
.card-stat {
  border-radius: 1rem;
  box-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1);
  padding: 1rem;
  text-align: center;
  background: white;
  border: 1px solid #e5e7eb;
}
.dark .card-stat {
  background: #1f2937;
  border-color: #374151;
}
.card-stat-clickable {
  cursor: pointer;
  transition: box-shadow 0.15s, border-color 0.15s;
}
.card-stat-clickable:hover {
  border-color: #00aeef;
  box-shadow: 0 0 0 2px rgba(0, 174, 239, 0.25);
}
.card-stat-active {
  border-color: #00aeef;
  box-shadow: 0 0 0 2px rgba(0, 174, 239, 0.35);
}
.th-sort {
  cursor: pointer;
  user-select: none;
  transition: color 0.15s;
}
.th-sort:hover {
  color: #00aeef;
}
.dark .th-sort:hover {
  color: #60a5fa;
}
.stat-val {
  font-size: 1.75rem;
  font-weight: 800;
}
.dark .stat-val {
  /* inherits color from utility classes */
}
.stat-val-default {
  color: #008dcc;
}
.dark .stat-val-default {
  color: #60a5fa;
}
.stat-label {
  margin-top: 0.375rem;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: 11px;
  font-weight: 600;
  color: #6b7280;
}
.dark .stat-label {
  color: #9ca3af;
}
.chart-card {
  padding: 1.5rem 1.75rem;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 1rem;
  box-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1);
  overflow: hidden;
}
.dark .chart-card {
  background: #1e293b;
  border-color: #475569;
}
.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}
.chart-title {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: #6b7280;
  margin: 0;
}
.dark .chart-title {
  color: #d1d5db;
}
.sort-group {
  display: inline-flex;
  gap: 4px;
}
.sort-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  min-width: 28px;
  height: 24px;
  padding: 0 6px;
  border-radius: 6px;
  border: 1px solid #d1d5db;
  background: #f9fafb;
  color: #6b7280;
  cursor: pointer;
  transition: all 0.15s;
  line-height: 1;
}
.sort-btn:hover {
  background: #e5e7eb;
  color: #374151;
}
.sort-btn.active {
  background: #00aeef;
  color: white;
  border-color: #00aeef;
}
.dark .sort-btn {
  border-color: #4b5563;
  background: #374151;
  color: #9ca3af;
}
.dark .sort-btn:hover {
  background: #4b5563;
  color: #d1d5db;
}
.dark .sort-btn.active {
  background: #3b82f6;
  color: white;
  border-color: #3b82f6;
}
</style>
