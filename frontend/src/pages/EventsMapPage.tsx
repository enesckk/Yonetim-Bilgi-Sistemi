import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  GeoJSON,
  MapContainer,
  Marker,
  Popup,
  TileLayer,
  useMap,
  useMapEvents,
} from 'react-leaflet'
import L, { type LatLngBoundsExpression, type Layer, type Path } from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { fetchMapPins, type MapPin } from '@/api/eventsApi'
import { fetchOrganizationTree, type OrgNode } from '@/api/organizationApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import {
  isLikelyCulture,
  isLikelySchool,
  mahalleFillColor,
  pointInGeometry,
  type MahalleCollection,
  type MahalleFeature,
} from '@/lib/geo'
import { makeLeafletIcon } from '@/lib/leafletIcon'

const facilityIcon = makeLeafletIcon('map-pin-facility')
const schoolIcon = makeLeafletIcon('map-pin-school')
const cultureIcon = makeLeafletIcon('map-pin-culture')
const eventIcon = makeLeafletIcon('map-pin-event')

type FacilityPinKind = 'school' | 'culture' | 'facility'

function facilityPinKind(pin: MapPin): FacilityPinKind {
  if (isLikelySchool(pin.title, pin.subtitle, pin.categoryName)) return 'school'
  if (isLikelyCulture(pin.title, pin.subtitle, pin.categoryName)) return 'culture'
  return 'facility'
}

function pinIconFor(pin: MapPin) {
  if (pin.kind === 'event') return eventIcon
  const kind = facilityPinKind(pin)
  if (kind === 'school') return schoolIcon
  if (kind === 'culture') return cultureIcon
  return facilityIcon
}

function isInCurrentLocalMonth(iso: string) {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return false
  const now = new Date()
  return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth()
}

const SEHITKAMIL_BOUNDS: LatLngBoundsExpression = [
  [36.99, 36.99],
  [37.36, 37.7],
]

const SEHITKAMIL_CENTER: [number, number] = [37.17, 37.35]
const MAP_VIEW_KEY = 'py.events.map.view'
const ORG_FACILITY = 6

type BoundaryGeo = Parameters<typeof L.geoJSON>[0]

type SavedMapView = {
  lat: number
  lng: number
  zoom: number
}

function flattenFacilities(nodes: OrgNode[], acc: OrgNode[] = []): OrgNode[] {
  for (const n of nodes) {
    if (n.type === ORG_FACILITY) acc.push(n)
    if (n.children?.length) flattenFacilities(n.children, acc)
  }
  return acc
}

