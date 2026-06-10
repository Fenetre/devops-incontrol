import { defineStore } from 'pinia'
import { useApi, setApiKey } from '../composables/useApi.js'
import { useDemoMode } from '../composables/useDemoMode.js'
import { transformResults, transformPrProjects, transformDbProjects, transformProjects, transformPermissionMatrix, transformRepoPermissions, transformPipelineRuns, transformReleaseRuns } from '../composables/demoTransform.js'

const api = useApi()
const MAX_CONCURRENT_DB_PROJECT_REQUESTS = 6
const MAX_CONCURRENT_DB_RULE_CHECKS = 4

async function runWithConcurrency(items, limit, worker) {
  if (!items.length) return
  const maxWorkers = Math.min(Math.max(1, limit), items.length)
  let nextIndex = 0

  const workers = Array.from({ length: maxWorkers }, async () => {
    while (true) {
      const current = nextIndex
      nextIndex += 1
      if (current >= items.length) break
      await worker(items[current], current)
    }
  })

  await Promise.all(workers)
}

export const useMonitorStore = defineStore('monitor', {
  state: () => ({
    apiKeyConfigured: false,
    allowUnprotectedApi: false,
    authenticated: false,
    setupComplete: false,
    setupProjectCount: 0,
    patConfigured: false,
    dbCredentialsConfigured: false,
    dbServers: [],  // [{index, configured, server}]
    emailFrom: '',
    emailFromConfigured: false,
    projects: [],
    auditDenylist: [],    // list of "org/project_id" composite keys
    auditProjects: [],     // [{ organization, project, project_id }] — all discoverable projects
    loadingAuditProjects: false,
    auditConfig: { group_config: [], rules: [] },  // persistent audit configuration
    dbProjects: [],
    allDatabases: [],
    allDbServers: [],  // [{server_index, server_name, databases}]
    dbProjectDatabases: {},  // { projectId: { databases: [...] } }
    dbRuleResults: {},       // { projectId: { results: [...] } }
    runningDbRules: false,
    results: null,        // RunResultResponse
    checkTypes: [],
    organizations: [],    // [{name}] — known org names from saved projects
    orgProjects: [],      // [{name, id}] for selected org
    areaPaths: [],        // [{path}] for selected project
    repos: [],             // [{name, id}] for selected project
    loadingOrgProjects: false,
    loadingAreaPaths: false,
    loadingRepos: false,
    orgProjectsError: null,
    loading: false,
    error: null,
    runningChecks: false,
    runningProjects: {},  // { projectId: true } for per-project runs
    // PR Monitor
    prProjects: [],           // list of PrProjectResponse
    prProjectData: {},        // { projectId: PrProjectResponse }
    loadingPrMonitor: false,
    loadingPrProject: false,
    // Dev Assessment cache
    devAssessmentData: null,
    devAssessmentMonths: 3,
    devAssessmentSelectedProject: null,
    devAssessmentSelectedDev: null,
    // Permission Check
    permissionData: {},   // { projectId: PermissionMatrixResponse }
    loadingPermissions: false,
    repoPermissionData: {},   // { projectId: RepoPermissionResponse }
    loadingRepoPermissions: false,
    areaPermissionData: {},   // { projectId: AreaPermissionResponse }
    loadingAreaPermissions: false,
    repoList: {},             // { projectId: [{id, name}] } — lightweight list for dropdowns
    areaList: {},             // { projectId: [{id, name}] } — lightweight list for dropdowns
    pipelineRunData: {},      // { cacheKey: PipelineRunsResponse }
    loadingPipelineRuns: false,
    releaseRunData: {},       // { cacheKey: ReleaseRunsResponse }
    loadingReleaseRuns: false,
    // Velocity
    velocityProjects: [],
    velocityTeams: {},        // { projectId: [{id, name}] }
    velocityIterations: {},   // { projectId:team: VelocityIterationInfo[] }
    velocityCapacity: {},     // { key: TeamCapacityResponse }
    velocityMetrics: null,    // VelocityMetricsResponse
    loadingVelocity: false,
    // Check Permissions
    personSearchResults: null,
    loadingPersonSearch: false,
    peopleList: null,
    loadingPeople: false,
    personGroups: {},        // { descriptor: { groups: [...], loading } }
    permissionAuditResults: null,
    loadingPermissionAudit: false,
    // Memo caches for DevOps lookups (cleared on page reload)
    _orgProjectsCache: {},
    _areaPathsCache: {},
    _reposCache: {},
    // Client-side fetch timestamps per data category
    lastFetched: {},  // { key: ISO string }
  }),

  getters: {
    // --- Demo-aware display getters ---
    displayResults() {
      // Force reactivity on isDemoMode so getters recompute on toggle
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      return transformResults(this.results)
    },
    displayPrProjects() {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      return transformPrProjects(this.prProjects)
    },
    displayDbProjects() {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      return transformDbProjects(this.dbProjects)
    },
    displayProjects() {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      return transformProjects(this.projects)
    },

    /** Projects that have at least one flagged issue in results */
    projectsWithIssues() {
      const results = this.displayResults
      if (!results) return []
      return results.projects.filter(p => p.total_issues > 0)
    },

    /** Projects that have zero issues in results */
    projectsWithoutIssues() {
      const results = this.displayResults
      if (!results) return []
      const withIssues = new Set(
        results.projects.filter(p => p.total_issues > 0).map(p => p.project_id)
      )
      return this.displayProjects.filter(p => !withIssues.has(p.id))
    },

    /** Get issues for a specific project and check type */
    issuesByProjectAndType: (state) => (projectId, checkType) => {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      const results = transformResults(state.results)
      if (!results) return null
      const proj = results.projects.find(p => p.project_id === projectId)
      if (!proj) return null
      return proj.checks.find(c => c.check_type === checkType) || null
    },

    /** Get all results for a project */
    resultForProject: (state) => (projectId) => {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      const results = transformResults(state.results)
      if (!results) return null
      return results.projects.find(p => p.project_id === projectId) || null
    },

    /** Sidebar menu items — only projects with issues, with their non-empty check types */
    sidebarItems() {
      const results = this.displayResults
      if (!results) return []
      return results.projects
        .filter(p => p.total_issues > 0 || p.checks.some(c => c.error))
        .map(p => ({
          projectId: p.project_id,
          projectName: p.project_name,
          totalIssues: p.total_issues,
          hasErrors: p.checks.some(c => c.error),
          checks: p.checks
            .filter(c => c.flagged_items.length > 0)
            .map(c => ({
              checkType: c.check_type,
              label: c.check_label,
              count: c.flagged_items.length,
            })),
        }))
    },
  },

  actions: {
    _toast(message, type = 'error') {
      if (this._toastFn) {
        this._toastFn(message, type)
      }
    },

    async fetchApiKeyStatus() {
      try {
        const data = await api.get('/api/settings/api-key/status')
        this.apiKeyConfigured = !!data.configured
        this.allowUnprotectedApi = !!data.allow_unprotected

        // When API key is configured, backend can still mark us authenticated via
        // a signed HttpOnly session cookie.
        this.authenticated = this.apiKeyConfigured ? !!data.authenticated : this.allowUnprotectedApi

        if (!this.apiKeyConfigured) {
          setApiKey('')
        }
      } catch {
        this.apiKeyConfigured = false
        this.allowUnprotectedApi = false
        this.authenticated = false
      }
    },

    async verifyApiKey(key) {
      const data = await api.post('/api/settings/api-key/verify', { api_key: key })
      if (data.valid) {
        setApiKey(key)
        this.authenticated = true
      }
      return data.valid
    },

    async saveApiKey(apiKey) {
      await api.post('/api/settings/api-key', { api_key: apiKey })
      setApiKey(apiKey)
      this.apiKeyConfigured = !!apiKey
      this.authenticated = true
    },

    setLocalApiKey(apiKey) {
      setApiKey(apiKey)
    },

    async fetchPatStatus() {
      try {
        const data = await api.get('/api/settings/pat/status')
        this.patConfigured = data.configured
      } catch {
        this.patConfigured = false
      }
    },

    async savePat(pat) {
      await api.post('/api/settings/pat', { pat })
      this.patConfigured = !!pat
    },

    async fetchDbCredentialsStatus() {
      try {
        const data = await api.get('/api/settings/db/status')
        this.dbServers = data.servers || []
        this.dbCredentialsConfigured = this.dbServers.some(s => s.configured)
      } catch {
        this.dbCredentialsConfigured = false
        this.dbServers = []
      }
    },

    async saveDbCredentials(index, server, port, username, password, driver = 'sqlserver', refreshStatus = true) {
      await api.post('/api/settings/db', { index, server, port, username, password, driver })
      if (refreshStatus) {
        await this.fetchDbCredentialsStatus()
      }
    },

    async testDbConnection(index = 0) {
      const data = await api.post('/api/settings/db/test', { index })
      return data.message
    },

    async fetchEmailFromStatus() {
      try {
        const data = await api.get('/api/settings/email-from/status')
        this.emailFromConfigured = data.configured
        this.emailFrom = data.email_from || ''
      } catch {
        this.emailFromConfigured = false
        this.emailFrom = ''
      }
    },

    async saveEmailFrom(emailFrom) {
      await api.post('/api/settings/email-from', { email_from: emailFrom })
      this.emailFrom = emailFrom
      this.emailFromConfigured = !!emailFrom
    },

    async sendCheckMail(projectId, checkType, to, assignedTo = null) {
      const body = { to }
      if (assignedTo) body.assigned_to = assignedTo
      return await api.post(`/api/checks/send-mail/${encodeURIComponent(projectId)}/${encodeURIComponent(checkType)}`, body)
    },

    async fetchParentTypeHierarchy(projectId) {
      return await api.get(`/api/checks/parent-type-hierarchy/${encodeURIComponent(projectId)}`)
    },

    async fetchCandidateParents(projectId, workItemId, workItemType) {
      return await api.get(`/api/checks/candidate-parents/${encodeURIComponent(projectId)}?work_item_id=${workItemId}&work_item_type=${encodeURIComponent(workItemType)}`)
    },

    async assignParent(projectId, workItemId, parentId) {
      return await api.post(`/api/checks/assign-parent/${encodeURIComponent(projectId)}`, { work_item_id: workItemId, parent_id: parentId })
    },

    async fetchWorkItemPreview(projectId, workItemId) {
      return await api.get(`/api/checks/work-item-preview/${encodeURIComponent(projectId)}?work_item_id=${workItemId}`)
    },

    async fetchKnownOrganizations() {
      try {
        this.organizations = await api.get('/api/devops/organizations')
      } catch (e) {
        this.organizations = []
        this._toast(e.message || 'Failed to load organizations')
      }
    },

    async fetchOrgProjects(org) {
      if (this._orgProjectsCache[org]) {
        this.orgProjects = this._orgProjectsCache[org]
        return
      }
      this.loadingOrgProjects = true
      this.orgProjectsError = null
      try {
        this.orgProjects = await api.get(`/api/devops/organizations/${encodeURIComponent(org)}/projects`)
        this._orgProjectsCache[org] = this.orgProjects
      } catch (e) {
        this.orgProjects = []
        this.orgProjectsError = e.message || 'Failed to load projects'
      } finally {
        this.loadingOrgProjects = false
      }
    },

    async fetchAreaPaths(org, project) {
      const cacheKey = `${org}/${project}`
      if (this._areaPathsCache[cacheKey]) {
        this.areaPaths = this._areaPathsCache[cacheKey]
        return
      }
      this.loadingAreaPaths = true
      this.areaPaths = []
      try {
        this.areaPaths = await api.get(`/api/devops/organizations/${encodeURIComponent(org)}/projects/${encodeURIComponent(project)}/areas`)
        this._areaPathsCache[cacheKey] = this.areaPaths
      } catch (e) {
        this.areaPaths = []
        this._toast(e.message || 'Failed to load area paths')
      } finally {
        this.loadingAreaPaths = false
      }
    },

    async fetchRepos(org, project) {
      const cacheKey = `${org}/${project}`
      if (this._reposCache[cacheKey]) {
        this.repos = this._reposCache[cacheKey]
        return
      }
      this.loadingRepos = true
      this.repos = []
      try {
        this.repos = await api.get(`/api/devops/organizations/${encodeURIComponent(org)}/projects/${encodeURIComponent(project)}/repos`)
        this._reposCache[cacheKey] = this.repos
      } catch (e) {
        this.repos = []
        this._toast(e.message || 'Failed to load repositories')
      } finally {
        this.loadingRepos = false
      }
    },

    async fetchProjects() {
      this.projects = await api.get('/api/projects')
    },

    async addProject(project) {
      const created = await api.post('/api/projects', project)
      this.projects.push(created)
      return created
    },

    async updateProject(id, project) {
      const updated = await api.put(`/api/projects/${encodeURIComponent(id)}`, project)
      const idx = this.projects.findIndex(p => p.id === id)
      if (idx !== -1) this.projects[idx] = updated
      return updated
    },

    async deleteProject(id) {
      await api.del(`/api/projects/${encodeURIComponent(id)}`)
      this.projects = this.projects.filter(p => p.id !== id)
    },

    async fetchAuditDenylist() {
      this.auditDenylist = await api.get('/api/check-permissions/denylist')
    },

    async addToAuditDenylist(key) {
      this.auditDenylist = await api.post('/api/check-permissions/denylist', { key })
    },

    async removeFromAuditDenylist(key) {
      this.auditDenylist = await api.del('/api/check-permissions/denylist', { key })
    },

    async fetchAuditProjects(retries = 2) {
      this.loadingAuditProjects = true
      try {
        this.auditProjects = await api.get('/api/check-permissions/projects')
      } catch (e) {
        if (retries > 0) {
          await new Promise(r => setTimeout(r, 2000))
          this.loadingAuditProjects = false
          return this.fetchAuditProjects(retries - 1)
        }
        this._toast(e.message || 'Failed to load discoverable projects')
      } finally {
        this.loadingAuditProjects = false
      }
    },

    async fetchAuditConfig() {
      try {
        this.auditConfig = await api.get('/api/check-permissions/audit-config')
      } catch (e) {
        this._toast(e.message || 'Failed to load audit configuration')
      }
    },

    async saveAuditConfig(config) {
      try {
        this.auditConfig = await api.put('/api/check-permissions/audit-config', config)
        this._toast('Audit configuration saved', 'success')
      } catch (e) {
        this._toast(e.message || 'Failed to save audit configuration')
      }
    },

    async fetchCheckTypes() {
      this.checkTypes = await api.get('/api/projects/checks/types')
    },

    async runChecks() {
      this.runningChecks = true
      this.error = null
      // Initialise results shell so cards render immediately
      if (!this.results) {
        this.results = { ran_at: new Date().toISOString(), total_issues: 0, projects: [] }
      }
      try {
        // Fire all projects in parallel; each updates the UI on completion
        const promises = this.projects.map(p =>
          this.runChecksForProject(p.id).catch(() => { /* per-project errors handled inside */ })
        )
        await Promise.all(promises)
        this.results.ran_at = new Date().toISOString()
      } catch (e) {
        this.error = e.message
      } finally {
        this.runningChecks = false
      }
    },

    async runChecksForProject(projectId) {
      this.runningProjects = { ...this.runningProjects, [projectId]: true }
      this.error = null
      try {
        const projectResult = await api.post(`/api/checks/run/${encodeURIComponent(projectId)}`)
        // Merge into existing results
        if (!this.results) {
          this.results = { ran_at: new Date().toISOString(), total_issues: 0, projects: [] }
        }
        const idx = this.results.projects.findIndex(p => p.project_id === projectId)
        if (idx !== -1) {
          this.results.projects[idx] = projectResult
        } else {
          this.results.projects.push(projectResult)
        }
        this.results.total_issues = this.results.projects.reduce((sum, p) => sum + p.total_issues, 0)
        return projectResult
      } catch (e) {
        this.error = e.message
        throw e
      } finally {
        const copy = { ...this.runningProjects }
        delete copy[projectId]
        this.runningProjects = copy
      }
    },

    async fetchCachedResults() {
      try {
        const data = await api.get('/api/checks/results')
        if (data && data.ran_at) {
          this.results = data
        }
      } catch (e) {
        this._toast(e.message || 'Failed to load cached results')
      }
    },

    // Setup wizard
    async fetchSetupStatus() {
      try {
        const data = await api.get('/api/settings/setup-status')
        this.setupComplete = data.setup_complete
        this.patConfigured = data.pat_configured
        this.setupProjectCount = data.project_count
        return data
      } catch {
        return null
      }
    },

    async completeSetup() {
      await api.post('/api/settings/setup-complete', {})
      this.setupComplete = true
    },

    // DB projects
    async fetchDbProjects() {
      this.dbProjects = await api.get('/api/db-projects')
    },

    async addDbProject(project) {
      const created = await api.post('/api/db-projects', project)
      this.dbProjects.push(created)
      return created
    },

    async updateDbProject(id, project) {
      const updated = await api.put(`/api/db-projects/${encodeURIComponent(id)}`, project)
      const idx = this.dbProjects.findIndex(p => p.id === id)
      if (idx !== -1) this.dbProjects[idx] = updated
      return updated
    },

    async deleteDbProject(id) {
      await api.del(`/api/db-projects/${encodeURIComponent(id)}`)
      this.dbProjects = this.dbProjects.filter(p => p.id !== id)
    },

    async fetchDbProjectDatabases(projectId) {
      return await api.get(`/api/db-projects/${encodeURIComponent(projectId)}/databases`)
    },

    async toggleDbAllowlist(projectId, databaseName) {
      return await api.post(`/api/db-projects/${encodeURIComponent(projectId)}/allowlist/toggle`, { database_name: databaseName })
    },

    async checkDbProjectRules(projectId) {
      return await api.post(`/api/db-projects/${encodeURIComponent(projectId)}/databases/check-rules`)
    },

    async fetchAllDatabases() {
      try {
        const data = await api.get('/api/db-projects/databases/all')
        this.allDatabases = data.databases || []
        this.allDbServers = data.servers || []
        this.lastFetched = { ...this.lastFetched, dbMonitor: new Date().toISOString() }
      } catch {
        this.allDatabases = []
        this.allDbServers = []
      }
    },

    async refreshDbSidebar() {
      const projects = [...this.dbProjects]
      const allDatabasesTask = this.fetchAllDatabases()

      const dbProjectsTask = runWithConcurrency(
        projects,
        MAX_CONCURRENT_DB_PROJECT_REQUESTS,
        async (proj) => {
          try {
            const result = await this.fetchDbProjectDatabases(proj.id)
            this.dbProjectDatabases[proj.id] = result
          } catch {
            // skip on error
          }
        }
      )

      await Promise.all([allDatabasesTask, dbProjectsTask])
    },

    async runAllDbRules() {
      this.runningDbRules = true
      try {
        await Promise.all([
          this.fetchDbProjects(),
          this.fetchDbCredentialsStatus(),
        ])
        if (!this.dbCredentialsConfigured || this.dbProjects.length === 0) return

        await runWithConcurrency(
          [...this.dbProjects],
          MAX_CONCURRENT_DB_RULE_CHECKS,
          async (proj) => {
            try {
              const result = await this.checkDbProjectRules(proj.id)
              this.dbRuleResults[proj.id] = result
            } catch {
              // skip on error
            }
          }
        )
        this.lastFetched = { ...this.lastFetched, dbRules: new Date().toISOString() }
      } finally {
        this.runningDbRules = false
      }
    },

    // PR Monitor
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

    // --- Check Permissions ---
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
      // Patch the local results
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

    // --- Velocity ---
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
      const data = await api.get(`/api/velocity/${encodeURIComponent(projectId)}/metrics?team=${encodeURIComponent(team)}&sprints=${sprints}`)
      return data
    },

    async fetchReleaseNotes() {
      // Skip SWR cache — this is rarely called and caching empty results causes intermittent failures
      api.invalidate('/api/release-notes')
      return await api.get('/api/release-notes')
    },
  },
})
