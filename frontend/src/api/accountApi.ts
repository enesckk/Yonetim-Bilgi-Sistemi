import { apiRequest } from './client'

export interface MyAccount {
  id: string
  userName: string
  displayName: string
  email: string
  lastLoginAtUtc?: string | null
  roleNames: string[]
  employeeName?: string | null
}

export async function fetchMyAccount(): Promise<MyAccount> {
  return apiRequest<MyAccount>('/api/account')
}

export async function updateMyAccount(payload: {
  userName: string
  displayName: string
  email: string
}): Promise<void> {
  await apiRequest<unknown>('/api/account', { method: 'PUT', body: payload })
}

export async function changeMyPassword(payload: {
  currentPassword: string
  newPassword: string
}): Promise<void> {
  await apiRequest<unknown>('/api/account/password', { method: 'POST', body: payload })
}
