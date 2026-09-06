import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { fetchOrganizationTree, type OrgNode } from '@/api/organizationApi'
import { fetchEmployeeFormOptions, type LookupItem } from '@/api/employeesApi'
import {
  changeEventStatus,
  checkEventConflicts,
  createEvent,
  deleteEvent,
  EVENT_RECURRENCE,
  EVENT_STATUSES,
  EVENT_TRANSITION_LABELS,
  fetchEventById,
  fetchEventTimeline,
  updateEvent,
  type EventDetail,
  type EventRecurrenceFrequency,
  type EventStatus,
  type EventTimelineItem,
  type EventWritePayload,
} from '@/api/eventsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { EventMiniMap } from '@/components/EventMiniMap'
import { LocationPickerMap } from '@/components/LocationPickerMap'
import { useAlert, useConfirm } from '@/components/ConfirmDialog'
import { geocodeAddress, reverseGeocode } from '@/lib/geocode'

const ORG_FACILITY = 6

function flattenFacilities(nodes: OrgNode[], acc: OrgNode[] = []): OrgNode[] {
  for (const n of nodes) {
    if (n.type === ORG_FACILITY) acc.push(n)
    if (n.children?.length) flattenFacilities(n.children, acc)
  }
  return acc
}

function flattenUnits(nodes: OrgNode[], acc: OrgNode[] = []): OrgNode[] {
  for (const n of nodes) {
    if (n.type !== ORG_FACILITY) acc.push(n)
    if (n.children?.length) flattenUnits(n.children, acc)
  }
  return acc
}

function toLocalInput(iso?: string | null) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function fromLocalInput(value: string) {
  return new Date(value).toISOString()
}

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}

type FormState = {
  title: string
  description: string
  startAtLocal: string
  endAtLocal: string
  status: EventStatus
  organizingUnitId: string
  facilityId: string
  latitude: string
  longitude: string
  address: string
  expectedAttendees: string
  responsibleEmployeeId: string
  recurrenceFrequency: EventRecurrenceFrequency
  recurrenceOccurrences: string
}

const emptyForm = (): FormState => ({
  title: '',
  description: '',
  startAtLocal: toLocalInput(new Date().toISOString()),
  endAtLocal: '',
  status: 1,
  organizingUnitId: '',
  facilityId: '',
  latitude: '',
  longitude: '',
  address: '',
  expectedAttendees: '',
  responsibleEmployeeId: '',
  recurrenceFrequency: 0,
  recurrenceOccurrences: '4',
})

