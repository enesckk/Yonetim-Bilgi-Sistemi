import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  eventPhase,
  eventPhaseLabel,
  fetchEvents,
  type EventListItem,
} from '@/api/eventsApi'
import { fetchOrganizationTree } from '@/api/organizationApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { eventVenueCatalog, matchesVenue, venueCards, type VenueCard } from '@/lib/eventVenues'

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Europe/Istanbul',
  })
}

function splitEvents(items: EventListItem[]) {
  const now = Date.now()
  const upcoming: EventListItem[] = []
  const past: EventListItem[] = []
  for (const e of items) {
    if (e.status === 3) continue
    if (e.status !== 4 && new Date(e.startAtUtc).getTime() >= now) upcoming.push(e)
    else past.push(e)
  }
  upcoming.sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc))
  past.sort((a, b) => +new Date(b.startAtUtc) - +new Date(a.startAtUtc))
  return { upcoming, past }
}

export function EventsFacilitiesPage() {
  const { hasPermission } = useAuth()
  const canView =
    hasPermission(PermissionCodes.EventsView) || hasPermission(PermissionCodes.OrganizationView)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const [searchParams, setSearchParams] = useSearchParams()

  const [cards, setCards] = useState<VenueCard[]>([])
  const [events, setEvents] = useState<EventListItem[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const selectedId = searchParams.get('facility') ?? ''

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      const [tree, list] = await Promise.all([fetchOrganizationTree(), fetchEvents()])
      setCards(venueCards(eventVenueCatalog(tree)))
      setEvents(list.items.filter((e) => e.status !== 3))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Tesis programı yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canView])

  useEffect(() => {
    void load()
  }, [load])

  const visible = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr-TR')
    if (!q) return cards
    return cards.filter(
      (c) =>
        c.name.toLocaleLowerCase('tr-TR').includes(q) ||
        c.subtitle.toLocaleLowerCase('tr-TR').includes(q),
    )
  }, [cards, search])

  const selected = visible.find((c) => c.id === selectedId) ?? visible[0] ?? null

  useEffect(() => {
    if (!selected || selectedId === selected.id) return
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      next.set('facility', selected.id)
      return next
    }, { replace: true })
  }, [selected, selectedId, setSearchParams])

  const grouped = useMemo(() => {
    if (!selected) return { upcoming: [] as EventListItem[], past: [] as EventListItem[] }
    return splitEvents(events.filter((e) => matchesVenue(e.facilityId, selected.ids)))
  }, [events, selected])

  const summaryById = useMemo(() => {
    const now = Date.now()
    const map = new Map<string, { total: number; upcoming: number; last?: EventListItem; next?: EventListItem }>()
    for (const card of cards) {
      const mine = events.filter((e) => matchesVenue(e.facilityId, card.ids))
      const upcoming = mine
        .filter((e) => e.status !== 4 && new Date(e.startAtUtc).getTime() >= now)
        .sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc))
      const past = mine
        .filter((e) => e.status === 4 || new Date(e.startAtUtc).getTime() < now)
        .sort((a, b) => +new Date(b.startAtUtc) - +new Date(a.startAtUtc))
      map.set(card.id, {
        total: mine.length,
        upcoming: upcoming.length,
        next: upcoming[0],
        last: past[0],
      })
    }
    return map
  }, [cards, events])

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Tesis programını görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-page employees-page events-venue-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Tesis programı</h2>
              <div className="stat-chip mobile-inline-chip">
                {loading ? '…' : `${visible.length} tesis`}
              </div>
            </div>
            <p className="muted small employees-toolbar-lead">
              Tesise girin; yapılan ve yaklaşan etkinlikleri burada görün.
            </p>
          </div>
        </div>

        <form className="settlement-search" role="search" onSubmit={(e) => e.preventDefault()}>
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tesis ara…"
            aria-label="Tesis ara"
          />
        </form>

        {error ? <p className="form-error">{error}</p> : null}
        {loading ? (
          <div className="ui-skeleton-stack">
            <span className="ui-skeleton" />
            <span className="ui-skeleton" />
          </div>
        ) : visible.length === 0 ? (
          <p className="muted">Gösterilecek tesis yok.</p>
        ) : (
          <div className="events-venue-layout">
            <ul className="events-venue-list">
              {visible.map((item) => {
                const row = summaryById.get(item.id)
                const on = selected?.id === item.id
                return (
                  <li key={item.id}>
                    <button
                      type="button"
                      className={`events-venue-card${on ? ' is-on' : ''}`}
                      onClick={() =>
                        setSearchParams((prev) => {
                          const next = new URLSearchParams(prev)
                          next.set('facility', item.id)
                          return next
                        })
                      }
                    >
                      <strong>{item.name}</strong>
                      {item.subtitle ? <span>{item.subtitle}</span> : null}
                      <em>
                        {row?.total ? `${row.total} etkinlik` : 'Kayıt yok'}
                        {row?.upcoming ? ` · ${row.upcoming} yaklaşan` : ''}
                      </em>
                    </button>
                  </li>
                )
              })}
            </ul>

            {selected ? (
              <div className="events-venue-detail">
                <header className="events-venue-detail-head">
                  <div>
                    <h3>{selected.name}</h3>
                    {selected.subtitle ? <p className="muted small">{selected.subtitle}</p> : null}
                  </div>
                  <div className="events-venue-detail-actions">
                    <Link to={`/events/calendar?facility=${encodeURIComponent(selected.id)}`} className="btn-secondary">
                      Takvim
                    </Link>
                    {canManage ? (
                      <Link
                        to={`/events/new?facility=${encodeURIComponent(selected.id)}`}
                        className="btn-primary"
                      >
                        Etkinlik ekle
                      </Link>
                    ) : null}
                  </div>
                </header>

                {(() => {
                  const row = summaryById.get(selected.id)
                  return (
                    <p className="events-venue-kicker">
                      {row?.next ? `Sıradaki: ${row.next.title}` : 'Yaklaşan etkinlik yok'}
                      {row?.last ? ` · Son: ${row.last.title}` : ''}
                    </p>
                  )
                })()}

                <section>
                  <h4>Yaklaşan</h4>
                  {grouped.upcoming.length === 0 ? (
                    <p className="muted small">Planlanmış etkinlik yok.</p>
                  ) : (
                    <ul className="events-home-feed">
                      {grouped.upcoming.map((e) => (
                        <li key={e.id}>
                          <Link to={`/events/${e.id}`}>
                            <span className="events-home-feed-date">{formatWhen(e.startAtUtc)}</span>
                            <span>
                              <strong>{e.title}</strong>
                              <em>
                                {eventPhaseLabel(e.status, e.startAtUtc)}
                                {e.facilityName ? ` · ${e.facilityName}` : ''}
                                {e.attendanceCount != null
                                  ? ` · ${e.attendanceCount.toLocaleString('tr-TR')} kişi`
                                  : ''}
                              </em>
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                <section>
                  <h4>Yapılan</h4>
                  {grouped.past.length === 0 ? (
                    <p className="muted small">Bu tesiste henüz yapılmış etkinlik yok.</p>
                  ) : (
                    <ul className="events-home-feed">
                      {grouped.past.map((e) => (
                        <li key={e.id}>
                          <Link to={`/events/${e.id}`}>
                            <span className="events-home-feed-date">{formatWhen(e.startAtUtc)}</span>
                            <span>
                              <strong>{e.title}</strong>
                              <em>
                                {eventPhase(e.status, e.startAtUtc) === 'done' ? 'Yapıldı' : e.statusLabel}
                                {e.facilityName ? ` · ${e.facilityName}` : ''}
                                {e.attendanceCount != null
                                  ? ` · ${e.attendanceCount.toLocaleString('tr-TR')} kişi`
                                  : ''}
                              </em>
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              </div>
            ) : null}
          </div>
        )}
      </section>
    </div>
  )
}
