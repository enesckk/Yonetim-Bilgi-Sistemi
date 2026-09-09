import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiClientError } from '@/api/client'
import { fetchHallBoard, type HallBoardItem, type HallBooking } from '@/api/eventsApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { istanbulDayKey } from '@/lib/trHolidays'
import { toIso } from '@/lib/eventsDates'

const WEEKDAYS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz']

function startOfWeek(d = new Date()) {
  const copy = new Date(d)
  const day = (copy.getDay() + 6) % 7
  copy.setHours(0, 0, 0, 0)
  copy.setDate(copy.getDate() - day)
  return copy
}

function addDays(d: Date, n: number) {
  const copy = new Date(d)
  copy.setDate(copy.getDate() + n)
  return copy
}

function formatClock(iso: string) {
  return new Date(iso).toLocaleTimeString('tr-TR', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Europe/Istanbul',
  })
}

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Europe/Istanbul',
  })
}

function peopleLabel(booking: HallBooking | null | undefined) {
  if (booking?.attendanceCount == null) return null
  return `${booking.attendanceCount.toLocaleString('tr-TR')} kişi`
}

function bookingsOnDay(hall: HallBoardItem, key: string) {
  return hall.bookings.filter((b) => istanbulDayKey(b.startAtUtc) === key)
}

