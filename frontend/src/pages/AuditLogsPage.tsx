import { useCallback, useEffect, useMemo, useState, type FormEvent, Fragment } from 'react'
import { fetchAuditLogs, type AuditLogItem } from '@/api/auditApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

const ACTION_OPTIONS = [
  { value: '', label: 'Tüm işlemler' },
  { value: 'Create', label: 'Oluşturma' },
  { value: 'Update', label: 'Güncelleme' },
  { value: 'SoftDelete', label: 'Arşivleme' },
  { value: 'Delete', label: 'Silme' },
] as const

const ENTITY_OPTIONS = [
  { value: '', label: 'Tüm kayıt türleri' },
  { value: 'Employee', label: 'Personel' },
  { value: 'EmployeeMovement', label: 'Görev hareketi' },
  { value: 'EmployeeNote', label: 'Personel notu' },
  { value: 'SpecialCondition', label: 'Özel durum' },
  { value: 'EmployeeSkill', label: 'Yetkinlik' },
  { value: 'EmployeeCertificate', label: 'Sertifika' },
  { value: 'EducationRecord', label: 'Eğitim' },
  { value: 'OrganizationUnit', label: 'Birim' },
  { value: 'FacilityCategory', label: 'Tesis türü' },
  { value: 'AppUser', label: 'Kullanıcı' },
  { value: 'Role', label: 'Rol' },
  { value: 'RolePermission', label: 'Rol yetkisi' },
] as const

const ENTITY_LABELS: Record<string, string> = Object.fromEntries(
  ENTITY_OPTIONS.filter((o) => o.value).map((o) => [o.value, o.label]),
)

