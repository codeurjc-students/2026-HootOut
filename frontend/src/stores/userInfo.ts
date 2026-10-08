import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types'

export const userInfoStore = defineStore('userInfo', () => {
  const user = ref<UserInfo>({})

  function setUser(newUser: UserInfo) {
    user.value = newUser;
  }

  return { user, setUser }
})
