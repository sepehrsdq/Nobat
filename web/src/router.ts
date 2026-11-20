import { createRouter, createWebHistory } from 'vue-router'
import PanelLayout from './layouts/PanelLayout.vue'
import LoginPage from './pages/LoginPage.vue'
import ServicesPage from './pages/ServicesPage.vue'
import StaffPage from './pages/StaffPage.vue'
import WorkingHoursPage from './pages/WorkingHoursPage.vue'
import { session } from './lib/auth'

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/panel/staff' },
    { path: '/login', name: 'login', component: LoginPage, meta: { public: true } },
    {
      path: '/panel',
      component: PanelLayout,
      children: [
        { path: '', redirect: '/panel/staff' },
        { path: 'staff', name: 'staff', component: StaffPage },
        {
          path: 'staff/:staffId/working-hours',
          name: 'working-hours',
          component: WorkingHoursPage,
        },
        { path: 'services', name: 'services', component: ServicesPage },
      ],
    },
    { path: '/:pathMatch(.*)*', redirect: '/panel/staff' },
  ],
})

router.beforeEach(async (to) => {
  if (to.meta.public === true) {
    return true
  }

  if (!session.loaded && !(await session.load())) {
    return { name: 'login', query: { next: to.fullPath } }
  }

  return true
})
