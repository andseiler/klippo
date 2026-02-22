import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/LoginView.vue'),
      meta: { layout: 'bare' },
    },
    {
      path: '/dashboard',
      name: 'dashboard',
      component: () => import('../views/DashboardView.vue'),
      meta: { requiresAuth: true },
    },
    {
      path: '/upload',
      name: 'upload',
      component: () => import('../views/UploadView.vue'),
      meta: { requiresAuth: true },
    },
{
      path: '/jobs/:id/review',
      name: 'job-review',
      component: () => import('../views/ReviewView.vue'),
      props: true,
      meta: { requiresAuth: true, layout: 'bare' },
    },
    {
      path: '/playground',
      name: 'playground',
      component: () => import('../views/PlaygroundView.vue'),
      meta: { layout: 'bare' },
    },
    {
      path: '/',
      redirect: '/playground',
    },
    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('../views/NotFoundView.vue'),
      meta: { layout: 'bare' },
    },
  ],
})

router.beforeEach((to) => {
  const token = localStorage.getItem('accessToken')
  const isGuest = localStorage.getItem('isGuest') === 'true'

  // Guest leaving playground: restore token or lock them in
  if (isGuest && to.name !== 'playground' && to.name !== 'login') {
    const previousToken = localStorage.getItem('previousToken')
    if (previousToken) {
      // Real user who entered playground — restore their token and let them through
      localStorage.setItem('accessToken', previousToken)
      localStorage.removeItem('previousToken')
      localStorage.removeItem('isGuest')
    } else {
      // Pure guest — cannot leave playground
      return { name: 'playground' }
    }
  }

  if (to.meta.requiresAuth && !token) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.name === 'login' && token && !isGuest) {
    return { name: 'dashboard' }
  }
})

export default router
