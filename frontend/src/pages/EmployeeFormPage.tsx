import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  createEmployee,
  deleteEmployeePhoto,
  fetchEmployeeById,
  fetchEmployeeForEdit,
  fetchEmployeeFormOptions,
  updateEmployee,
  uploadEmployeePhoto,
  type EmployeeFormOptions,
  type EmployeeStatus,
  type EmployeeWritePayload,
} from '@/api/employeesApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { EmployeeAvatar } from '@/components/EmployeeAvatar'
import { useAlert } from '@/components/ConfirmDialog'
import {
  ORG_TYPE,
  collectDescendantIds,
  resolveOrgCascade,
} from '@/lib/orgUnitHelpers'

type FormState = {
  firstName: string
  lastName: string
  employeeNumber: string
  birthDate: string
  gender: number
  status: EmployeeStatus
  personalPhone: string
  corporatePhone: string
  personalEmail: string
  corporateEmail: string
  address: string
  emergencyContactName: string
  emergencyContactPhone: string
  nationalId: string
  unitId: string
  facilityId: string
  employmentTypeId: string
  jobTitleId: string
  managerEmployeeId: string
  primaryJobDutyId: string
  hireDate: string
  directorateStartDate: string
  unitStartDate: string
  dutyStartDate: string
}

type SectionId = 'identity' | 'contact' | 'corporate' | 'dates'

const SECTIONS: { id: SectionId; label: string }[] = [
  { id: 'identity', label: 'Kimlik' },
  { id: 'contact', label: 'İletişim' },
  { id: 'corporate', label: 'Kurumsal' },
  { id: 'dates', label: 'Tarihler' },
]

const emptyForm = (): FormState => ({
  firstName: '',
  lastName: '',
  employeeNumber: '',
  birthDate: '',
  gender: 0,
  status: 1,
  personalPhone: '',
  corporatePhone: '',
  personalEmail: '',
  corporateEmail: '',
  address: '',
  emergencyContactName: '',
  emergencyContactPhone: '',
  nationalId: '',
  unitId: '',
  facilityId: '',
  employmentTypeId: '',
  jobTitleId: '',
  managerEmployeeId: '',
  primaryJobDutyId: '',
  hireDate: '',
  directorateStartDate: '',
  unitStartDate: '',
  dutyStartDate: '',
})

function emptyToNull(value: string): string | null {
  const t = value.trim()
  return t === '' ? null : t
}

function guidOrNull(value: string): string | null {
  return value ? value : null
}

function validateTckn(value: string): string | null {
  const digits = value.replace(/\D/g, '')
  if (!digits) return null
  if (!/^\d{11}$/.test(digits)) return 'T.C. kimlik numarası 11 haneli olmalıdır.'
  if (digits[0] === '0') return 'T.C. kimlik numarası 0 ile başlayamaz.'
  // Tüm haneleri aynı olan numaralar NVI tarafından kabul edilmez
  if (/^(\d)\1{10}$/.test(digits)) return 'T.C. kimlik numarası geçersiz.'
  const d = digits.split('').map(Number)
  const odd = d[0] + d[2] + d[4] + d[6] + d[8]
  const even = d[1] + d[3] + d[5] + d[7]
  const dig10 = ((odd * 7 - even) % 10 + 10) % 10
  if (dig10 !== d[9]) {
    return 'T.C. kimlik numarası hatalı (kontrol hanesi uyuşmuyor). Numarayı kontrol edin.'
  }
  const dig11 = d.slice(0, 10).reduce((a, b) => a + b, 0) % 10
  if (dig11 !== d[10]) {
    return 'T.C. kimlik numarası hatalı (kontrol hanesi uyuşmuyor). Numarayı kontrol edin.'
  }
  return null
}

function estimateCompletion(form: FormState, canEditNationalId: boolean, canEditPhone: boolean): number {
  const checks: boolean[] = [
    Boolean(form.firstName.trim() && form.lastName.trim()),
    Boolean(form.employeeNumber.trim()),
    Boolean(form.birthDate),
    form.gender !== 0,
    canEditPhone
      ? Boolean(form.personalPhone.trim() || form.corporatePhone.trim())
      : Boolean(form.personalEmail.trim() || form.corporateEmail.trim()),
    Boolean(form.personalEmail.trim() || form.corporateEmail.trim()),
    Boolean(form.unitId),
    Boolean(form.employmentTypeId),
    Boolean(form.jobTitleId),
    Boolean(form.hireDate),
  ]
  if (canEditNationalId) checks.push(Boolean(form.nationalId.trim()))
  const ok = checks.filter(Boolean).length
  return Math.round((100 * ok) / checks.length)
}

