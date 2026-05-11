<template>
  <div>
    <!-- Search bar -->
    <div class="px-4 py-3 border-b border-gray-200 dark:border-gray-700">
      <div class="relative">
        <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
        </svg>
        <input
          v-autofocus
          v-model="search"
          type="text"
          placeholder="Filter by ID, title, type, or assignee…"
          class="w-full pl-9 pr-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none transition-shadow"
        />
      </div>
    </div>

    <!-- Table -->
    <div class="overflow-x-auto">
      <table class="w-full text-sm [&_th]:whitespace-nowrap [&_td]:whitespace-nowrap">
        <thead>
          <tr class="text-left border-b border-gray-200 dark:border-gray-700">
            <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('id')">
              ID
              <span v-if="sortKey === 'id'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('title')">
              {{ isTagOverview ? 'Tag' : 'Title' }}
              <span v-if="sortKey === 'title'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('work_item_type')">
              {{ isTagOverview ? 'Count' : 'Type' }}
              <span v-if="sortKey === 'work_item_type'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasAssignedTo" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('assigned_to')">
              Assigned To
              <span v-if="sortKey === 'assigned_to'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasState" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('state')">
              State
              <span v-if="sortKey === 'state'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasIterationPath" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('iteration_path')">
              Iteration
              <span v-if="sortKey === 'iteration_path'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="hasCreatedDate" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 cursor-pointer select-none" @click="toggleSort('created_date')">
              Created
              <span v-if="sortKey === 'created_date'" class="ml-1 text-xs">{{ sortDir === 'asc' ? '▲' : '▼' }}</span>
            </th>
            <th v-if="isOrphanCheck" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 text-right">Actions</th>
            <th v-if="isTagOverview" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 text-right">Actions</th>
            <th v-if="isTagDetail" class="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
            <tr
              v-for="item in paginatedItems" :key="item.id"
              class="border-b border-gray-100 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-700/50 transition-colors"
            >
              <td class="px-4 py-3">
                <span v-if="isTagOverview" class="text-gray-500 dark:text-gray-400">#{{ item.id }}</span>
                <a
                  v-else
                  :href="item.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="text-primary-600 hover:text-primary-800 font-medium hover:underline"
                >
                  #{{ item.id }}
                </a>
              </td>
              <td class="px-4 py-3 text-gray-800 dark:text-gray-200 !whitespace-normal">
                <div class="flex items-center gap-1.5">
                  <router-link
                  v-if="isTagOverview && !isZeroCount(item)"
                  :to="{ name: 'tag-detail', params: { projectId: props.projectId, tagName: item.title } }"
                  class="text-primary-600 hover:text-primary-800 font-medium hover:underline"
                >
                  {{ item.title }}
                </router-link>
                <span v-else-if="isTagOverview" class="text-gray-400 dark:text-gray-500 italic">{{ item.title }}</span>
                <a
                  v-else
                  :href="item.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="hover:text-primary-600 hover:underline"
                >
                  {{ item.title }}
                </a>
                </div>
              </td>
              <td class="px-4 py-3 text-gray-600 dark:text-gray-400">
                {{ item.work_item_type || '—' }}
              </td>
              <td v-if="hasAssignedTo" class="px-4 py-3 text-gray-700 dark:text-gray-300">
                {{ item.assigned_to || '—' }}
              </td>
              <td v-if="hasState" class="px-4 py-3 whitespace-nowrap">
                <span class="inline-block px-2 py-0.5 rounded-md text-xs font-medium" :class="stateClass(item.state)">{{ item.state || '—' }}</span>
              </td>
              <td v-if="hasIterationPath" class="px-4 py-3 text-gray-600 dark:text-gray-400 whitespace-nowrap">
                {{ formatIterationPath(item.iteration_path) }}
              </td>
              <td v-if="hasCreatedDate" class="px-4 py-3 text-gray-600 dark:text-gray-400 whitespace-nowrap">
                {{ formatDate(item.created_date) }}
              </td>
              <td v-if="isOrphanCheck" class="px-4 py-3 text-right">
                <button
                  @click="emit('assign-parent', item)"
                  class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-md border text-primary-600 dark:text-primary-400 border-primary-300 dark:border-primary-600 hover:bg-primary-50 dark:hover:bg-primary-900/30 transition-colors"
                  :title="isParentDone(item) ? 'Change the parent work item' : 'Assign a parent work item'"
                >
                  <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                  </svg>
                  {{ isParentDone(item) ? 'Change Parent' : 'Assign Parent' }}
                </button>
              </td>
              <td v-if="isTagOverview" class="px-4 py-3 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <!-- Inline confirmation for remove/rename/delete -->
                  <template v-if="confirmingRemoveId === item.id && confirmAction === 'remove'">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Remove from all items?</span>
                    <button
                      @click="confirmBulkRemove(item.title)"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md text-white bg-red-600 hover:bg-red-700 transition-colors"
                    >
                      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                      </svg>
                      Yes
                    </button>
                    <button
                      @click="confirmingRemoveId = null; confirmAction = null"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                    >
                      Cancel
                    </button>
                  </template>
                  <template v-else-if="confirmingRemoveId === item.id && confirmAction === 'delete'">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Delete unused tag?</span>
                    <button
                      @click="confirmDelete(item.title)"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md text-white bg-red-600 hover:bg-red-700 transition-colors"
                    >
                      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                      </svg>
                      Yes
                    </button>
                    <button
                      @click="confirmingRemoveId = null; confirmAction = null"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                    >
                      Cancel
                    </button>
                  </template>
                  <template v-else-if="confirmingRemoveId === item.id && confirmAction === 'rename'">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Rename to "{{ pendingRenameTag?.newTag }}"?</span>
                    <button
                      @click="confirmBulkRename()"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md text-white bg-amber-600 hover:bg-amber-700 transition-colors"
                    >
                      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                      </svg>
                      Yes
                    </button>
                    <button
                      @click="confirmingRemoveId = null; confirmAction = null; pendingRenameTag = null"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                    >
                      Cancel
                    </button>
                  </template>
                  <!-- Normal actions -->
                  <template v-else>
                    <!-- Rename (in-use tags only) -->
                    <div v-if="!isZeroCount(item)" class="relative" :ref="el => setRenameRef(item.id, el)">
                      <button
                        @click.stop="toggleItemRename(item.id)"
                        :disabled="busyItemId === item.id"
                        class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-md border text-amber-600 dark:text-amber-400 border-amber-300 dark:border-amber-600 hover:bg-amber-50 dark:hover:bg-amber-900/30 transition-colors disabled:opacity-40"
                        title="Rename this tag on all work items"
                      >
                        <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
                        </svg>
                        Rename
                      </button>
                      <!-- Rename dropdown -->
                      <div
                        v-if="renameOpenId === item.id"
                        class="absolute right-0 top-full mt-1 w-64 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl shadow-lg z-50"
                      >
                        <div class="p-2 border-b border-gray-200 dark:border-gray-700">
                          <div class="relative">
                            <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                              <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
                            </svg>
                            <input
                              :ref="el => setSearchRef(item.id, el)"
                              v-model="renameSearchText"
                              type="text"
                              placeholder="Search tags…"
                              class="w-full pl-8 pr-3 py-1.5 text-xs border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none"
                              @click.stop
                            />
                          </div>
                        </div>
                        <div class="max-h-48 overflow-y-auto">
                          <div v-if="allTagsLoading" class="flex items-center gap-2 px-3 py-2 text-xs text-gray-500">
                            <svg class="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24">
                              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                            </svg>
                            Loading…
                          </div>
                          <div v-else-if="renameFilteredTagsForOverview(item.title).length === 0" class="px-3 py-2 text-xs text-gray-400 italic">
                            No matching tags.
                          </div>
                          <button
                            v-else
                            v-for="t in renameFilteredTagsForOverview(item.title)"
                            :key="t"
                            @click.stop="selectBulkRenameTag(item, t)"
                            class="w-full text-left px-3 py-1.5 text-xs text-gray-700 dark:text-gray-200 hover:bg-primary-50 dark:hover:bg-primary-900/20 transition-colors"
                          >
                            {{ t }}
                          </button>
                        </div>
                      </div>
                    </div>
                    <!-- Remove (in-use tags only) -->
                    <button
                      v-if="!isZeroCount(item)"
                      @click="confirmingRemoveId = item.id; confirmAction = 'remove'"
                      :disabled="busyItemId === item.id"
                      class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-md border text-red-600 dark:text-red-400 border-red-300 dark:border-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors disabled:opacity-40"
                      title="Remove this tag from all work items"
                    >
                      <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M9.75 9.75l4.5 4.5m0-4.5l-4.5 4.5M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                      Remove
                    </button>
                    <!-- Delete (zero-count tags only) -->
                    <button
                      v-if="isZeroCount(item)"
                      @click="confirmingRemoveId = item.id; confirmAction = 'delete'"
                      class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-md border text-red-600 dark:text-red-400 border-red-300 dark:border-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors"
                      title="Delete this unused tag"
                    >
                      <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                      </svg>
                      Delete
                    </button>
                  </template>
                </div>
              </td>
              <!-- Per-item tag actions (tag detail view) -->
              <td v-if="isTagDetail" class="px-4 py-3 text-right">
                <div class="inline-flex items-center gap-1.5">
                  <!-- Inline remove confirmation -->
                  <template v-if="confirmingRemoveId === item.id">
                    <span class="text-xs text-gray-600 dark:text-gray-300 whitespace-nowrap">Remove tag?</span>
                    <button
                      @click="confirmRemove(item.id)"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md text-white bg-red-600 hover:bg-red-700 transition-colors"
                    >
                      <svg class="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5" />
                      </svg>
                      Yes
                    </button>
                    <button
                      @click="confirmingRemoveId = null"
                      class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                    >
                      Cancel
                    </button>
                  </template>
                  <!-- Normal actions -->
                  <template v-else>
                    <!-- Rename -->
                    <div class="relative" :ref="el => setRenameRef(item.id, el)">
                      <button
                        @click.stop="toggleItemRename(item.id)"
                        :disabled="busyItemId === item.id"
                        class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-md border text-amber-600 dark:text-amber-400 border-amber-300 dark:border-amber-600 hover:bg-amber-50 dark:hover:bg-amber-900/30 transition-colors disabled:opacity-40"
                        title="Rename tag on this work item"
                      >
                        <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                          <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10" />
                        </svg>
                        Rename
                      </button>
                      <!-- Per-item rename dropdown -->
                      <div
                        v-if="renameOpenId === item.id"
                        class="absolute right-0 top-full mt-1 w-64 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl shadow-lg z-50"
                      >
                        <div class="p-2 border-b border-gray-200 dark:border-gray-700">
                          <div class="relative">
                            <svg class="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                              <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
                            </svg>
                            <input
                              :ref="el => setSearchRef(item.id, el)"
                              v-model="renameSearchText"
                              type="text"
                              placeholder="Search tags…"
                              class="w-full pl-8 pr-3 py-1.5 text-xs border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 dark:text-gray-100 focus:ring-2 focus:ring-primary-500 focus:border-primary-500 outline-none"
                              @click.stop
                            />
                          </div>
                        </div>
                        <div class="max-h-48 overflow-y-auto">
                          <div v-if="allTagsLoading" class="flex items-center gap-2 px-3 py-2 text-xs text-gray-500">
                            <svg class="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24">
                              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"></path>
                            </svg>
                            Loading…
                          </div>
                          <div v-else-if="renameFilteredTags.length === 0" class="px-3 py-2 text-xs text-gray-400 italic">
                            No matching tags.
                          </div>
                          <button
                            v-else
                            v-for="t in renameFilteredTags"
                            :key="t"
                            @click.stop="selectRenameTag(item.id, t)"
                            class="w-full text-left px-3 py-1.5 text-xs text-gray-700 dark:text-gray-200 hover:bg-primary-50 dark:hover:bg-primary-900/20 transition-colors"
                          >
                            {{ t }}
                          </button>
                        </div>
                      </div>
                    </div>
                    <!-- Remove -->
                    <button
                      @click="confirmingRemoveId = item.id"
                      :disabled="busyItemId === item.id"
                      class="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium rounded-md border text-red-600 dark:text-red-400 border-red-300 dark:border-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 transition-colors disabled:opacity-40"
                      title="Remove tag from this work item"
                    >
                      <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M9.75 9.75l4.5 4.5m0-4.5l-4.5 4.5M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                      </svg>
                      Remove
                    </button>
                  </template>
                </div>
              </td>
            </tr>
        </tbody>
      </table>

      <div v-if="filteredItems.length === 0" class="text-center py-8 text-gray-400 dark:text-gray-500 text-sm">
        {{ debouncedSearch ? 'No matching items.' : 'No items to display.' }}
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="totalPages > 1" class="flex items-center justify-between px-4 py-3 border-t border-gray-200 dark:border-gray-700">
      <span class="text-xs text-gray-500 dark:text-gray-400">
        {{ (page - 1) * pageSize + 1 }}–{{ Math.min(page * pageSize, filteredItems.length) }} of {{ filteredItems.length }}
      </span>
      <div class="flex items-center gap-1">
        <button
          @click="page = page - 1"
          :disabled="page <= 1"
          class="px-2.5 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        >Prev</button>
        <template v-for="p in visiblePages" :key="p">
          <span v-if="p === '…'" class="px-1.5 text-xs text-gray-400">…</span>
          <button
            v-else
            @click="page = p"
            class="px-2.5 py-1 text-xs font-medium rounded-md border transition-colors"
            :class="p === page ? 'bg-primary-600 text-white border-primary-600' : 'border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700'"
          >{{ p }}</button>
        </template>
        <button
          @click="page = page + 1"
          :disabled="page >= totalPages"
          class="px-2.5 py-1 text-xs font-medium rounded-md border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        >Next</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { useDebouncedRef } from '../composables/useDebounce.js'

