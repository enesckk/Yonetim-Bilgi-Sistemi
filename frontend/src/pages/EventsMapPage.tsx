import { useCallback, useEffect, useMemo, useState } from 'react'
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { GeoJSON, MapContainer, useMap } from 'react-leaflet'
import L, { type LatLngBoundsExpression, type Layer, type Path } from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { fetchSettlementSummaries, type SettlementSummary } from '@/api/mapApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { canSeeAllUnits } from '@/auth/roles'
import {
  COVERAGE_LEGEND,
  coverageFill,
  dateRangeForPreset,
  formatRate,
  levelFromCoverage,
  type DatePreset,
} from '@/lib/coverage'
import { type MahalleCollection, type MahalleFeature } from '@/lib/geo'
import { loadDistrictBoundary, loadMahalleler } from '@/lib/geoCache'

type BoundaryGeo = Parameters<typeof L.geoJSON>[0]

const SEHITKAMIL_BOUNDS: LatLngBoundsExpression = [
  [36.99, 36.99],
  [37.36, 37.7],
]
const SEHITKAMIL_CENTER: [number, number] = [37.17, 37.35]

const EVENT_CATEGORIES = [
  { value: '', label: 'Tüm türler' },
  { value: 'education', label: 'Eğitim' },
  { value: 'health', label: 'Sağlık' },
  { value: 'social_support', label: 'Sosyal Destek' },
  { value: 'culture', label: 'Kültür / Sanat' },
  { value: 'trip', label: 'Gezi' },
  { value: 'sports', label: 'Spor' },
  { value: 'youth', label: 'Çocuk / Gençlik' },
  { value: 'women', label: 'Kadın' },
  { value: 'elderly', label: 'Yaşlı' },
  { value: 'other', label: 'Diğer' },
]

const DATE_PRESETS: { value: DatePreset; label: string }[] = [
  { value: 'quarter', label: 'Son 3 ay' },
  { value: 'year', label: 'Bu yıl' },
  { value: 'lastYear', label: 'Geçen yıl' },
  { value: 'all', label: 'Tüm zamanlar' },
  { value: 'custom', label: 'Özel tarih' },
]

function DistrictFit({ geo, resetToken }: { geo: BoundaryGeo | null; resetToken: number }) {
  const map = useMap()

  const fit = useCallback(() => {
    map.invalidateSize()
    map.setMaxBounds(SEHITKAMIL_BOUNDS)
    map.setMinZoom(10)
    if (geo) {
      const layer = L.geoJSON(geo)
      const b = layer.getBounds()
      if (b.isValid()) {
        map.fitBounds(b.pad(0.04), { animate: false })
        map.setMaxBounds(b.pad(0.08))
        return
      }
    }
    map.fitBounds(SEHITKAMIL_BOUNDS, { animate: false })
  }, [geo, map])

  useEffect(() => {
    fit()
  }, [fit, resetToken])

  useEffect(() => {
    const bump = () => {
      window.setTimeout(() => map.invalidateSize(), 60)
      window.setTimeout(() => map.invalidateSize(), 280)
    }
    window.addEventListener('resize', bump)
    window.addEventListener('sidebar:toggled', bump)
    bump()
    return () => {
      window.removeEventListener('resize', bump)
      window.removeEventListener('sidebar:toggled', bump)
    }
  }, [map])

  return null
}

function boxesOverlap(a: DOMRect, b: DOMRect, pad: number) {
  return !(a.right + pad <= b.left || b.right + pad <= a.left || a.bottom + pad <= b.top || b.bottom + pad <= a.top)
}

