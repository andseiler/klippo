import apiClient from './client'
import type { AuthResponse, LoginRequest } from './types'

export async function login(data: LoginRequest): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/login', data)
  return response.data
}

export async function guestAuth(): Promise<AuthResponse> {
  const response = await apiClient.post<AuthResponse>('/auth/guest')
  return response.data
}
