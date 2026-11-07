import type { RouteRecordRaw } from 'vue-router'

export const catalogsRoutes: RouteRecordRaw[] = [
  {
    path: 'catalogs/careers',
    name: 'catalogs-careers',
    component: () => import('./views/CareersView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'social_service_officer', 'internship_coordinator', 'department_head'] },
  },
]
