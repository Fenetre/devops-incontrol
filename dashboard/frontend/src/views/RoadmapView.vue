<template>
  <div>
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h2 class="text-2xl font-bold text-primary-500 dark:text-gray-100">Roadmap</h2>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">Portfolio roadmap board — organize features across projects and timelines.</p>
      </div>
      <div v-if="activeTab === 'roadmap' && store.hasDirty" class="flex items-center gap-2">
        <span class="text-xs text-amber-600 dark:text-amber-400">{{ store.dirtyCount }} unsaved</span>
        <UButton variant="outline" color="neutral" icon="i-heroicons-eye" @click="showChangesPreview = true">Show Changes</UButton>
        <UButton icon="i-heroicons-cloud-arrow-up" :loading="store.pushing" @click="pushChanges">Push to DevOps</UButton>
      </div>
    </div>

    <!-- Tab bar -->
    <UTabs :items="tabs" v-model="activeTab" :content="false" variant="link" class="mb-6" />

    <!-- =============== ROADMAP TAB =============== -->
    <div v-if="activeTab === 'roadmap'">
      <!-- Link mode indicator -->
      <div v-if="linkSourceItem" class="mb-3 flex items-center gap-3 px-4 py-2 rounded-lg bg-primary-50 dark:bg-primary-900/30 border border-primary-200 dark:border-primary-700">
        <UIcon name="i-heroicons-link" class="w-4 h-4 text-primary-500" />
        <span class="text-sm text-primary-700 dark:text-primary-300">
          Linking from <strong>{{ dTitle(linkSourceItem.title) }}</strong> (#{{ linkSourceItem.id }}) — click another bar's chain icon to set the successor.
        </span>
        <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-x-mark" @click="linkSourceItem = null" class="ml-auto" />
      </div>
      <div v-if="store.loading" class="flex items-center justify-center py-8 gap-2 text-gray-500 dark:text-gray-400">
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
        <span class="text-sm">Loading configuration…</span>
      </div>
      <div v-else-if="store.config.projects.length === 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-6 py-12 text-center">
        <UIcon name="i-heroicons-map" class="w-12 h-12 mx-auto text-gray-300 dark:text-gray-600 mb-4" />
        <p class="text-gray-500 dark:text-gray-400 mb-4">No projects configured yet. Add projects in the Configuration tab to get started.</p>
        <UButton variant="soft" color="primary" icon="i-heroicons-cog-6-tooth" @click="activeTab = 'config'">Go to Configuration</UButton>
      </div>

      <div v-else-if="store.loadingItems" class="flex items-center justify-center py-8 gap-2 text-gray-500 dark:text-gray-400">
        <UIcon name="i-heroicons-arrow-path" class="w-4 h-4 animate-spin" />
        <span class="text-sm">Loading work items…</span>
      </div>

      <div v-else>
        <!-- Project legend -->
        <div v-if="store.config.projects.length > 1" class="flex flex-wrap items-center gap-3 text-xs text-gray-500 dark:text-gray-400 mb-4">
          <span class="font-medium">Projects:</span>
          <span v-for="proj in store.config.projects" :key="proj.project_id" class="flex items-center gap-1.5">
            <span class="w-2.5 h-2.5 rounded-full" :class="projectColorClass(proj.project)"></span>
            {{ dP(proj.project) }}
          </span>
        </div>

        <!-- Filter bar -->
        <div class="relative z-30 bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-5 py-3 mb-4 flex flex-wrap items-center gap-3">
          <USelectMenu v-model="activeView" :items="viewOptions" value-key="value" size="sm" class="w-full sm:w-56" />
          <USelectMenu v-model="projectFilter" :items="projectFilterOptions" value-key="value" placeholder="All projects" size="sm" multiple class="w-full sm:w-44" />
          <USelectMenu v-model="stateFilter" :items="stateFilterOptions" value-key="value" placeholder="All states" size="sm" multiple class="w-full sm:w-36" />
          <UInput name="search-filter" v-model="searchFilter" placeholder="Search items…" size="sm" icon="i-heroicons-magnifying-glass" class="w-full sm:w-48 app-search" />
          <div class="ml-auto flex items-center gap-2">
            <UButton
              :icon="showUnplanned ? 'i-heroicons-eye' : 'i-heroicons-eye-slash'"
              variant="ghost" color="neutral" size="sm"
              :label="showUnplanned ? 'Hide Unplanned' : 'Show Unplanned'"
              @click="showUnplanned = !showUnplanned"
            />
            <UButton icon="i-heroicons-arrow-path" :loading="store.loadingItems" :disabled="store.loadingItems" @click="store.loadItems()" :label="store.loadingItems ? 'Loading...' : 'Refresh'" />
          </div>
        </div>

        <!-- Error alert -->
        <UAlert v-if="store.error" color="error" icon="i-heroicons-exclamation-circle" :description="store.error" class="mb-4" />

        <!-- Roadmap grid: epics (rows) × timeline (columns) -->
        <div v-if="epics.length === 0" class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm px-6 py-8 text-center">
          <p class="text-gray-500 dark:text-gray-400">No epics found. Ensure your configured projects have active Epics.</p>
        </div>

        <div v-else class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm flex flex-col overflow-hidden max-h-[calc(100vh-250px)]">
          <!-- Month calendar bar (sprint mode only) -->
          <div v-if="monthBar.length > 0" class="flex border-b border-gray-200 dark:border-gray-700 shrink-0">
            <div class="shrink-0 bg-gray-50 dark:bg-gray-700 border-r border-gray-200 dark:border-gray-700" :style="{ width: EPIC_COL_WIDTH + 'px' }"></div>
            <div v-if="showUnplanned" class="shrink-0 bg-gray-50 dark:bg-gray-700 border-r border-gray-200 dark:border-gray-700" :style="{ width: UNPLANNED_WIDTH + 'px' }"></div>
            <div v-for="(m, idx) in monthBar" :key="idx"
              class="bg-gray-100 dark:bg-gray-700 border-r border-gray-200 dark:border-gray-700 last:border-r-0 text-[10px] font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider text-center py-1 min-w-0"
              :style="{ flex: m.days + ' 1 0%' }"
            >
              {{ m.label }}
            </div>
          </div>

          <!-- Sprint overlay rows (quarter mode only, one row per team) -->
          <div v-for="row in sprintOverlayRows" :key="row.configId" class="flex border-b border-gray-200 dark:border-gray-700 shrink-0 bg-gray-50 dark:bg-gray-700">
            <div class="shrink-0 border-r border-gray-200 dark:border-gray-700 px-2 flex items-center gap-1.5" :style="{ width: EPIC_COL_WIDTH + 'px' }">
              <span class="w-2 h-2 rounded-full shrink-0" :class="row.colorClass"></span>
              <span class="text-[10px] font-medium text-gray-500 dark:text-gray-400 truncate" :title="dT(row.team)">{{ dT(row.team) }}</span>
            </div>
            <div v-if="showUnplanned" class="shrink-0 border-r border-gray-200 dark:border-gray-700" :style="{ width: UNPLANNED_WIDTH + 'px' }"></div>
            <div class="flex-1 relative h-6 min-w-0">
              <div v-for="m in row.markers" :key="m.key"
                class="absolute top-0 bottom-0 border-l border-gray-300/50 dark:border-gray-500/50 flex items-center justify-center overflow-hidden"
                :class="projectBadgeClass(row.project)"
                :style="{ left: m.left + '%', width: m.width + '%' }"
                :title="m.label"
              >
                <span class="text-[9px] whitespace-nowrap px-0.5 truncate opacity-80">{{ m.label }}</span>
              </div>
            </div>
          </div>

          <!-- Epic rows (scrollable) -->
          <div ref="barsContainerRef" class="flex-1 overflow-y-auto overflow-x-clip relative">
          <!-- Timeline header (sticky inside scroll container so columns stay aligned) -->
          <div class="grid border-b border-gray-200 dark:border-gray-700 sticky top-0 z-20 bg-gray-50 dark:bg-gray-700" :style="gridStyle">
            <div class="px-4 py-2 bg-gray-50 dark:bg-gray-700 border-r border-gray-200 dark:border-gray-700 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider flex items-center gap-2">
              Epics
              <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-plus" @click="openCreateEpic" title="Create Epic" class="ml-auto" />
            </div>
            <div v-for="col in timelineColumns" :key="col.key"
              class="px-3 py-2 bg-gray-50 dark:bg-gray-700 border-r border-gray-200 dark:border-gray-700 last:border-r-0 text-xs font-semibold text-gray-600 dark:text-gray-300 text-center truncate"
              :class="col.isGap ? 'opacity-0' : ''"
              :title="col.label"
            >
              {{ col.label }}
            </div>
          </div>
          <div v-for="epic in epics" :key="epic.id"
            :data-epic-id="String(epic.id)"
            class="group/epic grid border-b border-gray-100 dark:border-gray-700 last:border-b-0" :style="gridStyle">
            <!-- Epic label -->
            <div class="px-4 py-3 border-r border-gray-200 dark:border-gray-700 bg-gray-50/50 dark:bg-gray-800" style="grid-row: 1; grid-column: 1">
              <div class="flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full shrink-0" :class="projectColorClass(epic.project)"></span>
                <button class="text-xs font-semibold text-gray-700 dark:text-gray-300 leading-tight hover:text-primary-600 dark:hover:text-primary-400 text-left transition-colors" :title="dTitle(epic.title)" @click="openDetail(epic)">{{ dTitle(epic.title) }}</button>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-plus" class="ml-auto shrink-0 opacity-0 group-hover/epic:opacity-100 transition-opacity" @click="openCreateFeature(epic)" title="Create Feature" />
              </div>
              <div class="mt-1 pl-[18px]">
                <span class="text-[10px] text-gray-400 dark:text-gray-500">#{{ epic.id }} · {{ epic.state }}</span>
              </div>
              <div class="mt-1 pl-[18px]">
                <span class="text-[10px] px-2 py-0.5 rounded-full font-medium whitespace-nowrap" :class="projectBadgeClass(epic.project)">{{ dP(epic.project) }}</span>
              </div>
            </div>
            <!-- Column cells -->
            <template v-for="(col, colIdx) in timelineColumns" :key="col.key">
              <div v-if="col.isGap" class="border-r border-gray-200 dark:border-gray-700" :style="{ gridRow: '1', gridColumn: String(colIdx + 2) }"></div>
              <div v-else
                :data-drop-col="col.key"
                class="relative px-2 py-2 border-r border-gray-200 dark:border-gray-700 last:border-r-0 min-h-[70px] transition-colors"
                :style="{ gridRow: '1', gridColumn: String(colIdx + 2) }"
                :class="[
                  dragOverCell === epic.id + '|' + col.key ? 'bg-primary-100/80 dark:bg-primary-900/40 outline-2 outline-dashed outline-primary-400 -outline-offset-2' : '',
                  col.key !== '_unplanned' && columnMode === 'sprints' && dragOverCell !== epic.id + '|' + col.key ? 'bg-gray-100/40 dark:bg-gray-700/20' : ''
                ]"
              >
                <!-- Unplanned column: show cards normally -->
                <div v-if="col.key === '_unplanned'" class="flex flex-col gap-1.5 min-h-[50px]">
                  <RoadmapCard
                    v-for="item in cellItems(epic.id, col.key)" :key="item.id"
                    :item="item"
                    :color-class="projectColorClass(item.project)"
                    :badge-class="projectBadgeClass(item.project)"
                    :is-dirty="isDirty(item.id)"
                    @open-detail="openDetail"
                  />
                </div>
              </div>
            </template>
            <!-- Feature bars overlay — positioned over the timeline columns -->
            <div
              class="pointer-events-none z-10"
              :style="{ gridColumn: (showUnplanned ? '3' : '2') + ' / -1', gridRow: '1', position: 'relative', minHeight: '70px' }"
            >
              <div class="flex flex-col gap-1.5 py-2">
                <div
                  v-for="item in epicBarFeatures(epic.id)" :key="item.id"
                  class="bar-wrapper px-2"
                  :class="draggingId ? 'pointer-events-none' : 'pointer-events-auto'"
                  :style="{
                    marginLeft: barPositions.get(item.id)?.left + '%',
                    width: barPositions.get(item.id)?.width + '%'
                  }"
                >
                  <div
                    class="bar-inner group rounded-lg border text-xs px-3 py-2 cursor-grab active:cursor-grabbing shadow-sm hover:shadow-md transition-shadow relative select-none"
                    :class="[
                      isDirty(item.id) || hasDirtyLink(item.id) ? 'ring-2 ring-amber-400' : '',
                      linkSourceItem?.id === item.id ? 'ring-2 ring-primary-500' : '',
                      draggingId === item.id ? 'opacity-30 pointer-events-none' : (draggingId ? 'pointer-events-none' : ''),
                      'border-gray-200 dark:border-gray-600 bg-gray-50 dark:bg-gray-700'
                    ]"
                    :data-item-id="item.id"
                    :title="item.title + ' (#' + item.id + ')'"
                    @pointerdown.stop="onBarPointerDown($event, item)"
                  >
                    <!-- Chain link icon -->
                    <button
                      class="absolute top-1 right-2 z-20 p-0.5 rounded opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer"
                      :class="linkSourceItem?.id === item.id ? 'opacity-100 text-primary-500' : 'text-gray-400 hover:text-primary-500'"
                      @pointerdown.stop
                      @click.stop="onChainClick(item)"
                      :title="linkSourceItem ? 'Click to link as successor' : 'Click to start linking'"
                    >
                      <UIcon name="i-heroicons-link" class="w-3.5 h-3.5" />
                    </button>
                    <!-- Left resize handle -->
                    <div
                      class="absolute left-0 top-0 bottom-0 w-2 cursor-col-resize opacity-0 group-hover:opacity-100 bg-primary-400/30 rounded-l z-10"
                      @pointerdown.stop="onResizeStart($event, item, 'left')"
                    ></div>
                    <div class="flex items-start gap-2 overflow-hidden">
                      <span class="w-2 h-2 rounded-full mt-1 shrink-0" :class="projectColorClass(item.project)"></span>
                      <div class="min-w-0 flex-1">
                        <div class="font-medium text-gray-800 dark:text-gray-200 line-clamp-3 leading-tight hover:text-primary-600 dark:hover:text-primary-400 cursor-pointer" @click.stop="openDetail(item)">{{ dTitle(item.title) }}</div>
                        <div class="flex items-center gap-1.5 mt-0.5">
                          <span class="text-gray-400">#{{ item.id }}</span>
                          <span class="px-1.5 py-0.5 rounded-full" :class="projectBadgeClass(item.project)">{{ item.work_item_type }}</span>
                          <span v-if="item.state" class="text-gray-400">{{ item.state }}</span>
                        </div>
                      </div>
                    </div>
                    <!-- Right resize handle -->
                    <div
                      class="absolute right-0 top-0 bottom-0 w-2 cursor-col-resize opacity-0 group-hover:opacity-100 bg-primary-400/30 rounded-r z-10"
                      @pointerdown.stop="onResizeStart($event, item, 'right')"
                    ></div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Unassigned row (features without a parent epic) -->
          <div v-if="unparentedFeatures.length > 0" data-epic-id="null" class="grid border-t border-dashed border-gray-300 dark:border-gray-600" :style="gridStyle">
            <div class="px-4 py-3 border-r border-gray-200 dark:border-gray-700 flex items-center gap-2 bg-gray-50/50 dark:bg-gray-800" style="grid-row: 1; grid-column: 1">
              <span class="text-xs font-semibold text-gray-500 dark:text-gray-400">No Parent Epic</span>
            </div>
            <template v-for="(col, colIdx) in timelineColumns" :key="col.key">
              <div v-if="col.isGap" class="border-r border-gray-200 dark:border-gray-700" :style="{ gridRow: '1', gridColumn: String(colIdx + 2) }"></div>
              <div v-else
                :data-drop-col="col.key"
                class="px-2 py-2 border-r border-gray-200 dark:border-gray-700 last:border-r-0 min-h-[70px] transition-colors"
                :style="{ gridRow: '1', gridColumn: String(colIdx + 2) }"
                :class="[
                  dragOverCell === 'null|' + col.key ? 'bg-primary-100/80 dark:bg-primary-900/40 outline-2 outline-dashed outline-primary-400 -outline-offset-2' : '',
                  col.key !== '_unplanned' && columnMode === 'sprints' && dragOverCell !== 'null|' + col.key ? 'bg-gray-100/40 dark:bg-gray-700/20' : ''
                ]"
              >
                <div v-if="col.key === '_unplanned'" class="flex flex-col gap-1.5 min-h-[50px]">
                  <RoadmapCard
                    v-for="item in cellItems(null, col.key)" :key="item.id"
                    :item="item"
                    :color-class="projectColorClass(item.project)"
                    :badge-class="projectBadgeClass(item.project)"
                    :is-dirty="isDirty(item.id)"
                    @open-detail="openDetail"
                  />
                </div>
              </div>
            </template>
            <!-- Feature bars overlay for unparented row -->
            <div
              class="pointer-events-none z-10"
              :style="{ gridColumn: (showUnplanned ? '3' : '2') + ' / -1', gridRow: '1', position: 'relative', minHeight: '70px' }"
            >
              <div class="flex flex-col gap-1.5 py-2">
                <div
                  v-for="item in epicBarFeatures(null)" :key="item.id"
                  class="bar-wrapper px-2"
                  :class="draggingId ? 'pointer-events-none' : 'pointer-events-auto'"
                  :style="{
                    marginLeft: barPositions.get(item.id)?.left + '%',
                    width: barPositions.get(item.id)?.width + '%'
                  }"
                >
                  <div
                    class="bar-inner group rounded-lg border text-xs px-3 py-2 cursor-grab active:cursor-grabbing shadow-sm hover:shadow-md transition-shadow relative select-none"
                    :class="[
                      isDirty(item.id) || hasDirtyLink(item.id) ? 'ring-2 ring-amber-400' : '',
                      linkSourceItem?.id === item.id ? 'ring-2 ring-primary-500' : '',
                      draggingId === item.id ? 'opacity-30 pointer-events-none' : (draggingId ? 'pointer-events-none' : ''),
                      'border-gray-200 dark:border-gray-600 bg-gray-50 dark:bg-gray-700'
                    ]"
                    :data-item-id="item.id"
                    :title="item.title + ' (#' + item.id + ')'"
                    @pointerdown.stop="onBarPointerDown($event, item)"
                  >
                    <!-- Chain link icon -->
                    <button
                      class="absolute top-1 right-2 z-20 p-0.5 rounded opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer"
                      :class="linkSourceItem?.id === item.id ? 'opacity-100 text-primary-500' : 'text-gray-400 hover:text-primary-500'"
                      @pointerdown.stop
                      @click.stop="onChainClick(item)"
                      :title="linkSourceItem ? 'Click to link as successor' : 'Click to start linking'"
                    >
                      <UIcon name="i-heroicons-link" class="w-3.5 h-3.5" />
                    </button>
                    <div
                      class="absolute left-0 top-0 bottom-0 w-2 cursor-col-resize opacity-0 group-hover:opacity-100 bg-primary-400/30 rounded-l z-10"
                      @pointerdown.stop="onResizeStart($event, item, 'left')"
                    ></div>
                    <div class="flex items-start gap-2 overflow-hidden">
                      <span class="w-2 h-2 rounded-full mt-1 shrink-0" :class="projectColorClass(item.project)"></span>
                      <div class="min-w-0 flex-1">
                        <div class="font-medium text-gray-800 dark:text-gray-200 line-clamp-3 leading-tight hover:text-primary-600 dark:hover:text-primary-400 cursor-pointer" @click.stop="openDetail(item)">{{ dTitle(item.title) }}</div>
                        <div class="flex items-center gap-1.5 mt-0.5">
                          <span class="text-gray-400">#{{ item.id }}</span>
                          <span class="px-1.5 py-0.5 rounded-full" :class="projectBadgeClass(item.project)">{{ item.work_item_type }}</span>
                          <span v-if="item.state" class="text-gray-400">{{ item.state }}</span>
                        </div>
                      </div>
                    </div>
                    <div
                      class="absolute right-0 top-0 bottom-0 w-2 cursor-col-resize opacity-0 group-hover:opacity-100 bg-primary-400/30 rounded-r z-10"
                      @pointerdown.stop="onResizeStart($event, item, 'right')"
                    ></div>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <!-- Dependency arrows SVG overlay -->
          <svg v-if="dependencyArrows.length > 0" class="absolute inset-0 w-full h-full pointer-events-none z-[5]" style="overflow: visible;">
            <defs>
              <marker id="dep-arrow" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto">
                <path d="M0,0 L6,3 L0,6" fill="currentColor" />
              </marker>
              <marker id="dep-arrow-red" markerWidth="6" markerHeight="6" refX="5" refY="3" orient="auto">
                <path d="M0,0 L6,3 L0,6" fill="#ef4444" />
              </marker>
            </defs>
            <g v-for="(arrow, idx) in dependencyArrows" :key="idx" class="dep-arrow-group">
              <!-- Wider invisible hit area -->
              <path
                :d="arrow.path"
                fill="none"
                stroke="transparent"
                stroke-width="12"
                class="pointer-events-auto cursor-pointer"
                @click.stop="onArrowClick(arrow)"
              />
              <!-- Visible arrow -->
              <path
                :d="arrow.path"
                fill="none"
                :stroke="arrow.pendingRemoval ? '#ef4444' : arrow.color"
                stroke-width="1.5"
                :stroke-dasharray="arrow.dashed || arrow.pendingRemoval ? '6 3' : 'none'"
                :marker-end="arrow.pendingRemoval ? 'url(#dep-arrow-red)' : 'url(#dep-arrow)'"
                :style="{ color: arrow.pendingRemoval ? '#ef4444' : arrow.color }"
                :opacity="arrow.pendingRemoval ? 0.5 : 0.7"
                class="pointer-events-none"
              />
              <!-- Remove icon at midpoint (visible on hover via CSS) -->
              <g
                :transform="`translate(${arrow.midX - 8}, ${arrow.midY - 8})`"
                class="dep-arrow-remove pointer-events-auto cursor-pointer"
                @click.stop="onArrowClick(arrow)"
              >
                <circle cx="8" cy="8" r="8" fill="white" stroke="#ef4444" stroke-width="1.5" />
                <path d="M5,5 L11,11 M11,5 L5,11" stroke="#ef4444" stroke-width="1.5" stroke-linecap="round" />
              </g>
            </g>
          </svg>
          </div><!-- end scrollable rows -->
        </div>

      </div>

      <!-- Drag ghost -->
      <Teleport to="body">
        <div v-show="dragGhostVisible" ref="dragGhostEl" class="fixed pointer-events-none z-[9999] opacity-85 rotate-1 shadow-lg px-3 py-2 rounded-lg border border-primary-300 dark:border-primary-600 bg-primary-50 dark:bg-primary-900/80 text-sm font-medium text-gray-700 dark:text-gray-200 whitespace-nowrap max-w-[220px] truncate"
        >
          {{ dragGhostText }}
        </div>
      </Teleport>
    </div>

    <!-- =============== CONFIGURATION TAB =============== -->
    <div v-if="activeTab === 'config'">
      <!-- Projects section -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm">
        <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center justify-between">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Projects</h3>
          <UButton size="xs" icon="i-heroicons-plus" @click="showAddProject = true">Add Project</UButton>
        </div>
        <div class="px-5 py-4">
          <div v-if="store.config.projects.length === 0" class="text-sm text-gray-400 dark:text-gray-500 py-4 text-center">
            No projects added. Add Azure DevOps projects to pull work items from.
          </div>
          <div v-else class="space-y-2">
            <div v-for="proj in store.config.projects" :key="proj.organization + '/' + proj.project_id"
              class="flex items-center gap-3 px-3 py-2 rounded-lg border border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
              <UIcon name="i-heroicons-folder" class="w-4 h-4 text-gray-400 shrink-0" />
              <div class="flex-1 min-w-0">
                <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ dP(proj.project) }}</span>
                <span class="text-xs text-gray-400 dark:text-gray-500 ml-2">{{ dO(proj.organization) }}</span>
              </div>
              <span class="text-xs text-gray-400 dark:text-gray-500">{{ proj.work_item_types.join(', ') }}</span>
              <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-trash"
                @click="confirmRemoveProject(proj)" />
            </div>
          </div>
        </div>
      </div>

      <!-- Sprint Configurations -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mt-4">
        <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700 flex items-center justify-between">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Sprint Views</h3>
          <UButton size="xs" icon="i-heroicons-plus" @click="showAddSprintConfig = true" :disabled="store.config.projects.length === 0">Add Sprint View</UButton>
        </div>
        <div class="px-5 py-4">
          <p class="text-xs text-gray-500 dark:text-gray-400 mb-3">
            Define project + team pairs to use as sprint timeline views. These appear in the view switcher on the Roadmap tab alongside the Quarter overview.
          </p>
          <div v-if="store.config.sprint_configs.length === 0" class="text-sm text-gray-400 dark:text-gray-500 py-4 text-center">
            No sprint views configured. A default sprint view will be available if projects are configured.
          </div>
          <div v-else class="space-y-2">
            <div v-for="sc in store.config.sprint_configs" :key="sc.id"
              class="rounded-lg border border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/40 transition-colors">
              <div class="flex items-center gap-3 px-3 py-2">
                <UButton variant="ghost" color="neutral" size="xs"
                  :icon="expandedSprintConfig === sc.id ? 'i-heroicons-chevron-down' : 'i-heroicons-chevron-right'"
                  @click="toggleExpandSprintConfig(sc.id)" />
                <UIcon name="i-heroicons-calendar-days" class="w-4 h-4 text-gray-400 shrink-0" />
                <div class="flex-1 min-w-0">
                  <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ dP(sc.project) }} – {{ dT(sc.team) }}</span>
                  <span class="text-xs text-gray-400 dark:text-gray-500 ml-2">{{ dO(sc.organization) }}</span>
                  <span v-if="(sc.excluded_sprints || []).length > 0" class="text-xs text-amber-500 dark:text-amber-400 ml-2">{{ (sc.excluded_sprints || []).length }} hidden</span>
                </div>
                <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-trash"
                  @click="removeSprintConfig(sc.id)" />
              </div>
              <!-- Expanded sprint list -->
              <div v-if="expandedSprintConfig === sc.id" class="px-3 pb-3">
                <div v-if="expandedSprintData.loading" class="flex items-center gap-2 py-2 text-xs text-gray-400">
                  <UIcon name="i-heroicons-arrow-path" class="w-3 h-3 animate-spin" /> Loading sprints…
                </div>
                <div v-else-if="expandedSprintData.sprints.length === 0" class="text-xs text-gray-400 py-2">
                  No sprints found for this team.
                </div>
                <div v-else class="max-h-[200px] overflow-y-auto space-y-1 mt-1">
                  <label v-for="sprint in expandedSprintData.sprints" :key="sprint.path"
                    class="flex items-center gap-2 px-2 py-1 rounded text-xs cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700/40">
                    <input type="checkbox"
                      :checked="!(sc.excluded_sprints || []).includes(sprint.path)"
                      @change="toggleSprintExclusion(sc.id, sprint.path)"
                      class="rounded border-gray-300 dark:border-gray-600 w-3.5 h-3.5" />
                    <span class="text-gray-700 dark:text-gray-300">{{ sprint.label }}</span>
                    <span class="text-gray-400 dark:text-gray-500 ml-auto">{{ formatSprintDates(sprint) }}</span>
                  </label>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Quarter Overview Configuration -->
      <div class="bg-white dark:bg-gray-800 rounded-xl border border-gray-200 dark:border-gray-700 shadow-sm mt-4">
        <div class="px-5 py-4 border-b border-gray-100 dark:border-gray-700">
          <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-300">Quarter Overview</h3>
          <p class="text-xs text-gray-500 dark:text-gray-400 mt-1">
            Select which quarters to show in the Quarter overview. Default: current quarter + 1 past + 3 future.
          </p>
        </div>
        <div class="px-5 py-4">
          <div class="space-y-3">
            <div v-for="year in quarterConfigYears" :key="year" class="flex items-start gap-4">
              <label class="flex items-center gap-2 w-16 shrink-0 cursor-pointer">
                <input type="checkbox"
                  :checked="isYearFullySelected(year)"
                  :indeterminate.prop="isYearPartiallySelected(year)"
                  @change="toggleYear(year)"
                  class="rounded border-gray-300 dark:border-gray-600 w-3.5 h-3.5" />
                <span class="text-sm font-medium text-gray-700 dark:text-gray-300">{{ year }}</span>
              </label>
              <div class="flex flex-wrap gap-3">
                <label v-for="q in 4" :key="`${year}-Q${q}`"
                  class="flex items-center gap-1.5 text-xs cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700/40 px-2 py-1 rounded">
                  <input type="checkbox"
                    :value="`${year}-Q${q}`"
                    :checked="quarterSelection.includes(`${year}-Q${q}`)"
                    @change="toggleQuarter(`${year}-Q${q}`)"
                    class="rounded border-gray-300 dark:border-gray-600 w-3.5 h-3.5" />
                  <span class="text-gray-700 dark:text-gray-300">Q{{ q }}</span>
                </label>
              </div>
            </div>
          </div>
          <div class="mt-4 flex items-center gap-3">
            <UButton size="xs" variant="outline" color="neutral" icon="i-heroicons-arrow-uturn-left" @click="resetQuarterDefaults">Reset to Default</UButton>
            <UButton size="xs" icon="i-heroicons-document-arrow-down" @click="saveQuarterConfig" :loading="savingQuarterConfig">Save</UButton>
            <span v-if="quarterConfigSaved" class="text-xs text-green-600 dark:text-green-400">Saved!</span>
          </div>

          <!-- Sprint overlay toggle -->
          <div class="mt-5 pt-4 border-t border-gray-100 dark:border-gray-700">
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" v-model="sprintOverlayEnabled"
                @change="saveSprintOverlayToggle"
                class="rounded border-gray-300 dark:border-gray-600 w-4 h-4" />
              <div>
                <span class="text-sm font-medium text-gray-700 dark:text-gray-300">Show sprint overlay</span>
                <p class="text-xs text-gray-500 dark:text-gray-400">Display sprint boundaries on top of the quarter grid as visual guides for team cadence.</p>
              </div>
            </label>
          </div>
        </div>
      </div>

      <!-- Add Sprint Config Modal -->
      <UModal v-model:open="showAddSprintConfig" title="Add Sprint View" description="Add a project + team combination as a sprint timeline view." :ui="{ overlay: 'z-[9998]', content: 'z-[9999]' }">
        <template #body>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
              <USelectMenu v-model="newSprintConfig.project_id" :items="sprintProjectOptions" value-key="value"
                placeholder="Choose a project…" class="w-full" />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Team</label>
              <USelectMenu v-model="newSprintConfig.team" :items="newSprintConfigTeams" value-key="value"
                placeholder="Choose a team…" :loading="loadingNewConfigTeams" :disabled="!newSprintConfig.project_id || loadingNewConfigTeams" class="w-full" />
            </div>
          </div>
        </template>
        <template #footer>
          <div class="flex justify-end gap-2">
            <UButton variant="outline" color="neutral" @click="showAddSprintConfig = false">Cancel</UButton>
            <UButton :disabled="!newSprintConfig.project_id || !newSprintConfig.team" @click="doAddSprintConfig">Add</UButton>
          </div>
        </template>
      </UModal>
    </div>

    <!-- Add Project Modal -->
    <UModal v-model:open="showAddProject" title="Add Project" description="Add an Azure DevOps project to the roadmap." :ui="{ overlay: 'z-[9998]', content: 'z-[9999]' }">
      <template #body>
        <div class="space-y-4">
          <UAlert v-if="addProjectError" color="error" icon="i-heroicons-exclamation-triangle" :description="addProjectError" class="mb-2" />
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Organization</label>
            <USelectMenu v-model="newProject.organization" :items="orgOptions" value-key="value"
              placeholder="Choose organization…" class="w-full" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Project</label>
            <USelectMenu v-model="newProject.project_id" :items="devopsProjectOptions" value-key="value"
              placeholder="Choose project…" :loading="loadingProjects" :disabled="!newProject.organization || loadingProjects" class="w-full" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Work Item Types</label>
            <p class="text-xs text-gray-500 dark:text-gray-400 mb-2">Select which types to show on the roadmap.</p>
            <div class="flex flex-wrap gap-2">
              <label v-for="t in availableTypes" :key="t" class="inline-flex items-center gap-1.5 text-sm">
                <input type="checkbox" :value="t" v-model="newProject.work_item_types"
                  class="rounded border-gray-300 dark:border-gray-600" />
                {{ t }}
              </label>
            </div>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" color="neutral" @click="showAddProject = false">Cancel</UButton>
          <UButton :disabled="!newProject.organization || !newProject.project_id" :loading="savingProject" @click="doAddProject">Add</UButton>
        </div>
      </template>
    </UModal>

    <!-- Remove Project Confirm Modal -->
    <UModal v-model:open="showRemoveProject" title="Remove Project" description="Remove a project from the roadmap." :ui="{ overlay: 'z-[9998]', content: 'z-[9999]' }">
      <template #body>
        <p class="text-sm text-gray-700 dark:text-gray-300">
          Remove <strong>{{ removingProject?.project }}</strong> ({{ removingProject?.organization }}) from the roadmap?
          This only removes it from the roadmap view — no work items are deleted.
        </p>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton variant="outline" color="neutral" @click="showRemoveProject = false">Cancel</UButton>
          <UButton color="error" :loading="savingProject" @click="doRemoveProject">Remove</UButton>
        </div>
      </template>
    </UModal>

    <!-- Link Confirmation Popup -->
    <UModal v-model:open="showLinkConfirm" title="Create Dependency Link" description="Confirm the dependency link between two work items." :ui="{ overlay: 'z-[9998]', content: 'z-[9999]' }">
      <template #body>
        <div v-if="linkSourceItem && linkTargetItem" class="space-y-4">
          <div class="rounded-lg border border-gray-200 dark:border-gray-600 p-4 space-y-3">
            <div>
              <span class="text-xs font-bold uppercase tracking-wider text-primary-500 dark:text-primary-400">Predecessor</span>
              <p class="text-base font-semibold text-gray-900 dark:text-gray-100 mt-0.5">{{ dTitle(linkSourceItem.title) }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ linkSourceItem.work_item_type }} #{{ linkSourceItem.id }}</p>
            </div>
            <div class="flex items-center gap-2 text-gray-400 dark:text-gray-300">
              <UIcon name="i-heroicons-arrow-down" class="w-4 h-4" />
              <span class="text-xs text-gray-500 dark:text-gray-400">blocks</span>
            </div>
            <div>
              <span class="text-xs font-bold uppercase tracking-wider text-primary-500 dark:text-primary-400">Successor</span>
              <p class="text-base font-semibold text-gray-900 dark:text-gray-100 mt-0.5">{{ dTitle(linkTargetItem.title) }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{{ linkTargetItem.work_item_type }} #{{ linkTargetItem.id }}</p>
            </div>
          </div>
          <div class="flex justify-center">
            <UButton variant="soft" color="primary" size="xs" icon="i-heroicons-arrows-up-down" label="Swap Direction" @click="swapLinkDirection" />
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3">
          <UButton variant="outline" color="neutral" @click="cancelLink">Cancel</UButton>
          <UButton icon="i-heroicons-link" @click="confirmLink">Confirm Link</UButton>
        </div>
      </template>
    </UModal>

    <!-- Changes Preview Modal -->
    <UModal v-model:open="showChangesPreview" title="Unsaved Changes" description="Preview of pending changes before pushing to Azure DevOps." :ui="{ overlay: 'z-[9998]', content: 'z-[9999]' }">
      <template #body>
        <div v-if="changesPreview.length === 0" class="text-sm text-gray-500 dark:text-gray-400 py-4 text-center">
          No unsaved changes.
        </div>
        <div v-else class="space-y-2 max-h-[400px] overflow-y-auto">
          <div v-for="change in changesPreview" :key="change.id"
            class="flex items-start gap-3 px-3 py-2.5 rounded-lg border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-700/30">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <span class="w-2 h-2 rounded-full shrink-0" :class="projectColorClass(change.project)"></span>
                <span class="text-sm font-medium text-gray-700 dark:text-gray-300 truncate">{{ change.title }}</span>
                <span class="text-xs text-gray-400">#{{ change.id }}</span>
              </div>
              <div class="mt-1.5 pl-4 space-y-1">
                <div v-if="change.startDateChanged" class="flex items-center gap-2 text-xs">
                  <span class="text-gray-500 dark:text-gray-400 w-16 shrink-0">Start:</span>
                  <span class="text-red-500 dark:text-red-400 line-through">{{ change.originalStartDate || '(none)' }}</span>
                  <UIcon name="i-heroicons-arrow-right" class="w-3 h-3 text-gray-400" />
                  <span class="text-green-600 dark:text-green-400">{{ change.newStartDate || '(none)' }}</span>
                </div>
                <div v-if="change.targetDateChanged" class="flex items-center gap-2 text-xs">
                  <span class="text-gray-500 dark:text-gray-400 w-16 shrink-0">Target:</span>
                  <span class="text-red-500 dark:text-red-400 line-through">{{ change.originalTargetDate || '(none)' }}</span>
                  <UIcon name="i-heroicons-arrow-right" class="w-3 h-3 text-gray-400" />
                  <span class="text-green-600 dark:text-green-400">{{ change.newTargetDate || '(none)' }}</span>
                </div>
                <div v-if="change.linksAdded" class="text-xs text-green-600 dark:text-green-400">
                  +{{ change.linksAdded }} link{{ change.linksAdded > 1 ? 's' : '' }} added
                </div>
                <div v-if="change.linksRemoved" class="text-xs text-red-500 dark:text-red-400">
                  −{{ change.linksRemoved }} link{{ change.linksRemoved > 1 ? 's' : '' }} removed
                </div>
              </div>
            </div>
            <UButton variant="ghost" color="neutral" size="xs" icon="i-heroicons-arrow-uturn-left"
              @click="revertChange(change.id)" title="Revert this change" />
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3">
          <UButton variant="outline" color="neutral" icon="i-heroicons-arrow-uturn-left" @click="revertAllChanges" :disabled="changesPreview.length === 0">Revert All</UButton>
          <UButton @click="showChangesPreview = false">Close</UButton>
        </div>
      </template>
    </UModal>

    <!-- Push Results Modal -->
    <UModal v-model:open="showPushResults" title="Push Results" description="Summary of changes pushed to Azure DevOps." :ui="{ overlay: 'z-[9998]', content: 'z-[9999]' }">
      <template #body>
        <div v-if="pushResults.length > 0" class="space-y-2 max-h-[300px] overflow-y-auto">
          <div v-for="r in pushResults" :key="r.id"
            class="flex items-center gap-2 px-3 py-2 rounded-lg text-sm"
            :class="r.ok ? 'bg-green-50 dark:bg-green-900/20' : 'bg-red-50 dark:bg-red-900/20'"
          >
            <UIcon :name="r.ok ? 'i-heroicons-check-circle' : 'i-heroicons-x-circle'"
              class="w-4 h-4 shrink-0" :class="r.ok ? 'text-green-500' : 'text-red-500'" />
            <span class="text-gray-700 dark:text-gray-300">#{{ r.id }}</span>
            <span v-if="!r.ok" class="text-xs text-red-600 dark:text-red-400 truncate">{{ r.error }}</span>
            <span v-else class="text-xs text-green-600 dark:text-green-400">Updated</span>
          </div>
        </div>
        <div class="mt-3 pt-3 border-t border-gray-100 dark:border-gray-700 flex items-center gap-4 text-sm">
          <span class="text-green-600 dark:text-green-400">{{ pushResults.filter(r => r.ok).length }} succeeded</span>
          <span v-if="pushResults.filter(r => !r.ok).length" class="text-red-600 dark:text-red-400">{{ pushResults.filter(r => !r.ok).length }} failed</span>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end">
          <UButton @click="showPushResults = false">Close</UButton>
        </div>
      </template>
    </UModal>

    <!-- Create work item modal -->
    <RoadmapCreateModal
      :open="showCreateModal"
      @update:open="showCreateModal = $event"
      :work-item-type="createModalType"
      :parent-id="createModalParentId"
      :parent-title="createModalParentTitle"
      :project="createModalProject"
      @created="onItemCreated"
    />

    <!-- Work item detail modal -->
    <RoadmapDetailModal
      :open="showDetailModal"
      @update:open="showDetailModal = $event"
      :work-item-id="detailWorkItemId"
      :project-id="detailProjectId"
      :organization="detailOrganization"
      :project-name="detailProjectName"
      @updated="onWorkItemUpdated"
    />
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, nextTick, provide, defineAsyncComponent } from 'vue'
import { useRoadmapStore } from '../stores/roadmap.js'
import { useMonitorStore } from '../stores/monitor.js'
import { useApi } from '../composables/useApi.js'
import { useDemoMode, anonProject, anonOrg, anonTeam, anonPrTitle, anonSprint } from '../composables/useDemoMode.js'
import RoadmapCard from '../components/RoadmapCard.vue'
const RoadmapCreateModal = defineAsyncComponent(() => import('../components/RoadmapCreateModal.vue'))
const RoadmapDetailModal = defineAsyncComponent(() => import('../components/RoadmapDetailModal.vue'))

