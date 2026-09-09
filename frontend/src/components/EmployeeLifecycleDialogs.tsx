import { useEffect, useState, type FormEvent } from 'react'
import {
  archiveEmployee,
  createMovement,
  fetchEmployeeById,
  fetchMovementFormOptions,
  setEmployeeStatus,
  type MovementFormOptions,
} from '@/api/employeesApi'
import { ApiClientError } from '@/api/client'
import { useConfirm } from '@/components/ConfirmDialog'

function todayIso() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

export function WorkplaceChangeDialog({
  employeeId,
  onClose,
  onSaved,
}: {
  employeeId: string
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const [options, setOptions] = useState<MovementFormOptions | null>(null)
  const [currentUnitId, setCurrentUnitId] = useState('')
  const [currentFacilityId, setCurrentFacilityId] = useState('')
  const [currentUnitName, setCurrentUnitName] = useState('—')
  const [currentFacilityName, setCurrentFacilityName] = useState('—')
  const [newUnitId, setNewUnitId] = useState('')
  const [newFacilityId, setNewFacilityId] = useState('')
  const [startDate, setStartDate] = useState(todayIso())
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const [detail, opts] = await Promise.all([
          fetchEmployeeById(employeeId),
          fetchMovementFormOptions(),
        ])
        if (cancelled) return
        const unitId = detail.corporate.unitId ?? ''
        const facilityId = detail.corporate.facilityId ?? ''
        setCurrentUnitId(unitId)
        setCurrentFacilityId(facilityId)
        setCurrentUnitName(detail.corporate.unitName ?? '—')
        setCurrentFacilityName(detail.corporate.facilityName ?? '—')
        setNewUnitId(unitId)
        setNewFacilityId(facilityId)
        setOptions(opts)
      } catch (err) {
        if (!cancelled)
          setError(err instanceof ApiClientError ? err.message : 'Form yüklenemedi.')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [employeeId])

  const unitOptions = [...(options?.units ?? [])]
  for (const f of options?.facilities ?? []) {
    if (!unitOptions.some((u) => u.id === f.id)) unitOptions.push(f)
  }
  unitOptions.sort((a, b) => a.name.localeCompare(b.name, 'tr'))

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    const nextUnit = newUnitId || currentUnitId
    const nextFacility = newFacilityId || currentFacilityId
    const unitChanged = nextUnit !== currentUnitId
    const facilityChanged = nextFacility !== currentFacilityId
    if (!unitChanged && !facilityChanged) {
      setError('Yeni birim veya tesis seçin.')
      return
    }
    if (!reason.trim()) {
      setError('Gerekçe zorunludur.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await createMovement(employeeId, {
        movementType: unitChanged ? 1 : 2,
        startDate,
        reason: reason.trim(),
        oldUnitId: currentUnitId || null,
        newUnitId: nextUnit || null,
        oldFacilityId: currentFacilityId || null,
        newFacilityId: nextFacility || null,
      })
      await onSaved()
    } catch (err) {
      if (err instanceof ApiClientError) {
        const extra = err.validationErrors
          ? Object.values(err.validationErrors).flat().join(' ')
          : ''
        setError([err.message, extra].filter(Boolean).join(' — '))
      } else {
        setError('Görev yeri güncellenemedi.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="emp-lifecycle-backdrop" role="presentation" onClick={onClose}>
      <div
        className="emp-lifecycle-dialog panel"
        role="dialog"
        aria-modal
        aria-labelledby="workplace-dialog-title"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="emp-lifecycle-head">
          <div>
            <p className="org-eyebrow">Kadro</p>
            <h2 id="workplace-dialog-title">Görev yeri değiştir</h2>
            <p className="muted small">
              Birim veya tesis güncellenir; geçmişe hareket kaydı düşer. Teşkilat şeması yeni
              yere göre yenilenir.
            </p>
          </div>
          <button type="button" className="btn-secondary" onClick={onClose}>
            Kapat
          </button>
        </header>
        {loading ? (
          <p className="muted">Yükleniyor…</p>
        ) : (
          <form className="emp-lifecycle-form" onSubmit={(e) => void onSubmit(e)}>
            <p className="muted small span-2">
              Mevcut: <strong>{currentUnitName}</strong>
              {currentFacilityName !== '—' ? ` · ${currentFacilityName}` : ''}
            </p>
            <label>
              <span>Yeni birim</span>
              <select value={newUnitId} onChange={(e) => setNewUnitId(e.target.value)}>
                <option value="">Seçin</option>
                {(unitOptions).map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Yeni tesis</span>
              <select value={newFacilityId} onChange={(e) => setNewFacilityId(e.target.value)}>
                <option value="">Değişmesin / yok</option>
                {(options?.facilities ?? []).map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Geçerlilik tarihi</span>
              <input
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
                required
              />
            </label>
            <label className="span-2">
              <span>Gerekçe</span>
              <textarea
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                rows={3}
                placeholder="Örn. birim içi görev yeri değişikliği…"
                required
              />
            </label>
            {error ? (
              <div className="form-error span-2" role="alert">
                {error}
              </div>
            ) : null}
            <div className="emp-lifecycle-actions span-2">
              <button type="button" className="btn-secondary" onClick={onClose} disabled={busy}>
                Vazgeç
              </button>
              <button type="submit" className="btn-primary" disabled={busy}>
                {busy ? 'Kaydediliyor…' : 'Görev yerini güncelle'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}

export function TransferDirectorateDialog({
  employeeId,
  fullName,
  onClose,
  onSaved,
}: {
  employeeId: string
  fullName: string
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const [destination, setDestination] = useState('')
  const [effectiveDate, setEffectiveDate] = useState(todayIso())
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!reason.trim()) {
      setError('Gerekçe zorunludur.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const dest = destination.trim()
      await setEmployeeStatus(employeeId, {
        status: 8,
        effectiveDate,
        reason: dest ? `Nakil: ${dest}. ${reason.trim()}` : reason.trim(),
      })
      await onSaved()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Nakil kaydedilemedi.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="emp-lifecycle-backdrop" role="presentation" onClick={onClose}>
      <div
        className="emp-lifecycle-dialog panel"
        role="dialog"
        aria-modal
        aria-labelledby="transfer-dialog-title"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="emp-lifecycle-head">
          <div>
            <p className="org-eyebrow">Nakil</p>
            <h2 id="transfer-dialog-title">Başka müdürlüğe geçti</h2>
            <p className="muted small">
              <strong>{fullName}</strong> bu müdürlüğün kadrosundan düşer, pasif nakil durumuna
              alınır ve teşkilat şemasında görünmez.
            </p>
          </div>
          <button type="button" className="btn-secondary" onClick={onClose}>
            Kapat
          </button>
        </header>
        <form className="emp-lifecycle-form" onSubmit={(e) => void onSubmit(e)}>
          <label className="span-2">
            <span>Geçtiği müdürlük</span>
            <input
              type="text"
              value={destination}
              onChange={(e) => setDestination(e.target.value)}
              placeholder="Örn. Fen İşleri Müdürlüğü"
            />
          </label>
          <label>
            <span>Geçerlilik tarihi</span>
            <input
              type="date"
              value={effectiveDate}
              onChange={(e) => setEffectiveDate(e.target.value)}
              required
            />
          </label>
          <label className="span-2">
            <span>Gerekçe</span>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              placeholder="Örn. kurum içi nakil / atama yazısı…"
              required
            />
          </label>
          {error ? (
            <div className="form-error span-2" role="alert">
              {error}
            </div>
          ) : null}
          <div className="emp-lifecycle-actions span-2">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={busy}>
              Vazgeç
            </button>
            <button type="submit" className="btn-primary" disabled={busy}>
              {busy ? 'Kaydediliyor…' : 'Nakli kaydet'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function DeleteEmployeeDialog({
  employeeId,
  fullName,
  onClose,
  onDeleted,
}: {
  employeeId: string
  fullName: string
  onClose: () => void
  onDeleted: () => Promise<void>
}) {
  const confirm = useConfirm()
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!reason.trim()) {
      setError('Gerekçe zorunludur.')
      return
    }
    const ok = await confirm({
      title: 'Personeli sil',
      message: `“${fullName}” listeden ve teşkilat şemasından kalkacak. Geçmiş korunur; kayıt fiziksel olarak silinmez.`,
      confirmLabel: 'Sil',
      tone: 'danger',
    })
    if (!ok) return
    setBusy(true)
    setError(null)
    try {
      await archiveEmployee(employeeId, reason.trim())
      await onDeleted()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silme başarısız.')
      setBusy(false)
    }
  }

  return (
    <div className="emp-lifecycle-backdrop" role="presentation" onClick={onClose}>
      <div
        className="emp-lifecycle-dialog panel"
        role="dialog"
        aria-modal
        aria-labelledby="delete-employee-title"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="emp-lifecycle-head">
          <div>
            <p className="org-eyebrow">Kadro</p>
            <h2 id="delete-employee-title">Personeli sil</h2>
            <p className="muted small">
              <strong>{fullName}</strong> personel listesinde ve şemada görünmez. Yanlış kayıt
              veya kadro dışı bırakma için kullanın.
            </p>
          </div>
          <button type="button" className="btn-secondary" onClick={onClose}>
            Kapat
          </button>
        </header>
        <form className="emp-lifecycle-form" onSubmit={(e) => void onSubmit(e)}>
          <label className="span-2">
            <span>Gerekçe</span>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              placeholder="Örn. mükerrer kayıt / kadro dışı…"
              required
            />
          </label>
          {error ? (
            <div className="form-error span-2" role="alert">
              {error}
            </div>
          ) : null}
          <div className="emp-lifecycle-actions span-2">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={busy}>
              Vazgeç
            </button>
            <button type="submit" className="btn-primary emp-archive-confirm" disabled={busy}>
              {busy ? 'Siliniyor…' : 'Sil'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
