import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { eventsLookbackFromUtc, fetchEvents, type EventListItem } from '@/api/eventsApi'
import { fetchOrganizationTree } from '@/api/organizationApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { eventVenueCatalog, matchesVenue, venueCards, type VenueCard } from '@/lib/eventVenues'

function clock(iso: string) {
  return new Date(iso).toLocaleTimeString('tr-TR', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Europe/Istanbul',
  })
}

function dayLabel(iso: string) {
  return new Date(iso).toLocaleDateString('tr-TR', {
    day: 'numeric',
    month: 'short',
    timeZone: 'Europe/Istanbul',
  })
}

function hallChips(subtitle: string) {
  return subtitle
    .split('·')
    .map((part) => part.trim())
    .filter(Boolean)
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

function VenueAgenda({
  items,
  empty,
  selectedName,
  markNext,
}: {
  items: EventListItem[]
  empty: string
  selectedName: string
  markNext?: boolean
}) {
  if (items.length === 0) return <p className="events-venue-empty">{empty}</p>
  return (
    <ul className="events-venue-agenda">
      {items.map((e, i) => (
        <li key={e.id} className={markNext && i === 0 ? 'is-next' : ''}>
          <Link to={`/events/${e.id}`}>
            <span className="events-venue-when">
              <time dateTime={e.startAtUtc}>{clock(e.startAtUtc)}</time>
              <span>{dayLabel(e.startAtUtc)}</span>
            </span>
            <span className="events-venue-agenda-body">
              <strong>
                {markNext && i === 0 ? <span className="events-venue-next-tag">Sıradaki</span> : null}
                {e.title}
              </strong>
              {(e.facilityName && e.facilityName !== selectedName) || e.attendanceCount != null ? (
                <span className="events-venue-agenda-meta">
                  {e.facilityName && e.facilityName !== selectedName ? <span>{e.facilityName}</span> : null}
                  {e.attendanceCount != null ? (
                    <span>{e.attendanceCount.toLocaleString('tr-TR')} kişi</span>
                  ) : null}
                </span>
              ) : null}
            </span>
            {e.status === 1 ? <span className="event-status-pill status-1">Taslak</span> : null}
          </Link>
        </li>
      ))}
    </ul>
  )
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
      const [tree, list] = await Promise.all([
        fetchOrganizationTree(),
        fetchEvents({ fromUtc: eventsLookbackFromUtc() }),
      ])
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
    <div className="events-page events-venue-page">
      <section className="panel">
        <header className="events-venue-page-head">
          <h2>Tesis programı</h2>
          <p>{loading ? '…' : `${visible.length} tesis`}</p>
        </header>

        {error ? <p className="form-error">{error}</p> : null}
        {loading ? (
          <div className="ui-skeleton-stack">
            <span className="ui-skeleton" />
            <span className="ui-skeleton" />
          </div>
        ) : visible.length === 0 && !search.trim() ? (
          <p className="muted">Gösterilecek tesis yok.</p>
        ) : (
          <div className="events-venue-layout">
            <aside className="events-venue-rail">
              <form className="events-venue-search" role="search" onSubmit={(e) => e.preventDefault()}>
                <input
                  type="search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Tesis ara…"
                  aria-label="Tesis ara"
                />
              </form>
              {visible.length === 0 ? (
                <p className="events-venue-empty">Eşleşen tesis yok.</p>
              ) : (
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
                          <em>
                            {row?.upcoming
                              ? `${row.upcoming} yaklaşan`
                              : row?.total
                                ? `${row.total} kayıt`
                                : 'Kayıt yok'}
                          </em>
                        </button>
                      </li>
                    )
                  })}
                </ul>
              )}
            </aside>

            {selected ? (
              <div className="events-venue-detail">
                <header className="events-venue-detail-head">
                  <div>
                    <h3>{selected.name}</h3>
                    {selected.subtitle ? (
                      <p className="events-venue-halls">
                        {hallChips(selected.subtitle).map((hall) => (
                          <span key={hall}>{hall}</span>
                        ))}
                      </p>
                    ) : null}
                    <p className="events-venue-stats">
                      <span>
                        <strong>{grouped.upcoming.length}</strong> yaklaşan
                      </span>
                      <span>
                        <strong>{grouped.past.length}</strong> yapılan
                      </span>
                    </p>
                  </div>
                  <div className="events-venue-detail-actions">
                    <Link
                      to={`/events/calendar?facility=${encodeURIComponent(selected.id)}`}
                      className="btn-link"
                    >
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

                <section>
                  <h4>Yaklaşan</h4>
                  <VenueAgenda
                    items={grouped.upcoming}
                    empty="Yaklaşan etkinlik yok."
                    selectedName={selected.name}
                    markNext
                  />
                </section>

                <section>
                  <h4>Yapılan</h4>
                  <VenueAgenda
                    items={grouped.past}
                    empty="Yapılmış etkinlik yok."
                    selectedName={selected.name}
                  />
                </section>
              </div>
            ) : null}
          </div>
        )}
      </section>
    </div>
  )
}
