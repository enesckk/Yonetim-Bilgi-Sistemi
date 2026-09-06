import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  EVENT_STATUSES,
  fetchEventFacilityStats,
  fetchEvents,
  fetchMapPins,
  type EventListItem,
  type FacilityEventStat,
} from '@/api/eventsApi'
import { fetchOrganizationTree, type OrgNode } from '@/api/organizationApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { endOfMonth, startOfMonth } from '@/lib/eventsDates'

const ORG_FACILITY = 6

function flattenFacilities(nodes: OrgNode[], acc: OrgNode[] = []): OrgNode[] {
  for (const n of nodes) {
    if (n.type === ORG_FACILITY) acc.push(n)
    if (n.children?.length) flattenFacilities(n.children, acc)
  }
  return acc
}

function formatFeedWhen(iso: string) {
  const d = new Date(iso)
  const now = new Date()
  const startToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const startEvent = new Date(d.getFullYear(), d.getMonth(), d.getDate())
  const dayDiff = Math.round((startEvent.getTime() - startToday.getTime()) / 86_400_000)
  let relative = d.toLocaleDateString('tr-TR', { weekday: 'short' })
  if (dayDiff === 0) relative = 'Bugün'
  else if (dayDiff === 1) relative = 'Yarın'
  else if (dayDiff > 1 && dayDiff <= 7) relative = 'Bu hafta'

  return {
    day: d.toLocaleDateString('tr-TR', { day: '2-digit' }),
    month: d.toLocaleDateString('tr-TR', { month: 'short' }),
    time: d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
    relative,
  }
}

