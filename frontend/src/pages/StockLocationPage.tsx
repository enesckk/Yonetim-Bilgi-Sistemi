import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useParams } from 'react-router-dom'
import { ApiClientError } from '@/api/client'
import {
  createStockMovement,
  fetchStockItems,
  fetchStockLocation,
  fetchStockMovements,
  fetchStockOptions,
  fmtQty,
  type StockItemRow,
  type StockLocationDetail,
  type StockMovementRow,
  type StockMovementType,
  type StockOptions,
} from '@/api/stockApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { PageBackLink } from '@/components/PageBackLink'

function todayIso() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

export function StockLocationPage() {
  const { id } = useParams()
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.StockView) || hasPermission(PermissionCodes.StockManage)
  const canManage = hasPermission(PermissionCodes.StockManage)

  const [detail, setDetail] = useState<StockLocationDetail | null>(null)
  const [moves, setMoves] = useState<StockMovementRow[]>([])
  const [items, setItems] = useState<StockItemRow[]>([])
  const [options, setOptions] = useState<StockOptions | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [moveOpen, setMoveOpen] = useState(false)
  const [moveError, setMoveError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [move, setMove] = useState({
    stockItemId: '',
    movementType: 1 as StockMovementType,
    fromLocationId: '',
    toLocationId: '',
    quantity: '1',
    occurredOn: todayIso(),
    reason: '',
  })

  const load = useCallback(async () => {
    if (!id || !canView) return
    setLoading(true)
    setError(null)
    try {
      const [loc, catalog, opts, history] = await Promise.all([
        fetchStockLocation(id),
        fetchStockItems(),
        fetchStockOptions(),
        fetchStockMovements({ locationId: id, take: 20 }),
      ])
      setDetail(loc)
      setItems(catalog)
      setOptions(opts)
      setMoves(history)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Yer stoku yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [id, canView])

  useEffect(() => {
    void load()
  }, [load])

  const openMove = (type: StockMovementType, itemId?: string) => {
    setMove({
      stockItemId: itemId ?? items[0]?.id ?? '',
      movementType: type,
      fromLocationId: type === 2 || type === 3 ? id ?? '' : '',
      toLocationId: type === 1 || type === 4 ? id ?? '' : type === 3 ? '' : id ?? '',
      quantity: '1',
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
      await load()
    } catch (err) {
      setMoveError(err instanceof ApiClientError ? err.message : 'Hareket kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Stok ekranı için yetkiniz yok.</p>
      </div>
    )
  }

  const lines = (detail?.lines ?? []).filter((row) => {
    if (!search.trim()) return true
    const q = search.trim().toLocaleLowerCase('tr')
    return (
      row.name.toLocaleLowerCase('tr').includes(q) ||
      (row.code ?? '').toLocaleLowerCase('tr').includes(q) ||
      row.categoryLabel.toLocaleLowerCase('tr').includes(q)
    )
  })
  const locations = options?.locations ?? []

  return (
    <div className="employees-page stock-page">
      <section className="panel">
        <PageBackLink to="/stock">Stok özeti</PageBackLink>
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>{detail?.name ?? 'Stok yeri'}</h2>
              {detail?.lowCount ? <span className="stock-pill is-low">{detail.lowCount} kritik</span> : null}
            </div>
            <p className="muted small employees-toolbar-lead">
              {detail ? `${detail.typeLabel} · ${detail.itemCount} çeşit malzeme` : 'Yükleniyor'}
            </p>
          </div>
          {canManage ? (
            <div className="employees-toolbar-actions">
              <button type="button" className="btn-secondary" onClick={() => openMove(1)}>
                Giriş
              </button>
              <button type="button" className="btn-secondary" onClick={() => openMove(2)}>
                Çıkış
              </button>
              <button type="button" className="btn-secondary" onClick={() => openMove(3)}>
                Transfer
              </button>
              <button type="button" className="btn-primary" onClick={() => openMove(4)}>
                Sayım
              </button>
            </div>
          ) : null}
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="stock-filters">
          <input
            type="search"
            placeholder="Bu yerdeki malzemeyi ara"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <div className="employees-table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>Malzeme</th>
                <th>Grup</th>
                <th>Miktar</th>
                <th>Asgari</th>
                {canManage ? <th></th> : null}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={canManage ? 5 : 4}>Yükleniyor…</td>
                </tr>
              ) : lines.length === 0 ? (
                <tr>
                  <td colSpan={canManage ? 5 : 4}>
                    {canManage ? 'Bu yerde henüz stok yok. Giriş ile başlatın.' : 'Bu tesiste kayıtlı stok yok.'}
                  </td>
                </tr>
              ) : (
                lines.map((row) => (
                  <tr key={row.stockItemId} className={row.isLow ? 'row-error' : undefined}>
                    <td>
                      <strong>{row.name}</strong>
                      <div className="muted small">
                        {row.code ?? 'Kod yok'}
                        {row.brand ? ` · ${row.brand}` : ''}
                      </div>
                    </td>
                    <td>{row.categoryLabel}</td>
                    <td>
                      {fmtQty(row.quantity)} {row.unitLabel}
                      {row.isLow ? <span className="stock-pill is-low">Kritik</span> : null}
                    </td>
                    <td>{fmtQty(row.minQuantity)}</td>
                    {canManage ? (
                      <td className="stock-row-actions">
                        <button type="button" className="skills-link-btn" onClick={() => openMove(1, row.stockItemId)}>
                          Giriş
                        </button>
                        <button type="button" className="skills-link-btn" onClick={() => openMove(2, row.stockItemId)}>
                          Çıkış
                        </button>
                      </td>
                    ) : null}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {moves.length > 0 ? (
          <section className="staff-dash-panel" style={{ marginTop: '0.85rem' }}>
            <header>
              <h3>Bu yerin son hareketleri</h3>
            </header>
            <ul className="staff-dash-feed">
              {moves.map((m) => (
                <li key={m.id}>
                  <strong>
                    {m.movementTypeLabel} · {m.itemName}
                  </strong>
                  <em>
                    {fmtQty(m.quantity)} {m.unitLabel}
                    {m.fromLocationName ? ` · ${m.fromLocationName}` : ''}
                    {m.toLocationName ? ` → ${m.toLocationName}` : ''}
                  </em>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </section>

      {moveOpen ? (
        <div className="org-modal-backdrop" onClick={() => setMoveOpen(false)}>
          <div className="org-modal panel" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <div className="org-modal-head">
              <div>
                <p className="org-modal-eyebrow">{detail?.name}</p>
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
