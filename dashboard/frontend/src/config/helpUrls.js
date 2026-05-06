/**
 * Help URL mapping — maps Vue route names to external documentation URLs.
 */
const DEFAULT_HELP_URL = 'https://www.devopsincontrol.com/docs'

const routeToHelpUrl = {
  'dashboard':          'https://www.devopsincontrol.com/docs#daily-monitoring',
  'setup':              'https://www.devopsincontrol.com/docs#getting-started',
  'config':             'https://www.devopsincontrol.com/docs#configuration',
  'issues':             'https://www.devopsincontrol.com/docs#viewing-flagged-issues',
  'tag-detail':         'https://www.devopsincontrol.com/docs#tag-monitor',
  'pr-monitor':         'https://www.devopsincontrol.com/docs#pr-monitor',
  'pr-project':         'https://www.devopsincontrol.com/docs#pr-monitor',
  'db-monitor':         'https://www.devopsincontrol.com/docs#database-cleanup',
  'db-project':         'https://www.devopsincontrol.com/docs#db-project',
  'sprint-populator':   'https://www.devopsincontrol.com/docs#sprint-populator',
  'velocity':           'https://www.devopsincontrol.com/docs#velocity-capacity',
  'dev-assessment':     'https://www.devopsincontrol.com/docs#development-assessment',
  'permission-check':   'https://www.devopsincontrol.com/docs#permission-overview',
  'check-permissions':  'https://www.devopsincontrol.com/docs#permission-audit',
  'pipelines':          'https://www.devopsincontrol.com/docs#pipelines',
  'releases':           'https://www.devopsincontrol.com/docs#releases',
}

/**
 * Returns the help URL for a given Vue route name.
 */
export function getHelpUrl(routeName) {
  return routeToHelpUrl[routeName] || DEFAULT_HELP_URL
}
