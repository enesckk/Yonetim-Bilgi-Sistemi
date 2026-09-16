import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiClientError } from '@/api/client'
import {
  archiveStockItem,
  createStockItem,
  createStockMovement,
  fetchStockItem,
  fetchStockItems,
  fetchStockOptions,
  fetchStockSummary,
  fmtQty,
  updateStockItem,
  type StockItemRow,
  type StockLookup,
  type StockMovementType,
  type StockOptions,
  type StockSummary,
  type UpsertStockItemPayload,
} from '@/api/stockApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useAlert, useConfirm } from '@/components/ConfirmDialog'
import { StockIcon } from '@/components/StockIcon'

type Tab = 'places' | 'items' | 'moves'
type ItemFormMode = 'create' | 'edit' | null
const emptyItem = (): UpsertStockItemPayload => ({
  name: '',
  code: '',
  category: 99,
  unit: 1,
  brand: '',
  model: '',
  description: '',
  minQuantity: 0,
  isActive: true,
})

function todayIso() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

export function StockPage() {
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const alert = useAlert()
  const navigate = useNavigate()
  const canView = hasPermission(PermissionCodes.StockView) || hasPermission(PermissionCodes.StockManage)
  const canManage = hasPermission(PermissionCodes.StockManage)

  const [tab, setTab] = useState<Tab>('items')
  const [summary, setSummary] = useState<StockSummary | null>(null)
  const [items, setItems] = useState<StockItemRow[]>([])
  const [options, setOptions] = useState<StockOptions | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<number | ''>('')
  const [locationId, setLocationId] = useState('')

  const [itemMode, setItemMode] = useState<ItemFormMode>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [itemForm, setItemForm] = useState(emptyItem)
  const [itemError, setItemError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const [moveOpen, setMoveOpen] = useState(false)
  const [moveError, setMoveError] = useState<string | null>(null)
  const [move, setMove] = useState({
    stockItemId: '',
    movementType: 1 as StockMovementType,
    fromLocationId: '',
    toLocationId: '',
    quantity: '1',
    occurredOn: todayIso(),
    reason: '',
  })

  const load = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) setLoading(true)
    if (!opts?.silent) setError(null)
    try {
      const [sum, catalog, optsData] = await Promise.all([
        fetchStockSummary(),
        fetchStockItems(),
        fetchStockOptions(),
      ])
      setSummary(sum)
      setItems(catalog)
      setOptions(optsData)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Stok yüklenemedi.')
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!canView) return
    void load()
  }, [canView, load])

  const categories = options?.categories ?? []
  const units = options?.units ?? []
  const locations = options?.locations ?? []
  const q = search.trim().toLocaleLowerCase('tr-TR')

  const itemMatches = useCallback(
    (row: StockItemRow) => {
      const places = row.locations ?? []
      if (locationId && !places.some((p) => p.locationId === locationId)) return false
      if (!q) return true
      const hay = [
        row.name,
        row.code,
        row.brand,
        row.model,
        row.categoryLabel,
        ...places.map((p) => p.locationName),
      ]
        .filter(Boolean)
        .join(' ')
        .toLocaleLowerCase('tr-TR')
      return hay.includes(q)
    },
    [locationId, q],
  )

  const filteredItems = useMemo(
    () =>
      items.filter((row) => {
        if (category !== '' && row.category !== category) return false
        return itemMatches(row)
      }),
    [items, category, itemMatches],
  )

  const visiblePlacesFor = (row: StockItemRow) => {
    const places = row.locations ?? []
    if (locationId) return places.filter((p) => p.locationId === locationId)
    return places
  }

  const qtyFor = (row: StockItemRow) => {
    const places = visiblePlacesFor(row)
    if (locationId) return places.reduce((sum, p) => sum + p.quantity, 0)
    return row.totalQuantity
  }

  const selectedCategoryLabel = categories.find((c) => c.value === category)?.label
  const selectedLocationName = locations.find((l) => l.id === locationId)?.name
  const lookingAt = [selectedCategoryLabel, selectedLocationName].filter(Boolean).join(' · ')

  const openCreateItem = () => {
    setItemForm(emptyItem())
    setEditingId(null)
    setItemError(null)
    setItemMode('create')
  }

  const openEditItem = async (row: StockItemRow) => {
    try {
      const detail = await fetchStockItem(row.id)
      setItemForm({
        name: detail.name,
        code: detail.code ?? '',
        category: detail.category,
        unit: detail.unit,
        brand: detail.brand ?? '',
        model: detail.model ?? '',
        minQuantity: detail.minQuantity,
        description: detail.description ?? '',
        isActive: detail.isActive,
      })
    } catch {
      setItemForm({
        name: row.name,
        code: row.code ?? '',
        category: row.category,
        unit: row.unit,
        brand: row.brand ?? '',
        model: row.model ?? '',
        minQuantity: row.minQuantity,
        description: '',
        isActive: row.isActive,
      })
    }
    setEditingId(row.id)
    setItemError(null)
    setItemMode('edit')
  }

  const onSaveItem = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setItemError(null)
    try {
      const payload: UpsertStockItemPayload = {
        ...itemForm,
        code: itemForm.code?.trim() || null,
        brand: itemForm.brand?.trim() || null,
        model: itemForm.model?.trim() || null,
        description: itemForm.description?.trim() || null,
      }
      if (itemMode === 'edit' && editingId) await updateStockItem(editingId, payload)
      else await createStockItem(payload)
      setItemMode(null)
      await load({ silent: true })
    } catch (err) {
      setItemError(err instanceof ApiClientError ? err.message : 'Kayıt kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  const onArchive = async (row: StockItemRow) => {
    const ok = await confirm({
      title: 'Malzemeyi kaldır',
      message: `${row.name} katalogdan kaldırılacak. Geçmiş hareketler durur.`,
      confirmLabel: 'Kaldır',
      tone: 'danger',
    })
    if (!ok) return
    try {
      await archiveStockItem(row.id)
      await load({ silent: true })
    } catch (err) {
    await alert({ message: err instanceof ApiClientError ? err.message : 'Silinemedi.' })
    }
  }

  const openMove = (preset?: Partial<typeof move>) => {
    setMove({
      stockItemId: preset?.stockItemId ?? items[0]?.id ?? '',
      movementType: preset?.movementType ?? 1,
      fromLocationId: preset?.fromLocationId ?? '',
      toLocationId: preset?.toLocationId ?? '',
      quantity: preset?.quantity ?? '1',
      occurredOn: todayIso(),
      reason: '',
    })
    setMoveError(null)
    setMoveOpen(true)
  }

  const onSaveMove = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setMoveError(null)
    try {
      const qty = Number(move.quantity.replace(',', '.'))
      await createStockMovement({
        stockItemId: move.stockItemId,
        movementType: move.movementType,
        fromLocationId: move.movementType === 1 || move.movementType === 4 ? null : move.fromLocationId || null,
        toLocationId: move.movementType === 2 ? null : move.toLocationId || null,
        quantity: qty,
        occurredOn: move.occurredOn || null,
        reason: move.reason.trim() || null,
      })
      setMoveOpen(false)
      await load({ silent: true })
    } catch (err) {
      setMoveError(err instanceof ApiClientError ? err.message : 'Hareket kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  const places = summary?.locations ?? []
  const stockedPlaces = useMemo(
    () =>
      places.filter((p) => {
        if (!(p.itemCount > 0 || p.lowCount > 0)) return false
        if (locationId && p.id !== locationId) return false
        if (!q) return true
        return p.name.toLocaleLowerCase('tr-TR').includes(q)
      }),
    [places, q, locationId],
  )
  const emptyPlaces = useMemo(
    () =>
      places.filter((p) => {
        if (!(p.itemCount === 0 && p.lowCount === 0)) return false
        if (locationId && p.id !== locationId) return false
        if (!q) return true
        return p.name.toLocaleLowerCase('tr-TR').includes(q)
      }),
    [places, q, locationId],
  )

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Stok ekranı için yetkiniz yok. Oturumu yenileyip tekrar deneyin.</p>
      </div>
    )
  }

  return (
    <div className="employees-page stock-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Stok takip</h2>
            </div>
            <p className="stock-meta">
              {loading ? (
                'Yükleniyor…'
              ) : (
                <>
                  <span>
                    <strong>{fmtQty(filteredItems.length)}</strong> malzeme
                  </span>
                  <span>
                    <strong>{fmtQty(summary?.locationsWithStock ?? 0)}</strong> stoklu yer
                  </span>
                  {(summary?.lowCount ?? 0) > 0 ? (
                    <span className="is-warn">
                      <strong>{fmtQty(summary?.lowCount ?? 0)}</strong> kritik
                    </span>
                  ) : (
                    <span>Kritik yok</span>
                  )}
                </>
              )}
            </p>
          </div>
          <div className="employees-toolbar-actions">
            {canManage ? (
              <>
                <button type="button" className="btn-secondary stock-toolbar-btn" onClick={() => openMove()}>
                  <StockIcon name="movement" />
                  Stok hareketi
                </button>
                <button type="button" className="btn-primary stock-toolbar-btn" onClick={openCreateItem}>
                  <StockIcon name="plus" />
                  Yeni malzeme
                </button>
              </>
            ) : null}
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="stock-tabs" role="tablist">
          <button type="button" className={tab === 'places' ? 'is-on' : ''} onClick={() => setTab('places')}>
            Yerler
          </button>
          <button type="button" className={tab === 'items' ? 'is-on' : ''} onClick={() => setTab('items')}>
            Malzemeler
          </button>
          <button type="button" className={tab === 'moves' ? 'is-on' : ''} onClick={() => setTab('moves')}>
            Hareketler
          </button>
        </div>

        {tab !== 'moves' ? (
          <div
            className={[
              'stock-bar',
              tab === 'places' ? 'is-places' : '',
              locations.length > 1 ? '' : 'is-solo',
            ]
              .filter(Boolean)
              .join(' ')}
          >
            <label className="iam-search">
              <span className="sr-only">Ara</span>
              <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden>
                <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
                <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
              <input
                type="search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={tab === 'places' ? 'Tesis ara…' : 'Malzeme veya kod ara…'}
                autoComplete="off"
              />
            </label>
            {tab === 'items' ? (
              <select
                aria-label="Grup"
                value={category}
                onChange={(e) => setCategory(e.target.value ? Number(e.target.value) : '')}
              >
                <option value="">Tüm gruplar</option>
                {categories.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
            ) : null}
            {locations.length > 1 ? (
              <select
                aria-label="Tesis"
                value={locationId}
                onChange={(e) => setLocationId(e.target.value)}
              >
                <option value="">Tüm tesisler</option>
                {locations.map((loc) => (
                  <option key={loc.id} value={loc.id}>
                    {loc.name}
                  </option>
                ))}
              </select>
            ) : null}
          </div>
        ) : null}

        {tab === 'places' ? (
          <div className="stock-places">
            {(summary?.lowStock.length ?? 0) > 0 ? (
              <section className="staff-dash-panel">
                <header>
                  <h3>Kritik stok</h3>
                </header>
                <ul className="stock-low-list">
                  {summary!.lowStock.map((row) => (
                    <li key={`${row.stockItemId}-${row.locationId}`}>
                      <Link to={`/stock/locations/${row.locationId}`}>
                        <strong>{row.itemName}</strong>
                        <em>
                          {row.locationName} · {fmtQty(row.quantity)} / min {fmtQty(row.minQuantity)} {row.unitLabel}
                        </em>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            ) : null}

            {loading ? (
              <div className="ui-skeleton-stack" aria-hidden="true">
                <span className="ui-skeleton" />
                <span className="ui-skeleton" />
              </div>
            ) : stockedPlaces.length === 0 && !canManage ? (
              <p className="staff-dash-empty">Henüz kayıtlı stok yok.</p>
            ) : stockedPlaces.length === 0 && emptyPlaces.length === 0 ? (
              <p className="staff-dash-empty">Stok tutulacak tesis henüz yok.</p>
            ) : (
              <>
                {stockedPlaces.length === 0 ? (
                  <p className="staff-dash-empty">Kayıtlı malzeme olan tesis yok. Aşağıdan yer seçip giriş yapın.</p>
                ) : (
                  <ul className="stock-place-grid">
                    {stockedPlaces.map((place) => (
                      <li key={place.id}>
                        <button type="button" onClick={() => navigate(`/stock/locations/${place.id}`)}>
                          <span className="stock-place-type">{place.typeLabel}</span>
                          <strong>{place.name}</strong>
                          <small>
                            {`${fmtQty(place.itemCount)} çeşit malzeme`}
                            {place.lowCount > 0 ? ` · ${place.lowCount} kritik` : ''}
                          </small>
                        </button>
                      </li>
                    ))}
                  </ul>
                )}
                {canManage && emptyPlaces.length > 0 ? (
                  <details className="stock-empty-places">
                    <summary>Stok olmayan yerler ({emptyPlaces.length})</summary>
                    <ul className="stock-place-grid">
                      {emptyPlaces.map((place) => (
                        <li key={place.id}>
                          <button type="button" onClick={() => navigate(`/stock/locations/${place.id}`)}>
                            <span className="stock-place-type">{place.typeLabel}</span>
                            <strong>{place.name}</strong>
                            <small>Henüz stok yok</small>
                          </button>
                        </li>
                      ))}
                    </ul>
                  </details>
                ) : null}
              </>
            )}
          </div>
        ) : null}

        {tab === 'items' ? (
          <div className="stock-items">
            <div className="employees-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Malzeme</th>
                    <th>Grup</th>
                    <th>Toplam</th>
                    <th>Nerede</th>
                    {canManage ? <th></th> : null}
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={canManage ? 5 : 4}>Yükleniyor…</td>
                    </tr>
                  ) : filteredItems.length === 0 ? (
                    <tr>
                      <td colSpan={canManage ? 5 : 4}>
                        {selectedLocationName && selectedCategoryLabel
                          ? `${selectedLocationName} tesisinde ${selectedCategoryLabel.toLocaleLowerCase('tr-TR')} stoğu yok.`
                          : lookingAt || search.trim()
                            ? 'Bu arama ve grupta malzeme yok.'
                            : 'Malzeme bulunamadı.'}
                      </td>
                    </tr>
                  ) : (
                    filteredItems.map((row) => {
                      const placesHere = visiblePlacesFor(row)
                      const qty = qtyFor(row)
                      return (
                      <tr key={row.id} className={row.isLow ? 'row-error' : undefined}>
                        <td>
                          <strong>{row.name}</strong>
                          <div className="muted small">
                            {row.code ?? 'Kod yok'}
                            {row.brand ? ` · ${row.brand}` : ''}
                            {row.model ? ` ${row.model}` : ''}
                          </div>
                        </td>
                        <td>
                          <span className="stock-group-tag">{row.categoryLabel}</span>
                        </td>
                        <td>
                          {fmtQty(qty)} {row.unitLabel}
                          {row.isLow ? <span className="stock-pill is-low">Kritik</span> : null}
                        </td>
                        <td>
                          {placesHere.length === 0 ? (
                            <span className="muted small">Stokta yok</span>
                          ) : (
                            <ul className="stock-where">
                              {placesHere.map((place) => (
                                <li key={place.locationId}>
                                  <Link to={`/stock/locations/${place.locationId}`} className="stock-where-chip">
                                    <strong>{place.locationName}</strong>
                                    <span>
                                      {fmtQty(place.quantity)} {row.unitLabel}
                                    </span>
                                  </Link>
                                </li>
                              ))}
                            </ul>
                          )}
                        </td>
                        {canManage ? (
                          <td className="stock-row-actions">
                            <button type="button" className="stock-table-action" onClick={() => void openEditItem(row)}>
                              <StockIcon name="edit" />
                              Düzenle
                            </button>
                            <button
                              type="button"
                              className="stock-table-action is-in"
                              onClick={() => openMove({ stockItemId: row.id, movementType: 1 })}
                            >
                              <StockIcon name="in" />
                              Giriş
                            </button>
                            <button type="button" className="stock-table-action is-danger" onClick={() => void onArchive(row)}>
                              <StockIcon name="archive" />
                              Kaldır
                            </button>
                          </td>
                        ) : null}
                      </tr>
                      )
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        ) : null}

        {tab === 'moves' ? (
          <ul className="staff-dash-feed stock-moves">
            {(summary?.recentMovements.length ?? 0) === 0 ? (
              <li className="staff-dash-empty">Henüz stok hareketi yok.</li>
            ) : (
              summary!.recentMovements.map((m) => (
                <li key={m.id}>
                  <strong>
                    {m.movementTypeLabel} · {m.itemName}
                  </strong>
                  <em>
                    {fmtQty(m.quantity)} {m.unitLabel}
                    {m.fromLocationName ? ` · ${m.fromLocationName}` : ''}
                    {m.toLocationName ? ` → ${m.toLocationName}` : ''}
                    {m.reason ? ` · ${m.reason}` : ''}
                  </em>
                </li>
              ))
            )}
          </ul>
        ) : null}
      </section>

      {itemMode ? (
        <div className="org-modal-backdrop stock-modal-backdrop" onClick={() => !saving && setItemMode(null)}>
          <div
            className="org-modal panel stock-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="stock-item-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <header className="stock-modal-head">
              <div className="stock-modal-heading">
                <span className="stock-modal-symbol"><StockIcon name="box" /></span>
                <div>
                  <p className="org-modal-eyebrow">Malzeme kataloğu</p>
                  <h2 id="stock-item-modal-title">{itemMode === 'create' ? 'Yeni malzeme' : 'Malzemeyi düzenle'}</h2>
                  <p>Malzemenin temel bilgilerini ve kritik stok seviyesini tanımlayın.</p>
                </div>
              </div>
              <button
                type="button"
                className="stock-modal-close"
                onClick={() => setItemMode(null)}
                disabled={saving}
                aria-label="Pencereyi kapat"
              >
                <StockIcon name="close" />
              </button>
            </header>
            <form className="org-form stock-modal-form" onSubmit={(e) => void onSaveItem(e)}>
              {itemError ? <div className="form-error">{itemError}</div> : null}
              <section className="stock-form-section">
                <div className="stock-form-section-title">
                  <strong>Temel bilgiler</strong>
                  <span>Malzemenin stok listesinde nasıl görüneceğini belirler.</span>
                </div>
                <div className="stock-form-grid">
                  <label>
                    <span>Malzeme adı <em>Zorunlu</em></span>
                    <input value={itemForm.name} onChange={(e) => setItemForm((f) => ({ ...f, name: e.target.value }))} required maxLength={200} autoFocus placeholder="Örn. Kablosuz mikrofon" />
                  </label>
                  <label>
                    <span>Stok kodu</span>
                    <input value={itemForm.code ?? ''} onChange={(e) => setItemForm((f) => ({ ...f, code: e.target.value }))} maxLength={40} placeholder="Örn. SES-MIK-EL" />
                  </label>
                  <label>
                    <span>Grup</span>
                    <select value={itemForm.category} onChange={(e) => setItemForm((f) => ({ ...f, category: Number(e.target.value) as StockItemRow['category'] }))}>
                      {categories.map((c: StockLookup) => <option key={c.value} value={c.value}>{c.label}</option>)}
                    </select>
                  </label>
                  <label>
                    <span>Ölçü birimi</span>
                    <select value={itemForm.unit} onChange={(e) => setItemForm((f) => ({ ...f, unit: Number(e.target.value) as StockItemRow['unit'] }))}>
                      {units.map((u) => <option key={u.value} value={u.value}>{u.label}</option>)}
                    </select>
                  </label>
                </div>
              </section>
              <section className="stock-form-section">
                <div className="stock-form-section-title">
                  <strong>Ürün ve stok ayarları</strong>
                  <span>İsteğe bağlı ürün detayları ve uyarı eşiği.</span>
                </div>
                <div className="stock-form-grid">
                  <label>
                    <span>Marka</span>
                    <input value={itemForm.brand ?? ''} onChange={(e) => setItemForm((f) => ({ ...f, brand: e.target.value }))} maxLength={80} placeholder="Marka adı" />
                  </label>
                  <label>
                    <span>Model</span>
                    <input value={itemForm.model ?? ''} onChange={(e) => setItemForm((f) => ({ ...f, model: e.target.value }))} maxLength={80} placeholder="Model bilgisi" />
                  </label>
                  <label>
                    <span>Asgari miktar</span>
                    <input type="number" min={0} step="0.01" value={itemForm.minQuantity} onChange={(e) => setItemForm((f) => ({ ...f, minQuantity: Number(e.target.value) }))} />
                    <small>Bu seviyenin altında kritik stok uyarısı gösterilir.</small>
                  </label>
                  <label className="stock-status-toggle">
                    <input type="checkbox" checked={itemForm.isActive} onChange={(e) => setItemForm((f) => ({ ...f, isActive: e.target.checked }))} />
                    <span><strong>Aktif malzeme</strong><small>Stok hareketlerinde seçilebilir.</small></span>
                  </label>
                  <label className="span-2">
                    <span>Açıklama</span>
                    <textarea rows={3} value={itemForm.description ?? ''} onChange={(e) => setItemForm((f) => ({ ...f, description: e.target.value }))} maxLength={1000} placeholder="Malzemeyle ilgili kısa bir not ekleyin…" />
                  </label>
                </div>
              </section>
              <footer className="org-form-actions stock-modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setItemMode(null)} disabled={saving}>
                  Vazgeç
                </button>
                <button type="submit" className="btn-primary stock-save-btn" disabled={saving}>
                  <StockIcon name="save" />
                  {saving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      ) : null}

      {moveOpen ? (
        <div className="org-modal-backdrop stock-modal-backdrop" onClick={() => !saving && setMoveOpen(false)}>
          <div className="org-modal panel stock-modal stock-movement-modal" role="dialog" aria-modal="true" aria-labelledby="stock-move-modal-title" onClick={(e) => e.stopPropagation()}>
            <header className="stock-modal-head">
              <div className="stock-modal-heading">
                <span className="stock-modal-symbol"><StockIcon name="movement" /></span>
                <div>
                  <p className="org-modal-eyebrow">Stok işlemi</p>
                  <h2 id="stock-move-modal-title">Yeni stok hareketi</h2>
                  <p>Giriş, çıkış, transfer veya sayım işlemini kaydedin.</p>
                </div>
              </div>
              <button type="button" className="stock-modal-close" onClick={() => setMoveOpen(false)} disabled={saving} aria-label="Pencereyi kapat"><StockIcon name="close" /></button>
            </header>
            <form className="org-form stock-modal-form" onSubmit={(e) => void onSaveMove(e)}>
              {moveError ? <div className="form-error">{moveError}</div> : null}
              <div className="stock-form-grid">
              <label>
                <span>İşlem türü <em>Zorunlu</em></span>
                <select
                  value={move.movementType}
                  onChange={(e) => setMove((m) => ({ ...m, movementType: Number(e.target.value) as StockMovementType }))}
                >
                  {(options?.movementTypes ?? []).map((t) => (
                    <option key={t.value} value={t.value}>
                      {t.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <span>Malzeme <em>Zorunlu</em></span>
                <select
                  value={move.stockItemId}
                  onChange={(e) => setMove((m) => ({ ...m, stockItemId: e.target.value }))}
                  required
                >
                  <option value="">Seçin</option>
                  {items.map((row) => (
                    <option key={row.id} value={row.id}>
                      {row.name}
                    </option>
                  ))}
                </select>
              </label>
              {move.movementType === 2 || move.movementType === 3 ? (
                <label>
                  <span>Kaynak yer <em>Zorunlu</em></span>
                  <select
                    value={move.fromLocationId}
                    onChange={(e) => setMove((m) => ({ ...m, fromLocationId: e.target.value }))}
                    required
                  >
                    <option value="">Seçin</option>
                    {locations.map((loc) => (
                      <option key={loc.id} value={loc.id}>
                        {loc.name}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
              {move.movementType !== 2 ? (
                <label>
                  <span>{move.movementType === 4 ? 'Sayım yeri' : 'Hedef yer'} <em>Zorunlu</em></span>
                  <select
                    value={move.toLocationId}
                    onChange={(e) => setMove((m) => ({ ...m, toLocationId: e.target.value }))}
                    required
                  >
                    <option value="">Seçin</option>
                    {locations.map((loc) => (
                      <option key={loc.id} value={loc.id}>
                        {loc.name}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
              <label>
                <span>{move.movementType === 4 ? 'Sayılan miktar' : 'Miktar'} <em>Zorunlu</em></span>
                <input
                  type="number"
                  min={0.01}
                  step="0.01"
                  value={move.quantity}
                  onChange={(e) => setMove((m) => ({ ...m, quantity: e.target.value }))}
                  required
                />
              </label>
              <label>
                <span>İşlem tarihi</span>
                <input
                  type="date"
                  value={move.occurredOn}
                  onChange={(e) => setMove((m) => ({ ...m, occurredOn: e.target.value }))}
                />
              </label>
              <label className="span-2">
                <span>Açıklama</span>
                <input
                  value={move.reason}
                  onChange={(e) => setMove((m) => ({ ...m, reason: e.target.value }))}
                  maxLength={400}
                  placeholder="Satın alma, etkinlik çıkışı, sayım…"
                />
              </label>
              </div>
              <footer className="org-form-actions stock-modal-actions">
                <button type="button" className="btn-secondary" onClick={() => setMoveOpen(false)} disabled={saving}>
                  Vazgeç
                </button>
                <button type="submit" className="btn-primary stock-save-btn" disabled={saving}>
                  <StockIcon name="save" />
                  {saving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </footer>
            </form>
          </div>
        </div>
      ) : null}
    </div>
  )
}
