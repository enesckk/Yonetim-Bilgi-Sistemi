import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  CERTIFICATE_CATEGORIES,
  createCertificateDefinition,
  deleteCertificateDefinition,
  fetchCertificateCatalog,
  fetchCertificateCatalogDetail,
  updateCertificateDefinition,
  type CertificateCatalog,
  type CertificateCatalogDetail,
  type CertificateCatalogItem,
} from '@/api/certificatesApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useConfirm, useAlert } from '@/components/ConfirmDialog'

type FormMode = 'create' | 'edit' | null

const emptyForm = () => ({
  name: '',
  category: 'Mesleki',
  isActive: true,
})

type FormState = ReturnType<typeof emptyForm>

function formatDate(value?: string | null): string {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return d.toLocaleDateString('tr-TR')
}

export function CertificatesPage() {
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const alert = useAlert()
  const canView = hasPermission(PermissionCodes.EmployeesView)
  const canManage = hasPermission(PermissionCodes.CertificatesManage)

  const [catalog, setCatalog] = useState<CertificateCatalog | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [search, setSearch] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [expiryFilter, setExpiryFilter] = useState('')

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<CertificateCatalogDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({})

  const [formMode, setFormMode] = useState<FormMode>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const loadCatalog = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) setLoading(true)
    if (!opts?.silent) setError(null)
    try {
      setCatalog(await fetchCertificateCatalog())
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Sertifikalar yüklenemedi.')
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
        const d = await fetchCertificateCatalogDetail(selectedId)
        if (!cancelled) setDetail(d)
      } catch (err) {
        if (!cancelled) {
          setDetail(null)
          setError(err instanceof ApiClientError ? err.message : 'Sertifika detayı yüklenemedi.')
        }
      } finally {
        if (!cancelled) setDetailLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [selectedId])

  const categoryOptions = useMemo(() => {
    const fromCatalog = catalog?.categories.map((c) => c.category) ?? []
    return [...new Set([...CERTIFICATE_CATEGORIES, ...fromCatalog])]
  }, [catalog])

  const filteredCategories = useMemo(() => {
    if (!catalog) return []
    const q = search.trim().toLowerCase()
    return catalog.categories
      .filter((group) => !categoryFilter || group.category === categoryFilter)
      .map((group) => ({
        ...group,
        certificates: group.certificates.filter((c) => {
          if (statusFilter === 'active' && !c.isActive) return false
          if (statusFilter === 'passive' && c.isActive) return false
          if (expiryFilter === 'expired' && c.expiredCount === 0) return false
          if (expiryFilter === 'expiring' && c.expiringSoonCount === 0) return false
          if (!q) return true
          return c.name.toLowerCase().includes(q)
        }),
      }))
      .filter((group) => group.certificates.length > 0)
  }, [catalog, search, categoryFilter, statusFilter, expiryFilter])

  const searching =
    search.trim() !== '' ||
    categoryFilter !== '' ||
    statusFilter !== '' ||
    expiryFilter !== ''
  const isOpen = (category: string) => (searching ? true : !collapsed[category])
  const toggleCategory = (category: string) =>
    setCollapsed((c) => ({ ...c, [category]: !c[category] }))

  const openCreate = () => {
    setEditingId(null)
    setForm(emptyForm())
    setFormError(null)
    window.setTimeout(() => setFormMode('create'), 0)
  }

  const openEdit = (item: CertificateCatalogItem) => {
    setEditingId(item.id)
    setForm({
      name: item.name,
      category: item.category || 'Mesleki',
      isActive: item.isActive,
    })
    setFormError(null)
    window.setTimeout(() => setFormMode('edit'), 0)
  }

  const onToggleActive = async (item: CertificateCatalogItem) => {
    try {
      await updateCertificateDefinition(item.id, {
        name: item.name,
        category: item.category,
        isActive: !item.isActive,
      })
      await loadCatalog({ silent: true })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Durum güncellenemedi.')
    }
  }

  const onDelete = async (item: CertificateCatalogItem) => {
    if (item.employeeCount > 0) {
      await alert({
        title: 'Silinemedi',
        message: 'Bu sertifika personellere atanmış. Silmek yerine pasife alabilirsiniz.',
        tone: 'warning',
      })
      return
    }
    const ok = await confirm({
      title: 'Sertifika tanımını sil',
      message: `“${item.name}” sertifika tanımı silinsin mi?`,
      confirmLabel: 'Sil',
    })
    if (!ok) return
    try {
      await deleteCertificateDefinition(item.id)
      if (selectedId === item.id) setSelectedId(null)
      await loadCatalog({ silent: true })
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Sertifika silinemedi.',
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
        category: form.category.trim() || null,
        isActive: form.isActive,
      }
      if (formMode === 'edit' && editingId) {
        await updateCertificateDefinition(editingId, payload)
      } else {
        await createCertificateDefinition(payload)
      }
      setFormMode(null)
      await loadCatalog({ silent: true })
      if (selectedId) {
        const d = await fetchCertificateCatalogDetail(selectedId).catch(() => null)
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
        <p className="form-error">Sertifikalar ekranı için yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="org-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Sertifika yönetimi</p>
          <h1>Sertifikalar</h1>
          <p className="muted">
            Sertifika kataloğunu yönetin; atanan personelleri, süresi dolan ve yaklaşan
            belgeleri tek bakışta izleyin.
          </p>
        </div>
        <div className="report-hero-side">
          {catalog && (
            <div className="report-hero-stats">
              <div>
                <strong>{catalog.totalDefinitions}</strong>
                <span>Tanım</span>
              </div>
              <div>
                <strong>{catalog.totalAssignments}</strong>
                <span>Atama</span>
              </div>
              <div>
                <strong className={catalog.expiringSoonCount > 0 ? 'is-warn' : undefined}>
                  {catalog.expiringSoonCount}
                </strong>
                <span>Yaklaşan</span>
              </div>
              <div>
                <strong className={catalog.expiredCount > 0 ? 'is-danger' : undefined}>
                  {catalog.expiredCount}
                </strong>
                <span>Dolmuş</span>
              </div>
            </div>
          )}
          {canManage && (
            <button type="button" className="btn-primary" onClick={openCreate}>
              + Sertifika ekle
            </button>
          )}
        </div>
      </header>

      <div className="org-toolbar panel skills-toolbar">
        <div className="skills-toolbar-left">
          <strong>Sertifika kataloğu</strong>
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
            placeholder="Sertifika adı ara…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
            <option value="">Tüm kategoriler</option>
            {categoryOptions.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">Tüm durumlar</option>
            <option value="active">Aktif</option>
            <option value="passive">Pasif</option>
          </select>
          <select value={expiryFilter} onChange={(e) => setExpiryFilter(e.target.value)}>
            <option value="">Tüm süreler</option>
            <option value="expiring">Süresi yaklaşan var</option>
            <option value="expired">Süresi dolmuş var</option>
          </select>
        </div>
      </div>

      {error && <div className="form-error panel">{error}</div>}

      {loading ? (
        <div className="panel org-loading">
          <p className="muted">Sertifikalar yükleniyor…</p>
        </div>
      ) : (
        <div className="org-workspace">
          <section className="org-main panel skills-main">
            {filteredCategories.length === 0 ? (
              <div className="skills-empty">
                <h3>Sertifika bulunamadı</h3>
                <p className="muted">Filtreleri temizleyin veya yeni bir tanım ekleyin.</p>
              </div>
            ) : (
              <div className="skill-accordion">
                <div className="skill-col-head cert-col-head" aria-hidden>
                  <span className="skill-col-name">Sertifika</span>
                  <span className="skill-col-num">Personel</span>
                  <span className="skill-col-num">Yaklaşan</span>
                  <span className="skill-col-num">Dolmuş</span>
                  <span className="skill-col-levels">Geçerli</span>
                  <span className="skill-col-actions">İşlem</span>
                </div>

                {filteredCategories.map((group) => {
                  const open = isOpen(group.category)
                  const totalPeople = group.certificates.reduce((s, c) => s + c.employeeCount, 0)
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
                            <strong>{group.certificates.length}</strong> tanım
                          </span>
                          <span>
                            <strong>{totalPeople}</strong> personel
                          </span>
                        </span>
                      </button>

                      {open && (
                        <div className="skill-rows">
                          {group.certificates.map((c) => (
                            <div
                              key={c.id}
                              className={`skill-row cert-row${selectedId === c.id ? ' is-selected' : ''}${
                                c.isActive ? '' : ' is-passive'
                              }`}
                              role="button"
                              tabIndex={0}
                              onClick={() => setSelectedId(c.id)}
                              onKeyDown={(e) => {
                                if (e.key === 'Enter' || e.key === ' ') {
                                  e.preventDefault()
                                  setSelectedId(c.id)
                                }
                              }}
                            >
                              <div className="skill-col-name skill-row-main">
                                <span
                                  className={`skill-dot${c.isActive ? '' : ' is-passive'}${
                                    c.expiredCount > 0
                                      ? ' is-danger'
                                      : c.expiringSoonCount > 0
                                        ? ' is-warn'
                                        : ''
                                  }`}
                                  aria-hidden
                                />
                                <span className="skill-row-name">{c.name}</span>
                                {!c.isActive && <span className="skill-tag-passive">Pasif</span>}
                              </div>

                              <span className="skill-col-num">
                                <strong>{c.employeeCount}</strong>
                              </span>
                              <span className="skill-col-num">
                                <strong className={c.expiringSoonCount > 0 ? 'is-warn' : undefined}>
                                  {c.expiringSoonCount}
                                </strong>
                              </span>
                              <span className="skill-col-num">
                                <strong className={c.expiredCount > 0 ? 'is-danger' : undefined}>
                                  {c.expiredCount}
                                </strong>
                              </span>
                              <div className="skill-col-levels">
                                {c.employeeCount === 0 ? (
                                  <span className="skill-chip empty">Atama yok</span>
                                ) : (
                                  <span className="skill-chip">Geçerli {c.validCount}</span>
                                )}
                              </div>

                              <div
                                className="skill-col-actions"
                                onClick={(e) => e.stopPropagation()}
                              >
                                <button
                                  type="button"
                                  className="org-card-btn primary"
                                  onClick={() => setSelectedId(c.id)}
                                >
                                  Detay
                                </button>
                                {canManage && (
                                  <div className="skill-row-more">
                                    <button
                                      type="button"
                                      className="org-card-btn"
                                      onClick={() => openEdit(c)}
                                    >
                                      Düzenle
                                    </button>
                                    <button
                                      type="button"
                                      className="org-card-btn"
                                      onClick={() => void onToggleActive(c)}
                                    >
                                      {c.isActive ? 'Pasife al' : 'Aktifleştir'}
                                    </button>
                                    <button
                                      type="button"
                                      className="org-card-btn danger"
                                      onClick={() => void onDelete(c)}
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
              <aside
                className="org-detail panel org-drawer"
                role="dialog"
                aria-label="Sertifika detayı"
              >
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
                        <strong className={detail.expiringSoonCount > 0 ? 'is-warn' : undefined}>
                          {detail.expiringSoonCount}
                        </strong>{' '}
                        yaklaşan
                      </span>
                      <span className="skill-detail-stat">
                        <strong className={detail.expiredCount > 0 ? 'is-danger' : undefined}>
                          {detail.expiredCount}
                        </strong>{' '}
                        dolmuş
                      </span>
                    </div>

                    <div className="skill-detail-block">
                      <h3 className="skill-detail-title">
                        Personeller
                        <span className="muted small"> ({detail.employees.length})</span>
                      </h3>
                      {detail.employees.length === 0 ? (
                        <p className="muted small">
                          Henüz atama yok. Personel detayındaki Sertifikalar bölümünden eklenir.
                        </p>
                      ) : (
                        <ul className="skill-employee-list">
                          {detail.employees.map((e) => (
                            <li key={e.id}>
                              <Link to={`/employees/${e.employeeId}`}>
                                <strong>{e.fullName}</strong>
                                <span className="muted small">
                                  {[e.jobTitleName, e.unitName].filter(Boolean).join(' · ') ||
                                    'Görev bilgisi yok'}
                                  {e.issuer ? ` · ${e.issuer}` : ''}
                                </span>
                                <span className="muted small">
                                  Veriliş: {formatDate(e.issuedOn)} · Bitiş:{' '}
                                  {formatDate(e.expiresOn)}
                                </span>
                              </Link>
                              <span className="skill-employee-meta">
                                <span
                                  className={`skill-chip cert-status-${e.expiryStatus}`}
                                >
                                  {e.expiryStatusLabel}
                                </span>
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
            aria-labelledby="cert-form-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="org-modal-head">
              <p className="org-modal-eyebrow">Sertifika</p>
              <h2 id="cert-form-title">
                {formMode === 'edit' ? 'Sertifikayı düzenle' : 'Yeni sertifika tanımı'}
              </h2>
            </div>
            {formError && <div className="form-error">{formError}</div>}
            <form className="form-grid" onSubmit={onSubmit}>
              <label className="span-2">
                Sertifika adı *
                <input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  required
                  maxLength={200}
                  placeholder="Örn. İlk Yardım"
                />
              </label>
              <label>
                Kategori
                <input
                  list="cert-category-list"
                  value={form.category}
                  onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))}
                  maxLength={100}
                  placeholder="Zorunlu / Mesleki / Dil…"
                />
                <datalist id="cert-category-list">
                  {categoryOptions.map((c) => (
                    <option key={c} value={c} />
                  ))}
                </datalist>
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
