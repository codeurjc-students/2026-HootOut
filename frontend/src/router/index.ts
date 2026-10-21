import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth.ts'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/HomeView.vue'),
      meta: { layout: 'HomeLayout', public: true, guestOnly: false }
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { layout: 'HomeLayout', public: true, guestOnly: true }
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { layout: 'HomeLayout', public: true, guestOnly: true }
    },
    {
      path: '/chat',
      name: 'chat',
      component: () => import('@/views/ChatView.vue'),
      meta: { layout: 'MainLayout' }
    },
    {
      path: '/chat/server/:id',
      name: 'server',
      component: () => import('@/views/ChatView.vue'),
      meta: { layout: 'MainLayout' }
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  if (!auth.ready) await auth.init()

  if (to.meta.guestOnly && auth.isAuthenticated) return { name: 'chat' }

  if (!to.meta.public && !auth.isAuthenticated && to.name !== 'home') {
    return { name: 'home', query: { redirect: to.fullPath } }
  }
})

export default router
