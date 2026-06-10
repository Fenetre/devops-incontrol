/**
 * Analyzes existing sprint iterations to suggest the next sprint's name and dates.
 * Takes an array of iterations: [{ name, path, start_date, finish_date, timeframe }]
 * Returns { suggestedName, suggestedStartDate, suggestedFinishDate, confidence }
 */
export function inferNextSprint(iterations) {
  const result = { suggestedName: '', suggestedParentPath: '', suggestedStartDate: '', suggestedFinishDate: '', sprintCount: 0 }

  if (!iterations || iterations.length < 2) return result

  // Sort by start_date if available, otherwise keep original order
  const sorted = [...iterations].sort((a, b) => {
    if (a.start_date && b.start_date) return new Date(a.start_date) - new Date(b.start_date)
    return 0
  })

  // Use last 10 for analysis
  const recent = sorted.slice(-10)
  result.sprintCount = sorted.length

  result.suggestedName = inferName(recent)
  result.suggestedParentPath = inferParentPath(recent)
  const dates = inferDates(sorted)
  result.suggestedStartDate = dates.start
  result.suggestedFinishDate = dates.finish

  return result
}

// ─── Name Inference ─────────────────────────────────────────────────────────

function inferName(sprints) {
  if (sprints.length < 2) return ''

  const names = sprints.map(s => s.name)

  // Tokenize: split each name into segments of (text | number | separator)
  const tokenized = names.map(n => tokenize(n))

  // Find the token position that auto-increments by 1
  const incrementPos = findIncrementingPosition(tokenized)

  if (incrementPos === -1) {
    // Fallback: regex-based detection of ALL incrementing numbers
    const fallback = findIncrementingNumber(names)
    if (!fallback) return ''

    // Use the last name that has enough numbers as the base
    const maxCol = Math.max(...fallback.map(f => f.col))
    const candidateNames = names.filter(n => {
      const matches = n.match(/\d+(?:\.\d+)?/g)
      return matches && matches.length > maxCol
    })
    if (candidateNames.length === 0) return ''

    const lastName = candidateNames[candidateNames.length - 1]
    const matches = [...lastName.matchAll(/\d+(?:\.\d+)?/g)]

    // Replace all incrementing numbers with their next values
    let result = ''
    let lastEnd = 0
    for (let i = 0; i < matches.length; i++) {
      const match = matches[i]
      const inc = fallback.find(f => f.col === i)
      result += lastName.slice(lastEnd, match.index)
      if (inc) {
        const next = Math.round((inc.lastValue + inc.step) * 1000) / 1000
        // Preserve decimal format
        if (match[0].includes('.')) {
          const decimals = match[0].split('.')[1].length
          result += next.toFixed(decimals)
        } else {
          result += String(Math.round(next))
        }
      } else {
        result += match[0]
      }
      lastEnd = match.index + match[0].length
    }
    result += lastName.slice(lastEnd)
    return result
  }

  // Filter to only sprints that have a valid number at the increment position
  const validIndices = []
  for (let i = 0; i < tokenized.length; i++) {
    if (incrementPos < tokenized[i].length && !isNaN(parseFloat(tokenized[i][incrementPos]))) {
      validIndices.push(i)
    }
  }
  if (validIndices.length < 2) return ''

  // Use the LAST valid sprint as the basis (not the last item overall)
  const lastValidIdx = validIndices[validIndices.length - 1]
  const lastTokens = tokenized[lastValidIdx]
  const lastNumber = parseFloat(lastTokens[incrementPos])

  // Only consider valid sprints for pattern analysis
  const validTokenized = validIndices.map(i => tokenized[i])

  // Find ALL positions that increment in lockstep with the main counter
  const coIncrementPositions = findCoIncrementPositions(validTokenized, incrementPos)

  // Detect if there's a varying suffix (potential sprint goal) — only among valid sprints
  const varyingSuffix = detectVaryingSuffix(validTokenized, incrementPos)

  const suggestedTokens = lastTokens.map((token, idx) => {
    if (idx === incrementPos) return String(Math.round(lastNumber + 1))
    if (coIncrementPositions.has(idx)) {
      const val = parseFloat(token)
      if (!isNaN(val)) {
        // Determine the increment step for this co-position
        const step = detectStep(validTokenized, idx)
        const next = Math.round((val + step) * 100) / 100
        // Preserve decimal format (e.g. 0.54 → 0.55)
        if (token.includes('.')) {
          const decimals = token.split('.')[1].length
          return next.toFixed(decimals)
        }
        return String(Math.round(next))
      }
    }
    return token
  })

  // If there's a varying suffix, replace it with placeholder directly in the tokens
  if (varyingSuffix.position !== -1) {
    // Find the separator token before the varying suffix
    const sepToken = suggestedTokens[varyingSuffix.position - 1] || ''
    const hasSeparator = /[-–—:]/.test(sepToken.trim())
    // Trim tokens to before the varying part and append goal placeholder
    const trimmed = suggestedTokens.slice(0, varyingSuffix.position)
    if (hasSeparator) {
      return trimmed.join('') + 'Sprint Goal'
    }
    // If the separator token contains " - " or similar, keep it
    if (/\s*[-–—:]\s*/.test(sepToken)) {
      return trimmed.join('') + 'Sprint Goal'
    }
    return trimmed.join('') + ' - Sprint Goal'
  }

  return suggestedTokens.join('')
}