const api = useApi()
const store = useRoadmapStore()
const { isDemoMode } = useDemoMode()

// Demo mode display wrappers
const dP = (v) => isDemoMode.value && v ? anonProject(v) : v
const dT = (v) => isDemoMode.value && v ? anonTeam(v) : v
const dO = (v) => isDemoMode.value && v ? anonOrg(v) : v
const dTitle = (v) => isDemoMode.value && v ? anonPrTitle(v) : v
const dS = (v) => isDemoMode.value && v ? anonSprint(v) : v

const tabs = [
  { label: 'Roadmap', value: 'roadmap', icon: 'i-heroicons-map' },
  { label: 'Configuration', value: 'config', icon: 'i-heroicons-cog-6-tooth' },
]
const activeTab = ref('roadmap')

// --- Project modals ---
const showAddProject = ref(false)
const showRemoveProject = ref(false)
const savingProject = ref(false)
const removingProject = ref(null)
const addProjectError = ref('')
const newProject = ref({ organization: '', project_id: '', project: '', work_item_types: ['Epic', 'Feature'] })

const monitorStore = useMonitorStore()
const loadingProjects = ref(false)
const orgOptions = computed(() => monitorStore.organizations.map(o => ({ value: o.name, label: o.name })))
const devopsProjectOptions = ref([])
const availableTypes = ['Epic', 'Feature', 'User Story', 'Bug']

