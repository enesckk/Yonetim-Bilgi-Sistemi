import { isSystemAdmin } from '@/auth/roles'
import { StockItemPhoto, StockPhotoEditor } from '@/components/StockItemPhoto'
import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ApiClientError } from '@/api/client'
import {
  assignCustody, fetchCustodies, fetchCustodyEmployees, fetchStockItems, fetchStockOptions,
  fmtQty, returnCustody, type AssignCustodyPayload, type CustodyEmployee,
  type StockCustody, type StockItemRow, type StockOptions,
} from '@/api/stockApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { StockIcon } from '@/components/StockIcon'

const today = () => new Date().toLocaleDateString('sv-SE', { timeZone: 'Europe/Istanbul' })
const remainingQty = (row: StockCustody) => row.remainingQuantity ?? (row.returnedOn ? 0 : row.quantity - (row.returnedQuantity ?? 0))
const registeredLabel = (value: string) => new Date(value).toLocaleDateString('tr-TR', { timeZone: 'Europe/Istanbul' })
const dateLabel = (value: string) => value.split('-').reverse().join('.')
function errorMessage(err: unknown) {
  if (!(err instanceof ApiClientError)) return 'İşlem tamamlanamadı. Bağlantıyı kontrol edip tekrar deneyin.'
  return err.validationErrors ? Object.values(err.validationErrors).flat().join(' ') : err.message
}

