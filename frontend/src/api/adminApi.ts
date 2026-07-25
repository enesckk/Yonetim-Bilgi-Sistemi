import { apiRequest } from './client'

export interface UserListItem {
  id: string
  userName: string
  displayName: string
  email: string
  isActive: boolean
  lastLoginAtUtc?: string | null
  employeeId?: string | null
  employeeName?: string | null
  roleCodes: string[]
  roleNames: string[]
}

export interface RoleListItem {
  id: string
  name: string
  code?: string | null
  description?: string | null
  isSystemRole: boolean
  permissionCount: number
  userCount: number
}

export interface CreateUserPayload {
  userName: string
  displayName: string
  email: string
  password: string
  employeeId?: string | null
  roleCodes: string[]
}

export interface UpdateUserPayload {
  displayName: string
  email: string
  isActive: boolean
  employeeId?: string | null
  roleCodes: string[]
}

export async function fetchUsers(): Promise<UserListItem[]> {
  return apiRequest<UserListItem[]>('/api/users')
}

export async function fetchRoles(): Promise<RoleListItem[]> {
  return apiRequest<RoleListItem[]>('/api/roles')
}

export async function createUser(payload: CreateUserPayload): Promise<{ id: string }> {
  return apiRequest<{ id: string }>('/api/users', { method: 'POST', body: payload })
}

export async function updateUser(id: string, payload: UpdateUserPayload): Promise<void> {
  await apiRequest<unknown>(`/api/users/${id}`, { method: 'PUT', body: payload })
}

export async function resetUserPassword(id: string, newPassword: string): Promise<void> {
  await apiRequest<unknown>(`/api/users/${id}/reset-password`, {
    method: 'POST',
    body: { newPassword },
  })
}

export interface PermissionItem {
  code: string
  name: string
  description: string
}

export interface PermissionGroup {
  group: string
  items: PermissionItem[]
}

export interface RoleMatrixRow {
  id: string
  name: string
  code?: string | null
  description?: string | null
  isSystemRole: boolean
  permissionCodes: string[]
  seedPermissionCodes: string[]
  differsFromSeed: boolean
}

export interface RolePermissionMatrix {
  permissionGroups: PermissionGroup[]
  roles: RoleMatrixRow[]
}

export async function fetchRolePermissionMatrix(): Promise<RolePermissionMatrix> {
  return apiRequest<RolePermissionMatrix>('/api/roles/matrix')
}

export async function updateRolePermissions(
  roleId: string,
  permissionCodes: string[],
): Promise<void> {
  await apiRequest<unknown>(`/api/roles/${roleId}/permissions`, {
    method: 'PUT',
    body: { permissionCodes },
  })
}

export async function resetRolePermissionsToSeed(roleId: string): Promise<void> {
  await apiRequest<unknown>(`/api/roles/${roleId}/permissions/reset-to-seed`, {
    method: 'POST',
  })
}
