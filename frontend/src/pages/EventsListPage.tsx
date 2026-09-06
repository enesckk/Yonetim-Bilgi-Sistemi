import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  deleteEvent,
  EVENT_STATUSES,
  fetchEvents,
  type EventListItem,
  type EventStatus,
} from '@/api/eventsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useAlert, useConfirm } from '@/components/ConfirmDialog'
import {
  downloadEventsCsv,
  downloadEventsPdf,
  endOfDay,
  endOfWeek,
  startOfDay,
  startOfWeek,
  toDateInput,
  toIso,
} from '@/lib/eventsDates'

type SortKey = 'startAsc' | 'startDesc' | 'titleAsc'
type Preset = '' | 'today' | 'week'

const PAGE_SIZE = 15

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function presetRange(preset: Preset): { from?: string; to?: string } {
  if (preset === 'today') return { from: toIso(startOfDay()), to: toIso(endOfDay()) }
  if (preset === 'week') return { from: toIso(startOfWeek()), to: toIso(endOfWeek()) }
  return {}
}

export function EventsListPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const confirm = useConfirm()
  const alert = useAlert()
  const [searchParams, setSearchParams] = useSearchParams()

  const initialPreset = (searchParams.get('preset') as Preset) || ''
  const [items, setItems] = useState<EventListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState(() => searchParams.get('search') ?? '')
  const [status, setStatus] = useState<EventStatus | ''>('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [preset, setPreset] = useState<Preset>(initialPreset)
  const [sort, setSort] = useState<SortKey>('startDesc')
  const [page, setPage] = useState(1)

  useEffect(() => {
    const q = searchParams.get('search')
    if (q != null) setSearch(q)
    const p = (searchParams.get('preset') as Preset) || ''
    if (p === 'today' || p === 'week') {
      setPreset(p)
      const r = presetRange(p)
      setFromDate(r.from ? toDateInput(new Date(r.from)) : '')
      setToDate(r.to ? toDateInput(new Date(r.to)) : '')
    }
  }, [searchParams])

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      const range =
        preset === 'today' || preset === 'week'
          ? presetRange(preset)
          : {
              from: fromDate ? toIso(startOfDay(new Date(fromDate))) : undefined,
              to: toDate ? toIso(endOfDay(new Date(toDate))) : undefined,
            }
      const data = await fetchEvents({
        search: search.trim() || undefined,
        status: status === '' ? undefined : status,
        fromUtc: range.from,
        toUtc: range.to,
      })
      setItems(data.items)
      setPage(1)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Etkinlikler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canView, fromDate, preset, search, status, toDate])

  useEffect(() => {
    void load()
  }, [load])

  const sorted = useMemo(() => {
    const copy = [...items]
    if (sort === 'startAsc') copy.sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc))
    else if (sort === 'startDesc') copy.sort((a, b) => +new Date(b.startAtUtc) - +new Date(a.startAtUtc))
    else copy.sort((a, b) => a.title.localeCompare(b.title, 'tr'))
    return copy
  }, [items, sort])

  const totalPages = Math.max(1, Math.ceil(sorted.length / PAGE_SIZE))
  const pageItems = sorted.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE)

  const filteredHint = useMemo(() => {
    if (loading) return 'Yükleniyor…'
    if (sorted.length === 0) return 'Kayıt bulunamadı'
    return `${sorted.length} kayıt`
  }, [loading, sorted.length])

  async function onDelete(item: EventListItem) {
    const ok = await confirm({
      title: 'Etkinliği sil',
      message: `"${item.title}" silinsin mi?`,
      confirmLabel: 'Sil',
      tone: 'danger',
    })
    if (!ok) return
    try {
      await deleteEvent(item.id)
      await load()
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Silinemedi.',
        tone: 'warning',
      })
    }
  }

  function onSearch(e: FormEvent) {
    e.preventDefault()
    setPreset('')
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      next.delete('preset')
      if (search.trim()) next.set('search', search.trim())
      else next.delete('search')
      return next
    })
    void load()
  }

  function applyPreset(next: Preset) {
    setPreset(next)
    setSearchParams((prev) => {
      const p = new URLSearchParams(prev)
      if (next) p.set('preset', next)
      else p.delete('preset')
      return p
    })
    if (next) {
      const r = presetRange(next)
      setFromDate(r.from ? toDateInput(new Date(r.from)) : '')
      setToDate(r.to ? toDateInput(new Date(r.to)) : '')
    } else {
      setFromDate('')
      setToDate('')
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Etkinlikleri görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-page employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Etkinlikler</h2>
              <div className="stat-chip mobile-inline-chip">{filteredHint}</div>
            </div>
            <p className="muted small employees-toolbar-lead">
              Tarih, durum ve arama ile filtreleyin; CSV veya PDF olarak dışa aktarın.
            </p>
          </div>

          <div className="employees-toolbar-actions">
            <div className="stat-chip desktop-inline-chip">{filteredHint}</div>
            <div className="export-actions" role="group" aria-label="Dışa aktarım">
              <button
                type="button"
                className="btn-export btn-export-excel"
                disabled={loading || sorted.length === 0}
                onClick={() => downloadEventsCsv(sorted)}
                title="Filtrelenmiş listeyi Excel/CSV olarak indir"
              >
                <span className="label-full">Excel indir</span>
                <span className="label-short">Excel</span>
              </button>
              <button
                type="button"
                className="btn-export btn-export-pdf"
                disabled={loading || sorted.length === 0}
                onClick={() => void downloadEventsPdf(sorted)}
                title="Filtrelenmiş listeyi PDF olarak indir"
              >
                <span className="label-full">PDF indir</span>
                <span className="label-short">PDF</span>
              </button>
            </div>
            <Link to="/events/calendar" className="btn-secondary">
              Takvim
            </Link>
            {canManage ? (
              <>
                <Link to="/events/import" className="btn-secondary">
                  CSV aktar
                </Link>
                <Link to="/events/new" className="btn-primary">
                  <span className="label-full">+ Yeni etkinlik</span>
                  <span className="label-short">+ Yeni</span>
                </Link>
              </>
            ) : null}
          </div>
        </div>

        <form className="employees-filters events-filters" onSubmit={onSearch}>
          <div className="events-filters-top">
            <div className="events-segment" role="group" aria-label="Hızlı dönem">
              <button
                type="button"
                className={preset === '' && !fromDate && !toDate ? 'is-active' : ''}
                onClick={() => {
                  setPreset('')
                  setFromDate('')
                  setToDate('')
                  setSearchParams((prev) => {
                    const p = new URLSearchParams(prev)
                    p.delete('preset')
                    return p
                  })
                }}
              >
                Tümü
              </button>
              <button
                type="button"
                className={preset === 'today' ? 'is-active' : ''}
                onClick={() => applyPreset(preset === 'today' ? '' : 'today')}
              >
                Bugün
              </button>
              <button
                type="button"
                className={preset === 'week' ? 'is-active' : ''}
                onClick={() => applyPreset(preset === 'week' ? '' : 'week')}
              >
                Bu hafta
              </button>
            </div>
            <div className="employees-filters-search">
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Başlık veya açıklama ara…"
                aria-label="Ara"
              />
              <button type="submit">Ara</button>
            </div>
          </div>
          <div className="employees-filters-row events-filters-row">
            <select
              value={status}
              onChange={(e) => setStatus(e.target.value === '' ? '' : (Number(e.target.value) as EventStatus))}
              aria-label="Durum"
            >
              <option value="">Tüm durumlar</option>
              {EVENT_STATUSES.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
            <input
              type="date"
              value={fromDate}
              onChange={(e) => {
                setPreset('')
                setFromDate(e.target.value)
              }}
              aria-label="Başlangıç tarihi"
            />
            <input
              type="date"
              value={toDate}
              onChange={(e) => {
                setPreset('')
                setToDate(e.target.value)
              }}
              aria-label="Bitiş tarihi"
            />
            <select value={sort} onChange={(e) => setSort(e.target.value as SortKey)} aria-label="Sıralama">
              <option value="startDesc">Tarih (yeni → eski)</option>
              <option value="startAsc">Tarih (eski → yeni)</option>
              <option value="titleAsc">Başlık (A → Z)</option>
            </select>
            <div className="employees-filters-actions">
              <button
                type="button"
                className="btn-secondary"
                onClick={() => {
                  setSearch('')
                  setStatus('')
                  setFromDate('')
                  setToDate('')
                  setPreset('')
                  setSort('startDesc')
                  setSearchParams({})
                }}
              >
                Temizle
              </button>
            </div>
          </div>
        </form>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="employees-table-wrap">
          <table className="data-table employees-table">
            <thead>
              <tr>
                <th>Başlık</th>
                <th>Durum</th>
                <th>Başlangıç</th>
                <th>Tesis / konum</th>
                <th>Katılımcı</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {pageItems.length === 0 ? (
                <tr>
                  <td colSpan={6} className="muted">
                    {loading ? 'Yükleniyor…' : 'Kayıt bulunamadı.'}
                  </td>
                </tr>
              ) : (
                pageItems.map((item) => (
                  <tr key={item.id}>
                    <td>
                      <Link to={`/events/${item.id}`} className="events-title-link">
                        {item.title}
                      </Link>
                      {item.recurrenceLabel ? (
                        <span className="events-series-chip">{item.recurrenceLabel}</span>
                      ) : null}
                    </td>
                    <td>
                      <span className={`event-status-pill status-${item.status}`}>{item.statusLabel}</span>
                    </td>
                    <td>{formatWhen(item.startAtUtc)}</td>
                    <td>{item.facilityName || item.address || '—'}</td>
                    <td>{item.expectedAttendees != null ? item.expectedAttendees.toLocaleString('tr-TR') : '—'}</td>
                    <td>
                      <div className="events-action-group" role="group" aria-label="İşlemler">
                        <Link to={`/events/${item.id}`} className="events-action-btn" title="Detay">
                          Detay
                        </Link>
                        {canManage ? (
                          <>
                            <Link
                              to={`/events/${item.id}/edit`}
                              className="events-action-btn"
                              title="Düzenle"
                            >
                              Düzenle
                            </Link>
                            <button
                              type="button"
                              className="events-action-btn is-danger"
                              title="Sil"
                              onClick={() => void onDelete(item)}
                            >
                              Sil
                            </button>
                          </>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {sorted.length > PAGE_SIZE ? (
          <div className="events-pagination">
            <button
              type="button"
              className="btn-secondary"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Önceki
            </button>
            <span className="muted small">
              Sayfa {page} / {totalPages}
            </span>
            <button
              type="button"
              className="btn-secondary"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              Sonraki
            </button>
          </div>
        ) : null}
      </section>
    </div>
  )
}
