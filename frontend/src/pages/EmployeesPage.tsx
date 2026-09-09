import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type FormEvent,
} from 'react'
import { createPortal } from 'react-dom'
import { Link, useSearchParams } from 'react-router-dom'
import {
  fetchEmployeeFormOptions,
  fetchEmployees,
  type EmployeeFormOptions,
  type EmployeeListItem,
  type EmployeeStatus,
  type EnumOption,
  type LookupItem,
} from '@/api/employeesApi'
import { downloadEmployeesExcel, downloadEmployeesPdf } from '@/api/reportsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { canImportEmployees, isSystemAdmin } from '@/auth/roles'
import { EmployeeAvatar } from '@/components/EmployeeAvatar'
import {
  DeleteEmployeeDialog,
  TransferDirectorateDialog,
  WorkplaceChangeDialog,
} from '@/components/EmployeeLifecycleDialogs'
import {
  COLUMN_DEFS,
  builtInViews,
  catalogIds,
  defaultVisibleColumns,
  describeListFilters,
  emptyAdvancedFilters,
  findNewColumnIds,
  loadUserSavedViews,
  loadVisibleColumns,
  normalizeColumns,
  resolveViewColumns,
  saveUserSavedViews,
  saveVisibleColumns,
  serviceYearsLabel,
  type AdvancedFiltersState,
  type ColumnId,
  type ListFiltersState,
  type SavedView,
} from './employeeListPrefs'

const FALLBACK_STATUSES: EnumOption[] = [
  { value: 1, label: 'Aktif' },
  { value: 2, label: 'Pasif' },
  { value: 3, label: 'İzinli' },
  { value: 4, label: 'Uzun süreli izinli' },
  { value: 5, label: 'Geçici görevli' },
  { value: 6, label: 'İşten ayrıldı' },
  { value: 7, label: 'Emekli oldu' },
  { value: 8, label: 'Başka müdürlüğe geçti' },
  { value: 9, label: 'Askıda' },
  { value: 10, label: 'Göreve dönmesi bekleniyor' },
  { value: 11, label: 'Görevden ayrıldı' },
]

const FALLBACK_DUTY_CATEGORIES: EnumOption[] = [
  { value: 1, label: 'Yönetici' },
  { value: 2, label: 'İdari' },
  { value: 3, label: 'Eğitmen' },
  { value: 4, label: 'Teknik' },
  { value: 5, label: 'Danışma' },
  { value: 6, label: 'Kütüphane' },
  { value: 7, label: 'Yardımcı' },
  { value: 8, label: 'Temizlik' },
  { value: 9, label: 'Proje' },
  { value: 10, label: 'Sosyal medya' },
  { value: 11, label: 'Halkla ilişkiler' },
  { value: 99, label: 'Diğer' },
]

const FALLBACK_EDUCATION_LEVELS: EnumOption[] = [
  { value: 1, label: 'İlköğretim' },
  { value: 2, label: 'Lise' },
  { value: 3, label: 'Ön lisans' },
  { value: 4, label: 'Lisans' },
  { value: 5, label: 'Yüksek lisans' },
  { value: 6, label: 'Doktora' },
]

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const
const PAGE_SIZE_KEY = 'py.employees.pageSize.v1'

function loadPageSize(): number {
  try {
    const n = Number(localStorage.getItem(PAGE_SIZE_KEY))
    return (PAGE_SIZE_OPTIONS as readonly number[]).includes(n) ? n : 25
  } catch {
    return 25
  }
}

