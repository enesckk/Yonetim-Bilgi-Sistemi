import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  createSkill,
  deleteSkill,
  fetchSkillCatalog,
  fetchSkillCatalogDetail,
  updateSkill,
  SKILL_CATEGORIES,
  type SkillCatalog,
  type SkillCatalogDetail,
  type SkillCatalogItem,
} from '@/api/skillsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useConfirm, useAlert } from '@/components/ConfirmDialog'

type FormMode = 'create' | 'edit' | null

const emptyForm = () => ({
  name: '',
  category: 1,
  isActive: true,
})

type FormState = ReturnType<typeof emptyForm>

export function SkillsPage() {
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const alert = useAlert()
  const canView = hasPermission(PermissionCodes.EmployeesView)
  const canManage = hasPermission(PermissionCodes.SkillsManage)

  const [catalog, setCatalog] = useState<SkillCatalog | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [search, setSearch] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<SkillCatalogDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [collapsed, setCollapsed] = useState<Record<number, boolean>>({})

  const [formMode, setFormMode] = useState<FormMode>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const loadCatalog = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) setLoading(true)
    if (!opts?.silent) setError(null)
    try {
      const data = await fetchSkillCatalog()
      setCatalog(data)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Yetkinlikler yüklenemedi.')
      if (!opts?.silent) setCatalog(null)
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!canView) return
    void loadCatalog()
  }, [canView, loadCatalog])

  useEffect(() => {
    if (!selectedId) {
      setDetail(null)
      return
    }
    let cancelled = false
    setDetailLoading(true)
    ;(async () => {
      try {
        const d = await fetchSkillCatalogDetail(selectedId)
        if (!cancelled) setDetail(d)
      } catch (err) {
        if (!cancelled) {
          setDetail(null)
          setError(err instanceof ApiClientError ? err.message : 'Yetkinlik detayı yüklenemedi.')
        }
      } finally {
        if (!cancelled) setDetailLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [selectedId])

  const filteredCategories = useMemo(() => {
    if (!catalog) return []
    const q = search.trim().toLowerCase()
    return catalog.categories
      .filter((group) => !categoryFilter || String(group.category) === categoryFilter)
      .map((group) => ({
        ...group,
        skills: group.skills.filter((s) => {
          if (statusFilter === 'active' && !s.isActive) return false
          if (statusFilter === 'passive' && s.isActive) return false
          if (!q) return true
          return s.name.toLowerCase().includes(q)
        }),
      }))
      .filter((group) => group.skills.length > 0)
  }, [catalog, search, categoryFilter, statusFilter])

  // Arama/filtre etkinken tüm gruplar açık kalsın; kullanıcı elle kapatmadıysa.
  const searching = search.trim() !== '' || categoryFilter !== '' || statusFilter !== ''
  const isOpen = (category: number) => (searching ? true : !collapsed[category])
  const toggleCategory = (category: number) =>
    setCollapsed((c) => ({ ...c, [category]: !c[category] }))

  const openCreate = () => {
    setEditingId(null)
    setForm(emptyForm())
    setFormError(null)
    window.setTimeout(() => setFormMode('create'), 0)
  }

  const openEdit = (skill: SkillCatalogItem) => {
    setEditingId(skill.id)
    setForm({ name: skill.name, category: skill.category, isActive: skill.isActive })
    setFormError(null)
    window.setTimeout(() => setFormMode('edit'), 0)
  }

  const onToggleActive = async (skill: SkillCatalogItem) => {
    try {
      await updateSkill(skill.id, {
        name: skill.name,
        category: skill.category,
        isActive: !skill.isActive,
      })
      await loadCatalog({ silent: true })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Durum güncellenemedi.')
    }
  }

  const onDelete = async (skill: SkillCatalogItem) => {
    if (skill.employeeCount > 0) {
      await alert({
        title: 'Silinemedi',
        message: 'Bu yetkinlik personellere atanmış. Silmek yerine pasife alabilirsiniz.',
        tone: 'warning',
      })
      return
    }
    const ok = await confirm({
      title: 'Yetkinliği sil',
      message: `“${skill.name}” yetkinliği silinsin mi?`,
      confirmLabel: 'Sil',
    })
    if (!ok) return
    try {
      await deleteSkill(skill.id)
      if (selectedId === skill.id) setSelectedId(null)
      await loadCatalog({ silent: true })
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Yetkinlik silinemedi.',
        tone: 'warning',
      })
    }
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    setFormError(null)
    try {
      const payload = {
        name: form.name.trim(),
        category: form.category,
        isActive: form.isActive,
      }
      if (formMode === 'edit' && editingId) {
        await updateSkill(editingId, payload)
      } else {
        await createSkill(payload)
      }
      setFormMode(null)
      await loadCatalog({ silent: true })
      if (selectedId) {
        const d = await fetchSkillCatalogDetail(selectedId).catch(() => null)
        setDetail(d)
      }
    } catch (err) {
      setFormError(err instanceof ApiClientError ? err.message : 'Kayıt kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Yetkinlikler ekranı için yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="org-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Yetkinlik yönetimi</p>
          <h1>Yetkinlikler</h1>
          <p className="muted">
            Kurumdaki yetkinlik kataloğunu yönetin; hangi yetkinliğe kaç personelin sahip olduğunu
            ve seviye dağılımını izleyin.
          </p>
        </div>
        <div className="report-hero-side">
          {catalog && (
            <div className="report-hero-stats">
              <div>
                <strong>{catalog.totalSkills}</strong>
                <span>Yetkinlik</span>
              </div>
              <div>
                <strong>{catalog.activeSkills}</strong>
                <span>Aktif</span>
              </div>
              <div>
                <strong>{catalog.totalAssignments}</strong>
                <span>Atama</span>
              </div>
              <div>
                <strong>{catalog.employeesWithoutSkills}</strong>
                <span>Girilmemiş</span>
              </div>
            </div>
          )}
          {canManage && (
            <button type="button" className="btn-primary" onClick={openCreate}>
              + Yetkinlik ekle
            </button>
          )}
        </div>
      </header>

      <div className="org-toolbar panel skills-toolbar">
        <div className="skills-toolbar-left">
          <strong>Yetkinlik kataloğu</strong>
          {filteredCategories.length > 0 && !searching && (
            <span className="skills-toolbar-expand">
              <button type="button" className="skills-link-btn" onClick={() => setCollapsed({})}>
                Tümünü aç
              </button>
              <span aria-hidden>·</span>
              <button
                type="button"
                className="skills-link-btn"
                onClick={() =>
                  setCollapsed(
                    Object.fromEntries(filteredCategories.map((g) => [g.category, true])),
                  )
                }
              >
                Tümünü kapat
              </button>
            </span>
          )}
        </div>
        <div className="org-filters">
          <input
            type="search"
            placeholder="Yetkinlik adı ara…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
            <option value="">Tüm kategoriler</option>
            {SKILL_CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">Tüm durumlar</option>
            <option value="active">Aktif</option>
            <option value="passive">Pasif</option>
          </select>
        </div>
      </div>

      {error && <div className="form-error panel">{error}</div>}

      {loading ? (
        <div className="panel org-loading">
          <p className="muted">Yetkinlikler yükleniyor…</p>
        </div>
      ) : (
        <div className="org-workspace">
          <section className="org-main panel skills-main">
            {filteredCategories.length === 0 ? (
              <div className="skills-empty">
                <h3>Yetkinlik bulunamadı</h3>
                <p className="muted">Filtreleri temizleyin veya yeni bir yetkinlik ekleyin.</p>
              </div>
            ) : (
              <div className="skill-accordion">
                <div className="skill-col-head" aria-hidden>
                  <span className="skill-col-name">Yetkinlik</span>
                  <span className="skill-col-num">Personel</span>
                  <span className="skill-col-num">Sertifika</span>
                  <span className="skill-col-levels">Seviye dağılımı</span>
                  <span className="skill-col-actions">İşlem</span>
                </div>

                {filteredCategories.map((group) => {
                  const open = isOpen(group.category)
                  const totalPeople = group.skills.reduce((sum, s) => sum + s.employeeCount, 0)
                  return (
                    <section
                      key={group.category}
                      className={`skill-cat${open ? ' is-open' : ''}`}
                    >
                      <button
                        type="button"
                        className="skill-cat-head"
                        aria-expanded={open}
                        onClick={() => !searching && toggleCategory(group.category)}
                      >
                        <span className="skill-cat-chevron" aria-hidden>
                          ▸
                        </span>
                        <span className="skill-cat-title">{group.categoryLabel}</span>
                        <span className="skill-cat-meta">
                          <span>
                            <strong>{group.skills.length}</strong> yetkinlik
                          </span>
                          <span>
                            <strong>{totalPeople}</strong> personel
                          </span>
                        </span>
                      </button>

                      {open && (
                        <div className="skill-rows">
                          {group.skills.map((s) => (
                            <div
                              key={s.id}
                              className={`skill-row${selectedId === s.id ? ' is-selected' : ''}${
                                s.isActive ? '' : ' is-passive'
                              }`}
                              role="button"
                              tabIndex={0}
                              onClick={() => setSelectedId(s.id)}
                              onKeyDown={(e) => {
                                if (e.key === 'Enter' || e.key === ' ') {
                                  e.preventDefault()
                                  setSelectedId(s.id)
                                }
                              }}
                            >
                              <div className="skill-col-name skill-row-main">
                                <span
                                  className={`skill-dot${s.isActive ? '' : ' is-passive'}`}
                                  title={s.isActive ? 'Aktif' : 'Pasif'}
                                  aria-hidden
                                />
                                <span className="skill-row-name">{s.name}</span>
                                {!s.isActive && <span className="skill-tag-passive">Pasif</span>}
                              </div>

                              <span className="skill-col-num">
                                <strong>{s.employeeCount}</strong>
                              </span>
                              <span className="skill-col-num">
                                <strong>{s.certificateCount}</strong>
                              </span>

                              <div className="skill-col-levels">
                                {s.levelBreakdown.filter((l) => l.count > 0).length > 0 ? (
                                  s.levelBreakdown
                                    .filter((l) => l.count > 0)
                                    .map((l) => (
                                      <span key={l.label} className="skill-chip">
                                        {l.label} {l.count}
                                      </span>
                                    ))
                                ) : (
                                  <span className="skill-chip empty">Atama yok</span>
                                )}
                              </div>

                              <div
                                className="skill-col-actions"
                                onClick={(e) => e.stopPropagation()}
                              >
                                <button
                                  type="button"
                                  className="org-card-btn primary"
                                  onClick={() => setSelectedId(s.id)}
                                >
                                  Detay
                                </button>
                                {canManage && (
                                  <div className="skill-row-more">
                                    <button
                                      type="button"
                                      className="org-card-btn"
                                      onClick={() => openEdit(s)}
                                    >
                                      Düzenle
                                    </button>
                                    <button
                                      type="button"
                                      className="org-card-btn"
                                      onClick={() => void onToggleActive(s)}
                                    >
                                      {s.isActive ? 'Pasife al' : 'Aktifleştir'}
                                    </button>
                                    <button
                                      type="button"
                                      className="org-card-btn danger"
                                      onClick={() => void onDelete(s)}
                                    >
                                      Sil
                                    </button>
                                  </div>
                                )}
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </section>
                  )
                })}
              </div>
            )}
          </section>

          {selectedId && (
            <>
              <button
                type="button"
                className="org-drawer-backdrop"
                aria-label="Detayı kapat"
                onClick={() => setSelectedId(null)}
              />
              <aside className="org-detail panel org-drawer" role="dialog" aria-label="Yetkinlik detayı">
                {detailLoading && (
                  <>
                    <div className="org-detail-head">
                      <h2>Detay</h2>
                      <button
                        type="button"
                        className="btn-secondary"
                        onClick={() => setSelectedId(null)}
                      >
                        Kapat
                      </button>
                    </div>
                    <p className="muted">Detay yükleniyor…</p>
                  </>
                )}
                {!detailLoading && detail && (
                  <>
                    <div className="org-detail-head">
                      <div>
                        <p className="org-eyebrow">{detail.categoryLabel}</p>
                        <h2>{detail.name}</h2>
                      </div>
                      <button
                        type="button"
                        className="btn-secondary"
                        onClick={() => setSelectedId(null)}
                      >
                        Kapat
                      </button>
                    </div>

                    <div className="skill-detail-summary">
                      <span className={`org-status-pill status-${detail.isActive ? 1 : 2}`}>
                        {detail.isActive ? 'Aktif' : 'Pasif'}
                      </span>
                      <span className="skill-detail-stat">
                        <strong>{detail.employeeCount}</strong> personel
                      </span>
                      <span className="skill-detail-stat">
                        <strong>{detail.certificateCount}</strong> sertifikalı
                      </span>
                    </div>

                    <div className="skill-detail-block">
                      <h3 className="skill-detail-title">Seviye dağılımı</h3>
                      <ul className="org-stat-list">
                        {detail.levelBreakdown.map((l) => (
                          <li key={l.label}>
                            <span>{l.label}</span>
                            <strong>{l.count}</strong>
                          </li>
                        ))}
                      </ul>
                    </div>

                    <div className="skill-detail-block">
                      <h3 className="skill-detail-title">
                        Personeller
                        <span className="muted small"> ({detail.employees.length})</span>
                      </h3>
                      {detail.employees.length === 0 ? (
                        <p className="muted small">
                          Henüz atama yok. Personel detayındaki Yetkinlikler sekmesinden eklenir.
                        </p>
                      ) : (
                        <ul className="skill-employee-list">
                          {detail.employees.map((e) => (
                            <li key={e.id}>
                              <Link to={`/employees/${e.id}`}>
                                <strong>{e.fullName}</strong>
                                <span className="muted small">
                                  {[e.jobTitleName, e.unitName].filter(Boolean).join(' · ') ||
                                    'Görev bilgisi yok'}
                                </span>
                              </Link>
                              <span className="skill-employee-meta">
                                <span className="skill-chip">{e.levelLabel}</span>
                                {e.hasCertificate && (
                                  <span className="skill-chip cert">Sertifikalı</span>
                                )}
                              </span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </div>
                  </>
                )}
              </aside>
            </>
          )}
        </div>
      )}

      {formMode && (
        <div className="org-modal-backdrop" role="presentation" onClick={() => setFormMode(null)}>
          <div
            className="org-modal panel"
            role="dialog"
            aria-modal="true"
            aria-labelledby="skill-form-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="org-modal-head">
              <p className="org-modal-eyebrow">Yetkinlik</p>
              <h2 id="skill-form-title">
                {formMode === 'edit' ? 'Yetkinliği düzenle' : 'Yeni yetkinlik'}
              </h2>
            </div>
            {formError && <div className="form-error">{formError}</div>}
            <form className="form-grid" onSubmit={onSubmit}>
              <label className="span-2">
                Yetkinlik adı *
                <input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  required
                  maxLength={200}
                  placeholder="Örn. Robotik kodlama"
                />
              </label>
              <label>
                Kategori *
                <select
                  value={form.category}
                  onChange={(e) => setForm((f) => ({ ...f, category: Number(e.target.value) }))}
                >
                  {SKILL_CATEGORIES.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Durum
                <select
                  value={form.isActive ? '1' : '0'}
                  onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === '1' }))}
                >
                  <option value="1">Aktif</option>
                  <option value="0">Pasif</option>
                </select>
              </label>
              <div className="form-actions span-2">
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setFormMode(null)}
                  disabled={saving}
                >
                  Vazgeç
                </button>
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Kaydediliyor…' : 'Kaydet'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
