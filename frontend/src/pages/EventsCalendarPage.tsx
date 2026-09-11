import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { createEvent, fetchEvents, eventPhase, eventPhaseLabel, type EventListItem } from '@/api/eventsApi'
import { fetchOrganizationTree } from '@/api/organizationApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { canImportEvents } from '@/auth/roles'
import { eventVenueCatalog, venueLabel, type EventVenueCatalog } from '@/lib/eventVenues'
import { endOfMonth, startOfMonth, toIso } from '@/lib/eventsDates'
import { istanbulDayKey, istanbulKeysBetween, specialDayOn } from '@/lib/trHolidays'
import type { jsPDF } from 'jspdf'

const WEEKDAYS = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz']
const EMPTY_VENUES: EventVenueCatalog = { groups: [], others: [], flat: [] }

function monthLabel(d: Date) {
  return d.toLocaleDateString('tr-TR', { month: 'long', year: 'numeric', timeZone: 'Europe/Istanbul' })
}

function calendarDayKey(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function combineLocal(date: string, time: string) {
  return `${date}T${time || '10:00'}`
}

function fromLocalInput(value: string) {
  return new Date(value).toISOString()
}

function agendaTitle(e: EventListItem) {
  const title = e.title.trim()
  if (e.status === 3) return title.replace(/^İptal:\s*/i, '')
  if (e.status === 1) return title.replace(/^Taslak:\s*/i, '')
  return title
}

function addCanvasToPdf(pdf: jsPDF, canvas: HTMLCanvasElement) {
  const pageW = pdf.internal.pageSize.getWidth()
  const pageH = pdf.internal.pageSize.getHeight()
  const margin = 7
  const usableW = pageW - margin * 2
  const usableH = pageH - margin * 2
  const imgW = usableW
  const imgH = (canvas.height * imgW) / canvas.width

  if (imgH <= usableH) {
    pdf.addImage(canvas.toDataURL('image/png'), 'PNG', margin, margin, imgW, imgH, undefined, 'NONE')
    return
  }

  const pageHeightPx = Math.max(1, Math.floor((usableH / imgW) * canvas.width))
  let y = 0
  let first = true
  while (y < canvas.height) {
    const sliceH = Math.min(pageHeightPx, canvas.height - y)
    const pageCanvas = document.createElement('canvas')
    pageCanvas.width = canvas.width
    pageCanvas.height = sliceH
    const ctx = pageCanvas.getContext('2d')
    if (!ctx) break
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, pageCanvas.width, pageCanvas.height)
    ctx.drawImage(canvas, 0, y, canvas.width, sliceH, 0, 0, canvas.width, sliceH)
    const sliceImgH = (sliceH * imgW) / canvas.width
    if (!first) pdf.addPage()
    first = false
    pdf.addImage(pageCanvas.toDataURL('image/png'), 'PNG', margin, margin, imgW, sliceImgH, undefined, 'NONE')
    y += sliceH
  }
}

