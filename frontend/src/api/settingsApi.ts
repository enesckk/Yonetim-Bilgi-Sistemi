import { apiRequest } from './client'

export type AppSettingValueType = 1 | 2 | 3

export interface AppSetting {
  key: string
  value: string
  valueType: AppSettingValueType
  groupName: string
  displayName: string
  description?: string | null
  isReadOnly: boolean
}

export async function fetchAppSettings(): Promise<AppSetting[]> {
  return apiRequest<AppSetting[]>('/api/settings')
}

export async function updateAppSetting(key: string, value: string): Promise<void> {
  await apiRequest<unknown>(`/api/settings/${encodeURIComponent(key)}`, {
    method: 'PUT',
    body: { value },
  })
}