watch(() => newProject.value.organization, async (org) => {
  newProject.value.project_id = ''
  newProject.value.project = ''
  devopsProjectOptions.value = []
  if (!org) return
  loadingProjects.value = true
  try {
    const data = await api.get(`/api/devops/organizations/${encodeURIComponent(org)}/projects`)
    devopsProjectOptions.value = (data || []).map(p => ({ value: p.id, label: p.name }))
  } catch { /* ignore */ }
  finally { loadingProjects.value = false }
})

watch(() => newProject.value.project_id, (id) => {
  const opt = devopsProjectOptions.value.find(p => p.value === id)
  newProject.value.project = opt?.label || ''
})

function confirmRemoveProject(proj) {
  removingProject.value = proj
  showRemoveProject.value = true
}

async function doAddProject() {
  savingProject.value = true
  addProjectError.value = ''
  try {
    await store.addProject({
      organization: newProject.value.organization,
      project: newProject.value.project,
      project_id: newProject.value.project_id,
      work_item_types: newProject.value.work_item_types,
    })
    showAddProject.value = false
    newProject.value = { organization: '', project_id: '', project: '', work_item_types: ['Epic', 'Feature'] }
  } catch (e) {
    addProjectError.value = e.message || 'Failed to add project'
  } finally {
    savingProject.value = false
  }
}

