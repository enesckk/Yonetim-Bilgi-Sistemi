import { describe, expect, it } from 'vitest'
import type { CurrentUser } from '@/api/types'
import { canUseEventsModule, canUsePersonnelModule, defaultHomePath } from './roles'
const officer: CurrentUser = { id: '1', userName: 'memur', displayName: 'Memur', email: '', roles: ['OFFICER'], permissions: ['Employees.View', 'Employees.ViewAllUnits', 'Stock.View', 'Stock.Manage', 'Stock.CustodyManage', 'Organization.View', 'Notifications.View', 'Messages.Use'] }
describe('officer navigation', () => {
  it('opens personnel with the personnel menu and no event module', () => {
    expect(defaultHomePath(officer)).toBe('/personnel')
    expect(canUsePersonnelModule(officer)).toBe(true)
    expect(canUseEventsModule(officer)).toBe(false)
  })
  it('never opens events for an officer even with stale event permissions', () => {
    const user = { ...officer, permissions: [...officer.permissions, 'Events.View'] }
    expect(canUseEventsModule(user)).toBe(false)
    expect(defaultHomePath(user)).toBe('/personnel')
  })
  it('recognizes personnel access by permissions for custom role names', () => {
    const user = { ...officer, roles: ['CUSTOM_OFFICER'] }
    expect(canUsePersonnelModule(user)).toBe(true)
    expect(defaultHomePath(user)).toBe('/personnel')
  })
  it('does not send accounts without event access to the map', () => {
    expect(defaultHomePath({ ...officer, roles: [], permissions: ['Messages.Use', 'Employees.ViewAllUnits'] })).toBe('/messages')
  })
  it('retains event navigation for event operators', () => {
    const user = { ...officer, roles: ['VIEWER'], permissions: ['Events.View', 'Employees.ViewAllUnits'] }
    expect(canUseEventsModule(user)).toBe(true)
    expect(defaultHomePath(user)).toBe('/events/map')
  })
})
