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
      <button
        @click="collapsed = !collapsed"
        class="p-1 rounded-md text-white/60 hover:text-white hover:bg-white/10 transition-colors"
        :title="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
      >
        <svg class="w-4 h-4 transition-transform" :class="{ 'rotate-180': collapsed }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" />
        </svg>
      </button>
    </div>

    <!-- Search bar -->
    <div v-if="!collapsed" class="px-3 pt-3 pb-1">
      <div class="relative">
        <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-white/50" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
        </svg>
        <input
          v-model="menuSearch"
          type="text"
          placeholder="Search menu…"
          data-sidebar-search
          class="w-full pl-8 pr-7 py-1.5 text-xs rounded-md bg-white/15 border border-white/20 text-white placeholder-white/50 focus:ring-1 focus:ring-white/40 focus:border-white/40 outline-none transition-shadow"
        />
        <button
          v-if="menuSearch"
          @click="menuSearch = ''"
          class="absolute right-2 top-1/2 -translate-y-1/2 text-white/50 hover:text-white/80"
        >
          <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
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
          <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 17.25v1.007a3 3 0 01-.879 2.122L7.5 21h9l-.621-.621A3 3 0 0115 18.257V17.25m6-12V15a2.25 2.25 0 01-2.25 2.25H5.25A2.25 2.25 0 013 15V5.25A2.25 2.25 0 015.25 3h13.5A2.25 2.25 0 0121 5.25z" />
          </svg>
          <span v-if="!collapsed">DevOps Monitor</span>
        </span>
        <svg v-if="!collapsed" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': devopsOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
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
                <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
                </svg>
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

      <!-- PR Monitor section -->
      <div v-show="!isSearching || hasPrMatches">
      <button
        @click="collapsed ? (collapsed = false, prOpen = true) : (prOpen = !prOpen)"
        class="w-full flex items-center px-4 py-2.5 text-sm font-semibold text-white/90 hover:bg-white/10 transition-colors"
        :class="collapsed ? 'justify-center' : 'justify-between'"
        :title="collapsed ? 'PR Monitor' : ''"
      >
        <span class="flex items-center gap-2">
          <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M7.5 21L3 16.5m0 0L7.5 12M3 16.5h13.5m0-13.5L21 7.5m0 0L16.5 12M21 7.5H7.5" />
          </svg>
          <span v-if="!collapsed">PR Monitor</span>
        </span>
        <svg v-if="!collapsed" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': prOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
      </button>

      <div v-show="(prOpen || isSearching) && !collapsed" class="ml-2 border-l border-white/15">
        <router-link
          v-if="matchesSearch('All Projects')"
          to="/pr-monitor"
          class="flex items-center gap-2 px-4 py-2.5 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'pr-monitor' ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 12h16.5m-16.5 3.75h16.5M3.75 19.5h16.5M5.625 4.5h12.75a1.875 1.875 0 010 3.75H5.625a1.875 1.875 0 010-3.75z" />
          </svg>
          All Projects
        </router-link>

        <router-link
          v-for="proj in filteredPrProjects"
          :key="proj.id"
          :to="{ name: 'pr-project', params: { projectId: proj.id } }"
          class="flex items-center justify-between pl-8 pr-4 py-2 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'pr-project' && route.params.projectId === proj.id ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <span class="flex items-center gap-2 truncate">
            <span class="w-1.5 h-1.5 rounded-full bg-teal-400 shrink-0"></span>
            {{ proj.project }}
          </span>
        </router-link>
      </div>
      </div>

      <!-- DB Monitor section -->
      <div v-show="!isSearching || hasDbMatches">
      <button
        @click="collapsed ? (collapsed = false, dbOpen = true) : (dbOpen = !dbOpen)"
        class="w-full flex items-center px-4 py-2.5 text-sm font-semibold text-white/90 hover:bg-white/10 transition-colors"
        :class="collapsed ? 'justify-center' : 'justify-between'"
        :title="collapsed ? 'DB Monitor' : ''"
      >
        <span class="flex items-center gap-2">
          <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 6.375c0 2.278-3.694 4.125-8.25 4.125S3.75 8.653 3.75 6.375m16.5 0c0-2.278-3.694-4.125-8.25-4.125S3.75 4.097 3.75 6.375m16.5 0v11.25c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125V6.375m16.5 0v3.75m-16.5-3.75v3.75m16.5 0v3.75C20.25 16.153 16.556 18 12 18s-8.25-1.847-8.25-4.125v-3.75m16.5 0c0 2.278-3.694 4.125-8.25 4.125s-8.25-1.847-8.25-4.125" />
          </svg>
          <span v-if="!collapsed">DB Monitor</span>
        </span>
        <svg v-if="!collapsed" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': dbOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
      </button>

      <div v-show="(dbOpen || isSearching) && !collapsed" class="ml-2 border-l border-white/15">
        <router-link
          v-if="matchesSearch('All Databases')"
          to="/db-monitor"
          class="flex items-center gap-2 px-4 py-2.5 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'db-monitor' ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 12h16.5m-16.5 3.75h16.5M3.75 19.5h16.5M5.625 4.5h12.75a1.875 1.875 0 010 3.75H5.625a1.875 1.875 0 010-3.75z" />
          </svg>
          All Databases
          <span v-if="store.allDatabases.length" class="ml-auto text-xs bg-white/90 text-primary-700 dark:bg-white/20 dark:text-white/80 rounded-full px-2 py-0.5 font-bold">{{ store.allDatabases.length }}</span>
        </router-link>

        <!-- Per-project sub-items -->
        <router-link
          v-for="dbProj in filteredDbProjects"
          :key="dbProj.id"
          :to="{ name: 'db-project', params: { projectId: dbProj.id } }"
          class="flex items-center justify-between pl-8 pr-4 py-2 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'db-project' && route.params.projectId === dbProj.id ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <span class="flex items-center gap-2 truncate">
            <span class="w-1.5 h-1.5 rounded-full bg-indigo-400 shrink-0"></span>
            {{ dbProj.name }}
          </span>
          <span v-if="store.dbProjectDatabases[dbProj.id]?.databases" class="text-xs opacity-75">{{ store.dbProjectDatabases[dbProj.id].databases.length }}</span>
        </router-link>
      </div>
      </div>

      <!-- Sprint Populator link -->
      <div v-show="!isSearching || matchesSearch('Sprint Populator')">
        <router-link
          to="/sprint-populator"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'sprint-populator' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Sprint Populator' : ''"
        >
          <span class="flex items-center gap-2">
            <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15.59 14.37a6 6 0 01-5.84 7.38v-4.8m5.84-2.58a14.98 14.98 0 006.16-12.12A14.98 14.98 0 009.631 8.41m5.96 5.96a14.926 14.926 0 01-5.841 2.58m-.119-8.54a6 6 0 00-7.381 5.84h4.8m2.58-5.84a14.927 14.927 0 00-2.58 5.84m2.699 2.7c-.103.021-.207.041-.311.06a15.09 15.09 0 01-2.448-2.448 14.9 14.9 0 01.06-.312m-2.24 2.39a4.493 4.493 0 00-1.757 4.306 4.493 4.493 0 004.306-1.758M16.5 9a1.5 1.5 0 11-3 0 1.5 1.5 0 013 0z" />
            </svg>
            <span v-if="!collapsed">Sprint Populator</span>
          </span>
        </router-link>
      </div>

      <!-- Velocity link -->
      <div v-show="!isSearching || matchesSearch('Velocity')">
        <router-link
          to="/velocity"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'velocity' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Velocity' : ''"
        >
          <span class="flex items-center gap-2">
            <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 3v11.25A2.25 2.25 0 006 16.5h2.25M3.75 3h-1.5m1.5 0h16.5m0 0h1.5m-1.5 0v11.25A2.25 2.25 0 0118 16.5h-2.25m-7.5 0h7.5m-7.5 0l-1 3m8.5-3l1 3m0 0l.5 1.5m-.5-1.5h-9.5m0 0l-.5 1.5m.75-9l3-1.5L12 12m0 0l3-1.5M12 12V9" />
            </svg>
            <span v-if="!collapsed">Velocity</span>
          </span>
        </router-link>
      </div>

      <!-- Permission Overview section -->
      <div v-show="!isSearching || hasPermCheckMatches">
      <button
        @click="collapsed ? (collapsed = false, permCheckOpen = true) : (permCheckOpen = !permCheckOpen)"
        class="w-full flex items-center px-4 py-2.5 text-sm font-semibold text-white/90 hover:bg-white/10 transition-colors"
        :class="collapsed ? 'justify-center' : 'justify-between'"
        :title="collapsed ? 'Permission Overview' : ''"
      >
        <span class="flex items-center gap-2">
          <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
          </svg>
          <span v-if="!collapsed">Permission Overview</span>
        </span>
        <svg v-if="!collapsed" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': permCheckOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
      </button>

      <div v-show="(permCheckOpen || isSearching) && !collapsed" class="ml-2 border-l border-white/15">
        <router-link
          v-for="proj in filteredPermCheckProjects"
          :key="'perm-' + proj.id"
          :to="{ name: 'permission-check', params: { projectId: proj.id } }"
          class="flex items-center justify-between pl-8 pr-4 py-2 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'permission-check' && route.params.projectId === proj.id ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <span class="flex items-center gap-2 truncate">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 shrink-0"></span>
            {{ proj.project }}
          </span>
        </router-link>
        <div v-if="filteredPermCheckProjects.length === 0 && isSearching" class="px-4 py-3 text-xs text-white/50 text-center italic">No matches</div>
        <div v-else-if="filteredPermCheckProjects.length === 0" class="px-4 py-3 text-xs text-white/50 text-center italic">No projects configured</div>
      </div>
      </div>

      <!-- Check Permissions link -->
      <div v-show="!isSearching || matchesSearch('Check Permissions')">
        <router-link
          to="/check-permissions"
          @click="collapsed && (collapsed = false)"
          class="w-full flex items-center px-4 py-2.5 text-sm font-semibold hover:bg-white/10 transition-colors"
          :class="[
            collapsed ? 'justify-center' : 'justify-between',
            route.name === 'check-permissions' ? 'text-white bg-white/15' : 'text-white/90'
          ]"
          :title="collapsed ? 'Check Permissions' : ''"
        >
          <span class="flex items-center gap-2">
            <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M10.5 6h9.75M10.5 6a1.5 1.5 0 11-3 0m3 0a1.5 1.5 0 10-3 0M3.75 6H7.5m3 12h9.75m-9.75 0a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m-3.75 0H7.5m9-6h3.75m-3.75 0a1.5 1.5 0 01-3 0m3 0a1.5 1.5 0 00-3 0m-9.75 0h9.75" />
            </svg>
            <span v-if="!collapsed">Check Permissions</span>
          </span>
        </router-link>
      </div>

      <!-- Pipelines section -->
      <div v-show="!isSearching || hasPipelinesMatches">
      <button
        @click="collapsed ? (collapsed = false, pipelinesOpen = true) : (pipelinesOpen = !pipelinesOpen)"
        class="w-full flex items-center px-4 py-2.5 text-sm font-semibold text-white/90 hover:bg-white/10 transition-colors"
        :class="collapsed ? 'justify-center' : 'justify-between'"
        :title="collapsed ? 'Pipelines' : ''"
      >
        <span class="flex items-center gap-2">
          <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M5.25 5.653c0-.856.917-1.398 1.667-.986l11.54 6.348a1.125 1.125 0 010 1.971l-11.54 6.347a1.125 1.125 0 01-1.667-.985V5.653z" />
          </svg>
          <span v-if="!collapsed">Pipelines</span>
        </span>
        <svg v-if="!collapsed" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': pipelinesOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
      </button>

      <div v-show="(pipelinesOpen || isSearching) && !collapsed" class="ml-2 border-l border-white/15">
        <router-link
          v-for="proj in filteredPipelinesProjects"
          :key="'pipe-' + proj.id"
          :to="{ name: 'pipelines', params: { projectId: proj.id } }"
          class="flex items-center justify-between pl-8 pr-4 py-2 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'pipelines' && route.params.projectId === proj.id ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <span class="flex items-center gap-2 truncate">
            <span class="w-1.5 h-1.5 rounded-full bg-cyan-400 shrink-0"></span>
            {{ proj.project }}
          </span>
        </router-link>
        <div v-if="filteredPipelinesProjects.length === 0 && isSearching" class="px-4 py-3 text-xs text-white/50 text-center italic">No matches</div>
        <div v-else-if="filteredPipelinesProjects.length === 0" class="px-4 py-3 text-xs text-white/50 text-center italic">No projects configured</div>
      </div>
      </div>

      <!-- Releases section -->
      <div v-show="!isSearching || hasReleasesMatches">
      <button
        @click="collapsed ? (collapsed = false, releasesOpen = true) : (releasesOpen = !releasesOpen)"
        class="w-full flex items-center px-4 py-2.5 text-sm font-semibold text-white/90 hover:bg-white/10 transition-colors"
        :class="collapsed ? 'justify-center' : 'justify-between'"
        :title="collapsed ? 'Releases' : ''"
      >
        <span class="flex items-center gap-2">
          <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9.568 3H5.25A2.25 2.25 0 003 5.25v4.318c0 .597.237 1.17.659 1.591l9.581 9.581c.699.699 1.78.872 2.607.33a18.095 18.095 0 005.223-5.223c.542-.827.369-1.908-.33-2.607L11.16 3.66A2.25 2.25 0 009.568 3z" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 6h.008v.008H6V6z" />
          </svg>
          <span v-if="!collapsed">Releases</span>
        </span>
        <svg v-if="!collapsed" class="w-4 h-4 transition-transform" :class="{ 'rotate-90': releasesOpen }" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
        </svg>
      </button>

      <div v-show="(releasesOpen || isSearching) && !collapsed" class="ml-2 border-l border-white/15">
        <router-link
          v-for="proj in filteredReleasesProjects"
          :key="'rel-' + proj.id"
          :to="{ name: 'releases', params: { projectId: proj.id } }"
          class="flex items-center justify-between pl-8 pr-4 py-2 text-sm hover:bg-white/10 hover:text-white transition-colors"
          :class="route.name === 'releases' && route.params.projectId === proj.id ? 'text-white bg-white/15' : 'text-white/60'"
        >
          <span class="flex items-center gap-2 truncate">
            <span class="w-1.5 h-1.5 rounded-full bg-orange-400 shrink-0"></span>
            {{ proj.project }}
          </span>
        </router-link>
        <div v-if="filteredReleasesProjects.length === 0 && isSearching" class="px-4 py-3 text-xs text-white/50 text-center italic">No matches</div>
        <div v-else-if="filteredReleasesProjects.length === 0" class="px-4 py-3 text-xs text-white/50 text-center italic">No projects configured</div>
      </div>
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
            <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M3 13.125C3 12.504 3.504 12 4.125 12h2.25c.621 0 1.125.504 1.125 1.125v6.75C7.5 20.496 6.996 21 6.375 21h-2.25A1.125 1.125 0 013 19.875v-6.75zM9.75 8.625c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125v11.25c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V8.625zM16.5 4.125c0-.621.504-1.125 1.125-1.125h2.25C20.496 3 21 3.504 21 4.125v15.75c0 .621-.504 1.125-1.125 1.125h-2.25a1.125 1.125 0 01-1.125-1.125V4.125z" />
            </svg>
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
        <svg class="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
        </svg>
        <span v-if="!collapsed">Manage Projects</span>
      </router-link>

      <div
        class="mt-3 flex items-center rounded-md border border-white/15 bg-white/5 px-2 py-2 text-white/75"
        :class="collapsed ? 'justify-center' : ''"
      >
        <span class="text-xs font-semibold tracking-wide">Version ID: {{ appVersion }}</span>
      </div>
    </div>
  </aside>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useMonitorStore } from '../stores/monitor.js'