async function doRemoveProject() {
  savingProject.value = true
  try {
    await store.removeProject(removingProject.value.organization, removingProject.value.project_id)
    showRemoveProject.value = false
    removingProject.value = null
  } finally {
    savingProject.value = false
  }
}

// --- View switching ---
const activeView = ref(store.config.active_view || 'quarters')

const sprintProjectOptions = computed(() => {
  return store.config.projects.map(p => ({ value: p.project_id, label: dP(p.project) }))
})

// Build view options: "Quarter overview" + one entry per sprint config
const viewOptions = computed(() => {
  const opts = [{ value: 'quarters', label: 'Quarter overview' }]
  const configs = store.config.sprint_configs || []
  for (const sc of configs) {
    opts.push({ value: sc.id, label: `${dT(sc.team)} sprints` })
  }
  return opts
})

// The effective column mode derived from activeView
const columnMode = computed(() => activeView.value === 'quarters' ? 'quarters' : 'sprints')

// The active sprint config (resolved from view)
const activeSprintConfig = computed(() => {
  if (activeView.value === 'quarters') return null
  return (store.config.sprint_configs || []).find(sc => sc.id === activeView.value) || null
})

watch(activeView, async (val) => {
  if (val !== store.config.active_view) {
    await store.saveConfig({ ...store.config, active_view: val, column_mode: val === 'quarters' ? 'quarters' : 'sprints' })
  }
  if (val !== 'quarters') {
    loadSprints()
  }
})

// --- Quarter config management (config tab) ---
const savingQuarterConfig = ref(false)
const quarterConfigSaved = ref(false)

// Generate default quarter selection: current + 1 past + 3 future
function getDefaultQuarters() {
  const now = new Date()
  const curYear = now.getFullYear()
  const curQ = Math.floor(now.getMonth() / 3) + 1
  const defaults = []
  for (let offset = -1; offset <= 3; offset++) {
    let q = curQ + offset
    let y = curYear
    while (q > 4) { q -= 4; y++ }
    while (q < 1) { q += 4; y-- }
    defaults.push(`${y}-Q${q}`)
  }
  return defaults.sort()
}

// Local reactive selection (initialized from config)
const quarterSelection = ref([...(store.config.visible_quarters?.length ? store.config.visible_quarters : getDefaultQuarters())])

// Years to show: from 1 year ago to 2 years ahead
const quarterConfigYears = computed(() => {
  const now = new Date()
  const curYear = now.getFullYear()
  return [curYear - 1, curYear, curYear + 1, curYear + 2]
})

function isYearFullySelected(year) {
  return [1, 2, 3, 4].every(q => quarterSelection.value.includes(`${year}-Q${q}`))
}

function isYearPartiallySelected(year) {
  const count = [1, 2, 3, 4].filter(q => quarterSelection.value.includes(`${year}-Q${q}`)).length
  return count > 0 && count < 4
}

function toggleYear(year) {
  const allKeys = [1, 2, 3, 4].map(q => `${year}-Q${q}`)
  if (isYearFullySelected(year)) {
    quarterSelection.value = quarterSelection.value.filter(k => !allKeys.includes(k))
  } else {
    const existing = new Set(quarterSelection.value)
    allKeys.forEach(k => existing.add(k))
    quarterSelection.value = [...existing].sort()
  }
}

function toggleQuarter(key) {
  const idx = quarterSelection.value.indexOf(key)
  if (idx >= 0) {
    quarterSelection.value.splice(idx, 1)
  } else {
    quarterSelection.value.push(key)
    quarterSelection.value.sort()
  }
}

function resetQuarterDefaults() {
  quarterSelection.value = getDefaultQuarters()
}

