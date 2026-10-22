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

const today = () => new Date().toLocaleDateString('sv-SE', { timeZone: 'Europe/Istanbul' })
const dateLabel = (value: string) => value.split('-').reverse().join('.')
function errorMessage(err: unknown) {
  if (!(err instanceof ApiClientError)) return 'İşlem tamamlanamadı. Bağlantıyı kontrol edip tekrar deneyin.'
  return err.validationErrors ? Object.values(err.validationErrors).flat().join(' ') : err.message
}

export function StockCustodiesPanel({ employeeId }: { employeeId?: string }) {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.StockView)
  const canManage = hasPermission(PermissionCodes.StockManage)
  const loadSequence = useRef(0)
  const [rows, setRows] = useState<StockCustody[]>([])
  const [items, setItems] = useState<StockItemRow[]>([])
  const [employees, setEmployees] = useState<CustodyEmployee[]>([])
  const [options, setOptions] = useState<StockOptions | null>(null)
  const [includeReturned, setIncludeReturned] = useState(false)
  const [search, setSearch] = useState('')
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

  const load = useCallback(async () => {
    const sequence = ++loadSequence.current
    setLoading(true)
    setError('')
    try {
      const [records, catalog, opts, people] = await Promise.all([
        fetchCustodies({ employeeId, includeReturned }), fetchStockItems(), fetchStockOptions(),
        canManage ? fetchCustodyEmployees() : Promise.resolve([]),
      ])
      if (sequence !== loadSequence.current) return
      setRows(records)
      setItems(catalog)
      setOptions(opts)
      setEmployees(people)
    } catch (err) { if (sequence === loadSequence.current) setError(errorMessage(err)) }
    finally { if (sequence === loadSequence.current) setLoading(false) }
  }, [employeeId, includeReturned, canManage])

  useEffect(() => { if (canView) void load(); return () => { loadSequence.current++ } }, [load, canView])
  useEffect(() => { setPage(0) }, [search, includeReturned, employeeId])
  const filtered = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('tr-TR')
    return rows.filter(row => [row.employeeName, row.employeeNumber, row.itemName, row.itemCode,
      row.locationName, row.serialNumber].join(' ').toLocaleLowerCase('tr-TR').includes(term))
  }, [rows, search])
  const pages = Math.max(1, Math.ceil(filtered.length / 25))
  const currentPage = Math.min(page, pages - 1)
  const oldRow = rows.find(row => row.id === form?.replacesId)
  const selectedItem = items.find(item => item.id === form?.stockItemId)
  const balances = selectedItem?.locations ?? []
  const available = (balances.find(b => b.locationId === form?.locationId)?.availableQuantity ?? 0)
    + (oldRow && oldRow.stockItemId === form?.stockItemId && oldRow.locationId === form?.locationId ? oldRow.quantity : 0)

  function openForm(old?: StockCustody, transfer = false) {
    setTransferOnly(transfer)
    setFormError('')
    setForm({
      id: crypto.randomUUID(), stockItemId: old?.stockItemId ?? '', locationId: old?.locationId ?? '',
      employeeId: transfer ? '' : old?.employeeId ?? employeeId ?? '', quantity: old?.quantity ?? 1,
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
      await returnCustody(returnRow.id, { returnedOn: returnDate, notes: returnNotes })
      setReturnRow(null)
      await load()
    } catch (err) { setFormError(errorMessage(err)) }
    finally { setSaving(false) }
  }

  if (!canView) return <p className="form-error">Zimmetleri görüntüleme yetkiniz yok.</p>
  return <section className="panel custody-panel">
    <div className="employees-toolbar">
      <div><h2>Zimmetli malzemeler</h2><p className="muted">Demirbaşları personele atayın, değiştirin ve iadelerini takip edin.</p></div>
      {canManage && <button className="btn-primary" type="button" disabled={loading || !!error} onClick={() => openForm()}>Yeni zimmet</button>}
    </div>
    <p className="muted small">Zimmet, malzemenin toplam stoğunu değiştirmez; kullanılabilir miktarını azaltır. İade edilen miktar kaynak yerin kullanılabilir stoğuna döner.</p>
    {error && <p className="form-error" role="alert">{error} <button type="button" className="btn-secondary" onClick={() => void load()}>Tekrar yükle</button></p>}
    <div className="custody-filters">
      <label>Arama<input type="search" placeholder="Personel, sicil, malzeme, yer veya seri numarası" value={search} onChange={e => setSearch(e.target.value)} /></label>
      <label className="custody-checkbox"><input type="checkbox" checked={includeReturned} onChange={e => setIncludeReturned(e.target.checked)} />İade ve değişim geçmişini göster</label>
    </div>
    <div className="employees-table-wrap"><table className="data-table">
      <thead><tr><th>Personel</th><th>Malzeme</th><th>Kaynak yer</th><th>Miktar</th><th>Zimmet tarihi</th><th>Durum / geçmiş</th>{canManage && <th>İşlem</th>}</tr></thead>
      <tbody>{loading ? <tr><td colSpan={canManage ? 7 : 6}>Yükleniyor…</td></tr> : filtered.length === 0 ?
        <tr><td colSpan={canManage ? 7 : 6}>Bu filtrelere uygun zimmet bulunamadı.</td></tr> :
        filtered.slice(currentPage * 25, (currentPage + 1) * 25).map(row => <tr key={row.id}>
          <td>{hasPermission(PermissionCodes.EmployeesView) ? <Link to={`/employees/${row.employeeId}`}>{row.employeeName}</Link> : row.employeeName}<div className="muted small">{row.employeeNumber}</div></td>
          <td><strong>{row.itemName}</strong><div className="muted small">{row.itemCode}{row.serialNumber ? ` · Seri: ${row.serialNumber}` : ''}</div>{row.notes && <div className="small">{row.notes}</div>}</td>
          <td>{row.locationName}</td><td>{fmtQty(row.quantity)} {row.unitLabel}</td><td>{dateLabel(row.assignedOn)}</td>
          <td>{row.returnedOn ? <>Kapandı · {dateLabel(row.returnedOn)}<div className="muted small">{row.returnNotes}</div></> : <span className="stock-pill">Aktif zimmet</span>}{row.replacesId && <div className="muted small">Önceki zimmet yerine verildi</div>}</td>
          {canManage && <td><div className="custody-actions">{!row.returnedOn && <>
            <button type="button" className="btn-secondary" disabled={saving || loading || !!error} onClick={() => openForm(row, true)}>Personeli değiştir</button>
            <button type="button" className="btn-secondary" disabled={saving || loading || !!error} onClick={() => openForm(row)}>Malzemeyi değiştir</button>
            <button type="button" className="btn-secondary" disabled={saving || loading || !!error} onClick={() => { setFormError(''); setReturnRow(row); setReturnDate(today()); setReturnNotes('') }}>İade al</button>
          </>}</div></td>}
        </tr>)}</tbody>
    </table></div>
    <div className="custody-pagination"><span>{filtered.length} kayıt · Sayfa {currentPage + 1} / {pages}</span>
      <button type="button" className="btn-secondary" disabled={currentPage === 0} onClick={() => setPage(currentPage - 1)}>Önceki</button>
      <button type="button" className="btn-secondary" disabled={currentPage + 1 >= pages} onClick={() => setPage(currentPage + 1)}>Sonraki</button>
    </div>

    {form && <div className="org-modal-backdrop stock-modal-backdrop">
      <div className="org-modal panel stock-modal custody-modal" role="dialog" aria-modal="true" aria-labelledby="custody-title">
        <header className="stock-modal-head"><h2 id="custody-title">{transferOnly ? 'Zimmetin personelini değiştir' : oldRow ? 'Zimmetli malzemeyi değiştir' : 'Yeni zimmet'}</h2><button className="btn-secondary" type="button" disabled={saving} onClick={() => setForm(null)} aria-label="Kapat">Kapat</button></header>
        {transferOnly && <p>Malzeme, seri numarası, kaynak yer ve miktar korunur. Yalnızca yeni personeli ve devir tarihini seçin.</p>}
        {oldRow && <p>{oldRow.employeeName} üzerindeki {fmtQty(oldRow.quantity)} {oldRow.unitLabel} {oldRow.itemName} iade alınacak, aşağıdaki yeni zimmet açılacak.</p>}
        <form className="org-form stock-modal-form" onSubmit={e => void save(e)}>
          <fieldset disabled={saving} className="custody-fieldset"><div className="stock-form-grid">
            <label>Personel<select aria-label="Personel" required value={form.employeeId} onChange={e => setForm({ ...form, employeeId: e.target.value })}><option value="">Aktif personel seçin</option>{employees.filter(p => (!employeeId || p.id === employeeId || !!oldRow) && (!transferOnly || p.id !== oldRow?.employeeId)).map(p => <option key={p.id} value={p.id}>{p.name}{p.employeeNumber ? ` · ${p.employeeNumber}` : ''}</option>)}</select></label>
            <label>Demirbaş<select aria-label="Demirbaş" disabled={transferOnly} required value={form.stockItemId} onChange={e => setForm({ ...form, stockItemId: e.target.value, locationId: '', serialNumber: '' })}><option value="">Malzeme seçin</option>{items.filter(i => i.kind === 2).map(i => <option key={i.id} value={i.id}>{i.name}{i.code ? ` · ${i.code}` : ''}</option>)}</select></label>
            <label>Kaynak stok yeri<select aria-label="Kaynak stok yeri" disabled={transferOnly} required value={form.locationId} onChange={e => setForm({ ...form, locationId: e.target.value })}><option value="">Yer seçin</option>{(options?.locations ?? []).filter(loc => balances.some(b => b.locationId === loc.id)).map(loc => <option key={loc.id} value={loc.id}>{loc.name}</option>)}</select></label>
            <label>Miktar<input aria-label="Miktar" disabled={transferOnly} required type="number" min="0.01" step="0.01" max={available || undefined} value={form.quantity} onChange={e => setForm({ ...form, quantity: Number(e.target.value) })} /><span className="muted small">Kullanılabilir: {fmtQty(available)} {selectedItem?.unitLabel}</span></label>
            <label>Zimmet / değişim tarihi<input required type="date" min={oldRow?.assignedOn} max={today()} value={form.assignedOn} onChange={e => setForm({ ...form, assignedOn: e.target.value })} /></label>
            <label>Seri / demirbaş numarası<input aria-label="Seri / demirbaş numarası" disabled={transferOnly} maxLength={100} value={form.serialNumber ?? ''} onChange={e => setForm({ ...form, serialNumber: e.target.value })} /><span className="muted small">Seri numarası girildiğinde miktar 1 olmalıdır.</span></label>
            <label>Açıklama<textarea maxLength={400} value={form.notes ?? ''} onChange={e => setForm({ ...form, notes: e.target.value })} /></label>
          </div></fieldset>
          {!items.some(i => i.kind === 2) && <p className="form-error">Zimmetlenebilir demirbaş yok. Stok takip ekranında malzeme türünü “Demirbaş” olarak belirleyin.</p>}
          {formError && <p role="alert" className="form-error">{formError}</p>}
          <footer className="org-form-actions"><button className="btn-secondary" type="button" disabled={saving} onClick={() => setForm(null)}>Vazgeç</button><button className="btn-primary" type="submit" disabled={saving || available < form.quantity || available <= 0}>{saving ? 'Kaydediliyor…' : transferOnly ? 'Personel devrini kaydet' : oldRow ? 'İade al ve yeni zimmet aç' : 'Zimmeti kaydet'}</button></footer>
        </form>
      </div>
    </div>}
    {returnRow && <div className="org-modal-backdrop stock-modal-backdrop"><div className="org-modal panel stock-modal custody-modal" role="dialog" aria-modal="true" aria-labelledby="custody-return-title">
      <header className="stock-modal-head"><h2 id="custody-return-title">Zimmet iadesi</h2><button type="button" className="btn-secondary" disabled={saving} onClick={() => setReturnRow(null)}>Kapat</button></header>
      <p>{returnRow.employeeName} · {returnRow.itemName} · {fmtQty(returnRow.quantity)} {returnRow.unitLabel}</p>
      <p>İade alınan miktar {returnRow.locationName} yerinde yeniden kullanılabilir olacak.</p>
      <form className="org-form stock-modal-form" onSubmit={e => void saveReturn(e)}><fieldset className="custody-fieldset stock-form-grid" disabled={saving}>
        <label>İade tarihi<input required type="date" min={returnRow.assignedOn} max={today()} value={returnDate} onChange={e => setReturnDate(e.target.value)} /></label>
        <label>İade açıklaması<textarea maxLength={400} value={returnNotes} onChange={e => setReturnNotes(e.target.value)} /></label>
      </fieldset>{formError && <p className="form-error" role="alert">{formError}</p>}
        <footer className="org-form-actions"><button className="btn-secondary" type="button" disabled={saving} onClick={() => setReturnRow(null)}>Vazgeç</button><button type="submit" className="btn-primary" disabled={saving}>{saving ? 'Kaydediliyor…' : 'İadeyi onayla'}</button></footer>
      </form></div></div>}
  </section>
}

export function StockCustodiesPage() {
  return <div className="employees-page stock-page"><StockCustodiesPanel /></div>
}