function readSavedView(): SavedMapView | null {
  try {
    const raw = localStorage.getItem(MAP_VIEW_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<SavedMapView>
    if (
      typeof parsed.lat === 'number' &&
      typeof parsed.lng === 'number' &&
      typeof parsed.zoom === 'number' &&
      Number.isFinite(parsed.lat) &&
      Number.isFinite(parsed.lng) &&
      Number.isFinite(parsed.zoom)
    ) {
      return { lat: parsed.lat, lng: parsed.lng, zoom: parsed.zoom }
    }
  } catch {
    /* ignore corrupt storage */
  }
  return null
}

function PersistMapView() {
  useMapEvents({
    moveend: (e) => {
      const map = e.target as L.Map
      const c = map.getCenter()
      const view: SavedMapView = { lat: c.lat, lng: c.lng, zoom: map.getZoom() }
      try {
        localStorage.setItem(MAP_VIEW_KEY, JSON.stringify(view))
      } catch {
        /* ignore quota / private mode */
      }
    },
  })
  return null
}

function DistrictFit({
  geo,
  skip,
}: {
  geo: BoundaryGeo | null
  skip: boolean
}) {
  const map = useMap()
  useEffect(() => {
    map.setMaxBounds(SEHITKAMIL_BOUNDS)
    map.setMinZoom(10)
    if (skip) return
    if (geo) {
      const layer = L.geoJSON(geo)
      const b = layer.getBounds()
      if (b.isValid()) {
        map.fitBounds(b.pad(0.06), { animate: false })
        return
      }
    }
    map.fitBounds(SEHITKAMIL_BOUNDS, { animate: false })
  }, [geo, map, skip])
  return null
}

function FitFeature({ feature }: { feature: MahalleFeature | null }) {
  const map = useMap()
  useEffect(() => {
    if (!feature) return
    const layer = L.geoJSON(feature as GeoJSON.GeoJsonObject)
    const b = layer.getBounds()
    if (b.isValid()) map.fitBounds(b.pad(0.12), { maxZoom: 14 })
  }, [feature, map])
  return null
}

function FocusLocation({
  lat,
  lng,
  zoom,
}: {
  lat: number
  lng: number
  zoom: number
}) {
  const map = useMap()
  useEffect(() => {
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) return
    map.flyTo([lat, lng], zoom, { duration: 0.55 })
    const timer = window.setTimeout(() => {
      map.eachLayer((layer) => {
        if (!(layer instanceof L.Marker)) return
        const ll = layer.getLatLng()
        if (Math.abs(ll.lat - lat) < 0.00015 && Math.abs(ll.lng - lng) < 0.00015) {
          layer.openPopup()
        }
      })
    }, 650)
    return () => window.clearTimeout(timer)
  }, [lat, lng, zoom, map])
  return null
}

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function findMahalleByQuery(collection: MahalleCollection, query: string): MahalleFeature | null {
  const q = query.trim()
  if (!q) return null
  const byId = collection.features.find((f) => String(f.properties.id) === q)
  if (byId) return byId
  const lower = q.toLocaleLowerCase('tr-TR')
  return (
    collection.features.find(
      (f) => f.properties.name.toLocaleLowerCase('tr-TR') === lower,
    ) ?? null
  )
}