import appPackage from '../../package.json'

const store = useMonitorStore()
const route = useRoute()
const collapsed = ref(false)
const devopsOpen = ref(true)
const dbOpen = ref(false)
const prOpen = ref(false)
const permCheckOpen = ref(false)
const pipelinesOpen = ref(false)
const releasesOpen = ref(false)
const menuSearch = ref('')
const appVersion = appPackage.version

const isSearching = computed(() => menuSearch.value.trim().length > 0)

function matchesSearch(text) {
  if (!isSearching.value) return true
  return text.toLowerCase().includes(menuSearch.value.trim().toLowerCase())
}

const prCheckTypes = ['pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check']

const prProjects = computed(() => {
  return store.displayProjects.filter(p =>
    p.checks.some(c => c.enabled && ['pr_approval_check', 'stale_pr_check', 'unreviewed_pr_check'].includes(c.check_type))
  )
})

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

const filteredPrProjects = computed(() => {
  if (!isSearching.value) return prProjects.value
  const q = menuSearch.value.trim().toLowerCase()
  return prProjects.value.filter(p => p.project.toLowerCase().includes(q))
})

const permCheckProjects = computed(() => store.displayProjects)

const filteredPermCheckProjects = computed(() => {
  if (!isSearching.value) return permCheckProjects.value
  const q = menuSearch.value.trim().toLowerCase()
  return permCheckProjects.value.filter(p => p.project.toLowerCase().includes(q))
})