function tokenize(name) {
  // Split into tokens: numbers (including decimals) and text chunks
  return name.match(/\d+(?:\.\d+)?|[^\d]+/g) || [name]
}

function findIncrementingPosition(tokenized) {
  if (tokenized.length < 2) return -1

  // Get max token count across all names
  const maxTokens = Math.max(...tokenized.map(t => t.length))

  let bestPos = -1
  let bestScore = 0

  // For each position, check if it's a number that increments
  for (let pos = 0; pos < maxTokens; pos++) {
    const values = []

    for (const tokens of tokenized) {
      if (pos >= tokens.length) continue
      const val = parseFloat(tokens[pos])
      if (isNaN(val)) continue
      values.push(val)
    }

    if (values.length < 2) continue

    // Check if values increment consistently (allow some gaps but look for +1 pattern)
    let incrementCount = 0
    for (let i = 1; i < values.length; i++) {
      if (values[i] - values[i - 1] === 1) incrementCount++
    }

    // Score: proportion of +1 increments, weighted by coverage
    const incrementRatio = incrementCount / (values.length - 1)
    const coverageRatio = values.length / tokenized.length
    const score = incrementRatio * coverageRatio

    // Need at least 50% increments and 50% coverage
    if (incrementRatio >= 0.5 && coverageRatio >= 0.5 && score > bestScore) {
      bestScore = score
      bestPos = pos
    }
  }

  return bestPos
}

// Fallback: find ALL incrementing numbers using regex on full names
function findIncrementingNumber(names) {
  // Extract all numbers (including decimals) from each name
  const allNumbers = names.map(name => {
    const matches = name.match(/\d+(?:\.\d+)?/g)
    return matches ? matches.map(Number) : []
  })

  // For each "column" of numbers, check if it increments
  const maxNums = Math.max(...allNumbers.map(n => n.length))
  const results = []

  for (let col = 0; col < maxNums; col++) {
    const values = allNumbers.map(nums => nums[col]).filter(v => v !== undefined)
    if (values.length < 2) continue

    // Detect the most common step
    const steps = []
    for (let i = 1; i < values.length; i++) {
      steps.push(Math.round((values[i] - values[i - 1]) * 1000) / 1000)
    }
    const stepCounts = {}
    for (const s of steps) {
      stepCounts[s] = (stepCounts[s] || 0) + 1
    }
    const dominantStep = Object.entries(stepCounts).sort((a, b) => b[1] - a[1])[0]
    if (dominantStep && parseFloat(dominantStep[0]) !== 0 && dominantStep[1] / steps.length >= 0.5) {
      results.push({ lastValue: values[values.length - 1], col, step: parseFloat(dominantStep[0]) })
    }
  }

  return results.length > 0 ? results : null
}

function findCoIncrementPositions(tokenized, primaryPos) {
  // Find other numeric positions that also increment consistently
  const positions = new Set()
  const maxTokens = Math.max(...tokenized.map(t => t.length))

  for (let pos = 0; pos < maxTokens; pos++) {
    if (pos === primaryPos) continue
    const values = []
    for (const tokens of tokenized) {
      if (pos >= tokens.length) continue
      const val = parseFloat(tokens[pos])
      if (isNaN(val)) continue // skip non-numeric entries instead of aborting
      values.push(val)
    }
    if (values.length < 2) continue

    // Detect the most common step
    const steps = []
    for (let i = 1; i < values.length; i++) {
      steps.push(Math.round((values[i] - values[i - 1]) * 1000) / 1000)
    }
    const stepCounts = {}
    for (const s of steps) {
      stepCounts[s] = (stepCounts[s] || 0) + 1
    }
    const dominantStep = Object.entries(stepCounts).sort((a, b) => b[1] - a[1])[0]
    if (dominantStep && parseFloat(dominantStep[0]) !== 0 && dominantStep[1] / steps.length >= 0.5) {
      positions.add(pos)
    }
  }
  return positions
}

