import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth.ts'
import NotFound from '@/views/NotFound.vue'
import ChatServerService from '@/services/chatServer/ChatServerService'
import { serverInfoStore } from '@/stores/serverInfo'
import ServerSettingsProfile from '@/components/server/settings/ServerSettingsProfile.vue'
import ServerSettingsMember from '@/components/server/settings/ServerSettingsMember.vue'
import ActiveMembers from '@/components/server/settings/Members/ActiveMembers.vue'
import ServerInvitations from '@/components/server/settings/Members/ServerInvitations.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/:pathMatch(.*)*',
      name: 'NotFound',
      component: NotFound,
      meta: { layout: 'HomeLayout', public: true, guestOnly: false },
    },
    {
      path: '/',
      name: 'home',
      component: () => import('@/views/HomeView.vue'),
      meta: { layout: 'HomeLayout', public: true, guestOnly: false },
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { layout: 'HomeLayout', public: true, guestOnly: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { layout: 'HomeLayout', public: true, guestOnly: true },
    },
    {
      path: '/chat',
      name: 'chat',
      component: () => import('@/views/ChatView.vue'),
      meta: { layout: 'MainLayout' },
    },
    {
      path: '/chat/server/:serverId?',
      name: 'server',
      component: () => import('@/views/ChatView.vue'),
      meta: { layout: 'MainLayout' },
    },
    {
      path: '/chat/server/:serverId/channel/:channelId',
      name: 'channel',
      component: () => import('@/views/ChatView.vue'),
      meta: { layout: 'MainLayout' },
    },
    {
      path: '/settings/server/:serverId',
      name: 'serverSettings',
      component: () => import('@/views/ServerSettingsView.vue'),
      meta: { layout: 'MainLayout' },
      beforeEnter: async (to, from) => {
        const serverId = to.params['serverId']
        try {
          const server = await ChatServerService.getServerById(serverId as string)
          if (!server) {
            return { name: 'chat' }
          }
          await serverInfoStore().setCurrentServerSettings(server)
        } catch (err) {
          return { name: 'chat' }
        }
      },
      children: [
        {
          path: 'profile',
          name: 'serverProfile',
          component: ServerSettingsProfile,
        },
        {
          path: 'members',
          name: 'serverMembers',
          component: ServerSettingsMember,
          children: [
            {
              path: 'active',
              name: 'serverActiveMembers',
              component: ActiveMembers,
            },
            {
              path: 'invitations',
              name: 'serverInvitations',
              component: ServerInvitations,
            },
          ],
        },
      ],
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  if (!auth.ready) await auth.init()

  if (to.meta.guestOnly && auth.isAuthenticated) return { name: 'chat' }

  if (!to.meta.public && !auth.isAuthenticated && to.name !== 'home') {
    return { name: 'home' }
  }
})

export default router
