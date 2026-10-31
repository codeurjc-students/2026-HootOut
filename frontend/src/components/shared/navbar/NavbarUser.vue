<script lang="ts" setup>
  import { get } from '@/services/common/api/ApiService'
  import { useAuthStore } from '@/stores/auth'
  import { userInfoStore } from '@/stores/userInfo'
  import type { UserInfo } from '@/types'
  import { storeToRefs } from 'pinia'
  import { onMounted } from 'vue'
  import { useRouter } from 'vue-router'

  const userInfo = userInfoStore()
  const authStore = useAuthStore()
  const { user } = storeToRefs(userInfo)
  const router = useRouter()

  onMounted(async () => {
    const user = await get<UserInfo>('users/me')
    userInfo.setUser(user)
  })

  async function logOut() {
    await authStore.logout()
    await router.push({ name: 'home' })
  }
</script>

<template>
  <div class="dropdown relative inline-block text-left">
    <span class="rounded-md">
      <button class="pointer" type="button" aria-controls="navbar-user-dropdown">
        {{ user?.username }}
      </button>
    </span>
    <div class="dropdown-menu hidden">
      <div
        class="absolute right-0 mt-2 w-46 origin-top-right divide-y rounded-sm border-1 background-soft"
        role="menu"
        id="navbar-user-dropdown"
      >
        <div class="py-1 hover:background-light">
          <button
            class="pointer w-full px-4 py-2 text-left text-sm"
            role="menuitem"
            @click="logOut"
          >
            Log out
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style>
  .dropdown:focus-within .dropdown-menu {
    display: block;
  }
</style>
