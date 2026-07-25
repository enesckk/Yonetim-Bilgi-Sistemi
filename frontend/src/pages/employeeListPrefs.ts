import type { EmployeeStatus } from '@/api/employeesApi'

export type ColumnId =
  | 'name'
  | 'employeeNumber'
  | 'jobTitle'
  | 'duty'
  | 'unit'
  | 'facility'
  | 'employment'
  | 'phone'
  | 'hireDate'
  | 'serviceYears'
  | 'status'
  | 'completion'
  | 'updated'
  | 'actions'

export type AdvancedFiltersState = {
  jobTitleId: string
  jobDutyId: string
  dutyCategory: '' | number
  educationLevel: '' | number
  skillId: string
  includeSubUnits: boolean
  incompleteProfileOnly: boolean
  missingSkillsOnly: boolean
  missingPhoneOnly: boolean
  missingFacilityOnly: boolean
  hasSpecialConditionOnly: boolean
  hireYearFrom: string
  hireYearTo: string
  universityContains: string
  graduationYearFrom: string
  graduationYearTo: string
  hasNotesOnly: boolean
  missingCertificatesOnly: boolean
  expiredCertificateOnly: boolean
}

export type ListFiltersState = {
  search: string
  unitId: string
  facilityId: string
  employmentTypeId: string
  status: '' | EmployeeStatus
  advanced: AdvancedFiltersState
}

export type SavedView = {
  id: string
  name: string
  builtIn?: boolean
  columns: ColumnId[]
  /** Görünüm kaydedildiğinde bilinen sütun kataloğu — sonradan eklenenleri ayırt etmek için */
  knownIds?: ColumnId[]
  filters: ListFiltersState
  sortBy: string
  sortDesc: boolean
}

export type ColumnDef = {
  id: ColumnId
  label: string
  locked?: boolean
  requiresPhone?: boolean
  defaultVisible: boolean
  sortKey?: string
}

export const emptyAdvancedFilters = (): AdvancedFiltersState => ({
  jobTitleId: '',
  jobDutyId: '',
  dutyCategory: '',
  educationLevel: '',
  skillId: '',
  includeSubUnits: true,
  incompleteProfileOnly: false,
  missingSkillsOnly: false,
  missingPhoneOnly: false,
  missingFacilityOnly: false,
  hasSpecialConditionOnly: false,
  hireYearFrom: '',
  hireYearTo: '',
  universityContains: '',
  graduationYearFrom: '',
  graduationYearTo: '',
  hasNotesOnly: false,
  missingCertificatesOnly: false,
  expiredCertificateOnly: false,
})

export const emptyListFilters = (): ListFiltersState => ({
  search: '',
  unitId: '',
  facilityId: '',
  employmentTypeId: '',
  status: '',
  advanced: emptyAdvancedFilters(),
})

/** Katalog sırası = tablo sırası. `actions` her zaman sonda. */
export const COLUMN_DEFS: ColumnDef[] = [
  { id: 'name', label: 'Ad Soyad', locked: true, defaultVisible: true, sortKey: 'lastName' },
  { id: 'employeeNumber', label: 'Sicil', defaultVisible: true },
  { id: 'jobTitle', label: 'Resmi unvan', defaultVisible: true },
  { id: 'duty', label: 'Fiili görev', defaultVisible: true },
  { id: 'unit', label: 'Birim', defaultVisible: true },
  { id: 'facility', label: 'Tesis', defaultVisible: true },
  { id: 'employment', label: 'İstihdam', defaultVisible: true },
  { id: 'phone', label: 'Telefon', requiresPhone: true, defaultVisible: true },
  { id: 'hireDate', label: 'İşe giriş', defaultVisible: true, sortKey: 'hireDate' },
  { id: 'serviceYears', label: 'Hizmet süresi', defaultVisible: false },
  { id: 'status', label: 'Durum', defaultVisible: true, sortKey: 'status' },
  { id: 'completion', label: 'Tamamlanma', defaultVisible: true, sortKey: 'completion' },
  { id: 'updated', label: 'Güncelleme', defaultVisible: true, sortKey: 'updated' },
  { id: 'actions', label: 'İşlem', locked: true, defaultVisible: true },
]

const COLUMNS_KEY = 'py.employees.visibleColumns.v2'
const VIEWS_KEY = 'py.employees.savedViews.v2'
const LEGACY_COLUMNS_KEY = 'py.employees.visibleColumns.v1'
const LEGACY_VIEWS_KEY = 'py.employees.savedViews.v1'

type StoredColumns = {
  columns: ColumnId[]
  knownIds: ColumnId[]
}

function availableDefs(canViewPhone: boolean): ColumnDef[] {
  return COLUMN_DEFS.filter((c) => !c.requiresPhone || canViewPhone)
}

export function catalogIds(canViewPhone: boolean): ColumnId[] {
  return availableDefs(canViewPhone).map((c) => c.id)
}

export function defaultVisibleColumns(canViewPhone: boolean): ColumnId[] {
  return availableDefs(canViewPhone)
    .filter((c) => c.defaultVisible)
    .map((c) => c.id)
}

