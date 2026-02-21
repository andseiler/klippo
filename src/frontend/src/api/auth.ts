import apiClient from './client'
import type { AuthResponse, LoginRequest, RegisterRequest } from './types'

export async function login(data: LoginRequest): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/login', data)
  return response.data
}

export async function register(data: RegisterRequest): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/register', data)
  return response.data
}

export async function refreshToken(token: string): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/refresh', {
    refreshToken: token,
  })
  return response.data
}

export async function guestAuth(): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/guest')
  return response.data
}
