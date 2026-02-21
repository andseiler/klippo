import apiClient from './client'

interface HealthInfo {
  llmAvailable: boolean
}

export async function getHealthInfo(): Promise<HealthInfo> {
  const response = await apiClient.get<HealthInfo>('/health')
  return response.data
}
