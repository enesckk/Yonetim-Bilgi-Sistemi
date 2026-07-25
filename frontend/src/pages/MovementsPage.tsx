import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  downloadMovementsExcel,
  fetchMovementList,
  type MovementListItem,
  type MovementListResult,
} from '@/api/movementsApi'
import { fetchEmployeeFormOptions, type EmployeeFormOptions } from '@/api/employeesApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

function formatDate(value?: string | null) {
  if (!value) return '—'
  const d = value.slice(0, 10)
  const [y, m, day] = d.split('-')
  if (!y || !m || !day) return value
  return `${day}.${m}.${y}`
}

function changeLine(oldName?: string | null, newName?: string | null) {
  if (!oldName && !newName) return null
  if (oldName && newName && oldName !== newName) return `${oldName} → ${newName}`
  return newName || oldName || null
}

function summarizeChange(item: MovementListItem) {
  const parts = [
    changeLine(item.oldUnitName, item.newUnitName),
    changeLine(item.oldFacilityName, item.newFacilityName),
    changeLine(item.oldJobTitleName, item.newJobTitleName),
    changeLine(item.oldJobDutyName, item.newJobDutyName),
  ].filter(Boolean) as string[]
  if (parts.length) return parts.join(' · ')
  return item.reason?.trim() || item.description?.trim() || '—'
}

function movementTone(type: number) {
  switch (type) {
    case 1: // UnitChange
      return 'unit'
    case 2: // FacilityChange
      return 'facility'
    case 3: // DutyChange
      return 'duty'
    case 4: // TitleChange
      return 'title'
    case 5: // TemporaryAssignment
      return 'temp'
    case 6: // PermanentAssignment
      return 'perm'
    case 7: // AdditionalDutyAssigned
      return 'extra'
    case 8: // DutyRemoved
      return 'removed'
    case 9: // TransferToOtherDirectorate
      return 'transfer'
    case 10: // LeftJob
      return 'exit'
    case 11: // Retirement
      return 'retire'
    case 12: // ReturnToDuty
      return 'return'
    default:
      return 'default'
  }
}

