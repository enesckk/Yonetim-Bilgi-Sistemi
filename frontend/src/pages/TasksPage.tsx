import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { ApiClientError, getAccessToken } from '@/api/client'
import {
  cancelWorkTask,
  createWorkTask,
  deleteWorkTask,
  fetchWorkTask,
  fetchWorkTaskOptions,
  fetchWorkTasks,
  reviewWorkTask,
  submitWorkTask,
  workTaskFileUrl,
  type WorkTaskDetail,
  type WorkTaskKind,
  type WorkTaskRow,
  type WorkTaskUser,
} from '@/api/workTasksApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useAlert, useConfirm } from '@/components/ConfirmDialog'

type Tab = 'inbox' | 'assigned' | 'approval' | 'done'

export function TasksPage() {
  const { hasPermission } = useAuth()
  const alert = useAlert()
  const confirm = useConfirm()
  const canView = hasPermission(PermissionCodes.TasksView)
  const canAssign = hasPermission(PermissionCodes.TasksAssign)
  const canSubmit = hasPermission(PermissionCodes.TasksSubmit)
  const canReview = hasPermission(PermissionCodes.TasksReview)

  const [tab, setTab] = useState<Tab>(canReview ? 'approval' : 'inbox')
  const [rows, setRows] = useState<WorkTaskRow[]>([])
  const [people, setPeople] = useState<WorkTaskUser[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<WorkTaskDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [compose, setCompose] = useState(false)

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [kind, setKind] = useState<WorkTaskKind>(canAssign ? 1 : 2)
  const [assigneeId, setAssigneeId] = useState('')
  const [dueOn, setDueOn] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [saving, setSaving] = useState(false)

  const [reviewNote, setReviewNote] = useState('')
  const [submitNote, setSubmitNote] = useState('')
  const [submitFiles, setSubmitFiles] = useState<FileList | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [list, opts] = await Promise.all([fetchWorkTasks(tab), fetchWorkTaskOptions()])
      setRows(list)
      setPeople(opts)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'İşler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [tab])

  useEffect(() => {
    if (!canView) return
    void load()
  }, [canView, load])

  useEffect(() => {
    if (!selectedId) {
      setDetail(null)
      return
    }
    let cancelled = false
    void fetchWorkTask(selectedId)
      .then((d) => {
        if (!cancelled) {
          setDetail(d)
          setReviewNote(d.reviewNote ?? '')
        }
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof ApiClientError ? err.message : 'İş açılmadı.')
      })
    return () => {
      cancelled = true
    }
  }, [selectedId])

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">İş ataması için yetkiniz yok.</p>
      </div>
    )
  }

  async function onCreate(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const form = new FormData()
      form.set('title', title.trim())
      form.set('description', description.trim())
      form.set('kind', String(kind))
      if (kind === 1 && assigneeId) form.set('assigneeUserId', assigneeId)
      if (dueOn) form.set('dueOn', dueOn)
      if (files.length) files.forEach((f) => form.append('files', f))
      const created = await createWorkTask(form)
      setCompose(false)
      setTitle('')
      setDescription('')
      setAssigneeId('')
      setDueOn('')
      setKind(canAssign ? 1 : 2)
      setFiles([])
      setSelectedId(created.id)
      await load()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'İş kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  async function onSubmitWork() {
    if (!detail) return
    setSaving(true)
    try {
      const form = new FormData()
      if (submitNote.trim()) form.set('note', submitNote.trim())
      if (submitFiles) Array.from(submitFiles).forEach((f) => form.append('files', f))
      const next = await submitWorkTask(detail.id, form)
      setDetail(next)
      setSubmitNote('')
      setSubmitFiles(null)
      await load()
    } catch (err) {
      await alert(err instanceof ApiClientError ? err.message : 'Teslim edilemedi.')
    } finally {
      setSaving(false)
    }
  }

  async function onReview(decision: 'approve' | 'reject' | 'revision') {
    if (!detail) return
    if ((decision === 'reject' || decision === 'revision') && !reviewNote.trim()) {
      await alert('Red veya revizyon için kısa bir açıklama yazın.')
      return
    }
    setSaving(true)
    try {
      const next = await reviewWorkTask(detail.id, decision, reviewNote.trim() || undefined)
      setDetail(next)
      await load()
    } catch (err) {
      await alert(err instanceof ApiClientError ? err.message : 'Onay işlemi yapılamadı.')
    } finally {
      setSaving(false)
    }
  }

  async function onCancelTask() {
    if (!detail) return
    const ok = await confirm({
      title: 'İşi iptal et',
      message: `"${detail.title}" iptal edilsin mi?`,
      confirmLabel: 'İptal et',
      tone: 'danger',
    })
    if (!ok) return
    setSaving(true)
    try {
      const next = await cancelWorkTask(detail.id)
      setDetail(next)
      await load()
    } catch (err) {
      await alert(err instanceof ApiClientError ? err.message : 'İptal edilemedi.')
    } finally {
      setSaving(false)
    }
  }

  async function onDeleteTask() {
    if (!detail) return
    const ok = await confirm({
      title: 'İşi sil',
      message: `"${detail.title}" silinsin mi? Bu işlem geri alınamaz.`,
      confirmLabel: 'Sil',
      tone: 'danger',
    })
    if (!ok) return
    setSaving(true)
    try {
      await deleteWorkTask(detail.id)
      setSelectedId(null)
      setDetail(null)
      await load()
    } catch (err) {
      await alert(err instanceof ApiClientError ? err.message : 'Silinemedi.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="employees-page tasks-page">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>İş ataması</h2>
            </div>
            <p className="muted small employees-toolbar-lead">
              {canAssign
                ? 'Amire iş verin, görsel ekleyin. Gelen teslimleri onaylayın, reddedin veya revizyon isteyin.'
                : 'Size verilen işi görselle teslim edin ya da müdüre onay için iş sunun.'}
            </p>
          </div>
          {(canAssign || canSubmit) && (
            <button type="button" className="btn-primary" onClick={() => setCompose(true)}>
              {canAssign ? 'İş ata' : 'Onay talebi'}
            </button>
          )}
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="stock-tabs" role="tablist">
          {(
            [
              ['inbox', 'Aktif'],
              ['assigned', 'Atadıklarım'],
              ['approval', 'Onay'],
              ['done', 'Kapanan'],
            ] as const
          )
            .filter(([id]) => (id === 'assigned' ? canAssign : id === 'approval' ? canReview || canAssign : true))
            .map(([id, label]) => (
              <button
                key={id}
                type="button"
                className={tab === id ? 'is-on' : ''}
                onClick={() => {
                  setTab(id)
                  setSelectedId(null)
                }}
              >
                {label}
              </button>
            ))}
        </div>

        <div className="tasks-split">
          <ul className="tasks-list">
            {loading ? (
              <li className="staff-dash-empty">Yükleniyor…</li>
            ) : rows.length === 0 ? (
              <li className="staff-dash-empty">Bu listede iş yok.</li>
            ) : (
              rows.map((row) => (
                <li key={row.id}>
                  <button
                    type="button"
                    className={selectedId === row.id ? 'is-on' : ''}
                    onClick={() => setSelectedId(row.id)}
                  >
                    <strong>{row.title}</strong>
                    <span>
                      {row.statusLabel}
                      {row.assigneeName ? ` · ${row.assigneeName}` : ''}
                    </span>
                    <em>{row.kindLabel}</em>
                  </button>
                </li>
              ))
            )}
          </ul>

          <div className="tasks-detail">
            {!detail ? (
              <p className="staff-dash-empty">Soldan bir iş seçin.</p>
            ) : (
              <>
                <header>
                  <p className="org-card-section-label">{detail.kindLabel}</p>
                  <h3>{detail.title}</h3>
                  <p className="muted small">
                    {detail.statusLabel}
                    {detail.assigneeName ? ` · ${detail.assigneeName}` : ''}
                    {detail.createdByName ? ` · ${detail.createdByName}` : ''}
                  </p>
                </header>
                {detail.description ? <p className="tasks-body">{detail.description}</p> : null}
                {detail.reviewNote ? (
                  <p className="tasks-review-note">
                    <strong>Müdür notu:</strong> {detail.reviewNote}
                  </p>
                ) : null}

                {detail.attachments.length > 0 ? (
                  <ul className="tasks-files">
                    {detail.attachments.map((f) => (
                      <li key={f.id}>
                        <button type="button" className="skills-link-btn" onClick={() => void openFile(detail.id, f.id, f.fileName)}>
                          {f.contentType.startsWith('image/') ? 'Görsel' : 'Dosya'}: {f.fileName}
                        </button>
                      </li>
                    ))}
                  </ul>
                ) : null}

                {detail.canSubmit ? (
                  <div className="tasks-actions">
                    <textarea
                      rows={3}
                      placeholder="Teslim notu"
                      value={submitNote}
                      onChange={(e) => setSubmitNote(e.target.value)}
                    />
                    <input
                      type="file"
                      accept="image/*,.pdf"
                      multiple
                      onChange={(e) => setSubmitFiles(e.target.files)}
                    />
                    <button type="button" className="btn-primary" disabled={saving} onClick={() => void onSubmitWork()}>
                      Onaya gönder
                    </button>
                  </div>
                ) : null}

                {detail.canReview || detail.canCancel || detail.canDelete ? (
                  <div className="tasks-actions">
                    {detail.canReview ? (
                      <textarea
                        rows={3}
                        placeholder="Onay, red veya revizyon açıklaması"
                        value={reviewNote}
                        onChange={(e) => setReviewNote(e.target.value)}
                      />
                    ) : null}
                    <div className="tasks-review-btns">
                      {detail.canReview ? (
                        <>
                          <button type="button" className="btn-primary" disabled={saving} onClick={() => void onReview('approve')}>
                            Onayla
                          </button>
                          <button type="button" className="btn-secondary" disabled={saving} onClick={() => void onReview('revision')}>
                            Revizyon iste
                          </button>
                          <button type="button" className="btn-secondary catalog-danger" disabled={saving} onClick={() => void onReview('reject')}>
                            Reddet
                          </button>
                        </>
                      ) : null}
                      {detail.canCancel ? (
                        <button type="button" className="btn-secondary" disabled={saving} onClick={() => void onCancelTask()}>
                          İptal et
                        </button>
                      ) : null}
                      {detail.canDelete ? (
                        <button type="button" className="btn-secondary catalog-danger" disabled={saving} onClick={() => void onDeleteTask()}>
                          Sil
                        </button>
                      ) : null}
                    </div>
                  </div>
                ) : null}

                <ul className="staff-dash-feed">
                  {detail.activities.map((a) => (
                    <li key={a.id}>
                      <strong>{a.actorName}</strong>
                      <em>
                        {a.actionLabel}
                        {a.note ? ` · ${a.note}` : ''}
                      </em>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>
        </div>
      </section>

      {compose ? (
        <div className="org-modal-backdrop" role="presentation" onClick={() => setCompose(false)}>
          <form
            className="panel org-modal tasks-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="tasks-compose-title"
            onClick={(e) => e.stopPropagation()}
            onSubmit={(e) => void onCreate(e)}
          >
            <div className="org-modal-head">
              <p className="org-modal-eyebrow">{canAssign ? 'Görev' : 'Talep'}</p>
              <h2 id="tasks-compose-title">{canAssign && kind === 1 ? 'İş ata' : 'Onay talebi'}</h2>
            </div>
            {canAssign ? (
              <div className="tasks-kind" role="tablist" aria-label="Tür">
                <button
                  type="button"
                  role="tab"
                  className={kind === 1 ? 'is-on' : ''}
                  aria-selected={kind === 1}
                  onClick={() => setKind(1)}
                >
                  İş ataması
                </button>
                <button
                  type="button"
                  role="tab"
                  className={kind === 2 ? 'is-on' : ''}
                  aria-selected={kind === 2}
                  onClick={() => setKind(2)}
                >
                  Onay talebi
                </button>
              </div>
            ) : null}
            <div className="form-grid">
              <label className="span-2">
                Başlık
                <input
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  required
                  maxLength={200}
                  placeholder="Kısa ve net bir başlık"
                />
              </label>
              <label className="span-2">
                Açıklama
                <textarea
                  rows={4}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Beklenen iş, kapsam veya teslim notu"
                />
              </label>
              {kind === 1 ? (
                <label>
                  Kime
                  <select value={assigneeId} onChange={(e) => setAssigneeId(e.target.value)} required>
                    <option value="">Kişi seçin</option>
                    {people.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.displayName}
                        {p.roleLabel ? ` · ${p.roleLabel}` : ''}
                      </option>
                    ))}
                  </select>
                </label>
              ) : null}
              <label className={kind === 1 ? '' : 'span-2'}>
                Son tarih
                <input type="date" value={dueOn} onChange={(e) => setDueOn(e.target.value)} />
              </label>
              <label className="span-2 tasks-drop-label">
                Görsel / PDF
                <span
                  className={`tasks-drop${files.length ? ' has-file' : ''}`}
                  onDragOver={(e) => {
                    e.preventDefault()
                    e.currentTarget.classList.add('is-drag')
                  }}
                  onDragLeave={(e) => e.currentTarget.classList.remove('is-drag')}
                  onDrop={(e) => {
                    e.preventDefault()
                    e.currentTarget.classList.remove('is-drag')
                    const next = Array.from(e.dataTransfer.files).filter(
                      (f) => f.type.startsWith('image/') || f.type === 'application/pdf' || /\.pdf$/i.test(f.name),
                    )
                    if (next.length) setFiles(next)
                  }}
                >
                  <input
                    type="file"
                    accept="image/*,.pdf"
                    multiple
                    onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
                  />
                  <strong>{files.length ? `${files.length} dosya seçildi` : 'Dosya ekleyin'}</strong>
                  <em>
                    {files.length
                      ? files.map((f) => f.name).join(', ')
                      : 'Görsel veya PDF sürükleyin, ya da seçmek için tıklayın'}
                  </em>
                </span>
              </label>
              <div className="form-actions span-2">
                <button type="button" className="btn-secondary" onClick={() => setCompose(false)}>
                  Vazgeç
                </button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </div>
            </div>
          </form>
        </div>
      ) : null}
    </div>
  )
}

async function openFile(taskId: string, attachmentId: string, fileName: string) {
  const token = getAccessToken()
  const res = await fetch(workTaskFileUrl(taskId, attachmentId), {
    credentials: 'include',
    headers: token ? { Authorization: `Bearer ${token}` } : undefined,
  })
  if (!res.ok) return
  const blob = await res.blob()
  const href = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = href
  a.target = '_blank'
  a.download = fileName
  a.click()
  window.setTimeout(() => URL.revokeObjectURL(href), 30_000)
}