/**
 * Görünür sütunları katalog sırasına dizer.
 * `name` başta, `actions` her zaman sonda; yeni açılan sütun da İşlem’den önce gelir.
 */
export function normalizeColumns(ids: ColumnId[], canViewPhone: boolean): ColumnId[] {
  const allowed = availableDefs(canViewPhone)
  const selected = new Set(ids.filter((id) => allowed.some((c) => c.id === id)))
  selected.add('name')
  selected.add('actions')
  return allowed.filter((c) => selected.has(c.id)).map((c) => c.id)
}

/** knownIds’te olmayan katalog sütunları (uygulamaya sonradan eklenenler). */
export function findNewColumnIds(
  knownIds: ColumnId[] | undefined | null,
  canViewPhone: boolean,
): ColumnId[] {
  const catalog = catalogIds(canViewPhone)
  if (!knownIds || knownIds.length === 0) return []
  const known = new Set(knownIds)
  return catalog.filter((id) => !known.has(id) && id !== 'name' && id !== 'actions')
}

/**
 * Kayıtlı tercih/görünüme, katalogda yeni olan ve defaultVisible sütunları ekler (İşlem’den önce).
 * Opsiyonel yeni sütunlar (defaultVisible: false) otomatik açılmaz; “Yeni” rozeti ile seçicide kalır.
 */
export function mergeColumnsWithCatalog(
  storedColumns: ColumnId[],
  knownIds: ColumnId[] | undefined | null,
  canViewPhone: boolean,
): { columns: ColumnId[]; knownIds: ColumnId[]; newlyIntroduced: ColumnId[] } {
  const catalog = catalogIds(canViewPhone)
  const newlyIntroduced = findNewColumnIds(knownIds, canViewPhone)
  const next = new Set(normalizeColumns(storedColumns, canViewPhone))

  for (const id of newlyIntroduced) {
    const def = COLUMN_DEFS.find((c) => c.id === id)
    if (def?.defaultVisible) next.add(id)
  }

  return {
    columns: normalizeColumns([...next], canViewPhone),
    knownIds: catalog,
    newlyIntroduced,
  }
}

function readStoredColumns(): StoredColumns | null {
  try {
    const raw = localStorage.getItem(COLUMNS_KEY)
    if (raw) {
      const parsed = JSON.parse(raw) as StoredColumns | ColumnId[]
      if (Array.isArray(parsed)) {
        return { columns: parsed, knownIds: parsed }
      }
      if (parsed && Array.isArray(parsed.columns)) {
        return {
          columns: parsed.columns,
          knownIds: Array.isArray(parsed.knownIds) ? parsed.knownIds : parsed.columns,
        }
      }
    }

    const legacy = localStorage.getItem(LEGACY_COLUMNS_KEY)
    if (legacy) {
      const parsed = JSON.parse(legacy) as ColumnId[]
      if (Array.isArray(parsed)) {
        // Eski kayıt: o sıradaki sütunlar “bilinen” sayılır; katalogdaki yeniler newlyIntroduced olur.
        return { columns: parsed, knownIds: parsed }
      }
    }
  } catch {
    // ignore
  }
  return null
}

export function loadVisibleColumns(canViewPhone: boolean): {
  columns: ColumnId[]
  knownIds: ColumnId[]
  newlyIntroduced: ColumnId[]
} {
  const stored = readStoredColumns()
  if (!stored) {
    const columns = defaultVisibleColumns(canViewPhone)
    const knownIds = catalogIds(canViewPhone)
    return { columns, knownIds, newlyIntroduced: [] }
  }

  const merged = mergeColumnsWithCatalog(stored.columns, stored.knownIds, canViewPhone)
  saveVisibleColumns(merged.columns, merged.knownIds)
  return merged
}

export function saveVisibleColumns(ids: ColumnId[], knownIds?: ColumnId[]): void {
  try {
    const payload: StoredColumns = {
      columns: ids,
      knownIds: knownIds ?? catalogIds(true),
    }
    localStorage.setItem(COLUMNS_KEY, JSON.stringify(payload))
  } catch {
    // ignore quota / private mode
  }
}

export function builtInViews(): SavedView[] {
  const columns = defaultVisibleColumns(true)
  const knownIds = catalogIds(true)
  return [
    {
      id: 'builtin-default',
      name: 'Varsayılan',
      builtIn: true,
      columns,
      knownIds,
      filters: emptyListFilters(),
      sortBy: 'lastName',
      sortDesc: false,
    },
    {
      id: 'builtin-missing-phone',
      name: 'Telefon eksik',
      builtIn: true,
      columns,
      knownIds,
      filters: {
        ...emptyListFilters(),
        advanced: { ...emptyAdvancedFilters(), missingPhoneOnly: true },
      },
      sortBy: 'lastName',
      sortDesc: false,
    },
    {
      id: 'builtin-missing-facility',
      name: 'Tesis eksik',
      builtIn: true,
      columns,
      knownIds,
      filters: {
        ...emptyListFilters(),
        advanced: { ...emptyAdvancedFilters(), missingFacilityOnly: true },
      },
      sortBy: 'lastName',
      sortDesc: false,
    },
    {
      id: 'builtin-incomplete',
      name: 'Eksik profil',
      builtIn: true,
      columns,
      knownIds,
      filters: {
        ...emptyListFilters(),
        advanced: { ...emptyAdvancedFilters(), incompleteProfileOnly: true },
      },
      sortBy: 'completion',
      sortDesc: false,
    },
    {
      id: 'builtin-missing-skills',
      name: 'Yetkinlik girilmemiş',
      builtIn: true,
      columns,
      knownIds,
      filters: {
        ...emptyListFilters(),
        advanced: { ...emptyAdvancedFilters(), missingSkillsOnly: true },
      },
      sortBy: 'lastName',
      sortDesc: false,
    },
  ]
}

