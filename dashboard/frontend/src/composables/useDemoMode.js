import { ref, watch } from 'vue'

const STORAGE_KEY = 'demo_mode'

const isDemoMode = ref(localStorage.getItem(STORAGE_KEY) === 'true')

watch(isDemoMode, (v) => {
  localStorage.setItem(STORAGE_KEY, v ? 'true' : 'false')
})

// --- Name pools ---
const PERSON_NAMES = [
  'Gary Thompson', 'Alex Johnson', 'Sam Williams', 'Jordan Lee', 'Taylor Brown',
  'Morgan Davis', 'Casey Wilson', 'Riley Martinez', 'Quinn Anderson', 'Jamie Thomas',
  'Avery Jackson', 'Cameron White', 'Drew Harris', 'Emery Clark', 'Finley Lewis',
  'Harper Robinson', 'Kai Walker', 'Logan Hall', 'Parker Young', 'Reese King',
  'Skyler Wright', 'Blake Scott', 'Charlie Green', 'Devon Adams', 'Ellis Baker',
  'Frankie Nelson', 'Gray Hill', 'Hayden Moore', 'Indigo Carter', 'Jesse Phillips',
]

const PR_TITLES = [
  'Performance improvement', 'UI enhancements', 'Bug fix for edge case',
  'Code refactoring', 'Security update', 'Dependency upgrade',
  'Feature implementation', 'Documentation update', 'Test coverage improvement',
  'API optimization', 'Database migration', 'Logging improvements',
  'Error handling update', 'Configuration cleanup', 'Build pipeline fix',
  'Accessibility improvements', 'Responsive design update', 'Cache optimization',
  'Memory leak fix', 'Authentication update', 'Data validation improvement',
  'Search functionality update', 'Export feature enhancement', 'Notification system update',
]

const REPO_NAMES = [
  'repo-alpha', 'repo-beta', 'repo-gamma', 'repo-delta', 'repo-epsilon',
  'repo-zeta', 'repo-eta', 'repo-theta', 'repo-iota', 'repo-kappa',
  'repo-lambda', 'repo-mu', 'repo-nu', 'repo-xi', 'repo-omicron',
]

const ORG_NAMES = [
  'Organization A', 'Organization B', 'Organization C', 'Organization D', 'Organization E',
]

// --- Deterministic mapping (per-session) ---
const maps = {
  person: new Map(),
  project: new Map(),
  prTitle: new Map(),
  repo: new Map(),
  org: new Map(),
  db: new Map(),
  iteration: new Map(),
  team: new Map(),
  area: new Map(),
}

function getOrAssign(map, key, pool, prefix) {
  if (!key) return key
  const normalized = key.trim()
  if (!normalized) return key
  if (map.has(normalized)) return map.get(normalized)
  let value
  if (pool) {
    value = pool[map.size % pool.length]
  } else {
    // letter-based: Project A, Project B, ..., Project Z, Project AA, ...
    const idx = map.size
    if (idx < 26) {
      value = `${prefix} ${String.fromCharCode(65 + idx)}`
    } else {
      value = `${prefix} ${String.fromCharCode(65 + Math.floor(idx / 26) - 1)}${String.fromCharCode(65 + idx % 26)}`
    }
  }
  map.set(normalized, value)
  return value
}

export function anonName(real) {
  return getOrAssign(maps.person, real, PERSON_NAMES)
}

export function anonProject(real) {
  return getOrAssign(maps.project, real, null, 'Project')
}

export function anonPrTitle(real) {
  return getOrAssign(maps.prTitle, real, PR_TITLES)
}

export function anonRepo(real) {
  return getOrAssign(maps.repo, real, REPO_NAMES)
}

export function anonOrg(real) {
  return getOrAssign(maps.org, real, ORG_NAMES)
}

export function anonEmail(real) {
  if (!real) return real
  // Derive from the anonymized name for the part before @
  const name = anonName(real.split('@')[0]) || 'user'
  return name.toLowerCase().replace(/\s+/g, '.') + '@example.com'
}

export function anonDbName(real) {
  return getOrAssign(maps.db, real, null, 'Database')
}

export function anonTeam(real) {
  return getOrAssign(maps.team, real, null, 'Team')
}

export function anonSprint(real) {
  if (!real) return real
  const normalized = real.trim()
  if (!normalized) return real
  if (maps.iteration.has(normalized)) return maps.iteration.get(normalized)
  const value = `Sprint ${maps.iteration.size + 1}`
  maps.iteration.set(normalized, value)
  return value
}

function anonArea(real) {
  return getOrAssign(maps.area, real, null, 'Area')
}

export function anonAreaPath(real) {
  if (!real) return real
  const parts = real.split('\\')
  parts[0] = anonProject(parts[0])
  for (let i = 1; i < parts.length; i++) {
    parts[i] = anonArea(parts[i])
  }
  return parts.join('\\')
}

export function anonIterationPath(real) {
  if (!real) return real
  const parts = real.split('\\')
  parts[0] = anonProject(parts[0])
  for (let i = 1; i < parts.length; i++) {
    parts[i] = anonSprint(parts[i])
  }
  return parts.join('\\')
}

export function resetMaps() {
  Object.values(maps).forEach(m => m.clear())
}

export function useDemoMode() {
  return {
    isDemoMode,
    toggle() { isDemoMode.value = !isDemoMode.value },
  }
}