function StatusChart({ events }: { events: EventListItem[] }) {
  const rows = EVENT_STATUSES.map((s) => ({
    name: s.label,
    count: events.filter((e) => e.status === s.value).length,
  })).filter((r) => r.count > 0)
  const total = rows.reduce((s, r) => s + r.count, 0)
  const max = Math.max(1, ...rows.map((r) => r.count))

  return (
    <article className="dash-chart tone-primary">
      <header className="dash-chart-head">
        <h3>Durum dağılımı</h3>
        {total > 0 ? <span className="dash-chart-total">{total}</span> : null}
      </header>
      {rows.length === 0 ? (
        <div className="events-empty-state">
          <p>Henüz etkinlik yok.</p>
        </div>
      ) : (
        <ul className="dash-bars">
          {rows.map((row, index) => {
            const pct = total > 0 ? Math.round((row.count / total) * 100) : 0
            const width = Math.max(6, Math.round((row.count / max) * 100))
            return (
              <li key={row.name} style={{ animationDelay: `${index * 40}ms` }}>
                <div className="dash-bar-meta">
                  <span className="dash-bar-name">{row.name}</span>
                  <span className="dash-bar-stats">
                    <strong>{row.count}</strong>
                    <span>{pct}%</span>
                  </span>
                </div>
                <div className="dash-bar-track" aria-hidden="true">
                  <div className="dash-bar-fill" style={{ width: `${width}%` }} />
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </article>
  )
}

export function EventsHomePage() {
  const { user, hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const canOrg = hasPermission(PermissionCodes.OrganizationView)

  const [events, setEvents] = useState<EventListItem[]>([])
  const [facilityPins, setFacilityPins] = useState(0)
  const [eventPins, setEventPins] = useState(0)
  const [facilityTotal, setFacilityTotal] = useState(0)
  const [facilityMissing, setFacilityMissing] = useState(0)
  const [facilityStats, setFacilityStats] = useState<FacilityEventStat[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!canView) {
      setLoading(false)
      return
    }
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const tasks: Promise<unknown>[] = [
          fetchEvents(),
          fetchMapPins({ kinds: 'facility,event' }),
          fetchEventFacilityStats(),
        ]
        if (canOrg) tasks.push(fetchOrganizationTree())
        const results = await Promise.all(tasks)
        if (cancelled) return
        const list = results[0] as Awaited<ReturnType<typeof fetchEvents>>
        const pins = results[1] as Awaited<ReturnType<typeof fetchMapPins>>
        const statsResult = results[2] as Awaited<ReturnType<typeof fetchEventFacilityStats>>
        setEvents(list.items)
        setFacilityPins(pins.pins.filter((p) => p.kind === 'facility').length)
        setEventPins(pins.pins.filter((p) => p.kind === 'event').length)
        setFacilityStats(
          [...statsResult.items]
            .sort((a, b) => b.upcomingEvents - a.upcomingEvents || b.totalEvents - a.totalEvents)
            .slice(0, 8),
        )
        if (canOrg && results[3]) {
          const facilities = flattenFacilities(results[3] as OrgNode[])
          setFacilityTotal(facilities.length)
          setFacilityMissing(facilities.filter((f) => f.latitude == null || f.longitude == null).length)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiClientError ? err.message : 'Özet yüklenemedi.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canOrg, canView])

  const stats = useMemo(() => {
    const now = Date.now()
    const weekAhead = now + 7 * 24 * 60 * 60 * 1000
    const thisMonthStart = startOfMonth().getTime()
    const thisMonthEnd = endOfMonth().getTime()

    const upcoming = events.filter((e) => {
      const t = new Date(e.startAtUtc).getTime()
      return t >= now && (e.status === 2 || e.status === 1)
    })
    const thisWeek = upcoming.filter((e) => new Date(e.startAtUtc).getTime() <= weekAhead)
    const today = events.filter((e) => {
      const t = new Date(e.startAtUtc)
      const n = new Date()
      return t.toDateString() === n.toDateString()
    })
    const thisMonth = events.filter((e) => {
      const t = new Date(e.startAtUtc).getTime()
      return t >= thisMonthStart && t <= thisMonthEnd
    })
    const draft = events.filter((e) => e.status === 1).length

    return {
      total: events.length,
      draft,
      upcoming: upcoming.length,
      thisWeek: thisWeek.length,
      today: today.length,
      thisMonth: thisMonth.length,
      upcomingList: [...upcoming]
        .sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc))
        .slice(0, 6),
    }
  }, [events])

  const greeting = useMemo(() => {
    const hour = new Date().getHours()
    if (hour < 12) return 'Günaydın'
    if (hour < 18) return 'İyi günler'
    return 'İyi akşamlar'
  }, [])

  const firstName = user?.displayName?.split(' ')[0] ?? 'Yönetici'

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Etkinlik genel bakışını görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-home dashboard">
      <header className="dash-hero events-home-hero">
        <div className="dash-hero-main">
          <p className="dash-hero-brand">Etkinlik Yönetim Takip</p>
          <h2>Genel bakış</h2>
          <p className="dash-hero-lead">
            <span>
              {greeting}, {firstName}
            </span>
            <span className="dash-hero-sep" aria-hidden="true">
              ·
            </span>
            <span>Şehitkamil odaklı etkinlik ve tesis haritası</span>
          </p>
          {!loading ? (
            <ul className="events-home-meta" aria-label="Özet metrikler">
              <li>
                <span>Yaklaşan</span>
                <strong>{stats.upcoming}</strong>
              </li>
              <li>
                <span>Taslak</span>
                <strong>{stats.draft}</strong>
              </li>
              <li>
                <span>Bu ay</span>
                <strong>{stats.thisMonth}</strong>
              </li>
              <li>
                <span>Harita pin</span>
                <strong>{eventPins + facilityPins}</strong>
              </li>
              {canOrg ? (
                <li className={facilityMissing > 0 ? 'is-warn' : undefined}>
                  <Link to="/events/facilities-locations?missing=1">
                    <span>Konum eksik</span>
                    <strong>
                      {facilityMissing}
                      <em>/{facilityTotal}</em>
                    </strong>
                  </Link>
                </li>
              ) : null}
            </ul>
          ) : null}
        </div>
        {!loading ? (
          <div className="dash-hero-kpis" aria-label="Özet göstergeler">
            <Link to="/events/list" className="dash-kpi is-link is-accent">
              <span>Toplam</span>
              <strong>{stats.total}</strong>
            </Link>
            <Link to="/events/list?preset=today" className="dash-kpi is-link is-ok">
              <span>Bugün</span>
              <strong>{stats.today}</strong>
            </Link>
            <Link to="/events/list?preset=week" className="dash-kpi is-link">
              <span>Bu hafta</span>
              <strong>{stats.thisWeek}</strong>
            </Link>
            <Link to="/events/map" className="dash-kpi is-link">
              <span>Harita</span>
              <strong>{eventPins + facilityPins}</strong>
            </Link>
          </div>
        ) : null}
      </header>

      {error ? (
        <div className="form-error" role="alert">
          {error}
        </div>
      ) : null}

      {loading ? <p className="panel muted">Özet yükleniyor…</p> : null}

      {!loading ? (
        <section className="events-home-main panel" aria-label="Etkinlik özeti">
          <div className="events-home-main-grid">
            <StatusChart events={events} />

            <div className="events-home-upcoming">
              <header className="events-home-section-head">
                <div>
                  <h3>Yaklaşan etkinlikler</h3>
                  <p>
                    {stats.upcomingList.length > 0
                      ? `${stats.upcoming} yaklaşan · en yakın ${stats.upcomingList.length} kayıt`
                      : 'En yakın tarihli kayıtlar'}
                  </p>
                </div>
                <Link to="/events/list" className="btn-link">
                  Tümü ›
                </Link>
              </header>
              {stats.upcomingList.length === 0 ? (
                <div className="events-empty-state">
                  <p>Yaklaşan etkinlik yok.</p>
                  {canManage ? (
                    <Link to="/events/new" className="btn-primary">
                      İlk etkinliği oluştur
                    </Link>
                  ) : null}
                </div>
              ) : (
                <ul className="events-home-feed">
                  {stats.upcomingList.map((e) => {
                    const when = formatFeedWhen(e.startAtUtc)
                    return (
                      <li key={e.id}>
                        <Link to={`/events/${e.id}`}>
                          <span className="events-home-feed-when" aria-hidden="true">
                            <strong>{when.day}</strong>
                            <em>{when.month}</em>
                          </span>
                          <span className="events-home-feed-body">
                            <strong>{e.title}</strong>
                            <em>
                              <span>{when.relative}</span>
                              <span>{when.time}</span>
                              {e.facilityName ? <span>{e.facilityName}</span> : null}
                            </em>
                          </span>
                          <span className="events-home-feed-side">
                            <span className={`event-status-pill status-${e.status}`}>{e.statusLabel}</span>
                            <span className="events-home-feed-chevron" aria-hidden="true">
                              ›
                            </span>
                          </span>
                        </Link>
                      </li>
                    )
                  })}
                </ul>
              )}
            </div>
          </div>

          {facilityStats.length > 0 ? (
            <div className="events-home-facility-stats">
              <header className="events-home-section-head">
                <div>
                  <h3>Tesis etkinlik özeti</h3>
                  <p>Yaklaşan ve toplam kayıtlar</p>
                </div>
                <Link to="/events/list" className="btn-link">
                  Liste ›
                </Link>
              </header>
              <table className="events-facility-stats-table">
                <thead>
                  <tr>
                    <th>Tesis</th>
                    <th>Yaklaşan</th>
                    <th>Toplam</th>
                  </tr>
                </thead>
                <tbody>
                  {facilityStats.map((row) => (
                    <tr key={row.facilityId}>
                      <td>{row.facilityName}</td>
                      <td>{row.upcomingEvents}</td>
                      <td>{row.totalEvents}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}

          <footer className="events-home-rail">
            <span className="events-home-rail-label">Hızlı erişim</span>
            <nav className="events-home-rail-links" aria-label="Hızlı erişim">
              <Link to="/events/list?preset=today">
                <strong>Bugün</strong>
                <span>{stats.today}</span>
              </Link>
              <Link to="/events/list?preset=week">
                <strong>Bu hafta</strong>
                <span>{stats.thisWeek}</span>
              </Link>
              <Link to="/events/calendar">
                <strong>Takvim</strong>
                <span>Aylık</span>
              </Link>
              <Link to="/events/map">
                <strong>Harita</strong>
                <span>Pinler</span>
              </Link>
              <Link to="/events/list">
                <strong>Liste</strong>
                <span>Tümü</span>
              </Link>
              <Link to="/events/facilities-locations">
                <strong>Tesisler</strong>
                <span>{canOrg && facilityMissing > 0 ? `${facilityMissing} eksik` : 'Konum'}</span>
              </Link>
              {canManage ? (
                <Link to="/events/new" className="is-primary">
                  <strong>Yeni</strong>
                  <span>Oluştur</span>
                </Link>
              ) : null}
            </nav>
          </footer>
        </section>
      ) : null}
    </div>
  )
}