function pruneSettlementLabels(map: L.Map) {
  type Candidate = {
    el: HTMLElement
    rank: number
    area: number
    point: L.Point
    pxW: number
    pxH: number
  }

  const zoom = map.getZoom()
  const filtering = Boolean(map.getContainer().closest('.is-filtering'))
  const items: Candidate[] = []
  const containerBox = map.getContainer().getBoundingClientRect()

  map.eachLayer((layer) => {
    const tooltip = layer.getTooltip?.()
    const el = tooltip?.getElement() as HTMLElement | undefined
    if (!el?.classList.contains('settlement-label')) return
    const bounded = layer as Layer & { getBounds?: () => L.LatLngBounds }
    if (!bounded.getBounds) return
    const bounds = bounded.getBounds()
    if (!bounds.isValid()) return
    const sw = map.latLngToContainerPoint(bounds.getSouthWest())
    const ne = map.latLngToContainerPoint(bounds.getNorthEast())
    const pxW = Math.abs(ne.x - sw.x)
    const pxH = Math.abs(ne.y - sw.y)
    const rank = el.classList.contains('is-low')
      ? 0
      : el.classList.contains('is-medium')
        ? 1
        : el.classList.contains('is-good')
          ? 2
          : 3
    items.push({
      el,
      rank,
      area: pxW * pxH,
      point: map.latLngToContainerPoint(bounds.getCenter()),
      pxW,
      pxH,
    })
  })

  for (const it of items) it.el.classList.remove('is-hidden-overlap')

  const minW = filtering ? 18 : zoom >= 14 ? 14 : zoom >= 13.1 ? 22 : zoom >= 12.3 ? 32 : 40
  const minH = minW * 0.42
  const pad = filtering ? 8 : zoom >= 14 ? 8 : zoom >= 13.1 ? 11 : zoom >= 12.3 ? 13 : 15

  items.sort((a, b) => a.rank - b.rank || b.area - a.area)

  const measurable: Candidate[] = []
  for (const it of items) {
    const hideNone = !filtering && zoom < 13.15 && it.rank === 3
    const tooSmall = it.pxW < minW || it.pxH < minH
    if (hideNone || tooSmall) {
      it.el.classList.add('is-hidden-overlap')
      continue
    }
    measurable.push(it)
  }

  void map.getContainer().offsetHeight

  const kept: DOMRect[] = []
  for (const it of measurable) {
    const live = it.el.getBoundingClientRect()
    const box =
      live.width >= 2 && live.height >= 2
        ? live
        : new DOMRect(
            containerBox.left + it.point.x - 41,
            containerBox.top + it.point.y - 8,
            82,
            16,
          )
    const gap = pad + (it.rank === 0 ? 6 : 0)
    if (kept.some((other) => boxesOverlap(box, other, gap))) {
      it.el.classList.add('is-hidden-overlap')
    } else {
      kept.push(box)
    }
  }
}

function MapLabelController({ nonce }: { nonce: string }) {
  const map = useMap()

  useEffect(() => {
    const apply = () => pruneSettlementLabels(map)
    map.on('zoomend', apply)
    map.on('moveend', apply)
    const timers = [80, 280, 700].map((ms) => window.setTimeout(apply, ms))
    apply()
    return () => {
      map.off('zoomend', apply)
      map.off('moveend', apply)
      timers.forEach((id) => window.clearTimeout(id))
    }
  }, [map, nonce])

  return null
}

function invertBoundary(geo: BoundaryGeo | null): GeoJSON.Feature | null {
  if (!geo || Array.isArray(geo) || geo.type !== 'FeatureCollection') return null
  const first = (geo as GeoJSON.FeatureCollection).features[0]
  if (!first?.geometry) return null
  const world: GeoJSON.Position[] = [
    [-180, -90],
    [180, -90],
    [180, 90],
    [-180, 90],
    [-180, -90],
  ]
  const holes: GeoJSON.Position[][] = []
  if (first.geometry.type === 'Polygon') {
    holes.push(...first.geometry.coordinates)
  } else if (first.geometry.type === 'MultiPolygon') {
    for (const poly of first.geometry.coordinates) holes.push(...poly)
  } else {
    return null
  }
  return {
    type: 'Feature',
    properties: { mask: true },
    geometry: { type: 'Polygon', coordinates: [world, ...holes] },
  }
}

function escapeHtml(value: string) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}

function coverageRank(level: 'none' | 'low' | 'medium' | 'good') {
  if (level === 'low') return 0
  if (level === 'medium') return 1
  if (level === 'good') return 2
  return 3
}

function coverageCaption(s: SettlementSummary) {
  const level = levelFromCoverage(s.activityCount, s.coverageRate)
  if (level === 'none') return 'Etkinlik yok'
  const rate = formatRate(s.coverageRate)
  return `${rate} · ${s.activityCount} etkinlik`
}

function MahalleViewSwitch({
  view,
  onChange,
}: {
  view: 'map' | 'cards'
  onChange: (next: 'map' | 'cards') => void
}) {
  return (
    <div className="mahalle-view-switch" role="tablist" aria-label="Mahalle görünümü">
      <button
        type="button"
        role="tab"
        className={view === 'map' ? 'is-active' : ''}
        aria-selected={view === 'map'}
        onClick={() => onChange('map')}
      >
        Harita
      </button>
      <button
        type="button"
        role="tab"
        className={view === 'cards' ? 'is-active' : ''}
        aria-selected={view === 'cards'}
        onClick={() => onChange('cards')}
      >
        Kartlar
      </button>
    </div>
  )
}

