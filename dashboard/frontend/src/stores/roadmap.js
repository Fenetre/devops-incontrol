import { defineStore } from 'pinia'
import { useApi } from '../composables/useApi.js'

const api = useApi()

export const useRoadmapStore = defineStore('roadmap', {
  state: () => ({
    config: { lanes: [], projects: [], column_mode: 'quarters', sprint_configs: [], active_view: 'quarters', visible_quarters: [], show_sprint_overlay: false },
    items: [],
    loading: true,
    loadingItems: false,
    _configLoaded: false,
    error: null,
    dirty: new Set(), // work item IDs with unsaved position changes
    dirtyVersion: 0, // incremented on any dirty/dirtyOriginals change for reactivity
    dirtyOriginals: new Map(), // work item ID -> { start_date, target_date } original values
    dirtyLinks: new Map(), // work item ID -> { added: [{target_id, link_type}], removed: [{target_id, link_type}] }
    dirtyLinksVersion: 0, // incremented on any dirtyLinks change for reactivity
    savedLinks: new Map(), // work item ID -> { predecessors: [targetId,...], successors: [targetId,...] }
    savedLinksVersion: 0,
    itemsPositionVersion: 0, // incremented when item dates change
    pushing: false,
  }),

  getters: {
    hasDirty: (state) => { void state.dirtyVersion; return state.dirty.size > 0 || state.dirtyLinks.size > 0 },
    dirtyCount: (state) => {
      void state.dirtyVersion
      const linkIds = new Set(state.dirtyLinks.keys())
      const combined = new Set([...state.dirty, ...linkIds])
      return combined.size
    },
  },

  actions: {
    // --- Config ---
    async loadConfig(force = false) {
      if (this._configLoaded && !force) {
        this.loading = false
        return
      }
      this.loading = true
      this.error = null
      try {
        this.config = await api.get('/api/roadmap/config')
        this._configLoaded = true
      } catch (e) {
        this.error = e.message
      } finally {
        this.loading = false
      }
    },

    async saveConfig(config) {
      this.error = null
      try {
        this.config = await api.put('/api/roadmap/config', config)
      } catch (e) {
        this.error = e.message
        throw e
      }
    },

    // --- Lanes ---
    async addLane(lane) {
      const created = await api.post('/api/roadmap/lanes', lane)
      this.config.lanes.push(created)
      return created
    },

    async updateLane(laneId, lane) {
      const updated = await api.put(`/api/roadmap/lanes/${laneId}`, lane)
      const idx = this.config.lanes.findIndex(l => l.id === laneId)
      if (idx >= 0) this.config.lanes[idx] = updated
      return updated
    },

    async deleteLane(laneId) {
      await api.del(`/api/roadmap/lanes/${laneId}`)
      this.config.lanes = this.config.lanes.filter(l => l.id !== laneId)
    },

    async reorderLanes(laneIds) {
      this.config.lanes = await api.post('/api/roadmap/lanes/reorder', laneIds)
    },

    // --- Projects ---
    async addProject(project) {
      const created = await api.post('/api/roadmap/projects', project)
      this.config.projects.push(created)
      return created
    },

    async removeProject(organization, projectId) {
      await api.del(`/api/roadmap/projects?organization=${encodeURIComponent(organization)}&projectId=${encodeURIComponent(projectId)}`)
      this.config.projects = this.config.projects.filter(
        p => !(p.organization === organization && p.project_id === projectId)
      )
    },

    // --- Work Items ---
    async loadItems() {
      this.loadingItems = true
      this.error = null
      try {
        // Snapshot dirty item fields before refresh so we can re-apply them
        const dirtySnapshot = new Map()
        for (const id of this.dirty) {
          const item = this.items.find(i => i.id === id)
          if (item) {
            dirtySnapshot.set(id, { start_date: item.start_date, target_date: item.target_date })
          }
        }

        const resp = await api.get('/api/roadmap/items')
        this.items = resp.items || []

        // Re-apply unsaved changes on top of fresh data
        for (const [id, fields] of dirtySnapshot) {
          const item = this.items.find(i => i.id === id)
          if (item) Object.assign(item, fields)
        }
      } catch (e) {
        this.error = e.message
      } finally {
        this.loadingItems = false
      }
    },

    // Update a work item field and mark dirty (ensures reactivity)
    updateItem(workItemId, fields) {
      const item = this.items.find(i => i.id === workItemId)
      if (!item) return
      // Store original values before first modification
      if (!this.dirtyOriginals.has(workItemId)) {
        this.dirtyOriginals.set(workItemId, { start_date: item.start_date || '', target_date: item.target_date || '' })
      }
      Object.assign(item, fields)
      // Check if item is back to its original state (compare by calendar date to handle timezone/format differences)
      const original = this.dirtyOriginals.get(workItemId)
      const toDateKey = (v) => {
        if (!v) return ''
        const d = new Date(v)
        if (isNaN(d)) return ''
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
      }
      if (original && toDateKey(item.start_date) === toDateKey(original.start_date) && toDateKey(item.target_date) === toDateKey(original.target_date)) {
        this.dirty.delete(workItemId)
        this.dirtyOriginals.delete(workItemId)
      } else {
        this.dirty.add(workItemId)
      }
      this.dirtyVersion++
      this.itemsPositionVersion++
    },

    // Revert a single dirty item to its original values
    revertItem(workItemId) {
      const original = this.dirtyOriginals.get(workItemId)
      const item = this.items.find(i => i.id === workItemId)
      if (original && item) {
        Object.assign(item, original)
      }
      this.dirty.delete(workItemId)
      this.dirtyOriginals.delete(workItemId)
      this.dirtyLinks.delete(workItemId)
      this.dirtyVersion++
      this.dirtyLinksVersion++
      this.itemsPositionVersion++
    },

    // Mark a work item position as changed locally
    markDirty(workItemId) {
      this.dirty.add(workItemId)
      this.dirtyVersion++
    },

    // Add a dependency link (tracked locally until push)
    addLink(workItemId, targetId, linkType) {
      let entry = this.dirtyLinks.get(workItemId)
      if (!entry) {
        entry = { added: [], removed: [] }
        this.dirtyLinks.set(workItemId, entry)
      }
      // If this link was previously removed in this session, just un-remove it
      const removeIdx = entry.removed.findIndex(l => l.target_id === targetId && l.link_type === linkType)
      if (removeIdx >= 0) {
        entry.removed.splice(removeIdx, 1)
      } else {
        // Only add if not already in added list
        if (!entry.added.some(l => l.target_id === targetId && l.link_type === linkType)) {
          entry.added.push({ target_id: targetId, link_type: linkType })
        }
      }
      if (entry.added.length === 0 && entry.removed.length === 0) {
        this.dirtyLinks.delete(workItemId)
      }
      this.dirtyLinksVersion++
    },

    // Remove a dependency link (tracked locally until push)
    removeLink(workItemId, targetId, linkType) {
      let entry = this.dirtyLinks.get(workItemId)
      if (!entry) {
        entry = { added: [], removed: [] }
        this.dirtyLinks.set(workItemId, entry)
      }
      // If this link was previously added in this session, just un-add it
      const addIdx = entry.added.findIndex(l => l.target_id === targetId && l.link_type === linkType)
      if (addIdx >= 0) {
        entry.added.splice(addIdx, 1)
      } else {
        // Only add to removed if not already there
        if (!entry.removed.some(l => l.target_id === targetId && l.link_type === linkType)) {
          entry.removed.push({ target_id: targetId, link_type: linkType })
        }
      }
      if (entry.added.length === 0 && entry.removed.length === 0) {
        this.dirtyLinks.delete(workItemId)
      }
      this.dirtyLinksVersion++
    },

    // --- Push ---
    async pushPositions() {
      if (this.dirty.size === 0 && this.dirtyLinks.size === 0) return

      this.pushing = true

      // Combine all dirty item IDs (positions + links)
      const allDirtyIds = new Set([...this.dirty, ...this.dirtyLinks.keys()])

      const dirtyItems = this.items
        .filter(i => allDirtyIds.has(i.id))
        .map(i => {
          const linkEntry = this.dirtyLinks.get(i.id)
          return {
            id: i.id,
            organization: i.organization,
            project: i.project,
            start_date: this.dirty.has(i.id) ? (i.start_date ?? null) : undefined,
            target_date: this.dirty.has(i.id) ? (i.target_date ?? null) : undefined,
            links_added: linkEntry?.added?.map(l => ({ target_id: l.target_id, link_type: l.link_type })) || undefined,
            links_removed: linkEntry?.removed?.map(l => ({ target_id: l.target_id, link_type: l.link_type })) || undefined,
          }
        })

      try {
        const resp = await api.post('/api/roadmap/push', dirtyItems)
        const results = resp.results || []
        for (const r of results) {
          if (r.ok) {
            // Move pushed links into savedLinks before clearing
            const linkEntry = this.dirtyLinks.get(r.id)
            if (linkEntry) {
              const saved = this.savedLinks.get(r.id) || { predecessors: [], successors: [] }
              for (const link of (linkEntry.added || [])) {
                if (link.link_type === 'predecessor') {
                  if (!saved.predecessors.includes(link.target_id)) saved.predecessors.push(link.target_id)
                  // Also register on the other side
                  const otherSaved = this.savedLinks.get(link.target_id) || { predecessors: [], successors: [] }
                  if (!otherSaved.successors.includes(r.id)) otherSaved.successors.push(r.id)
                  this.savedLinks.set(link.target_id, otherSaved)
                } else {
                  if (!saved.successors.includes(link.target_id)) saved.successors.push(link.target_id)
                  const otherSaved = this.savedLinks.get(link.target_id) || { predecessors: [], successors: [] }
                  if (!otherSaved.predecessors.includes(r.id)) otherSaved.predecessors.push(r.id)
                  this.savedLinks.set(link.target_id, otherSaved)
                }
              }
              for (const link of (linkEntry.removed || [])) {
                if (link.link_type === 'predecessor') {
                  saved.predecessors = saved.predecessors.filter(id => id !== link.target_id)
                  const otherSaved = this.savedLinks.get(link.target_id)
                  if (otherSaved) otherSaved.successors = otherSaved.successors.filter(id => id !== r.id)
                } else {
                  saved.successors = saved.successors.filter(id => id !== link.target_id)
                  const otherSaved = this.savedLinks.get(link.target_id)
                  if (otherSaved) otherSaved.predecessors = otherSaved.predecessors.filter(id => id !== r.id)
                }
              }
              this.savedLinks.set(r.id, saved)
              this.savedLinksVersion++
            }
            this.dirty.delete(r.id)
            this.dirtyOriginals.delete(r.id)
            this.dirtyLinks.delete(r.id)
          }
        }
        this.dirtyLinksVersion++
        this.dirtyVersion++
        return results
      } catch (e) {
        this.error = e.message
        throw e
      } finally {
        this.pushing = false
      }
    },
  },
})