const props = defineProps({
  items: { type: Array, default: () => [] },
  checkType: { type: String, default: '' },
  projectId: { type: String, default: '' },
  tagName: { type: String, default: '' },
  allTags: { type: Array, default: () => [] },
  allTagsLoading: { type: Boolean, default: false },
  busyItemId: { type: Number, default: null },
})

const emit = defineEmits(['delete-tag', 'remove-item-tag', 'rename-item-tag', 'remove-tag', 'rename-tag', 'assign-parent'])

const isOrphanCheck = computed(() => props.checkType === 'orphan_check')
const isTagOverview = computed(() => props.checkType === 'tag_overview_check')
const isTagDetail = computed(() => props.checkType === 'tag_detail')

function isParentDone(item) {
  return (item.work_item_type || '').toLowerCase().includes('[parent done]')
}

function isZeroCount(item) {
  return (item.work_item_type || '').startsWith('0 ')
}

// --- Per-item rename dropdown ---
const confirmingRemoveId = ref(null)
const confirmAction = ref(null)
const pendingRenameTag = ref(null)
const renameOpenId = ref(null)
const renameSearchText = ref('')
const renameRefs = {}
const searchRefs = {}

function setRenameRef(id, el) { if (el) renameRefs[id] = el }
function setSearchRef(id, el) { if (el) searchRefs[id] = el }

