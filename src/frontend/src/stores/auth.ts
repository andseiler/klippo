import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { login as apiLogin, register as apiRegister } from '../api/auth'
import apiClient from '../api/client'
import type { LoginRequest, RegisterRequest, UserRole } from '../api/types'

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(localStorage.getItem('accessToken'))
  const refreshTokenValue = ref<string | null>(localStorage.getItem('refreshToken'))
  const userId = ref<string | null>(localStorage.getItem('userId'))
  const email = ref<string | null>(localStorage.getItem('email'))
  const name = ref<string | null>(localStorage.getItem('name'))
  const role = ref<UserRole | null>(localStorage.getItem('role') as UserRole | null)
  const organizationId = ref<string | null>(localStorage.getItem('organizationId'))

  const isAuthenticated = computed(() => !!accessToken.value)
  const isAdmin = computed(() => role.value === 'Admin')
  const isReviewer = computed(() => role.value === 'Reviewer')

  function setAuth(data: {
    accessToken: string
    refreshToken: string
    userId: string
    email: string
    name: string
    role: UserRole
    organizationId: string
  }) {
    accessToken.value = data.accessToken
    refreshTokenValue.value = data.refreshToken
    userId.value = data.userId
    email.value = data.email
    name.value = data.name
    role.value = data.role
    organizationId.value = data.organizationId

    localStorage.setItem('accessToken', data.accessToken)
    localStorage.setItem('refreshToken', data.refreshToken)
    localStorage.setItem('userId', data.userId)
    localStorage.setItem('email', data.email)
    localStorage.setItem('name', data.name)
    localStorage.setItem('role', data.role)
    localStorage.setItem('organizationId', data.organizationId)
  }

  async function login(request: LoginRequest) {
    const response = await apiLogin(request)
    setAuth(response)
  }

  async function register(request: RegisterRequest) {
    const response = await apiRegister(request)
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
    role.value = null
    organizationId.value = null

    localStorage.removeItem('accessToken')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('userId')
    localStorage.removeItem('email')
    localStorage.removeItem('name')
    localStorage.removeItem('role')
    localStorage.removeItem('organizationId')
  }

  return {
    accessToken,
    refreshToken: refreshTokenValue,
    userId,
    email,
    name,
    role,
    organizationId,
    isAuthenticated,
    isAdmin,
    isReviewer,
    login,
    register,
    logout,
  }
})
