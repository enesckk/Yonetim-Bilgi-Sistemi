import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, Navigate, useParams, useSearchParams } from 'react-router-dom'
import { canSeeAllUnits } from '@/auth/roles'
import { GeoJSON, MapContainer, useMap } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import {
  eventPhase,
  eventPhaseLabel,
  fetchEvents,
  type EventListItem,
} from '@/api/eventsApi'
import {
  fetchSettlementDetail,
  type SettlementArea,
  type SettlementFacility,
  type SettlementSchool,
  type SettlementSummary,
} from '@/api/mapApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { COVERAGE_LEGEND, coverageFill, formatRate, levelFromCoverage } from '@/lib/coverage'
import { loadMahalleler } from '@/lib/geoCache'
import { type MahalleFeature } from '@/lib/geo'
import { PageBackLink } from '@/components/PageBackLink'
import { formatPhone, telHref } from '@/lib/phone'
import {
  AreaEditor,
  HeadmanEditor,
  PopulationEditor,
  SchoolEditor,
  useSettlementDelete,
} from '@/pages/SettlementRecordForms'

function formatDay(iso?: string | null) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString('tr-TR', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
}

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function coverageFact(detail: SettlementSummary, level: 'none' | 'low' | 'medium' | 'good') {
  if (level === 'none') return 'Etkinlik yok'
  if (detail.coverageRate != null) return `${formatRate(detail.coverageRate)} kaplama`
  if (level === 'low') return 'Kapsama düşük'
  if (level === 'medium') return 'Kapsama orta'
  return 'Kapsama iyi'
}

function MiniFit({ feature }: { feature: MahalleFeature }) {
  const map = useMap()
  useEffect(() => {
    const layer = L.geoJSON(feature as unknown as GeoJSON.Feature)
    const bounds = layer.getBounds()
    const fit = () => {
      map.invalidateSize()
      if (bounds.isValid()) map.fitBounds(bounds.pad(0.38), { animate: false })
    }
    fit()
    const id = window.setTimeout(fit, 80)
    return () => window.clearTimeout(id)
  }, [feature, map])
  return null
}

export function SettlementDetailPage() {
  const { user } = useAuth()
  if (!canSeeAllUnits(user)) return <Navigate to="/events/calendar" replace />
  return <SettlementDetailPageInner />
}