export function EventFormPage() {
  const { id } = useParams<{ id: string }>()
  const isEdit = Boolean(id)
  const navigate = useNavigate()
  const confirm = useConfirm()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(PermissionCodes.EventsManage)

  const [form, setForm] = useState<FormState>(emptyForm)
  const [initialStatus, setInitialStatus] = useState<EventStatus>(1)
  const [tree, setTree] = useState<OrgNode[]>([])
  const [people, setPeople] = useState<LookupItem[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [conflictHint, setConflictHint] = useState<string | null>(null)
  const [geocoding, setGeocoding] = useState(false)

  const facilities = useMemo(() => flattenFacilities(tree).sort((a, b) => a.name.localeCompare(b.name, 'tr')), [tree])
  const units = useMemo(() => flattenUnits(tree).sort((a, b) => a.name.localeCompare(b.name, 'tr')), [tree])

  const statusOptions = useMemo(() => {
    if (!isEdit) return EVENT_STATUSES.filter((s) => s.value === 1 || s.value === 2)
    return EVENT_STATUSES.filter((s) => s.value === initialStatus || allowedFrom(initialStatus).includes(s.value))
  }, [initialStatus, isEdit])

  useEffect(() => {
    if (!canManage) {
      setLoading(false)
      return
    }
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const [org, opts] = await Promise.all([
          fetchOrganizationTree(),
          fetchEmployeeFormOptions().catch(() => ({ managers: [] as LookupItem[] })),
        ])
        if (cancelled) return
        setTree(org)
        setPeople(opts.managers ?? [])
        if (isEdit && id) {
          const ev = await fetchEventById(id)
          if (cancelled) return
          setInitialStatus(ev.status)
          setForm({
            title: ev.title,
            description: ev.description ?? '',
            startAtLocal: toLocalInput(ev.startAtUtc),
            endAtLocal: toLocalInput(ev.endAtUtc),
            status: ev.status,
            organizingUnitId: ev.organizingUnitId ?? '',
            facilityId: ev.facilityId ?? '',
            latitude: ev.latitude != null ? String(ev.latitude) : '',
            longitude: ev.longitude != null ? String(ev.longitude) : '',
            address: ev.address ?? '',
            expectedAttendees: ev.expectedAttendees != null ? String(ev.expectedAttendees) : '',
            responsibleEmployeeId: ev.responsibleEmployeeId ?? '',
            recurrenceFrequency: 0,
            recurrenceOccurrences: '4',
          })
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof ApiClientError ? err.message : 'Form yüklenemedi.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManage, id, isEdit])

  useEffect(() => {
    if (!form.facilityId || !form.startAtLocal) {
      setConflictHint(null)
      return
    }
    let cancelled = false
    const timer = window.setTimeout(() => {
      void (async () => {
        try {
          const result = await checkEventConflicts({
            facilityId: form.facilityId,
            startAtUtc: fromLocalInput(form.startAtLocal),
            endAtUtc: form.endAtLocal ? fromLocalInput(form.endAtLocal) : null,
            excludeEventId: id ?? null,
          })
          if (cancelled) return
          if (!result.hasConflicts) {
            setConflictHint(null)
            return
          }
          const names = result.items
            .slice(0, 2)
            .map((c) => c.title)
            .join(', ')
          const more = result.items.length > 2 ? ` (+${result.items.length - 2})` : ''
          setConflictHint(`Bu tesiste çakışma olabilir: ${names}${more}`)
        } catch {
          if (!cancelled) setConflictHint(null)
        }
      })()
    }, 400)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [form.facilityId, form.startAtLocal, form.endAtLocal, id])

  function setField<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  function onFacilityChange(facilityId: string) {
    const f = facilities.find((x) => x.id === facilityId)
    setForm((prev) => ({
      ...prev,
      facilityId,
      latitude: f?.latitude != null ? String(f.latitude) : prev.latitude,
      longitude: f?.longitude != null ? String(f.longitude) : prev.longitude,
      address: f?.address?.trim() ? f.address : prev.address,
    }))
  }

  async function fillCoordsFromAddress() {
    const q = form.address.trim()
    if (q.length < 3) {
      setError('Adres en az 3 karakter olmalı.')
      return
    }
    setGeocoding(true)
    setError(null)
    try {
      const hit = await geocodeAddress(q)
      if (!hit) {
        setError('Adres için konum bulunamadı. Daha açık yazın (mahalle, cadde, Gaziantep).')
        return
      }
      setForm((prev) => ({
        ...prev,
        latitude: hit.latitude.toFixed(6),
        longitude: hit.longitude.toFixed(6),
      }))
    } catch {
      setError('Adres araması şu an yapılamadı. Biraz sonra tekrar deneyin.')
    } finally {
      setGeocoding(false)
    }
  }

  async function onMapPick(lat: number, lng: number) {
    setForm((prev) => ({
      ...prev,
      latitude: lat.toFixed(6),
      longitude: lng.toFixed(6),
    }))
    try {
      const hit = await reverseGeocode(lat, lng)
      if (hit?.displayName) {
        setForm((prev) => ({
          ...prev,
          address: prev.address.trim() ? prev.address : hit.displayName,
        }))
      }
    } catch {
      /* adres opsiyonel */
    }
  }

  function buildPayload(allowConflicts: boolean): EventWritePayload {
    const attendees = form.expectedAttendees.trim()
      ? Number(form.expectedAttendees)
      : null
    return {
      title: form.title.trim(),
      description: form.description.trim() || null,
      startAtUtc: fromLocalInput(form.startAtLocal),
      endAtUtc: form.endAtLocal ? fromLocalInput(form.endAtLocal) : null,
      status: form.status,
      organizingUnitId: form.organizingUnitId || null,
      facilityId: form.facilityId || null,
      latitude: form.latitude.trim() ? Number(form.latitude) : null,
      longitude: form.longitude.trim() ? Number(form.longitude) : null,
      address: form.address.trim() || null,
      expectedAttendees: attendees != null && Number.isFinite(attendees) ? attendees : null,
      responsibleEmployeeId: form.responsibleEmployeeId || null,
      recurrenceFrequency: isEdit ? 0 : form.recurrenceFrequency,
      recurrenceOccurrences:
        !isEdit && form.recurrenceFrequency !== 0
          ? Math.min(26, Math.max(2, Number(form.recurrenceOccurrences) || 4))
          : null,
      allowConflicts,
    }
  }

  async function save(allowConflicts: boolean) {
    const payload = buildPayload(allowConflicts)
    if (isEdit && id) {
      await updateEvent(id, payload)
      navigate(`/events/${id}`)
    } else {
      const created = await createEvent(payload)
      navigate(`/events/${created.id}`)
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!canManage) return
    setSaving(true)
    setError(null)
    try {
      await save(false)
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        const ok = await confirm({
          title: 'Tesis çakışması',
          message: `${err.message}\n\nYine de kaydedilsin mi?`,
          confirmLabel: 'Yine de kaydet',
          tone: 'primary',
        })
        if (ok) {
          try {
            await save(true)
            return
          } catch (retryErr) {
            setError(retryErr instanceof ApiClientError ? retryErr.message : 'Kayıt kaydedilemedi.')
          }
        }
      } else {
        setError(err instanceof ApiClientError ? err.message : 'Kayıt kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  if (!canManage) {
    return (
      <div className="panel">
        <p className="form-error">Etkinlik yönetme yetkiniz yok.</p>
      </div>
    )
  }

  if (loading) return <div className="panel muted">Yükleniyor…</div>

  return (
    <div className="events-page employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>{isEdit ? 'Etkinlik düzenle' : 'Yeni etkinlik'}</h2>
            </div>
            <p className="muted small employees-toolbar-lead">
              Tesis seçerseniz harita pin’i tesis koordinatından da okunur.
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <Link to="/events/list" className="btn-secondary">
              Listeye dön
            </Link>
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}
        {conflictHint ? (
          <p className="events-conflict-hint" role="status">
            {conflictHint}
          </p>
        ) : null}

        <form className="form-grid" onSubmit={onSubmit}>
          <label className="span-2">
            Başlık *
            <input value={form.title} onChange={(e) => setField('title', e.target.value)} required />
          </label>
          <label className="span-2">
            Açıklama
            <textarea
              rows={4}
              value={form.description}
              onChange={(e) => setField('description', e.target.value)}
            />
          </label>
          <label>
            Başlangıç *
            <input
              type="datetime-local"
              value={form.startAtLocal}
              onChange={(e) => setField('startAtLocal', e.target.value)}
              required
            />
          </label>
          <label>
            Bitiş
            <input
              type="datetime-local"
              value={form.endAtLocal}
              onChange={(e) => setField('endAtLocal', e.target.value)}
            />
          </label>
          <label>
            Durum
            <select
              value={form.status}
              onChange={(e) => setField('status', Number(e.target.value) as EventStatus)}
            >
              {statusOptions.map((s) => (
                <option key={s.value} value={s.value}>
                  {s.label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Düzenleyen birim
            <select
              value={form.organizingUnitId}
              onChange={(e) => setField('organizingUnitId', e.target.value)}
            >
              <option value="">Seçiniz</option>
              {units.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Beklenen katılımcı
            <input
              type="number"
              min={1}
              max={100000}
              value={form.expectedAttendees}
              onChange={(e) => setField('expectedAttendees', e.target.value)}
              placeholder="Örn. 120"
            />
          </label>
          <label>
            Sorumlu personel
            <select
              value={form.responsibleEmployeeId}
              onChange={(e) => setField('responsibleEmployeeId', e.target.value)}
            >
              <option value="">Seçiniz (opsiyonel)</option>
              {people.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          {!isEdit ? (
            <>
              <label>
                Tekrar
                <select
                  value={form.recurrenceFrequency}
                  onChange={(e) =>
                    setField('recurrenceFrequency', Number(e.target.value) as EventRecurrenceFrequency)
                  }
                >
                  {EVENT_RECURRENCE.map((r) => (
                    <option key={r.value} value={r.value}>
                      {r.label}
                    </option>
                  ))}
                </select>
              </label>
              {form.recurrenceFrequency !== 0 ? (
                <label>
                  Tekrar sayısı (2–26)
                  <input
                    type="number"
                    min={2}
                    max={26}
                    value={form.recurrenceOccurrences}
                    onChange={(e) => setField('recurrenceOccurrences', e.target.value)}
                  />
                </label>
              ) : (
                <div />
              )}
            </>
          ) : null}
          <label className="span-2">
            Tesis
            <select value={form.facilityId} onChange={(e) => onFacilityChange(e.target.value)}>
              <option value="">Seçiniz (opsiyonel)</option>
              {facilities.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.name}
                  {f.latitude != null && f.longitude != null ? '' : ' — konum yok'}
                </option>
              ))}
            </select>
          </label>
          <label className="span-2">
            Adres
            <div className="events-address-row">
              <input
                value={form.address}
                onChange={(e) => setField('address', e.target.value)}
                placeholder="Mahalle, cadde veya tesis adı"
              />
              <button
                type="button"
                className="btn-secondary"
                disabled={geocoding || form.address.trim().length < 3}
                onClick={() => void fillCoordsFromAddress()}
              >
                {geocoding ? 'Aranıyor…' : 'Adresten konum bul'}
              </button>
            </div>
            <span className="muted small">
              Koordinat bilmiyorsanız adresi yazıp konum bulabilirsiniz (Şehitkamil öncelikli).
            </span>
          </label>
          <label>
            Enlem
            <input
              value={form.latitude}
              onChange={(e) => setField('latitude', e.target.value)}
              placeholder="37.06"
              inputMode="decimal"
            />
          </label>
          <label>
            Boylam
            <input
              value={form.longitude}
              onChange={(e) => setField('longitude', e.target.value)}
              placeholder="37.38"
              inputMode="decimal"
            />
          </label>
          <div className="span-2">
            <LocationPickerMap
              latitude={form.latitude.trim() ? Number(form.latitude) : null}
              longitude={form.longitude.trim() ? Number(form.longitude) : null}
              onPick={(lat, lng) => void onMapPick(lat, lng)}
            />
          </div>
          <div className="form-actions span-2">
            <button type="submit" className="btn-primary" disabled={saving}>
              {saving ? 'Kaydediliyor…' : isEdit ? 'Güncelle' : 'Oluştur'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}

function allowedFrom(status: EventStatus): EventStatus[] {
  if (status === 1) return [2, 3]
  if (status === 2) return [4, 3, 1]
  if (status === 3) return [1]
  if (status === 4) return [1]
  return []
}

export function EventDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const alert = useAlert()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const [error, setError] = useState<string | null>(null)
  const [item, setItem] = useState<EventDetail | null>(null)
  const [timeline, setTimeline] = useState<EventTimelineItem[]>([])
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (!canView || !id) return
    let cancelled = false
    ;(async () => {
      try {
        const [data, tl] = await Promise.all([
          fetchEventById(id),
          fetchEventTimeline(id).catch(() => ({ items: [] as EventTimelineItem[] })),
        ])
        if (!cancelled) {
          setItem(data)
          setTimeline(tl.items)
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof ApiClientError ? err.message : 'Yüklenemedi.')
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canView, id])

  async function onDelete() {
    if (!item) return
    const ok = await confirm({
      title: 'Etkinliği sil',
      message: `"${item.title}" silinsin mi?`,
      confirmLabel: 'Sil',
      tone: 'danger',
    })
    if (!ok) return
    setBusy(true)
    try {
      await deleteEvent(item.id)
      navigate('/events/list')
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Silinemedi.',
        tone: 'warning',
      })
    } finally {
      setBusy(false)
    }
  }

  async function applyStatus(next: EventStatus) {
    if (!item) return
    const label = EVENT_TRANSITION_LABELS[next] ?? 'Durumu değiştir'
    const ok = await confirm({
      title: label,
      message: `"${item.title}" → ${EVENT_STATUSES.find((s) => s.value === next)?.label ?? next}`,
      confirmLabel: label,
      tone: next === 3 ? 'danger' : 'primary',
    })
    if (!ok) return

    setBusy(true)
    setError(null)
    try {
      try {
        const updated = await changeEventStatus(item.id, next, false)
        setItem(updated)
        const tl = await fetchEventTimeline(item.id).catch(() => ({ items: [] as EventTimelineItem[] }))
        setTimeline(tl.items)
      } catch (err) {
        if (err instanceof ApiClientError && err.status === 409) {
          const force = await confirm({
            title: 'Tesis çakışması',
            message: `${err.message}\n\nYine de devam edilsin mi?`,
            confirmLabel: 'Yine de devam',
            tone: 'primary',
          })
          if (!force) return
          const updated = await changeEventStatus(item.id, next, true)
          setItem(updated)
          const tl = await fetchEventTimeline(item.id).catch(() => ({ items: [] as EventTimelineItem[] }))
          setTimeline(tl.items)
        } else {
          throw err
        }
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Durum güncellenemedi.')
    } finally {
      setBusy(false)
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Etkinlik görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  if (error && !item) return <div className="panel form-error">{error}</div>
  if (!item) return <div className="panel muted">Yükleniyor…</div>

  const transitions = item.allowedTransitions ?? []
  const hasMap = item.latitude != null && item.longitude != null

  return (
    <div className="events-page employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>{item.title}</h2>
              <span className={`event-status-pill status-${item.status}`}>{item.statusLabel}</span>
            </div>
            <p className="muted small employees-toolbar-lead">
              {formatWhen(item.startAtUtc)}
              {item.endAtUtc ? ` – ${formatWhen(item.endAtUtc)}` : ''}
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <div className="events-action-group events-action-group-lg" role="group" aria-label="İşlemler">
              <Link to="/events/list" className="events-action-btn">
                Liste
              </Link>
              <Link to="/events/map" className="events-action-btn">
                Harita
              </Link>
              {canManage ? (
                <>
                  <Link to={`/events/${item.id}/edit`} className="events-action-btn is-primary">
                    Düzenle
                  </Link>
                  <button
                    type="button"
                    className="events-action-btn is-danger"
                    disabled={busy}
                    onClick={() => void onDelete()}
                  >
                    Sil
                  </button>
                </>
              ) : null}
            </div>
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        {canManage && transitions.length > 0 ? (
          <div className="events-status-flow" role="group" aria-label="Durum geçişleri">
            <span className="events-status-flow-label">Durum</span>
            <div className="events-status-flow-actions">
              {transitions.map((st) => (
                <button
                  key={st}
                  type="button"
                  className={`events-status-btn status-${st}`}
                  disabled={busy}
                  onClick={() => void applyStatus(st)}
                >
                  {EVENT_TRANSITION_LABELS[st] ?? EVENT_STATUSES.find((s) => s.value === st)?.label}
                </button>
              ))}
            </div>
          </div>
        ) : null}

        <div className={`events-detail-layout${hasMap ? ' has-map' : ''}`}>
          <div className="events-detail-grid">
            <article>
              <span>Açıklama</span>
              <p>{item.description || '—'}</p>
            </article>
            <article>
              <span>Birim</span>
              <p>{item.organizingUnitName || '—'}</p>
            </article>
            <article>
              <span>Tesis</span>
              <p>{item.facilityName || '—'}</p>
            </article>
            <article>
              <span>Sorumlu personel</span>
              <p>
                {item.responsibleEmployeeId ? (
                  <Link to={`/employees/${item.responsibleEmployeeId}`}>
                    {item.responsibleEmployeeName || 'Personel'}
                  </Link>
                ) : (
                  '—'
                )}
              </p>
            </article>
            <article>
              <span>Beklenen katılımcı</span>
              <p>{item.expectedAttendees != null ? item.expectedAttendees.toLocaleString('tr-TR') : '—'}</p>
            </article>
            <article>
              <span>Tekrar / seri</span>
              <p>
                {item.recurrenceLabel
                  ? `${item.recurrenceLabel}${item.seriesCount ? ` · ${item.seriesCount} kayıt` : ''}`
                  : item.seriesId
                    ? `Seri${item.seriesCount ? ` · ${item.seriesCount} kayıt` : ''}`
                    : 'Tek etkinlik'}
              </p>
            </article>
            <article>
              <span>Konum</span>
              <p>
                {item.address || '—'}
                {hasMap ? ` (${item.latitude!.toFixed(5)}, ${item.longitude!.toFixed(5)})` : ''}
              </p>
            </article>
          </div>
          {hasMap ? (
            <div className="events-detail-map">
              <EventMiniMap
                latitude={item.latitude!}
                longitude={item.longitude!}
                title={item.title}
              />
              <Link
                to={`/events/map?lat=${item.latitude}&lng=${item.longitude}&zoom=16&pin=${item.id}`}
                className="btn-link events-detail-map-link"
              >
                Tam haritada aç ›
              </Link>
            </div>
          ) : null}
        </div>

        <section className="events-timeline" aria-label="Zaman çizelgesi">
          <header className="events-timeline-head">
            <h3>Zaman çizelgesi</h3>
            <p>Etkinlik takvimi ve işlem geçmişi</p>
          </header>
          <ol className="events-timeline-schedule">
            <li>
              <span>Başlangıç</span>
              <strong>{formatWhen(item.startAtUtc)}</strong>
            </li>
            <li>
              <span>Bitiş</span>
              <strong>{item.endAtUtc ? formatWhen(item.endAtUtc) : '—'}</strong>
            </li>
            <li>
              <span>Durum</span>
              <strong>{item.statusLabel}</strong>
            </li>
          </ol>
          {timeline.length === 0 ? (
            <p className="muted small">Henüz işlem kaydı yok.</p>
          ) : (
            <ul className="events-timeline-log">
              {timeline.map((t, i) => (
                <li key={`${t.occurredAtUtc}-${i}`}>
                  <time>{formatWhen(t.occurredAtUtc)}</time>
                  <div>
                    <strong>{t.actionLabel}</strong>
                    <em>{t.userName || 'sistem'}</em>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      </section>
    </div>
  )
}
