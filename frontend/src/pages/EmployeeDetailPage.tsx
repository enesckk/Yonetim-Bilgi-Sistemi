import { useCallback, useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import {
  createAssignment,
  createCertificate,
  createEducation,
  createEmployeeSkill,
  createMovement,
  createNote,
  createSpecialCondition,
  deleteAssignment,
  deleteCertificate,
  deleteEducation,
  deleteEmployeePhoto,
  deleteEmployeeSkill,
  deleteMovement,
  deleteNote,
  deleteSpecialCondition,
  downloadSpecialConditionDocument,
  fetchAssignmentFormOptions,
  fetchCertificateFormOptions,
  fetchEducationFormOptions,
  fetchEmployeeById,
  fetchEmployeeFormOptions,
  fetchMovementFormOptions,
  fetchNoteFormOptions,
  fetchSkillFormOptions,
  fetchSpecialConditionFormOptions,
  removeSpecialConditionDocument,
  setEmployeeStatus,
  updateAssignment,
  updateCertificate,
  updateEducation,
  updateEmployeeSkill,
  updateMovement,
  updateNote,
  updateSpecialCondition,
  uploadEmployeePhoto,
  uploadSpecialConditionDocument,
  type AssignmentFormOptions,
  type CertificateFormOptions,
  type CreateMovementPayload,
  type EducationFormOptions,
  type EmployeeDetail,
  type EmployeeStatus,
  type EnumOption,
  type MovementFormOptions,
  type NoteFormOptions,
  type SkillFormOptions,
  type SpecialConditionFormOptions,
  type UpsertAssignmentPayload,
  type UpsertCertificatePayload,
  type UpsertEducationPayload,
  type UpsertEmployeeSkillPayload,
  type UpsertNotePayload,
  type UpsertSpecialConditionPayload,
} from '@/api/employeesApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { isSystemAdmin } from '@/auth/roles'
import { useConfirm } from '@/components/ConfirmDialog'
import { EmployeeAvatar } from '@/components/EmployeeAvatar'
import {
  DeleteEmployeeDialog,
  TransferDirectorateDialog,
  WorkplaceChangeDialog,
} from '@/components/EmployeeLifecycleDialogs'
import { PageBackLink } from '@/components/PageBackLink'

type TabId = 'general' | 'corporate' | 'education' | 'skills' | 'history' | 'notes' | 'special'

const TABS: { id: TabId; label: string }[] = [
  { id: 'general', label: 'Genel' },
  { id: 'corporate', label: 'Kurumsal' },
  { id: 'education', label: 'Eğitim' },
  { id: 'skills', label: 'Yetkinlikler' },
  { id: 'history', label: 'Görev geçmişi' },
  { id: 'notes', label: 'Notlar' },
  { id: 'special', label: 'Özel durum' },
]

export function EmployeeDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const { hasPermission, user } = useAuth()
  const admin = isSystemAdmin(user)
  const [tab, setTab] = useState<TabId>('general')
  const [data, setData] = useState<EmployeeDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [photoBusy, setPhotoBusy] = useState(false)
  const [photoError, setPhotoError] = useState<string | null>(null)
  const [photoKey, setPhotoKey] = useState(0)
  const [statusOpen, setStatusOpen] = useState(false)
  const [archiveOpen, setArchiveOpen] = useState(false)
  const [workplaceOpen, setWorkplaceOpen] = useState(false)
  const [transferOpen, setTransferOpen] = useState(false)

  const load = useCallback(async () => {
    if (!id) return
    setLoading(true)
    setError(null)
    try {
      const result = await fetchEmployeeById(id)
      setData(result)
    } catch (err) {
      setData(null)
      setError(err instanceof ApiClientError ? err.message : 'Personel yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    const action = searchParams.get('action')
    if (!action) return
    if (action === 'workplace') setWorkplaceOpen(true)
    if (action === 'transfer') setTransferOpen(true)
    if (action === 'delete') setArchiveOpen(true)
    const next = new URLSearchParams(searchParams)
    next.delete('action')
    setSearchParams(next, { replace: true })
  }, [searchParams, setSearchParams])

  if (loading && !data) {
    return (
      <div className="emp-detail-page">
        <div className="panel emp-detail-loading">
          <p className="muted">Profil yükleniyor…</p>
        </div>
      </div>
    )
  }

  if (error || !data) {
    return (
      <div className="emp-detail-page">
        <div className="panel">
          <p className="form-error">{error ?? 'Kayıt bulunamadı.'}</p>
          <PageBackLink to="/employees">Personel listesine dön</PageBackLink>
        </div>
      </div>
    )
  }

  const canViewSpecial = hasPermission(PermissionCodes.EmployeesViewSpecialConditions)
  const canManageSpecial = hasPermission(PermissionCodes.EmployeesManageSpecialConditions)
  const canUploadFiles = hasPermission(PermissionCodes.FilesUpload)
  const canViewFiles = hasPermission(PermissionCodes.FilesView)
  const canUpdate = hasPermission(PermissionCodes.EmployeesUpdate)
  const canManageEducation = hasPermission(PermissionCodes.EducationManage)
  const canManageSkills = hasPermission(PermissionCodes.SkillsManage)
  const canManageCertificates = hasPermission(PermissionCodes.CertificatesManage)
  const canManageAssignments = hasPermission(PermissionCodes.AssignmentsManage)
  const canCreateMovements = hasPermission(PermissionCodes.MovementsCreate)
  const canCreateNotes = hasPermission(PermissionCodes.NotesCreate)
  const canSetStatus = hasPermission(PermissionCodes.EmployeesSetStatus)
  const canArchive = hasPermission(PermissionCodes.EmployeesArchive)
  const canUploadPhoto =
    hasPermission(PermissionCodes.EmployeesUpdate) && hasPermission(PermissionCodes.FilesUpload)

  const orgTrail = [
    data.corporate.directorateName,
    data.corporate.mainUnitName,
    data.corporate.subUnitName,
  ]
    .filter(Boolean)
    .join(' › ')

  const missingHints = buildMissingHints(data)

  async function onPhotoSelected(file: File | null) {
    if (!file || !data) return
    setPhotoBusy(true)
    setPhotoError(null)
    try {
      await uploadEmployeePhoto(data.id, file)
      const refreshed = await fetchEmployeeById(data.id)
      setData(refreshed)
      setPhotoKey((k) => k + 1)
    } catch (err) {
      setPhotoError(err instanceof ApiClientError ? err.message : 'Fotoğraf yüklenemedi.')
    } finally {
      setPhotoBusy(false)
    }
  }

  async function onPhotoRemove() {
    if (!data) return
    setPhotoBusy(true)
    setPhotoError(null)
    try {
      await deleteEmployeePhoto(data.id)
      const refreshed = await fetchEmployeeById(data.id)
      setData(refreshed)
      setPhotoKey((k) => k + 1)
    } catch (err) {
      setPhotoError(err instanceof ApiClientError ? err.message : 'Fotoğraf silinemedi.')
    } finally {
      setPhotoBusy(false)
    }
  }

  return (
    <div className="emp-detail-page">
      <header className="emp-detail-hero panel">
        <div className="emp-detail-hero-main">
          <div className="emp-detail-photo">
            <EmployeeAvatar
              key={`${data.id}-${photoKey}-${data.hasPhoto ? '1' : '0'}`}
              employeeId={data.id}
              name={data.fullName}
              hasPhoto={Boolean(data.hasPhoto)}
              size="lg"
            />
            {canUploadPhoto && (
              <div className="emp-detail-photo-actions">
                <label className="btn-secondary link-btn emp-detail-photo-upload">
                  {photoBusy ? 'İşleniyor…' : data.hasPhoto ? 'Değiştir' : 'Fotoğraf yükle'}
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
                {data.hasPhoto && (
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
            {photoError && <p className="form-error emp-detail-photo-error">{photoError}</p>}
          </div>

          <div className="emp-detail-hero-copy">
            <PageBackLink to="/employees">Personeller</PageBackLink>
            <p className="emp-detail-eyebrow">Personel profili</p>
            <h1>{data.fullName}</h1>
            <p className="emp-detail-lead">
              {[data.employeeNumber ? `Sicil ${data.employeeNumber}` : null, data.corporate.jobTitleName, data.corporate.primaryDutyName]
                .filter(Boolean)
                .join(' · ') || 'Kurumsal bilgi henüz girilmedi'}
            </p>
            {orgTrail && <p className="emp-detail-org">{orgTrail}</p>}
            {data.corporate.facilityName && (
              <p className="emp-detail-facility">Tesis: {data.corporate.facilityName}</p>
            )}
            <div className="emp-detail-hero-actions">
              {canUpdate && (
                <Link to={`/employees/${data.id}/edit`} className="btn-primary link-btn">
                  Düzenle
                </Link>
              )}
              {canCreateMovements && data.status === 1 && (
                <button type="button" className="btn-secondary" onClick={() => setWorkplaceOpen(true)}>
                  Görev yeri değiştir
                </button>
              )}
              {canSetStatus && data.status === 1 && (
                <button type="button" className="btn-secondary" onClick={() => setTransferOpen(true)}>
                  Başka müdürlüğe geçti
                </button>
              )}
              {canSetStatus && (
                <button type="button" className="btn-secondary" onClick={() => setStatusOpen(true)}>
                  Durum değiştir
                </button>
              )}
              {canArchive && (
                <button type="button" className="btn-secondary emp-archive-btn" onClick={() => setArchiveOpen(true)}>
                  Sil
                </button>
              )}
            </div>
          </div>
        </div>

        <aside className="emp-detail-hero-meta">
          <span className={`status-pill status-${data.status}`}>{data.statusLabel}</span>
          {admin ? (
          <div className="completion emp-detail-completion">
            <span className="muted small">Profil doluluğu</span>
            <div className="completion-track">
              <div className="completion-bar" style={{ width: `${data.profileCompletionPercent}%` }} />
            </div>
            <strong>{data.profileCompletionPercent}%</strong>
          </div>
          ) : null}
          {data.hasSpecialCondition && (
            <p className={`emp-detail-special-flag ${canViewSpecial ? 'is-open' : ''}`}>
              {canViewSpecial
                ? 'Özel durum kaydı mevcut'
                : 'Özel durum kaydı bulunuyor (detay yetkiye bağlı)'}
            </p>
          )}
        </aside>
      </header>

      {admin && missingHints.length > 0 && (
        <div className="emp-detail-gaps panel" role="status">
          <strong>Eksik / dikkat</strong>
          <ul>
            {missingHints.map((h) => (
              <li key={h}>{h}</li>
            ))}
          </ul>
        </div>
      )}

      <nav className="emp-detail-tabs" role="tablist" aria-label="Profil sekmeleri">
        {TABS.filter((t) => t.id !== 'special' || canViewSpecial).map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            className={tab === t.id ? 'is-active' : ''}
            aria-selected={tab === t.id}
            onClick={() => setTab(t.id)}
          >
            {t.label}
            {t.id === 'special' && data.hasSpecialCondition ? (
              <span className="emp-detail-tab-dot" aria-hidden />
            ) : null}
            {t.id === 'skills' && data.certificates.some((c) => isCertExpired(c.expiresOn)) ? (
              <span className="emp-detail-tab-warn" title="Süresi dolmuş sertifika var">
                !
              </span>
            ) : null}
          </button>
        ))}
      </nav>

      <section className="panel emp-detail-panel" role="tabpanel">
        {tab === 'general' && <GeneralTab data={data} />}
        {tab === 'corporate' && (
          <CorporateTab
            data={data}
            canManageAssignments={canManageAssignments}
            onChanged={async () => {
              const refreshed = await fetchEmployeeById(data.id)
              setData(refreshed)
            }}
          />
        )}
        {tab === 'education' && (
          <EducationTab
            data={data}
            canManage={canManageEducation}
            onChanged={async () => {
              const refreshed = await fetchEmployeeById(data.id)
              setData(refreshed)
            }}
          />
        )}
        {tab === 'skills' && (
          <SkillsTab
            data={data}
            canManageSkills={canManageSkills}
            canManageCertificates={canManageCertificates}
            onChanged={async () => {
              const refreshed = await fetchEmployeeById(data.id)
              setData(refreshed)
            }}
          />
        )}
        {tab === 'history' && (
          <HistoryTab
            data={data}
            canManage={canCreateMovements}
            onChanged={async () => {
              const refreshed = await fetchEmployeeById(data.id)
              setData(refreshed)
            }}
          />
        )}
        {tab === 'notes' && (
          <NotesTab
            data={data}
            canCreate={canCreateNotes}
            onChanged={async () => {
              const refreshed = await fetchEmployeeById(data.id)
              setData(refreshed)
            }}
          />
        )}
        {tab === 'special' && (
          <SpecialTab
            data={data}
            canViewSpecial={canViewSpecial}
            canManage={canManageSpecial}
            canUploadFiles={canUploadFiles}
            canViewFiles={canViewFiles}
            onChanged={async () => {
              const refreshed = await fetchEmployeeById(data.id)
              setData(refreshed)
            }}
          />
        )}
      </section>

      {statusOpen ? (
        <StatusChangeDialog
          employeeId={data.id}
          currentStatus={data.status}
          currentLabel={data.statusLabel}
          onClose={() => setStatusOpen(false)}
          onSaved={async () => {
            setStatusOpen(false)
            const refreshed = await fetchEmployeeById(data.id)
            setData(refreshed)
          }}
        />
      ) : null}

      {workplaceOpen ? (
        <WorkplaceChangeDialog
          employeeId={data.id}
          onClose={() => setWorkplaceOpen(false)}
          onSaved={async () => {
            setWorkplaceOpen(false)
            const refreshed = await fetchEmployeeById(data.id)
            setData(refreshed)
          }}
        />
      ) : null}

      {transferOpen ? (
        <TransferDirectorateDialog
          employeeId={data.id}
          fullName={data.fullName}
          onClose={() => setTransferOpen(false)}
          onSaved={async () => {
            setTransferOpen(false)
            const refreshed = await fetchEmployeeById(data.id)
            setData(refreshed)
          }}
        />
      ) : null}

      {archiveOpen ? (
        <DeleteEmployeeDialog
          employeeId={data.id}
          fullName={data.fullName}
          onClose={() => setArchiveOpen(false)}
          onDeleted={async () => {
            navigate('/employees', { replace: true })
          }}
        />
      ) : null}
    </div>
  )
}

function todayIso() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

function StatusChangeDialog({
  employeeId,
  currentStatus,
  currentLabel,
  onClose,
  onSaved,
}: {
  employeeId: string
  currentStatus: EmployeeStatus
  currentLabel: string
  onClose: () => void
  onSaved: () => Promise<void>
}) {
  const [statuses, setStatuses] = useState<EnumOption[]>([])
  const [status, setStatus] = useState<EmployeeStatus>(currentStatus)
  const [effectiveDate, setEffectiveDate] = useState(todayIso())
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void fetchEmployeeFormOptions()
      .then((o) => setStatuses(o.statuses ?? []))
      .catch(() =>
        setStatuses([
          { value: 1, label: 'Aktif' },
          { value: 2, label: 'Pasif' },
          { value: 3, label: 'İzinli' },
          { value: 6, label: 'İşten ayrıldı' },
          { value: 7, label: 'Emekli oldu' },
          { value: 8, label: 'Başka müdürlüğe geçti' },
          { value: 11, label: 'Görevden ayrıldı' },
        ]),
      )
  }, [])

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (status === currentStatus) {
      setError('Yeni durum mevcut durumdan farklı olmalıdır.')
      return
    }
    if (!reason.trim()) {
      setError('Gerekçe zorunludur.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await setEmployeeStatus(employeeId, {
        status,
        effectiveDate,
        reason: reason.trim(),
      })
      await onSaved()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Durum güncellenemedi.')
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
        aria-labelledby="status-dialog-title"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="emp-lifecycle-head">
          <div>
            <p className="org-eyebrow">Personel yaşam döngüsü</p>
            <h2 id="status-dialog-title">Durum değiştir</h2>
            <p className="muted small">
              Kayıt silinmez. Mevcut durum: <strong>{currentLabel}</strong>. Ayrılış
              durumlarında görev geçmişine otomatik kayıt eklenir. “Başka müdürlüğe geçti”
              seçilirse personel kadrodan düşer ve teşkilat şemasında görünmez.
            </p>
          </div>
          <button type="button" className="btn-secondary" onClick={onClose}>
            Kapat
          </button>
        </header>
        <form className="emp-lifecycle-form" onSubmit={(e) => void onSubmit(e)}>
          <label>
            <span>Yeni durum</span>
            <select
              value={status}
              onChange={(e) => setStatus(Number(e.target.value) as EmployeeStatus)}
              required
            >
              {statuses.map((s) => (
                <option key={s.value} value={s.value} disabled={s.value === currentStatus}>
                  {s.label}
                </option>
              ))}
            </select>
          </label>
          {status === 8 ? (
            <p className="muted small span-2">
              Bu durum personeli pasif nakil kadrosuna alır; şemada ve aktif listede görünmez.
            </p>
          ) : null}
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
              placeholder="Örn. emeklilik başvurusu onaylandı / başka müdürlüğe nakil…"
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
              {busy ? 'Kaydediliyor…' : 'Durumu kaydet'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

function Field({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="field">
      <dt>{label}</dt>
      <dd>{value?.trim() ? value : '—'}</dd>
    </div>
  )
}

function SectionHead({
  title,
  lead,
  action,
}: {
  title: string
  lead?: string
  action?: ReactNode
}) {
  return (
    <div className="emp-detail-section-head">
      <div>
        <h2>{title}</h2>
        {lead ? <p className="muted small">{lead}</p> : null}
      </div>
      {action}
    </div>
  )
}

function GeneralTab({ data }: { data: EmployeeDetail }) {
  const g = data.general
  return (
    <div className="emp-detail-section">
      <SectionHead
        title="Genel bilgiler"
        lead="Kimlik, iletişim ve acil durum — hassas alanlar yetkiye göre maskelenir"
      />
      <div className="emp-detail-blocks">
        <div>
          <h3 className="emp-detail-block-title">Kimlik</h3>
          <dl className="field-grid">
            <Field label="Ad" value={data.firstName} />
            <Field label="Soyad" value={data.lastName} />
            <Field label="Personel numarası" value={data.employeeNumber} />
            <Field label="Personel durumu" value={data.statusLabel} />
            <Field label="Doğum tarihi" value={formatDate(g.birthDate)} />
            <Field label="Cinsiyet" value={g.genderLabel} />
            <Field
              label="T.C. kimlik numarası"
              value={
                g.nationalIdDisplay
                  ? g.nationalIdIsMasked
                    ? `${g.nationalIdDisplay} (maskeli)`
                    : g.nationalIdDisplay
                  : null
              }
            />
          </dl>
        </div>
        <div>
          <h3 className="emp-detail-block-title">İletişim</h3>
          <dl className="field-grid">
            <Field label="Telefon" value={g.personalPhone} />
            <Field label="Kurumsal telefon" value={g.corporatePhone} />
            <Field label="E-posta" value={g.personalEmail} />
            <Field label="Kurumsal e-posta" value={g.corporateEmail} />
            <Field label="Adres" value={g.address} />
            <Field label="Acil durumda aranacak kişi" value={g.emergencyContactName} />
            <Field label="Acil durum telefonu" value={g.emergencyContactPhone} />
          </dl>
        </div>
      </div>
    </div>
  )
}

function CorporateTab({
  data,
  canManageAssignments,
  onChanged,
}: {
  data: EmployeeDetail
  canManageAssignments: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  const c = data.corporate
  type Assignment = EmployeeDetail['assignments'][number]
  type FormState = {
    jobDutyId: string
    isPrimary: boolean
    startDate: string
    endDate: string
    description: string
  }

  const emptyForm = (): FormState => ({
    jobDutyId: '',
    isPrimary: data.assignments.every((a) => !a.isPrimary || a.endDate),
    startDate: new Date().toISOString().slice(0, 10),
    endDate: '',
    description: '',
  })

  const [options, setOptions] = useState<AssignmentFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canManageAssignments) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchAssignmentFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* formda görünür */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManageAssignments])

  function openCreate() {
    setEditingId(null)
    setForm(emptyForm())
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: Assignment) {
    setEditingId(item.id)
    setForm({
      jobDutyId: item.jobDutyId,
      isPrimary: item.isPrimary,
      startDate: item.startDate,
      endDate: item.endDate ?? '',
      description: item.description ?? '',
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function buildPayload(): UpsertAssignmentPayload {
    return {
      jobDutyId: form.jobDutyId,
      isPrimary: form.isPrimary,
      startDate: form.startDate,
      endDate: form.endDate.trim() || null,
      description: form.description.trim() || null,
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})

    const payload = buildPayload()
    const end = payload.endDate
    const overlap = data.assignments.find((a) => {
      if (editingId && a.id === editingId) return false
      if (a.jobDutyId !== payload.jobDutyId) return false
      const aEnd = a.endDate ?? '9999-12-31'
      const bEnd = end ?? '9999-12-31'
      return payload.startDate <= aEnd && a.startDate <= bEnd
    })
    if (overlap) {
      setFieldErrors({
        jobDutyId: [
          `Bu görev için zaten ${overlap.isPrimary && !overlap.endDate ? 'aktif ana' : overlap.endDate ? 'örtüşen bir' : 'aktif ek'} görev kaydı var. Mevcut kaydı düzenleyin veya sonlandırın.`,
        ],
      })
      setError('Aynı görev iki kez eklenemez.')
      setSaving(false)
      return
    }

    try {
      if (editingId) {
        await updateAssignment(data.id, editingId, payload)
      } else {
        await createAssignment(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Görev ataması kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: Assignment) {
    if (
      !(await confirm({
        title: 'Atamayı sil',
        message: `“${item.dutyName}” atamasını silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteAssignment(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <>
      <div className="emp-detail-section">
        <SectionHead
          title="Kurumsal bilgiler"
          lead="Resmi unvan ile fiili görev ayrı tutulur; birim ve tesis bağımsızdır"
        />
        <div className="emp-detail-blocks">
          <div>
            <h3 className="emp-detail-block-title">Organizasyon</h3>
            <dl className="field-grid">
              <Field label="Bağlı olduğu müdürlük" value={c.directorateName} />
              <Field label="Ana birim" value={c.mainUnitName} />
              <Field label="Alt birim" value={c.subUnitName ?? c.unitName} />
              <Field label="Çalıştığı tesis" value={c.facilityName} />
              <Field label="Birim sorumlusu" value={c.unitSupervisorName} />
              <Field label="Bağlı olduğu yönetici" value={c.managerName} />
            </dl>
          </div>
          <div>
            <h3 className="emp-detail-block-title">Unvan ve görev</h3>
            <dl className="field-grid">
              <Field label="Resmi unvan" value={c.jobTitleName} />
              <Field label="Fiilen yaptığı görev" value={c.primaryDutyName} />
              <Field label="Görev kategorisi" value={c.primaryDutyCategoryLabel} />
              <Field label="İstihdam türü" value={c.employmentTypeName} />
              <Field label="Personel durumu" value={data.statusLabel} />
            </dl>
          </div>
          <div>
            <h3 className="emp-detail-block-title">Tarihler</h3>
            <dl className="field-grid">
              <Field label="Belediyede işe giriş" value={formatDate(c.hireDate)} />
              <Field label="Mevcut müdürlüğe başlangıç" value={formatDate(c.directorateStartDate)} />
              <Field label="Mevcut birime başlangıç" value={formatDate(c.unitStartDate)} />
              <Field label="Mevcut göreve başlangıç" value={formatDate(c.dutyStartDate)} />
            </dl>
          </div>
        </div>
      </div>

      <div className="nested-crud emp-detail-nested">
        <div className="nested-crud-head">
          <div>
            <h3 className="subheading" style={{ margin: 0 }}>
              Fiili görev atamaları
            </h3>
            <p className="muted small" style={{ margin: '0.35rem 0 0' }}>
              Resmi unvan ≠ fiili görev. Aynı anda bir aktif ana görev; diğerleri ek görev olabilir.
            </p>
          </div>
          {canManageAssignments && !showForm && (
            <button type="button" className="btn-primary" onClick={openCreate}>
              + Atama ekle
            </button>
          )}
        </div>

        {!canManageAssignments && (
          <p className="muted small">Atama yönetimi için yetkili kullanıcı gerekir.</p>
        )}

        {error && <div className="form-error">{error}</div>}

        {showForm && options && (
          <form className="nested-form" onSubmit={onSubmit}>
            <h4>{editingId ? 'Atamayı düzenle' : 'Yeni görev ataması'}</h4>
            <div className="form-grid">
              <label className="span-2">
                Fiili görev *
                <select
                  value={form.jobDutyId}
                  required
                  onChange={(e) => setForm((f) => ({ ...f, jobDutyId: e.target.value }))}
                >
                  <option value="">Seçiniz</option>
                  {options.duties.map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name} ({d.categoryLabel})
                    </option>
                  ))}
                </select>
                {fieldError('jobDutyId')}
              </label>
              <label>
                Başlangıç *
                <input
                  type="date"
                  required
                  value={form.startDate}
                  onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))}
                />
                {fieldError('startDate')}
              </label>
              <label>
                Bitiş (boş = devam)
                <input
                  type="date"
                  value={form.endDate}
                  onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))}
                />
                {fieldError('endDate')}
              </label>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={form.isPrimary}
                  onChange={(e) => setForm((f) => ({ ...f, isPrimary: e.target.checked }))}
                />
                Ana görev
              </label>
              <label className="span-2">
                Açıklama
                <textarea
                  rows={2}
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                />
              </label>
            </div>
            {form.isPrimary && !form.endDate && (
              <p className="field-hint">
                Ana görev olarak kaydedilince diğer aktif ana görevler otomatik “ek görev”e
                düşer. Aynı görev tanımı ikinci kez eklenemez.
              </p>
            )}
            {!form.isPrimary && (
              <p className="field-hint">
                Aynı görev için devam eden bir atama varsa yeniden eklenemez; mevcut kaydı
                düzenleyin.
              </p>
            )}
            <div className="form-actions">
              <button type="submit" disabled={saving}>
                {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
              </button>
              <button
                type="button"
                className="btn-secondary"
                onClick={() => {
                  setShowForm(false)
                  setEditingId(null)
                }}
              >
                İptal
              </button>
            </div>
          </form>
        )}

        {data.assignments.length === 0 && !showForm ? (
          <p className="muted">Atama yok.</p>
        ) : (
          <ul className="card-list">
            {data.assignments.map((a) => (
              <li key={a.id} className={a.isPrimary && !a.endDate ? 'is-primary-duty' : undefined}>
                <div className="card-list-row">
                  <div>
                    <div className="emp-detail-card-title">
                      <strong>{a.dutyName}</strong>
                      <span className={`emp-chip ${a.isPrimary ? 'emp-chip-primary' : ''}`}>
                        {a.isPrimary ? 'Ana görev' : 'Ek görev'}
                      </span>
                      {a.endDate ? <span className="emp-chip">Sona erdi</span> : null}
                    </div>
                    <span className="muted">
                      {a.categoryLabel} · {formatDate(a.startDate)}
                      {a.endDate ? ` – ${formatDate(a.endDate)}` : ' – devam'}
                    </span>
                    {a.description && <p className="muted small">{a.description}</p>}
                  </div>
                  {canManageAssignments && (
                    <div className="row-actions">
                      <button type="button" className="btn-secondary" onClick={() => openEdit(a)}>
                        Düzenle
                      </button>
                      <button type="button" className="btn-danger" onClick={() => void onDelete(a)}>
                        Sil
                      </button>
                    </div>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  )
}

function EducationTab({
  data,
  canManage,
  onChanged,
}: {
  data: EmployeeDetail
  canManage: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  type Edu = EmployeeDetail['education'][number]
  type FormState = {
    level: number
    university: string
    faculty: string
    school: string
    department: string
    program: string
    graduationYear: string
    completionStatus: number
    diplomaNumber: string
    description: string
  }

  const emptyForm = (): FormState => ({
    level: 4,
    university: '',
    faculty: '',
    school: '',
    department: '',
    program: '',
    graduationYear: '',
    completionStatus: 2,
    diplomaNumber: '',
    description: '',
  })

  const [options, setOptions] = useState<EducationFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canManage) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchEducationFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* form açılınca tekrar deneriz */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManage])

  function openCreate() {
    setEditingId(null)
    setForm(emptyForm())
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: Edu) {
    setEditingId(item.id)
    setForm({
      level: item.level,
      university: item.university ?? '',
      faculty: item.faculty ?? '',
      school: item.school ?? '',
      department: item.department ?? '',
      program: item.program ?? '',
      graduationYear: item.graduationYear != null ? String(item.graduationYear) : '',
      completionStatus: item.completionStatus,
      diplomaNumber: item.diplomaNumber ?? '',
      description: item.description ?? '',
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function buildPayload(): UpsertEducationPayload {
    const year = form.graduationYear.trim()
    return {
      level: form.level,
      university: form.university.trim() || null,
      faculty: form.faculty.trim() || null,
      school: form.school.trim() || null,
      department: form.department.trim() || null,
      program: form.program.trim() || null,
      graduationYear: year ? Number(year) : null,
      completionStatus: form.completionStatus,
      diplomaNumber: form.diplomaNumber.trim() || null,
      description: form.description.trim() || null,
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      if (editingId) {
        await updateEducation(data.id, editingId, payload)
      } else {
        await createEducation(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Eğitim kaydı kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: Edu) {
    if (
      !(await confirm({
        title: 'Eğitim kaydını sil',
        message: `“${item.levelLabel}” kaydını silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteEducation(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="nested-crud">
      <div className="nested-crud-head">
        <div>
          <h3 className="subheading" style={{ margin: 0 }}>
            Eğitim kayıtları
          </h3>
          <p className="muted small" style={{ margin: '0.35rem 0 0' }}>
            Birden fazla eğitim seviyesi tutulabilir; mezuniyet ve bölüme göre listede filtrelenir
          </p>
        </div>
        {canManage && !showForm && (
          <button type="button" className="btn-primary" onClick={openCreate}>
            + Eğitim ekle
          </button>
        )}
      </div>

      {!canManage && (
        <p className="muted small">Eğitim ekleme/düzenleme için yetkili kullanıcı gerekir.</p>
      )}

      {error && <div className="form-error">{error}</div>}

      {showForm && options && (
        <form className="nested-form" onSubmit={onSubmit}>
          <h4>{editingId ? 'Eğitimi düzenle' : 'Yeni eğitim'}</h4>
          <div className="form-grid">
            <label>
              Seviye *
              <select
                value={form.level}
                onChange={(e) => setForm((f) => ({ ...f, level: Number(e.target.value) }))}
              >
                {options.levels.map((l) => (
                  <option key={l.value} value={l.value}>
                    {l.label}
                  </option>
                ))}
              </select>
              {fieldError('level')}
            </label>
            <label>
              Tamamlanma *
              <select
                value={form.completionStatus}
                onChange={(e) =>
                  setForm((f) => ({ ...f, completionStatus: Number(e.target.value) }))
                }
              >
                {options.completionStatuses.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
              {fieldError('completionStatus')}
            </label>
            <label>
              Üniversite / okul
              <input
                value={form.university}
                onChange={(e) => setForm((f) => ({ ...f, university: e.target.value }))}
              />
            </label>
            <label>
              Fakülte
              <input
                value={form.faculty}
                onChange={(e) => setForm((f) => ({ ...f, faculty: e.target.value }))}
              />
            </label>
            <label>
              Okul (lise vb.)
              <input
                value={form.school}
                onChange={(e) => setForm((f) => ({ ...f, school: e.target.value }))}
              />
            </label>
            <label>
              Bölüm
              <input
                value={form.department}
                onChange={(e) => setForm((f) => ({ ...f, department: e.target.value }))}
              />
            </label>
            <label>
              Program
              <input
                value={form.program}
                onChange={(e) => setForm((f) => ({ ...f, program: e.target.value }))}
              />
            </label>
            <label>
              Mezuniyet yılı
              <input
                type="number"
                value={form.graduationYear}
                onChange={(e) => setForm((f) => ({ ...f, graduationYear: e.target.value }))}
                placeholder="örn. 2018"
              />
              {fieldError('graduationYear')}
            </label>
            <label>
              Diploma no
              <input
                value={form.diplomaNumber}
                onChange={(e) => setForm((f) => ({ ...f, diplomaNumber: e.target.value }))}
              />
            </label>
            <label className="span-2">
              Açıklama
              <textarea
                rows={2}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </label>
          </div>
          <div className="form-actions">
            <button type="submit" disabled={saving}>
              {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setShowForm(false)
                setEditingId(null)
              }}
            >
              İptal
            </button>
          </div>
        </form>
      )}

      {data.education.length === 0 && !showForm ? (
        <p className="muted">Eğitim kaydı yok.</p>
      ) : (
        <ul className="card-list">
          {data.education.map((e) => (
            <li key={e.id}>
              <div className="card-list-row">
                <div>
                  <div className="emp-detail-card-title">
                    <strong>
                      {e.levelLabel}
                      {e.department ? ` — ${e.department}` : e.program ? ` — ${e.program}` : ''}
                    </strong>
                    <span className="emp-chip">{e.completionStatusLabel}</span>
                  </div>
                  <span className="muted">
                    {[e.university, e.school, e.faculty, e.program, e.graduationYear]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                  {e.diplomaNumber && (
                    <p className="muted small">Diploma no: {e.diplomaNumber}</p>
                  )}
                  {e.description && <p className="muted small">{e.description}</p>}
                </div>
                {canManage && (
                  <div className="row-actions">
                    <button type="button" className="btn-secondary" onClick={() => openEdit(e)}>
                      Düzenle
                    </button>
                    <button type="button" className="btn-danger" onClick={() => void onDelete(e)}>
                      Sil
                    </button>
                  </div>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function SkillsTab({
  data,
  canManageSkills,
  canManageCertificates,
  onChanged,
}: {
  data: EmployeeDetail
  canManageSkills: boolean
  canManageCertificates: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  type SkillItem = EmployeeDetail['skills'][number]
  type FormState = {
    skillId: string
    level: number
    experienceDuration: string
    hasCertificate: boolean
    certificateDate: string
    certificateIssuer: string
    description: string
  }

  const emptyForm = (): FormState => ({
    skillId: '',
    level: 2,
    experienceDuration: '',
    hasCertificate: false,
    certificateDate: '',
    certificateIssuer: '',
    description: '',
  })

  const [options, setOptions] = useState<SkillFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canManageSkills) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchSkillFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* form açılınca görünür */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManageSkills])

  function openCreate() {
    setEditingId(null)
    setForm(emptyForm())
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: SkillItem) {
    setEditingId(item.id)
    setForm({
      skillId: item.skillId,
      level: item.level,
      experienceDuration: item.experienceDuration ?? '',
      hasCertificate: item.hasCertificate,
      certificateDate: item.certificateDate ?? '',
      certificateIssuer: item.certificateIssuer ?? '',
      description: item.description ?? '',
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function buildPayload(): UpsertEmployeeSkillPayload {
    return {
      skillId: form.skillId,
      level: form.level,
      experienceDuration: form.experienceDuration.trim() || null,
      hasCertificate: form.hasCertificate,
      certificateDate: form.hasCertificate ? form.certificateDate || null : null,
      certificateIssuer: form.hasCertificate ? form.certificateIssuer.trim() || null : null,
      description: form.description.trim() || null,
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      if (editingId) {
        await updateEmployeeSkill(data.id, editingId, payload)
      } else {
        await createEmployeeSkill(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Yetkinlik kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: SkillItem) {
    if (
      !(await confirm({
        title: 'Yetkinliği sil',
        message: `“${item.name}” yetkinliğini silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteEmployeeSkill(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="two-col">
      <div className="nested-crud">
        <div className="nested-crud-head">
          <h3 className="subheading" style={{ margin: 0 }}>
            Yetkinlikler
          </h3>
          {canManageSkills && !showForm && (
            <button type="button" className="btn-primary" onClick={openCreate}>
              + Yetkinlik ekle
            </button>
          )}
        </div>

        {!canManageSkills && (
          <p className="muted small">Yetkinlik yönetimi için yetkili kullanıcı gerekir.</p>
        )}

        {error && <div className="form-error">{error}</div>}

        {showForm && options && (
          <form className="nested-form" onSubmit={onSubmit}>
            <h4>{editingId ? 'Yetkinliği düzenle' : 'Yeni yetkinlik'}</h4>
            <div className="form-grid">
              <label className="span-2">
                Yetkinlik (katalog) *
                <select
                  value={form.skillId}
                  required
                  onChange={(e) => setForm((f) => ({ ...f, skillId: e.target.value }))}
                >
                  <option value="">Seçiniz</option>
                  {options.skills.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name} ({s.categoryLabel})
                    </option>
                  ))}
                </select>
                {fieldError('skillId')}
              </label>
              <label>
                Seviye *
                <select
                  value={form.level}
                  onChange={(e) => setForm((f) => ({ ...f, level: Number(e.target.value) }))}
                >
                  {options.levels.map((l) => (
                    <option key={l.value} value={l.value}>
                      {l.label}
                    </option>
                  ))}
                </select>
                {fieldError('level')}
              </label>
              <label>
                Deneyim süresi
                <input
                  value={form.experienceDuration}
                  onChange={(e) => setForm((f) => ({ ...f, experienceDuration: e.target.value }))}
                  placeholder="örn. 3 yıl"
                />
              </label>
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={form.hasCertificate}
                  onChange={(e) =>
                    setForm((f) => ({
                      ...f,
                      hasCertificate: e.target.checked,
                      certificateDate: e.target.checked ? f.certificateDate : '',
                      certificateIssuer: e.target.checked ? f.certificateIssuer : '',
                    }))
                  }
                />
                Sertifikası var (özet bayrak)
              </label>
              {form.hasCertificate && (
                <>
                  <label>
                    Sertifika tarihi
                    <input
                      type="date"
                      value={form.certificateDate}
                      onChange={(e) => setForm((f) => ({ ...f, certificateDate: e.target.value }))}
                    />
                    {fieldError('certificateDate')}
                  </label>
                  <label>
                    Sertifika kurumu
                    <input
                      value={form.certificateIssuer}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, certificateIssuer: e.target.value }))
                      }
                    />
                  </label>
                </>
              )}
              <label className="span-2">
                Açıklama
                <textarea
                  rows={2}
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                />
              </label>
            </div>
            <div className="form-actions">
              <button type="submit" disabled={saving}>
                {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
              </button>
              <button
                type="button"
                className="btn-secondary"
                onClick={() => {
                  setShowForm(false)
                  setEditingId(null)
                }}
              >
                İptal
              </button>
            </div>
          </form>
        )}

        {data.skills.length === 0 && !showForm ? (
          <p className="muted">Yetkinlik yok.</p>
        ) : (
        <ul className="card-list">
          {data.skills.map((s) => (
            <li key={s.id}>
              <div className="card-list-row">
                <div>
                  <div className="emp-detail-card-title">
                    <strong>{s.name}</strong>
                    <span className="emp-chip emp-chip-primary">{s.levelLabel}</span>
                  </div>
                  <span className="muted">
                    {s.categoryLabel}
                    {s.experienceDuration ? ` · Deneyim: ${s.experienceDuration}` : ''}
                    {s.hasCertificate
                      ? ` · Sertifika${s.certificateIssuer ? `: ${s.certificateIssuer}` : ''}${s.certificateDate ? ` (${formatDate(s.certificateDate)})` : ''}`
                      : ''}
                  </span>
                  {s.description && <p className="muted small">{s.description}</p>}
                </div>
                {canManageSkills && (
                  <div className="row-actions">
                    <button type="button" className="btn-secondary" onClick={() => openEdit(s)}>
                      Düzenle
                    </button>
                    <button type="button" className="btn-danger" onClick={() => void onDelete(s)}>
                      Sil
                    </button>
                  </div>
                )}
              </div>
            </li>
          ))}
        </ul>
        )}
      </div>

      <CertificatesPanel
        data={data}
        canManage={canManageCertificates}
        onChanged={onChanged}
      />
    </div>
  )
}

function CertificatesPanel({
  data,
  canManage,
  onChanged,
}: {
  data: EmployeeDetail
  canManage: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  type Cert = EmployeeDetail['certificates'][number]
  type FormState = {
    certificateDefinitionId: string
    name: string
    issuer: string
    category: string
    issuedOn: string
    expiresOn: string
    documentNumber: string
    description: string
    relatedSkillId: string
  }

  const emptyForm = (): FormState => ({
    certificateDefinitionId: '',
    name: '',
    issuer: '',
    category: '',
    issuedOn: '',
    expiresOn: '',
    documentNumber: '',
    description: '',
    relatedSkillId: '',
  })

  const [options, setOptions] = useState<CertificateFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canManage) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchCertificateFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* ignore */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManage])

  function onDefinitionChange(id: string) {
    const def = options?.definitions.find((d) => d.id === id)
    setForm((f) => ({
      ...f,
      certificateDefinitionId: id,
      name: def ? def.name : f.name,
      category: def?.code ?? f.category,
    }))
  }

  function openCreate() {
    setEditingId(null)
    setForm(emptyForm())
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: Cert) {
    setEditingId(item.id)
    setForm({
      certificateDefinitionId: item.certificateDefinitionId ?? '',
      name: item.name,
      issuer: item.issuer ?? '',
      category: item.category ?? '',
      issuedOn: item.issuedOn ?? '',
      expiresOn: item.expiresOn ?? '',
      documentNumber: item.documentNumber ?? '',
      description: item.description ?? '',
      relatedSkillId: item.relatedSkillId ?? '',
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function buildPayload(): UpsertCertificatePayload {
    return {
      certificateDefinitionId: form.certificateDefinitionId || null,
      name: form.name.trim(),
      issuer: form.issuer.trim() || null,
      category: form.category.trim() || null,
      issuedOn: form.issuedOn || null,
      expiresOn: form.expiresOn || null,
      documentNumber: form.documentNumber.trim() || null,
      description: form.description.trim() || null,
      relatedSkillId: form.relatedSkillId || null,
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      if (editingId) {
        await updateCertificate(data.id, editingId, payload)
      } else {
        await createCertificate(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Sertifika kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: Cert) {
    if (
      !(await confirm({
        title: 'Sertifikayı sil',
        message: `“${item.name}” sertifikasını silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteCertificate(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="nested-crud">
      <div className="nested-crud-head">
        <div>
          <h3 className="subheading" style={{ margin: 0 }}>
            Sertifikalar
          </h3>
          <p className="muted small" style={{ margin: '0.35rem 0 0' }}>
            Asıl belge kaydı. Yetkinlikteki bayrak yalnızca özet bilgidir.
          </p>
        </div>
        {canManage && !showForm && (
          <button type="button" className="btn-primary" onClick={openCreate}>
            + Sertifika ekle
          </button>
        )}
      </div>

      {!canManage && (
        <p className="muted small">Sertifika yönetimi için yetkili kullanıcı gerekir.</p>
      )}

      {error && <div className="form-error">{error}</div>}

      {showForm && options && (
        <form className="nested-form" onSubmit={onSubmit}>
          <h4>{editingId ? 'Sertifikayı düzenle' : 'Yeni sertifika'}</h4>
          <div className="form-grid">
            <label className="span-2">
              Katalog tanımı (opsiyonel)
              <select
                value={form.certificateDefinitionId}
                onChange={(e) => onDefinitionChange(e.target.value)}
              >
                <option value="">Serbest / katalog dışı</option>
                {options.definitions.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                    {d.code ? ` (${d.code})` : ''}
                  </option>
                ))}
              </select>
            </label>
            <label className="span-2">
              Ad *
              <input
                value={form.name}
                required
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
              />
              {fieldError('name')}
            </label>
            <label>
              Kurum
              <input
                value={form.issuer}
                onChange={(e) => setForm((f) => ({ ...f, issuer: e.target.value }))}
              />
            </label>
            <label>
              Kategori
              <input
                value={form.category}
                onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))}
              />
            </label>
            <label>
              Veriliş
              <input
                type="date"
                value={form.issuedOn}
                onChange={(e) => setForm((f) => ({ ...f, issuedOn: e.target.value }))}
              />
              {fieldError('issuedOn')}
            </label>
            <label>
              Geçerlilik bitiş
              <input
                type="date"
                value={form.expiresOn}
                onChange={(e) => setForm((f) => ({ ...f, expiresOn: e.target.value }))}
              />
              {fieldError('expiresOn')}
            </label>
            <label>
              Belge no
              <input
                value={form.documentNumber}
                onChange={(e) => setForm((f) => ({ ...f, documentNumber: e.target.value }))}
              />
            </label>
            <label>
              İlişkili yetkinlik
              <select
                value={form.relatedSkillId}
                onChange={(e) => setForm((f) => ({ ...f, relatedSkillId: e.target.value }))}
              >
                <option value="">—</option>
                {options.skills.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="span-2">
              Açıklama
              <textarea
                rows={2}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </label>
          </div>
          <div className="form-actions">
            <button type="submit" disabled={saving}>
              {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setShowForm(false)
                setEditingId(null)
              }}
            >
              İptal
            </button>
          </div>
        </form>
      )}

      {data.certificates.length === 0 && !showForm ? (
        <p className="muted">Sertifika yok.</p>
      ) : (
        <ul className="card-list">
          {data.certificates.map((c) => {
            const expiry = certExpiryState(c.expiresOn)
            return (
              <li key={c.id}>
                <div className="card-list-row">
                  <div>
                    <div className="emp-detail-card-title">
                      <strong>{c.name}</strong>
                      {c.category ? <span className="emp-chip">{c.category}</span> : null}
                      {expiry === 'expired' ? (
                        <span className="emp-chip emp-chip-danger">Süresi doldu</span>
                      ) : null}
                      {expiry === 'soon' ? (
                        <span className="emp-chip emp-chip-warn">Yakında bitiyor</span>
                      ) : null}
                    </div>
                    <span className="muted">
                      {[
                        c.issuer,
                        c.issuedOn ? `Veriliş ${formatDate(c.issuedOn)}` : null,
                        c.expiresOn ? `Geçerlilik ${formatDate(c.expiresOn)}` : null,
                        c.documentNumber ? `No: ${c.documentNumber}` : null,
                        c.relatedSkillName ? `Yetkinlik: ${c.relatedSkillName}` : null,
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </span>
                    {c.description && <p className="muted small">{c.description}</p>}
                  </div>
                  {canManage && (
                    <div className="row-actions">
                      <button type="button" className="btn-secondary" onClick={() => openEdit(c)}>
                        Düzenle
                      </button>
                      <button type="button" className="btn-danger" onClick={() => void onDelete(c)}>
                        Sil
                      </button>
                    </div>
                  )}
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

function HistoryTab({
  data,
  canManage,
  onChanged,
}: {
  data: EmployeeDetail
  canManage: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  type Movement = EmployeeDetail['movements'][number]
  type FormState = {
    movementType: number
    startDate: string
    endDate: string
    reason: string
    description: string
    approvedBy: string
    oldUnitId: string
    newUnitId: string
    oldFacilityId: string
    newFacilityId: string
    oldJobTitleId: string
    newJobTitleId: string
    oldJobDutyId: string
    newJobDutyId: string
  }

  const emptyForm = (): FormState => ({
    movementType: 1,
    startDate: new Date().toISOString().slice(0, 10),
    endDate: '',
    reason: '',
    description: '',
    approvedBy: '',
    oldUnitId: data.corporate.unitId ?? '',
    newUnitId: '',
    oldFacilityId: data.corporate.facilityId ?? '',
    newFacilityId: '',
    oldJobTitleId: '',
    newJobTitleId: '',
    oldJobDutyId: '',
    newJobDutyId: '',
  })

  const [options, setOptions] = useState<MovementFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canManage) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchMovementFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* formda görünür */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManage])

  function openCreate() {
    setEditingId(null)
    setForm(emptyForm())
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: Movement) {
    setEditingId(item.id)
    setForm({
      movementType: item.movementType,
      startDate: item.startDate,
      endDate: item.endDate ?? '',
      reason: item.reason ?? '',
      description: item.description ?? '',
      approvedBy: item.approvedBy ?? '',
      oldUnitId: item.oldUnitId ?? '',
      newUnitId: item.newUnitId ?? '',
      oldFacilityId: item.oldFacilityId ?? '',
      newFacilityId: item.newFacilityId ?? '',
      oldJobTitleId: item.oldJobTitleId ?? '',
      newJobTitleId: item.newJobTitleId ?? '',
      oldJobDutyId: item.oldJobDutyId ?? '',
      newJobDutyId: item.newJobDutyId ?? '',
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function guidOrNull(v: string) {
    return v ? v : null
  }

  function buildPayload(): CreateMovementPayload {
    return {
      movementType: form.movementType,
      startDate: form.startDate,
      endDate: form.endDate.trim() || null,
      reason: form.reason.trim() || null,
      description: form.description.trim() || null,
      approvedBy: form.approvedBy.trim() || null,
      oldUnitId: guidOrNull(form.oldUnitId),
      newUnitId: guidOrNull(form.newUnitId),
      oldFacilityId: guidOrNull(form.oldFacilityId),
      newFacilityId: guidOrNull(form.newFacilityId),
      oldJobTitleId: guidOrNull(form.oldJobTitleId),
      newJobTitleId: guidOrNull(form.newJobTitleId),
      oldJobDutyId: guidOrNull(form.oldJobDutyId),
      newJobDutyId: guidOrNull(form.newJobDutyId),
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      if (editingId) {
        await updateMovement(data.id, editingId, payload)
      } else {
        await createMovement(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Hareket kaydı kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: Movement) {
    if (
      !(await confirm({
        title: 'Görev geçmişini sil',
        message: `“${item.movementTypeLabel}” kaydını silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteMovement(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="nested-crud">
      <div className="nested-crud-head">
        <div>
          <h3 className="subheading" style={{ margin: 0 }}>
            Görev geçmişi
          </h3>
          <p className="muted small" style={{ margin: '0.25rem 0 0' }}>
            Atama ve kurumsal değişiklikler zaman sırasıyla listelenir.
          </p>
        </div>
        {canManage && !showForm && (
          <button type="button" className="btn-primary" onClick={openCreate}>
            + Hareket ekle
          </button>
        )}
      </div>

      {!canManage && (
        <p className="muted small">Manuel hareket eklemek için yetkili kullanıcı gerekir.</p>
      )}

      {error && <div className="form-error">{error}</div>}

      {showForm && options && (
        <form className="nested-form" onSubmit={onSubmit}>
          <h4>{editingId ? 'Hareketi düzenle' : 'Yeni hareket'}</h4>
          <div className="form-grid">
            <label>
              Tür *
              <select
                value={form.movementType}
                onChange={(e) => setForm((f) => ({ ...f, movementType: Number(e.target.value) }))}
              >
                {options.movementTypes.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
              {fieldError('movementType')}
            </label>
            <label>
              Başlangıç *
              <input
                type="date"
                required
                value={form.startDate}
                onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))}
              />
              {fieldError('startDate')}
            </label>
            <label>
              Bitiş
              <input
                type="date"
                value={form.endDate}
                onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))}
              />
              {fieldError('endDate')}
            </label>
            <label>
              Onaylayan
              <input
                value={form.approvedBy}
                onChange={(e) => setForm((f) => ({ ...f, approvedBy: e.target.value }))}
              />
            </label>
            <label>
              Eski birim
              <select
                value={form.oldUnitId}
                onChange={(e) => setForm((f) => ({ ...f, oldUnitId: e.target.value }))}
              >
                <option value="">—</option>
                {options.units.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Yeni birim
              <select
                value={form.newUnitId}
                onChange={(e) => setForm((f) => ({ ...f, newUnitId: e.target.value }))}
              >
                <option value="">—</option>
                {options.units.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Eski tesis
              <select
                value={form.oldFacilityId}
                onChange={(e) => setForm((f) => ({ ...f, oldFacilityId: e.target.value }))}
              >
                <option value="">—</option>
                {options.facilities.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Yeni tesis
              <select
                value={form.newFacilityId}
                onChange={(e) => setForm((f) => ({ ...f, newFacilityId: e.target.value }))}
              >
                <option value="">—</option>
                {options.facilities.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Eski unvan
              <select
                value={form.oldJobTitleId}
                onChange={(e) => setForm((f) => ({ ...f, oldJobTitleId: e.target.value }))}
              >
                <option value="">—</option>
                {options.jobTitles.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Yeni unvan
              <select
                value={form.newJobTitleId}
                onChange={(e) => setForm((f) => ({ ...f, newJobTitleId: e.target.value }))}
              >
                <option value="">—</option>
                {options.jobTitles.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Eski görev
              <select
                value={form.oldJobDutyId}
                onChange={(e) => setForm((f) => ({ ...f, oldJobDutyId: e.target.value }))}
              >
                <option value="">—</option>
                {options.duties.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Yeni görev
              <select
                value={form.newJobDutyId}
                onChange={(e) => setForm((f) => ({ ...f, newJobDutyId: e.target.value }))}
              >
                <option value="">—</option>
                {options.duties.map((u) => (
                  <option key={u.id} value={u.id}>
                    {u.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="span-2">
              Neden
              <input
                value={form.reason}
                onChange={(e) => setForm((f) => ({ ...f, reason: e.target.value }))}
              />
              {fieldError('reason')}
            </label>
            <label className="span-2">
              Açıklama
              <textarea
                rows={2}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
              {fieldError('description')}
            </label>
          </div>
          <div className="form-actions">
            <button type="submit" disabled={saving}>
              {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setShowForm(false)
                setEditingId(null)
              }}
            >
              İptal
            </button>
          </div>
        </form>
      )}

      {data.movements.length === 0 && !showForm ? (
        <p className="muted">Henüz hareket kaydı yok.</p>
      ) : (
        <ol className="emp-mv-list">
          {data.movements.map((m) => {
            const summary = movementSummary(m)
            const meta = [
              m.createdBy ? m.createdBy : null,
              m.approvedBy ? `Onay: ${m.approvedBy}` : null,
            ].filter(Boolean)

            return (
              <li key={m.id} className="emp-mv-item">
                <div className="emp-mv-rail" aria-hidden>
                  <span className="emp-mv-dot" />
                </div>
                <div className="emp-mv-body">
                  <header className="emp-mv-head">
                    <div className="emp-mv-title-row">
                      <strong className="emp-mv-type">{m.movementTypeLabel}</strong>
                      <time className="emp-mv-date" dateTime={m.startDate}>
                        {formatDate(m.startDate)}
                        {m.endDate ? ` – ${formatDate(m.endDate)}` : ''}
                      </time>
                    </div>
                    {canManage ? (
                      <div className="emp-mv-actions">
                        <button type="button" className="emp-mv-action" onClick={() => openEdit(m)}>
                          Düzenle
                        </button>
                        <button
                          type="button"
                          className="emp-mv-action is-danger"
                          onClick={() => void onDelete(m)}
                        >
                          Sil
                        </button>
                      </div>
                    ) : null}
                  </header>

                  {summary.headline ? <p className="emp-mv-headline">{summary.headline}</p> : null}

                  {summary.details.length > 0 ? (
                    <ul className="emp-mv-details">
                      {summary.details.map((line) => (
                        <li key={line}>{line}</li>
                      ))}
                    </ul>
                  ) : null}

                  {m.description && summary.headline !== m.description ? (
                    <p className="emp-mv-note">{m.description}</p>
                  ) : null}
                  {m.reason && summary.headline !== m.reason ? (
                    <p className="emp-mv-note">Neden: {m.reason}</p>
                  ) : null}

                  {meta.length > 0 ? (
                    <p className="emp-mv-meta">{meta.join(' · ')}</p>
                  ) : null}
                </div>
              </li>
            )
          })}
        </ol>
      )}
    </div>
  )
}

function NotesTab({
  data,
  canCreate,
  onChanged,
}: {
  data: EmployeeDetail
  canCreate: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  type Note = EmployeeDetail['notes'][number]
  type FormState = {
    title: string
    category: number
    content: string
    visibility: number
    reminderDate: string
  }

  const emptyForm = (): FormState => ({
    title: '',
    category: 1,
    content: '',
    visibility: 1,
    reminderDate: '',
  })

  const [options, setOptions] = useState<NoteFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canCreate) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchNoteFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* formda görünür */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canCreate])

  function openCreate() {
    setEditingId(null)
    const next = emptyForm()
    if (options?.categories.length) next.category = options.categories[0].value
    if (options?.visibilities.length) next.visibility = options.visibilities[0].value
    setForm(next)
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: Note) {
    setEditingId(item.id)
    setForm({
      title: item.title,
      category: item.category,
      content: item.content,
      visibility: item.visibility,
      reminderDate: item.reminderDate ?? '',
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function buildPayload(): UpsertNotePayload {
    return {
      title: form.title.trim(),
      category: form.category,
      content: form.content.trim(),
      visibility: form.visibility,
      reminderDate: form.reminderDate.trim() || null,
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      if (editingId) {
        await updateNote(data.id, editingId, payload)
      } else {
        await createNote(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Not kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: Note) {
    if (
      !(await confirm({
        title: 'Notu sil',
        message: `“${item.title}” notunu silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteNote(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="nested-crud">
      <div className="nested-crud-head">
        <h3 className="subheading" style={{ margin: 0 }}>
          Personel notları
        </h3>
        {canCreate && !showForm && (
          <button type="button" className="btn-primary" onClick={openCreate}>
            + Not ekle
          </button>
        )}
      </div>

      <p className="muted small">
        Liste, görünürlük seviyenize göre filtrelenir. Başkasının “yalnızca yazar” notunu
        görmezsiniz.
      </p>

      {!canCreate && (
        <p className="muted small">Not eklemek için Notes.Create yetkisi gerekir.</p>
      )}

      {error && <div className="form-error">{error}</div>}

      {showForm && options && (
        <form className="nested-form" onSubmit={onSubmit}>
          <h4>{editingId ? 'Notu düzenle' : 'Yeni not'}</h4>
          <div className="form-grid">
            <label className="span-2">
              Başlık *
              <input
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                maxLength={200}
              />
              {fieldError('title')}
            </label>
            <label>
              Kategori *
              <select
                value={form.category}
                onChange={(e) => setForm((f) => ({ ...f, category: Number(e.target.value) }))}
              >
                {options.categories.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
              {fieldError('category')}
            </label>
            <label>
              Görünürlük *
              <select
                value={form.visibility}
                onChange={(e) => setForm((f) => ({ ...f, visibility: Number(e.target.value) }))}
              >
                {options.visibilities.map((v) => (
                  <option key={v.value} value={v.value}>
                    {v.label}
                  </option>
                ))}
              </select>
              {fieldError('visibility')}
            </label>
            <label>
              Hatırlatma tarihi
              <input
                type="date"
                value={form.reminderDate}
                onChange={(e) => setForm((f) => ({ ...f, reminderDate: e.target.value }))}
              />
              {fieldError('reminderDate')}
            </label>
            <label className="span-2">
              İçerik *
              <textarea
                rows={4}
                value={form.content}
                onChange={(e) => setForm((f) => ({ ...f, content: e.target.value }))}
              />
              {fieldError('content')}
            </label>
          </div>
          <div className="form-actions">
            <button type="submit" disabled={saving}>
              {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setShowForm(false)
                setEditingId(null)
              }}
            >
              İptal
            </button>
          </div>
        </form>
      )}

      {data.notes.length === 0 && !showForm ? (
        <p className="muted">Görünür not yok.</p>
      ) : (
        <ul className="card-list">
          {data.notes.map((n) => (
            <li key={n.id}>
              <div className="card-list-row">
                <div>
                  <div className="emp-detail-card-title">
                    <strong>{n.title}</strong>
                    <span className="emp-chip">{n.categoryLabel}</span>
                    <span className="emp-chip">{n.visibilityLabel}</span>
                  </div>
                  <span className="muted">
                    {[
                      n.createdBy ? `Ekleyen: ${n.createdBy}` : null,
                      formatDateTime(n.noteDateUtc),
                      n.reminderDate ? `Hatırlatma ${formatDate(n.reminderDate)}` : null,
                    ]
                      .filter(Boolean)
                      .join(' · ')}
                  </span>
                  <p className="muted small" style={{ whiteSpace: 'pre-wrap' }}>
                    {n.content}
                  </p>
                </div>
                {n.canModify && (
                  <div className="row-actions">
                    <button type="button" className="btn-secondary" onClick={() => openEdit(n)}>
                      Düzenle
                    </button>
                    <button type="button" className="btn-danger" onClick={() => void onDelete(n)}>
                      Sil
                    </button>
                  </div>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function SpecialTab({
  data,
  canViewSpecial,
  canManage,
  canUploadFiles,
  canViewFiles,
  onChanged,
}: {
  data: EmployeeDetail
  canViewSpecial: boolean
  canManage: boolean
  canUploadFiles: boolean
  canViewFiles: boolean
  onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  type Cond = NonNullable<EmployeeDetail['specialConditions']>[number]
  type FormState = {
    conditionType: string
    description: string
    startDate: string
    endDate: string
    isPermanent: boolean
    requiresDutyAdjustment: boolean
    requiresWorkspaceAdjustment: boolean
  }

  const emptyForm = (): FormState => ({
    conditionType: '',
    description: '',
    startDate: '',
    endDate: '',
    isPermanent: false,
    requiresDutyAdjustment: false,
    requiresWorkspaceAdjustment: false,
  })

  const [options, setOptions] = useState<SpecialConditionFormOptions | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [uploadingId, setUploadingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  useEffect(() => {
    if (!canManage) return
    let cancelled = false
    ;(async () => {
      try {
        const opts = await fetchSpecialConditionFormOptions()
        if (!cancelled) setOptions(opts)
      } catch {
        /* formda görünür */
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canManage])

  function openCreate() {
    setEditingId(null)
    setForm(emptyForm())
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(item: Cond) {
    setEditingId(item.id)
    setForm({
      conditionType: item.conditionType,
      description: item.description,
      startDate: item.startDate ?? '',
      endDate: item.endDate ?? '',
      isPermanent: item.isPermanent,
      requiresDutyAdjustment: item.requiresDutyAdjustment,
      requiresWorkspaceAdjustment: item.requiresWorkspaceAdjustment,
    })
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function buildPayload(): UpsertSpecialConditionPayload {
    return {
      conditionType: form.conditionType.trim(),
      description: form.description.trim(),
      startDate: form.startDate.trim() || null,
      endDate: form.isPermanent ? null : form.endDate.trim() || null,
      isPermanent: form.isPermanent,
      requiresDutyAdjustment: form.requiresDutyAdjustment,
      requiresWorkspaceAdjustment: form.requiresWorkspaceAdjustment,
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      if (editingId) {
        await updateSpecialCondition(data.id, editingId, payload)
      } else {
        await createSpecialCondition(data.id, payload)
      }
      setShowForm(false)
      setEditingId(null)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setError('Özel durum kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(item: Cond) {
    if (
      !(await confirm({
        title: 'Özel durum kaydını sil',
        message: `“${item.conditionType}” kaydını silmek istiyor musunuz?`,
      }))
    )
      return
    setError(null)
    try {
      await deleteSpecialCondition(data.id, item.id)
      if (editingId === item.id) {
        setShowForm(false)
        setEditingId(null)
      }
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    }
  }

  async function onUpload(item: Cond, file: File | undefined) {
    if (!file) return
    setUploadingId(item.id)
    setError(null)
    try {
      await uploadSpecialConditionDocument(data.id, item.id, file)
      await onChanged()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors?.file) setError(err.validationErrors.file.join(' '))
      } else {
        setError('Belge yüklenemedi.')
      }
    } finally {
      setUploadingId(null)
    }
  }

  async function onRemoveDocument(item: Cond) {
    if (
      !(await confirm({
        title: 'Belgeyi kaldır',
        message: 'Belgeyi kaldırmak istiyor musunuz?',
        confirmLabel: 'Kaldır',
      }))
    )
      return
    setError(null)
    try {
      await removeSpecialConditionDocument(data.id, item.id)
      await onChanged()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Belge kaldırılamadı.')
    }
  }

  async function onDownload(item: Cond) {
    setError(null)
    try {
      await downloadSpecialConditionDocument(data.id, item.id)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Belge indirilemedi.')
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  const canSeeDetails = canViewSpecial
  const items = data.specialConditions ?? []
  const canAttach = canManage && canUploadFiles

  if (!canSeeDetails && !canManage) {
    if (!data.hasSpecialCondition) {
      return <p className="muted">Özel durum kaydı bulunmuyor.</p>
    }
    return (
      <div className="notice">
        <strong>Özel durum kaydı bulunuyor</strong>
        <p className="muted">
          Detaylı açıklama yalnızca özel yetkili kullanıcılar tarafından görüntülenebilir.
          Normal kullanıcıya yalnızca varlık uyarısı gösterilir.
        </p>
      </div>
    )
  }

  if (!canSeeDetails && canManage) {
    return (
      <div className="notice">
        <strong>Yönetme yetkiniz var, görüntüleme yok</strong>
        <p className="muted">
          Özel durum içeriğini görmek için görüntüleme yetkisi de gerekir. Yazma genelde View
          ile birlikte verilir.
        </p>
      </div>
    )
  }

  return (
    <div className="nested-crud">
      <div className="nested-crud-head">
        <h3 className="subheading" style={{ margin: 0 }}>
          Özel durum kayıtları
        </h3>
        {canManage && !showForm && (
          <button type="button" className="btn-primary" onClick={openCreate}>
            + Özel durum ekle
          </button>
        )}
      </div>

      <p className="muted small">
        Hassas alan + belge: dosyalar yalnızca yetkili kullanıcılar tarafından indirilebilir.
        İzinli türler: PDF, PNG, JPG, DOCX (max 5 MB).
      </p>

      {!canManage && (
        <p className="muted small">Düzenleme için özel durum yönetimi yetkisi gerekir.</p>
      )}

      {error && <div className="form-error">{error}</div>}

      {showForm && (
        <form className="nested-form" onSubmit={onSubmit}>
          <h4>{editingId ? 'Özel durumu düzenle' : 'Yeni özel durum'}</h4>
          <div className="form-grid">
            <label className="span-2">
              Durum tipi *
              {options && options.suggestedTypes.length > 0 && (
                <select
                  className="emp-detail-suggest-select"
                  value=""
                  aria-label="Önerilen durum tipi"
                  onChange={(e) => {
                    const v = e.target.value
                    if (v) setForm((f) => ({ ...f, conditionType: v }))
                  }}
                >
                  <option value="">Önerilenlerden seçin…</option>
                  {options.suggestedTypes.map((t) => (
                    <option key={t} value={t}>
                      {t}
                    </option>
                  ))}
                </select>
              )}
              <input
                value={form.conditionType}
                onChange={(e) => setForm((f) => ({ ...f, conditionType: e.target.value }))}
                maxLength={150}
                placeholder="veya yazın — örn. Engellilik"
              />
              {fieldError('conditionType')}
            </label>
            <label className="span-2">
              Açıklama *
              <textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                maxLength={2000}
              />
              {fieldError('description')}
            </label>
            <label>
              Başlangıç
              <input
                type="date"
                value={form.startDate}
                onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))}
              />
            </label>
            <label>
              Bitiş
              <input
                type="date"
                value={form.endDate}
                disabled={form.isPermanent}
                onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))}
              />
              {fieldError('endDate')}
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={form.isPermanent}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    isPermanent: e.target.checked,
                    endDate: e.target.checked ? '' : f.endDate,
                  }))
                }
              />
              Sürekli
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={form.requiresDutyAdjustment}
                onChange={(e) =>
                  setForm((f) => ({ ...f, requiresDutyAdjustment: e.target.checked }))
                }
              />
              Görev uyarlaması gerekir
            </label>
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={form.requiresWorkspaceAdjustment}
                onChange={(e) =>
                  setForm((f) => ({ ...f, requiresWorkspaceAdjustment: e.target.checked }))
                }
              />
              Çalışma alanı uyarlaması gerekir
            </label>
          </div>
          <p className="muted small">Belgeyi kayıt oluşturduktan sonra satırdan yükleyin.</p>
          <div className="form-actions">
            <button type="submit" disabled={saving}>
              {saving ? 'Kaydediliyor…' : editingId ? 'Güncelle' : 'Ekle'}
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setShowForm(false)
                setEditingId(null)
              }}
            >
              İptal
            </button>
          </div>
        </form>
      )}

      {items.length === 0 && !showForm ? (
        <p className="muted">Özel durum kaydı yok.</p>
      ) : (
        <ul className="card-list">
          {items.map((s) => (
            <li key={s.id}>
              <div className="card-list-row">
                <div>
                  <strong>{s.conditionType}</strong>
                  <p>{s.description}</p>
                  <span className="muted">
                    {s.isPermanent ? 'Sürekli' : 'Geçici'}
                    {s.startDate ? ` · ${formatDate(s.startDate)}` : ''}
                    {s.endDate ? ` – ${formatDate(s.endDate)}` : ''}
                    {s.requiresDutyAdjustment ? ' · görev uyarlaması' : ''}
                    {s.requiresWorkspaceAdjustment ? ' · alan uyarlaması' : ''}
                    {s.hasDocument ? ' · belge var' : ''}
                  </span>
                  <div className="row-actions" style={{ marginTop: '0.5rem' }}>
                    {s.hasDocument && canViewFiles && canViewSpecial && (
                      <button
                        type="button"
                        className="btn-secondary"
                        onClick={() => void onDownload(s)}
                      >
                        Belgeyi indir
                      </button>
                    )}
                    {canAttach && (
                      <label className="btn-secondary" style={{ cursor: 'pointer' }}>
                        {uploadingId === s.id
                          ? 'Yükleniyor…'
                          : s.hasDocument
                            ? 'Belgeyi değiştir'
                            : 'Belge yükle'}
                        <input
                          type="file"
                          accept=".pdf,.png,.jpg,.jpeg,.docx,application/pdf,image/png,image/jpeg,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                          hidden
                          disabled={uploadingId === s.id}
                          onChange={(e) => {
                            const f = e.target.files?.[0]
                            e.target.value = ''
                            void onUpload(s, f)
                          }}
                        />
                      </label>
                    )}
                    {s.hasDocument && canAttach && (
                      <button
                        type="button"
                        className="btn-danger"
                        onClick={() => void onRemoveDocument(s)}
                      >
                        Belgeyi kaldır
                      </button>
                    )}
                  </div>
                </div>
                {canManage && (
                  <div className="row-actions">
                    <button type="button" className="btn-secondary" onClick={() => openEdit(s)}>
                      Düzenle
                    </button>
                    <button type="button" className="btn-danger" onClick={() => void onDelete(s)}>
                      Sil
                    </button>
                  </div>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function formatDate(isoDate?: string | null): string | null {
  if (!isoDate) return null
  const [y, m, d] = isoDate.split('-')
  if (!y || !m || !d) return isoDate
  return `${d}.${m}.${y}`
}

function formatDateTime(iso?: string | null): string {
  if (!iso) return '—'
  try {
    return new Date(iso).toLocaleString('tr-TR')
  } catch {
    return iso
  }
}

function isCertExpired(expiresOn?: string | null): boolean {
  return certExpiryState(expiresOn) === 'expired'
}

function certExpiryState(expiresOn?: string | null): 'ok' | 'soon' | 'expired' | 'none' {
  if (!expiresOn) return 'none'
  const end = new Date(`${expiresOn}T00:00:00`)
  if (Number.isNaN(end.getTime())) return 'none'
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const diffDays = Math.ceil((end.getTime() - today.getTime()) / 86_400_000)
  if (diffDays < 0) return 'expired'
  if (diffDays <= 60) return 'soon'
  return 'ok'
}

/** Zaman çizelgesi satırı için kısa, okunabilir özet. */
function movementSummary(m: {
  oldUnitName?: string | null
  newUnitName?: string | null
  oldFacilityName?: string | null
  newFacilityName?: string | null
  oldJobTitleName?: string | null
  newJobTitleName?: string | null
  oldJobDutyName?: string | null
  newJobDutyName?: string | null
  reason?: string | null
  description?: string | null
}): { headline: string | null; details: string[] } {
  const details: string[] = []

  function pushChange(label: string, from?: string | null, to?: string | null) {
    if (!from && !to) return
    if (from && to && from === to) return
    if (!from && to) details.push(`${label}: ${to}`)
    else if (from && !to) details.push(`${label} kaldırıldı (${from})`)
    else details.push(`${label}: ${from} → ${to}`)
  }

  pushChange('Birim', m.oldUnitName, m.newUnitName)
  pushChange('Tesis', m.oldFacilityName, m.newFacilityName)
  pushChange('Unvan', m.oldJobTitleName, m.newJobTitleName)
  pushChange('Görev', m.oldJobDutyName, m.newJobDutyName)

  let headline: string | null = null
  if (details.length === 1) {
    headline = details[0]
    return { headline, details: [] }
  }
  if (details.length === 0 && m.description) {
    headline = m.description
    return { headline, details: [] }
  }
  if (details.length === 0 && m.reason) {
    headline = m.reason
    return { headline, details: [] }
  }
  return { headline: null, details }
}

function buildMissingHints(data: EmployeeDetail): string[] {
  const hints: string[] = []
  const c = data.corporate
  const g = data.general
  if (!data.hasPhoto) hints.push('Profil fotoğrafı yok')
  if (!c.jobTitleName) hints.push('Resmi unvan girilmemiş')
  if (!c.primaryDutyName) hints.push('Ana fiili görev atanmamış')
  if (!c.unitName && !c.mainUnitName && !c.subUnitName) hints.push('Birim bilgisi eksik')
  if (!c.facilityName) hints.push('Tesis atanmamış')
  if (!c.hireDate) hints.push('İşe giriş tarihi yok')
  if (!g.personalPhone && !g.corporatePhone) hints.push('Telefon bilgisi eksik')
  if (data.education.length === 0) hints.push('Eğitim kaydı yok')
  if (data.skills.length === 0) hints.push('Yetkinlik girilmemiş')
  if (data.certificates.some((x) => isCertExpired(x.expiresOn))) {
    hints.push('Süresi dolmuş sertifika var')
  } else if (data.certificates.some((x) => certExpiryState(x.expiresOn) === 'soon')) {
    hints.push('Yakında süresi dolacak sertifika var')
  }
  return hints.slice(0, 6)
}