export function EventsMapPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const canOrg = hasPermission(PermissionCodes.OrganizationView)
  const [searchParams, setSearchParams] = useSearchParams()

  const [pins, setPins] = useState<MapPin[]>([])
  const [boundary, setBoundary] = useState<BoundaryGeo | null>(null)
  const [mahalleler, setMahalleler] = useState<MahalleCollection | null>(null)
  const [selected, setSelected] = useState<MahalleFeature | null>(null)
  const [search, setSearch] = useState('')
  const [showFacilities, setShowFacilities] = useState(true)
  const [showSchools, setShowSchools] = useState(true)
  const [showCulture, setShowCulture] = useState(true)
  const [showEvents, setShowEvents] = useState(true)
  const [showMahalleler, setShowMahalleler] = useState(true)
  const [showMissing, setShowMissing] = useState(false)
  const [missingFacilities, setMissingFacilities] = useState<OrgNode[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [savedView] = useState(() => readSavedView())

  const selectMahalle = useCallback(
    (feature: MahalleFeature | null) => {
      setSelected(feature)
      setSearch(feature?.properties.name ?? '')
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          if (feature) {
            next.set('mahalle', feature.properties.id || feature.properties.name)
          } else {
            next.delete('mahalle')
          }
          return next
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      const wantFacilities = showFacilities || showSchools || showCulture
      const kinds = [wantFacilities ? 'facility' : null, showEvents ? 'event' : null]
        .filter(Boolean)
        .join(',')
      const tasks: Promise<unknown>[] = [
        fetchMapPins({ kinds: kinds || 'facility,event' }),
        fetch('/geo/sehitkamil-boundary.geojson').then((r) =>
          r.ok ? (r.json() as Promise<BoundaryGeo>) : null,
        ),
        fetch('/geo/sehitkamil-mahalleler.geojson').then((r) =>
          r.ok ? (r.json() as Promise<MahalleCollection>) : null,
        ),
      ]
      if (canOrg) tasks.push(fetchOrganizationTree())

      const results = await Promise.all(tasks)
      const data = results[0] as Awaited<ReturnType<typeof fetchMapPins>>
      const district = results[1] as BoundaryGeo | null
      const mahalle = results[2] as MahalleCollection | null
      setPins(data.pins)
      setBoundary(district)
      setMahalleler(mahalle)

      if (canOrg && results[3]) {
        const facilities = flattenFacilities(results[3] as OrgNode[])
        setMissingFacilities(
          facilities
            .filter((f) => f.latitude == null || f.longitude == null)
            .sort((a, b) => a.name.localeCompare(b.name, 'tr')),
        )
      } else {
        setMissingFacilities([])
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Harita yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canOrg, canView, showCulture, showEvents, showFacilities, showSchools])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!mahalleler) return
    const q = searchParams.get('mahalle')
    if (!q) {
      setSelected((cur) => (cur ? null : cur))
      return
    }
    const found = findMahalleByQuery(mahalleler, q)
    if (!found) return
    setSelected((cur) => (cur?.properties.id === found.properties.id ? cur : found))
    setSearch(found.properties.name)
  }, [mahalleler, searchParams])

  const visible = useMemo(
    () =>
      pins.filter((p) => {
        if (p.kind === 'event') return showEvents
        if (p.kind === 'facility') {
          const kind = facilityPinKind(p)
          if (kind === 'school') return showSchools
          if (kind === 'culture') return showCulture
          return showFacilities
        }
        return true
      }),
    [pins, showCulture, showEvents, showFacilities, showSchools],
  )

  const searchHits = useMemo(() => {
    if (!mahalleler || !search.trim()) return []
    const q = search.trim().toLocaleLowerCase('tr-TR')
    return mahalleler.features
      .filter((f) => f.properties.name.toLocaleLowerCase('tr-TR').includes(q))
      .slice(0, 12)
  }, [mahalleler, search])

  const selectedPins = useMemo(() => {
    const empty = {
      facilities: [] as MapPin[],
      events: [] as MapPin[],
      schools: [] as MapPin[],
      culture: [] as MapPin[],
      thisMonthEvents: 0,
    }
    if (!selected) return empty
    const inside = pins.filter((p) =>
      pointInGeometry(p.latitude, p.longitude, selected.geometry),
    )
    const facilityPins = inside.filter((p) => p.kind === 'facility')
    const events = inside.filter((p) => p.kind === 'event')
    const schools = facilityPins.filter((p) => facilityPinKind(p) === 'school')
    const culture = facilityPins.filter((p) => facilityPinKind(p) === 'culture')
    const facilities = facilityPins.filter((p) => facilityPinKind(p) === 'facility')
    const thisMonthEvents = events.filter((p) => p.startAtUtc && isInCurrentLocalMonth(p.startAtUtc))
      .length
    return { facilities, events, schools, culture, thisMonthEvents }
  }, [pins, selected])

  const mahalleStyle = useCallback(
    (feature?: GeoJSON.Feature) => {
      const id = String(feature?.properties?.id ?? feature?.properties?.name ?? '')
      const name = String(feature?.properties?.name ?? '')
      const isSel = selected?.properties.id === id
      return {
        color: isSel ? '#0f4c81' : '#ffffff',
        weight: isSel ? 2.5 : 1,
        opacity: 0.95,
        fillColor: mahalleFillColor(id || name),
        fillOpacity: isSel ? 0.55 : 0.38,
      }
    },
    [selected],
  )

  const onEachMahalle = useCallback(
    (feature: GeoJSON.Feature, layer: Layer) => {
      const props = feature.properties as MahalleFeature['properties'] | null
      if (!props?.name) return
      layer.bindTooltip(props.name, {
        sticky: true,
        direction: 'top',
        opacity: 0.95,
        className: 'mahalle-tooltip',
      })
      layer.on({
        mouseover: (e) => {
          const path = e.target as Path
          path.setStyle({ weight: 2.5, fillOpacity: 0.55 })
          path.bringToFront()
        },
        mouseout: (e) => {
          const path = e.target as Path
          const id = String(props.id)
          const isSel = selected?.properties.id === id
          path.setStyle({
            color: isSel ? '#0f4c81' : '#ffffff',
            weight: isSel ? 2.5 : 1,
            fillOpacity: isSel ? 0.55 : 0.38,
          })
        },
        click: () => {
          selectMahalle({
            type: 'Feature',
            properties: { id: String(props.id), name: props.name },
            geometry: feature.geometry as MahalleFeature['geometry'],
          })
        },
      })
    },
    [selectMahalle, selected],
  )

  const focusLat = Number(searchParams.get('lat'))
  const focusLng = Number(searchParams.get('lng'))
  const focusZoomRaw = Number(searchParams.get('zoom'))
  const hasFocus = Number.isFinite(focusLat) && Number.isFinite(focusLng)
  const focusZoom = Number.isFinite(focusZoomRaw) ? Math.min(18, Math.max(11, focusZoomRaw)) : 16

  const skipDistrictFit =
    Boolean(savedView) ||
    Boolean(selected) ||
    Boolean(searchParams.get('mahalle')) ||
    hasFocus
  const mapCenter: [number, number] = hasFocus
    ? [focusLat, focusLng]
    : savedView
      ? [savedView.lat, savedView.lng]
      : SEHITKAMIL_CENTER
  const mapZoom = hasFocus ? focusZoom : (savedView?.zoom ?? 11)

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Haritayı görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="events-map-page employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Şehitkamil haritası</h2>
              <div className="stat-chip mobile-inline-chip">
                {loading
                  ? 'Yükleniyor…'
                  : `${mahalleler?.features.length ?? 0} mahalle · ${visible.length} pin`}
              </div>
            </div>
            <p className="muted small employees-toolbar-lead">
              Mahalleye tıklayın; tesis, okul, kültür ve etkinlikler sağ panelde listelenir.
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <div className="stat-chip desktop-inline-chip">
              {loading
                ? 'Yükleniyor…'
                : `${mahalleler?.features.length ?? 0} mahalle · ${visible.length} pin`}
            </div>
            <div className="events-map-legend" role="group" aria-label="Harita katmanları">
              <label className="events-map-legend-item">
                <span className="events-map-legend-dot is-facility" aria-hidden />
                <input
                  type="checkbox"
                  checked={showFacilities}
                  onChange={(e) => setShowFacilities(e.target.checked)}
                />
                <span>Tesis</span>
              </label>
              <label className="events-map-legend-item">
                <span className="events-map-legend-dot is-school" aria-hidden />
                <input
                  type="checkbox"
                  checked={showSchools}
                  onChange={(e) => setShowSchools(e.target.checked)}
                />
                <span>Okul</span>
              </label>
              <label className="events-map-legend-item">
                <span className="events-map-legend-dot is-culture" aria-hidden />
                <input
                  type="checkbox"
                  checked={showCulture}
                  onChange={(e) => setShowCulture(e.target.checked)}
                />
                <span>Kültür</span>
              </label>
              <label className="events-map-legend-item">
                <span className="events-map-legend-dot is-event" aria-hidden />
                <input
                  type="checkbox"
                  checked={showEvents}
                  onChange={(e) => setShowEvents(e.target.checked)}
                />
                <span>Etkinlik</span>
              </label>
            </div>
            <div className="map-layers">
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={showMahalleler}
                  onChange={(e) => setShowMahalleler(e.target.checked)}
                />
                <span>Mahalleler</span>
              </label>
              {canOrg && missingFacilities.length > 0 ? (
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={showMissing}
                    onChange={(e) => setShowMissing(e.target.checked)}
                  />
                  <span>Konum eksik ({missingFacilities.length})</span>
                </label>
              ) : null}
            </div>
            <Link to="/events/list" className="btn-secondary">
              Liste
            </Link>
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="events-map-layout">
          <div className="events-map-shell">
            <div className="map-search-bar">
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Mahalle ara veya seç…"
                aria-label="Mahalle ara"
              />
              {searchHits.length > 0 && (!selected || search !== selected.properties.name) ? (
                <ul className="map-search-results" role="listbox">
                  {searchHits.map((f) => (
                    <li key={f.properties.id}>
                      <button type="button" onClick={() => selectMahalle(f)}>
                        {f.properties.name}
                      </button>
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>

            <MapContainer
              center={mapCenter}
              zoom={mapZoom}
              className="events-leaflet-map"
              scrollWheelZoom
              maxBounds={SEHITKAMIL_BOUNDS}
              maxBoundsViscosity={0.9}
              minZoom={10}
            >
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> · Mahalle: Gaziantep BB (CC BY 4.0)'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              <PersistMapView />
              <DistrictFit geo={boundary} skip={skipDistrictFit} />
              <FitFeature feature={selected} />
              {hasFocus ? <FocusLocation lat={focusLat} lng={focusLng} zoom={focusZoom} /> : null}
              {boundary && !Array.isArray(boundary) ? (
                <GeoJSON
                  data={boundary}
                  pathOptions={{
                    color: '#0f4c81',
                    weight: 2,
                    fillOpacity: 0,
                    dashArray: '4 4',
                  }}
                />
              ) : null}
              {showMahalleler && mahalleler ? (
                <GeoJSON
                  key={`mahalle-${selected?.properties.id ?? 'none'}-${mahalleler.features.length}`}
                  data={mahalleler as GeoJSON.GeoJsonObject}
                  style={mahalleStyle}
                  onEachFeature={onEachMahalle}
                />
              ) : null}
              {visible.map((pin) => (
                <Marker
                  key={`${pin.kind}-${pin.id}`}
                  position={[pin.latitude, pin.longitude]}
                  icon={pinIconFor(pin)}
                >
                  <Popup>
                    <strong>{pin.title}</strong>
                    <div className="muted small">
                      {pin.kind === 'event'
                        ? 'Etkinlik'
                        : facilityPinKind(pin) === 'school'
                          ? 'Okul'
                          : facilityPinKind(pin) === 'culture'
                            ? 'Kültür'
                            : 'Tesis'}
                      {pin.statusLabel ? ` · ${pin.statusLabel}` : ''}
                    </div>
                    {pin.categoryName ? (
                      <div className="muted small">{pin.categoryName}</div>
                    ) : null}
                    {pin.subtitle ? <div>{pin.subtitle}</div> : null}
                    {pin.startAtUtc ? (
                      <div className="small">{formatWhen(pin.startAtUtc)}</div>
                    ) : null}
                    {pin.linkPath ? (
                      <div style={{ marginTop: 6 }}>
                        <Link to={pin.linkPath}>Detaya git</Link>
                      </div>
                    ) : null}
                  </Popup>
                </Marker>
              ))}
            </MapContainer>
          </div>

          <aside className="events-map-detail" aria-live="polite">
            {showMissing && missingFacilities.length > 0 ? (
              <section className="events-map-detail-section" style={{ marginBottom: '1rem' }}>
                <header className="events-map-detail-head">
                  <div>
                    <p className="dash-hero-brand">Tesis</p>
                    <h3 className="section-title" style={{ fontSize: '1.15rem', margin: 0 }}>
                      Konum eksik
                    </h3>
                  </div>
                  <Link to="/events/facilities-locations?missing=1" className="btn-secondary">
                    Düzenle
                  </Link>
                </header>
                <p className="muted small">
                  {missingFacilities.length} tesisin enlem/boylamı yok. Konum atamak için tesis
                  konumları sayfasına gidin.
                </p>
                <ul>
                  {missingFacilities.slice(0, 40).map((f) => (
                    <li key={f.id}>
                      <Link to={`/events/facilities-locations?missing=1`}>
                        <strong>{f.name}</strong>
                        <em>
                          {f.facilityCategoryName ?? f.typeLabel}
                          {f.address ? ` · ${f.address}` : ''}
                        </em>
                      </Link>
                    </li>
                  ))}
                </ul>
                {missingFacilities.length > 40 ? (
                  <p className="muted small">
                    +{missingFacilities.length - 40} daha —{' '}
                    <Link to="/events/facilities-locations?missing=1">tümünü gör</Link>
                  </p>
                ) : null}
              </section>
            ) : null}

            {!selected ? (
              !showMissing || missingFacilities.length === 0 ? (
                <div className="events-map-detail-empty">
                  <h3 className="section-title" style={{ fontSize: '1.05rem' }}>
                    Mahalle detayı
                  </h3>
                  <p className="muted">
                    Haritadan bir mahalle / köye tıklayın veya yukarıdan arayın. Tesis, okul benzeri
                    kayıtlar ve etkinlikler burada listelenir.
                  </p>
                </div>
              ) : null
            ) : (
              <>
                <header className="events-map-detail-head">
                  <div>
                    <p className="dash-hero-brand">Mahalle</p>
                    <h3 className="section-title" style={{ fontSize: '1.15rem', margin: 0 }}>
                      {selected.properties.name}
                    </h3>
                  </div>
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={() => selectMahalle(null)}
                  >
                    Kapat
                  </button>
                </header>

                <div className="events-map-detail-kpis">
                  <div>
                    <span>Tesis</span>
                    <strong>{selectedPins.facilities.length}</strong>
                  </div>
                  <div>
                    <span>Okul*</span>
                    <strong>{selectedPins.schools.length}</strong>
                  </div>
                  <div>
                    <span>Kültür*</span>
                    <strong>{selectedPins.culture.length}</strong>
                  </div>
                  <div>
                    <span>Etkinlik</span>
                    <strong>{selectedPins.events.length}</strong>
                  </div>
                  <div>
                    <span>Bu ay</span>
                    <strong>{selectedPins.thisMonthEvents}</strong>
                  </div>
                </div>

                <DetailSection title="Tesisler" empty="Bu mahallede pin’li tesis yok.">
                  {selectedPins.facilities.map((p) => (
                    <DetailRow key={p.id} pin={p} />
                  ))}
                </DetailSection>

                <DetailSection
                  title="Okullar"
                  empty="Adında okul geçen tesis kaydı yok. (Ayrı okul modülü henüz yok.)"
                >
                  {selectedPins.schools.map((p) => (
                    <DetailRow key={p.id} pin={p} />
                  ))}
                </DetailSection>

                <DetailSection
                  title="Kültür"
                  empty="Kültür / sanat benzeri tesis kaydı yok."
                >
                  {selectedPins.culture.map((p) => (
                    <DetailRow key={p.id} pin={p} />
                  ))}
                </DetailSection>

                <DetailSection title="Etkinlikler" empty="Bu mahallede pin’li etkinlik yok.">
                  {selectedPins.events.map((p) => (
                    <DetailRow key={p.id} pin={p} />
                  ))}
                </DetailSection>

                <p className="muted small" style={{ marginTop: '0.75rem' }}>
                  * Okul ve kültür, tesis adı / kategorisinden tahmin edilir. «Bu ay» yerel takvim
                  ayına göre etkinlik başlangıçlarıdır. Konum ataması pin koordinatına göredir.
                </p>
              </>
            )}
          </aside>
        </div>
      </section>
    </div>
  )
}

function DetailSection({
  title,
  empty,
  children,
}: {
  title: string
  empty: string
  children: ReactNode
}) {
  const list = Array.isArray(children) ? children.filter(Boolean) : children ? [children] : []
  return (
    <section className="events-map-detail-section">
      <h4>
        {title}
        <span>{list.length}</span>
      </h4>
      {list.length === 0 ? <p className="muted small">{empty}</p> : <ul>{list}</ul>}
    </section>
  )
}

function DetailRow({ pin }: { pin: MapPin }) {
  const body = (
    <>
      <strong>{pin.title}</strong>
      <em>
        {pin.kind === 'event'
          ? (pin.statusLabel ?? 'Etkinlik')
          : (pin.categoryName ?? 'Tesis')}
        {pin.subtitle ? ` · ${pin.subtitle}` : ''}
        {pin.startAtUtc ? ` · ${formatWhen(pin.startAtUtc)}` : ''}
      </em>
    </>
  )
  return (
    <li>
      {pin.linkPath ? <Link to={pin.linkPath}>{body}</Link> : <div>{body}</div>}
    </li>
  )
}
