import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { UserInfo } from '@/types'

export const userInfoStore = defineStore('userInfo', () => {
  const username = ref("")

  function setUsername(name: string) {
    username.value = name
  }

  function getUserInfo(): UserInfo {
    return {
      username: username.value
    }
  }

  return { username, setUsername, getUserInfo}
})
