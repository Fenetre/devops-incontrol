import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  { path: '/', name: 'dashboard', component: () => import('../views/DashboardView.vue'), meta: { title: 'Dashboard' } },
  { path: '/setup', name: 'setup', component: () => import('../views/SetupWizardView.vue'), meta: { title: 'Setup', layout: 'none' } },
  { path: '/projects/:projectId/issues/:checkType', name: 'issues', component: () => import('../views/IssueDetailView.vue'), props: true, meta: { title: 'Issues' } },
  { path: '/projects/:projectId/tag/:tagName', name: 'tag-detail', component: () => import('../views/TagDetailView.vue'), props: true, meta: { title: 'Tag Items' } },
  { path: '/config', name: 'config', component: () => import('../views/ConfigView.vue'), meta: { title: 'Configuration' } },
  { path: '/pr-monitor', name: 'pr-monitor', component: () => import('../views/PrMonitorView.vue'), meta: { title: 'PR Monitor' } },
  { path: '/pr-monitor/:projectId', name: 'pr-project', component: () => import('../views/PrProjectView.vue'), props: true, meta: { title: 'PR Monitor' } },
  { path: '/db-monitor', name: 'db-monitor', component: () => import('../views/DbMonitorView.vue'), meta: { title: 'DB Monitor' } },
  { path: '/db-monitor/:projectId', name: 'db-project', component: () => import('../views/DbProjectView.vue'), props: true, meta: { title: 'DB Project' } },
  { path: '/sprint-populator', name: 'sprint-populator', component: () => import('../views/SprintPopulatorView.vue'), meta: { title: 'Sprint Manager' } },
  { path: '/template-manager', name: 'template-manager', component: () => import('../views/TemplateManagerView.vue'), meta: { title: 'Template Manager' } },
  { path: '/velocity', name: 'velocity', component: () => import('../views/VelocityView.vue'), meta: { title: 'Velocity' } },
  { path: '/roadmap', name: 'roadmap', component: () => import('../views/RoadmapView.vue'), meta: { title: 'Roadmap' } },
  { path: '/dev-assessment', name: 'dev-assessment', component: () => import('../views/DevAssessmentView.vue'), meta: { title: 'DEV Assessment' } },
  { path: '/permissions/:projectId', name: 'permissions', component: () => import('../views/PermissionsView.vue'), props: true, meta: { title: 'Permissions' } },
  { path: '/permission-check/:projectId', redirect: to => ({ name: 'permissions', params: { projectId: to.params.projectId } }) },
  { path: '/pipelines/:projectId', name: 'pipelines', component: () => import('../views/PipelinesView.vue'), props: true, meta: { title: 'Pipelines & Releases' } },
  { path: '/releases/:projectId', redirect: to => ({ name: 'pipelines', params: { projectId: to.params.projectId } }) },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.afterEach((to) => {
  document.title = `${to.meta.title || 'Dashboard'} \u2014 DevOps InControl Dashboard`
})



export default router
