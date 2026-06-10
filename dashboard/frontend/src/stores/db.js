import { defineStore } from 'pinia'
import { useApi } from '../composables/useApi.js'
import { useDemoMode } from '../composables/useDemoMode.js'
import { transformDbProjects } from '../composables/demoTransform.js'

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

export const useDbStore = defineStore('db', {
  state: () => ({
    dbProjects: [],
    allDatabases: [],
    allDbServers: [],
    dbProjectDatabases: {},
    dbRuleResults: {},
    runningDbRules: false,
    lastFetched: {},
  }),

  getters: {
    displayDbProjects() {
      const { isDemoMode } = useDemoMode()
      void isDemoMode.value
      return transformDbProjects(this.dbProjects)
    },
  },

  actions: {
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

    async runAllDbRules(dbCredentialsConfigured) {
      this.runningDbRules = true
      try {
        await this.fetchDbProjects()
        if (!dbCredentialsConfigured || this.dbProjects.length === 0) return

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
  },
})