export function EventsMapPage() {
  const { user } = useAuth()
  if (!canSeeAllUnits(user)) return <Navigate to="/events/calendar" replace />
  return <EventsMapPageInner />
}

function EventsMapPageInner() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.EventsView)
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()

  const view: 'map' | 'cards' = searchParams.get('view') === 'cards' ? 'cards' : 'map'

  const [boundary, setBoundary] = useState<BoundaryGeo | null>(null)
  const [mahalleler, setMahalleler] = useState<MahalleCollection | null>(null)
  const [summaries, setSummaries] = useState<SettlementSummary[]>([])
  const [search, setSearch] = useState(searchParams.get('q') ?? '')
  const [preset, setPreset] = useState<DatePreset>('year')
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')
  const [category, setCategory] = useState('')
  const [filterOpen, setFilterOpen] = useState(false)
  const [coverageFilter, setCoverageFilter] = useState<(typeof COVERAGE_LEGEND)[number]['level'] | null>(
    null,
  )
  const [resetToken, setResetToken] = useState(0)
  const [cardSort, setCardSort] = useState<'name' | 'coverage'>('name')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [geoError, setGeoError] = useState<string | null>(null)

  const loadGeo = useCallback(async () => {
    try {
      const [district, mahalle] = await Promise.all([loadDistrictBoundary(), loadMahalleler()])
      setBoundary((district as BoundaryGeo | null) ?? null)
      setMahalleler(mahalle)
      if (!mahalle) setGeoError('Mahalle sınır verisi yüklenemedi.')
    } catch {
      setGeoError('Harita sınır verisi yüklenemedi.')
    }
  }, [])

  const loadSummary = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      const range =
        preset === 'custom'
          ? {
              fromUtc: customFrom ? new Date(customFrom).toISOString() : undefined,
              toUtc: customTo ? new Date(customTo).toISOString() : undefined,
            }
          : dateRangeForPreset(preset)
      const data = await fetchSettlementSummaries({
        ...range,
        category: category || undefined,
      })
      setSummaries(data.items)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Harita özeti yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canView, category, customFrom, customTo, preset])

  useEffect(() => {
    if (view === 'map') void loadGeo()
  }, [loadGeo, view])

  useEffect(() => {
    void loadSummary()
  }, [loadSummary])

  useEffect(() => {
    const code = searchParams.get('mahalle')
    if (code) navigate(`/events/settlements/${encodeURIComponent(code)}`, { replace: true })
  }, [navigate, searchParams])

  const byCode = useMemo(() => {
    const map = new Map<string, SettlementSummary>()
    for (const s of summaries) map.set(s.officialCode, s)
    return map
  }, [summaries])

  const coverageCounts = useMemo(() => {
    const counts = { none: 0, low: 0, medium: 0, good: 0 }
    for (const s of summaries) counts[levelFromCoverage(s.activityCount, s.coverageRate)] += 1
    return counts
  }, [summaries])

  const activeLegend = coverageFilter
    ? COVERAGE_LEGEND.find((row) => row.level === coverageFilter) ?? null
    : null

  const setView = useCallback(
    (next: 'map' | 'cards') => {
      const nextParams = new URLSearchParams(searchParams)
      if (next === 'cards') nextParams.set('view', 'cards')
      else nextParams.delete('view')
      setSearchParams(nextParams, { replace: true })
    },
    [searchParams, setSearchParams],
  )

  const searchHits = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr-TR')
    if (!q) return []
    return summaries
      .filter((s) => s.name.toLocaleLowerCase('tr-TR').includes(q))
      .slice(0, 12)
  }, [search, summaries])

  const visibleCards = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr-TR')
    const rows = summaries.filter((s) => {
      const level = levelFromCoverage(s.activityCount, s.coverageRate)
      if (coverageFilter && level !== coverageFilter) return false
      if (!q) return true
      return s.name.toLocaleLowerCase('tr-TR').includes(q)
    })
    rows.sort((a, b) => {
      if (cardSort === 'coverage') {
        const da = coverageRank(levelFromCoverage(a.activityCount, a.coverageRate))
        const db = coverageRank(levelFromCoverage(b.activityCount, b.coverageRate))
        if (da !== db) return da - db
      }
      return a.name.localeCompare(b.name, 'tr')
    })
    return rows
  }, [cardSort, coverageFilter, search, summaries])

  const openSettlement = useCallback(
    (code: string) => {
      const suffix = view === 'cards' ? '?from=cards' : ''
      navigate(`/events/settlements/${encodeURIComponent(code)}${suffix}`)
    },
    [navigate, view],
  )

  const toggleCoverageFilter = useCallback((level: (typeof COVERAGE_LEGEND)[number]['level']) => {
    setCoverageFilter((prev) => (prev === level ? null : level))
  }, [])

  const mask = useMemo(() => invertBoundary(boundary), [boundary])

  const mahalleStyle = useCallback(
    (feature?: GeoJSON.Feature) => {
      const id = String(feature?.properties?.id ?? '')
      const summary = byCode.get(id)
      const level = levelFromCoverage(summary?.activityCount, summary?.coverageRate)
      const dimmed = coverageFilter != null && level !== coverageFilter
      const isLow = !dimmed && level === 'low'
      return {
        color: dimmed ? '#2a2a2a' : isLow ? '#9B1C1C' : '#ffffff',
        weight: dimmed ? 0.8 : isLow ? 2 : 1.15,
        opacity: dimmed ? 0.55 : 0.95,
        fillColor: dimmed ? '#0d0d0d' : coverageFill(level),
        fillOpacity: dimmed ? 0.78 : level === 'none' ? 0.88 : 0.82,
      }
    },
    [byCode, coverageFilter],
  )

  const onEachMahalle = useCallback(
    (feature: GeoJSON.Feature, layer: Layer) => {
      const props = feature.properties as MahalleFeature['properties'] | null
      if (!props?.name) return
      const summary = byCode.get(String(props.id))
      const level = levelFromCoverage(summary?.activityCount, summary?.coverageRate)
      const dimmed = coverageFilter != null && level !== coverageFilter
      layer.bindTooltip(
        `<span class="settlement-label-name">${escapeHtml(props.name)}</span>`,
        {
          permanent: true,
          direction: 'center',
          className: ['settlement-label', `is-${level}`, dimmed ? 'is-dimmed' : '']
            .filter(Boolean)
            .join(' '),
          opacity: 1,
          sticky: false,
          interactive: false,
        },
      )
      layer.on({
        mouseover: (e) => {
          const path = e.target as Path
          path.setStyle({
            weight: 2.6,
            fillOpacity: dimmed ? 0.86 : 0.9,
            color: dimmed ? '#4a4a4a' : '#208B5F',
          })
          path.bringToFront()
          const tip = (path as Layer).getTooltip()?.getElement()
          if (tip) tip.classList.add('is-force-show')
        },
        mouseout: (e) => {
          const path = e.target as Path
          const isLow = !dimmed && level === 'low'
          path.setStyle({
            color: dimmed ? '#2a2a2a' : isLow ? '#9B1C1C' : '#ffffff',
            weight: dimmed ? 0.8 : isLow ? 2 : 1.15,
            fillOpacity: dimmed ? 0.78 : level === 'none' ? 0.88 : 0.82,
          })
          const tip = (path as Layer).getTooltip()?.getElement()
          if (tip) tip.classList.remove('is-force-show')
        },
        click: () => openSettlement(String(props.id)),
      })
    },
    [byCode, coverageFilter, openSettlement],
  )

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Haritayı görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  if (view === 'cards') {
    return (
      <div className="events-map-page is-mahalle-cards">
        {error ? (
          <div className="ops-map-banner" role="alert">
            <p>{error}</p>
            <button type="button" className="btn-secondary" onClick={() => void loadSummary()}>
              Yeniden dene
            </button>
          </div>
        ) : null}

        <header className="mahalle-cards-head">
          <div>
            <h2>Mahalleler</h2>
            <p className="muted small">
              {loading ? 'Yükleniyor…' : `${visibleCards.length} mahalle`}
              {coverageFilter
                ? ` · ${COVERAGE_LEGEND.find((row) => row.level === coverageFilter)?.label}`
                : ''}
            </p>
          </div>
          <MahalleViewSwitch view={view} onChange={setView} />
        </header>

        <div className="mahalle-cards-toolbar">
          <label className="mahalle-cards-search">
            <span className="sr-only">Mahalle ara</span>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Mahalle ara…"
            />
          </label>
          <select value={preset} onChange={(e) => setPreset(e.target.value as DatePreset)} aria-label="Dönem">
            {DATE_PRESETS.map((p) => (
              <option key={p.value} value={p.value}>
                {p.label}
              </option>
            ))}
          </select>
          {preset === 'custom' ? (
            <>
              <input type="date" value={customFrom} onChange={(e) => setCustomFrom(e.target.value)} />
              <input type="date" value={customTo} onChange={(e) => setCustomTo(e.target.value)} />
            </>
          ) : null}
          <select value={category} onChange={(e) => setCategory(e.target.value)} aria-label="Etkinlik türü">
            {EVENT_CATEGORIES.map((c) => (
              <option key={c.value || 'all'} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
          <select
            value={cardSort}
            onChange={(e) => setCardSort(e.target.value as 'name' | 'coverage')}
            aria-label="Sıralama"
          >
            <option value="name">Ada göre</option>
            <option value="coverage">Kaplamaya göre</option>
          </select>
        </div>

        <div className="mahalle-cards-legend" role="group" aria-label="Kaplama renkleri">
          {COVERAGE_LEGEND.map((row) => (
            <button
              key={row.level}
              type="button"
              className={`ops-colorbar-btn${coverageFilter === row.level ? ' is-active' : ''}${coverageFilter && coverageFilter !== row.level ? ' is-muted' : ''}`}
              aria-pressed={coverageFilter === row.level}
              onClick={() => toggleCoverageFilter(row.level)}
            >
              <span className={`ops-swatch is-${row.level}`} />
              <span>{row.short}</span>
              <em>{coverageCounts[row.level]}</em>
            </button>
          ))}
        </div>

        {visibleCards.length === 0 && !loading ? (
          <p className="muted">Bu filtreye uyan mahalle yok.</p>
        ) : (
          <ul className="mahalle-cards-grid">
            {visibleCards.map((s) => {
              const level = levelFromCoverage(s.activityCount, s.coverageRate)
              return (
                <li key={s.settlementId}>
                  <button
                    type="button"
                    className={`mahalle-tile is-${level}`}
                    onClick={() => openSettlement(s.officialCode)}
                  >
                    <span className="mahalle-tile-bar" aria-hidden />
                    <span className="mahalle-tile-body">
                      <strong>{s.name}</strong>
                      <em>{coverageCaption(s)}</em>
                      {s.population != null ? (
                        <span>{s.population.toLocaleString('tr-TR')} nüfus</span>
                      ) : null}
                    </span>
                  </button>
                </li>
              )
            })}
          </ul>
        )}
      </div>
    )
  }

  return (
    <div className="events-map-page is-ops-map">
      {error ? (
        <div className="ops-map-banner" role="alert">
          <p>{error}</p>
          <button type="button" className="btn-secondary" onClick={() => void loadSummary()}>
            Yeniden dene
          </button>
        </div>
      ) : null}
      {geoError ? (
        <div className="ops-map-banner" role="alert">
          {geoError}
        </div>
      ) : null}

      <div className={`events-map-layout ops-map-layout${coverageFilter ? ' is-filtering' : ''}`}>
        <div className="events-map-shell">
          {loading ? <div className="ops-map-loading">Harita özeti yükleniyor…</div> : null}

          <div className="ops-map-float-search map-search-bar">
            <MahalleViewSwitch view={view} onChange={setView} />
            <label className="ops-map-search-field">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path
                  d="M21 21l-4.35-4.35M19 11a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                />
              </svg>
              <input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Mahalle ara veya seç…"
                aria-label="Mahalle ara"
              />
            </label>
            {searchHits.length > 0 && search.trim() ? (
              <ul className="map-search-results" role="listbox">
                {searchHits.map((s) => (
                  <li key={s.settlementId}>
                    <button type="button" onClick={() => openSettlement(s.officialCode)}>
                      <strong>{s.name}</strong>
                      <span>
                        {s.activityCount === 0
                          ? 'Etkinlik yok'
                          : `${s.activityCount} etkinlik${s.coverageRate != null ? ` · ${formatRate(s.coverageRate)}` : ''}`}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>

          <div className="ops-map-float-filters">
            <div className={`ops-filter-card${filterOpen ? ' is-open' : ''}`}>
              <button
                type="button"
                className="ops-filter-card-toggle"
                onClick={() => setFilterOpen((v) => !v)}
                aria-expanded={filterOpen}
              >
                <span>
                  <strong>Filtrele</strong>
                  <em>
                    {DATE_PRESETS.find((p) => p.value === preset)?.label ?? 'Dönem'}
                    {' · '}
                    {EVENT_CATEGORIES.find((c) => c.value === category)?.label ?? 'Tüm türler'}
                  </em>
                </span>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <path
                    d={filterOpen ? 'm6 15 6-6 6 6' : 'm6 9 6 6 6-6'}
                    stroke="currentColor"
                    strokeWidth="2.2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  />
                </svg>
              </button>
              {filterOpen ? (
                <div className="ops-filter-card-body">
              <label>
                <span>Dönem</span>
                <select value={preset} onChange={(e) => setPreset(e.target.value as DatePreset)}>
                  {DATE_PRESETS.map((p) => (
                    <option key={p.value} value={p.value}>
                      {p.label}
                    </option>
                  ))}
                </select>
              </label>
              {preset === 'custom' ? (
                <div className="ops-filter-dates">
                  <input type="date" value={customFrom} onChange={(e) => setCustomFrom(e.target.value)} />
                  <input type="date" value={customTo} onChange={(e) => setCustomTo(e.target.value)} />
                </div>
              ) : null}
              <label>
                <span>Etkinlik türü</span>
                <select value={category} onChange={(e) => setCategory(e.target.value)}>
                  {EVENT_CATEGORIES.map((c) => (
                    <option key={c.value || 'all'} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </label>
              <button
                type="button"
                className="ops-filter-reset"
                onClick={() => {
                  setPreset('year')
                  setCategory('')
                  setCustomFrom('')
                  setCustomTo('')
                }}
              >
                Sıfırla
              </button>
                </div>
              ) : null}
            </div>
            <button
              type="button"
              className="ops-map-reset"
              onClick={() => setResetToken((n) => n + 1)}
            >
              İlk görünüme dön
            </button>
          </div>
          <MapContainer
            center={SEHITKAMIL_CENTER}
            zoom={11}
            className="events-leaflet-map"
            scrollWheelZoom
            maxBounds={SEHITKAMIL_BOUNDS}
            maxBoundsViscosity={1}
            minZoom={10}
            maxZoom={16}
            preferCanvas
          >
            <DistrictFit geo={boundary} resetToken={resetToken} />
            <MapLabelController
              nonce={`${preset}-${category}-${summaries.length}-${coverageFilter ?? 'all'}`}
            />
            {mask ? (
              <GeoJSON
                data={mask}
                pathOptions={{
                  fillColor: '#F7FAFB',
                  fillOpacity: 0.92,
                  color: '#D8E2E9',
                  weight: 0,
                  interactive: false,
                }}
              />
            ) : null}
            {boundary && !Array.isArray(boundary) ? (
              <GeoJSON
                data={boundary}
                pathOptions={{ color: '#208B5F', weight: 2.2, fillOpacity: 0, interactive: false }}
              />
            ) : null}
            {mahalleler ? (
              <GeoJSON
                key={`cov-${preset}-${category}-${summaries.length}-${coverageFilter ?? 'all'}`}
                data={mahalleler as GeoJSON.GeoJsonObject}
                style={mahalleStyle}
                onEachFeature={onEachMahalle}
              />
            ) : null}
          </MapContainer>

          <div
            className={`ops-colorbar${coverageFilter ? ' is-filtering' : ''}`}
            role="group"
            aria-label="Kaplama renkleri"
          >
            <p className="ops-colorbar-caption">Kaplama</p>
            <p className="ops-colorbar-meanings">
              Açık yeşil: etkinlik yok · Kırmızı %20 altı · Sarı %20–50 · Yeşil %50 üzeri
            </p>
            <div className="ops-colorbar-row">
              {COVERAGE_LEGEND.map((row) => (
                <button
                  key={row.level}
                  type="button"
                  className={`ops-colorbar-btn${coverageFilter === row.level ? ' is-active' : ''}${coverageFilter && coverageFilter !== row.level ? ' is-muted' : ''}`}
                  aria-pressed={coverageFilter === row.level}
                  title={`${row.label} · ${coverageCounts[row.level]} mahalle`}
                  onClick={() => toggleCoverageFilter(row.level)}
                >
                  <span className={`ops-swatch is-${row.level}`} />
                  <span>{row.short}</span>
                  <em>{coverageCounts[row.level]}</em>
                </button>
              ))}
            </div>
            {activeLegend ? (
              <div className="ops-colorbar-chip">
                <span>
                  Yalnızca {activeLegend.label} · {coverageCounts[activeLegend.level]} mahalle
                </span>
                <button type="button" onClick={() => setCoverageFilter(null)}>
                  Tümünü göster
                </button>
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  )
}
