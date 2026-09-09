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

function Icon({ d }: { d: string }) {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d={d} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

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
        fetchStockItems({
          search: search.trim() || undefined,
          category,
        }),
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
  }, [search, category])

  useEffect(() => {
    if (!canView) return
    void load()
  }, [canView, load])

  const categories = options?.categories ?? []
  const units = options?.units ?? []
  const locations = options?.locations ?? []

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
  const q = search.trim().toLocaleLowerCase('tr-TR')
  const stockedPlaces = useMemo(
    () =>
      places.filter((p) => {
        if (!(p.itemCount > 0 || p.lowCount > 0)) return false
        if (!q) return true
        return p.name.toLocaleLowerCase('tr-TR').includes(q)
      }),
    [places, q],
  )
  const emptyPlaces = useMemo(
    () =>
      places.filter((p) => {
        if (!(p.itemCount === 0 && p.lowCount === 0)) return false
        if (!q) return true
        return p.name.toLocaleLowerCase('tr-TR').includes(q)
      }),
    [places, q],
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
              <div className="stat-chip mobile-inline-chip">
                {loading ? '…' : `${summary?.catalogCount ?? 0} malzeme`}
              </div>
            </div>
            <p className="muted small employees-toolbar-lead">
              {canManage
                ? 'Tesislerdeki malzeme. Giriş, çıkış, transfer ve sayımı buradan işleyin.'
                : 'Tesislerdeki malzeme özeti. Giriş, çıkış ve sayımı amirler yapar.'}
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <div className="stock-filters stock-toolbar-search">
              <input
                type="search"
                placeholder="Malzeme, kod, marka veya yer"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
              <select value={category} onChange={(e) => setCategory(e.target.value ? Number(e.target.value) : '')}>
                <option value="">Tüm gruplar</option>
                {categories.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>
            {canManage ? (
              <>
                <button type="button" className="btn-secondary" onClick={() => openMove()}>
                  Hareket
                </button>
                <button type="button" className="btn-primary" onClick={openCreateItem}>
                  Malzeme ekle
                </button>
              </>
            ) : null}
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="staff-dash-cards stock-kpis">
          <div className="staff-dash-card is-people">
            <span>
              <Icon d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" />
              Katalog
            </span>
            <strong className={loading ? 'is-skeleton' : undefined}>
              {loading ? '—' : fmtQty(summary?.catalogCount ?? 0)}
            </strong>
            <small>Tanımlı malzeme çeşidi</small>
          </div>
          <div className="staff-dash-card is-facilities">
            <span>
              <Icon d="M3 21h18M5 21V8h14v13M8 11h3v3H8v-3Z" />
              Stoklu yer
            </span>
            <strong className={loading ? 'is-skeleton' : undefined}>
              {loading ? '—' : fmtQty(summary?.locationsWithStock ?? 0)}
            </strong>
            <small>{fmtQty(summary?.locationCount ?? 0)} tesis</small>
          </div>
          <div className="staff-dash-card is-stock-low">
            <span>
              <Icon d="M12 9v4M12 17h.01M10.3 4.3 2.8 17a2 2 0 0 0 1.7 3h15a2 2 0 0 0 1.7-3L13.7 4.3a2 2 0 0 0-3.4 0Z" />
              Kritik stok
            </span>
            <strong className={loading ? 'is-skeleton' : undefined}>
              {loading ? '—' : fmtQty(summary?.lowCount ?? 0)}
            </strong>
            <small>Asgari miktarın altında</small>
          </div>
        </div>

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
                    <th>Yer</th>
                    {canManage ? <th></th> : null}
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={canManage ? 5 : 4}>Yükleniyor…</td>
                    </tr>
                  ) : items.length === 0 ? (
                    <tr>
                      <td colSpan={canManage ? 5 : 4}>Malzeme bulunamadı.</td>
                    </tr>
                  ) : (
                    items.map((row) => (
                      <tr key={row.id} className={row.isLow ? 'row-error' : undefined}>
                        <td>
                          <strong>{row.name}</strong>
                          <div className="muted small">
                            {row.code ?? 'Kod yok'}
                            {row.brand ? ` · ${row.brand}` : ''}
                            {row.model ? ` ${row.model}` : ''}
                          </div>
                        </td>
                        <td>{row.categoryLabel}</td>
                        <td>
                          {fmtQty(row.totalQuantity)} {row.unitLabel}
                          {row.isLow ? <span className="stock-pill is-low">Kritik</span> : null}
                        </td>
                        <td>{row.locationCount}</td>
                        {canManage ? (
                          <td className="stock-row-actions">
                            <button type="button" className="skills-link-btn" onClick={() => void openEditItem(row)}>
                              Düzenle
                            </button>
                            <button
                              type="button"
                              className="skills-link-btn"
                              onClick={() => openMove({ stockItemId: row.id, movementType: 1 })}
                            >
                              Giriş
                            </button>
                            <button type="button" className="skills-link-btn catalog-danger" onClick={() => void onArchive(row)}>
                              Kaldır
                            </button>
                          </td>
                        ) : null}
                      </tr>
                    ))
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
        <div className="org-modal-backdrop" onClick={() => setItemMode(null)}>
          <div className="org-modal panel" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="org-modal-head">
              <div>
                <p className="org-modal-eyebrow">Stok</p>
                <h2>{itemMode === 'create' ? 'Malzeme ekle' : 'Malzeme düzenle'}</h2>
              </div>
            </div>
            <form className="org-form" onSubmit={(e) => void onSaveItem(e)}>
              {itemError ? <div className="form-error">{itemError}</div> : null}
              <label>
                Ad
                <input
                  value={itemForm.name}
                  onChange={(e) => setItemForm((f) => ({ ...f, name: e.target.value }))}
                  required
                  maxLength={200}
                  autoFocus
                />
              </label>
              <label>
                Kod
                <input
                  value={itemForm.code ?? ''}
                  onChange={(e) => setItemForm((f) => ({ ...f, code: e.target.value }))}
                  maxLength={40}
                  placeholder="SES-MIK-EL"
                />
              </label>
              <label>
                Grup
                <select
                  value={itemForm.category}
                  onChange={(e) => setItemForm((f) => ({ ...f, category: Number(e.target.value) as StockItemRow['category'] }))}
                >
                  {categories.map((c: StockLookup) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Birim
                <select
                  value={itemForm.unit}
                  onChange={(e) => setItemForm((f) => ({ ...f, unit: Number(e.target.value) as StockItemRow['unit'] }))}
                >
                  {units.map((u) => (
                    <option key={u.value} value={u.value}>
                      {u.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Marka
                <input
                  value={itemForm.brand ?? ''}
                  onChange={(e) => setItemForm((f) => ({ ...f, brand: e.target.value }))}
                  maxLength={80}
                />
              </label>
              <label>
                Model
                <input
                  value={itemForm.model ?? ''}
                  onChange={(e) => setItemForm((f) => ({ ...f, model: e.target.value }))}
                  maxLength={80}
                />
              </label>
              <label>
                Asgari miktar
                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={itemForm.minQuantity}
                  onChange={(e) => setItemForm((f) => ({ ...f, minQuantity: Number(e.target.value) }))}
                />
              </label>
              <label className="span-2">
                Açıklama
                <textarea
                  rows={3}
                  value={itemForm.description ?? ''}
                  onChange={(e) => setItemForm((f) => ({ ...f, description: e.target.value }))}
                  maxLength={1000}
                />
              </label>
              <label className="checkbox-row">
                <input
                  type="checkbox"
                  checked={itemForm.isActive}
                  onChange={(e) => setItemForm((f) => ({ ...f, isActive: e.target.checked }))}
                />
                Aktif
              </label>
              <div className="org-form-actions">
                <button type="button" className="btn-secondary" onClick={() => setItemMode(null)}>
                  Vazgeç
                </button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}

      {moveOpen ? (
        <div className="org-modal-backdrop" onClick={() => setMoveOpen(false)}>
          <div className="org-modal panel" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="org-modal-head">
              <div>
                <p className="org-modal-eyebrow">Stok</p>
                <h2>Stok hareketi</h2>
              </div>
            </div>
            <form className="org-form" onSubmit={(e) => void onSaveMove(e)}>
              {moveError ? <div className="form-error">{moveError}</div> : null}
              <label>
                Tür
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
                Malzeme
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
                  Kaynak yer
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
                  {move.movementType === 4 ? 'Yer' : 'Hedef yer'}
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
                {move.movementType === 4 ? 'Sayılan miktar' : 'Miktar'}
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
                Tarih
                <input
                  type="date"
                  value={move.occurredOn}
                  onChange={(e) => setMove((m) => ({ ...m, occurredOn: e.target.value }))}
                />
              </label>
              <label className="span-2">
                Açıklama
                <input
                  value={move.reason}
                  onChange={(e) => setMove((m) => ({ ...m, reason: e.target.value }))}
                  maxLength={400}
                  placeholder="Satın alma, etkinlik çıkışı, sayım…"
                />
              </label>
              <div className="org-form-actions">
                <button type="button" className="btn-secondary" onClick={() => setMoveOpen(false)}>
                  Vazgeç
                </button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </div>
            </form>
          </div>
        </div>
      ) : null}
    </div>
  )
}