function detectStep(tokenized, pos) {
  const values = []
  for (const tokens of tokenized) {
    if (pos >= tokens.length) continue
    const val = parseFloat(tokens[pos])
    if (!isNaN(val)) values.push(val)
  }
  if (values.length < 2) return 1
  // Find the most common step
  const steps = []
  for (let i = 1; i < values.length; i++) {
    steps.push(Math.round((values[i] - values[i - 1]) * 1000) / 1000)
  }
  const stepCounts = {}
  for (const s of steps) {
    stepCounts[s] = (stepCounts[s] || 0) + 1
  }
  const dominant = Object.entries(stepCounts).sort((a, b) => b[1] - a[1])[0]
  return dominant ? parseFloat(dominant[0]) : steps[steps.length - 1]
}

function detectVaryingSuffix(tokenized, incrementPos) {
  // Look for text tokens after the increment position that vary between sprints
  const lastTokenCount = tokenized[tokenized.length - 1].length

  for (let pos = incrementPos + 1; pos < lastTokenCount; pos++) {
    const values = tokenized.map(t => pos < t.length ? t[pos] : null).filter(v => v !== null)
    if (values.length < 2) continue

    // If it's text (not a number) and varies significantly
    const unique = new Set(values)
    const isText = isNaN(parseFloat(values[0]))
    if (isText && unique.size > values.length * 0.5) {
      return { position: pos, isText: true }
    }
  }

  return { position: -1 }
}

// ─── Parent Path Inference ───────────────────────────────────────────────────

function inferParentPath(sprints) {
  // Extract parent paths from iteration paths (path format: \Project\Parent\SprintName)
  const paths = sprints.map(s => s.path).filter(Boolean)
  if (paths.length === 0) return ''

  // Get parent of each path (everything except the last segment)
  const parents = paths.map(p => {
    const segments = p.split('\\').filter(Boolean)
    // Remove project name (first segment) and sprint name (last segment)
    if (segments.length <= 2) return ''
    return segments.slice(1, -1).join('\\')
  })

  // Find the most common parent
  const counts = {}
  for (const parent of parents) {
    if (parent) counts[parent] = (counts[parent] || 0) + 1
  }

  const entries = Object.entries(counts)
  if (entries.length === 0) return ''

  // Return the most common parent path
  entries.sort((a, b) => b[1] - a[1])
  return entries[0][0]
}

// ─── Date Inference ─────────────────────────────────────────────────────────

function inferDates(sprints) {
  const result = { start: '', finish: '' }

  // Filter sprints with valid date ranges
  const withDates = sprints.filter(s => s.start_date && s.finish_date)
  if (withDates.length < 2) return result

  // Calculate durations in days
  const durations = withDates.map(s => {
    const start = new Date(s.start_date)
    const finish = new Date(s.finish_date)
    return Math.round((finish - start) / (1000 * 60 * 60 * 24))
  }).filter(d => d > 0)

  if (durations.length === 0) return result

  // Calculate gaps between consecutive sprints
  const gaps = []
  for (let i = 1; i < withDates.length; i++) {
    const prevEnd = new Date(withDates[i - 1].finish_date)
    const currStart = new Date(withDates[i].start_date)
    const gap = Math.round((currStart - prevEnd) / (1000 * 60 * 60 * 24))
    if (gap >= 0 && gap <= 14) gaps.push(gap) // ignore unreasonable gaps
  }

  const medianDuration = median(durations)
  const medianGap = gaps.length > 0 ? median(gaps) : 1

  // Find the last sprint's finish date
  const lastFinish = new Date(withDates[withDates.length - 1].finish_date)

  // Suggest next start = last finish + median gap
  const suggestedStart = new Date(lastFinish)
  suggestedStart.setDate(suggestedStart.getDate() + medianGap)

  // Suggest next finish = start + median duration
  const suggestedFinish = new Date(suggestedStart)
  suggestedFinish.setDate(suggestedFinish.getDate() + medianDuration)

  result.start = formatDate(suggestedStart)
  result.finish = formatDate(suggestedFinish)

  return result
}

function median(values) {
  if (values.length === 0) return 0
  const sorted = [...values].sort((a, b) => a - b)
  const mid = Math.floor(sorted.length / 2)
  return sorted.length % 2 !== 0 ? sorted[mid] : Math.round((sorted[mid - 1] + sorted[mid]) / 2)
}

function formatDate(date) {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}
