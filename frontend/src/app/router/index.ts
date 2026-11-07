import { createRouter, createWebHistory } from 'vue-router'
import { loadSession, clearSession } from '../../shared/composables/useSession'
import { companiesRoutes } from '../../modules/companies/routes'

import { authRoutes, authenticatedAuthRoutes } from '../../modules/auth'
import { catalogsRoutes } from '../../modules/catalogs'
import { expedientsRoutes } from '../../modules/expedients'
import { internshipsRoutes } from '../../modules/internships'
import { qualityRoutes } from '../../modules/quality'
import { reportsRoutes } from '../../modules/reports'
import { socialServiceRoutes } from '../../modules/social-service'
import { studentsRoutes } from '../../modules/students'
import {
  clearSessionMarker,
  hasActiveSession,
} from '../../shared/utils/sessionCookie'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    ...authRoutes,
    {
      path: '/',
      component: () => import('../layouts/MainLayout.vue'),
      meta: { requiresAuth: true },
      children: [
        ...authenticatedAuthRoutes,
        ...catalogsRoutes,
        ...companiesRoutes,
        { path: 'access-denied', name: 'access-denied', component: () => import('../../modules/auth/views/AccessDeniedView.vue') },
        ...studentsRoutes,
        ...expedientsRoutes,
        ...socialServiceRoutes,
        ...internshipsRoutes,
        ...qualityRoutes,
        ...reportsRoutes,
      ],
    },
  ],

  scrollBehavior(to, _from, savedPosition) {
    return savedPosition ?? (to.hash ? { el: to.hash, top: 24 } : { top: 0 })
  },
})

router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth) return true

  if (!hasActiveSession()) {
    clearSessionMarker()
    clearSession()
    return {
      name: 'login',
      query: { redirect: to.fullPath },
    }
  }

  try {
    const user = await loadSession()
    if (to.meta.roles && !to.meta.roles.some(role => user.roles.some(assigned => assigned === role))) return { name: 'access-denied' }
  } catch (error: any) {
    if (error.response?.status === 401) return { name: 'login', query: { redirect: to.fullPath } }
    if (to.name !== 'access-denied') return { name: 'access-denied' }
  }
  return true
})

export default router
