import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { fetchEvents, type EventListItem } from '@/api/eventsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { endOfMonth, startOfMonth, toIso } from '@/lib/eventsDates'

const WEEKDAYS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz']

function monthLabel(d: Date) {
  return d.toLocaleDateString('tr-TR', { month: 'long', year: 'numeric' })
}

function dayKey(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function EventsCalendarPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)

  const [cursor, setCursor] = useState(() => startOfMonth())
  const [items, setItems] = useState<EventListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedDay, setSelectedDay] = useState<string | null>(null)

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
        const data = await fetchEvents({
          fromUtc: toIso(startOfMonth(cursor)),
          toUtc: toIso(endOfMonth(cursor)),
        })
        if (!cancelled) setItems(data.items)
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiClientError ? err.message : 'Takvim yüklenemedi.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canView, cursor])

  const byDay = useMemo(() => {
    const map = new Map<string, EventListItem[]>()
    for (const e of items) {
      const key = dayKey(new Date(e.startAtUtc))
      const list = map.get(key) ?? []
      list.push(e)
      map.set(key, list)
    }
    for (const list of map.values()) {
      list.sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc))
    }
    return map
  }, [items])

  const cells = useMemo(() => {
    const first = startOfMonth(cursor)
    const startPad = (first.getDay() + 6) % 7 // Monday=0
    const daysInMonth = endOfMonth(cursor).getDate()
    const out: Array<{ date: Date | null; key: string | null }> = []
    for (let i = 0; i < startPad; i++) out.push({ date: null, key: null })
    for (let d = 1; d <= daysInMonth; d++) {
      const date = new Date(cursor.getFullYear(), cursor.getMonth(), d)
      out.push({ date, key: dayKey(date) })
    }
    while (out.length % 7 !== 0) out.push({ date: null, key: null })
    return out
  }, [cursor])

  const selectedEvents = selectedDay ? (byDay.get(selectedDay) ?? []) : []
  const todayKey = dayKey(new Date())

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Takvimi görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-page employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Etkinlik takvimi</h2>
              <div className="stat-chip mobile-inline-chip">
                {loading ? '…' : `${items.length} kayıt`}
              </div>
            </div>
            <p className="muted small employees-toolbar-lead">
              Aylık görünüm · güne tıklayarak detayları görün
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setCursor(startOfMonth(new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1)))}
            >
              ‹
            </button>
            <strong className="events-cal-month">{monthLabel(cursor)}</strong>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setCursor(startOfMonth(new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1)))}
            >
              ›
            </button>
            <button type="button" className="btn-secondary" onClick={() => setCursor(startOfMonth())}>
              Bu ay
            </button>
            <Link to="/events/list" className="btn-secondary">
              Liste
            </Link>
            {canManage ? (
              <Link to="/events/new" className="btn-primary">
                + Yeni
              </Link>
            ) : null}
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="events-cal-grid" role="grid" aria-label={monthLabel(cursor)}>
          {WEEKDAYS.map((d) => (
            <div key={d} className="events-cal-head">
              {d}
            </div>
          ))}
          {cells.map((cell, i) => {
            if (!cell.date || !cell.key) {
              return <div key={`e-${i}`} className="events-cal-cell is-empty" />
            }
            const events = byDay.get(cell.key) ?? []
            const isToday = cell.key === todayKey
            const isSelected = cell.key === selectedDay
            return (
              <button
                key={cell.key}
                type="button"
                className={[
                  'events-cal-cell',
                  isToday ? 'is-today' : '',
                  isSelected ? 'is-selected' : '',
                  events.length ? 'has-events' : '',
                ]
                  .filter(Boolean)
                  .join(' ')}
                onClick={() => setSelectedDay(cell.key)}
              >
                <span className="events-cal-day">{cell.date.getDate()}</span>
                {events.length > 0 ? (
                  <span className="events-cal-count">{events.length}</span>
                ) : null}
                <ul className="events-cal-dots" aria-hidden="true">
                  {events.slice(0, 3).map((e) => (
                    <li key={e.id} title={e.title} />
                  ))}
                </ul>
              </button>
            )
          })}
        </div>

        <div className="events-cal-detail">
          <h3 className="section-title" style={{ fontSize: '1.05rem' }}>
            {selectedDay
              ? new Date(selectedDay + 'T12:00:00').toLocaleDateString('tr-TR', {
                  weekday: 'long',
                  day: 'numeric',
                  month: 'long',
                })
              : 'Gün seçin'}
          </h3>
          {!selectedDay ? (
            <p className="muted">Detay için takvimden bir güne tıklayın.</p>
          ) : selectedEvents.length === 0 ? (
            <p className="muted">Bu günde etkinlik yok.</p>
          ) : (
            <ul className="events-home-feed">
              {selectedEvents.map((e) => (
                <li key={e.id}>
                  <Link to={`/events/${e.id}`}>
                    <span className="events-home-feed-date">
                      {new Date(e.startAtUtc).toLocaleTimeString('tr-TR', {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </span>
                    <span>
                      <strong>{e.title}</strong>
                      <em>
                        {e.statusLabel}
                        {e.facilityName ? ` · ${e.facilityName}` : ''}
                      </em>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      </section>
    </div>
  )
}