export function StockCustodiesPanel({ employeeId }: { employeeId?: string }) {
  const { hasPermission, user } = useAuth()
  const canView = hasPermission(PermissionCodes.StockView)
  const canManage = isSystemAdmin(user) && (hasPermission(PermissionCodes.StockManage) || hasPermission(PermissionCodes.StockCustodyManage))
  const loadSequence = useRef(0)
  const [rows, setRows] = useState<StockCustody[]>([])
  const [items, setItems] = useState<StockItemRow[]>([])
  const [employees, setEmployees] = useState<CustodyEmployee[]>([])
  const [options, setOptions] = useState<StockOptions | null>(null)
  const [includeReturned, setIncludeReturned] = useState(false)
  const [search, setSearch] = useState('')
  const [placeFilter, setPlaceFilter] = useState('')
  const [page, setPage] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [formError, setFormError] = useState('')
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<AssignCustodyPayload | null>(null)
  const [transferOnly, setTransferOnly] = useState(false)
  const [returnRow, setReturnRow] = useState<StockCustody | null>(null)
  const [returnDate, setReturnDate] = useState(today)
  const [returnNotes, setReturnNotes] = useState('')
  const [returnQuantity, setReturnQuantity] = useState(1)
  const [returnOperationId, setReturnOperationId] = useState('')

  const load = useCallback(async () => {
    const sequence = ++loadSequence.current
    setLoading(true)
    setError('')
    try {
      const [records, catalog, opts, people] = await Promise.all([
        fetchCustodies({ employeeId, includeReturned: true }), fetchStockItems(), fetchStockOptions(),
        canManage ? fetchCustodyEmployees() : Promise.resolve([]),
      ])
      if (sequence !== loadSequence.current) return
      setRows(records)
      setItems(catalog)
      setOptions(opts)
      setEmployees(people)
    } catch (err) { if (sequence === loadSequence.current) setError(errorMessage(err)) }
    finally { if (sequence === loadSequence.current) setLoading(false) }
  }, [employeeId, canManage])

  useEffect(() => { if (canView) void load(); return () => { loadSequence.current++ } }, [load, canView])
  useEffect(() => { setPage(0) }, [search, includeReturned, employeeId, placeFilter])
  const filtered = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr-TR')
    return rows.filter(row => (includeReturned || !row.returnedOn) && (!placeFilter || row.locationId === placeFilter) && [row.employeeName, row.employeeNumber, row.itemName, row.itemCode,
      row.locationName, row.serialNumber].join(' ').toLocaleLowerCase('tr-TR').includes(term))
  }, [rows, search, includeReturned, placeFilter])
  const activeRows = rows.filter(row => !row.returnedOn)
  const activePeople = new Set(activeRows.map(row => row.employeeId)).size
  const activeItems = new Set(activeRows.map(row => row.stockItemId)).size
  const places = Array.from(new Map(rows.map(row => [row.locationId, row.locationName])).entries())
    .sort((a, b) => a[1].localeCompare(b[1], 'tr'))
  const initials = (name: string) => name.trim().split(/\s+/).map(part => part[0]).filter(Boolean).slice(0, 2).join('')
  const pages = Math.max(1, Math.ceil(filtered.length / 25))
  const currentPage = Math.min(page, pages - 1)
  const oldRow = rows.find(row => row.id === form?.replacesId)
  const selectedItem = items.find(item => item.id === form?.stockItemId)
  const balances = selectedItem?.locations ?? []
  const available = (balances.find(b => b.locationId === form?.locationId)?.availableQuantity ?? 0)
    + (oldRow && oldRow.stockItemId === form?.stockItemId && oldRow.locationId === form?.locationId ? remainingQty(oldRow) : 0)

  function openForm(old?: StockCustody, transfer = false) {
    setTransferOnly(transfer)
    setFormError('')
    setForm({
      id: crypto.randomUUID(), stockItemId: old?.stockItemId ?? '', locationId: old?.locationId ?? '',
      employeeId: transfer ? '' : old?.employeeId ?? employeeId ?? '', quantity: old ? remainingQty(old) : 1,
      assignedOn: today(), serialNumber: old?.serialNumber ?? '', notes: transfer ? old?.notes ?? '' : '', replacesId: old?.id,
    })
  }

  async function save(e: FormEvent) {
    e.preventDefault()
    if (!form || saving) return
    setSaving(true)
    setFormError('')
    try {
      await assignCustody(form)
      setForm(null)
      await load()
    } catch (err) { setFormError(errorMessage(err)) }
    finally { setSaving(false) }
  }

  async function saveReturn(e: FormEvent) {
    e.preventDefault()
    if (!returnRow || saving) return
    setSaving(true)
    setFormError('')
    try {
      await returnCustody(returnRow.id, { operationId: returnOperationId, quantity: returnQuantity, returnedOn: returnDate, notes: returnNotes })
      setReturnRow(null)
      await load()
    } catch (err) { setFormError(errorMessage(err)) }
    finally { setSaving(false) }
  }

  if (!canView) return <p className="form-error">Zimmetleri görüntüleme yetkiniz yok.</p>
  return <section className="custody-workspace">
    <header className="custody-page-head">
      <div><span className="custody-eyebrow">PERSONEL ENVANTERİ</span><h2>Zimmetli malzemeler</h2><p>Kimde, hangi malzeme var? Zimmetleri ve teslim geçmişini tek yerden takip edin.</p></div>
      <div className="custody-head-actions">
        <Link className="custody-secondary" to="/stock"><StockIcon name="box" />Stok takibi</Link>
        {canManage && <button className="custody-primary" type="button" disabled={loading || !!error} onClick={() => openForm()}><StockIcon name="plus" />Yeni zimmet</button>}
      </div>
    </header>
    <div className="custody-stats">
      <article><span className="custody-stat-icon"><StockIcon name="check" /></span><div><span>Aktif zimmet</span><strong>{loading ? '—' : activeRows.length}<small>kayıt</small></strong></div></article>
      <article><span className="custody-stat-icon is-blue"><StockIcon name="users" /></span><div><span>Zimmetli personel</span><strong>{loading ? '—' : activePeople}<small>kişi</small></strong></div></article>
      <article><span className="custody-stat-icon is-amber"><StockIcon name="box" /></span><div><span>Malzeme çeşidi</span><strong>{loading ? '—' : activeItems}<small>demirbaş</small></strong></div></article>
    </div>
    {error && <p className="form-error" role="alert">{error} <button type="button" className="custody-secondary" onClick={() => void load()}>Tekrar yükle</button></p>}
    <div className="custody-register">
      <div className="custody-register-top">
        <div className="custody-view-tabs" role="tablist" aria-label="Zimmet görünümü">
          <button type="button" role="tab" aria-selected={!includeReturned} className={!includeReturned ? 'is-active' : ''} onClick={() => setIncludeReturned(false)}>Aktif zimmetler<span>{activeRows.length}</span></button>
          <button type="button" role="tab" aria-selected={includeReturned} className={includeReturned ? 'is-active' : ''} onClick={() => setIncludeReturned(true)}>Tüm geçmiş<span>{rows.length}</span></button>
        </div>
        <span className="custody-register-note"><span />Stokla birlikte takip edilir</span>
      </div>
      <div className="custody-toolbar">
        <label className="custody-search"><StockIcon name="search" /><input aria-label="Zimmetlerde ara" type="search" placeholder="Malzeme, personel veya seri numarası ara…" value={search} onChange={e => setSearch(e.target.value)} /></label>
        <label className="custody-place-filter"><span>Tesis</span><select aria-label="Tesis filtresi" value={placeFilter} onChange={e => setPlaceFilter(e.target.value)}><option value="">Tüm tesisler</option>{places.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
      </div>
      <div className="custody-table-scroll"><table className="custody-table">
        <thead><tr><th>Malzeme</th><th>Sorumlu personel</th><th>Tesis</th><th>Miktar</th><th>Teslim tarihi</th><th>Durum</th>{canManage && <th className="custody-action-heading">İşlemler</th>}</tr></thead>
        <tbody>{loading ? <tr><td className="custody-empty" colSpan={canManage ? 7 : 6}><StockIcon name="clock" /><strong>Zimmetler yükleniyor…</strong></td></tr> : filtered.length === 0 ?
          <tr><td className="custody-empty" colSpan={canManage ? 7 : 6}><StockIcon name="box" /><strong>{search || placeFilter ? 'Aramanıza uygun zimmet bulunamadı.' : 'Henüz zimmet kaydı yok.'}</strong><span>{search || placeFilter ? 'Aramayı veya tesis seçimini değiştirebilirsiniz.' : canManage ? 'Bir demirbaş seçip personele atayarak başlayın.' : 'Personellere yapılan zimmetler burada listelenir.'}</span></td></tr> :
          filtered.slice(currentPage * 25, (currentPage + 1) * 25).map(row => <tr className="custody-record" key={row.id}>
            <td><div className="custody-item-cell"><StockItemPhoto itemId={row.stockItemId} name={row.itemName} hasPhoto={row.hasPhoto} version={row.photoVersion} /><div><strong>{row.itemName}</strong><div className="custody-item-meta"><span>{row.itemCode || 'Kod belirtilmedi'}</span>{row.serialNumber && <span className="custody-serial">SN · {row.serialNumber}</span>}</div></div></div></td>
            <td><div className="custody-person"><span className="custody-avatar">{initials(row.employeeName)}</span><div>{hasPermission(PermissionCodes.EmployeesView) ? <Link to={`/employees/${row.employeeId}`}>{row.employeeName}</Link> : <strong>{row.employeeName}</strong>}<small>{row.employeeNumber || 'Personel'}</small></div></div></td>
            <td data-label="Tesis"><span className="custody-location">{row.locationName}</span></td>
            <td data-label="Miktar"><span className="custody-quantity">{fmtQty(row.returnedOn ? row.quantity : remainingQty(row))}<small>{row.unitLabel}</small></span></td>
            <td data-label="Teslim tarihi"><time className="custody-date" dateTime={row.assignedOn}>{dateLabel(row.assignedOn)}</time></td>
            <td className="custody-status-cell"><span className={`custody-status ${row.returnedOn ? 'is-returned' : ''}`}><span />{row.returnedOn ? ((row.returnedQuantity ?? row.quantity) < row.quantity ? 'Devir / değişim' : 'Teslim alındı') : 'Aktif zimmet'}</span><details className="custody-record-details"><summary>Kaydın ayrıntıları</summary>
              {canManage && hasPermission(PermissionCodes.FilesUpload) && <StockPhotoEditor itemId={row.stockItemId} name={row.itemName} hasPhoto={row.hasPhoto} onChanged={load} />}
              <p>İlk teslim: {fmtQty(row.quantity)} {row.unitLabel}{(row.returnedQuantity ?? 0) > 0 ? ` · İade: ${fmtQty(row.returnedQuantity)} ${row.unitLabel}` : ''}</p>
              <p>Sisteme kayıt: {row.itemCreatedAtUtc ? registeredLabel(row.itemCreatedAtUtc) : 'Bilgi bulunamadı'}</p>
              <p>İlk stok girişi: {row.firstStockEntryOn ? dateLabel(row.firstStockEntryOn) : 'Stok giriş hareketi bulunamadı'}</p>
              {(row.returns ?? []).map(r => <p key={r.id}>{dateLabel(r.returnedOn)} · {fmtQty(r.quantity)} {row.unitLabel} iade{r.notes ? ` · ${r.notes}` : ''}</p>)}{row.notes && <p>{row.notes}</p>}{row.returnedOn && <p>{dateLabel(row.returnedOn)} · {row.returnNotes || 'İade alındı.'}</p>}{row.replacesId && <p>Önceki zimmet kapatılarak oluşturuldu.</p>}</details></td>
            {canManage && <td className="custody-action-cell"><div className="custody-actions">{!row.returnedOn ? <>
              <button type="button" className="custody-transfer-btn" disabled={saving || loading || !!error} onClick={() => openForm(row, true)}><StockIcon name="transfer" />Personeli değiştir</button>
              <button type="button" className="custody-transfer-btn" aria-label="Malzemeyi değiştir" title="Malzemeyi değiştir" disabled={saving || loading || !!error} onClick={() => openForm(row)}><StockIcon name="edit" />Düzenle</button>
              <button type="button" className="custody-transfer-btn" aria-label="İade al" title="İade al" disabled={saving || loading || !!error} onClick={() => { setFormError(''); setReturnRow(row); setReturnDate(today()); setReturnNotes(''); setReturnQuantity(remainingQty(row)); setReturnOperationId(crypto.randomUUID()) }}>İade al</button>
            </> : <span className="custody-closed-note">Kayıt kapalı</span>}</div></td>}
          </tr>)}</tbody>
      </table></div>
      <footer className="custody-register-footer"><span><strong>{filtered.length}</strong> zimmet kaydı{pages > 1 ? ` · Sayfa ${currentPage + 1} / ${pages}` : ''}</span>{pages > 1 && <div><button type="button" className="custody-secondary" disabled={currentPage === 0} onClick={() => setPage(currentPage - 1)}>Önceki</button><button type="button" className="custody-secondary" disabled={currentPage + 1 >= pages} onClick={() => setPage(currentPage + 1)}>Sonraki</button></div>}<details className="custody-stock-help"><summary>Zimmet stoğu nasıl etkiler?</summary><p>Zimmet, toplam stoğu değiştirmez; kullanılabilir miktarı ayırır. İade edilen malzeme yeniden kullanılabilir olur.</p></details></footer>
    </div>

    {form && <div className="org-modal-backdrop stock-modal-backdrop">
      <div className={`org-modal panel stock-modal custody-modal${transferOnly ? ' is-transfer' : ''}`} role="dialog" aria-modal="true" aria-labelledby="custody-title">
        <header className="stock-modal-head"><h2 id="custody-title">{transferOnly ? 'Zimmetin personelini değiştir' : oldRow ? 'Zimmetli malzemeyi değiştir' : 'Yeni zimmet'}</h2><button className="btn-secondary" type="button" disabled={saving} onClick={() => setForm(null)} aria-label="Kapat">Kapat</button></header>
        {transferOnly && oldRow && <div className="custody-transfer-context"><span className="custody-item-symbol"><StockIcon name="box" /></span><div><strong>{oldRow.itemName}</strong><small>{fmtQty(remainingQty(oldRow))} {oldRow.unitLabel}{oldRow.serialNumber ? ` · ${oldRow.serialNumber}` : ''}</small><p>Mevcut sorumlu: <b>{oldRow.employeeName}</b></p></div></div>}
        {oldRow && !transferOnly && <p>{oldRow.employeeName} üzerindeki {fmtQty(remainingQty(oldRow))} {oldRow.unitLabel} {oldRow.itemName} iade alınacak, aşağıdaki yeni zimmet açılacak.</p>}
        <form className="org-form stock-modal-form" onSubmit={e => void save(e)}>
          <fieldset disabled={saving} className="custody-fieldset"><div className="stock-form-grid">
            <label>{transferOnly ? 'Yeni sorumlu personel' : 'Personel'}<select aria-label="Personel" required value={form.employeeId} onChange={e => setForm({ ...form, employeeId: e.target.value })}><option value="">Aktif personel seçin</option>{employees.filter(p => (!employeeId || p.id === employeeId || !!oldRow) && (!transferOnly || p.id !== oldRow?.employeeId)).map(p => <option key={p.id} value={p.id}>{p.name}{p.employeeNumber ? ` · ${p.employeeNumber}` : ''}</option>)}</select></label>
            {!transferOnly && <label>Demirbaş<select aria-label="Demirbaş" disabled={transferOnly} required value={form.stockItemId} onChange={e => setForm({ ...form, stockItemId: e.target.value, locationId: '', serialNumber: '' })}><option value="">Malzeme seçin</option>{items.filter(i => i.kind === 2).map(i => <option key={i.id} value={i.id}>{i.name}{i.code ? ` · ${i.code}` : ''}</option>)}</select></label>}
            {!transferOnly && <label>Kaynak stok yeri<select aria-label="Kaynak stok yeri" disabled={transferOnly} required value={form.locationId} onChange={e => setForm({ ...form, locationId: e.target.value })}><option value="">Yer seçin</option>{(options?.locations ?? []).filter(loc => balances.some(b => b.locationId === loc.id)).map(loc => <option key={loc.id} value={loc.id}>{loc.name}</option>)}</select></label>}
            {!transferOnly && <label>Miktar<input aria-label="Miktar" disabled={transferOnly} required type="number" min="0.01" step="0.01" max={available || undefined} value={form.quantity} onChange={e => setForm({ ...form, quantity: Number(e.target.value) })} /><span className="muted small">Kullanılabilir: {fmtQty(available)} {selectedItem?.unitLabel}</span></label>}
            <label>{transferOnly ? 'Devir tarihi' : 'Zimmet / değişim tarihi'}<input required type="date" min={oldRow?.assignedOn} max={today()} value={form.assignedOn} onChange={e => setForm({ ...form, assignedOn: e.target.value })} /></label>
            {!transferOnly && <label>Seri / demirbaş numarası<input aria-label="Seri / demirbaş numarası" disabled={transferOnly} maxLength={100} value={form.serialNumber ?? ''} onChange={e => setForm({ ...form, serialNumber: e.target.value })} /><span className="muted small">Seri numarası girildiğinde miktar 1 olmalıdır.</span></label>}
            <label className={transferOnly ? 'span-2' : undefined}>{transferOnly ? 'Devir notu (isteğe bağlı)' : 'Açıklama'}<textarea maxLength={400} value={form.notes ?? ''} onChange={e => setForm({ ...form, notes: e.target.value })} /></label>
          </div></fieldset>
          {!items.some(i => i.kind === 2) && <p className="form-error">Zimmetlenebilir demirbaş yok. Stok takip ekranında malzeme türünü “Demirbaş” olarak belirleyin.</p>}
          {formError && <p role="alert" className="form-error">{formError}</p>}
          <footer className="org-form-actions"><button className="btn-secondary" type="button" disabled={saving} onClick={() => setForm(null)}>Vazgeç</button><button className="btn-primary" type="submit" disabled={saving || available < form.quantity || available <= 0}>{saving ? 'Kaydediliyor…' : transferOnly ? 'Personel devrini kaydet' : oldRow ? 'İade al ve yeni zimmet aç' : 'Zimmeti kaydet'}</button></footer>
        </form>
      </div>
    </div>}
    {returnRow && <div className="org-modal-backdrop stock-modal-backdrop"><div className="org-modal panel stock-modal custody-modal" role="dialog" aria-modal="true" aria-labelledby="custody-return-title">
      <header className="stock-modal-head"><h2 id="custody-return-title">Zimmet iadesi</h2><button type="button" className="btn-secondary" disabled={saving} onClick={() => setReturnRow(null)}>Kapat</button></header>
      <p>{returnRow.employeeName} · {returnRow.itemName} · {fmtQty(remainingQty(returnRow))} {returnRow.unitLabel}</p>
      <p>İade alınan miktar {returnRow.locationName} yerinde yeniden kullanılabilir olacak.</p>
      <form className="org-form stock-modal-form" onSubmit={e => void saveReturn(e)}><fieldset className="custody-fieldset stock-form-grid" disabled={saving}>
        <label>İade miktarı<input aria-label="İade miktarı" readOnly={!!returnRow.serialNumber} required type="number" min={returnRow.serialNumber ? remainingQty(returnRow) : "0.01"} step={returnRow.serialNumber ? "1" : "0.01"} max={remainingQty(returnRow)} value={returnQuantity} onChange={e => setReturnQuantity(Number(e.target.value))} /><span className="muted small">{returnRow.serialNumber && 'Seri numaralı demirbaş tam iade alınır. '}Personelde kalacak: {fmtQty(Math.max(0, remainingQty(returnRow) - returnQuantity))} {returnRow.unitLabel}</span></label>
        <label>İade tarihi<input required type="date" min={returnRow.assignedOn} max={today()} value={returnDate} onChange={e => setReturnDate(e.target.value)} /></label>
        <label>İade açıklaması<textarea maxLength={400} value={returnNotes} onChange={e => setReturnNotes(e.target.value)} /></label>
      </fieldset>{formError && <p className="form-error" role="alert">{formError}</p>}
        <footer className="org-form-actions"><button className="btn-secondary" type="button" disabled={saving} onClick={() => setReturnRow(null)}>Vazgeç</button><button type="submit" className="btn-primary" disabled={saving || returnQuantity <= 0 || returnQuantity > remainingQty(returnRow)}>{saving ? 'Kaydediliyor…' : 'İadeyi onayla'}</button></footer>
      </form></div></div>}
  </section>
}

export function StockCustodiesPage() {
  return <div className="employees-page stock-page"><StockCustodiesPanel /></div>
}