/**
 * Create ve Edit aynı formu paylaşır — detay sayfası ile uyumlu kurumsal düzen.
 */
export function EmployeeFormPage() {
  const { id } = useParams<{ id: string }>()
  const isEdit = Boolean(id)
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const alert = useAlert()

  const canCreate = hasPermission(PermissionCodes.EmployeesCreate)
  const canUpdate = hasPermission(PermissionCodes.EmployeesUpdate)
  const canPickPhoto =
    hasPermission(PermissionCodes.FilesUpload) && (isEdit ? canUpdate : canCreate)
  const canUploadPhoto = canPickPhoto && (isEdit ? canUpdate : true)

  const [options, setOptions] = useState<EmployeeFormOptions | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [hasPhoto, setHasPhoto] = useState(false)
  const [photoKey, setPhotoKey] = useState(0)
  const [pendingPhoto, setPendingPhoto] = useState<File | null>(null)
  const [photoBusy, setPhotoBusy] = useState(false)
  const [photoError, setPhotoError] = useState<string | null>(null)

  const [canEditPhone, setCanEditPhone] = useState(true)
  const [canEditAddress, setCanEditAddress] = useState(true)
  const [canEditNationalId, setCanEditNationalId] = useState(true)
  const [initialNationalId, setInitialNationalId] = useState('')
  const [canChangeStatus, setCanChangeStatus] = useState(() =>
    hasPermission(PermissionCodes.EmployeesSetStatus),
  )

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [activeSection, setActiveSection] = useState<SectionId>('identity')

  const sectionRefs = useRef<Record<SectionId, HTMLElement | null>>({
    identity: null,
    contact: null,
    corporate: null,
    dates: null,
  })

  const denied = (isEdit && !canUpdate) || (!isEdit && !canCreate)

  useEffect(() => {
    if (denied) {
      setLoading(false)
      return
    }

    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const opts = await fetchEmployeeFormOptions()
        if (cancelled) return
        setOptions(opts)

        if (isEdit && id) {
          const edit = await fetchEmployeeForEdit(id)
          if (cancelled) return
          setCanEditPhone(edit.canEditPhone)
          setCanEditAddress(edit.canEditAddress)
          setCanEditNationalId(edit.canEditNationalId)
          setInitialNationalId(edit.nationalId ?? '')
          setCanChangeStatus(edit.canChangeStatus)
          setForm({
            firstName: edit.firstName,
            lastName: edit.lastName,
            employeeNumber: edit.employeeNumber ?? '',
            birthDate: edit.birthDate ?? '',
            gender: edit.gender,
            status: edit.status,
            personalPhone: edit.personalPhone ?? '',
            corporatePhone: edit.corporatePhone ?? '',
            personalEmail: edit.personalEmail ?? '',
            corporateEmail: edit.corporateEmail ?? '',
            address: edit.address ?? '',
            emergencyContactName: edit.emergencyContactName ?? '',
            emergencyContactPhone: edit.emergencyContactPhone ?? '',
            nationalId: edit.nationalId ?? '',
            unitId: edit.unitId ?? '',
            facilityId: edit.facilityId ?? '',
            employmentTypeId: edit.employmentTypeId ?? '',
            jobTitleId: edit.jobTitleId ?? '',
            managerEmployeeId: edit.managerEmployeeId ?? '',
            primaryJobDutyId: edit.primaryJobDutyId ?? '',
            hireDate: edit.hireDate ?? '',
            directorateStartDate: edit.directorateStartDate ?? '',
            unitStartDate: edit.unitStartDate ?? '',
            dutyStartDate: edit.dutyStartDate ?? '',
          })
          try {
            const detail = await fetchEmployeeById(id)
            if (!cancelled) {
              setHasPhoto(Boolean(detail.hasPhoto))
              setPhotoKey((k) => k + 1)
            }
          } catch {
            if (!cancelled) setHasPhoto(false)
          }
        } else {
          setCanEditPhone(hasPermission(PermissionCodes.EmployeesViewPhone))
          setCanEditAddress(hasPermission(PermissionCodes.EmployeesViewAddress))
          setCanEditNationalId(hasPermission(PermissionCodes.EmployeesViewNationalId))
          setInitialNationalId('')
          setCanChangeStatus(hasPermission(PermissionCodes.EmployeesSetStatus))
          setHasPhoto(false)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiClientError ? err.message : 'Form verileri yüklenemedi.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [denied, hasPermission, id, isEdit])

  const displayName = useMemo(() => {
    const name = `${form.firstName.trim()} ${form.lastName.trim()}`.trim()
    return name || (isEdit ? 'Personel' : 'Yeni personel')
  }, [form.firstName, form.lastName, isEdit])

  const completion = useMemo(
    () => estimateCompletion(form, canEditNationalId, canEditPhone),
    [form, canEditNationalId, canEditPhone],
  )

  const orgCascade = useMemo(
    () => resolveOrgCascade(options?.units ?? [], form.unitId),
    [options?.units, form.unitId],
  )

  const directorateOptions = useMemo(
    () => (options?.units ?? []).filter((u) => u.type === ORG_TYPE.Directorate),
    [options?.units],
  )

  const mainUnitOptions = useMemo(() => {
    const units = options?.units ?? []
    if (!orgCascade.directorateId) return units.filter((u) => u.type === ORG_TYPE.MainUnit)
    return units.filter(
      (u) => u.type === ORG_TYPE.MainUnit && u.parentId === orgCascade.directorateId,
    )
  }, [options?.units, orgCascade.directorateId])

  const subUnitOptions = useMemo(() => {
    const units = options?.units ?? []
    if (!orgCascade.mainUnitId) {
      if (!orgCascade.directorateId) return units.filter((u) => u.type === ORG_TYPE.SubUnit)
      const mainIds = new Set(mainUnitOptions.map((u) => u.id))
      return units.filter((u) => u.type === ORG_TYPE.SubUnit && u.parentId && mainIds.has(u.parentId))
    }
    return units.filter((u) => u.type === ORG_TYPE.SubUnit && u.parentId === orgCascade.mainUnitId)
  }, [options?.units, orgCascade.directorateId, orgCascade.mainUnitId, mainUnitOptions])

  const facilityOptions = useMemo(() => {
    const facilities = options?.facilities ?? []
    if (!form.unitId || !options?.units) {
      return { inScope: facilities, other: [] as typeof facilities }
    }
    const scope = collectDescendantIds(options.units, form.unitId)
    const inScope: typeof facilities = []
    const other: typeof facilities = []
    for (const f of facilities) {
      if (f.parentId && scope.has(f.parentId)) inScope.push(f)
      else other.push(f)
    }
    return { inScope, other }
  }, [options?.facilities, options?.units, form.unitId])

  const managerOptions = useMemo(() => {
    const list = options?.managers ?? []
    if (isEdit && id) return list.filter((m) => m.id !== id)
    return list
  }, [options?.managers, isEdit, id])

  function setField<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm((prev) => ({ ...prev, [key]: value }))
    setFieldErrors((prev) => {
      if (!prev[key]) return prev
      const next = { ...prev }
      delete next[key]
      return next
    })
  }

  function setUnitFromCascade(nextUnitId: string) {
    // Tesis fiili çalışma yeri — birim değişince silinmez (birim dışı tesis olabilir)
    setForm((prev) => ({
      ...prev,
      unitId: nextUnitId,
    }))
    setFieldErrors((prev) => {
      const next = { ...prev }
      delete next.unitId
      return next
    })
  }

  function scrollToSection(section: SectionId) {
    setActiveSection(section)
    sectionRefs.current[section]?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  function clientValidate(): Record<string, string[]> {
    const errs: Record<string, string[]> = {}
    if (!form.firstName.trim()) errs.firstName = ['Ad zorunludur.']
    if (!form.lastName.trim()) errs.lastName = ['Soyad zorunludur.']

    if (form.personalEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.personalEmail.trim())) {
      errs.personalEmail = ['Geçerli bir e-posta girin.']
    }
    if (form.corporateEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.corporateEmail.trim())) {
      errs.corporateEmail = ['Geçerli bir e-posta girin.']
    }

    if (canEditNationalId && form.nationalId.trim()) {
      const nationalIdChanged =
        !isEdit || form.nationalId.replace(/\D/g, '') !== initialNationalId.replace(/\D/g, '')
      // Düzenlemede değişmeyen (eski/seed) değer kaydı engellemesin
      if (nationalIdChanged) {
        const tcknErr = validateTckn(form.nationalId)
        if (tcknErr) errs.nationalId = [tcknErr]
      }
    }

    if (form.birthDate) {
      const birth = new Date(`${form.birthDate}T00:00:00`)
      const today = new Date()
      today.setHours(0, 0, 0, 0)
      if (birth >= today) errs.birthDate = ['Doğum tarihi bugünden önce olmalıdır.']
    }

    if (form.hireDate) {
      const hire = new Date(`${form.hireDate}T00:00:00`)
      const limit = new Date()
      limit.setHours(0, 0, 0, 0)
      limit.setDate(limit.getDate() + 30)
      if (hire > limit) errs.hireDate = ['İşe giriş tarihi en fazla 30 gün ileri olabilir.']
    }

    return errs
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    const localErrs = clientValidate()
    if (Object.keys(localErrs).length > 0) {
      setFieldErrors(localErrs)
      setError('Lütfen işaretli alanları kontrol edin.')
      const firstKey = Object.keys(localErrs)[0]
      const section =
        firstKey === 'personalEmail' ||
        firstKey === 'corporateEmail' ||
        firstKey === 'address' ||
        firstKey === 'personalPhone' ||
        firstKey === 'corporatePhone' ||
        firstKey === 'emergencyContactName' ||
        firstKey === 'emergencyContactPhone'
          ? 'contact'
          : firstKey === 'hireDate' ||
              firstKey === 'directorateStartDate' ||
              firstKey === 'unitStartDate' ||
              firstKey === 'dutyStartDate'
            ? 'dates'
            : firstKey === 'unitId' ||
                firstKey === 'facilityId' ||
                firstKey === 'employmentTypeId' ||
                firstKey === 'jobTitleId' ||
                firstKey === 'managerEmployeeId'
              ? 'corporate'
              : 'identity'
      scrollToSection(section)
      return
    }

    setSaving(true)
    setError(null)
    setFieldErrors({})

    const statusValue: EmployeeStatus =
      !canChangeStatus && !isEdit ? 1 : form.status

    const nationalIdDigits = form.nationalId.replace(/\D/g, '')
    const initialDigits = initialNationalId.replace(/\D/g, '')
    const nationalIdChanged = !isEdit || nationalIdDigits !== initialDigits

    const payload: EmployeeWritePayload = {
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      employeeNumber: emptyToNull(form.employeeNumber),
      birthDate: emptyToNull(form.birthDate),
      gender: form.gender,
      status: statusValue,
      personalPhone: emptyToNull(form.personalPhone),
      corporatePhone: emptyToNull(form.corporatePhone),
      personalEmail: emptyToNull(form.personalEmail),
      corporateEmail: emptyToNull(form.corporateEmail),
      address: emptyToNull(form.address),
      emergencyContactName: emptyToNull(form.emergencyContactName),
      emergencyContactPhone: emptyToNull(form.emergencyContactPhone),
      nationalId: emptyToNull(nationalIdDigits),
      // Yalnızca gerçekten değiştiyse gönder — eski hatalı kayıtlar diğer alanları kilitlemesin
      nationalIdProvided: canEditNationalId && nationalIdChanged,
      unitId: guidOrNull(form.unitId),
      facilityId: guidOrNull(form.facilityId),
      employmentTypeId: guidOrNull(form.employmentTypeId),
      jobTitleId: guidOrNull(form.jobTitleId),
      managerEmployeeId: guidOrNull(form.managerEmployeeId),
      primaryJobDutyId: guidOrNull(form.primaryJobDutyId),
      hireDate: emptyToNull(form.hireDate),
      directorateStartDate: emptyToNull(form.directorateStartDate),
      unitStartDate: emptyToNull(form.unitStartDate),
      dutyStartDate: emptyToNull(form.dutyStartDate),
    }

    try {
      if (isEdit && id) {
        await updateEmployee(id, payload)
        navigate(`/employees/${id}`)
      } else {
        const created = await createEmployee(payload)
        if (pendingPhoto && canPickPhoto) {
          try {
            await uploadEmployeePhoto(created.id, pendingPhoto)
          } catch {
            // Kayıt oluştu; fotoğraf detaydan yüklenebilir
          }
        }
        navigate(`/employees/${created.id}`)
      }
    } catch (err) {
      if (err instanceof ApiClientError) {
        const isDuplicateNationalId =
          err.status === 409 &&
          /T\.C\. kimlik|TCKN|kimlik numarası başka/i.test(err.message)

        if (isDuplicateNationalId) {
          setFieldErrors({ nationalId: [err.message] })
          setError(null)
          scrollToSection('identity')
          await alert({
            title: 'T.C. kimlik numarası kullanımda',
            message: err.message,
            tone: 'warning',
            okLabel: 'Tamam',
          })
        } else {
          setError(err.message)
          if (err.validationErrors) setFieldErrors(err.validationErrors)
        }
      } else {
        setError('Kayıt kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onPhotoSelected(file: File | null) {
    if (!file) return
    setPhotoError(null)

    if (!isEdit || !id) {
      setPendingPhoto(file)
      setHasPhoto(true)
      setPhotoKey((k) => k + 1)
      return
    }

    if (!canUploadPhoto) return
    setPhotoBusy(true)
    try {
      await uploadEmployeePhoto(id, file)
      setHasPhoto(true)
      setPhotoKey((k) => k + 1)
    } catch (err) {
      setPhotoError(err instanceof ApiClientError ? err.message : 'Fotoğraf yüklenemedi.')
    } finally {
      setPhotoBusy(false)
    }
  }

  async function onPhotoRemove() {
    setPhotoError(null)
    if (!isEdit || !id) {
      setPendingPhoto(null)
      setHasPhoto(false)
      setPhotoKey((k) => k + 1)
      return
    }
    if (!canUploadPhoto) return
    setPhotoBusy(true)
    try {
      await deleteEmployeePhoto(id)
      setHasPhoto(false)
      setPhotoKey((k) => k + 1)
    } catch (err) {
      setPhotoError(err instanceof ApiClientError ? err.message : 'Fotoğraf silinemedi.')
    } finally {
      setPhotoBusy(false)
    }
  }

  const pendingPreviewUrl = useMemo(
    () => (pendingPhoto ? URL.createObjectURL(pendingPhoto) : null),
    [pendingPhoto],
  )

  useEffect(() => {
    return () => {
      if (pendingPreviewUrl) URL.revokeObjectURL(pendingPreviewUrl)
    }
  }, [pendingPreviewUrl])

  const backTo = isEdit && id ? `/employees/${id}` : '/employees'
  const statusLabel =
    options?.statuses.find((s) => s.value === form.status)?.label ?? 'Aktif'
  const jobTitleLabel =
    options?.jobTitles.find((t) => t.id === form.jobTitleId)?.name ?? 'Unvan seçilmedi'
  const unitLabel = options?.units.find((u) => u.id === form.unitId)?.name ?? 'Birim seçilmedi'

  if (denied) {
    return (
      <div className="panel">
        <p className="form-error">Bu işlem için yetkiniz yok.</p>
        <Link to="/employees" className="back-link">
          ← Personel listesine dön
        </Link>
      </div>
    )
  }

  if (loading) {
    return (
      <div className="panel emp-form-loading">
        <p className="muted">Form hazırlanıyor…</p>
      </div>
    )
  }

  if (!options) {
    return (
      <div className="panel">
        <p className="form-error">{error ?? 'Form seçenekleri alınamadı.'}</p>
        <Link to="/employees" className="back-link">
          ← Personel listesine dön
        </Link>
      </div>
    )
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name] ?? fieldErrors[name.charAt(0).toUpperCase() + name.slice(1)]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="employee-form-page">
      <header className="emp-form-hero panel">
        <div className="emp-form-hero-main">
          <div className="emp-form-photo">
            {pendingPreviewUrl ? (
              <img className="emp-avatar emp-avatar-lg emp-avatar-img" src={pendingPreviewUrl} alt="" />
            ) : isEdit && id ? (
              <EmployeeAvatar
                key={`${id}-${photoKey}-${hasPhoto ? '1' : '0'}`}
                employeeId={id}
                name={displayName}
                hasPhoto={hasPhoto}
                size="lg"
              />
            ) : (
              <span className="emp-avatar emp-avatar-lg emp-avatar-fallback" aria-hidden="true">
                {displayName
                  .split(/\s+/)
                  .filter(Boolean)
                  .slice(0, 2)
                  .map((p) => p[0])
                  .join('')
                  .toLocaleUpperCase('tr-TR') || '?'}
              </span>
            )}
            {canPickPhoto && (
              <div className="emp-form-photo-actions">
                <label className="btn-secondary link-btn emp-form-photo-upload">
                  {photoBusy ? 'İşleniyor…' : hasPhoto || pendingPhoto ? 'Değiştir' : 'Fotoğraf'}
                  <input
                    type="file"
                    accept="image/jpeg,image/png,.jpg,.jpeg,.png"
                    hidden
                    disabled={photoBusy}
                    onChange={(e) => {
                      const file = e.target.files?.[0] ?? null
                      e.target.value = ''
                      void onPhotoSelected(file)
                    }}
                  />
                </label>
                {(hasPhoto || pendingPhoto) && (
                  <button
                    type="button"
                    className="btn-secondary"
                    disabled={photoBusy}
                    onClick={() => void onPhotoRemove()}
                  >
                    Kaldır
                  </button>
                )}
              </div>
            )}
            {photoError && <p className="form-error emp-form-photo-error">{photoError}</p>}
          </div>

          <div className="emp-form-hero-copy">
            <Link to={backTo} className="back-link">
              ← {isEdit ? 'Personele dön' : 'Personeller'}
            </Link>
            <p className="emp-form-eyebrow">{isEdit ? 'Personel kaydı' : 'Yeni kayıt'}</p>
            <h1>{displayName}</h1>
            <p className="muted emp-form-lead">
              {form.employeeNumber.trim() || 'Sicil yok'} · {jobTitleLabel} · {unitLabel}
            </p>
            {!isEdit && (
              <p className="muted small emp-form-hint">
                Eğitim, yetkinlik, görev ataması ve notlar kayıt sonrası personel detayından eklenir.
              </p>
            )}
          </div>
        </div>

        <div className="emp-form-hero-meta">
          <span className={`status-pill status-${form.status}`}>{statusLabel}</span>
          <div className="completion" title="Form doluluk tahmini">
            <div className="completion-bar" style={{ width: `${completion}%` }} />
            <span>{completion}%</span>
          </div>
        </div>
      </header>

      <div className="emp-form-layout">
        <nav className="emp-form-nav panel" aria-label="Form bölümleri">
          {SECTIONS.map((s) => (
            <button
              key={s.id}
              type="button"
              className={activeSection === s.id ? 'is-active' : ''}
              onClick={() => scrollToSection(s.id)}
            >
              {s.label}
            </button>
          ))}
        </nav>

        <form className="emp-form-body panel" onSubmit={onSubmit} noValidate>
          {error && <div className="form-error emp-form-banner">{error}</div>}

          <section
            className="emp-form-section"
            id="form-identity"
            ref={(el) => {
              sectionRefs.current.identity = el
            }}
          >
            <div className="emp-form-section-head">
              <h2>Kimlik</h2>
              <p className="muted small">Temel kimlik ve durum bilgileri</p>
            </div>
            <div className="form-grid">
              <label>
                Ad <span className="req">*</span>
                <input
                  value={form.firstName}
                  onChange={(e) => setField('firstName', e.target.value)}
                  autoComplete="given-name"
                  aria-invalid={Boolean(fieldErrors.firstName)}
                />
                {fieldError('firstName')}
              </label>
              <label>
                Soyad <span className="req">*</span>
                <input
                  value={form.lastName}
                  onChange={(e) => setField('lastName', e.target.value)}
                  autoComplete="family-name"
                  aria-invalid={Boolean(fieldErrors.lastName)}
                />
                {fieldError('lastName')}
              </label>
              <label>
                Sicil no
                <input
                  value={form.employeeNumber}
                  onChange={(e) => setField('employeeNumber', e.target.value)}
                />
                {fieldError('employeeNumber')}
              </label>
              <label>
                Doğum tarihi
                <input
                  type="date"
                  value={form.birthDate}
                  onChange={(e) => setField('birthDate', e.target.value)}
                />
                {fieldError('birthDate')}
              </label>
              <label>
                Cinsiyet
                <select
                  value={form.gender}
                  onChange={(e) => setField('gender', Number(e.target.value))}
                >
                  {options.genders.map((g) => (
                    <option key={g.value} value={g.value}>
                      {g.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Durum
                <select
                  value={form.status}
                  disabled={!canChangeStatus}
                  onChange={(e) => setField('status', Number(e.target.value) as EmployeeStatus)}
                >
                  {options.statuses.map((s) => (
                    <option key={s.value} value={s.value}>
                      {s.label}
                    </option>
                  ))}
                </select>
                {!canChangeStatus && (
                  <span className="field-hint">
                    {isEdit
                      ? 'Durum değiştirme yetkiniz yok.'
                      : 'Yeni kayıt Aktif olarak açılır (durum yetkisi yok).'}
                  </span>
                )}
              </label>
              {canEditNationalId && (
                <label>
                  T.C. kimlik no
                  <input
                    value={form.nationalId}
                    onChange={(e) =>
                      setField('nationalId', e.target.value.replace(/\D/g, '').slice(0, 11))
                    }
                    inputMode="numeric"
                    maxLength={11}
                  placeholder="Örn. 10000000146"
                  autoComplete="off"
                />
                <small className="muted">11 haneli; rastgele sayı kabul edilmez, kontrol hanesi doğru olmalı.</small>
                  {fieldError('nationalId')}
                </label>
              )}
            </div>
          </section>

          <section
            className="emp-form-section"
            id="form-contact"
            ref={(el) => {
              sectionRefs.current.contact = el
            }}
          >
            <div className="emp-form-section-head">
              <h2>İletişim</h2>
              <p className="muted small">Telefon, e-posta ve acil iletişim</p>
            </div>
            <div className="form-grid">
              {canEditPhone && (
                <>
                  <label>
                    Kişisel telefon
                    <input
                      value={form.personalPhone}
                      onChange={(e) => setField('personalPhone', e.target.value)}
                      inputMode="tel"
                    />
                    {fieldError('personalPhone')}
                  </label>
                  <label>
                    Kurumsal telefon
                    <input
                      value={form.corporatePhone}
                      onChange={(e) => setField('corporatePhone', e.target.value)}
                      inputMode="tel"
                    />
                    {fieldError('corporatePhone')}
                  </label>
                </>
              )}
              <label>
                Kişisel e-posta
                <input
                  type="email"
                  value={form.personalEmail}
                  onChange={(e) => setField('personalEmail', e.target.value)}
                  autoComplete="email"
                />
                {fieldError('personalEmail')}
              </label>
              <label>
                Kurumsal e-posta
                <input
                  type="email"
                  value={form.corporateEmail}
                  onChange={(e) => setField('corporateEmail', e.target.value)}
                />
                {fieldError('corporateEmail')}
              </label>
              {canEditAddress && (
                <label className="span-2">
                  Adres
                  <textarea
                    rows={3}
                    value={form.address}
                    onChange={(e) => setField('address', e.target.value)}
                  />
                  {fieldError('address')}
                </label>
              )}
              <label>
                Acil iletişim adı
                <input
                  value={form.emergencyContactName}
                  onChange={(e) => setField('emergencyContactName', e.target.value)}
                />
              </label>
              <label>
                Acil iletişim telefon
                <input
                  value={form.emergencyContactPhone}
                  onChange={(e) => setField('emergencyContactPhone', e.target.value)}
                  inputMode="tel"
                />
              </label>
            </div>
          </section>

          <section
            className="emp-form-section"
            id="form-corporate"
            ref={(el) => {
              sectionRefs.current.corporate = el
            }}
          >
            <div className="emp-form-section-head">
              <h2>Kurumsal</h2>
              <p className="muted small">Organizasyon, unvan ve yönetici — liste filtreleriyle aynı hiyerarşi</p>
            </div>
            <div className="form-grid">
              <label>
                Müdürlük
                <select
                  value={orgCascade.directorateId}
                  onChange={(e) => setUnitFromCascade(e.target.value)}
                >
                  <option value="">Seçiniz</option>
                  {directorateOptions.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Ana birim
                <select
                  value={orgCascade.mainUnitId}
                  onChange={(e) =>
                    setUnitFromCascade(e.target.value || orgCascade.directorateId)
                  }
                >
                  <option value="">Seçiniz</option>
                  {mainUnitOptions.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Alt birim
                <select
                  value={orgCascade.subUnitId}
                  onChange={(e) =>
                    setUnitFromCascade(
                      e.target.value || orgCascade.mainUnitId || orgCascade.directorateId,
                    )
                  }
                >
                  <option value="">Seçiniz</option>
                  {subUnitOptions.map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
                {fieldError('unitId')}
              </label>
              <label>
                Tesis
                <select
                  value={form.facilityId}
                  onChange={(e) => setField('facilityId', e.target.value)}
                >
                  <option value="">Seçiniz</option>
                  {facilityOptions.other.length === 0 ? (
                    facilityOptions.inScope.map((f) => (
                      <option key={f.id} value={f.id}>
                        {f.name}
                      </option>
                    ))
                  ) : (
                    <>
                      {facilityOptions.inScope.length > 0 ? (
                        <optgroup label="Bu birime bağlı tesisler">
                          {facilityOptions.inScope.map((f) => (
                            <option key={f.id} value={f.id}>
                              {f.name}
                            </option>
                          ))}
                        </optgroup>
                      ) : null}
                      <optgroup label="Diğer tesisler">
                        {facilityOptions.other.map((f) => (
                          <option key={f.id} value={f.id}>
                            {f.name}
                          </option>
                        ))}
                      </optgroup>
                    </>
                  )}
                </select>
                <small className="muted">
                  Organizasyon birimi ile fiili çalışma tesisi farklı olabilir.
                </small>
                {fieldError('facilityId')}
              </label>
              <label>
                İstihdam türü
                <select
                  value={form.employmentTypeId}
                  onChange={(e) => setField('employmentTypeId', e.target.value)}
                >
                  <option value="">Seçiniz</option>
                  {options.employmentTypes.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
                {fieldError('employmentTypeId')}
              </label>
              <label>
                Resmi unvan
                <select
                  value={form.jobTitleId}
                  onChange={(e) => setField('jobTitleId', e.target.value)}
                >
                  <option value="">Seçiniz</option>
                  {options.jobTitles.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
                {fieldError('jobTitleId')}
              </label>
              <label>
                Fiili görev
                <select
                  value={form.primaryJobDutyId}
                  onChange={(e) => setField('primaryJobDutyId', e.target.value)}
                >
                  <option value="">Seçiniz</option>
                  {options.duties.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
                {fieldError('primaryJobDutyId')}
              </label>
              <label className="span-2">
                Bağlı yönetici
                <select
                  value={form.managerEmployeeId}
                  onChange={(e) => setField('managerEmployeeId', e.target.value)}
                >
                  <option value="">Seçiniz</option>
                  {managerOptions.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.name}
                      {m.code ? ` · ${m.code}` : ''}
                    </option>
                  ))}
                </select>
                {fieldError('managerEmployeeId')}
              </label>
            </div>
          </section>

          <section
            className="emp-form-section"
            id="form-dates"
            ref={(el) => {
              sectionRefs.current.dates = el
            }}
          >
            <div className="emp-form-section-head">
              <h2>Tarihler</h2>
              <p className="muted small">İşe giriş ve birim / görev başlangıçları</p>
            </div>
            <div className="form-grid">
              <label>
                İşe giriş
                <input
                  type="date"
                  value={form.hireDate}
                  onChange={(e) => setField('hireDate', e.target.value)}
                />
                {fieldError('hireDate')}
              </label>
              <label>
                Müdürlük başlangıç
                <input
                  type="date"
                  value={form.directorateStartDate}
                  onChange={(e) => setField('directorateStartDate', e.target.value)}
                />
              </label>
              <label>
                Birim başlangıç
                <input
                  type="date"
                  value={form.unitStartDate}
                  onChange={(e) => setField('unitStartDate', e.target.value)}
                />
              </label>
              <label>
                Görev başlangıç
                <input
                  type="date"
                  value={form.dutyStartDate}
                  onChange={(e) => setField('dutyStartDate', e.target.value)}
                />
              </label>
            </div>
          </section>

          <div className="emp-form-actions">
            <button type="submit" className="btn-primary" disabled={saving}>
              {saving ? 'Kaydediliyor…' : isEdit ? 'Değişiklikleri kaydet' : 'Personeli oluştur'}
            </button>
            <Link to={backTo} className="btn-secondary link-btn">
              İptal
            </Link>
          </div>
        </form>
      </div>
    </div>
  )
}
