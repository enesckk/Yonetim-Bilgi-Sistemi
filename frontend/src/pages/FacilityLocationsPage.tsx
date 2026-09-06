import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, Fragment } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  fetchOrganizationTree,
  updateOrganizationUnit,
  type OrgNode,
} from '@/api/organizationApi'
import {
  downloadFacilityCoordsTemplate,
  importFacilityCoordsFromCsv,
} from '@/api/eventsImportApi'
import type { FacilityCoordsImportResult } from '@/api/eventsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useAlert } from '@/components/ConfirmDialog'
import { LocationPickerMap } from '@/components/LocationPickerMap'
import { geocodeAddress, reverseGeocode } from '@/lib/geocode'

const ORG_FACILITY = 6

function flattenFacilities(nodes: OrgNode[], acc: OrgNode[] = []): OrgNode[] {
  for (const n of nodes) {
    if (n.type === ORG_FACILITY) acc.push(n)
    if (n.children?.length) flattenFacilities(n.children, acc)
  }
  return acc
}

type Draft = { latitude: string; longitude: string; address: string }

function hasCoords(f: OrgNode) {
  return f.latitude != null && f.longitude != null
}

export function FacilityLocationsPage() {
  const { hasPermission } = useAuth()
  const canView =
    hasPermission(PermissionCodes.OrganizationView) || hasPermission(PermissionCodes.EventsView)
  const canManage = hasPermission(PermissionCodes.OrganizationManage)
  const alert = useAlert()

  const [facilities, setFacilities] = useState<OrgNode[]>([])
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [loading, setLoading] = useState(true)
  const [savingId, setSavingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [searchParams] = useSearchParams()
  const [onlyMissing, setOnlyMissing] = useState(() => searchParams.get('missing') === '1')
  const [importBusy, setImportBusy] = useState(false)
  const [importResult, setImportResult] = useState<FacilityCoordsImportResult | null>(null)
  const [importError, setImportError] = useState<string | null>(null)
  const [geocodingId, setGeocodingId] = useState<string | null>(null)
  const [mapPickId, setMapPickId] = useState<string | null>(null)
  const csvInputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (searchParams.get('missing') === '1') setOnlyMissing(true)
  }, [searchParams])

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      const tree = await fetchOrganizationTree()
      const list = flattenFacilities(tree).sort((a, b) => a.name.localeCompare(b.name, 'tr'))
      setFacilities(list)
      const next: Record<string, Draft> = {}
      for (const f of list) {
        next[f.id] = {
          latitude: f.latitude != null ? String(f.latitude) : '',
          longitude: f.longitude != null ? String(f.longitude) : '',
          address: f.address ?? '',
        }
      }
      setDrafts(next)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Tesisler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canView])

  useEffect(() => {
    void load()
  }, [load])

  const missingCount = useMemo(
    () => facilities.filter((f) => !hasCoords(f)).length,
    [facilities],
  )

  const visible = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr-TR')
    return facilities.filter((f) => {
      if (onlyMissing && hasCoords(f)) return false
      if (!q) return true
      return (
        f.name.toLocaleLowerCase('tr-TR').includes(q) ||
        (drafts[f.id]?.address ?? f.address ?? '').toLocaleLowerCase('tr-TR').includes(q)
      )
    })
  }, [facilities, onlyMissing, search, drafts])

  function onSearchSubmit(e: FormEvent) {
    e.preventDefault()
    setSearch(searchInput)
  }

  function patchDraft(id: string, patch: Partial<Draft>) {
    setDrafts((prev) => ({
      ...prev,
      [id]: {
        latitude: prev[id]?.latitude ?? '',
        longitude: prev[id]?.longitude ?? '',
        address: prev[id]?.address ?? '',
        ...patch,
      },
    }))
  }

  async function geocodeFacility(facility: OrgNode) {
    if (!canManage) return
    const draft = drafts[facility.id]
    const q = [facility.name, draft?.address || facility.address, 'Şehitkamil', 'Gaziantep']
      .filter(Boolean)
      .join(', ')
    setGeocodingId(facility.id)
    try {
      const hit = await geocodeAddress(q)
      if (!hit) {
        await alert({
          title: 'Konum bulunamadı',
          message: 'Adres veya tesis adı ile sonuç çıkmadı. Adresi güncelleyip tekrar deneyin.',
          tone: 'warning',
        })
        return
      }
      patchDraft(facility.id, {
        latitude: hit.latitude.toFixed(6),
        longitude: hit.longitude.toFixed(6),
      })
    } catch {
      await alert({
        title: 'Adres araması başarısız',
        message: 'Ağ veya servis hatası. Biraz sonra tekrar deneyin.',
        tone: 'warning',
      })
    } finally {
      setGeocodingId(null)
    }
  }

  async function onFacilityMapPick(facilityId: string, lat: number, lng: number) {
    patchDraft(facilityId, {
      latitude: lat.toFixed(6),
      longitude: lng.toFixed(6),
    })
    try {
      const hit = await reverseGeocode(lat, lng)
      if (hit?.displayName) {
        setDrafts((prev) => {
          const cur = prev[facilityId]
          if (!cur || cur.address.trim()) return prev
          return { ...prev, [facilityId]: { ...cur, address: hit.displayName } }
        })
      }
    } catch {
      /* ignore */
    }
  }

  async function save(facility: OrgNode) {
    if (!canManage) return
    const draft = drafts[facility.id]
    if (!draft) return
    const lat = draft.latitude.trim() ? Number(draft.latitude) : null
    const lng = draft.longitude.trim() ? Number(draft.longitude) : null
    const address = draft.address.trim() || null
    if ((lat == null) !== (lng == null)) {
      await alert({
        title: 'Eksik koordinat',
        message: 'Enlem ve boylam birlikte girilmelidir (veya ikisi de boş).',
        tone: 'warning',
      })
      return
    }
    if (lat != null && (Number.isNaN(lat) || lat < -90 || lat > 90)) {
      await alert({
        title: 'Geçersiz enlem',
        message: 'Enlem -90 ile 90 arasında olmalıdır.',
        tone: 'warning',
      })
      return
    }
    if (lng != null && (Number.isNaN(lng) || lng < -180 || lng > 180)) {
      await alert({
        title: 'Geçersiz boylam',
        message: 'Boylam -180 ile 180 arasında olmalıdır.',
        tone: 'warning',
      })
      return
    }

    setSavingId(facility.id)
    try {
      await updateOrganizationUnit(facility.id, {
        name: facility.name,
        code: facility.code ?? null,
        type: facility.type,
        status: facility.status,
        parentId: facility.parentId ?? null,
        description: facility.description ?? null,
        phone: facility.phone ?? null,
        email: facility.email ?? null,
        facilityCategoryId: facility.facilityCategoryId ?? null,
        address,
        latitude: lat,
        longitude: lng,
        capacity: facility.capacity ?? null,
        workingHours: facility.workingHours ?? null,
        idealStaffCount: facility.idealStaffCount ?? null,
        managerEmployeeId: facility.managerEmployeeId ?? null,
        openedOn: facility.openedOn ?? null,
        closedOn: facility.closedOn ?? null,
      })
      await load()
      setMapPickId(null)
    } catch (err) {
      await alert({
        title: 'Kaydedilemedi',
        message: err instanceof ApiClientError ? err.message : 'Konum kaydedilemedi.',
        tone: 'warning',
      })
    } finally {
      setSavingId(null)
    }
  }

  async function onDownloadTemplate() {
    setImportError(null)
    try {
      await downloadFacilityCoordsTemplate()
    } catch (err) {
      setImportError(err instanceof ApiClientError ? err.message : 'Şablon indirilemedi.')
    }
  }

  async function onCsvSelected(file: File | null) {
    if (!file || !canManage) return
    setImportBusy(true)
    setImportError(null)
    setImportResult(null)
    try {
      const res = await importFacilityCoordsFromCsv(file)
      setImportResult(res)
      if (res.successCount > 0) await load()
    } catch (err) {
      setImportError(err instanceof ApiClientError ? err.message : 'CSV aktarımı başarısız.')
    } finally {
      setImportBusy(false)
      if (csvInputRef.current) csvInputRef.current.value = ''
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Bu ekranı görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  const hint = loading
    ? 'Yükleniyor…'
    : `${visible.length} / ${facilities.length} tesis · ${missingCount} konum eksik`

  return (
    <div className="facility-locations-page employees-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Tesis konumları</h2>
              <div className="stat-chip mobile-inline-chip">{hint}</div>
            </div>
            <p className="muted small employees-toolbar-lead">
              Haritada görünmesi için tesislere enlem / boylam atayın. Değişiklik satır bazında
              kaydedilir.
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <div className="stat-chip desktop-inline-chip">{hint}</div>
            {canManage ? (
              <div className="facility-csv-actions">
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => void onDownloadTemplate()}
                  disabled={importBusy}
                >
                  Şablon indir
                </button>
                <label className={`btn-secondary facility-csv-upload${importBusy ? ' is-disabled' : ''}`}>
                  {importBusy ? 'Aktarılıyor…' : 'CSV aktar'}
                  <input
                    ref={csvInputRef}
                    type="file"
                    accept=".csv,text/csv"
                    hidden
                    disabled={importBusy}
                    onChange={(e) => void onCsvSelected(e.target.files?.[0] ?? null)}
                  />
                </label>
              </div>
            ) : null}
            <Link to="/events/map" className="btn-secondary">
              Haritaya git
            </Link>
          </div>
        </div>

        {importError ? <p className="form-error">{importError}</p> : null}
        {importResult ? (
          <p className="muted small facility-csv-summary" role="status">
            CSV aktarım: {importResult.successCount} başarılı, {importResult.failureCount} hatalı
            (toplam {importResult.totalRows} satır).
          </p>
        ) : null}

        <form className="employees-filters" onSubmit={onSearchSubmit}>
          <div className="employees-filters-search">
            <input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Tesis adı veya adres ara…"
              aria-label="Ara"
            />
            <button type="submit">Ara</button>
          </div>
          <div
            className="employees-filters-row"
            style={{ gridTemplateColumns: 'minmax(0, 1fr) auto' }}
          >
            <label className="checkbox-row facility-loc-filter">
              <input
                type="checkbox"
                checked={onlyMissing}
                onChange={(e) => setOnlyMissing(e.target.checked)}
              />
              <span>Sadece konum eksik olanlar</span>
            </label>
            <div className="employees-filters-actions">
              <button
                type="button"
                className="btn-secondary"
                onClick={() => {
                  setSearchInput('')
                  setSearch('')
                  setOnlyMissing(false)
                }}
              >
                Temizle
              </button>
            </div>
          </div>
        </form>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="employees-table-wrap">
          <table className="data-table employees-table facility-loc-table">
            <thead>
              <tr>
                <th>Tesis</th>
                <th>Adres</th>
                <th>Durum</th>
                <th>Enlem</th>
                <th>Boylam</th>
                <th>İşlem</th>
              </tr>
            </thead>
            <tbody>
              {visible.length === 0 ? (
                <tr>
                  <td colSpan={6} className="muted">
                    {loading ? 'Yükleniyor…' : 'Kayıt bulunamadı.'}
                  </td>
                </tr>
              ) : (
                visible.map((f) => {
                  const draft = drafts[f.id] ?? { latitude: '', longitude: '', address: '' }
                  const ok = hasCoords(f)
                  const picking = mapPickId === f.id
                  return (
                    <Fragment key={f.id}>
                      <tr className={ok ? undefined : 'row-warn'}>
                        <td>
                          <strong>{f.name}</strong>
                          {f.code ? <div className="muted small">{f.code}</div> : null}
                        </td>
                        <td>
                          {canManage ? (
                            <input
                              className="facility-loc-address"
                              value={draft.address}
                              disabled={savingId === f.id}
                              onChange={(e) => patchDraft(f.id, { address: e.target.value })}
                              placeholder="Adres"
                              aria-label={`${f.name} adres`}
                            />
                          ) : (
                            draft.address || '—'
                          )}
                        </td>
                        <td>
                          <span className={`facility-loc-badge ${ok ? 'is-ok' : 'is-warn'}`}>
                            {ok ? 'Konumlu' : 'Eksik'}
                          </span>
                        </td>
                        <td>
                          <input
                            className="facility-loc-input"
                            value={draft.latitude}
                            disabled={!canManage || savingId === f.id}
                            onChange={(e) => patchDraft(f.id, { latitude: e.target.value })}
                            placeholder="37.0662"
                            inputMode="decimal"
                            aria-label={`${f.name} enlem`}
                          />
                        </td>
                        <td>
                          <input
                            className="facility-loc-input"
                            value={draft.longitude}
                            disabled={!canManage || savingId === f.id}
                            onChange={(e) => patchDraft(f.id, { longitude: e.target.value })}
                            placeholder="37.3833"
                            inputMode="decimal"
                            aria-label={`${f.name} boylam`}
                          />
                        </td>
                        <td className="row-actions">
                          {canManage ? (
                            <div className="facility-loc-actions">
                              <button
                                type="button"
                                className="btn-secondary"
                                disabled={geocodingId === f.id || savingId === f.id}
                                title="Adres veya tesis adından konum bul"
                                onClick={() => void geocodeFacility(f)}
                              >
                                {geocodingId === f.id ? 'Aranıyor…' : 'Adresten bul'}
                              </button>
                              <button
                                type="button"
                                className="btn-secondary"
                                disabled={savingId === f.id}
                                onClick={() => setMapPickId(picking ? null : f.id)}
                              >
                                {picking ? 'Haritayı kapat' : 'Haritadan'}
                              </button>
                              <button
                                type="button"
                                className="btn-secondary"
                                disabled={savingId === f.id}
                                onClick={() => void save(f)}
                              >
                                {savingId === f.id ? 'Kaydediliyor…' : 'Kaydet'}
                              </button>
                            </div>
                          ) : (
                            <span className="muted small">Salt okunur</span>
                          )}
                        </td>
                      </tr>
                      {picking ? (
                        <tr className="facility-loc-map-row">
                          <td colSpan={6}>
                            <LocationPickerMap
                              latitude={draft.latitude.trim() ? Number(draft.latitude) : null}
                              longitude={draft.longitude.trim() ? Number(draft.longitude) : null}
                              onPick={(lat, lng) => void onFacilityMapPick(f.id, lat, lng)}
                              height={220}
                            />
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  )
                })
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