async function saveQuarterConfig() {
  savingQuarterConfig.value = true
  quarterConfigSaved.value = false
  try {
    const updatedConfig = { ...store.config, visible_quarters: [...quarterSelection.value] }
    await store.saveConfig(updatedConfig)
    quarterConfigSaved.value = true
    setTimeout(() => { quarterConfigSaved.value = false }, 3000)
  } finally {
    savingQuarterConfig.value = false
  }
}

// Sync local selection when config is loaded
watch(() => store.config.visible_quarters, (val) => {
  if (val && val.length > 0) {
    quarterSelection.value = [...val]
  }
})

// --- Sprint overlay on quarter view ---
const sprintOverlayEnabled = ref(store.config.show_sprint_overlay || false)
const allSprintData = ref([]) // all sprints from all configs for overlay

watch(() => store.config.show_sprint_overlay, (val) => {
  sprintOverlayEnabled.value = !!val
})

async function saveSprintOverlayToggle() {
  await store.saveConfig({ ...store.config, show_sprint_overlay: sprintOverlayEnabled.value })
  if (sprintOverlayEnabled.value) loadAllSprints()
}

async function loadAllSprints() {
  const configs = store.config.sprint_configs || []
  if (configs.length === 0) { allSprintData.value = []; return }
  const results = []
  for (const sc of configs) {
    const team = sc.team || sc.project + ' Team'
    try {
      const resp = await api.get(`/api/roadmap/iterations?organization=${encodeURIComponent(sc.organization)}&project=${encodeURIComponent(sc.project)}&team=${encodeURIComponent(team)}`)
      const excluded = new Set(sc.excluded_sprints || [])
      for (const s of (resp.iterations || [])) {
        if (s.startDate && s.finishDate && !excluded.has(s.path)) {
          results.push({ ...s, team: sc.team, project: sc.project, configId: sc.id })
        }
      }
    } catch { /* skip failed loads */ }
  }
  allSprintData.value = results.sort((a, b) => new Date(a.startDate) - new Date(b.startDate))
}

// Sprint overlay bar computed — positions sprints proportionally within the visible quarter range
// Sprint overlay rows — one row per team config, each with positioned sprint markers
const sprintOverlayRows = computed(() => {
  if (!sprintOverlayEnabled.value || columnMode.value !== 'quarters') return []
  if (allSprintData.value.length === 0) return []

  const cols = timelineColumns.value.filter(c => c.key !== '_unplanned' && c.startDate && c.finishDate)
  if (cols.length === 0) return []

  const rangeStart = new Date(Math.min(...cols.map(c => new Date(c.startDate))))
  const rangeEnd = new Date(Math.max(...cols.map(c => new Date(c.finishDate))))
  const totalDays = Math.max(1, (rangeEnd - rangeStart) / (1000 * 60 * 60 * 24))

  // Group sprints by configId (team)
  const grouped = new Map()
  for (const s of allSprintData.value) {
    if (!grouped.has(s.configId)) grouped.set(s.configId, { team: s.team, project: s.project, sprints: [] })
    grouped.get(s.configId).sprints.push(s)
  }

  const rows = []
  for (const [configId, { team, project, sprints }] of grouped) {
    // Sort sprints chronologically
    const sorted = [...sprints]
      .filter(s => new Date(s.finishDate) >= rangeStart && new Date(s.startDate) <= rangeEnd)
      .sort((a, b) => new Date(a.startDate) - new Date(b.startDate))

    const markers = []
    for (let i = 0; i < sorted.length; i++) {
      const s = sorted[i]
      const sStart = new Date(s.startDate)
      const sEnd = new Date(s.finishDate)
      const clampedStart = new Date(Math.max(sStart, rangeStart))
      // Extend end to next sprint's start to eliminate gaps, or use own end if last
      const nextStart = i < sorted.length - 1 ? new Date(sorted[i + 1].startDate) : null
      const effectiveEnd = nextStart && nextStart <= rangeEnd && nextStart > sEnd ? nextStart : sEnd
      const clampedEnd = new Date(Math.min(effectiveEnd, rangeEnd))
      const leftPct = ((clampedStart - rangeStart) / (1000 * 60 * 60 * 24)) / totalDays * 100
      const widthPct = ((clampedEnd - clampedStart) / (1000 * 60 * 60 * 24)) / totalDays * 100
      const label = dS(s.path.split('\\').pop() || s.path)
      markers.push({ key: s.path + '|' + configId, label, left: leftPct, width: widthPct })
    }
    const colorClass = projectColorClass(project)
    if (markers.length > 0) rows.push({ configId, team, project, colorClass, markers })
  }
  return rows
})

// --- Sprint config management (config tab) ---
const showAddSprintConfig = ref(false)
const newSprintConfig = ref({ project_id: '', team: '' })
const newSprintConfigTeams = ref([])
const loadingNewConfigTeams = ref(false)

watch(() => newSprintConfig.value.project_id, async (val) => {
  newSprintConfig.value.team = ''
  newSprintConfigTeams.value = []
  if (!val) return
  const proj = store.config.projects.find(p => p.project_id === val)
  if (!proj) return
  loadingNewConfigTeams.value = true
  try {
    const data = await api.get(`/api/devops/organizations/${encodeURIComponent(proj.organization)}/projects/${encodeURIComponent(proj.project_id)}/teams`)
    newSprintConfigTeams.value = (data || []).map(t => ({ value: t.name, label: t.name }))
  } catch { /* ignore */ }
  finally { loadingNewConfigTeams.value = false }
})

async function doAddSprintConfig() {
  const proj = store.config.projects.find(p => p.project_id === newSprintConfig.value.project_id)
  if (!proj) return
  const sc = {
    id: crypto.randomUUID(),
    project_id: proj.project_id,
    organization: proj.organization,
    project: proj.project,
    team: newSprintConfig.value.team,
  }
  const configs = [...(store.config.sprint_configs || []), sc]
  await store.saveConfig({ ...store.config, sprint_configs: configs })
  showAddSprintConfig.value = false
  newSprintConfig.value = { project_id: '', team: '' }
}

async function removeSprintConfig(id) {
  const configs = (store.config.sprint_configs || []).filter(sc => sc.id !== id)
  await store.saveConfig({ ...store.config, sprint_configs: configs })
  if (activeView.value === id) activeView.value = 'quarters'
}

// --- Sprint exclusion management ---
const expandedSprintConfig = ref(null)
const expandedSprintData = ref({ loading: false, sprints: [] })

async function toggleExpandSprintConfig(id) {
  if (expandedSprintConfig.value === id) {
    expandedSprintConfig.value = null
    return
  }
  expandedSprintConfig.value = id
  expandedSprintData.value = { loading: true, sprints: [] }
  const sc = (store.config.sprint_configs || []).find(c => c.id === id)
  if (!sc) return
  try {
    const resp = await api.get(`/api/roadmap/iterations?organization=${encodeURIComponent(sc.organization)}&project=${encodeURIComponent(sc.project)}&team=${encodeURIComponent(sc.team)}`)
    expandedSprintData.value.sprints = (resp.iterations || [])
      .filter(s => s.startDate && s.finishDate)
      .sort((a, b) => new Date(a.startDate) - new Date(b.startDate))
      .map(s => ({ path: s.path, label: dS(s.path.split('\\').pop() || s.path), startDate: s.startDate, finishDate: s.finishDate }))
  } catch { expandedSprintData.value.sprints = [] }
  finally { expandedSprintData.value.loading = false }
}

function formatSprintDates(sprint) {
  const fmt = d => new Date(d).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
  return `${fmt(sprint.startDate)} – ${fmt(sprint.finishDate)}`
}

async function toggleSprintExclusion(configId, sprintPath) {
  const configs = [...(store.config.sprint_configs || [])]
  const sc = configs.find(c => c.id === configId)
  if (!sc) return
  const excluded = [...(sc.excluded_sprints || [])]
  const idx = excluded.indexOf(sprintPath)
  if (idx >= 0) excluded.splice(idx, 1)
  else excluded.push(sprintPath)
  sc.excluded_sprints = excluded
  await store.saveConfig({ ...store.config, sprint_configs: configs })
}

// --- Filters ---
const searchFilter = ref('')
const projectFilter = ref([])
const stateFilter = ref([])
const showUnplanned = ref(true)

// --- Create / Detail modals ---
const showCreateModal = ref(false)
const createModalType = ref('Epic')
const createModalParentId = ref(null)
const createModalParentTitle = ref('')
const createModalProject = ref('')

const showDetailModal = ref(false)
const detailWorkItemId = ref(null)
const detailProjectId = ref('')
const detailOrganization = ref('')
const detailProjectName = ref('')

function openCreateEpic() {
  createModalType.value = 'Epic'
  createModalParentId.value = null
  createModalParentTitle.value = ''
  createModalProject.value = ''
  showCreateModal.value = true
}

function openCreateFeature(epic) {
  createModalType.value = 'Feature'
  createModalParentId.value = epic.id
  createModalParentTitle.value = epic.title
  createModalProject.value = epic.project
  showCreateModal.value = true
}

function onItemCreated(item) {
  store.items.push(item)
}

function openDetail(item) {
  const proj = store.config.projects.find(p => p.project === item.project)
  if (!proj) return
  detailWorkItemId.value = item.id
  detailProjectId.value = proj.project_id
  detailOrganization.value = proj.organization
  detailProjectName.value = proj.project
  showDetailModal.value = true
}

function onWorkItemUpdated(updated) {
  // Refresh the roadmap data
  store.fetchItems()
}

const projectFilterOptions = computed(() => {
  return store.config.projects.map(p => ({ value: p.project, label: dP(p.project) }))
})

const stateFilterOptions = computed(() => {
  const states = new Set(store.items.filter(i => !i.error).map(i => i.state).filter(Boolean))
  return [...states].map(s => ({ value: s, label: s }))
})

function matchesFilters(item) {
  if (item.error) return false
  if (projectFilter.value.length && !projectFilter.value.includes(item.project)) return false
  if (stateFilter.value.length && !stateFilter.value.includes(item.state)) return false
  if (searchFilter.value) {
    const q = searchFilter.value.toLowerCase()
    if (!item.title?.toLowerCase().includes(q) && !String(item.id).includes(q)) return false
  }
  return true
}

// --- Epics & Features ---
const epics = computed(() => {
  // Collect IDs of epics that are parents of features matching the project filter
  const parentIdsOfFilteredFeatures = new Set()
  if (projectFilter.value.length) {
    for (const f of store.items) {
      if (f.work_item_type === 'Epic' || f.error || !f.parent_id) continue
      if (projectFilter.value.includes(f.project)) {
        parentIdsOfFilteredFeatures.add(f.parent_id)
      }
    }
  }

  const filtered = store.items.filter(i => {
    if (i.work_item_type !== 'Epic' || i.error) return false
    if (projectFilter.value.length && !projectFilter.value.includes(i.project) && !parentIdsOfFilteredFeatures.has(i.id)) return false
    return true
  })

  // Single pass over features to compute earliest dates AND matching parents
  const epicEarliestDate = new Map() // epic id -> earliest start_date timestamp
  const matchingParents = new Set()
  const needMatching = !!(searchFilter.value || stateFilter.value.length)

  for (const f of store.items) {
    if (f.work_item_type === 'Epic' || f.error || !f.parent_id) continue
    if (needMatching && matchesFilters(f)) matchingParents.add(f.parent_id)
    // Check if feature is planned (has dates that fit a visible column)
    if (itemColumn(f) === '_unplanned') continue
    if (!f.start_date) continue
    const d = new Date(f.start_date)
    if (isNaN(d)) continue
    const ts = d.getTime()
    const prev = epicEarliestDate.get(f.parent_id)
    if (!prev || ts < prev) epicEarliestDate.set(f.parent_id, ts)
  }

  filtered.sort((a, b) => {
    const aDate = epicEarliestDate.get(a.id)
    const bDate = epicEarliestDate.get(b.id)
    // Epics with planned features first, sorted by earliest start_date
    if (aDate != null && bDate == null) return -1
    if (aDate == null && bDate != null) return 1
    if (aDate != null && bDate != null) return aDate - bDate
    return 0
  })

  // When unplanned is hidden, hide epics that have no features in timeline columns
  if (!showUnplanned.value) {
    return filtered.filter(e => epicEarliestDate.has(e.id))
  }

  // When search or state filter is active, hide epics with no matching child features
  if (needMatching) {
    return filtered.filter(e => matchingParents.has(e.id))
  }

  return filtered
})

