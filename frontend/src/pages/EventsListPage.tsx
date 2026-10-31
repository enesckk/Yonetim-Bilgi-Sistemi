import { groupEvents, recordedAttendance } from '@/lib/eventGroups'
import { EventYearOverview } from '@/components/EventYearOverview'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  eventPhase,
  eventPhaseLabel,
  fetchEvents,
  type EventListItem,
  type EventPhase,
} from '@/api/eventsApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { canImportEvents, isSystemAdmin } from '@/auth/roles'
import {
  downloadEventsCsv,
  downloadEventsPdf,
  eventDateRange,
  formatEventWhen,
  startOfDay,
  endOfDay,
  startOfWeek,
  endOfWeek,
  toDateInput,
  toIso,
} from '@/lib/eventsDates'

const PAGE_SIZE = 15
const categories = [
  ['trip', 'Gezi'],
  ['culture', 'Kültür / Sanat'],
  ['education', 'Eğitim'],
  ['health', 'Sağlık'],
  ['social_support', 'Sosyal Destek'],
  ['sports', 'Spor'],
  ['youth', 'Çocuk / Gençlik'],
  ['women', 'Kadın'],
  ['elderly', 'Yaşlı'],
  ['other', 'Diğer'],
]
type Preset = '' | 'today' | 'week'
function rangeFor(p: Preset) {
  return p === 'today'
    ? { from: toIso(startOfDay()), to: toIso(endOfDay()) }
    : p === 'week'
      ? { from: toIso(startOfWeek()), to: toIso(endOfWeek()) }
      : ({} as { from?: string; to?: string })
}
export function EventsListPage() {
  const { user, hasPermission } = useAuth()
  const admin = isSystemAdmin(user)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const canView = hasPermission(PermissionCodes.EventsView)
  const [params, setParams] = useSearchParams()
  const kind =
    params.get('kind') === 'project' ? 'project' : params.get('kind') === 'event' ? 'event' : ''
  const category = params.get('category') ?? ''
  const [items, setItems] = useState<EventListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState(params.get('search') ?? '')
  const [query, setQuery] = useState(params.get('search') ?? '')
  const [phase, setPhase] = useState<EventPhase | ''>('done')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [preset, setPreset] = useState<Preset>('')
  const [sort, setSort] = useState('startDesc')
  const [page, setPage] = useState(1)
  const sequence = useRef(0)
  useEffect(() => {
    const s = params.get('search') ?? ''
    setSearch(s)
    setQuery(s)
    const p = params.get('preset')
    if (p === 'today' || p === 'week') {
      setPreset(p)
      const r = rangeFor(p)
      setFrom(r.from ? toDateInput(new Date(r.from)) : '')
      setTo(r.to ? toDateInput(new Date(r.to)) : '')
    }
  }, [params])
  const load = useCallback(async () => {
    if (!canView) return
    const current = ++sequence.current
    if (from && to && from > to) {
      setError('Bitiş tarihi başlangıç tarihinden önce olamaz.')
      setItems([])
      setLoading(false)
      return
    }
    setLoading(true)
    setError(null)
    try {
      const r = preset
        ? rangeFor(preset)
        : { from: eventDateRange(from, to).fromUtc, to: eventDateRange(from, to).toUtc }
      const d = await fetchEvents({
        kind: kind || undefined,
        category: category || undefined,
        search: query || undefined,
        fromUtc: r.from,
        toUtc: r.to,
      })
      if (current === sequence.current) {
        setItems(d.items)
        setPage(1)
      }
    } catch (e) {
      if (current === sequence.current)
        setError(e instanceof Error ? e.message : 'Kayıtlar yüklenemedi.')
    } finally {
      if (current === sequence.current) setLoading(false)
    }
  }, [canView, from, to, preset, kind, category, query])
  useEffect(() => {
    void load()
  }, [load])
  useEffect(() => setPage(1), [phase, sort])
  const scoped = useMemo(
    () =>
      items.filter((e) =>
        kind === 'project'
          ? e.category === 'project'
          : kind === 'event'
            ? e.category !== 'project'
            : true,
      ),
    [items, kind],
  )
  const upcoming = useMemo(
    () =>
      scoped
        .filter((e) => eventPhase(e.status, e.startAtUtc) === 'planned')
        .sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc)),
    [scoped],
  )
  const sorted = useMemo(() => {
    const rows = scoped.filter((e) => !phase || eventPhase(e.status, e.startAtUtc) === phase)
    return rows.sort((a, b) =>
      sort === 'titleAsc'
        ? a.title.localeCompare(b.title, 'tr')
        : sort === 'startAsc'
          ? Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc)
          : Date.parse(b.startAtUtc) - Date.parse(a.startAtUtc),
    )
  }, [scoped, phase, sort])
  const groups = useMemo(() => groupEvents(sorted), [sorted])
  const pages = Math.max(1, Math.ceil(groups.length / PAGE_SIZE))
  const shown = groups.slice((Math.min(page, pages) - 1) * PAGE_SIZE, Math.min(page, pages) * PAGE_SIZE)
  function setKind(next: string) {
    setParams((p) => {
      const n = new URLSearchParams(p)
      next ? n.set('kind', next) : n.delete('kind')
      n.delete('category')
      return n
    })
  }
  function submit(e: FormEvent) {
    e.preventDefault()
    setQuery(search.trim())
    setParams((p) => {
      const n = new URLSearchParams(p)
      search.trim() ? n.set('search', search.trim()) : n.delete('search')
      return n
    })
  }
  function applyPreset(next: Preset) {
    setPreset(next)
    const r = rangeFor(next)
    setFrom(r.from ? toDateInput(new Date(r.from)) : '')
    setTo(r.to ? toDateInput(new Date(r.to)) : '')
    setParams((p) => {
      const n = new URLSearchParams(p)
      next ? n.set('preset', next) : n.delete('preset')
      return n
    })
  }
  function reset() {
    setSearch('')
    setQuery('')
    setPhase('done')
    setFrom('')
    setTo('')
    setPreset('')
    setSort('startDesc')
    setParams(kind ? { kind } : {})
  }
  if (!canView)
    return (
      <section className="panel">
        <p className="form-error">Bu kayıtları görüntüleme yetkiniz yok.</p>
      </section>
    )
  return (
    <div className="events-page events-catalog-page">
      <section className="panel events-catalog-header">
        <div>
          <p className="information-notes-eyebrow">Etkinlik yönetimi</p>
          <h1>Etkinlik ve projeler</h1>
          <p className="muted">
            Yaklaşan çalışmaları takip edin, tamamlanan kayıtları ve bilgi notlarını inceleyin.
          </p>
        </div>
        <div className="events-catalog-actions">
          {admin && (
            <>
              <button
                className="btn-secondary"
                disabled={loading || !sorted.length}
                onClick={() => downloadEventsCsv(sorted)}
              >
                Excel indir
              </button>
              <button
                className="btn-secondary"
                disabled={loading || !sorted.length}
                onClick={() => void downloadEventsPdf(sorted)}
              >
                PDF indir
              </button>
            </>
          )}
          {canManage && (
            <>
              <Link className="btn-secondary" to="/events/new?kind=project">
                + Proje ekle
              </Link>
              <Link className="btn-primary" to="/events/new">
                + Etkinlik ekle
              </Link>
            </>
          )}
        </div>
      </section>
      <EventYearOverview />
      <nav className="events-kind-tabs" aria-label="Kayıt türü">
        {[
          ['', 'Tümü'],
          ['event', 'Etkinlik ve geziler'],
          ['project', 'Projeler'],
        ].map(([v, label]) => (
          <button
            type="button"
            key={v}
            className={kind === v ? 'is-active' : ''}
            aria-pressed={kind === v}
            onClick={() => setKind(v)}
          >
            {label}
          </button>
        ))}
      </nav>
      <section className="panel events-upcoming">
        <header>
          <h2>Yaklaşanlar</h2>
          <span>{loading ? 'Yükleniyor…' : `${upcoming.length} kayıt`}</span>
        </header>
        {upcoming.length ? (
          <div className="events-upcoming-grid" role="region" aria-label="Yaklaşan kayıtlar, yatay kaydırılabilir" tabIndex={0}>
            {upcoming.slice(0, 3).map((e) => (
              <Link to={'/events/' + e.id} key={e.id}>
                <small>{e.category === 'project' ? 'Proje' : e.categoryLabel || 'Etkinlik'}</small>
                <strong>{e.title}</strong>
                <span>{formatEventWhen(e.startAtUtc, e.isDateOnly)}</span>
                <span>{e.facilityName || e.address || 'Konum belirtilmemiş'}</span>
              </Link>
            ))}
          </div>
        ) : (
          <p className="muted small">
            {loading
              ? 'Kayıtlar kontrol ediliyor…'
              : 'Seçili filtrelerde yaklaşan kayıt bulunmuyor.'}
          </p>
        )}
        {upcoming.length > 3 && (
          <button className="btn-link" onClick={() => setPhase('planned')}>
            Yaklaşanların tümünü göster ›
          </button>
        )}
      </section>
      <section className="panel events-history">
        <header className="events-history-head">
          <div>
            <h2>
              {phase === 'done'
                ? 'Tamamlananlar'
                : phase === 'planned'
                  ? 'Yaklaşan kayıtlar'
                  : 'Tüm kayıtlar'}
            </h2>
            <p className="muted small">
              {loading ? 'Yükleniyor…' : `${groups.length.toLocaleString('tr-TR')} grup · ${sorted.length.toLocaleString('tr-TR')} kayıt`}
            </p>
          </div>
          <div className="events-segment" role="group" aria-label="Kayıt durumu">
            {[
              ['done', 'Tamamlanan'],
              ['planned', 'Yaklaşan'],
              ['', 'Tümü'],
            ].map(([v, label]) => (
              <button
                type="button"
                key={v}
                className={phase === v ? 'is-active' : ''}
                onClick={() => setPhase(v as EventPhase | '')}
              >
                {label}
              </button>
            ))}
          </div>
        </header>
        <form className="events-catalog-search" onSubmit={submit}>
          <input
            aria-label="Ara"
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Başlık veya açıklama ara…"
          />
          <button type="submit" className="btn-primary">
            Ara
          </button>
          {canManage && canImportEvents(user) && (
            <Link className="btn-secondary" to="/events/import">
              Excel yükle
            </Link>
          )}
        </form>
        <details className="events-advanced-filters">
          <summary>Tarih ve tür filtreleri</summary>
          <div className="events-advanced-fields">
            {kind !== 'project' && (
              <label>
                Tür
                <select
                  aria-label="Tür"
                  value={category}
                  onChange={(e) =>
                    setParams((p) => {
                      const n = new URLSearchParams(p)
                      e.target.value ? n.set('category', e.target.value) : n.delete('category')
                      return n
                    })
                  }
                >
                  <option value="">Tüm türler</option>
                  {categories.map(([v, l]) => (
                    <option key={v} value={v}>
                      {l}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label>
              Başlangıç tarihi
              <input
                aria-label="Başlangıç tarihi"
                type="date"
                value={from}
                max={to || undefined}
                onChange={(e) => {
                  setPreset('')
                  setFrom(e.target.value)
                  setParams((p) => {
                    const n = new URLSearchParams(p)
                    n.delete('preset')
                    return n
                  })
                }}
              />
            </label>
            <label>
              Bitiş tarihi
              <input
                aria-label="Bitiş tarihi"
                type="date"
                value={to}
                min={from || undefined}
                onChange={(e) => {
                  setPreset('')
                  setTo(e.target.value)
                  setParams((p) => {
                    const n = new URLSearchParams(p)
                    n.delete('preset')
                    return n
                  })
                }}
              />
            </label>
            <label>
              Sıralama
              <select aria-label="Sıralama" value={sort} onChange={(e) => setSort(e.target.value)}>
                <option value="startDesc">Tarih (yeni → eski)</option>
                <option value="startAsc">Tarih (eski → yeni)</option>
                <option value="titleAsc">Başlık (A → Z)</option>
              </select>
            </label>
            <div className="events-segment" role="group" aria-label="Hızlı dönem">
              <button
                type="button"
                className={!preset && !from && !to ? 'is-active' : ''}
                onClick={() => applyPreset('')}
              >
                Tümü
              </button>
              <button
                type="button"
                className={preset === 'today' ? 'is-active' : ''}
                onClick={() => applyPreset('today')}
              >
                Bugün
              </button>
              <button
                type="button"
                className={preset === 'week' ? 'is-active' : ''}
                onClick={() => applyPreset('week')}
              >
                Bu hafta
              </button>
            </div>
            <button type="button" className="btn-secondary" onClick={reset}>
              Temizle
            </button>
          </div>
        </details>
        {error && (
          <p className="form-error" role="alert">
            {error}
            <button className="btn-link" onClick={() => void load()}>
              Yeniden dene
            </button>
          </p>
        )}
        <div className="event-group-list" aria-label="Etkinlik ve proje grupları" aria-busy={loading}>
          {loading ? <p className="muted" role="status">Yükleniyor…</p> : !shown.length ? <p className="muted">Kayıt bulunamadı.</p> : shown.map((group) => (
            <details className="event-group-card" key={group.key}>
              <summary>
                <div className="event-group-copy">
                  <span className="event-group-category">{group.categoryLabel}</span>
                  <h3>{group.title} <span>{group.year}</span></h3>
                  <div className="event-group-metrics">
                    <span>{group.items.length.toLocaleString('tr-TR')} {group.categoryLabel === 'Proje' ? 'proje kaydı' : 'etkinlik'}</span>
                    <span className="event-group-attendance">{group.recordsWithAttendance ? `${group.attendance.toLocaleString('tr-TR')} kayıtlı katılım` : 'Katılım belirtilmemiş'}</span>
                    {group.recordsWithAttendance > 0 && group.recordsWithAttendance < group.items.length && <small>{group.items.length - group.recordsWithAttendance} kayıtta katılım eksik</small>}
                  </div>
                </div>
                <span className="event-group-expand"><span className="when-closed">Detayları gör</span><span className="when-open">Detayları gizle</span><span aria-hidden="true">⌄</span></span>
              </summary>
              <div className="event-group-records">
                {group.items.map((e) => (
                  <article className="event-group-record" key={e.id}>
                    <div className="event-group-record-body">
                      <Link to={'/events/' + e.id} className="events-title-link">{e.title}</Link>
                      <div className="event-group-record-meta">
                        <span>{formatEventWhen(e.startAtUtc, e.isDateOnly)}</span>
                        <span className={'event-status-pill status-' + e.status}>{eventPhaseLabel(e.status, e.startAtUtc)}</span>
                      </div>
                      <p>{e.settlementNames?.length ? e.settlementNames.join(', ') : e.facilityName || e.address || 'Konum belirtilmemiş'}</p>
                      {e.settlementNames?.length && e.facilityName ? <small>{e.facilityName}</small> : null}
                    </div>
                    <div className="event-group-record-actions">
                      <span className="event-group-record-count">{recordedAttendance(e) !== null ? `${recordedAttendance(e)!.toLocaleString('tr-TR')} kayıtlı katılım` : e.expectedAttendees != null ? `${e.expectedAttendees.toLocaleString('tr-TR')} beklenen katılım` : 'Katılım belirtilmemiş'}</span>
                      <div className="events-action-group">
                        <Link className="events-action-btn" to={'/events/' + e.id}>Detay</Link>
                        {hasPermission(PermissionCodes.EmployeesViewAllUnits) && <Link className="events-action-btn" to={'/information-notes?eventId=' + e.id}>Bilgi notları</Link>}
                      </div>
                    </div>
                  </article>
                ))}
                <p className="event-group-note">Toplam, kayıtlı katılımların toplamıdır; tekil kişi sayısı değildir. Beklenen katılımlar toplama dahil edilmez.</p>
              </div>
            </details>
          ))}
        </div>
        {pages > 1 && (
          <div className="events-pagination">
            <button
              className="btn-secondary"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              Önceki
            </button>
            <span>
              Sayfa {page} / {pages}
            </span>
            <button
              className="btn-secondary"
              disabled={page >= pages}
              onClick={() => setPage((p) => Math.min(pages, p + 1))}
            >
              Sonraki
            </button>
          </div>
        )}
      </section>
    </div>
  )
}
