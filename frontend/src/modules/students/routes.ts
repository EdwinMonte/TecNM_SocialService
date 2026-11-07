import type { RouteRecordRaw } from 'vue-router'

export const studentsRoutes: RouteRecordRaw[] = [
  {
    path: 'students',
    name: 'students-list',
    component: () => import('./views/StudentsListView.vue'),
    meta: { requiresAuth: true, roles: ['admin', 'social_service_officer', 'internship_coordinator', 'department_head'] },
  },
  {
    path: 'students/me',
    name: 'students-profile',
    component: () => import('./views/StudentProfileView.vue'),
    meta: { requiresAuth: true, roles: ['student'] },
  },
]