const features = computed(() => {
  return store.items.filter(i => i.work_item_type !== 'Epic' && !i.error)
})

const epicIdSet = computed(() => new Set(epics.value.map(e => e.id)))

const unparentedFeatures = computed(() => {
  return features.value.filter(f => !f.parent_id || !epicIdSet.value.has(f.parent_id))
})

// --- Timeline columns ---
const sprintData = ref([]) // { path, startDate, finishDate }

function generateQuarterColumns() {
  const configured = store.config.visible_quarters || []

  // Default: current quarter + 1 past + 3 future
  let selected = configured
  if (selected.length === 0) {
    const now = new Date()
    const curYear = now.getFullYear()
    const curQ = Math.floor(now.getMonth() / 3) + 1
    selected = []
    for (let offset = -1; offset <= 3; offset++) {
      let q = curQ + offset
      let y = curYear
      while (q > 4) { q -= 4; y++ }
      while (q < 1) { q += 4; y-- }
      selected.push(`${y}-Q${q}`)
    }
  }

  // Generate columns for selected quarters (sorted chronologically)
  selected = [...selected].sort()
  const cols = []
  for (const key of selected) {
    const match = key.match(/^(\d{4})-Q([1-4])$/)
    if (!match) continue
    const y = parseInt(match[1])
    const q = parseInt(match[2])
    const startMonth = (q - 1) * 3
    // Use UTC dates to avoid timezone shifts
    const startDate = new Date(Date.UTC(y, startMonth, 1)).toISOString()
    const finishDate = new Date(Date.UTC(y, startMonth + 3, 0)).toISOString()
    cols.push({ key, label: `Q${q} ${y}`, startDate, finishDate })
  }
  return cols
}

function generateSprintColumns() {
  if (sprintData.value.length === 0) return [{ key: '_none', label: 'No sprints loaded' }]
  const now = new Date()

  // Get excluded sprints from active config
  const sc = activeSprintConfig.value
  const excluded = new Set(sc?.excluded_sprints || [])

  const sorted = [...sprintData.value]
    .filter(s => s.startDate && s.finishDate && !excluded.has(s.path))
    .sort((a, b) => new Date(a.startDate) - new Date(b.startDate))

  const currentIdx = sorted.findIndex(s => new Date(s.finishDate) >= now)
  const start = Math.max(0, currentIdx - 2)
  const end = Math.min(sorted.length, currentIdx + 6)
  let visible = sorted.slice(start, end)
  if (visible.length === 0) visible = sorted.slice(0, 8)

  return visible.map(s => ({
    key: s.path,
    label: dS(s.path.split('\\').pop() || s.path),
    startDate: s.startDate,
    finishDate: s.finishDate,
  }))
}

const timelineColumns = computed(() => {
  const mode = columnMode.value
  const cols = mode === 'sprints' ? generateSprintColumns() : generateQuarterColumns()
  if (showUnplanned.value) {
    cols.unshift({ key: '_unplanned', label: 'Unplanned' })
  }
  return cols
})

// px per day for sprint timeline scaling
const PX_PER_DAY = 8
const UNPLANNED_WIDTH = 240
const EPIC_COL_WIDTH = 200

// Compute the overall date range for sprint columns
const sprintRange = computed(() => {
  const sprintCols = timelineColumns.value.filter(c => c.key !== '_unplanned' && c.startDate && c.finishDate)
  if (sprintCols.length === 0) return null
  const minDate = new Date(Math.min(...sprintCols.map(c => new Date(c.startDate))))
  const maxDate = new Date(Math.max(...sprintCols.map(c => new Date(c.finishDate))))
  return { min: minDate, max: maxDate }
})

function daysBetween(a, b) {
  return Math.max(1, Math.round((b - a) / (1000 * 60 * 60 * 24)))
}

const gridStyle = computed(() => {
  const mode = columnMode.value
  const hasUnplanned = showUnplanned.value
  if (mode === 'sprints' && sprintRange.value) {
    // Sprint columns: equal-width (1fr) so they scale with the number of sprints
    const sprintCols = timelineColumns.value.filter(c => c.key !== '_unplanned' && !c.isGap)
    const minW = sprintCols.length > 6 ? 100 : 140
    const widths = timelineColumns.value.map(col => {
      if (col.key === '_unplanned') return `${UNPLANNED_WIDTH}px`
      if (col.isGap) return 'minmax(4px, 8px)'
      return `minmax(${minW}px, 1fr)`
    })
    return { gridTemplateColumns: `${EPIC_COL_WIDTH}px ${widths.join(' ')}` }
  }
  // Quarter mode: fixed unplanned width, equal-width quarter columns
  const quarterCols = timelineColumns.value.filter(c => c.key !== '_unplanned')
  if (hasUnplanned) {
    return { gridTemplateColumns: `${EPIC_COL_WIDTH}px ${UNPLANNED_WIDTH}px repeat(${quarterCols.length}, minmax(140px, 1fr))` }
  }
  return { gridTemplateColumns: `${EPIC_COL_WIDTH}px repeat(${quarterCols.length}, minmax(140px, 1fr))` }
})

// Month bar for sprint mode — proportional to how much of each sprint falls in each month
const monthBar = computed(() => {
  const mode = columnMode.value
  if (mode !== 'sprints' || !sprintRange.value) return []
  const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

  const sprintColsOnly = timelineColumns.value.filter(c => c.key !== '_unplanned' && !c.isGap && c.startDate && c.finishDate)
  if (sprintColsOnly.length === 0) return []

  // Each sprint column has equal width (1 unit). Split that unit across months proportionally.
  const monthMap = new Map() // key -> { label, weight }
  for (const col of sprintColsOnly) {
    const start = new Date(col.startDate)
    const end = new Date(col.finishDate)
    const totalDays = Math.max(1, Math.round((end - start) / (1000 * 60 * 60 * 24)))

    // Walk through each month the sprint overlaps
    let cursor = new Date(start.getFullYear(), start.getMonth(), 1)
    while (cursor <= end) {
      const monthStart = new Date(Math.max(cursor, start))
      const nextMonth = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1)
      const monthEnd = new Date(Math.min(nextMonth, end))
      const daysInMonth = Math.max(0, Math.round((monthEnd - monthStart) / (1000 * 60 * 60 * 24)))
      const fraction = daysInMonth / totalDays

      if (fraction > 0) {
        const key = `${cursor.getFullYear()}-${cursor.getMonth()}`
        if (!monthMap.has(key)) {
          monthMap.set(key, { label: `${monthNames[cursor.getMonth()]} ${cursor.getFullYear()}`, weight: 0 })
        }
        monthMap.get(key).weight += fraction
      }
      cursor = nextMonth
    }
  }

  return [...monthMap.values()].map(m => ({ label: m.label, days: m.weight }))
})

// Determine which column an item belongs to
function itemColumn(item) {
  const mode = columnMode.value
  if (mode === 'sprints') {
    const parsed = sprintColsParsed.value
    // Feature is planned if it has at least target_date
    if (item.start_date && item.target_date) {
      const sdMs = new Date(item.start_date).getTime()
      const tdMs = new Date(item.target_date).getTime()
      const col = parsed.find(c => sdMs <= c.finishMs && tdMs >= c.startMs)
      if (col) return col.key
    } else if (item.target_date) {
      const tdMs = new Date(item.target_date).getTime()
      const col = parsed.find(c => c.startMs <= tdMs && c.finishMs >= tdMs)
      if (col) return col.key
    } else if (item.start_date) {
      const sdMs = new Date(item.start_date).getTime()
      const col = parsed.find(c => c.startMs <= sdMs && c.finishMs >= sdMs)
      if (col) return col.key
    }
    return '_unplanned'
  }
  // Quarters mode: use target_date or start_date to find the primary quarter
  const dateStr = item.target_date || item.start_date
  if (dateStr) {
    const d = new Date(dateStr)
    if (!isNaN(d)) {
      const q = Math.ceil((d.getMonth() + 1) / 3)
      const key = `${d.getFullYear()}-Q${q}`
      // Only place in column if it exists in our visible columns
      if (timelineColumns.value.some(c => c.key === key)) return key
    }
  }
  return '_unplanned'
}

// --- Feature bar positioning (Gantt-style for sprint mode) ---
// Returns the sprint columns excluding _unplanned and gaps
const sprintCols = computed(() => {
  return timelineColumns.value.filter(c => c.key !== '_unplanned' && !c.isGap && c.startDate && c.finishDate)
})

// Pre-parsed sprint column dates to avoid repeated Date construction in hot loops
const sprintColsParsed = computed(() => {
  return sprintCols.value.map(c => ({
    key: c.key,
    startMs: new Date(c.startDate).getTime(),
    finishMs: new Date(c.finishDate).getTime(),
  }))
})

// Cumulative equal-width positions for all sprint/gap columns (matches grid proportions)
const sprintColumnPositions = computed(() => {
  const cols = timelineColumns.value.filter(c => c.key !== '_unplanned')
  if (cols.length === 0) return []
  // Equal width for sprint columns, tiny width for gaps
  const widths = cols.map(c => c.isGap ? 0.1 : 1)
  const total = widths.reduce((a, b) => a + b, 0)
  let cumulative = 0
  return cols.map((col, i) => {
    const startPct = (cumulative / total) * 100
    cumulative += widths[i]
    const endPct = (cumulative / total) * 100
    return { key: col.key, label: col.label, isGap: col.isGap, startDate: col.startDate, finishDate: col.finishDate, startPct, endPct }
  })
})

// Total days across all visible sprints (for resize cursor-to-date mapping)
const totalSprintDays = computed(() => {
  if (!sprintRange.value) return 1
  return daysBetween(sprintRange.value.min, sprintRange.value.max)
})

// Quarter column positions (equal-width like sprints)
const quarterColumnPositions = computed(() => {
  const cols = timelineColumns.value.filter(c => c.key !== '_unplanned' && c.startDate && c.finishDate)
  if (cols.length === 0) return []
  const total = cols.length
  return cols.map((col, i) => ({
    key: col.key,
    label: col.label,
    isGap: false,
    startDate: col.startDate,
    finishDate: col.finishDate,
    startPct: (i / total) * 100,
    endPct: ((i + 1) / total) * 100,
  }))
})

// Generic positions for current mode
const columnPositions = computed(() => {
  return columnMode.value === 'sprints' ? sprintColumnPositions.value : quarterColumnPositions.value
})

// Pre-parsed column positions with Date milliseconds for fast comparisons in hot loops
const columnPositionsParsed = computed(() => {
  return columnPositions.value.map(c => ({
    ...c,
    startMs: c.startDate ? new Date(c.startDate).getTime() : NaN,
    finishMs: c.finishDate ? new Date(c.finishDate).getTime() : NaN,
  }))
})

