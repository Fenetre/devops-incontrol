import { defineStore } from 'pinia'
import { useApi } from '../composables/useApi.js'
import { useDemoMode } from '../composables/useDemoMode.js'
import { transformPermissionMatrix, transformRepoPermissions } from '../composables/demoTransform.js'

const api = useApi()

export const usePermissionStore = defineStore('permission', {
  state: () => ({
    permissionData: {},       // { projectId: PermissionMatrixResponse }
    loadingPermissions: false,
    repoPermissionData: {},   // { projectId: RepoPermissionResponse }
    loadingRepoPermissions: false,
    areaPermissionData: {},   // { projectId: AreaPermissionResponse }
    loadingAreaPermissions: false,
    repoList: {},             // { projectId: [{id, name}] }
    areaList: {},             // { projectId: [{id, name}] }
    pipelineRunData: {},      // { cacheKey: PipelineRunsResponse }
    loadingPipelineRuns: false,
    releaseRunData: {},       // { cacheKey: ReleaseRunsResponse }
    loadingReleaseRuns: false,
    // Check Permissions
    personSearchResults: null,
    loadingPersonSearch: false,
    peopleList: null,
    loadingPeople: false,
    personGroups: {},
    permissionAuditResults: null,
    loadingPermissionAudit: false,
  }),

  actions: {
    async fetchPermissions(projectId, force = false) {
      if (!force && this.permissionData[projectId]) return this.permissionData[projectId]
      this.loadingPermissions = true
      try {
        const url = `/api/permission-check/${encodeURIComponent(projectId)}${force ? '?force=true' : ''}`
        const data = await api.get(url)
        this.permissionData[projectId] = data
        return data
      } catch (e) {
        console.error('Failed to fetch permissions', e)
        throw e
      } finally {
        this.loadingPermissions = false
      }
    },

    async fetchRepoPermissions(projectId, force = false) {
      if (!force && this.repoPermissionData[projectId]) return this.repoPermissionData[projectId]
      this.loadingRepoPermissions = true
      try {
        const url = `/api/permission-check/${encodeURIComponent(projectId)}/repos${force ? '?force=true' : ''}`
        const data = await api.get(url)
        this.repoPermissionData[projectId] = data
        return data
      } catch (e) {
        console.error('Failed to fetch repo permissions', e)
        throw e
      } finally {
        this.loadingRepoPermissions = false
      }
    },

    async fetchAreaPermissions(projectId, force = false) {
      if (!force && this.areaPermissionData[projectId]) return this.areaPermissionData[projectId]
      this.loadingAreaPermissions = true
      try {
        const url = `/api/permission-check/${encodeURIComponent(projectId)}/areas${force ? '?force=true' : ''}`
        const data = await api.get(url)
        this.areaPermissionData[projectId] = data
        return data
      } catch (e) {
        console.error('Failed to fetch area permissions', e)
        throw e
      } finally {
        this.loadingAreaPermissions = false
      }
    },

    async fetchRepoList(projectId) {
      if (this.repoList[projectId]) return this.repoList[projectId]
      try {
        const data = await api.get(`/api/permission-check/${encodeURIComponent(projectId)}/repo-list`)
        this.repoList[projectId] = data
        return data
      } catch (e) {
        console.error('Failed to fetch repo list', e)
        return []
      }
    },

    async fetchAreaList(projectId) {
      if (this.areaList[projectId]) return this.areaList[projectId]
      try {
        const data = await api.get(`/api/permission-check/${encodeURIComponent(projectId)}/area-list`)
        this.areaList[projectId] = data
        return data
      } catch (e) {
        console.error('Failed to fetch area list', e)
        return []
      }
    },

    async fetchPipelineRuns(projectId, minDate = '', maxDate = '', force = false) {
      const cacheKey = `${projectId}:${minDate}:${maxDate}`
      if (!force && this.pipelineRunData[cacheKey]) return this.pipelineRunData[cacheKey]
      this.loadingPipelineRuns = true
      try {
        let url = `/api/permission-check/${encodeURIComponent(projectId)}/pipelines`
        const params = []
        if (minDate) params.push(`minDate=${encodeURIComponent(minDate)}`)
        if (maxDate) params.push(`maxDate=${encodeURIComponent(maxDate)}`)
        if (force) params.push('force=true')
        if (params.length) url += '?' + params.join('&')
        const data = await api.get(url)
        this.pipelineRunData[cacheKey] = data
        return data
      } catch (e) {
        console.error('Failed to fetch pipeline runs', e)
        throw e
      } finally {
        this.loadingPipelineRuns = false
      }
    },

    async fetchReleaseRuns(projectId, minDate = '', maxDate = '', force = false) {
      const cacheKey = `${projectId}:${minDate}:${maxDate}`
      if (!force && this.releaseRunData[cacheKey]) return this.releaseRunData[cacheKey]
      this.loadingReleaseRuns = true
      try {
        let url = `/api/permission-check/${encodeURIComponent(projectId)}/releases`
        const params = []
        if (minDate) params.push(`minDate=${encodeURIComponent(minDate)}`)
        if (maxDate) params.push(`maxDate=${encodeURIComponent(maxDate)}`)
        if (force) params.push('force=true')
        if (params.length) url += '?' + params.join('&')
        const data = await api.get(url)
        this.releaseRunData[cacheKey] = data
        return data
      } catch (e) {
        console.error('Failed to fetch release runs', e)
        throw e
      } finally {
        this.loadingReleaseRuns = false
      }
    },

    async fetchPeople(force = false) {
      if (!force && this.peopleList) return this.peopleList
      this.loadingPeople = true
      try {
        const data = await api.get(`/api/check-permissions/people${force ? '?force=true' : ''}`)
        this.peopleList = data
        return data
      } catch (e) {
        console.error('Failed to fetch people', e)
        throw e
      } finally {
        this.loadingPeople = false
      }
    },

    async fetchPersonGroups(org, descriptor) {
      if (this.personGroups[descriptor]?.groups) return this.personGroups[descriptor]
      this.personGroups[descriptor] = { groups: null, loading: true }
      try {
        const data = await api.get(`/api/check-permissions/person-groups?org=${encodeURIComponent(org)}&descriptor=${encodeURIComponent(descriptor)}`)
        const groups = data?.results?.[0]?.groups || []
        this.personGroups[descriptor] = { groups, loading: false }
        return { groups }
      } catch (e) {
        console.error('Failed to fetch person groups', e)
        this.personGroups[descriptor] = { groups: null, loading: false }
        throw e
      }
    },

    async runPermissionAudit(force = false) {
      this.loadingPermissionAudit = true
      try {
        const data = await api.post(`/api/check-permissions/audit${force ? '?force=true' : ''}`)
        this.permissionAuditResults = data
        return data
      } catch (e) {
        console.error('Failed to run permission audit', e)
        throw e
      } finally {
        this.loadingPermissionAudit = false
      }
    },

    async refreshSingleProjectAudit(org, projectId) {
      const data = await api.post(`/api/check-permissions/audit/${encodeURIComponent(org)}/${encodeURIComponent(projectId)}`)
      if (this.permissionAuditResults) {
        const idx = this.permissionAuditResults.projects.findIndex(
          p => p.project_id === projectId && p.organization === org
        )
        if (idx >= 0) {
          this.permissionAuditResults.projects[idx] = data
        }
      }
      return data
    },
  },
})