const renameFilteredTags = computed(() => {
  const q = renameSearchText.value.toLowerCase()
  const current = (props.tagName || '').toLowerCase()
  return props.allTags
    .filter(t => t.toLowerCase() !== current)
    .filter(t => !q || t.toLowerCase().includes(q))
})

function confirmRemove(id) {
  confirmingRemoveId.value = null
  confirmAction.value = null
  emit('remove-item-tag', id)
}

function selectRenameTag(itemId, tag) {
  renameOpenId.value = null
  emit('rename-item-tag', itemId, tag)
}

// --- Tag overview bulk operations ---
function renameFilteredTagsForOverview(currentTag) {
  const q = renameSearchText.value.toLowerCase()
  const current = currentTag.toLowerCase()
  return props.allTags
    .filter(t => t.toLowerCase() !== current)
    .filter(t => !q || t.toLowerCase().includes(q))
}

function confirmBulkRemove(tagName) {
  confirmingRemoveId.value = null
  confirmAction.value = null
  emit('remove-tag', tagName)
}

function confirmDelete(tagName) {
  confirmingRemoveId.value = null
  confirmAction.value = null
  emit('delete-tag', tagName)
}

function selectBulkRenameTag(item, newTag) {
  renameOpenId.value = null
  // Set confirm state for rename
  confirmingRemoveId.value = item.id
  confirmAction.value = 'rename'
  // Stash the newTag so confirmation can use it
  pendingRenameTag.value = { tagName: item.title, newTag }
}

