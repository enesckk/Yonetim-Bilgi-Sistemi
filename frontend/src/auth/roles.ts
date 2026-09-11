import type { CurrentUser } from '@/api/types'

export const SYSTEM_ADMIN_ROLE = 'SYSTEM_ADMIN'
export const DIRECTOR_ROLE = 'DIRECTOR'
export const DEPUTY_DIRECTOR_ROLE = 'DEPUTY_DIRECTOR'
export const UNIT_MANAGER_ROLE = 'UNIT_MANAGER'
export const ADMINISTRATIVE_OFFICER_ROLE = 'ADMINISTRATIVE_OFFICER'

const PERSONNEL_MODULE_ROLES = new Set([
  SYSTEM_ADMIN_ROLE,
  DIRECTOR_ROLE,
  DEPUTY_DIRECTOR_ROLE,
  UNIT_MANAGER_ROLE,
  ADMINISTRATIVE_OFFICER_ROLE,
])

export function isSystemAdmin(user: CurrentUser | null | undefined): boolean {
  return user?.roles?.includes(SYSTEM_ADMIN_ROLE) ?? false
}

export function isDirector(user: CurrentUser | null | undefined): boolean {
  return user?.roles?.includes(DIRECTOR_ROLE) ?? false
}

/** Excel ile etkinlik yükleme: admin ve amirler. Müdür hesabında görünmez. */
export function canImportEvents(user: CurrentUser | null | undefined): boolean {
  if (isSystemAdmin(user)) return true
  if (isDirector(user)) return false
  const roles = user?.roles ?? []
  return (
    roles.includes(ADMINISTRATIVE_OFFICER_ROLE) ||
    roles.includes(UNIT_MANAGER_ROLE) ||
    roles.includes(DEPUTY_DIRECTOR_ROLE)
  )
}

/** Personel Excel yükleme: yalnız sistem yöneticisi. Müdür hesabında görünmez. */
export function canImportEmployees(user: CurrentUser | null | undefined): boolean {
  return isSystemAdmin(user)
}

/** Müdür, idari amir ve admin Müdürlük sekmesini görür. */
export function canUsePersonnelModule(user: CurrentUser | null | undefined): boolean {
  return user?.roles?.some((role) => PERSONNEL_MODULE_ROLES.has(role)) ?? false
}

/** Müdür ve admin tüm tesis/mahalle genelini görür; idari amir görmez. */
export function canSeeAllUnits(user: CurrentUser | null | undefined): boolean {
  if (isSystemAdmin(user)) return true
  return user?.permissions?.includes('Employees.ViewAllUnits') ?? false
}

export function defaultHomePath(user: CurrentUser | null | undefined): string {
  if (isSystemAdmin(user)) return '/'
  if (canUsePersonnelModule(user)) return '/personnel'
  return canSeeAllUnits(user) ? '/events/map' : '/events/calendar'
}
