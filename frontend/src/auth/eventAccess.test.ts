import { describe, expect, it } from 'vitest'
import { hasEffectivePermission } from './roles'
import { PermissionCodes } from './permissionCodes'
import type { CurrentUser } from '@/api/types'
const user = (roles: string[], permissions = [PermissionCodes.EventsManage, PermissionCodes.EventsView]): CurrentUser => ({ id: '1', userName: 'test', displayName: 'Test', email: '', roles, permissions })
describe('director event access', () => {
  it('blocks legacy director writes while preserving event and note viewing', () => {
    expect(hasEffectivePermission(user(['DIRECTOR']), PermissionCodes.EventsManage)).toBe(false)
    expect(hasEffectivePermission(user(['DIRECTOR']), PermissionCodes.EventsView)).toBe(true)
  })
  it('does not gain event writes through another non-admin role', () => {
    expect(hasEffectivePermission(user(['DIRECTOR', 'UNIT_MANAGER']), PermissionCodes.EventsManage)).toBe(false)
  })
  it('preserves admin and administrative officer permissions without granting missing ones', () => {
    expect(hasEffectivePermission(user(['SYSTEM_ADMIN']), PermissionCodes.EventsManage)).toBe(true)
    expect(hasEffectivePermission(user(['ADMINISTRATIVE_OFFICER']), PermissionCodes.EventsManage)).toBe(true)
    expect(hasEffectivePermission(user(['SYSTEM_ADMIN'], []), PermissionCodes.EventsManage)).toBe(false)
    expect(hasEffectivePermission(null, PermissionCodes.EventsManage)).toBe(false)
  })
})