const ACTION_LABELS: Record<string, string> = {
  Create: 'Oluşturma',
  Update: 'Güncelleme',
  SoftDelete: 'Arşivleme',
  Delete: 'Silme',
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const
const PAGE_SIZE_KEY = 'py.audit.pageSize.v1'

function loadPageSize(): number {
  try {
    const n = Number(localStorage.getItem(PAGE_SIZE_KEY))
    return (PAGE_SIZE_OPTIONS as readonly number[]).includes(n) ? n : 25
  } catch {
    return 25
  }
}

const FIELD_LABELS: Record<string, string> = {
  FirstName: 'Ad',
  LastName: 'Soyad',
  Email: 'E-posta',
  Phone: 'Telefon',
  CorporatePhone: 'Kurumsal telefon',
  CorporateEmail: 'Kurumsal e-posta',
  Address: 'Adres',
  Status: 'Durum',
  HireDate: 'İşe giriş',
  BirthDate: 'Doğum tarihi',
  Gender: 'Cinsiyet',
  EmployeeNumber: 'Personel no',
  UnitId: 'Birim',
  FacilityId: 'Tesis',
  JobTitleId: 'Unvan',
  JobDutyId: 'Fiili görev',
  EmploymentTypeId: 'İstihdam',
  IsActive: 'Aktif',
  IsDeleted: 'Arşiv',
  UserName: 'Giriş adı',
  Name: 'Ad',
  Code: 'Kod',
  Description: 'Açıklama',
  Title: 'Başlık',
  Content: 'İçerik',
  NoteType: 'Not türü',
  MovementType: 'Hareket türü',
  EffectiveDate: 'Geçerlilik',
  Reason: 'Gerekçe',
}

type DiffRow = { key: string; label: string; oldValue: string; newValue: string }

function entityLabel(name: string) {
  return ENTITY_LABELS[name] ?? name
}

function actionLabel(action: string) {
  return ACTION_LABELS[action] ?? action
}

function fieldLabel(key: string) {
  return FIELD_LABELS[key] ?? key
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '—'
  if (typeof value === 'boolean') return value ? 'Evet' : 'Hayır'
  if (typeof value === 'string') {
    if (/^\d{4}-\d{2}-\d{2}/.test(value)) {
      try {
        return new Date(value).toLocaleString('tr-TR')
      } catch {
        return value
      }
    }
    return value
  }
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

function parseJson(raw?: string | null): Record<string, unknown> | null {
  if (!raw) return null
  try {
    const parsed = JSON.parse(raw) as unknown
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>
    }
  } catch {
    /* ignore */
  }
  return null
}

function buildDiff(row: AuditLogItem): DiffRow[] {
  const oldObj = parseJson(row.oldValuesJson)
  const newObj = parseJson(row.newValuesJson)
  if (!oldObj && !newObj) return []

  const keys = new Set([
    ...Object.keys(oldObj ?? {}),
    ...Object.keys(newObj ?? {}),
  ])

  const skip = new Set([
    'Id',
    'CreatedAtUtc',
    'UpdatedAtUtc',
    'CreatedBy',
    'UpdatedBy',
    'RowVersion',
  ])

  const rows: DiffRow[] = []
  for (const key of [...keys].sort()) {
    if (skip.has(key)) continue
    const oldValue = formatValue(oldObj?.[key])
    const newValue = formatValue(newObj?.[key])
    if (oldValue === newValue && row.action === 'Update') continue
    rows.push({
      key,
      label: fieldLabel(key),
      oldValue,
      newValue,
    })
  }
  return rows
}

function formatWhen(iso: string) {
  try {
    return new Date(iso).toLocaleString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return iso
  }
}

function shortId(id?: string | null) {
  if (!id) return '—'
  return id.length > 10 ? `${id.slice(0, 8)}…` : id
}

export function AuditLogsPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.AuditLogsView)

  const [items, setItems] = useState<AuditLogItem[]>([])
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(loadPageSize)
  const [totalPages, setTotalPages] = useState(0)
  const [totalCount, setTotalCount] = useState(0)
  const [action, setAction] = useState('')
  const [entityName, setEntityName] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      const result = await fetchAuditLogs({
        page,
        pageSize,
        action: action || undefined,
        entityName: entityName || undefined,
        search: search || undefined,
        from: from || undefined,
        to: to || undefined,
      })
      setItems(result.items)
      setTotalPages(result.totalPages)
      setTotalCount(result.totalCount)
      setExpandedId(null)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'İşlem geçmişi alınamadı.')
    } finally {
      setLoading(false)
    }
  }, [action, canView, entityName, from, page, pageSize, search, to])

  useEffect(() => {
    void load()
  }, [load])

  const initialLoading = loading && items.length === 0 && !error

  const createCount = useMemo(
    () => items.filter((i) => i.action === 'Create').length,
    [items],
  )
  const updateCount = useMemo(
    () => items.filter((i) => i.action === 'Update').length,
    [items],
  )

  if (!canView) {
    return (
      <div className="org-page">
        <div className="panel">
          <h1>İşlem geçmişi</h1>
          <p className="muted">Bu ekranı görüntüleme yetkiniz bulunmuyor.</p>
        </div>
      </div>
    )
  }

  function onFilter(e: FormEvent) {
    e.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
  }

  function clearFilters() {
    setSearchInput('')
    setSearch('')
    setAction('')
    setEntityName('')
    setFrom('')
    setTo('')
    setPage(1)
  }

  return (
    <div className="org-page audit-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Denetim</p>
          <h1>İşlem geçmişi</h1>
          <p className="muted">
            Kim neyi ne zaman değiştirdi? Personel ekleme, güncelleme, görev değişikliği ve
            not gibi kritik işlemler burada tutulur. Kayıtlar silinemez.
          </p>
        </div>
        <div className="report-hero-side">
          <div className="report-hero-stats">
            <div>
              <strong>{totalCount}</strong>
              <span>Toplam kayıt</span>
            </div>
            <div>
              <strong>{createCount}</strong>
              <span>Oluşturma*</span>
            </div>
            <div>
              <strong>{updateCount}</strong>
              <span>Güncelleme*</span>
            </div>
          </div>
          <button type="button" className="btn-secondary" onClick={() => void load()} disabled={loading}>
            Yenile
          </button>
        </div>
      </header>

      <section className="panel audit-panel">
        <form className="audit-filters" onSubmit={onFilter}>
          <label className="audit-search">
            <span className="sr-only">Ara</span>
            <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden>
              <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
              <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Kullanıcı, kayıt türü veya kimlik ara…"
            />
          </label>
          <select
            value={entityName}
            onChange={(e) => {
              setEntityName(e.target.value)
              setPage(1)
            }}
            aria-label="Kayıt türü"
          >
            {ENTITY_OPTIONS.map((o) => (
              <option key={o.value || 'all'} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          <select
            value={action}
            onChange={(e) => {
              setAction(e.target.value)
              setPage(1)
            }}
            aria-label="İşlem türü"
          >
            {ACTION_OPTIONS.map((o) => (
              <option key={o.value || 'all'} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          <label className="audit-date">
            <span>Başlangıç</span>
            <input
              type="date"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value)
                setPage(1)
              }}
            />
          </label>
          <label className="audit-date">
            <span>Bitiş</span>
            <input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value)
                setPage(1)
              }}
            />
          </label>
          <div className="audit-filter-actions">
            <button type="submit" className="btn-primary">
              Filtrele
            </button>
            <button type="button" className="btn-secondary" onClick={clearFilters}>
              Temizle
            </button>
          </div>
        </form>

        <p className="audit-note muted small">
          * Sayfa özetindeki oluşturma/güncelleme sayıları yalnızca bu sayfadaki kayıtlara aittir.
          Hassas alanlar (TCKN, parola) kayıtta maskelenir.
        </p>

        {error ? (
          <div className="form-error" role="alert">
            {error}
          </div>
        ) : null}

        {initialLoading ? (
          <p className="muted">Yükleniyor…</p>
        ) : items.length === 0 && !loading ? (
          <div className="audit-empty">
            <strong>Kayıt bulunamadı</strong>
            <p className="muted">
              Filtreleri gevşetin veya sistemde bir personel / görev değişikliği yapıp yeniden
              deneyin.
            </p>
          </div>
        ) : items.length === 0 ? (
          <p className="muted">Yükleniyor…</p>
        ) : (
          <div className={`audit-table-wrap${loading ? ' is-refreshing' : ''}`}>
            <table className="audit-table">
              <thead>
                <tr>
                  <th>Zaman</th>
                  <th>Kullanıcı</th>
                  <th>İşlem</th>
                  <th>Kayıt</th>
                  <th>Kimlik</th>
                  <th>IP</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((row) => {
                  const open = expandedId === row.id
                  const diffs = open ? buildDiff(row) : []
                  return (
                    <Fragment key={row.id}>
                      <tr className={open ? 'is-open' : undefined}>
                        <td className="audit-when">{formatWhen(row.occurredAtUtc)}</td>
                        <td>
                          <span className="audit-user">{row.userName ?? 'Sistem'}</span>
                        </td>
                        <td>
                          <span
                            className={`audit-pill action-${row.action.toLowerCase()}`}
                          >
                            {actionLabel(row.action)}
                          </span>
                        </td>
                        <td>
                          <span className="audit-entity">{entityLabel(row.entityName)}</span>
                        </td>
                        <td className="audit-id">{shortId(row.entityId)}</td>
                        <td className="audit-ip">{row.ipAddress ?? '—'}</td>
                        <td>
                          <button
                            type="button"
                            className="btn-secondary audit-detail-btn"
                            onClick={() => setExpandedId((id) => (id === row.id ? null : row.id))}
                          >
                            {open ? 'Gizle' : 'Değişiklikler'}
                          </button>
                        </td>
                      </tr>
                      {open ? (
                        <tr className="audit-detail-row">
                          <td colSpan={7}>
                            {diffs.length === 0 ? (
                              <p className="muted small">Bu işlem için alan detayı yok.</p>
                            ) : (
                              <table className="audit-diff">
                                <thead>
                                  <tr>
                                    <th>Alan</th>
                                    <th>Eski değer</th>
                                    <th>Yeni değer</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {diffs.map((d) => (
                                    <tr key={d.key}>
                                      <td>{d.label}</td>
                                      <td className="audit-old">{d.oldValue}</td>
                                      <td className="audit-new">{d.newValue}</td>
                                    </tr>
                                  ))}
                                </tbody>
                              </table>
                            )}
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {(totalCount > 0 || totalPages > 1) ? (
          <div className="pager audit-pager">
            <div className="pager-nav">
              <button
                type="button"
                className="btn-secondary"
                disabled={page <= 1 || loading}
                onClick={() => setPage((p) => p - 1)}
              >
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
                className="btn-secondary"
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
                  setExpandedId(null)
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
        ) : null}
      </section>
    </div>
  )
}