function confirmBulkRename() {
  if (!pendingRenameTag.value) return
  const { tagName, newTag } = pendingRenameTag.value
  confirmingRemoveId.value = null
  confirmAction.value = null
  pendingRenameTag.value = null
  emit('rename-tag', tagName, newTag)
}

async function toggleItemRename(itemId) {
  if (renameOpenId.value === itemId) {
    renameOpenId.value = null
    return
  }
  renameOpenId.value = itemId
  renameSearchText.value = ''
  await nextTick()
  searchRefs[itemId]?.focus()
}

function onDocClick(e) {
  if (renameOpenId.value != null) {
    const ref = renameRefs[renameOpenId.value]
    if (ref && !ref.contains(e.target)) {
      renameOpenId.value = null
    }
  }
}
onMounted(() => document.addEventListener('click', onDocClick))
onBeforeUnmount(() => document.removeEventListener('click', onDocClick))

const search = ref('')
const debouncedSearch = useDebouncedRef(search, 250)
const sortKey = ref('id')
const sortDir = ref('asc')
const page = ref(1)
const pageSize = 50

// Reset to page 1 when search or items change
watch([debouncedSearch, () => props.items], () => { page.value = 1 })

const hasAssignedTo = computed(() => props.items.some(i => i.assigned_to))
const hasCreatedDate = computed(() => props.items.some(i => i.created_date))
const hasIterationPath = computed(() => props.items.some(i => i.iteration_path))
const hasState = computed(() => props.items.some(i => i.state))

