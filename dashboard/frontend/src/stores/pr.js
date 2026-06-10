import { defineStore } from 'pinia'
import { useApi } from '../composables/useApi.js'
import { useDemoMode } from '../composables/useDemoMode.js'
import { transformPrProjects } from '../composables/demoTransform.js'

const api = useApi()

export const usePrStore = defineStore('pr', {
  state: () => ({
    prProjects: [],           // list of PrProjectResponse
    prProjectData: {},        // { projectId: PrProjectResponse }
    loadingPrMonitor: false,
    loadingPrProject: false,
    lastFetched: {},
  }),

  getters: {
    displayPrProjects() {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      return transformPrProjects(this.prProjects)
    },
  },

  actions: {
    async fetchPrProjects(force = false) {
      if (!force && this.prProjects.length > 0) return this.prProjects
      this.loadingPrMonitor = true
      try {
        this.prProjects = await api.get('/api/pr-monitor')
        this.lastFetched = { ...this.lastFetched, prMonitor: new Date().toISOString() }
      } catch (e) {
        console.error('Failed to fetch PR monitor data', e)
      } finally {
        this.loadingPrMonitor = false
      }
      return this.prProjects
    },

    async fetchPrProjectData(projectId, force = false) {
      if (!force && this.prProjectData[projectId]) return this.prProjectData[projectId]
      this.loadingPrProject = true
      try {
        const data = await api.get(`/api/pr-monitor/${encodeURIComponent(projectId)}`)
        this.prProjectData[projectId] = data
        this.lastFetched = { ...this.lastFetched, [`prProject_${projectId}`]: new Date().toISOString() }
        return data
      } catch (e) {
        console.error('Failed to fetch project PRs', e)
        return null
      } finally {
        this.loadingPrProject = false
      }
    },
  },
})
