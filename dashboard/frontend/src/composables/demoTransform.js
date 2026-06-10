import { useDemoMode, anonName, anonProject, anonPrTitle, anonRepo, anonOrg, anonEmail, anonDbName, anonIterationPath, anonAreaPath, anonTeam } from './useDemoMode.js'

function clone(obj) {
  return JSON.parse(JSON.stringify(obj))
}

/**
 * Wrap a transform function with a simple identity-based memo.
 * Cache key includes the input reference AND demo mode state,
 * so toggling demo mode invalidates the cache.
 */
function memoTransform(fn) {
  let lastInput = null
  let lastDemo = null
  let lastOutput = null
  return (input) => {
    const { isDemoMode } = useDemoMode()
    const demo = isDemoMode.value
    if (input === lastInput && demo === lastDemo && lastOutput !== null) return lastOutput
    lastInput = input
    lastDemo = demo
    lastOutput = fn(input)
    return lastOutput
  }
}

/** Transform RunResultResponse (checks results) */
export const transformResults = memoTransform(function(results) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !results) return results
  const r = clone(results)
  for (const proj of r.projects) {
    proj.project_name = anonProject(proj.project_name)
    proj.organization = anonOrg(proj.organization)
    for (const check of proj.checks) {
      check.project_name = anonProject(check.project_name)
      for (const item of check.flagged_items) {
        const origProject = item.project
        item.title = anonPrTitle(item.title)
        item.assigned_to = anonName(item.assigned_to)
        item.assigned_to_email = anonEmail(item.assigned_to_email)
        item.project = anonProject(item.project)
        if (item.iteration_path) item.iteration_path = anonIterationPath(item.iteration_path)
        if (item.work_item_type) {
          // Replace project name in embedded iteration paths (e.g. "Bug [Active · ProjectName\Sprint 42]")
          if (origProject) {
            item.work_item_type = item.work_item_type.replaceAll(origProject, anonProject(origProject))
          }
          // Replace repo names in parentheses (e.g. "PR (repo-name)")
          item.work_item_type = item.work_item_type.replace(/\(([^)]+)\)/g, (_, name) => `(${anonRepo(name)})`)
        }
      }
    }
  }
  return r
})

/** Transform PrProjectResponse[] (PR monitor) */
export const transformPrProjects = memoTransform(function(prProjects) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !prProjects) return prProjects
  return prProjects.map(proj => {
    const p = clone(proj)
    p.project_name = anonProject(p.project_name)
    p.organization = anonOrg(p.organization)
    for (const pr of p.prs) {
      pr.title = anonPrTitle(pr.title)
      pr.created_by = anonName(pr.created_by)
      pr.repository = anonRepo(pr.repository)
    }
    return p
  })
})

/** Transform a single PrProjectResponse */
export const transformPrProject = memoTransform(function(proj) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !proj) return proj
  const p = clone(proj)
  p.project_name = anonProject(p.project_name)
  p.organization = anonOrg(p.organization)
  for (const pr of p.prs) {
    pr.title = anonPrTitle(pr.title)
    pr.created_by = anonName(pr.created_by)
    pr.repository = anonRepo(pr.repository)
    pr.reviewers = (pr.reviewers || []).map(r => typeof r === 'string' ? anonName(r) : { ...r, name: anonName(r.name) })
  }
  return p
})

/** Transform DbProjectConfig[] */
export function transformDbProjects(dbProjects) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !dbProjects) return dbProjects
  return dbProjects.map(p => {
    const d = clone(p)
    d.name = anonProject(d.name)
    if (d.name_filter) d.name_filter = anonProject(d.name_filter)
    return d
  })
}

/** Transform database list responses */
export function transformDbDatabases(databases) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !databases) return databases
  return databases.map(db => anonDbName(db))
}