function SettlementDetailPageInner() {
  const { code = '' } = useParams()
  const [searchParams] = useSearchParams()
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const backTo = searchParams.get('from') === 'cards' ? '/events/map?view=cards' : '/events/map'

  const [detail, setDetail] = useState<SettlementSummary | null>(null)
  const [events, setEvents] = useState<EventListItem[]>([])
  const [feature, setFeature] = useState<MahalleFeature | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<'ozet' | 'okullar' | 'tesis' | 'etkinlikler'>('ozet')
  const [editRecord, setEditRecord] = useState(false)
  const [schoolForm, setSchoolForm] = useState<SettlementSchool | null | 'new'>(null)
  const [areaForm, setAreaForm] = useState<SettlementArea | null | 'new'>(null)
  const removeRecord = useSettlementDelete()

  const load = useCallback(async () => {
    if (!canView || !code) return
    setLoading(true)
    setError(null)
    try {
      const [row, geo] = await Promise.all([fetchSettlementDetail(code), loadMahalleler()])
      setDetail(row)
      const match = geo?.features.find((f) => f.properties.id === row.officialCode) ?? null
      setFeature(match)
      const ev = await fetchEvents({
        settlementId: row.settlementId,
      })
      setEvents(ev.items)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Mahalle bilgisi yüklenemedi.')
      setDetail(null)
    } finally {
      setLoading(false)
    }
  }, [canView, code])

  useEffect(() => {
    void load()
  }, [load])

  const upcoming = useMemo(
    () =>
      events.filter((e) => eventPhase(e.status, e.startAtUtc) === 'planned').sort((a, b) => +new Date(a.startAtUtc) - +new Date(b.startAtUtc)),
    [events],
  )
  const done = useMemo(
    () =>
      events.filter((e) => eventPhase(e.status, e.startAtUtc) === 'done').sort((a, b) => +new Date(b.startAtUtc) - +new Date(a.startAtUtc)),
    [events],
  )
  const other = useMemo(
    () => events.filter((e) => eventPhase(e.status, e.startAtUtc) === 'other'),
    [events],
  )

  const schools = detail?.schools ?? []
  const schoolStudents = schools.reduce((sum, row) => sum + (row.studentCount ?? 0), 0)
  const facilities = detail?.facilities ?? []
  const areas = detail?.areas ?? []
  const venueCount = facilities.length + areas.length

  const categoryMix = useMemo(() => {
    const counts = new Map<string, { label: string; n: number }>()
    for (const e of events) {
      if (e.status === 3) continue
      const key = e.category || 'uncategorized'
      const label = e.categoryLabel || 'Tür yok'
      const row = counts.get(key) ?? { label, n: 0 }
      row.n += 1
      counts.set(key, row)
    }
    return [...counts.values()].sort((a, b) => b.n - a.n)
  }, [events])

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Mahalle görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  if (loading) {
    return (
      <div className="panel">
        <p className="muted">Mahalle yükleniyor…</p>
      </div>
    )
  }

  if (error || !detail) {
    return (
      <div className="panel">
        <p className="form-error">{error ?? 'Mahalle bulunamadı.'}</p>
        <PageBackLink to={backTo}>Mahallelere dön</PageBackLink>
      </div>
    )
  }

  const level = levelFromCoverage(detail.activityCount, detail.coverageRate)
  const legend = COVERAGE_LEGEND.find((row) => row.level === level)
  const call = telHref(detail.headmanPhone)
  const phoneLabel = formatPhone(detail.headmanPhone)
  const addEventTo = `/events/new?settlement=${encodeURIComponent(detail.settlementId)}`

  return (
    <div className="settlement-page">
      <header className="settlement-hero">
        <div>
          <PageBackLink to={backTo}>Mahallelere dön</PageBackLink>
          <div className="settlement-hero-title">
            <h1>{detail.name}</h1>
            <span className={`settlement-cov-pill is-${level}`}>{legend?.label ?? 'Kaplama'}</span>
          </div>
          <p className="settlement-hero-meta">
            {detail.population != null ? (
              <span>{detail.population.toLocaleString('tr-TR')} kişi</span>
            ) : (
              <span>Nüfus yok</span>
            )}
            <span>{schools.length} okul</span>
            <span>{venueCount} tesis / alan</span>
            {detail.lastActivityDate ? <span>Son {formatDay(detail.lastActivityDate)}</span> : null}
          </p>
        </div>
        {canManage ? (
          <div className="settlement-hero-actions">
            <Link to={addEventTo} className="btn-primary">
              Etkinlik ekle
            </Link>
          </div>
        ) : null}
      </header>

      <nav className="settlement-tabs" aria-label="Mahalle bölümleri">
        <button type="button" className={tab === 'ozet' ? 'is-on' : ''} onClick={() => setTab('ozet')}>
          Özet
        </button>
        <button type="button" className={tab === 'okullar' ? 'is-on' : ''} onClick={() => setTab('okullar')}>
          Okullar
          <em>{schools.length}</em>
        </button>
        <button type="button" className={tab === 'tesis' ? 'is-on' : ''} onClick={() => setTab('tesis')}>
          Tesis ve alanlar
          <em>{venueCount}</em>
        </button>
        <button type="button" className={tab === 'etkinlikler' ? 'is-on' : ''} onClick={() => setTab('etkinlikler')}>
          Etkinlikler
          <em>{events.length}</em>
        </button>
      </nav>

      {tab === 'ozet' ? (
        <>
          <section className="settlement-overview">
            <div className="settlement-sheet">
              <article className="settlement-sheet-row">
                <h2>Durum</h2>
                <p className="settlement-stat">{coverageFact(detail, level)}</p>
                {detail.coverageRate != null ? (
                  <div className="settlement-bar" aria-hidden>
                    <div
                      className={`is-${level}`}
                      style={{ width: `${Math.min(100, Math.max(0, detail.coverageRate ?? 0))}%` }}
                    />
                  </div>
                ) : null}
                {detail.activityCount > 0 ? (
                  <p className="muted small">
                    {detail.activityCount} etkinlik
                    {detail.attendanceCount
                      ? ` · ${detail.attendanceCount.toLocaleString('tr-TR')} katılım`
                      : ''}
                  </p>
                ) : null}
              </article>

              <article className="settlement-sheet-row">
                <h2>Nüfus</h2>
                <p className="settlement-stat">{detail.population?.toLocaleString('tr-TR') ?? '—'}</p>
                <p className="muted small">
                  {detail.populationYear
                    ? `${detail.populationYear}${detail.populationIsOfficial ? ' ADNKS' : ''}`
                    : 'Resmi nüfus yok'}
                </p>
                <ul className="settlement-demo">
                  {detail.maleCount != null ? <li>{detail.maleCount.toLocaleString('tr-TR')} erkek</li> : null}
                  {detail.femaleCount != null ? <li>{detail.femaleCount.toLocaleString('tr-TR')} kadın</li> : null}
                  {detail.childCount != null ? <li>{detail.childCount.toLocaleString('tr-TR')} çocuk</li> : null}
                </ul>
              </article>

              <article className="settlement-sheet-row">
                <h2>Muhtar</h2>
                <p className="settlement-stat settlement-stat-name">{detail.headmanName || 'Kayıt yok'}</p>
                {phoneLabel ? (
                  <p className="settlement-sheet-contact">
                    {call ? <a href={call}>{phoneLabel}</a> : phoneLabel}
                    {call ? (
                      <a className="settlement-call" href={call}>
                        Ara
                      </a>
                    ) : null}
                  </p>
                ) : (
                  <p className="muted small">Telefon yok</p>
                )}
              </article>

              <article className="settlement-sheet-row is-last">
                <h2>Yerleşim</h2>
                <p className="settlement-sheet-links">
                  <button type="button" onClick={() => setTab('okullar')}>
                    {schools.length} okul
                  </button>
                  <button type="button" onClick={() => setTab('tesis')}>
                    {venueCount} tesis / alan
                  </button>
                  <button type="button" onClick={() => setTab('etkinlikler')}>
                    {events.length} etkinlik
                  </button>
                </p>
                {canManage ? (
                  <button type="button" className="settlement-sheet-edit" onClick={() => setEditRecord((v) => !v)}>
                    {editRecord ? 'Düzenlemeyi kapat' : 'Nüfus ve muhtarı düzenle'}
                  </button>
                ) : null}
              </article>
            </div>

            {feature ? (
              <article className="settlement-map-card" aria-label="Mahalle konumu">
                <MapContainer
                  center={[37.17, 37.35]}
                  zoom={13}
                  zoomControl={false}
                  attributionControl={false}
                  dragging={false}
                  scrollWheelZoom={false}
                  doubleClickZoom={false}
                  className="settlement-leaflet"
                >
                  <MiniFit feature={feature} />
                  <GeoJSON
                    data={feature as unknown as GeoJSON.GeoJsonObject}
                    pathOptions={{
                      color: level === 'none' ? '#208B5F' : '#ffffff',
                      weight: level === 'none' ? 2.2 : 2,
                      fillColor: coverageFill(level),
                      fillOpacity: level === 'none' ? 0.86 : 0.78,
                    }}
                  />
                </MapContainer>
              </article>
            ) : (
              <article className="settlement-map-card is-empty">
                <p className="muted">Harita sınırı yok.</p>
              </article>
            )}
          </section>

          {canManage && editRecord ? (
            <section className="settlement-edit-grid">
              <HeadmanEditor
                key={`${detail.settlementId}-h-${detail.headmanName}-${detail.headmanPhone}`}
                detail={detail}
                onSaved={load}
              />
              <PopulationEditor
                key={`${detail.settlementId}-p-${detail.populationYear}-${detail.population}`}
                detail={detail}
                onSaved={load}
              />
            </section>
          ) : null}

          {upcoming.length > 0 || done.length > 0 ? (
            <section className="settlement-follow">
              <div className="settlement-section-head">
                <h2>{upcoming.length > 0 ? 'Yaklaşan' : 'Son etkinlikler'}</h2>
                <button type="button" className="skills-link-btn" onClick={() => setTab('etkinlikler')}>
                  Tümü
                </button>
              </div>
              <ul className="settlement-event-list">
                {(upcoming.length > 0 ? upcoming : done).slice(0, 3).map((row) => (
                  <li key={row.id}>
                    <Link to={`/events/${row.id}`}>
                      <strong>{row.title}</strong>
                      <span>
                        {formatWhen(row.startAtUtc)}
                        {row.facilityName ? ` · ${row.facilityName}` : ''}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ) : categoryMix.length > 0 ? (
            <section className="settlement-mix" aria-label="Etkinlik türleri">
              <h2>Tür dağılımı</h2>
              <ul>
                {categoryMix.map((row) => (
                  <li key={row.label}>
                    <span>{row.label}</span>
                    <strong>{row.n}</strong>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
        </>
      ) : null}

      {tab === 'tesis' ? (
        <section className="panel settlement-block">
          <div className="settlement-section-head">
            <h2>Tesis ve alanlar</h2>
            <span className="muted small">
              {facilities.length} tesis · {areas.length} alan
            </span>
            {canManage ? (
              <button type="button" className="btn-secondary" onClick={() => setAreaForm('new')}>
                Alan ekle
              </button>
            ) : null}
          </div>
          {canManage && areaForm !== null ? (
            <AreaEditor
              key={areaForm === 'new' ? 'new-area' : areaForm.id}
              settlementId={detail.settlementId}
              area={areaForm === 'new' ? null : areaForm}
              onSaved={async () => {
                setAreaForm(null)
                await load()
              }}
              onCancel={() => setAreaForm(null)}
            />
          ) : null}
          {venueCount === 0 ? (
            <p className="muted">Bu mahallede kayıtlı belediye tesisi veya alanı yok.</p>
          ) : (
            <div className="settlement-venue-cols">
              <div>
                <h3>
                  Tesisler <em>{facilities.length}</em>
                </h3>
                {facilities.length === 0 ? (
                  <p className="muted small">Bu mahallede müdürlük tesisi yok.</p>
                ) : (
                  <ul className="settlement-schools">
                    {facilities.map((row) => (
                      <FacilityCard key={row.id} facility={row} />
                    ))}
                  </ul>
                )}
              </div>
              <div>
                <h3>
                  Alanlar <em>{areas.length}</em>
                </h3>
                {areas.length === 0 ? (
                  <p className="muted small">Bu mahallede kayıtlı açık alan yok.</p>
                ) : (
                  <ul className="settlement-schools">
                    {areas.map((row) => (
                      <AreaCard
                        key={row.id}
                        area={row}
                        canManage={canManage}
                        onEdit={() => setAreaForm(row)}
                        onDelete={() => void removeRecord('area', detail.settlementId, row.id, load)}
                      />
                    ))}
                  </ul>
                )}
              </div>
            </div>
          )}
        </section>
      ) : null}

      {tab === 'okullar' ? (
        <section className="panel settlement-block">
          <div className="settlement-section-head">
            <h2>Okullar</h2>
            <span className="muted small">
              {schools.length} okul
              {schoolStudents > 0 ? ` · ${schoolStudents.toLocaleString('tr-TR')} öğrenci` : ''}
            </span>
            {canManage ? (
              <button type="button" className="btn-secondary" onClick={() => setSchoolForm('new')}>
                Okul ekle
              </button>
            ) : null}
          </div>
          {canManage && schoolForm !== null ? (
            <SchoolEditor
              key={schoolForm === 'new' ? 'new-school' : schoolForm.id}
              settlementId={detail.settlementId}
              school={schoolForm === 'new' ? null : schoolForm}
              onSaved={async () => {
                setSchoolForm(null)
                await load()
              }}
              onCancel={() => setSchoolForm(null)}
            />
          ) : null}
          {schools.length === 0 ? (
            <p className="muted">Bu mahallede kayıtlı okul yok.</p>
          ) : (
            <ul className="settlement-schools">
              {schools.map((school) => (
                <SchoolCard
                  key={school.id}
                  school={school}
                  canManage={canManage}
                  onEdit={() => setSchoolForm(school)}
                  onDelete={() => void removeRecord('school', detail.settlementId, school.id, load)}
                />
              ))}
            </ul>
          )}
        </section>
      ) : null}

      {tab === 'etkinlikler' ? (
        <section className="panel settlement-block">
          <div className="settlement-section-head">
            <h2>Etkinlikler</h2>
            {canManage ? (
              <Link to={addEventTo} className="btn-secondary">
                Etkinlik ekle
              </Link>
            ) : null}
          </div>
          {events.length === 0 ? (
            <div className="settlement-empty">
              <p>Bu mahallede henüz etkinlik yok.</p>
              {canManage ? <Link to={addEventTo}>İlk programı ekle</Link> : null}
            </div>
          ) : (
            <div className="settlement-event-cols">
              <EventGroup title="Yaklaşan" items={upcoming} empty="Yaklaşan etkinlik yok." />
              <EventGroup title="Yapılan" items={done} empty="Yapılan kayıt yok." />
              {other.length > 0 ? <EventGroup title="Taslak / iptal" items={other} empty="" /> : null}
            </div>
          )}
        </section>
      ) : null}
    </div>
  )
}

function SchoolCard({
  school,
  canManage,
  onEdit,
  onDelete,
}: {
  school: SettlementSchool
  canManage?: boolean
  onEdit?: () => void
  onDelete?: () => void
}) {
  const call = telHref(school.principalPhone)
  const phoneLabel = formatPhone(school.principalPhone)
  return (
    <li className="settlement-school">
      <div>
        <em>{school.schoolType}</em>
        <strong>{school.name}</strong>
      </div>
      <p className="settlement-school-students">
        {school.studentCount != null
          ? `${school.studentCount.toLocaleString('tr-TR')} öğrenci`
          : 'Öğrenci sayısı yok'}
      </p>
      <p className="settlement-school-principal">
        {school.principalName || 'Müdür kaydı yok'}
        {phoneLabel ? (
          <>
            {' · '}
            {call ? <a href={call}>{phoneLabel}</a> : phoneLabel}
          </>
        ) : (
          <span className="muted"> · telefon yok</span>
        )}
      </p>
      {canManage ? (
        <p className="settlement-record-actions">
          <button type="button" className="skills-link-btn" onClick={onEdit}>
            Düzenle
          </button>
          <button type="button" className="skills-link-btn" onClick={onDelete}>
            Sil
          </button>
        </p>
      ) : null}
    </li>
  )
}

function FacilityCard({ facility }: { facility: SettlementFacility }) {
  const { hasPermission } = useAuth()
  const canOrg = hasPermission(PermissionCodes.OrganizationView)
  const call = telHref(facility.phone)
  const phoneLabel = formatPhone(facility.phone)
  const statusClass =
    facility.status === 1 ? 'is-good' : facility.status === 4 ? 'is-medium' : 'is-low'
  return (
    <li className="settlement-school">
      <div>
        <em>{facility.categoryName || 'Tesis'}</em>
        <strong>{facility.name}</strong>
      </div>
      <p className="settlement-school-students">
        <span className={`settlement-cov-pill ${statusClass}`}>{facility.statusLabel}</span>
      </p>
      {facility.managerName ? (
        <p className="settlement-school-principal">{facility.managerName}</p>
      ) : null}
      {phoneLabel ? (
        <p className="settlement-school-principal">
          {call ? <a href={call}>{phoneLabel}</a> : phoneLabel}
        </p>
      ) : null}
      {facility.address ? <p className="muted small">{facility.address}</p> : null}
      {facility.capacity != null ? (
        <p className="muted small">{facility.capacity.toLocaleString('tr-TR')} kişilik</p>
      ) : null}
      {canOrg ? (
        <p className="settlement-school-principal">
          <Link to={`/facilities?unit=${encodeURIComponent(facility.id)}`}>Tesis kaydına git</Link>
        </p>
      ) : null}
    </li>
  )
}

function AreaCard({
  area,
  canManage,
  onEdit,
  onDelete,
}: {
  area: SettlementArea
  canManage?: boolean
  onEdit?: () => void
  onDelete?: () => void
}) {
  return (
    <li className="settlement-school">
      <div>
        <em>{area.areaType}</em>
        <strong>{area.name}</strong>
      </div>
      {area.note ? <p className="settlement-school-principal">{area.note}</p> : null}
      {area.address ? <p className="muted small">{area.address}</p> : null}
      {canManage ? (
        <p className="settlement-record-actions">
          <button type="button" className="skills-link-btn" onClick={onEdit}>
            Düzenle
          </button>
          <button type="button" className="skills-link-btn" onClick={onDelete}>
            Sil
          </button>
        </p>
      ) : null}
    </li>
  )
}

function EventGroup({
  title,
  items,
  empty,
}: {
  title: string
  items: EventListItem[]
  empty: string
}) {
  return (
    <div>
      <h3>
        {title} <em>{items.length}</em>
      </h3>
      {items.length === 0 ? (
        empty ? <p className="muted small">{empty}</p> : null
      ) : (
        <ul className="settlement-event-list">
          {items.map((item) => (
            <li key={item.id}>
              <Link to={`/events/${item.id}`}>
                <strong>{item.title}</strong>
                <span>
                  {formatWhen(item.startAtUtc)}
                  {item.categoryLabel ? ` · ${item.categoryLabel}` : ''}
                  {item.facilityName ? ` · ${item.facilityName}` : ''}
                </span>
              </Link>
              <em className={`event-status-pill status-${item.status}`}>
                {eventPhaseLabel(item.status, item.startAtUtc)}
              </em>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