const hasPermCheckMatches = computed(() => {
  if (matchesSearch('Permission Overview')) return true
  return filteredPermCheckProjects.value.length > 0
})

const pipelinesProjects = computed(() => store.displayProjects)

const filteredPipelinesProjects = computed(() => {
  if (!isSearching.value) return pipelinesProjects.value
  const q = menuSearch.value.trim().toLowerCase()
  return pipelinesProjects.value.filter(p => p.project.toLowerCase().includes(q))
})

const hasPipelinesMatches = computed(() => {
  if (matchesSearch('Pipelines')) return true
  return filteredPipelinesProjects.value.length > 0
})

const releasesProjects = computed(() => store.displayProjects)

const filteredReleasesProjects = computed(() => {
  if (!isSearching.value) return releasesProjects.value
  const q = menuSearch.value.trim().toLowerCase()
  return releasesProjects.value.filter(p => p.project.toLowerCase().includes(q))
})

const hasReleasesMatches = computed(() => {
  if (matchesSearch('Releases')) return true
  return filteredReleasesProjects.value.length > 0
})

const filteredDbProjects = computed(() => {
  if (!isSearching.value) return store.displayDbProjects
  const q = menuSearch.value.trim().toLowerCase()
  return store.displayDbProjects.filter(p => p.name.toLowerCase().includes(q))
})

// Section visibility when searching
const hasDevopsMatches = computed(() => {
  if (!isSearching.value) return true
  return filteredSidebarItems.value.length > 0 || matchesSearch('DevOps Monitor')
})

const hasPrMatches = computed(() => {
  if (!isSearching.value) return true
  return matchesSearch('All Projects') || matchesSearch('PR Monitor') || filteredPrProjects.value.length > 0
})

const hasDbMatches = computed(() => {
  if (!isSearching.value) return true
  return matchesSearch('All Databases') || matchesSearch('DB Monitor') || filteredDbProjects.value.length > 0
})

onMounted(async () => {
  const initTasks = [store.fetchDbProjects()]
  if (!store.projects.length) initTasks.push(store.fetchProjects())
  await Promise.all(initTasks)
  if (store.dbCredentialsConfigured) {
    store.refreshDbSidebar()
  }
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