export function loadUserSavedViews(): SavedView[] {
  try {
    const raw = localStorage.getItem(VIEWS_KEY) ?? localStorage.getItem(LEGACY_VIEWS_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw) as SavedView[]
    if (!Array.isArray(parsed)) return []
    return parsed.filter((v) => v && typeof v.id === 'string' && typeof v.name === 'string' && !v.builtIn)
  } catch {
    return []
  }
}

export function saveUserSavedViews(views: SavedView[]): void {
  try {
    const userViews = views.filter((v) => !v.builtIn)
    localStorage.setItem(VIEWS_KEY, JSON.stringify(userViews))
  } catch {
    // ignore
  }
}

/** Görünüm uygulanırken: kayıtlı sütunlar + katalogdaki yeni defaultVisible sütunlar. */
export function resolveViewColumns(view: SavedView, canViewPhone: boolean): ColumnId[] {
  if (view.builtIn) {
    return defaultVisibleColumns(canViewPhone)
  }
  const known = view.knownIds ?? view.columns
  return mergeColumnsWithCatalog(view.columns, known, canViewPhone).columns
}

export function describeListFilters(
  filters: ListFiltersState,
  labels?: {
    unitName?: string
    facilityName?: string
    employmentTypeName?: string
    statusLabel?: string
  },
): string[] {
  const chips: string[] = []
  if (filters.search) chips.push(`Arama: “${filters.search}”`)
  if (filters.unitId) chips.push(labels?.unitName ? `Birim: ${labels.unitName}` : 'Birim seçili')
  if (filters.facilityId)
    chips.push(labels?.facilityName ? `Tesis: ${labels.facilityName}` : 'Tesis seçili')
  if (filters.employmentTypeId)
    chips.push(
      labels?.employmentTypeName
        ? `İstihdam: ${labels.employmentTypeName}`
        : 'İstihdam seçili',
    )
  if (filters.status)
    chips.push(labels?.statusLabel ? `Durum: ${labels.statusLabel}` : 'Durum seçili')

  const a = filters.advanced
  if (a.jobTitleId) chips.push('Unvan filtresi')
  if (a.jobDutyId) chips.push('Görev filtresi')
  if (a.dutyCategory !== '') chips.push('Görev kategorisi')
  if (a.educationLevel !== '') chips.push('Eğitim seviyesi')
  if (a.skillId) chips.push('Yetkinlik filtresi')
  if (a.hireYearFrom || a.hireYearTo) {
    chips.push(`İşe giriş: ${a.hireYearFrom || '…'}–${a.hireYearTo || '…'}`)
  }
  if (a.incompleteProfileOnly) chips.push('Eksik profil')
  if (a.missingSkillsOnly) chips.push('Yetkinlik girilmemiş')
  if (a.missingPhoneOnly) chips.push('Telefon eksik')
  if (a.missingFacilityOnly) chips.push('Tesis eksik')
  if (a.hasSpecialConditionOnly) chips.push('Özel durumu olanlar')
  if (a.universityContains) chips.push(`Üniversite: “${a.universityContains}”`)
  if (a.graduationYearFrom || a.graduationYearTo) {
    chips.push(`Mezuniyet: ${a.graduationYearFrom || '…'}–${a.graduationYearTo || '…'}`)
  }
  if (a.hasNotesOnly) chips.push('Notu olanlar')
  if (a.missingCertificatesOnly) chips.push('Sertifika yok')
  if (a.expiredCertificateOnly) chips.push('Süresi dolmuş sertifika')
  if (filters.unitId && !a.includeSubUnits) chips.push('Alt birimler hariç')

  return chips
}

export function serviceYearsLabel(hireDate?: string | null): string {
  if (!hireDate) return '—'
  const start = new Date(`${hireDate}T00:00:00`)
  if (Number.isNaN(start.getTime())) return '—'
  const years = (Date.now() - start.getTime()) / (365.25 * 24 * 60 * 60 * 1000)
  if (years < 0) return '—'
  if (years < 1) {
    const months = Math.max(0, Math.floor(years * 12))
    return months <= 0 ? '< 1 ay' : `${months} ay`
  }
  const whole = Math.floor(years)
  const remMonths = Math.floor((years - whole) * 12)
  return remMonths > 0 ? `${whole} yıl ${remMonths} ay` : `${whole} yıl`
}