function formatDate(dateStr) {
  if (!dateStr) return '—'
  const d = new Date(dateStr)
  if (isNaN(d)) return '—'
  return d.toLocaleDateString('nl-NL', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

function formatIterationPath(path) {
  if (!path) return '—'
  const parts = path.split('\\')
  return parts[parts.length - 1]
}

function stateClass(state) {
  if (!state) return 'bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
  const s = state.toLowerCase()
  if (s.includes('progress') || s === 'active' || s === 'committed') return 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300'
  if (s.includes('refine') || s === 'new' || s === 'proposed') return 'bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300'
  if (s.includes('review') || s.includes('test')) return 'bg-purple-100 dark:bg-purple-900/40 text-purple-700 dark:text-purple-300'
  if (s === 'resolved') return 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300'
  return 'bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300'
}

const filteredItems = computed(() => {
  let items = props.items
  if (!debouncedSearch.value) return items
  const q = debouncedSearch.value.toLowerCase()
  return items.filter(i =>
    String(i.id).includes(q) ||
    (i.title || '').toLowerCase().includes(q) ||
    (i.work_item_type || '').toLowerCase().includes(q) ||
    (i.assigned_to || '').toLowerCase().includes(q) ||
    (i.iteration_path || '').toLowerCase().includes(q) ||
    (i.state || '').toLowerCase().includes(q)
  )
})

function toggleSort(key) {
  if (sortKey.value === key) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortDir.value = 'asc'
  }
}

const sortedItems = computed(() => {
  const copy = [...filteredItems.value]
  const dir = sortDir.value === 'asc' ? 1 : -1
  copy.sort((a, b) => {
    const va = a[sortKey.value]
    const vb = b[sortKey.value]
    if (typeof va === 'number') return (va - vb) * dir
    return String(va || '').localeCompare(String(vb || '')) * dir
  })
  return copy
})

const totalPages = computed(() => Math.ceil(sortedItems.value.length / pageSize) || 1)

const paginatedItems = computed(() => {
  const start = (page.value - 1) * pageSize
  return sortedItems.value.slice(start, start + pageSize)
})

const visiblePages = computed(() => {
  const total = totalPages.value
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const cur = page.value
  const pages = [1]
  if (cur > 3) pages.push('…')
  for (let i = Math.max(2, cur - 1); i <= Math.min(total - 1, cur + 1); i++) pages.push(i)
  if (cur < total - 2) pages.push('…')
  pages.push(total)
  return pages
})
</script>