// Compute bar position for a feature: { left%, width% } relative to the timeline area
function featureBarPosition(item) {
  const mode = columnMode.value
  const positions = columnPositionsParsed.value
  if (positions.length === 0) return null
  if (mode === 'sprints' && !sprintRange.value) return null

  // Determine start and end dates
  const startDateStr = item.start_date || item.target_date
  const endDateStr = item.target_date || item.start_date
  if (!startDateStr) return null

  const sdMs = new Date(startDateStr).getTime()
  const tdMs = new Date(endDateStr).getTime()
  if (isNaN(sdMs)) return null

  // In quarter mode, position proportionally WITHIN each column (columns are equal-width in CSS grid)
  if (mode === 'quarters') {
    const cols = positions.filter(c => !c.isGap)
    if (cols.length === 0) return null
    const rangeStartMs = cols[0].startMs
    const rangeEndMs = cols[cols.length - 1].finishMs

    // Clamp dates to visible range
    const clampedStartMs = Math.max(sdMs, rangeStartMs)
    const clampedEndMs = Math.min(isNaN(tdMs) ? sdMs : tdMs, rangeEndMs)
    if (clampedStartMs > rangeEndMs || clampedEndMs < rangeStartMs) return null

    // Map a date (ms) to a percentage that respects equal-width column boundaries
    function dateToPct(ms) {
      for (let i = 0; i < cols.length; i++) {
        const c = cols[i]
        if (ms <= c.finishMs) {
          const colDays = Math.max(1, (c.finishMs - c.startMs) / 86400000)
          const dayInCol = Math.max(0, (ms - c.startMs) / 86400000)
          return c.startPct + (dayInCol / colDays) * (c.endPct - c.startPct)
        }
      }
      return cols[cols.length - 1].endPct
    }

    const leftPct = dateToPct(clampedStartMs)
    const endPct = dateToPct(clampedEndMs)
    // Minimum width so single-day items are visible
    const widthPct = Math.max(endPct - leftPct, 1.5)

    return { left: Math.max(0, leftPct), width: Math.min(widthPct, 100 - leftPct) }
  }

  // Sprint mode: snap to column boundaries
  let startCol = null
  if (!isNaN(sdMs)) {
    startCol = positions.find(c => !c.isGap && c.startMs <= sdMs && c.finishMs >= sdMs)
    if (!startCol) startCol = positions.find(c => !c.isGap && c.startMs >= sdMs)
  }
  if (!startCol) return null

  let endCol = startCol
  if (!isNaN(tdMs)) {
    const found = positions.find(c => !c.isGap && c.startMs <= tdMs && c.finishMs >= tdMs)
    if (found && found.endPct >= startCol.startPct) {
      endCol = found
    } else if (!found) {
      const candidates = positions.filter(c => !c.isGap && c.startMs <= tdMs)
      if (candidates.length > 0) endCol = candidates[candidates.length - 1]
    }
  }

  const leftPct = startCol.startPct
  const widthPct = endCol.endPct - startCol.startPct

  return { left: Math.max(0, leftPct), width: Math.min(widthPct, 100 - leftPct) }
}

// Pre-compute column assignment and filter match for every feature (single pass)
const featureColumnMap = computed(() => {
  const map = new Map() // featureId -> { column, matches }
  for (const f of features.value) {
    map.set(f.id, { column: itemColumn(f), matches: matchesFilters(f) })
  }
  return map
})

// Pre-compute bar positions for all features (avoids calling featureBarPosition 2x per bar)
const barPositions = computed(() => {
  const map = new Map() // featureId -> { left, width } | null
  for (const f of features.value) {
    map.set(f.id, featureBarPosition(f))
  }
  return map
})

// Pre-group features into cells: Map<"epicId|colKey", sorted feature[]>
const cellItemsMap = computed(() => {
  const map = new Map()
  const eIds = epicIdSet.value
  for (const f of features.value) {
    const meta = featureColumnMap.value.get(f.id)
    if (!meta || !meta.matches) continue
    // Determine parent key
    let parentKey
    if (f.parent_id && eIds.has(f.parent_id)) {
      parentKey = f.parent_id
    } else {
      parentKey = null // unparented row
    }
    const cellKey = `${parentKey}|${meta.column}`
    let arr = map.get(cellKey)
    if (!arr) { arr = []; map.set(cellKey, arr) }
    arr.push(f)
  }
  // Sort each cell by date
  for (const arr of map.values()) {
    arr.sort((a, b) => {
      const dateA = new Date(a.start_date || a.target_date || '9999')
      const dateB = new Date(b.start_date || b.target_date || '9999')
      if (dateA.getTime() !== dateB.getTime()) return dateA - dateB
      return a.id - b.id
    })
  }
  return map
})

// Get features for a specific epic that have bar positions (sprint mode)
function epicBarFeatures(epicId) {
  const eIds = epicIdSet.value
  const colMap = featureColumnMap.value
  return features.value.filter(f => {
    if (epicId === null) {
      if (f.parent_id && eIds.has(f.parent_id)) return false
    } else {
      if (f.parent_id !== epicId) return false
    }
    const meta = colMap.get(f.id)
    return meta && meta.column !== '_unplanned' && meta.matches
  }).sort((a, b) => {
    const dateA = new Date(a.start_date || a.target_date || '9999')
    const dateB = new Date(b.start_date || b.target_date || '9999')
    if (dateA.getTime() !== dateB.getTime()) return dateA - dateB
    return a.id - b.id
  })
}

// Get features for a specific cell (parent epic + column)
function cellItems(epicId, colKey) {
  return cellItemsMap.value.get(`${epicId}|${colKey}`) || []
}

// --- Project colors ---
const projectColors = ['bg-sky-500', 'bg-violet-500', 'bg-emerald-500', 'bg-orange-500', 'bg-pink-500', 'bg-cyan-500', 'bg-amber-500', 'bg-indigo-500', 'bg-rose-500', 'bg-lime-500']
const projectHexColors = ['#0ea5e9', '#8b5cf6', '#10b981', '#f97316', '#ec4899', '#06b6d4', '#f59e0b', '#6366f1', '#f43f5e', '#84cc16']
const projectBadgePairs = [
  'bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300',
  'bg-violet-100 text-violet-700 dark:bg-violet-900/40 dark:text-violet-300',
  'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  'bg-orange-100 text-orange-700 dark:bg-orange-900/40 dark:text-orange-300',
  'bg-pink-100 text-pink-700 dark:bg-pink-900/40 dark:text-pink-300',
  'bg-cyan-100 text-cyan-700 dark:bg-cyan-900/40 dark:text-cyan-300',
  'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  'bg-indigo-100 text-indigo-700 dark:bg-indigo-900/40 dark:text-indigo-300',
  'bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300',
  'bg-lime-100 text-lime-700 dark:bg-lime-900/40 dark:text-lime-300',
]

// Pre-compute project -> color index map (avoids findIndex per call)
const projectColorMap = computed(() => {
  const map = new Map()
  store.config.projects.forEach((p, i) => map.set(p.project, i % projectColors.length))
  return map
})

function projectColorClass(projectName) {
  const idx = projectColorMap.value.get(projectName)
  return idx != null ? projectColors[idx] : 'bg-gray-400'
}

function projectHexColor(projectName) {
  const idx = projectColorMap.value.get(projectName)
  return idx != null ? projectHexColors[idx] : '#9ca3af'
}

function isDirty(itemId) {
  void store.dirtyVersion
  return store.dirty.has(itemId)
}

function hasDirtyLink(itemId) {
  void store.dirtyLinksVersion
  if (store.dirtyLinks.has(itemId)) return true
  for (const [, entry] of store.dirtyLinks) {
    if (entry.added?.some(l => l.target_id === itemId)) return true
  }
  return false
}

function projectBadgeClass(projectName) {
  const idx = projectColorMap.value.get(projectName)
  return idx != null ? projectBadgePairs[idx] : 'bg-gray-200 text-gray-600 dark:bg-gray-600 dark:text-gray-300'
}

// --- Bar resize (drag edges to span sprints) ---
const resizing = ref(null) // { item, side, startX, overlayEl }

function onResizeStart(evt, item, side) {
  evt.preventDefault()
  const overlayEl = evt.target.closest('[style*="gridColumn"]') || evt.target.closest('.pointer-events-none')
  if (!overlayEl) return
  resizing.value = { item, side, startX: evt.clientX, overlayEl }
  document.addEventListener('pointermove', onResizeMove)
  document.addEventListener('pointerup', onResizeEnd)
}

function onResizeMove(evt) {
  if (!resizing.value) return
  const { item, side, overlayEl } = resizing.value
  const rect = overlayEl.getBoundingClientRect()
  const pct = ((evt.clientX - rect.left) / rect.width) * 100

  // Find which column the cursor is over using column positions
  const positions = columnPositions.value
  const targetPos = positions.find(c => !c.isGap && pct >= c.startPct && pct <= c.endPct)
  if (!targetPos) return

  if (side === 'left') {
    // Snap start to this column — update start_date
    const newStartDate = targetPos.startDate
    // Don't allow start to go past the end column
    const currentEnd = item.target_date ? new Date(item.target_date) : null
    if (currentEnd && new Date(newStartDate) > currentEnd) return
    const updates = { start_date: newStartDate }
    if (columnMode.value === 'sprints') updates.iteration_path = targetPos.key
    if (item.start_date !== newStartDate) {
      store.updateItem(item.id, updates)
    }
  } else {
    // Snap end to this column — update target_date
    const newTargetDate = targetPos.finishDate
    // Don't allow end to go before the start column
    const currentStart = item.start_date ? new Date(item.start_date) : null
    if (currentStart && new Date(newTargetDate) < currentStart) return
    if (item.target_date !== newTargetDate) {
      store.updateItem(item.id, { target_date: newTargetDate })
    }
  }
}

function onResizeEnd() {
  resizing.value = null
  document.removeEventListener('pointermove', onResizeMove)
  document.removeEventListener('pointerup', onResizeEnd)
  nextTick(updateDependencyArrows)
}

// --- Pointer-based Drag & Drop ---
const dragOverCell = ref('')
const draggingId = ref(null)
const dragGhostEl = ref(null) // template ref for ghost element
const dragGhostVisible = ref(false)
const dragGhostText = ref('')
const barsContainerRef = ref(null)
const dependencyArrows = ref([])

// --- Dependency linking mode ---
const linkSourceItem = ref(null) // first clicked item (predecessor)
const linkTargetItem = ref(null) // second clicked item (successor)
const showLinkConfirm = ref(false)

function onChainClick(item) {
  if (!linkSourceItem.value) {
    // First click — select as predecessor
    linkSourceItem.value = item
  } else if (linkSourceItem.value.id === item.id) {
    // Clicked same item — deselect
    linkSourceItem.value = null
  } else {
    // Second click — show confirmation popup
    linkTargetItem.value = item
    showLinkConfirm.value = true
  }
}

function swapLinkDirection() {
  const temp = linkSourceItem.value
  linkSourceItem.value = linkTargetItem.value
  linkTargetItem.value = temp
}

function confirmLink() {
  if (!linkSourceItem.value || !linkTargetItem.value) return
  // Add link: source is predecessor, target is successor
  // From successor's perspective: "predecessor" link points to the source
  store.addLink(linkTargetItem.value.id, linkSourceItem.value.id, 'predecessor')
  showLinkConfirm.value = false
  linkSourceItem.value = null
  linkTargetItem.value = null
}

function cancelLink() {
  showLinkConfirm.value = false
  linkSourceItem.value = null
  linkTargetItem.value = null
}

function onArrowClick(arrow) {
  if (arrow.pendingRemoval) {
    // Undo the pending removal — re-add the link
    // The link was stored as removed under the successor (toId) with type 'predecessor'
    // Find which item has it in removed list
    const entry1 = store.dirtyLinks.get(arrow.toId)
    if (entry1?.removed?.some(l => l.target_id === arrow.fromId && l.link_type === 'predecessor')) {
      store.addLink(arrow.toId, arrow.fromId, 'predecessor')
    } else {
      const entry2 = store.dirtyLinks.get(arrow.fromId)
      if (entry2?.removed?.some(l => l.target_id === arrow.toId && l.link_type === 'successor')) {
        store.addLink(arrow.fromId, arrow.toId, 'successor')
      }
    }
  } else if (arrow.dashed) {
    // Undo a pending addition
    const entry = store.dirtyLinks.get(arrow.toId)
    if (entry?.added?.some(l => l.target_id === arrow.fromId && l.link_type === 'predecessor')) {
      store.removeLink(arrow.toId, arrow.fromId, 'predecessor')
    } else {
      store.removeLink(arrow.fromId, arrow.toId, 'successor')
    }
  } else {
    // Mark a saved link for removal
    store.removeLink(arrow.toId, arrow.fromId, 'predecessor')
  }
}

