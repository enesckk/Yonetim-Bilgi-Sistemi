import { useState, type FormEvent } from 'react'
import { ApiClientError } from '@/api/client'
import {
  deleteSettlementArea,
  deleteSettlementSchool,
  updateSettlementProfile,
  upsertSettlementArea,
  upsertSettlementPopulation,
  upsertSettlementSchool,
  type SettlementArea,
  type SettlementSchool,
  type SettlementSummary,
} from '@/api/mapApi'
import { useAlert, useConfirm } from '@/components/ConfirmDialog'

export const SCHOOL_TYPES = ['Anaokulu', 'İlkokul', 'Ortaokul', 'Lise', 'İmam Hatip', 'Diğer'] as const
export const AREA_TYPES = ['Park', 'Meydan', 'Spor alanı', 'Açık etkinlik alanı', 'Çocuk oyun alanı', 'Yeşil alan'] as const

function numOrNull(value: string) {
  const t = value.trim()
  if (!t) return null
  const n = Number(t)
  return Number.isFinite(n) ? n : null
}

export function HeadmanEditor({
  detail,
  onSaved,
}: {
  detail: SettlementSummary
  onSaved: () => Promise<void>
}) {
  const alert = useAlert()
  const [name, setName] = useState(detail.headmanName ?? '')
  const [phone, setPhone] = useState(detail.headmanPhone ?? '')
  const [saving, setSaving] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    try {
      await updateSettlementProfile(detail.settlementId, {
        headmanName: name.trim() || null,
        headmanPhone: phone.trim() || null,
      })
      await onSaved()
    } catch (err) {
      await alert({
        title: 'Kaydedilemedi',
        message: err instanceof ApiClientError ? err.message : 'Muhtar bilgisi kaydedilemedi.',
        tone: 'warning',
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="settlement-edit" onSubmit={(e) => void onSubmit(e)}>
      <h3>Muhtar</h3>
      <label>
        Ad soyad
        <input value={name} onChange={(e) => setName(e.target.value)} maxLength={120} />
      </label>
      <label>
        Telefon
        <input value={phone} onChange={(e) => setPhone(e.target.value)} maxLength={30} inputMode="tel" />
      </label>
      <button type="submit" className="btn-secondary" disabled={saving}>
        {saving ? 'Kaydediliyor…' : 'Muhtarı kaydet'}
      </button>
    </form>
  )
}

export function PopulationEditor({
  detail,
  onSaved,
}: {
  detail: SettlementSummary
  onSaved: () => Promise<void>
}) {
  const alert = useAlert()
  const [year, setYear] = useState(String(detail.populationYear ?? new Date().getFullYear()))
  const [population, setPopulation] = useState(detail.population != null ? String(detail.population) : '')
  const [male, setMale] = useState(detail.maleCount != null ? String(detail.maleCount) : '')
  const [female, setFemale] = useState(detail.femaleCount != null ? String(detail.femaleCount) : '')
  const [child, setChild] = useState(detail.childCount != null ? String(detail.childCount) : '')
  const [saving, setSaving] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    const pop = Number(population)
    const y = Number(year)
    if (!Number.isFinite(pop) || pop < 1) {
      await alert({ title: 'Nüfus', message: 'Geçerli bir nüfus girin.', tone: 'warning' })
      return
    }
    setSaving(true)
    try {
      const maleN = numOrNull(male)
      const femaleN = numOrNull(female)
      const childN = numOrNull(child)
      await upsertSettlementPopulation(detail.settlementId, {
        year: y,
        population: pop,
        maleCount: maleN,
        femaleCount: femaleN,
        childCount: childN,
        source: 'manual',
        isOfficial: false,
      })
      await onSaved()
    } catch (err) {
      await alert({
        title: 'Kaydedilemedi',
        message: err instanceof ApiClientError ? err.message : 'Nüfus kaydedilemedi.',
        tone: 'warning',
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="settlement-edit" onSubmit={(e) => void onSubmit(e)}>
      <h3>Nüfus</h3>
      <div className="settlement-edit-row">
        <label>
          Yıl
          <input type="number" min={1990} max={2100} value={year} onChange={(e) => setYear(e.target.value)} required />
        </label>
        <label>
          Toplam
          <input type="number" min={1} value={population} onChange={(e) => setPopulation(e.target.value)} required />
        </label>
      </div>
      <div className="settlement-edit-row">
        <label>
          Erkek
          <input type="number" min={0} value={male} onChange={(e) => setMale(e.target.value)} />
        </label>
        <label>
          Kadın
          <input type="number" min={0} value={female} onChange={(e) => setFemale(e.target.value)} />
        </label>
        <label>
          Çocuk
          <input type="number" min={0} value={child} onChange={(e) => setChild(e.target.value)} />
        </label>
      </div>
      <p className="muted small">Kırılım girilirse erkek + kadın + çocuk toplama eşit olmalı.</p>
      <button type="submit" className="btn-secondary" disabled={saving}>
        {saving ? 'Kaydediliyor…' : 'Nüfusu kaydet'}
      </button>
    </form>
  )
}

export function SchoolEditor({
  settlementId,
  school,
  onSaved,
  onCancel,
}: {
  settlementId: string
  school: SettlementSchool | null
  onSaved: () => Promise<void>
  onCancel: () => void
}) {
  const alert = useAlert()
  const [name, setName] = useState(school?.name ?? '')
  const [schoolType, setSchoolType] = useState(school?.schoolType ?? 'İlkokul')
  const [students, setStudents] = useState(school?.studentCount != null ? String(school.studentCount) : '')
  const [principal, setPrincipal] = useState(school?.principalName ?? '')
  const [phone, setPhone] = useState(school?.principalPhone ?? '')
  const [saving, setSaving] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) {
      await alert({ title: 'Okul', message: 'Okul adını yazın.', tone: 'warning' })
      return
    }
    setSaving(true)
    try {
      await upsertSettlementSchool(settlementId, {
        id: school?.id,
        name: name.trim(),
        schoolType,
        studentCount: numOrNull(students),
        principalName: principal.trim() || null,
        principalPhone: phone.trim() || null,
      })
      await onSaved()
    } catch (err) {
      await alert({
        title: 'Kaydedilemedi',
        message: err instanceof ApiClientError ? err.message : 'Okul kaydedilemedi.',
        tone: 'warning',
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="settlement-edit" onSubmit={(e) => void onSubmit(e)}>
      <h3>{school ? 'Okulu düzenle' : 'Okul ekle'}</h3>
      <label>
        Ad
        <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
      </label>
      <label>
        Tür
        <select value={schoolType} onChange={(e) => setSchoolType(e.target.value)}>
          {SCHOOL_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </label>
      <label>
        Öğrenci
        <input type="number" min={0} value={students} onChange={(e) => setStudents(e.target.value)} />
      </label>
      <label>
        Müdür
        <input value={principal} onChange={(e) => setPrincipal(e.target.value)} maxLength={120} />
      </label>
      <label>
        Telefon
        <input value={phone} onChange={(e) => setPhone(e.target.value)} maxLength={30} inputMode="tel" />
      </label>
      <div className="settlement-edit-actions">
        <button type="button" className="btn-secondary" onClick={onCancel}>
          Vazgeç
        </button>
        <button type="submit" className="btn-primary" disabled={saving}>
          {saving ? 'Kaydediliyor…' : 'Kaydet'}
        </button>
      </div>
    </form>
  )
}

export function AreaEditor({
  settlementId,
  area,
  onSaved,
  onCancel,
}: {
  settlementId: string
  area: SettlementArea | null
  onSaved: () => Promise<void>
  onCancel: () => void
}) {
  const alert = useAlert()
  const [name, setName] = useState(area?.name ?? '')
  const [areaType, setAreaType] = useState(area?.areaType ?? 'Park')
  const [note, setNote] = useState(area?.note ?? '')
  const [address, setAddress] = useState(area?.address ?? '')
  const [saving, setSaving] = useState(false)

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim()) {
      await alert({ title: 'Alan', message: 'Alan adını yazın.', tone: 'warning' })
      return
    }
    setSaving(true)
    try {
      await upsertSettlementArea(settlementId, {
        id: area?.id,
        name: name.trim(),
        areaType,
        note: note.trim() || null,
        address: address.trim() || null,
      })
      await onSaved()
    } catch (err) {
      await alert({
        title: 'Kaydedilemedi',
        message: err instanceof ApiClientError ? err.message : 'Alan kaydedilemedi.',
        tone: 'warning',
      })
    } finally {
      setSaving(false)
    }
  }

  return (
    <form className="settlement-edit" onSubmit={(e) => void onSubmit(e)}>
      <h3>{area ? 'Alanı düzenle' : 'Alan ekle'}</h3>
      <label>
        Ad
        <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
      </label>
      <label>
        Tür
        <select value={areaType} onChange={(e) => setAreaType(e.target.value)}>
          {AREA_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </label>
      <label>
        Not
        <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} />
      </label>
      <label>
        Adres
        <input value={address} onChange={(e) => setAddress(e.target.value)} maxLength={300} />
      </label>
      <div className="settlement-edit-actions">
        <button type="button" className="btn-secondary" onClick={onCancel}>
          Vazgeç
        </button>
        <button type="submit" className="btn-primary" disabled={saving}>
          {saving ? 'Kaydediliyor…' : 'Kaydet'}
        </button>
      </div>
    </form>
  )
}

export function useSettlementDelete() {
  const confirm = useConfirm()
  const alert = useAlert()
  return async function remove(kind: 'school' | 'area', settlementId: string, id: string, onSaved: () => Promise<void>) {
    const ok = await confirm({
      title: kind === 'school' ? 'Okulu sil' : 'Alanı sil',
      message: 'Kayıt listeden kalkar. Emin misiniz?',
      confirmLabel: 'Sil',
      tone: 'danger',
    })
    if (!ok) return
    try {
      if (kind === 'school') await deleteSettlementSchool(settlementId, id)
      else await deleteSettlementArea(settlementId, id)
      await onSaved()
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Silinemedi.',
        tone: 'warning',
      })
    }
  }
}
