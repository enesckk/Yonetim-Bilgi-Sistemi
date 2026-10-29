import { formatEventWhen } from '@/lib/eventsDates'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, Navigate, useParams, useSearchParams } from 'react-router-dom'
import { canSeeAllUnits } from '@/auth/roles'
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
import { COVERAGE_LEGEND, formatRate, levelFromCoverage } from '@/lib/coverage'
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

function formatWhen(iso: string, isDateOnly = false) { return formatEventWhen(iso, isDateOnly) }

function coverageFact(detail: SettlementSummary, level: 'low' | 'medium' | 'good') {
  if (detail.coverageRate == null) return 'Oran hesaplanamıyor'
  if (level === 'low') return 'Hizmet oranı düşük'
  if (level === 'medium') return 'Hizmet oranı orta'
  return 'Hizmet oranı yüksek'
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
  const loadSequence = useRef(0)
  const [eventsError, setEventsError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tab, setTab] = useState<'ozet' | 'okullar' | 'tesis' | 'etkinlikler'>('ozet')
  const [editRecord, setEditRecord] = useState(false)
  const [schoolForm, setSchoolForm] = useState<SettlementSchool | null | 'new'>(null)
  const [areaForm, setAreaForm] = useState<SettlementArea | null | 'new'>(null)
  const removeRecord = useSettlementDelete()

  const load = useCallback(async () => {
    if (!canView || !code) return
    const sequence = ++loadSequence.current
    setLoading(true)
    setError(null)
    setEventsError(null)
    try {
      const row = await fetchSettlementDetail(code)
      if (sequence !== loadSequence.current) return
      setDetail(row)
      setEvents([])
      try {
        const ev = await fetchEvents({ settlementId: row.settlementId })
        if (sequence === loadSequence.current) setEvents(ev.items)
      } catch {
        if (sequence === loadSequence.current) setEventsError('Etkinlik listesi yüklenemedi. Yeniden deneyin.')
      }
    } catch (err) {
      if (sequence === loadSequence.current) {
        setError(err instanceof ApiClientError ? err.message : 'Mahalle bilgisi yüklenemedi.')
        setDetail(null)
      }
    } finally {
      if (sequence === loadSequence.current) setLoading(false)
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
    <div className="settlement-page settlement-page-simple">
      <header className="settlement-hero">
        <div>
          <PageBackLink to={backTo}>Mahallelere dön</PageBackLink>
          <p className="settlement-municipality">Şehitkamil Belediyesi</p>
          <div className="settlement-hero-title">
            <h1>{detail.name}</h1>
            <span className={`settlement-cov-pill is-${level}`}>{legend?.label ?? 'Hizmet oranı'}</span>
          </div>
          <p className="settlement-hero-meta">
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
          <em>{eventsError ? '—' : events.length}</em>
        </button>
      </nav>

      {eventsError ? <div className="settlement-events-error" role="alert"><p>{eventsError}</p><button type="button" className="btn-secondary" onClick={() => void load()}>Yeniden dene</button></div> : null}

      {tab === 'ozet' ? (
        <>
          <section className="settlement-simple-overview" aria-label="Mahalle özeti">
            <div className="settlement-population-cards">
              <article><strong>{detail.population?.toLocaleString('tr-TR') ?? '—'}</strong><span>Toplam nüfus</span></article>
              <article className="is-female"><strong>{detail.femaleCount?.toLocaleString('tr-TR') ?? '—'}</strong><span>Kadın nüfusu</span></article>
              <article className="is-male"><strong>{detail.maleCount?.toLocaleString('tr-TR') ?? '—'}</strong><span>Erkek nüfusu</span></article>
            </div>
            <p className="settlement-population-source">
              {detail.population == null ? 'Nüfus bilgisi henüz eklenmemiş' : detail.populationYear ? `${detail.populationYear}${detail.populationIsOfficial ? ' ADNKS' : ''} nüfus verisi` : 'Nüfus yılı belirtilmemiş'}
              {detail.childCount != null ? ` · ${detail.childCount.toLocaleString('tr-TR')} çocuk` : ''}
            </p>

            <article className={`settlement-service-card is-${level}`} aria-label="Hizmetten yararlanma oranı">
              <div className="settlement-service-heading">
                <div><h2>Hizmetten yararlanma oranı</h2><p>{coverageFact(detail, level)}</p></div>
                <strong className="settlement-service-rate">{formatRate(detail.coverageRate)}</strong>
              </div>
              {detail.coverageRate != null ? <div className="settlement-service-track" role="progressbar" aria-label="Hizmetten yararlanma oranı" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.min(100, Math.max(0, detail.coverageRate))}><span style={{width: `${Math.min(100, Math.max(0, detail.coverageRate))}%`}} /></div> : null}
              <div className="settlement-service-bands" aria-label="Hizmet oranı aralıkları">{COVERAGE_LEGEND.map(row => <span key={row.level} className={`is-${row.level}${row.level === level ? ' is-current' : ''}`}><i aria-hidden="true" />{row.label}</span>)}</div>
              <dl className="settlement-service-stats">
                <div><dt>Etkinlik</dt><dd>{detail.activityCount.toLocaleString('tr-TR')}</dd></div>
                <div><dt>{detail.hasUniqueBeneficiaries ? 'Bildirilen yararlanıcı' : 'Toplam katılım'}</dt><dd>{(detail.hasUniqueBeneficiaries ? detail.uniqueBeneficiaryCount ?? 0 : detail.attendanceCount).toLocaleString('tr-TR')}</dd></div>
                <div><dt>Mahalle nüfusu</dt><dd>{detail.population?.toLocaleString('tr-TR') ?? '—'}</dd></div>
              </dl>
              {detail.activityCount === 0 ? <p className="settlement-service-note">Henüz etkinlik kaydı girilmedi.{detail.coverageRate === 0 ? ' Hizmetten yararlanma oranı %0 olarak gösterilir.' : ''}</p> : !detail.hasUniqueBeneficiaries ? <p className="settlement-service-note">Oran toplam katılım sayısına dayanır; aynı kişi farklı etkinliklerde yer alabilir.</p> : null}
            </article>

            <article className="settlement-contact-card" aria-label="Muhtar bilgisi">
              <div className="settlement-contact-person">
                <span className="settlement-contact-avatar" aria-hidden="true">{detail.headmanName?.trim().charAt(0).toLocaleUpperCase('tr-TR') || '—'}</span>
                <div><h2>{detail.headmanName || 'Muhtar bilgisi eklenmemiş'}</h2><p>{detail.headmanName?.startsWith('Muhtarlık Yok') ? 'OSB bölge yönetimi' : 'Mahalle muhtarı'}</p></div>
              </div>
              <div className="settlement-contact-actions">
                {phoneLabel ? <span className="settlement-contact-number">{call ? <a href={call}>{phoneLabel}</a> : phoneLabel}</span> : <span className="muted small">Telefon bilgisi eklenmemiş</span>}
                {call ? <a href={call} className="btn-primary settlement-contact-call"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M22 16.92v3a2 2 0 0 1-2.18 2A19.79 19.79 0 0 1 3.09 5.18 2 2 0 0 1 5.08 3h3a2 2 0 0 1 2 1.72c.12.96.35 1.9.69 2.79a2 2 0 0 1-.45 2.11L9.05 10.9a16 16 0 0 0 4.05 4.05l1.28-1.27a2 2 0 0 1 2.11-.45c.89.34 1.83.57 2.79.69A2 2 0 0 1 22 16.92Z" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg>Muhtarı ara</a> : null}
              </div>
            </article>

            {canManage ? <div className="settlement-summary-actions"><button type="button" className="btn-secondary" onClick={() => setEditRecord(v => !v)}>{editRecord ? 'Düzenlemeyi kapat' : 'Nüfus ve muhtarı düzenle'}</button></div> : null}
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
          {eventsError ? null : events.length === 0 ? (
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
                  {formatWhen(item.startAtUtc, item.isDateOnly)}
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