// Recompute dependency arrows from DOM positions
function updateDependencyArrows() {
  if (!barsContainerRef.value) { dependencyArrows.value = []; return }
  const container = barsContainerRef.value
  const containerRect = container.getBoundingClientRect()
  const scrollTop = container.scrollTop

  // Build a map of item ID -> bar DOM element
  const barEls = container.querySelectorAll('[data-item-id]')
  const barMap = new Map()
  barEls.forEach(el => barMap.set(Number(el.dataset.itemId), el))

  // Batch all DOM measurements upfront to avoid layout thrashing
  const barRects = new Map()
  for (const [id, el] of barMap) {
    barRects.set(id, el.getBoundingClientRect())
  }

  const arrows = []
  const drawnPairs = new Set() // avoid duplicates

  // Helper to create an arrow between two items
  function addArrow(fromId, toId, isDirty, isPendingRemoval = false) {
    const pairKey = `${fromId}->${toId}`
    if (drawnPairs.has(pairKey)) return
    drawnPairs.add(pairKey)
    const fromRect = barRects.get(fromId)
    const toRect = barRects.get(toId)
    if (!fromRect || !toRect) return
    const x1 = fromRect.right - containerRect.left
    const y1 = fromRect.top + fromRect.height / 2 - containerRect.top + scrollTop
    const x2 = toRect.left - containerRect.left
    const y2 = toRect.top + toRect.height / 2 - containerRect.top + scrollTop
    const cpx = (x1 + x2) / 2
    const midX = (x1 + x2) / 2
    const midY = (y1 + y2) / 2
    const item = store.items.find(i => i.id === fromId)
    arrows.push({
      path: `M${x1},${y1} C${cpx},${y1} ${cpx},${y2} ${x2},${y2}`,
      color: item ? projectHexColor(item.project) : '#9ca3af',
      dashed: isDirty,
      pendingRemoval: isPendingRemoval,
      midX,
      midY,
      fromId,
      toId,
    })
  }

  // Collect pending removals to skip them from solid arrows
  const pendingRemovals = new Set()
  for (const [itemId, entry] of store.dirtyLinks) {
    for (const link of (entry.removed || [])) {
      const fromId = link.link_type === 'predecessor' ? link.target_id : itemId
      const toId = link.link_type === 'predecessor' ? itemId : link.target_id
      pendingRemovals.add(`${fromId}->${toId}`)
    }
  }

  // Draw saved (solid) arrows — skip those pending removal
  for (const [itemId, saved] of store.savedLinks) {
    for (const predId of saved.predecessors) {
      const key = `${predId}->${itemId}`
      if (!pendingRemovals.has(key)) addArrow(predId, itemId, false)
    }
    for (const succId of saved.successors) {
      const key = `${itemId}->${succId}`
      if (!pendingRemovals.has(key)) addArrow(itemId, succId, false)
    }
  }

  // Draw dirty (dashed) arrows for additions
  for (const [itemId, entry] of store.dirtyLinks) {
    for (const link of (entry.added || [])) {
      const fromId = link.link_type === 'predecessor' ? link.target_id : itemId
      const toId = link.link_type === 'predecessor' ? itemId : link.target_id
      addArrow(fromId, toId, true)
    }
  }

  // Draw pending removal arrows (red dashed)
  for (const [itemId, entry] of store.dirtyLinks) {
    for (const link of (entry.removed || [])) {
      const fromId = link.link_type === 'predecessor' ? link.target_id : itemId
      const toId = link.link_type === 'predecessor' ? itemId : link.target_id
      addArrow(fromId, toId, false, true)
    }
  }

  dependencyArrows.value = arrows
}

// Schedule arrow update after DOM + layout settle (nextTick + rAF ensures grid reflow completes)
function scheduleArrowUpdate() {
  nextTick(() => requestAnimationFrame(updateDependencyArrows))
}

// Update arrows when dirty links change or items move
watch(() => store.dirtyLinksVersion, scheduleArrowUpdate)
watch(() => store.savedLinksVersion, scheduleArrowUpdate)
watch(() => store.dirtyVersion, scheduleArrowUpdate)
watch(() => store.itemsPositionVersion, scheduleArrowUpdate)
// Update arrows when view changes (bar positions shift)
watch(activeView, scheduleArrowUpdate)
watch(barPositions, scheduleArrowUpdate)
// Update arrows when filters change (hidden items should hide their arrows)
watch(projectFilter, scheduleArrowUpdate)
watch(stateFilter, scheduleArrowUpdate)
watch(searchFilter, scheduleArrowUpdate)
// Update arrows after items finish loading (new rows may shift existing bars)
watch(() => store.loadingItems, (loading) => { if (!loading) scheduleArrowUpdate() })

// Drag state (non-reactive for performance)
let dragItem = null
let dragStartX = 0
let dragStartY = 0
let isDragging = false
const DRAG_THRESHOLD = 5

// Provide the drag starter to RoadmapCard children
provide('roadmapStartDrag', (evt, item) => {
  beginDragTracking(evt, item)
})
provide('roadmapDraggingId', draggingId)

function onBarPointerDown(evt, item) {
  if (evt.button !== 0) return
  evt.preventDefault()
  beginDragTracking(evt, item)
}

function beginDragTracking(evt, item) {
  dragItem = item
  dragStartX = evt.clientX
  dragStartY = evt.clientY
  isDragging = false
  document.addEventListener('pointermove', onPointerMove)
  document.addEventListener('pointerup', onPointerUp)
}

function onPointerMove(evt) {
  if (!dragItem) return

  const dx = evt.clientX - dragStartX
  const dy = evt.clientY - dragStartY

  if (!isDragging) {
    if (Math.abs(dx) + Math.abs(dy) < DRAG_THRESHOLD) return
    isDragging = true
    draggingId.value = dragItem.id
    dragGhostText.value = dTitle(dragItem.title)
    dragGhostVisible.value = true
  }

  // Move ghost via direct DOM (no reactive update)
  const ghost = dragGhostEl.value
  if (ghost) {
    ghost.style.left = (evt.clientX + 12) + 'px'
    ghost.style.top = (evt.clientY - 10) + 'px'
  }

  // Hit-test at actual pointer position
  const el = document.elementFromPoint(evt.clientX, evt.clientY)
  const cell = el?.closest?.('[data-drop-col]')
  if (cell) {
    const row = cell.closest('[data-epic-id]')
    const epicId = row?.dataset?.epicId || ''
    const newVal = epicId + '|' + cell.dataset.dropCol
    if (dragOverCell.value !== newVal) dragOverCell.value = newVal
  } else {
    if (dragOverCell.value !== '') dragOverCell.value = ''
  }
}

function onPointerUp(evt) {
  document.removeEventListener('pointermove', onPointerMove)
  document.removeEventListener('pointerup', onPointerUp)

  if (!isDragging || !dragItem) {
    dragItem = null
    isDragging = false
    return
  }

  // Find target cell at actual pointer position
  const el = document.elementFromPoint(evt.clientX, evt.clientY)
  const cell = el?.closest?.('[data-drop-col]')
  const targetColKey = cell?.dataset?.dropCol

  // Clean up visual state
  dragGhostVisible.value = false
  dragOverCell.value = ''
  draggingId.value = null

  if (!targetColKey) {
    dragItem = null
    isDragging = false
    return
  }

  const item = dragItem
  dragItem = null
  isDragging = false

  const oldCol = itemColumn(item)
  if (oldCol === targetColKey) return

  const mode = columnMode.value
  if (targetColKey === '_unplanned') {
    store.updateItem(item.id, { start_date: '', target_date: '' })
  } else if (mode === 'quarters') {
    const col = timelineColumns.value.find(c => c.key === targetColKey)
    if (col) {
      // Set both dates to quarter boundaries so the feature is visible in sprint view too
      store.updateItem(item.id, { start_date: col.startDate, target_date: col.finishDate })
    }
  } else if (mode === 'sprints') {
    const col = timelineColumns.value.find(c => c.key === targetColKey)
    if (col) {
      store.updateItem(item.id, { start_date: col.startDate, target_date: col.finishDate })
    }
  }
}

// --- Push ---
const showPushResults = ref(false)
const pushResults = ref([])
const showChangesPreview = ref(false)

const changesPreview = computed(() => {
  void store.dirtyVersion
  void store.dirtyLinksVersion
  const changes = []
  const toDateKey = (v) => {
    if (!v) return ''
    const d = new Date(v)
    if (isNaN(d)) return ''
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
  }
  const processedIds = new Set()
  for (const id of store.dirty) {
    const item = store.items.find(i => i.id === id)
    const original = store.dirtyOriginals.get(id)
    if (!item || !original) continue
    const startDateChanged = toDateKey(item.start_date) !== toDateKey(original.start_date)
    const targetDateChanged = toDateKey(item.target_date) !== toDateKey(original.target_date)
    const linkEntry = store.dirtyLinks.get(id)
    const linksAdded = linkEntry?.added?.length || 0
    const linksRemoved = linkEntry?.removed?.length || 0
    if (!startDateChanged && !targetDateChanged && !linksAdded && !linksRemoved) continue
    processedIds.add(id)
    changes.push({
      id: item.id,
      title: item.title,
      project: item.project,
      startDateChanged,
      targetDateChanged,
      originalStartDate: formatDateShort(original.start_date),
      newStartDate: formatDateShort(item.start_date),
      originalTargetDate: formatDateShort(original.target_date),
      newTargetDate: formatDateShort(item.target_date),
      linksAdded,
      linksRemoved,
    })
  }
  // Also include items with only link changes (not in dirty set)
  for (const [id, entry] of store.dirtyLinks) {
    if (processedIds.has(id)) continue
    const item = store.items.find(i => i.id === id)
    if (!item) continue
    const linksAdded = entry.added?.length || 0
    const linksRemoved = entry.removed?.length || 0
    if (!linksAdded && !linksRemoved) continue
    changes.push({
      id: item.id,
      title: item.title,
      project: item.project,
      startDateChanged: false,
      targetDateChanged: false,
      originalStartDate: '',
      newStartDate: '',
      originalTargetDate: '',
      newTargetDate: '',
      linksAdded,
      linksRemoved,
    })
  }
  return changes
})

function formatDateShort(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  if (isNaN(d)) return ''
  return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
}

function revertChange(workItemId) {
  store.revertItem(workItemId)
  if (store.dirty.size === 0) showChangesPreview.value = false
}

function revertAllChanges() {
  const ids = [...new Set([...store.dirty, ...store.dirtyLinks.keys()])]
  for (const id of ids) store.revertItem(id)
  showChangesPreview.value = false
}

async function pushChanges() {
  const results = await store.pushPositions()
  if (results) {
    pushResults.value = results
    showPushResults.value = true
  }
}

// --- Init ---
onMounted(async () => {
  await store.loadConfig()
  activeView.value = store.config.active_view || 'quarters'
  sprintOverlayEnabled.value = store.config.show_sprint_overlay || false
  if (store.config.projects.length > 0) {
    store.loadItems()
    if (activeView.value !== 'quarters') {
      loadSprints()
    }
    if (sprintOverlayEnabled.value) {
      loadAllSprints()
    }
  }
})

async function loadSprints() {
  const sc = activeSprintConfig.value
  if (!sc) return
  const team = sc.team || sc.project + ' Team'
  try {
    const resp = await api.get(`/api/roadmap/iterations?organization=${encodeURIComponent(sc.organization)}&project=${encodeURIComponent(sc.project)}&team=${encodeURIComponent(team)}`)
    sprintData.value = resp.iterations || []
  } catch { sprintData.value = [] }
}

watch(() => showAddProject.value, async (v) => {
  if (!v) return
  await monitorStore.fetchKnownOrganizations()
  if (monitorStore.organizations.length === 1 && !newProject.value.organization) {
    newProject.value.organization = monitorStore.organizations[0].name
  }
})
</script>

<style scoped>
.dep-arrow-remove {
  opacity: 0;
  transition: opacity 0.15s;
}
.dep-arrow-group:hover .dep-arrow-remove {
  opacity: 1;
}
/* Enlarge narrow bars on hover so content is readable */
.bar-wrapper {
  position: relative;
  z-index: 1;
  transition: z-index 0s;
  /* Extra invisible hover area prevents flicker when bar height changes on expand */
  padding-bottom: 12px;
  margin-bottom: -12px;
}
.bar-wrapper:hover {
  z-index: 30;
}
.bar-wrapper:hover .bar-inner {
  min-width: 220px;
}
</style>
