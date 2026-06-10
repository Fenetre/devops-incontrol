import { defineStore } from 'pinia'
import { useApi } from '../composables/useApi.js'

const api = useApi()

export const useVelocityStore = defineStore('velocity', {
  state: () => ({
    velocityProjects: [],
    velocityTeams: {},        // { projectId: [{id, name}] }
    velocityIterations: {},   // { projectId:team: VelocityIterationInfo[] }
    velocityCapacity: {},     // { key: TeamCapacityResponse }
    velocityMetrics: null,    // VelocityMetricsResponse
    loadingVelocity: false,
    lastFetched: {},
  }),

  actions: {
    async fetchVelocityProjects() {
      try {
        this.velocityProjects = await api.get('/api/velocity/projects')
      } catch (e) {
        console.error('Failed to fetch velocity projects', e)
        this.velocityProjects = []
      }
    },

    async fetchVelocityTeams(projectId) {
      if (this.velocityTeams[projectId]) return this.velocityTeams[projectId]
      try {
        const data = await api.get(`/api/velocity/${encodeURIComponent(projectId)}/teams`)
        this.velocityTeams[projectId] = data
        return data
      } catch (e) {
        console.error('Failed to fetch velocity teams', e)
        return []
      }
    },

    async fetchVelocityTeamMembers(projectId, team) {
      try {
        return await api.get(`/api/velocity/${encodeURIComponent(projectId)}/team-members?team=${encodeURIComponent(team)}`)
      } catch (e) {
        console.error('Failed to fetch team members', e)
        return []
      }
    },

    async fetchVelocityIterations(projectId, team) {
      const key = `${projectId}:${team}`
      if (this.velocityIterations[key]) return this.velocityIterations[key]
      try {
        const data = await api.get(`/api/velocity/${encodeURIComponent(projectId)}/iterations?team=${encodeURIComponent(team)}`)
        this.velocityIterations[key] = data
        return data
      } catch (e) {
        console.error('Failed to fetch velocity iterations', e)
        return []
      }
    },

    async fetchVelocityCapacity(projectId, team, iterationId) {
      try {
        return await api.get(`/api/velocity/${encodeURIComponent(projectId)}/capacity?team=${encodeURIComponent(team)}&iterationId=${encodeURIComponent(iterationId)}`)
      } catch (e) {
        console.error('Failed to fetch capacity', e)
        throw e
      }
    },

    async pushVelocityCapacity(projectId, body) {
      try {
        return await api.put(`/api/velocity/${encodeURIComponent(projectId)}/capacity`, body)
      } catch (e) {
        console.error('Failed to push capacity', e)
        throw e
      }
    },

    async fetchPreviousCapacity(projectId, team, iterationId) {
      try {
        return await api.get(`/api/velocity/${encodeURIComponent(projectId)}/previous-capacity?team=${encodeURIComponent(team)}&iterationId=${encodeURIComponent(iterationId)}`)
      } catch (e) {
        console.error('Failed to fetch previous capacity', e)
        throw e
      }
    },

    async fetchSprintPoints(projectId, team, iterationId) {
      try {
        return await api.get(`/api/velocity/${encodeURIComponent(projectId)}/sprint-points?team=${encodeURIComponent(team)}&iterationId=${encodeURIComponent(iterationId)}`)
      } catch (e) {
        console.error('Failed to fetch sprint points', e)
        throw e
      }
    },

    async calculateVelocity(projectId, team, lastIterationId, targetIterationId, overridePoints = null, includeUnassigned = false) {
      try {
        let url = `/api/velocity/${encodeURIComponent(projectId)}/calculate?team=${encodeURIComponent(team)}&lastIterationId=${encodeURIComponent(lastIterationId)}&targetIterationId=${encodeURIComponent(targetIterationId)}`
        if (overridePoints !== null) url += `&overridePoints=${overridePoints}`
        if (includeUnassigned) url += `&includeUnassigned=true`
        return await api.get(url)
      } catch (e) {
        console.error('Failed to calculate velocity', e)
        throw e
      }
    },

    async fetchVelocityMetrics(sprints = 5) {
      this.loadingVelocity = true
      try {
        this.velocityMetrics = await api.get(`/api/velocity/metrics?sprints=${sprints}`)
        this.lastFetched = { ...this.lastFetched, velocity: new Date().toISOString() }
        return this.velocityMetrics
      } catch (e) {
        console.error('Failed to fetch velocity metrics', e)
        throw e
      } finally {
        this.loadingVelocity = false
      }
    },

    async fetchVelocityProjectMetrics(projectId, team, sprints = 10) {
      return await api.get(`/api/velocity/${encodeURIComponent(projectId)}/metrics?team=${encodeURIComponent(team)}&sprints=${sprints}`)
    },
  },
})