/** Transform AllDbListResponse[] (servers with databases) */
export function transformDbServers(servers) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !servers) return servers
  return servers.map(s => ({
    ...s,
    server_name: anonDbName(s.server_name),
    databases: s.databases.map(db => anonDbName(db)),
  }))
}

/** Transform DbListResponse (single project databases) */
export function transformDbListResponse(resp) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !resp) return resp
  const r = clone(resp)
  r.project_name = anonProject(r.project_name)
  r.databases = r.databases.map(db => anonDbName(db))
  return r
}

/** Transform DbRulesResponse */
export function transformDbRulesResponse(resp) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !resp) return resp
  const r = clone(resp)
  r.project_name = anonProject(r.project_name)
  r.results = r.results.map(item => ({ ...item, name: anonDbName(item.name) }))
  return r
}

/** Transform DevAssessmentResponse */
export const transformDevAssessment = memoTransform(function(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  for (const pr of d.prs || []) {
    pr.title = anonPrTitle(pr.title)
    pr.created_by = anonName(pr.created_by)
    pr.repository = anonRepo(pr.repository)
    pr.project = anonProject(pr.project)
    pr.organization = anonOrg(pr.organization)
    pr.reviewers = (pr.reviewers || []).map(r => typeof r === 'string' ? anonName(r) : { ...r, name: anonName(r.name) })
  }
  for (const wi of d.work_items || []) {
    wi.title = anonPrTitle(wi.title)
    wi.assigned_to = anonName(wi.assigned_to)
    wi.project = anonProject(wi.project)
    wi.organization = anonOrg(wi.organization)
  }
  for (const pr of d.stale_review_prs || []) {
    pr.title = anonPrTitle(pr.title)
    pr.created_by = anonName(pr.created_by)
    pr.repository = anonRepo(pr.repository)
    pr.project = anonProject(pr.project)
    pr.organization = anonOrg(pr.organization)
    pr.reviewers = (pr.reviewers || []).map(r => typeof r === 'string' ? anonName(r) : { ...r, name: anonName(r.name) })
  }
  for (const pr of d.open_prs || []) {
    pr.title = anonPrTitle(pr.title)
    pr.created_by = anonName(pr.created_by)
    pr.repository = anonRepo(pr.repository)
    pr.project = anonProject(pr.project)
    pr.organization = anonOrg(pr.organization)
  }
  return d
})

/** Transform project config list (for sidebar / config) */
export function transformProjects(projects) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !projects) return projects
  return projects.map(p => {
    const proj = clone(p)
    proj.project = anonProject(proj.project)
    proj.organization = anonOrg(proj.organization)
    return proj
  })
}

/** Transform tag work items */
export function transformTagItems(items) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !items) return items
  return items.map(wi => {
    const item = clone(wi)
    item.title = anonPrTitle(item.title)
    if (item.assigned_to) item.assigned_to = anonName(item.assigned_to)
    return item
  })
}

/** Transform sprint populator data */
export function transformSprintProjects(projects) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !projects) return projects
  return projects.map(p => ({
    ...p,
    project: anonProject(p.project),
    organization: anonOrg(p.organization),
  }))
}

/** Transform permission matrix data */
export function transformPermissionMatrix(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.teams) d.teams = d.teams.map(t => ({ ...t, name: anonTeam(t.name) }))
  if (d.members) d.members = d.members.map(m => ({
    ...m,
    display_name: anonName(m.display_name),
    unique_name: anonEmail(m.unique_name),
    team_names: (m.team_names || []).map(t => anonTeam(t)),
  }))
  return d
}

/** Transform repo permission data */
export function transformRepoPermissions(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.repos) d.repos = d.repos.map(r => ({
    ...r,
    repo_name: anonRepo(r.repo_name),
    permissions: (r.permissions || []).map(p => ({
      ...p,
      members_allowed: (p.members_allowed || []).map(n => anonName(n)),
      members_denied: (p.members_denied || []).map(n => anonName(n)),
    })),
  }))
  return d
}

