import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { login as apiLogin } from '../api/auth'
import apiClient from '../api/client'
import type { LoginRequest } from '../api/types'

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(localStorage.getItem('accessToken'))
  const refreshTokenValue = ref<string | null>(localStorage.getItem('refreshToken'))
  const userId = ref<string | null>(localStorage.getItem('userId'))
  const email = ref<string | null>(localStorage.getItem('email'))
  const name = ref<string | null>(localStorage.getItem('name'))

  const isAuthenticated = computed(() => !!accessToken.value)

  function setAuth(data: {
    accessToken: string
    refreshToken: string
    userId: string
    email: string
    name: string
  }) {
    accessToken.value = data.accessToken
    refreshTokenValue.value = data.refreshToken
    userId.value = data.userId
    email.value = data.email
    name.value = data.name

    localStorage.setItem('accessToken', data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
    localStorage.setItem('userId', data.userId)
    localStorage.setItem('email', data.email)
    localStorage.setItem('name', data.name)
  }

  async function login(request: LoginRequest) {
    const response = await apiLogin(request)
    setAuth(response)
  }

  async function logout() {
    try {
      await apiClient.post('/auth/logout')
    } catch {
      // best-effort
    }

    accessToken.value = null
    refreshTokenValue.value = null
    userId.value = null
    email.value = null
    name.value = null

    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('userId')
    localStorage.removeItem('email')
    localStorage.removeItem('name')
  }

  return {
    accessToken,
    refreshToken: refreshTokenValue,
    userId,
    email,
    name,
    isAuthenticated,
    login,
    logout,
  }
})