export function EmployeesPage() {
  const { hasPermission, user } = useAuth()
  const [urlParams] = useSearchParams()
  const urlSearch = urlParams.get('search')?.trim() ?? ''
  const canViewPhone = hasPermission(PermissionCodes.EmployeesViewPhone)
  const canCreate = hasPermission(PermissionCodes.EmployeesCreate)
  const canUpdate = hasPermission(PermissionCodes.EmployeesUpdate)
  const canCreateMovements = hasPermission(PermissionCodes.MovementsCreate)
  const canSetStatus = hasPermission(PermissionCodes.EmployeesSetStatus)
  const canArchive = hasPermission(PermissionCodes.EmployeesArchive)
  const admin = isSystemAdmin(user)
  const canImport = canImportEmployees(user)
  const canExportExcel = hasPermission(PermissionCodes.ReportsExportExcel)
  const canExportPdf = hasPermission(PermissionCodes.ReportsExportPdf)

  const [options, setOptions] = useState<EmployeeFormOptions | null>(null)
  const [search, setSearch] = useState(urlSearch)
  const [searchInput, setSearchInput] = useState(urlSearch)
  const [unitId, setUnitId] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [employmentTypeId, setEmploymentTypeId] = useState('')
  const [status, setStatus] = useState<'' | EmployeeStatus>('')
  const [advanced, setAdvanced] = useState<AdvancedFiltersState>(emptyAdvancedFilters)
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(loadPageSize)
  const [sortBy, setSortBy] = useState('lastName')
  const [sortDesc, setSortDesc] = useState(false)

  const [visibleColumns, setVisibleColumns] = useState<ColumnId[]>([])
  const [newColumnIds, setNewColumnIds] = useState<ColumnId[]>([])
  const [prefsReady, setPrefsReady] = useState(false)
  const [columnsOpen, setColumnsOpen] = useState(false)
  const [viewsOpen, setViewsOpen] = useState(false)
  const [userViews, setUserViews] = useState<SavedView[]>(() => loadUserSavedViews())
  const [activeViewId, setActiveViewId] = useState<string | null>('builtin-default')
  const [newViewName, setNewViewName] = useState('')

  const [items, setItems] = useState<EmployeeListItem[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [totalPages, setTotalPages] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [exportBusy, setExportBusy] = useState<'excel' | 'pdf' | null>(null)
  const [optionsError, setOptionsError] = useState<string | null>(null)
  const [lifecycle, setLifecycle] = useState<
    | { kind: 'workplace'; id: string }
    | { kind: 'transfer'; id: string; name: string }
    | { kind: 'delete'; id: string; name: string }
    | null
  >(null)

  const columnsPanelRef = useRef<HTMLDivElement>(null)
  const viewsPanelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    setSearch(urlSearch)
    setSearchInput(urlSearch)
    setPage(1)
  }, [urlSearch])

  const statusOptions = options?.statuses?.length ? options.statuses : FALLBACK_STATUSES
  const dutyCategoryOptions = options?.dutyCategories?.length
    ? options.dutyCategories
    : FALLBACK_DUTY_CATEGORIES
  const educationLevelOptions = options?.educationLevels?.length
    ? options.educationLevels
    : FALLBACK_EDUCATION_LEVELS

  const orgCascade = useMemo(
    () => resolveOrgCascade(options?.units ?? [], unitId),
    [options?.units, unitId],
  )

  const directorateOptions = useMemo(
    () => (options?.units ?? []).filter((u) => u.type === ORG_TYPE.Directorate),
    [options?.units],
  )

  const mainUnitOptions = useMemo(() => {
    const units = options?.units ?? []
    if (!orgCascade.directorateId) return units.filter((u) => u.type === ORG_TYPE.MainUnit)
    return units.filter(
      (u) => u.type === ORG_TYPE.MainUnit && u.parentId === orgCascade.directorateId,
    )
  }, [options?.units, orgCascade.directorateId])

  const subUnitOptions = useMemo(() => {
    const units = options?.units ?? []
    if (!orgCascade.mainUnitId) {
      if (!orgCascade.directorateId) return units.filter((u) => u.type === ORG_TYPE.SubUnit)
      const mainIds = new Set(mainUnitOptions.map((u) => u.id))
      return units.filter((u) => u.type === ORG_TYPE.SubUnit && u.parentId && mainIds.has(u.parentId))
    }
    return units.filter((u) => u.type === ORG_TYPE.SubUnit && u.parentId === orgCascade.mainUnitId)
  }, [options?.units, orgCascade.directorateId, orgCascade.mainUnitId, mainUnitOptions])

  const facilityOptions = useMemo(() => {
    const facilities = options?.facilities ?? []
    if (!unitId || !options?.units) return facilities
    const scope = collectDescendantIds(options.units, unitId)
    return facilities.filter((f) => f.parentId && scope.has(f.parentId))
  }, [options?.facilities, options?.units, unitId])

  const allViews = useMemo(() => [...builtInViews(), ...userViews], [userViews])

  const columnDefs = useMemo(
    () => COLUMN_DEFS.filter((c) => !c.requiresPhone || canViewPhone),
    [canViewPhone],
  )

  const activeColumns = useMemo(() => {
    const cols = prefsReady
      ? normalizeColumns(visibleColumns, canViewPhone)
      : defaultVisibleColumns(canViewPhone)
    if (admin) return cols
    return cols.filter((id) => id !== 'completion')
  }, [visibleColumns, canViewPhone, prefsReady, admin])

  useEffect(() => {
    const loaded = loadVisibleColumns(canViewPhone)
    setVisibleColumns(loaded.columns)
    setNewColumnIds(loaded.newlyIntroduced)
    setPrefsReady(true)
  }, [canViewPhone])

  const reloadOptions = useCallback(async () => {
    try {
      const opts = await fetchEmployeeFormOptions()
      setOptions({
        ...opts,
        duties: opts.duties ?? [],
        skills: opts.skills ?? [],
        dutyCategories: opts.dutyCategories?.length
          ? opts.dutyCategories
          : FALLBACK_DUTY_CATEGORIES,
        educationLevels: opts.educationLevels?.length
          ? opts.educationLevels
          : FALLBACK_EDUCATION_LEVELS,
      })
      setOptionsError(null)
    } catch {
      setOptionsError('Filtre listeleri yüklenemedi; API yeniden başlatılmış olabilir.')
    }
  }, [])

  useEffect(() => {
    void reloadOptions()
  }, [reloadOptions])

  // Organizasyonda tesis/istihdam eklenince dropdown’lar güncellensin
  useEffect(() => {
    function onFocus() {
      void reloadOptions()
    }
    function onVisible() {
      if (document.visibilityState === 'visible') void reloadOptions()
    }
    window.addEventListener('focus', onFocus)
    document.addEventListener('visibilitychange', onVisible)
    return () => {
      window.removeEventListener('focus', onFocus)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [reloadOptions])

  useEffect(() => {
    if (advancedOpen || viewsOpen) void reloadOptions()
  }, [advancedOpen, viewsOpen, reloadOptions])

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      const t = e.target as Node
      if (columnsOpen && columnsPanelRef.current && !columnsPanelRef.current.contains(t)) {
        setColumnsOpen(false)
      }
      if (viewsOpen && viewsPanelRef.current && !viewsPanelRef.current.contains(t)) {
        setViewsOpen(false)
      }
    }
    document.addEventListener('mousedown', onDocClick)
    return () => document.removeEventListener('mousedown', onDocClick)
  }, [columnsOpen, viewsOpen])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await fetchEmployees({
        search: search || undefined,
        unitId: unitId || undefined,
        includeSubUnits: Boolean(unitId) && advanced.includeSubUnits,
        facilityId: facilityId || undefined,
        employmentTypeId: employmentTypeId || undefined,
        jobTitleId: advanced.jobTitleId || undefined,
        jobDutyId: advanced.jobDutyId || undefined,
        dutyCategory: advanced.dutyCategory,
        educationLevel: advanced.educationLevel,
        skillId: advanced.skillId || undefined,
        status,
        incompleteProfileOnly: advanced.incompleteProfileOnly,
        missingSkillsOnly: advanced.missingSkillsOnly,
        missingPhoneOnly: advanced.missingPhoneOnly,
        missingFacilityOnly: advanced.missingFacilityOnly,
        hasSpecialConditionOnly: advanced.hasSpecialConditionOnly,
        hireYearFrom: advanced.hireYearFrom ? Number(advanced.hireYearFrom) : '',
        hireYearTo: advanced.hireYearTo ? Number(advanced.hireYearTo) : '',
        universityContains: advanced.universityContains.trim() || undefined,
        graduationYearFrom: advanced.graduationYearFrom
          ? Number(advanced.graduationYearFrom)
          : '',
        graduationYearTo: advanced.graduationYearTo ? Number(advanced.graduationYearTo) : '',
        hasNotesOnly: advanced.hasNotesOnly,
        missingCertificatesOnly: advanced.missingCertificatesOnly,
        expiredCertificateOnly: advanced.expiredCertificateOnly,
        page,
        pageSize,
        sortBy,
        sortDesc,
      })
      setItems(result.items)
      setTotalCount(result.totalCount)
      setTotalPages(result.totalPages)
    } catch (err) {
      setItems([])
      if (err instanceof ApiClientError) setError(err.message)
      else setError('Personel listesi alınamadı.')
    } finally {
      setLoading(false)
    }
  }, [
    search,
    unitId,
    facilityId,
    employmentTypeId,
    status,
    advanced,
    page,
    pageSize,
    sortBy,
    sortDesc,
  ])

  useEffect(() => {
    void load()
  }, [load])

  function toggleSort(column: string) {
    if (sortBy === column) {
      setSortDesc((v) => !v)
    } else {
      setSortBy(column)
      setSortDesc(false)
    }
    setPage(1)
    setActiveViewId(null)
  }

  function onSearchSubmit(e: FormEvent) {
    e.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
    setActiveViewId(null)
  }

  function patchAdvanced<K extends keyof AdvancedFiltersState>(
    key: K,
    value: AdvancedFiltersState[K],
  ) {
    setPage(1)
    setAdvanced((prev) => ({ ...prev, [key]: value }))
    setActiveViewId(null)
  }

  function clearFilters() {
    setSearchInput('')
    setSearch('')
    setUnitId('')
    setFacilityId('')
    setEmploymentTypeId('')
    setStatus('')
    setAdvanced(emptyAdvancedFilters())
    setPage(1)
    setActiveViewId(null)
  }

  function currentFilters(): ListFiltersState {
    return {
      search,
      unitId,
      facilityId,
      employmentTypeId,
      status,
      advanced,
    }
  }

  function applyView(view: SavedView) {
    // Hazır görünümler yalnızca filtre + sıralama uygular; sütun tercihi korunur.
    if (!view.builtIn) {
      const cols = resolveViewColumns(view, canViewPhone)
      const known = catalogIds(canViewPhone)
      const introduced = findNewColumnIds(view.knownIds ?? view.columns, canViewPhone)
      setVisibleColumns(cols)
      setNewColumnIds(
        introduced.filter((id) => !COLUMN_DEFS.find((c) => c.id === id)?.defaultVisible),
      )
      saveVisibleColumns(cols, known)

      const nextViews = userViews.map((v) =>
        v.id === view.id ? { ...v, columns: cols, knownIds: known } : v,
      )
      setUserViews(nextViews)
      saveUserSavedViews(nextViews)
    }

    const adv = { ...emptyAdvancedFilters(), ...(view.filters.advanced ?? {}) }
    // Eski kayıtlarda eksik boolean’lar false kalsın; true olanlar kesin gelsin
    adv.incompleteProfileOnly = Boolean(view.filters.advanced?.incompleteProfileOnly)
    adv.missingSkillsOnly = Boolean(view.filters.advanced?.missingSkillsOnly)
    adv.missingPhoneOnly = Boolean(view.filters.advanced?.missingPhoneOnly)
    adv.missingFacilityOnly = Boolean(view.filters.advanced?.missingFacilityOnly)
    adv.hasSpecialConditionOnly = Boolean(view.filters.advanced?.hasSpecialConditionOnly)
    adv.hasNotesOnly = Boolean(view.filters.advanced?.hasNotesOnly)
    adv.missingCertificatesOnly = Boolean(view.filters.advanced?.missingCertificatesOnly)
    adv.expiredCertificateOnly = Boolean(view.filters.advanced?.expiredCertificateOnly)
    adv.universityContains = view.filters.advanced?.universityContains ?? ''
    adv.graduationYearFrom = view.filters.advanced?.graduationYearFrom ?? ''
    adv.graduationYearTo = view.filters.advanced?.graduationYearTo ?? ''
    adv.includeSubUnits =
      view.filters.advanced?.includeSubUnits !== undefined
        ? Boolean(view.filters.advanced.includeSubUnits)
        : true

    setSearch(view.filters.search ?? '')
    setSearchInput(view.filters.search ?? '')
    setUnitId(view.filters.unitId ?? '')
    setFacilityId(view.filters.facilityId ?? '')
    setEmploymentTypeId(view.filters.employmentTypeId ?? '')
    setStatus(view.filters.status ?? '')
    setAdvanced(adv)
    setSortBy(view.sortBy)
    setSortDesc(view.sortDesc)
    setPage(1)
    setActiveViewId(view.id)
    setViewsOpen(false)
    setAdvancedOpen(hasAdvancedInFilters(adv, view.filters.unitId ?? ''))
    setError(null)
  }

  function saveCurrentAsView() {
    const name = newViewName.trim()
    if (!name) return
    const filters = currentFilters()
    if (!listFiltersActive(filters)) {
      setError('Görünüm kaydetmek için önce en az bir filtre seçin (ör. Gelişmiş → Tesis eksik).')
      setViewsOpen(true)
      setAdvancedOpen(true)
      return
    }
    const known = catalogIds(canViewPhone)
    const view: SavedView = {
      id: `user-${Date.now()}`,
      name,
      columns: activeColumns,
      knownIds: known,
      filters,
      sortBy,
      sortDesc,
    }
    const next = [...userViews, view]
    setUserViews(next)
    saveUserSavedViews(next)
    setNewColumnIds([])
    saveVisibleColumns(activeColumns, known)
    setActiveViewId(view.id)
    setNewViewName('')
    setError(null)
  }

  function deleteUserView(id: string) {
    const next = userViews.filter((v) => v.id !== id)
    setUserViews(next)
    saveUserSavedViews(next)
    if (activeViewId === id) setActiveViewId(null)
  }

  function toggleColumn(id: ColumnId) {
    const def = COLUMN_DEFS.find((c) => c.id === id)
    if (!def || def.locked) return
    setVisibleColumns((prev) => {
      const has = prev.includes(id)
      // Açınca katalog sırasına yerleşir → her zaman İşlem’den önce
      const next = normalizeColumns(
        has ? prev.filter((c) => c !== id) : [...prev, id],
        canViewPhone,
      )
      const known = catalogIds(canViewPhone)
      saveVisibleColumns(next, known)
      return next
    })
    setNewColumnIds((prev) => prev.filter((c) => c !== id))
    setActiveViewId(null)
  }

  function resetColumns() {
    const next = defaultVisibleColumns(canViewPhone)
    const known = catalogIds(canViewPhone)
    setVisibleColumns(next)
    setNewColumnIds([])
    saveVisibleColumns(next, known)
    setActiveViewId(null)
  }

  function buildExportFilters() {
    return {
      search: search || undefined,
      unitId: unitId || undefined,
      includeSubUnits: Boolean(unitId) && advanced.includeSubUnits,
      facilityId: facilityId || undefined,
      employmentTypeId: employmentTypeId || undefined,
      jobTitleId: advanced.jobTitleId || undefined,
      jobDutyId: advanced.jobDutyId || undefined,
      dutyCategory: advanced.dutyCategory,
      educationLevel: advanced.educationLevel,
      skillId: advanced.skillId || undefined,
      status,
      incompleteProfileOnly: advanced.incompleteProfileOnly,
      missingSkillsOnly: advanced.missingSkillsOnly,
      missingPhoneOnly: advanced.missingPhoneOnly,
      missingFacilityOnly: advanced.missingFacilityOnly,
      hasSpecialConditionOnly: advanced.hasSpecialConditionOnly,
      hireYearFrom: advanced.hireYearFrom ? Number(advanced.hireYearFrom) : ('' as const),
      hireYearTo: advanced.hireYearTo ? Number(advanced.hireYearTo) : ('' as const),
      universityContains: advanced.universityContains.trim() || undefined,
      graduationYearFrom: advanced.graduationYearFrom
        ? Number(advanced.graduationYearFrom)
        : ('' as const),
      graduationYearTo: advanced.graduationYearTo
        ? Number(advanced.graduationYearTo)
        : ('' as const),
      hasNotesOnly: advanced.hasNotesOnly,
      missingCertificatesOnly: advanced.missingCertificatesOnly,
      expiredCertificateOnly: advanced.expiredCertificateOnly,
    }
  }

  async function onExport(format: 'excel' | 'pdf') {
    if (format === 'excel' && !canExportExcel) return
    if (format === 'pdf' && !canExportPdf) return
    setExportBusy(format)
    setError(null)
    try {
      const filters = buildExportFilters()
      if (format === 'excel') await downloadEmployeesExcel(filters)
      else await downloadEmployeesPdf(filters)
    } catch (err) {
      setError(
        err instanceof ApiClientError
          ? err.message
          : format === 'excel'
            ? 'Excel indirilemedi.'
            : 'PDF indirilemedi.',
      )
    } finally {
      setExportBusy(null)
    }
  }

  const hasAdvancedActive =
    Boolean(
      advanced.jobTitleId ||
        advanced.jobDutyId ||
        advanced.dutyCategory ||
        advanced.educationLevel ||
        advanced.skillId ||
        advanced.hireYearFrom ||
        advanced.hireYearTo ||
        advanced.universityContains.trim() ||
        advanced.graduationYearFrom ||
        advanced.graduationYearTo,
    ) ||
    advanced.incompleteProfileOnly ||
    advanced.missingSkillsOnly ||
    advanced.missingPhoneOnly ||
    advanced.missingFacilityOnly ||
    advanced.hasSpecialConditionOnly ||
    advanced.hasNotesOnly ||
    advanced.missingCertificatesOnly ||
    advanced.expiredCertificateOnly ||
    (Boolean(unitId) && !advanced.includeSubUnits)

  const hasActiveFilters = Boolean(
    search || unitId || facilityId || employmentTypeId || status || hasAdvancedActive,
  )

  const activeViewName = allViews.find((v) => v.id === activeViewId)?.name

  const filterChips = useMemo(() => {
    const filters = currentFiltersSnapshot(
      search,
      unitId,
      facilityId,
      employmentTypeId,
      status,
      advanced,
    )
    return describeListFilters(filters, {
      unitName: options?.units.find((u) => u.id === unitId)?.name,
      facilityName: options?.facilities.find((f) => f.id === facilityId)?.name,
      employmentTypeName: options?.employmentTypes.find((t) => t.id === employmentTypeId)?.name,
      statusLabel: statusOptions.find((s) => s.value === status)?.label,
    })
  }, [
    search,
    unitId,
    facilityId,
    employmentTypeId,
    status,
    advanced,
    options,
    statusOptions,
  ])

  return (
    <div className="employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Personeller</h2>
              <div className="stat-chip mobile-inline-chip">{totalCount} kayıt</div>
            </div>
            <p className="muted small employees-toolbar-lead">
              {admin
                ? 'Filtreleyin, sütun seçin, görünüm kaydedin. Excel/PDF mevcut filtreleri dışa aktarır.'
                : 'Ad, sicil veya birim ile arayın. Durum ve birim filtreleriyle listeyi daraltın.'}
            </p>
          </div>

          <div className="employees-toolbar-actions">
            <div className="stat-chip desktop-inline-chip">{totalCount} kayıt</div>

            {(canExportExcel || canExportPdf) && (
              <div className="export-actions" role="group" aria-label="Dışa aktarım">
                {canExportExcel && (
                  <button
                    type="button"
                    className="btn-export btn-export-excel"
                    disabled={exportBusy !== null || loading}
                    onClick={() => void onExport('excel')}
                    title="Filtrelenmiş listeyi Excel olarak indir"
                  >
                    <span className="label-full">
                      {exportBusy === 'excel' ? 'Hazırlanıyor…' : 'Excel indir'}
                    </span>
                    <span className="label-short">
                      {exportBusy === 'excel' ? '…' : 'Excel'}
                    </span>
                  </button>
                )}
                {canExportPdf && (
                  <button
                    type="button"
                    className="btn-export btn-export-pdf"
                    disabled={exportBusy !== null || loading}
                    onClick={() => void onExport('pdf')}
                    title="Filtrelenmiş listeyi PDF olarak indir"
                  >
                    <span className="label-full">
                      {exportBusy === 'pdf' ? 'Hazırlanıyor…' : 'PDF indir'}
                    </span>
                    <span className="label-short">{exportBusy === 'pdf' ? '…' : 'PDF'}</span>
                  </button>
                )}
              </div>
            )}

            {(canImport || canCreate) && (canExportExcel || canExportPdf) && (
              <span className="toolbar-divider" aria-hidden="true" />
            )}

            {canImport && (
              <Link
                to="/employees/import"
                className="btn-import"
                title="Excel şablonu ile toplu personel yükle (içe aktarım)"
              >
                <span className="label-full">Toplu yükle</span>
                <span className="label-short">Yükle</span>
              </Link>
            )}
            {canCreate && (
              <Link to="/employees/new" className="btn-primary">
                <span className="label-full">+ Yeni personel</span>
                <span className="label-short">+ Yeni</span>
              </Link>
            )}
          </div>
        </div>

        <form className="employees-filters" onSubmit={onSearchSubmit}>
          <div className="employees-filters-search">
            <input
              placeholder="Ad, soyad, sicil, unvan, görev, birim, tesis, e-posta, telefon, istihdam, bölüm, yetkinlik…"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
            <button type="submit">Ara</button>
          </div>

          <div className="employees-filters-row employees-filters-org">
            <select
              value={orgCascade.directorateId}
              onFocus={() => void reloadOptions()}
              onChange={(e) => {
                setPage(1)
                setUnitId(e.target.value)
                setFacilityId('')
                setAdvanced((prev) => ({ ...prev, includeSubUnits: true }))
                setActiveViewId(null)
              }}
              aria-label="Müdürlük"
            >
              <option value="">Tüm müdürlükler</option>
              {directorateOptions.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
            <select
              value={orgCascade.mainUnitId}
              onFocus={() => void reloadOptions()}
              onChange={(e) => {
                setPage(1)
                setUnitId(e.target.value || orgCascade.directorateId)
                setFacilityId('')
                setAdvanced((prev) => ({ ...prev, includeSubUnits: true }))
                setActiveViewId(null)
              }}
              aria-label="Ana birim"
            >
              <option value="">Tüm ana birimler</option>
              {mainUnitOptions.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
            <select
              value={orgCascade.subUnitId}
              onFocus={() => void reloadOptions()}
              onChange={(e) => {
                setPage(1)
                setUnitId(e.target.value || orgCascade.mainUnitId || orgCascade.directorateId)
                setFacilityId('')
                setAdvanced((prev) => ({ ...prev, includeSubUnits: true }))
                setActiveViewId(null)
              }}
              aria-label="Alt birim"
            >
              <option value="">Tüm alt birimler</option>
              {subUnitOptions.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
            <select
              value={facilityId}
              onFocus={() => void reloadOptions()}
              onChange={(e) => {
                setPage(1)
                setFacilityId(e.target.value)
                setActiveViewId(null)
              }}
              aria-label="Tesis"
            >
              <option value="">Tüm tesisler</option>
              {facilityOptions.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                </option>
              ))}
            </select>
            <select
              value={employmentTypeId}
              onFocus={() => void reloadOptions()}
              onChange={(e) => {
                setPage(1)
                setEmploymentTypeId(e.target.value)
                setActiveViewId(null)
              }}
              aria-label="İstihdam türü"
            >
              <option value="">Tüm istihdam türleri</option>
              {(options?.employmentTypes ?? []).map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
            <select
              value={status}
              onChange={(e) => {
                setPage(1)
                setStatus(e.target.value === '' ? '' : (Number(e.target.value) as EmployeeStatus))
                setActiveViewId(null)
              }}
              aria-label="Durum"
            >
              <option value="">Tüm durumlar</option>
              {statusOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
            <div className="employees-filters-actions">
              {admin ? (
                <button
                  type="button"
                  className={`btn-advanced ${advancedOpen || hasAdvancedActive ? 'is-active' : ''}`}
                  onClick={() => setAdvancedOpen((v) => !v)}
                  aria-expanded={advancedOpen}
                >
                  Gelişmiş filtre{hasAdvancedActive ? ` (${countAdvancedActive(advanced, unitId)})` : ''}
                </button>
              ) : null}
              {hasActiveFilters && (
                <button type="button" className="btn-filter-clear" onClick={clearFilters}>
                  Temizle
                </button>
              )}
            </div>
          </div>
        </form>

        {admin && advancedOpen && (
          <div className="advanced-filters">
            <div className="advanced-filters-head">
              <strong>Gelişmiş filtreler</strong>
              <span className="muted small">Seçimler listeye anında uygulanır</span>
            </div>
            {optionsError && <div className="form-error">{optionsError}</div>}
            {!optionsError && !(options?.jobTitles?.length) && (
              <div className="form-error">
                Unvan / görev listeleri boş. API’yi yeniden başlatıp sayfayı yenileyin.
              </div>
            )}
            <div className="advanced-filters-grid">
              <label className="filter-field">
                <span>Resmi unvan</span>
                <select
                  value={advanced.jobTitleId}
                  onChange={(e) => patchAdvanced('jobTitleId', e.target.value)}
                >
                  <option value="">Tümü</option>
                  {(options?.jobTitles ?? []).map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="filter-field">
                <span>Fiili görev</span>
                <select
                  value={advanced.jobDutyId}
                  onChange={(e) => patchAdvanced('jobDutyId', e.target.value)}
                >
                  <option value="">Tümü</option>
                  {(options?.duties ?? []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="filter-field">
                <span>Görev kategorisi</span>
                <select
                  value={advanced.dutyCategory}
                  onChange={(e) =>
                    patchAdvanced(
                      'dutyCategory',
                      e.target.value === '' ? '' : Number(e.target.value),
                    )
                  }
                >
                  <option value="">Tümü</option>
                  {dutyCategoryOptions.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="filter-field">
                <span>Eğitim seviyesi</span>
                <select
                  value={advanced.educationLevel}
                  onChange={(e) =>
                    patchAdvanced(
                      'educationLevel',
                      e.target.value === '' ? '' : Number(e.target.value),
                    )
                  }
                >
                  <option value="">Tümü</option>
                  {educationLevelOptions.map((l) => (
                    <option key={l.value} value={l.value}>
                      {l.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="filter-field">
                <span>Yetkinlik</span>
                <select
                  value={advanced.skillId}
                  onChange={(e) => patchAdvanced('skillId', e.target.value)}
                >
                  <option value="">Tümü</option>
                  {(options?.skills ?? []).map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="filter-field">
                <span>İşe giriş yılı (başlangıç)</span>
                <input
                  type="number"
                  min={1950}
                  max={2100}
                  placeholder="örn. 2018"
                  value={advanced.hireYearFrom}
                  onChange={(e) => patchAdvanced('hireYearFrom', e.target.value)}
                />
              </label>
              <label className="filter-field">
                <span>İşe giriş yılı (bitiş)</span>
                <input
                  type="number"
                  min={1950}
                  max={2100}
                  placeholder="örn. 2024"
                  value={advanced.hireYearTo}
                  onChange={(e) => patchAdvanced('hireYearTo', e.target.value)}
                />
              </label>
              <label className="filter-field">
                <span>Üniversite</span>
                <input
                  type="text"
                  placeholder="örn. Gaziantep"
                  value={advanced.universityContains}
                  onChange={(e) => patchAdvanced('universityContains', e.target.value)}
                />
              </label>
              <label className="filter-field">
                <span>Mezuniyet yılı (başlangıç)</span>
                <input
                  type="number"
                  min={1950}
                  max={2100}
                  placeholder="örn. 2010"
                  value={advanced.graduationYearFrom}
                  onChange={(e) => patchAdvanced('graduationYearFrom', e.target.value)}
                />
              </label>
              <label className="filter-field">
                <span>Mezuniyet yılı (bitiş)</span>
                <input
                  type="number"
                  min={1950}
                  max={2100}
                  placeholder="örn. 2020"
                  value={advanced.graduationYearTo}
                  onChange={(e) => patchAdvanced('graduationYearTo', e.target.value)}
                />
              </label>
            </div>

            <div className="advanced-filters-flags">
              <label className={!unitId ? 'is-disabled' : undefined}>
                <input
                  type="checkbox"
                  checked={advanced.includeSubUnits}
                  disabled={!unitId}
                  onChange={(e) => patchAdvanced('includeSubUnits', e.target.checked)}
                />
                Alt birimleri dahil et
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.incompleteProfileOnly}
                  onChange={(e) => patchAdvanced('incompleteProfileOnly', e.target.checked)}
                />
                Eksik profil (&lt; %80)
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.missingSkillsOnly}
                  onChange={(e) => patchAdvanced('missingSkillsOnly', e.target.checked)}
                />
                Yetkinlik girilmemiş
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.missingPhoneOnly}
                  onChange={(e) => patchAdvanced('missingPhoneOnly', e.target.checked)}
                />
                Telefon eksik
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.missingFacilityOnly}
                  onChange={(e) => patchAdvanced('missingFacilityOnly', e.target.checked)}
                />
                Tesis eksik
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.hasSpecialConditionOnly}
                  onChange={(e) => patchAdvanced('hasSpecialConditionOnly', e.target.checked)}
                />
                Özel durumu olanlar
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.hasNotesOnly}
                  onChange={(e) => patchAdvanced('hasNotesOnly', e.target.checked)}
                />
                Notu olanlar
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.missingCertificatesOnly}
                  onChange={(e) => patchAdvanced('missingCertificatesOnly', e.target.checked)}
                />
                Sertifika yok
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={advanced.expiredCertificateOnly}
                  onChange={(e) => patchAdvanced('expiredCertificateOnly', e.target.checked)}
                />
                Süresi dolmuş sertifika
              </label>
            </div>
            <div className="advanced-filters-foot">
              <span className="muted small">
                {loading ? 'Filtreleniyor…' : `${totalCount} kayıt eşleşti`}
              </span>
              <button type="button" className="btn-filter-clear" onClick={() => setAdvancedOpen(false)}>
                Paneli kapat
              </button>
            </div>
          </div>
        )}

        {admin ? (
        <div className="employees-viewbar">
          <div className="employees-viewbar-left">
            <div className="popover-wrap" ref={viewsPanelRef}>
              <button
                type="button"
                className={`btn-viewbar ${viewsOpen || activeViewId ? 'is-active' : ''}`}
                onClick={() => {
                  setViewsOpen((v) => !v)
                  setColumnsOpen(false)
                }}
                aria-expanded={viewsOpen}
              >
                <span className="label-full">
                  Görünümler{activeViewName ? `: ${activeViewName}` : ''}
                </span>
                <span className="label-short">Görünümler</span>
              </button>
              {viewsOpen && (
                <div className="popover-panel" role="dialog" aria-label="Kayıtlı görünümler">
                  <div className="popover-head">
                    <strong>Kayıtlı görünümler</strong>
                    <span className="muted small">Filtre + sıralama · sütunlar ayrı</span>
                  </div>
                  <ul className="view-list">
                    {allViews.map((view) => {
                      const summary = describeListFilters(view.filters)
                      return (
                        <li key={view.id} className={activeViewId === view.id ? 'is-active' : ''}>
                          <button
                            type="button"
                            className="view-list-main"
                            onClick={() => applyView(view)}
                          >
                            <span className="view-list-text">
                              <span className="view-list-name">
                                {view.name}
                                {view.builtIn && <span className="view-tag">Hazır</span>}
                              </span>
                              <span className="view-list-summary">
                                {summary.length > 0 ? summary.join(' · ') : 'Filtre yok (tüm kayıtlar)'}
                              </span>
                            </span>
                          </button>
                          {!view.builtIn && (
                            <button
                              type="button"
                              className="view-list-del"
                              title="Sil"
                              onClick={() => deleteUserView(view.id)}
                            >
                              ×
                            </button>
                          )}
                        </li>
                      )
                    })}
                  </ul>
                  <div className="view-save">
                    <p className="muted small view-save-hint">
                      Önce filtreyi seçin (ör. Tesis eksik), sonra kaydedin. Listedeki özet o
                      filtrenin farkını gösterir.
                    </p>
                    <input
                      value={newViewName}
                      onChange={(e) => setNewViewName(e.target.value)}
                      placeholder="Yeni görünüm adı"
                      maxLength={40}
                    />
                    <button
                      type="button"
                      className="btn-primary"
                      disabled={!newViewName.trim()}
                      onClick={saveCurrentAsView}
                    >
                      Kaydet
                    </button>
                  </div>
                </div>
              )}
            </div>

            <div className="popover-wrap" ref={columnsPanelRef}>
              <button
                type="button"
                className={`btn-viewbar ${columnsOpen ? 'is-active' : ''}`}
                onClick={() => {
                  setColumnsOpen((v) => !v)
                  setViewsOpen(false)
                }}
                aria-expanded={columnsOpen}
              >
                <span className="label-full">Sütunlar ({activeColumns.length})</span>
                <span className="label-short">Sütunlar</span>
              </button>
              {columnsOpen && (
                <div className="popover-panel" role="dialog" aria-label="Sütun seçici">
                  <div className="popover-head">
                    <strong>Görünen sütunlar</strong>
                    <button type="button" className="linkish" onClick={resetColumns}>
                      Varsayılana dön
                    </button>
                  </div>
                  {newColumnIds.length > 0 && (
                    <p className="column-new-banner">
                      Yeni sütun:{' '}
                      {newColumnIds
                        .map((id) => COLUMN_DEFS.find((c) => c.id === id)?.label)
                        .filter(Boolean)
                        .join(', ')}
                    </p>
                  )}
                  <div className="column-check-grid">
                    {columnDefs.map((col) => (
                      <label key={col.id} className={col.locked ? 'is-locked' : undefined}>
                        <input
                          type="checkbox"
                          checked={activeColumns.includes(col.id)}
                          disabled={col.locked}
                          onChange={() => toggleColumn(col.id)}
                        />
                        <span>
                          {col.label}
                          {col.locked ? <span className="muted small"> (sabit)</span> : null}
                          {newColumnIds.includes(col.id) ? (
                            <span className="col-new-tag">Yeni</span>
                          ) : null}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
          <p className="muted small employees-viewbar-hint">Tercihler bu tarayıcıda saklanır.</p>
        </div>
        ) : null}

        {filterChips.length > 0 && (
          <div className="active-filter-chips" aria-label="Aktif filtreler">
            {filterChips.map((chip) => (
              <span key={chip} className="filter-chip">
                {chip}
              </span>
            ))}
            <button type="button" className="filter-chip-clear" onClick={clearFilters}>
              Filtreleri temizle
            </button>
          </div>
        )}

        {error && <div className="form-error">{error}</div>}

        <p className="employees-table-scroll-hint">Tabloyu yana kaydırarak tüm sütunları görün.</p>

        <div className="table-wrap employees-table-wrap">
          <table className="data-table employees-table">
            <thead>
              <tr>
                {activeColumns.map((id) => {
                  const def = COLUMN_DEFS.find((c) => c.id === id)!
                  const sticky =
                    id === 'name' ? 'is-sticky-start' : id === 'actions' ? 'is-sticky-end' : ''
                  return (
                    <th key={id} className={`${sticky} ${id === 'actions' ? 'col-actions' : ''}`.trim()}>
                      {def.sortKey ? (
                        <button
                          type="button"
                          className="th-btn"
                          onClick={() => toggleSort(def.sortKey!)}
                        >
                          {def.label} {sortMark(sortBy, sortDesc, def.sortKey)}
                        </button>
                      ) : (
                        def.label
                      )}
                    </th>
                  )
                })}
              </tr>
            </thead>
            <tbody>
              {loading && (
                <tr>
                  <td colSpan={activeColumns.length} className="muted">
                    <div className="ui-skeleton-stack" aria-hidden="true">
                      <span className="ui-skeleton" />
                      <span className="ui-skeleton" />
                      <span className="ui-skeleton" />
                    </div>
                    Personel listesi yükleniyor…
                  </td>
                </tr>
              )}
              {!loading && items.length === 0 && (
                <tr>
                  <td colSpan={activeColumns.length} className="muted">
                    {filterChips.length > 0
                      ? `Bu filtrelere uyan kayıt yok (${filterChips.join(' · ')}).`
                      : 'Kayıt bulunamadı.'}
                  </td>
                </tr>
              )}
              {!loading &&
                items.map((row) => (
                  <tr key={row.id}>
                    {activeColumns.map((id) => (
                      <EmployeeCell
                        key={id}
                        id={id}
                        row={row}
                        canUpdate={canUpdate}
                        canCreateMovements={canCreateMovements}
                        canSetStatus={canSetStatus}
                        canArchive={canArchive}
                        onLifecycle={setLifecycle}
                      />
                    ))}
                  </tr>
                ))}
            </tbody>
          </table>
        </div>

        <div className="pager">
          <div className="pager-nav">
            <button type="button" disabled={page <= 1 || loading} onClick={() => setPage((p) => p - 1)}>
              Önceki
            </button>
            <span className="muted">
              Sayfa {page} / {Math.max(totalPages, 1)}
              <span className="pager-range">
                {' '}
                · {totalCount === 0 ? 0 : (page - 1) * pageSize + 1}–
                {Math.min(page * pageSize, totalCount)} / {totalCount}
              </span>
            </span>
            <button
              type="button"
              disabled={page >= totalPages || loading || totalPages === 0}
              onClick={() => setPage((p) => p + 1)}
            >
              Sonraki
            </button>
          </div>
          <label className="pager-size">
            <span className="muted small">Sayfa başı</span>
            <select
              value={pageSize}
              aria-label="Sayfa başına kayıt"
              onChange={(e) => {
                const next = Number(e.target.value)
                setPageSize(next)
                setPage(1)
                try {
                  localStorage.setItem(PAGE_SIZE_KEY, String(next))
                } catch {
                  // ignore
                }
              }}
            >
              {PAGE_SIZE_OPTIONS.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
        </div>
      </section>
      {lifecycle?.kind === 'workplace' ? (
        <WorkplaceChangeDialog
          employeeId={lifecycle.id}
          onClose={() => setLifecycle(null)}
          onSaved={async () => {
            setLifecycle(null)
            await load()
          }}
        />
      ) : null}
      {lifecycle?.kind === 'transfer' ? (
        <TransferDirectorateDialog
          employeeId={lifecycle.id}
          fullName={lifecycle.name}
          onClose={() => setLifecycle(null)}
          onSaved={async () => {
            setLifecycle(null)
            await load()
          }}
        />
      ) : null}
      {lifecycle?.kind === 'delete' ? (
        <DeleteEmployeeDialog
          employeeId={lifecycle.id}
          fullName={lifecycle.name}
          onClose={() => setLifecycle(null)}
          onDeleted={async () => {
            setLifecycle(null)
            await load()
          }}
        />
      ) : null}
    </div>
  )
}

function EmployeeCell({
  id,
  row,
  canUpdate,
  canCreateMovements,
  canSetStatus,
  canArchive,
  onLifecycle,
}: {
  id: ColumnId
  row: EmployeeListItem
  canUpdate: boolean
  canCreateMovements: boolean
  canSetStatus: boolean
  canArchive: boolean
  onLifecycle: (
    next:
      | { kind: 'workplace'; id: string }
      | { kind: 'transfer'; id: string; name: string }
      | { kind: 'delete'; id: string; name: string },
  ) => void
}) {
  switch (id) {
    case 'name':
      return (
        <td className="is-sticky-start">
          <div className="emp-name-cell">
            <EmployeeAvatar
              employeeId={row.id}
              name={row.fullName}
              hasPhoto={Boolean(row.hasPhoto)}
            />
            <div className="emp-name-text">
              <Link to={`/employees/${row.id}`} className="row-link">
                <strong>{row.fullName}</strong>
              </Link>
              {row.hasSpecialCondition && (
                <span className="badge-warn" title="Özel durum kaydı var (detay yetkiye bağlı)">
                  Özel
                </span>
              )}
            </div>
          </div>
        </td>
      )
    case 'employeeNumber':
      return <td>{row.employeeNumber ?? '—'}</td>
    case 'jobTitle':
      return <td className="cell-clip">{row.jobTitleName ?? '—'}</td>
    case 'duty':
      return <td className="cell-clip">{row.primaryDutyName ?? '—'}</td>
    case 'unit':
      return <td className="cell-clip">{row.unitName ?? '—'}</td>
    case 'facility':
      return (
        <td className="cell-clip" title={row.facilityName ?? undefined}>
          {row.facilityName ?? '—'}
        </td>
      )
    case 'employment':
      return <td>{row.employmentTypeName ?? '—'}</td>
    case 'phone':
      return <td>{row.personalPhone ?? row.corporatePhone ?? '—'}</td>
    case 'hireDate':
      return <td>{row.hireDate ? formatDate(row.hireDate) : '—'}</td>
    case 'serviceYears':
      return <td>{serviceYearsLabel(row.hireDate)}</td>
    case 'status':
      return (
        <td>
          <span className={`status-pill status-${row.status}`}>{row.statusLabel}</span>
        </td>
      )
    case 'completion':
      return (
        <td>
          <div className="completion">
            <div
              className="completion-bar"
              style={{ width: `${row.profileCompletionPercent}%` }}
            />
            <span>{row.profileCompletionPercent}%</span>
          </div>
        </td>
      )
    case 'updated':
      return <td className="muted small">{formatDateTime(row.updatedAtUtc)}</td>
    case 'actions':
      return (
        <td className="col-actions is-sticky-end">
          <EmployeeRowActionsMenu
            employeeId={row.id}
            fullName={row.fullName}
            status={row.status}
            canUpdate={canUpdate}
            canCreateMovements={canCreateMovements}
            canSetStatus={canSetStatus}
            canArchive={canArchive}
            onLifecycle={onLifecycle}
          />
        </td>
      )
    default:
      return <td>—</td>
  }
}

function EmployeeRowActionsMenu({
  employeeId,
  fullName,
  status,
  canUpdate,
  canCreateMovements,
  canSetStatus,
  canArchive,
  onLifecycle,
}: {
  employeeId: string
  fullName: string
  status: EmployeeStatus
  canUpdate: boolean
  canCreateMovements: boolean
  canSetStatus: boolean
  canArchive: boolean
  onLifecycle: (
    next:
      | { kind: 'workplace'; id: string }
      | { kind: 'transfer'; id: string; name: string }
      | { kind: 'delete'; id: string; name: string },
  ) => void
}) {
  const [open, setOpen] = useState(false)
  const [pos, setPos] = useState<{ top: number; right: number } | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)
  const dropdownRef = useRef<HTMLDivElement>(null)

  // Yapışkan sütun kendi yığın bağlamını kurduğu ve tablo taşmayı kırptığı için
  // menü body'ye taşınıp tetikleyiciye göre sabit konumlandırılır.
  useLayoutEffect(() => {
    if (!open) return
    const trigger = rootRef.current
    if (!trigger) return

    function place() {
      const rect = trigger!.getBoundingClientRect()
      setPos({
        top: rect.bottom + 4,
        right: Math.max(8, window.innerWidth - rect.right),
      })
    }

    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open])

  useEffect(() => {
    if (!open) return

    function onDocPointer(e: MouseEvent) {
      const target = e.target as Node
      if (rootRef.current?.contains(target)) return
      if (dropdownRef.current?.contains(target)) return
      setOpen(false)
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false)
    }

    document.addEventListener('mousedown', onDocPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDocPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  return (
    <div className={`row-actions-menu${open ? ' is-open' : ''}`} ref={rootRef}>
      <button
        type="button"
        className="row-actions-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Satır işlemleri"
        onClick={() => setOpen((v) => !v)}
      >
        İşlem
        <span aria-hidden="true">▾</span>
      </button>
      {open &&
        pos &&
        createPortal(
          <div
            ref={dropdownRef}
            className="row-actions-dropdown"
            role="menu"
            style={{ top: pos.top, right: pos.right }}
          >
            <Link
              to={`/employees/${employeeId}`}
              className="row-actions-item"
              role="menuitem"
              onClick={() => setOpen(false)}
            >
              Görüntüle
            </Link>
            {canUpdate && (
              <Link
                to={`/employees/${employeeId}/edit`}
                className="row-actions-item"
                role="menuitem"
                onClick={() => setOpen(false)}
              >
                Düzenle
              </Link>
            )}
            {canCreateMovements && status === 1 && (
              <button
                type="button"
                className="row-actions-item"
                role="menuitem"
                onClick={() => {
                  setOpen(false)
                  onLifecycle({ kind: 'workplace', id: employeeId })
                }}
              >
                Görev yeri değiştir
              </button>
            )}
            {canSetStatus && status === 1 && (
              <button
                type="button"
                className="row-actions-item"
                role="menuitem"
                onClick={() => {
                  setOpen(false)
                  onLifecycle({ kind: 'transfer', id: employeeId, name: fullName })
                }}
              >
                Başka müdürlüğe geçti
              </button>
            )}
            {canArchive && (
              <button
                type="button"
                className="row-actions-item is-danger"
                role="menuitem"
                onClick={() => {
                  setOpen(false)
                  onLifecycle({ kind: 'delete', id: employeeId, name: fullName })
                }}
              >
                Sil
              </button>
            )}
          </div>,
          document.body,
        )}
    </div>
  )
}

function sortMark(sortBy: string, sortDesc: boolean, column: string): string {
  if (sortBy !== column) return ''
  return sortDesc ? '↓' : '↑'
}

function countAdvancedActive(advanced: AdvancedFiltersState, unitId: string): number {
  let n = 0
  if (advanced.jobTitleId) n++
  if (advanced.jobDutyId) n++
  if (advanced.dutyCategory !== '') n++
  if (advanced.educationLevel !== '') n++
  if (advanced.skillId) n++
  if (advanced.hireYearFrom) n++
  if (advanced.hireYearTo) n++
  if (advanced.universityContains.trim()) n++
  if (advanced.graduationYearFrom) n++
  if (advanced.graduationYearTo) n++
  if (advanced.incompleteProfileOnly) n++
  if (advanced.missingSkillsOnly) n++
  if (advanced.missingPhoneOnly) n++
  if (advanced.missingFacilityOnly) n++
  if (advanced.hasSpecialConditionOnly) n++
  if (advanced.hasNotesOnly) n++
  if (advanced.missingCertificatesOnly) n++
  if (advanced.expiredCertificateOnly) n++
  if (unitId && !advanced.includeSubUnits) n++
  return n
}

function hasAdvancedInFilters(advanced: AdvancedFiltersState, unitId: string): boolean {
  return countAdvancedActive(advanced, unitId) > 0
}

function listFiltersActive(filters: ListFiltersState): boolean {
  return Boolean(
    filters.search ||
      filters.unitId ||
      filters.facilityId ||
      filters.employmentTypeId ||
      filters.status ||
      hasAdvancedInFilters(filters.advanced, filters.unitId),
  )
}

function currentFiltersSnapshot(
  search: string,
  unitId: string,
  facilityId: string,
  employmentTypeId: string,
  status: '' | EmployeeStatus,
  advanced: AdvancedFiltersState,
): ListFiltersState {
  return { search, unitId, facilityId, employmentTypeId, status, advanced }
}

const ORG_TYPE = {
  Directorate: 3,
  MainUnit: 4,
  SubUnit: 5,
} as const

function resolveOrgCascade(units: LookupItem[], selectedUnitId: string) {
  const empty = { directorateId: '', mainUnitId: '', subUnitId: '' }
  if (!selectedUnitId) return empty

  const byId = new Map(units.map((u) => [u.id, u]))
  const chain: LookupItem[] = []
  let current: LookupItem | undefined = byId.get(selectedUnitId)
  while (current) {
    chain.unshift(current)
    current = current.parentId ? byId.get(current.parentId) : undefined
  }

  const result = { ...empty }
  for (const u of chain) {
    if (u.type === ORG_TYPE.Directorate) result.directorateId = u.id
    if (u.type === ORG_TYPE.MainUnit) result.mainUnitId = u.id
    if (u.type === ORG_TYPE.SubUnit) result.subUnitId = u.id
  }

  // Tip bilinmeyen eski kayıt: seçili düğümü mümkün olan en derin seviyeye koy
  if (!result.directorateId && !result.mainUnitId && !result.subUnitId) {
    const selected = byId.get(selectedUnitId)
    if (selected?.type === ORG_TYPE.Directorate) result.directorateId = selectedUnitId
    else if (selected?.type === ORG_TYPE.MainUnit) result.mainUnitId = selectedUnitId
    else if (selected?.type === ORG_TYPE.SubUnit) result.subUnitId = selectedUnitId
    else result.directorateId = selectedUnitId
  }

  return result
}

function collectDescendantIds(units: LookupItem[], rootId: string): Set<string> {
  const children = new Map<string, string[]>()
  for (const u of units) {
    if (!u.parentId) continue
    const list = children.get(u.parentId) ?? []
    list.push(u.id)
    children.set(u.parentId, list)
  }

  const result = new Set<string>([rootId])
  const queue = [rootId]
  while (queue.length > 0) {
    const id = queue.pop()!
    for (const childId of children.get(id) ?? []) {
      if (!result.has(childId)) {
        result.add(childId)
        queue.push(childId)
      }
    }
  }
  return result
}

function formatDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-')
  if (!y || !m || !d) return isoDate
  return `${d}.${m}.${y}`
}

function formatDateTime(iso?: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