export function MovementsPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.MovementsView)

  const [result, setResult] = useState<MovementListResult | null>(null)
  const [options, setOptions] = useState<EmployeeFormOptions | null>(null)
  const [loading, setLoading] = useState(true)
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [unitId, setUnitId] = useState('')
  const [facilityId, setFacilityId] = useState('')
  const [movementType, setMovementType] = useState<number | ''>('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  const queryParams = useMemo(
    () => ({
      search: appliedSearch || undefined,
      unitId: unitId || undefined,
      facilityId: facilityId || undefined,
      movementType: movementType === '' ? undefined : movementType,
      from: from || undefined,
      to: to || undefined,
      take: 250,
    }),
    [appliedSearch, facilityId, from, movementType, to, unitId],
  )

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      setResult(await fetchMovementList(queryParams))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Görev geçmişi yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canView, queryParams])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!canView) return
    void fetchEmployeeFormOptions()
      .then(setOptions)
      .catch(() => setOptions(null))
  }, [canView])

  const facilities = useMemo(() => {
    const list = options?.facilities ?? []
    if (!unitId) return list
    return list.filter((f) => !f.parentId || f.parentId === unitId)
  }, [options?.facilities, unitId])

  const applySearch = () => setAppliedSearch(search.trim())

  const clearFilters = () => {
    setSearch('')
    setAppliedSearch('')
    setUnitId('')
    setFacilityId('')
    setMovementType('')
    setFrom('')
    setTo('')
  }

  const onExport = async () => {
    setExporting(true)
    setError(null)
    try {
      await downloadMovementsExcel(queryParams)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Excel indirilemedi.')
    } finally {
      setExporting(false)
    }
  }

  const onPrint = () => window.print()

  const hasActiveFilters = Boolean(
    appliedSearch || unitId || facilityId || movementType !== '' || from || to || search.trim(),
  )

  const total = result?.totalCount ?? 0
  const shown = result?.returnedCount ?? 0
  const stats = result?.typeStats ?? []
  const types = result?.movementTypes ?? []
  const personCount = useMemo(() => {
    if (!result?.items.length) return 0
    return new Set(result.items.map((i) => i.employeeId)).size
  }, [result])
  const typeCount = stats.length

  if (!canView) {
    return (
      <div className="org-page">
        <div className="panel">
          <h1>Görev geçmişi</h1>
          <p className="muted">Bu ekranı görüntüleme yetkiniz bulunmuyor.</p>
        </div>
      </div>
    )
  }

  return (
    <div className="org-page mv-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Personel hareketleri</p>
          <h1>Görev geçmişi</h1>
          <p className="muted">
            Kurum genelindeki birim, tesis, unvan ve görev değişiklikleri. Personel kartından detaya
            inebilirsiniz.
          </p>
        </div>
        <div className="report-hero-side">
          <div className="report-hero-stats">
            <div>
              <strong>{total.toLocaleString('tr-TR')}</strong>
              <span>Toplam</span>
            </div>
            <div>
              <strong>{shown.toLocaleString('tr-TR')}</strong>
              <span>Gösterilen</span>
            </div>
            <div>
              <strong>{personCount.toLocaleString('tr-TR')}</strong>
              <span>Personel</span>
            </div>
            <div>
              <strong>{typeCount}</strong>
              <span>Hareket türü</span>
            </div>
          </div>
          <div className="report-hero-actions">
            <button type="button" className="btn-secondary" onClick={() => void load()} disabled={loading}>
              Yenile
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => void onExport()}
              disabled={exporting || loading}
            >
              {exporting ? 'İndiriliyor…' : 'Excel'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={onPrint}
              disabled={loading || !result?.items.length}
            >
              Yazdır
            </button>
          </div>
        </div>
      </header>

      {error ? (
        <div className="panel form-error" role="alert">
          {error}
        </div>
      ) : null}

      <section className="panel mv-panel">
        <div className="mv-filters">
          <label className="mv-search">
            <span className="sr-only">Ara</span>
            <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden>
              <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
              <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') applySearch()
              }}
              placeholder="Ad, sicil veya neden ara…"
            />
          </label>
          <select
            value={unitId}
            aria-label="Birim"
            onChange={(e) => {
              setUnitId(e.target.value)
              setFacilityId('')
            }}
          >
            <option value="">Tüm birimler</option>
            {(options?.units ?? []).map((u) => (
              <option key={u.id} value={u.id}>
                {u.name}
              </option>
            ))}
          </select>
          <select
            value={facilityId}
            aria-label="Tesis"
            onChange={(e) => setFacilityId(e.target.value)}
          >
            <option value="">Tüm tesisler</option>
            {facilities.map((f) => (
              <option key={f.id} value={f.id}>
                {f.name}
              </option>
            ))}
          </select>
          <select
            value={movementType === '' ? '' : String(movementType)}
            aria-label="Hareket türü"
            onChange={(e) => setMovementType(e.target.value ? Number(e.target.value) : '')}
          >
            <option value="">Tüm hareketler</option>
            {types.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
          <label className="mv-date-field">
            <span>Başlangıç</span>
            <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label className="mv-date-field">
            <span>Bitiş</span>
            <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
          <div className="mv-filter-actions">
            <button type="button" className="btn-primary" onClick={applySearch}>
              Filtrele
            </button>
            {hasActiveFilters ? (
              <button type="button" className="btn-ghost" onClick={clearFilters}>
                Temizle
              </button>
            ) : null}
          </div>
        </div>

        {stats.length > 0 ? (
          <div className="mv-type-chips" role="group" aria-label="Hareket türü özeti">
            <button
              type="button"
              className={`mv-chip ${movementType === '' ? 'is-active' : ''}`}
              onClick={() => setMovementType('')}
            >
              Tümü <em>{total.toLocaleString('tr-TR')}</em>
            </button>
            {stats.map((s) => (
              <button
                key={s.movementType}
                type="button"
                className={`mv-chip tone-${movementTone(s.movementType)} ${
                  movementType === s.movementType ? 'is-active' : ''
                }`}
                onClick={() =>
                  setMovementType((prev) => (prev === s.movementType ? '' : s.movementType))
                }
              >
                {s.label} <em>{s.count.toLocaleString('tr-TR')}</em>
              </button>
            ))}
          </div>
        ) : null}

        {!result && loading ? (
          <p className="muted">Kayıtlar yükleniyor…</p>
        ) : !result?.items.length ? (
          <div className="mv-empty">
            <strong>Kayıt bulunamadı</strong>
            <p className="muted">Filtreleri gevşetin veya personel kartından yeni hareket ekleyin.</p>
          </div>
        ) : (
          <div className={`table-wrap mv-table-wrap${loading ? ' is-refreshing' : ''}`}>
            <table className="data-table mv-table">
              <thead>
                <tr>
                  <th>Tarih</th>
                  <th>Personel</th>
                  <th>Hareket</th>
                  <th>Değişiklik</th>
                  <th>Neden</th>
                  <th>Onay</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((item) => (
                  <tr key={item.id}>
                    <td className="mv-date">
                      <strong>{formatDate(item.startDate)}</strong>
                      {item.endDate ? <small>→ {formatDate(item.endDate)}</small> : null}
                    </td>
                    <td>
                      <Link className="mv-person-link" to={`/employees/${item.employeeId}`}>
                        {item.employeeName}
                      </Link>
                      <div className="mv-meta">
                        {item.employeeNumber ? <span>Sicil: {item.employeeNumber}</span> : null}
                        {item.currentUnitName ? <span>{item.currentUnitName}</span> : null}
                      </div>
                    </td>
                    <td>
                      <span className={`mv-type-pill tone-${movementTone(item.movementType)}`}>
                        {item.movementTypeLabel}
                      </span>
                    </td>
                    <td className="mv-change">{summarizeChange(item)}</td>
                    <td className="mv-reason">{item.reason?.trim() || '—'}</td>
                    <td className="mv-meta-col">
                      {item.approvedBy?.trim() || '—'}
                      {item.createdBy ? <small>{item.createdBy}</small> : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {result && result.totalCount > result.returnedCount ? (
          <p className="muted mv-limit-note">
            İlk {result.returnedCount.toLocaleString('tr-TR')} kayıt gösteriliyor. Tam liste için
            Excel indirin.
          </p>
        ) : null}
      </section>
    </div>
  )
}