/** Transform area permission data */
export function transformAreaPermissions(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.areas) d.areas = d.areas.map(a => ({
    ...a,
    area_path: anonAreaPath(a.area_path),
    permissions: (a.permissions || []).map(p => ({
      ...p,
      members_allowed: (p.members_allowed || []).map(n => anonName(n)),
      members_denied: (p.members_denied || []).map(n => anonName(n)),
    })),
  }))
  return d
}

/** Transform pipeline runs data */
export function transformPipelineRuns(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.runs) d.runs = d.runs.map(r => ({
    ...r,
    pipeline_name: anonPrTitle(r.pipeline_name),
    branch: r.branch ? anonRepo(r.branch) : '',
    requested_by: anonName(r.requested_by),
  }))
  return d
}

/** Transform release runs data */
export function transformReleaseRuns(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.runs) d.runs = d.runs.map(r => ({
    ...r,
    release_name: anonPrTitle(r.release_name),
    definition_name: anonPrTitle(r.definition_name),
    created_by: anonName(r.created_by),
  }))
  return d
}

/** Transform velocity capacity data */
export function transformVelocityCapacity(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.iteration_name) d.iteration_name = anonIterationPath(d.iteration_name)
  if (d.members) d.members = d.members.map(m => ({
    ...m,
    display_name: anonName(m.display_name),
  }))
  return d
}

/** Transform velocity calculation result */
export function transformVelocityCalc(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.last_sprint?.name) d.last_sprint.name = anonIterationPath(d.last_sprint.name)
  if (d.target_sprint?.name) d.target_sprint.name = anonIterationPath(d.target_sprint.name)
  return d
}

/** Transform velocity metrics data */
export function transformVelocityMetrics(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.projects) d.projects = d.projects.map(p => ({
    ...p,
    project_name: anonProject(p.project_name),
    team_name: anonTeam(p.team_name),
    sprints: (p.sprints || []).map(s => ({
      ...s,
      name: anonIterationPath(s.name),
    })),
  }))
  return d
}

/** Transform people list (check-permissions) */
export function transformPeopleList(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.people) d.people = d.people.map(p => ({
    ...p,
    display_name: anonName(p.display_name),
    unique_name: anonEmail(p.unique_name),
    organization: anonOrg(p.organization),
  }))
  return d
}

/** Transform person groups result (check-permissions) */
export function transformPersonGroups(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.groups) d.groups = d.groups.map(g => {
    const name = g.display_name || ''
    const match = name.match(/^\[(.+?)\]\\(.+)$/)
    if (match) {
      return { ...g, display_name: `[${anonProject(match[1])}]\\${match[2]}` }
    }
    return g
  })
  return d
}

/** Transform permission audit results (check-permissions) */
export function transformPermissionAudit(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  const d = clone(data)
  if (d.projects) d.projects = d.projects.map(p => ({
    ...p,
    project: anonProject(p.project),
    organization: anonOrg(p.organization),
    _realOrg: p.organization,
    checks: {
      ...p.checks,
      teams_in_groups: (p.checks.teams_in_groups || []).map(t => ({
        ...t,
        team_name: anonTeam(t.team_name),
      })),
      repos_missing_groups: (p.checks.repos_missing_groups || []).map(r => ({
        ...r,
        repo_name: anonRepo(r.repo_name),
      })),
      areas_missing_groups: (p.checks.areas_missing_groups || []).map(a => ({
        ...a,
        area_path: anonAreaPath(a.area_path),
      })),
    },
  }))
  return d
}

/** Transform audit projects list (scope panel) */
export function transformAuditProjects(data) {
  const { isDemoMode } = useDemoMode()
  if (!isDemoMode.value || !data) return data
  return data.map(p => ({
    ...p,
    project: anonProject(p.project),
    organization: anonOrg(p.organization),
    _realOrg: p.organization,
  }))
}