export function EventsHallsPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)

  const [weekStart, setWeekStart] = useState(() => startOfWeek())
  const [halls, setHalls] = useState<HallBoardItem[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const days = useMemo(
    () => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)),
    [weekStart],
  )
  const todayKey = istanbulDayKey(new Date())

  useEffect(() => {
    if (!canView) {
      setLoading(false)
      return
    }
    let cancelled = false
    void (async () => {
      setLoading(true)
      setError(null)
      try {
        const from = weekStart
        const to = addDays(weekStart, 7)
        const data = await fetchHallBoard({ fromUtc: toIso(from), toUtc: toIso(to) })
        if (cancelled) return
        setHalls(data.halls)
        setSelectedId((prev) => {
          if (prev && data.halls.some((h) => h.facilityId === prev)) return prev
          return data.halls[0]?.facilityId ?? null
        })
      } catch (err) {
        if (!cancelled) setError(err instanceof ApiClientError ? err.message : 'Salon tahsisi yüklenemedi.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canView, weekStart])

  const selected = halls.find((h) => h.facilityId === selectedId) ?? null
  const kkmHalls = halls.filter((h) => h.group === 'kkm')
  const otherHalls = halls.filter((h) => h.group !== 'kkm')
  const weekLabel = `${days[0].toLocaleDateString('tr-TR', { day: 'numeric', month: 'short' })} – ${days[6].toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' })}`
  const allocateDate = days.some((d) => istanbulDayKey(d) === todayKey)
    ? todayKey
    : istanbulDayKey(days[0])
  const allocateHref = selected
    ? `/events/new?facility=${selected.facilityId}&date=${allocateDate}`
    : '/events/new'

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Salon tahsisini görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-page employees-page halls-page">
      <section className="panel halls-panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Salon tahsisi</h2>
            </div>
            <p className="muted small employees-toolbar-lead">
              Kongre Merkezi salonları ve sahneler. Son yapılan iş ve gelen kişi sayısı salonda durur.
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <div className="events-cal-nav" role="group" aria-label="Hafta">
              <button
                type="button"
                className="btn-secondary"
                onClick={() => setWeekStart(addDays(weekStart, -7))}
              >
                ‹
              </button>
              <strong className="events-cal-month">{weekLabel}</strong>
              <button
                type="button"
                className="btn-secondary"
                onClick={() => setWeekStart(addDays(weekStart, 7))}
              >
                ›
              </button>
            </div>
            <button type="button" className="btn-secondary" onClick={() => setWeekStart(startOfWeek())}>
              Bu hafta
            </button>
            {canManage && selected ? (
              <Link to={allocateHref} className="btn-primary">
                + Tahsis et
              </Link>
            ) : null}
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}
        {loading ? <p className="muted">Yükleniyor…</p> : null}

        <div className="halls-layout">
          <div className="halls-board-wrap">
            <table className="halls-board">
              <thead>
                <tr>
                  <th>Salon</th>
                  {days.map((d) => {
                    const key = istanbulDayKey(d)
                    return (
                      <th key={key} className={key === todayKey ? 'is-today' : undefined}>
                        <span>{WEEKDAYS[(d.getDay() + 6) % 7]}</span>
                        <strong>{d.getDate()}</strong>
                      </th>
                    )
                  })}
                </tr>
              </thead>
              <tbody>
                {kkmHalls.length > 0 ? (
                  <tr className="halls-group">
                    <td colSpan={8}>Kültür ve Kongre Merkezi</td>
                  </tr>
                ) : null}
                {kkmHalls.map((hall) => (
                  <HallRow
                    key={hall.facilityId}
                    hall={hall}
                    days={days}
                    selected={selectedId === hall.facilityId}
                    onSelect={setSelectedId}
                  />
                ))}
                {otherHalls.length > 0 ? (
                  <tr className="halls-group">
                    <td colSpan={8}>Diğer sahneler</td>
                  </tr>
                ) : null}
                {otherHalls.map((hall) => (
                  <HallRow
                    key={hall.facilityId}
                    hall={hall}
                    days={days}
                    selected={selectedId === hall.facilityId}
                    onSelect={setSelectedId}
                  />
                ))}
              </tbody>
            </table>
          </div>

          <aside className="halls-side">
            {selected ? (
              <>
                <header>
                  <p>{selected.parentName || 'Tesis'}</p>
                  <h3>{selected.name}</h3>
                  <p className="muted small">
                    {selected.capacity ? `${selected.capacity.toLocaleString('tr-TR')} kişilik` : 'Kapasite belirtilmedi'}
                    {selected.occupiedNow ? ' · Şu an dolu' : ' · Şu an boş'}
                  </p>
                </header>

                <section>
                  <h4>En son ne yaptık</h4>
                  {selected.lastDone ? (
                    <Link to={`/events/${selected.lastDone.id}`} className="halls-last">
                      <strong>{selected.lastDone.title}</strong>
                      <span>{formatWhen(selected.lastDone.startAtUtc)}</span>
                      <em>{peopleLabel(selected.lastDone) ?? 'Katılım girilmedi'}</em>
                    </Link>
                  ) : (
                    <p className="muted">Bu salonda tamamlanmış kayıt yok.</p>
                  )}
                </section>

                <section>
                  <h4>Sıradaki</h4>
                  {selected.next ? (
                    <Link to={`/events/${selected.next.id}`} className="halls-next">
                      <strong>{selected.next.title}</strong>
                      <span>
                        {formatWhen(selected.next.startAtUtc)}
                        {selected.next.expectedAttendees
                          ? ` · ${selected.next.expectedAttendees.toLocaleString('tr-TR')} beklenen`
                          : ''}
                      </span>
                    </Link>
                  ) : (
                    <p className="muted">Planlanan tahsis yok.</p>
                  )}
                </section>

                {canManage ? (
                  <Link to={allocateHref} className="btn-primary">
                    Bu salona etkinlik ekle
                  </Link>
                ) : null}
              </>
            ) : (
              <p className="muted">Soldan bir salon seçin.</p>
            )}
          </aside>
        </div>
      </section>
    </div>
  )
}

function HallRow({
  hall,
  days,
  selected,
  onSelect,
}: {
  hall: HallBoardItem
  days: Date[]
  selected: boolean
  onSelect: (id: string) => void
}) {
  return (
    <tr className={selected ? 'is-selected' : undefined} onClick={() => onSelect(hall.facilityId)}>
      <th>
        <button type="button" onClick={() => onSelect(hall.facilityId)}>
          <strong>{hall.name}</strong>
          <span>
            {hall.occupiedNow ? 'Dolu' : 'Boş'}
            {hall.lastDone?.attendanceCount != null
              ? ` · son ${hall.lastDone.attendanceCount.toLocaleString('tr-TR')} kişi`
              : ''}
          </span>
        </button>
      </th>
      {days.map((d) => {
        const key = istanbulDayKey(d)
        const items = bookingsOnDay(hall, key)
        return (
          <td key={key}>
            {items.length === 0 ? null : (
              <ul>
                {items.map((b) => (
                  <li key={b.id}>
                    <Link to={`/events/${b.id}`} className={b.status === 4 ? 'is-done' : 'is-planned'}>
                      <em>{formatClock(b.startAtUtc)}</em>
                      {b.title}
                      {b.attendanceCount ? ` · ${b.attendanceCount}` : ''}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </td>
        )
      })}
    </tr>
  )
}
