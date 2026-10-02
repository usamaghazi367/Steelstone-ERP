import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/LoginView.vue'),
      meta: { guest: true },
    },
    {
      path: '/',
      redirect: '/modules/01',
    },
    {
      path: '/modules/:code',
      name: 'module',
      component: () => import('../views/ModuleView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/tools/backup',
      name: 'data-backup',
      component: () => import('../views/DataBackupView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/:pathMatch(.*)*',
      redirect: '/modules/01',
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.guest && auth.isAuthenticated) {
    return { name: 'module', params: { code: '01' }, query: { action: 'view' } }
  }

  if (to.meta.requiresAuth && auth.isAuthenticated && !auth.user) {
    await auth.fetchUser()
  }
})

export default router