export function EventsCalendarPage() {
  const { user, hasPermission } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const canImport = canManage && canImportEvents(user)
  const facilityId = searchParams.get('facility') ?? ''

  const [cursor, setCursor] = useState(() => startOfMonth())
  const [items, setItems] = useState<EventListItem[]>([])
  const [venues, setVenues] = useState<EventVenueCatalog>(EMPTY_VENUES)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedDay, setSelectedDay] = useState<string | null>(() => istanbulDayKey(new Date()))
  const [pdfBusy, setPdfBusy] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const [quickTitle, setQuickTitle] = useState('')
  const [quickTime, setQuickTime] = useState('10:00')
  const [quickFacility, setQuickFacility] = useState('')
  const [quickSaving, setQuickSaving] = useState(false)
  const [quickError, setQuickError] = useState<string | null>(null)
  const quickTitleRef = useRef<HTMLInputElement>(null)

  const selectedFacilityName = useMemo(() => venueLabel(venues, facilityId), [venues, facilityId])

  useEffect(() => {
    if (!canView) return
    let cancelled = false
    void (async () => {
      try {
        const org = await fetchOrganizationTree()
        if (cancelled) return
        setVenues(eventVenueCatalog(org))
      } catch {
        if (!cancelled) setVenues(EMPTY_VENUES)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canView])

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
        const from = startOfMonth(cursor)
        from.setDate(from.getDate() - 1)
        const to = endOfMonth(cursor)
        to.setDate(to.getDate() + 1)
        const data = await fetchEvents({
          fromUtc: toIso(from),
          toUtc: toIso(to),
          facilityId: facilityId || undefined,
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
  }, [canView, cursor, facilityId, reloadKey])

  const byDay = useMemo(() => {
    const map = new Map<string, EventListItem[]>()
    for (const e of items) {
      for (const key of istanbulKeysBetween(e.startAtUtc, e.endAtUtc)) {
        const list = map.get(key) ?? []
        if (!list.some((x) => x.id === e.id)) list.push(e)
        map.set(key, list)
      }
    }
    for (const list of map.values()) {
      list.sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc))
    }
    return map
  }, [items])

  const cells = useMemo(() => {
    const first = startOfMonth(cursor)
    const startPad = (first.getDay() + 6) % 7
    const daysInMonth = endOfMonth(cursor).getDate()
    const out: Array<{ date: Date | null; key: string | null }> = []
    for (let i = 0; i < startPad; i++) out.push({ date: null, key: null })
    for (let d = 1; d <= daysInMonth; d++) {
      const date = new Date(cursor.getFullYear(), cursor.getMonth(), d)
      out.push({ date, key: calendarDayKey(date) })
    }
    while (out.length % 7 !== 0) out.push({ date: null, key: null })
    return out
  }, [cursor])

  const selectedEvents = selectedDay ? (byDay.get(selectedDay) ?? []) : []
  const selectedSpecial = selectedDay ? specialDayOn(selectedDay) : undefined
  const todayKey = istanbulDayKey(new Date())
  const monthEventCount = useMemo(() => {
    const prefix = `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}`
    const ids = new Set<string>()
    for (const [key, list] of byDay) {
      if (!key.startsWith(prefix)) continue
      for (const e of list) ids.add(e.id)
    }
    return ids.size
  }, [byDay, cursor])

  const newEventHref = useMemo(() => {
    const q = new URLSearchParams()
    if (selectedDay) q.set('date', selectedDay)
    if (facilityId) q.set('facility', facilityId)
    const qs = q.toString()
    return `/events/new${qs ? `?${qs}` : ''}`
  }, [facilityId, selectedDay])

  useEffect(() => {
    setQuickFacility(facilityId)
    setQuickError(null)
  }, [facilityId, selectedDay])

  function onFacilityChange(value: string) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev)
      if (value) next.set('facility', value)
      else next.delete('facility')
      return next
    })
  }

  async function onQuickCreate(e: FormEvent) {
    e.preventDefault()
    if (!canManage || !selectedDay) return
    const title = quickTitle.trim()
    if (!title) {
      setQuickError('Başlık yazın.')
      quickTitleRef.current?.focus()
      return
    }
    setQuickSaving(true)
    setQuickError(null)
    try {
      const startAtUtc = fromLocalInput(combineLocal(selectedDay, quickTime || '10:00'))
      const end = new Date(startAtUtc)
      end.setHours(end.getHours() + 1)
      const venue = venues.flat.find((x) => x.id === quickFacility)
      await createEvent({
        title,
        startAtUtc,
        endAtUtc: end.toISOString(),
        status: 2,
        facilityId: quickFacility || null,
        latitude: venue?.latitude ?? null,
        longitude: venue?.longitude ?? null,
        address: venue?.address?.trim() || null,
      })
      setQuickTitle('')
      setReloadKey((k) => k + 1)
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setQuickError('Bu tesiste çakışma var. Saati veya salonu değiştirin.')
      } else {
        setQuickError(err instanceof ApiClientError ? err.message : 'Eklenemedi.')
      }
    } finally {
      setQuickSaving(false)
    }
  }

  async function downloadCalendarPdf() {
    const element = document.getElementById('events-calendar-export')
    if (!element) {
      setError('Takvim henüz hazır değil.')
      return
    }
    setPdfBusy(true)
    setError(null)
    try {
      const [{ default: html2canvas }, { jsPDF }] = await Promise.all([
        import('html2canvas'),
        import('jspdf'),
      ])
      const canvas = await html2canvas(element, {
        backgroundColor: '#ffffff',
        scale: Math.min(2, window.devicePixelRatio || 1.5),
        useCORS: true,
        logging: false,
        scrollX: 0,
        scrollY: 0,
        windowWidth: Math.max(element.scrollWidth, 1280),
        onclone: (doc) => {
          doc.documentElement.style.overflow = 'visible'
          doc.body.style.overflow = 'visible'
          const cloned = doc.getElementById('events-calendar-export')
          if (!cloned) return
          cloned.classList.add('is-exporting')
          cloned.style.width = `${Math.max(element.scrollWidth, 1100)}px`
          cloned.style.maxWidth = 'none'
          cloned.style.transform = 'none'
        },
      })
      const pdf = new jsPDF({
        orientation: 'landscape',
        unit: 'mm',
        format: 'a4',
        compress: true,
      })
      addCanvasToPdf(pdf, canvas)
      const stamp = `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}`
      const facilitySlug = selectedFacilityName
        ? `-${selectedFacilityName.toLocaleLowerCase('tr-TR').replace(/\s+/g, '-')}`
        : ''
      pdf.save(`etkinlik-takvimi-${stamp}${facilitySlug}.pdf`)
    } catch {
      setError('Takvim PDF olarak hazırlanamadı.')
    } finally {
      setPdfBusy(false)
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Takvimi görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-page employees-page events-cal-page">
      <section className="panel events-cal-panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Etkinlik takvimi</h2>
              <div className="stat-chip mobile-inline-chip">
                {loading ? '…' : `${monthEventCount} kayıt`}
              </div>
            </div>
            <p className="muted small employees-toolbar-lead">
              {selectedFacilityName
                ? `${selectedFacilityName} takvimi`
                : canManage
                  ? 'Güne tıklayın, başlığı yazın, ekleyin. Salon filtresi takvimi daraltır.'
                  : 'Birim veya salon seçerek yalnızca o programı görün'}
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <label className="events-cal-facility-field">
              <span>Birim / salon</span>
              <select
                className="events-cal-facility"
                value={facilityId}
                onChange={(e) => onFacilityChange(e.target.value)}
                aria-label="Takvim birimi"
              >
                <option value="">Tüm birimler</option>
                {venues.groups.map((g) => (
                  <optgroup key={g.parent.id} label={g.label}>
                    <option value={g.parent.id}>Tüm salonlar</option>
                    {g.halls.map((h) => (
                      <option key={h.id} value={h.id}>
                        {h.name}
                      </option>
                    ))}
                  </optgroup>
                ))}
                {venues.others.length > 0 ? (
                  <optgroup label="Diğer tesisler">
                    {venues.others.map((f) => (
                      <option key={f.id} value={f.id}>
                        {f.name}
                      </option>
                    ))}
                  </optgroup>
                ) : null}
              </select>
            </label>
            <div className="events-cal-nav" role="group" aria-label="Ay">
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
            </div>
            <div className="events-cal-toolbar-btns">
            <button
              type="button"
              className="btn-secondary"
              disabled={pdfBusy || loading}
              onClick={() => void downloadCalendarPdf()}
            >
              {pdfBusy ? 'PDF hazırlanıyor…' : 'PDF indir'}
            </button>
            <Link to="/events/list" className="btn-secondary">
              Liste
            </Link>
            {canImport ? (
              <Link to="/events/import" className="btn-secondary">
                Excel yükle
              </Link>
            ) : null}
            {canManage ? (
              <Link to={newEventHref} className="btn-primary">
                + Yeni
              </Link>
            ) : null}
            </div>
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div id="events-calendar-export" className="events-cal-export">
          <header className="events-cal-export-head">
            <p>Etkinlik takvimi{selectedFacilityName ? ` · ${selectedFacilityName}` : ''}</p>
            <h3>{monthLabel(cursor)}</h3>
          </header>
          <ul className="events-cal-legend" aria-label="Takvim işaretleri">
            <li>
              <span className="events-cal-swatch is-holiday" /> Resmi tatil
            </li>
            <li>
              <span className="events-cal-swatch is-observance" /> Özel gün
            </li>
            <li>
              <span className="events-cal-dot is-planned" /> Planlandı
            </li>
            <li>
              <span className="events-cal-dot is-done" /> Yapıldı
            </li>
          </ul>

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
              const special = specialDayOn(cell.key)
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
                    special ? `is-${special.kind}` : '',
                  ]
                    .filter(Boolean)
                    .join(' ')}
                  onClick={() => {
                    setSelectedDay(cell.key)
                    if (canManage) {
                      window.setTimeout(() => quickTitleRef.current?.focus(), 0)
                    }
                  }}
                >
                  <span className="events-cal-cell-top">
                    <span className="events-cal-day">{cell.date.getDate()}</span>
                    {events.length > 0 ? (
                      <span className="events-cal-count">{events.length}</span>
                    ) : canManage ? (
                      <span className="events-cal-quick" aria-hidden>
                        +
                      </span>
                    ) : null}
                  </span>
                  {special ? (
                    <span className={`events-cal-special is-${special.kind}`} title={special.name}>
                      {special.name}
                    </span>
                  ) : null}
                  <ul className="events-cal-chips">
                    {events.slice(0, 3).map((e) => (
                      <li
                        key={e.id}
                        title={`${e.title} · ${eventPhaseLabel(e.status, e.startAtUtc)}`}
                        className={eventPhase(e.status, e.startAtUtc) === 'done' ? 'is-done' : 'is-planned'}
                      >
                        {e.title}
                      </li>
                    ))}
                    {events.length > 3 ? <li className="is-more">+{events.length - 3}</li> : null}
                  </ul>
                </button>
              )
            })}
          </div>
        </div>

        <div className="events-cal-detail">
          <div className="events-cal-detail-head">
            <div>
              <h3>
                {selectedDay
                  ? new Date(selectedDay + 'T12:00:00').toLocaleDateString('tr-TR', {
                      weekday: 'long',
                      day: 'numeric',
                      month: 'long',
                    })
                  : 'Gün seçin'}
              </h3>
              {selectedDay ? (
                <p className="events-cal-detail-meta">
                  {loading
                    ? 'Yükleniyor…'
                    : selectedEvents.length === 0
                      ? 'Kayıt yok'
                      : `${selectedEvents.length} kayıt`}
                  {selectedSpecial
                    ? ` · ${selectedSpecial.kind === 'holiday' ? 'Resmi tatil' : 'Özel gün'}: ${selectedSpecial.name}`
                    : ''}
                </p>
              ) : (
                <p className="events-cal-detail-meta">Takvimden bir gün seçin</p>
              )}
            </div>
            {canManage && selectedDay ? (
              <Link to={newEventHref} className="btn-link events-cal-add">
                Detaylı form
              </Link>
            ) : null}
          </div>
          {canManage && selectedDay ? (
            <form className="events-cal-compose" onSubmit={(e) => void onQuickCreate(e)}>
              <input
                ref={quickTitleRef}
                className="events-cal-compose-title"
                value={quickTitle}
                onChange={(e) => setQuickTitle(e.target.value)}
                placeholder="Etkinlik adı"
                aria-label="Etkinlik adı"
                maxLength={200}
                disabled={quickSaving}
              />
              <input
                type="time"
                className="events-cal-compose-time"
                value={quickTime}
                onChange={(e) => setQuickTime(e.target.value)}
                aria-label="Saat"
                disabled={quickSaving}
              />
              <select
                className="events-cal-compose-venue"
                value={quickFacility}
                onChange={(e) => setQuickFacility(e.target.value)}
                aria-label="Tesis veya salon"
                disabled={quickSaving}
              >
                <option value="">Tesis / salon</option>
                {venues.groups.map((g) => (
                  <optgroup key={g.parent.id} label={g.label}>
                    {g.halls.map((h) => (
                      <option key={h.id} value={h.id}>
                        {h.name}
                      </option>
                    ))}
                  </optgroup>
                ))}
                {venues.others.length > 0 ? (
                  <optgroup label="Diğer tesisler">
                    {venues.others.map((f) => (
                      <option key={f.id} value={f.id}>
                        {f.name}
                      </option>
                    ))}
                  </optgroup>
                ) : null}
              </select>
              <button type="submit" className="btn-primary" disabled={quickSaving}>
                {quickSaving ? '…' : 'Ekle'}
              </button>
              {quickError ? <p className="form-error events-cal-compose-error">{quickError}</p> : null}
            </form>
          ) : null}
          {!selectedDay ? (
            <p className="events-cal-empty">Detay için takvimden bir güne tıklayın.</p>
          ) : loading ? (
            <p className="events-cal-empty">Bu günün etkinlikleri yükleniyor…</p>
          ) : selectedEvents.length === 0 ? (
            <p className="events-cal-empty">
              {canManage ? 'Bu günde henüz etkinlik yok.' : 'Bu günde etkinlik yok.'}
            </p>
          ) : (
            <ul className="events-cal-agenda">
              {selectedEvents.map((e) => {
                const phase = eventPhase(e.status, e.startAtUtc)
                const showStatus = e.status === 1 || e.status === 3 || e.status === 4
                return (
                  <li
                    key={e.id}
                    className={e.status === 3 ? 'is-cancelled' : phase === 'done' ? 'is-done' : ''}
                  >
                    <Link to={`/events/${e.id}`}>
                      <time dateTime={e.startAtUtc}>
                        {new Date(e.startAtUtc).toLocaleTimeString('tr-TR', {
                          hour: '2-digit',
                          minute: '2-digit',
                          timeZone: 'Europe/Istanbul',
                        })}
                      </time>
                      <span className="events-cal-agenda-body">
                        <strong>{agendaTitle(e)}</strong>
                        {e.categoryLabel || e.facilityName ? (
                          <em>
                            {e.categoryLabel ? <span>{e.categoryLabel}</span> : null}
                            {e.facilityName ? <span>{e.facilityName}</span> : null}
                          </em>
                        ) : null}
                      </span>
                      {showStatus ? (
                        <span className={`event-status-pill status-${e.status}`}>
                          {eventPhaseLabel(e.status, e.startAtUtc)}
                        </span>
                      ) : null}
                    </Link>
                  </li>
                )
              })}
            </ul>
          )}
        </div>
      </section>
    </div>
  )
}
