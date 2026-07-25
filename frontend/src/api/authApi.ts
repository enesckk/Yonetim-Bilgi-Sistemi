import { apiRequest, setAccessToken } from './client'
import type { CurrentUser, LoginResponse } from './types'

export async function login(userName: string, password: string): Promise<LoginResponse> {
  const data = await apiRequest<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: { userName, password },
    skipAuth: true,
  })
  setAccessToken(data.accessToken)
  return data
}

export async function logout(): Promise<void> {
  try {
    await apiRequest('/api/auth/logout', { method: 'POST', skipAuth: true })
  } finally {
    setAccessToken(null)
  }
}

export async function fetchMe(): Promise<CurrentUser> {
  return apiRequest<CurrentUser>('/api/auth/me')
}

export async function refreshSession(): Promise<LoginResponse | null> {
  try {
    const data = await apiRequest<LoginResponse>('/api/auth/refresh', {
      method: 'POST',
      skipAuth: true,
    })
    setAccessToken(data.accessToken)
    return data
  } catch {
    setAccessToken(null)
    return null
  }
}
