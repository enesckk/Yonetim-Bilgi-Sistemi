import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  createCatalogItem,
  deleteCatalogItem,
  fetchCatalogList,
  fetchCatalogsOverview,
  updateCatalogItem,
  type CatalogItem,
  type CatalogKind,
  type CatalogList,
  type CatalogsOverview,
} from '@/api/catalogsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useConfirm, useAlert } from '@/components/ConfirmDialog'

type Tab = CatalogKind
type FormMode = 'create' | 'edit' | null

const TABS: { id: Tab; label: string; singular: string }[] = [
  { id: 'job-duties', label: 'Fiili görevler', singular: 'Fiili görev' },
  { id: 'job-titles', label: 'Unvanlar', singular: 'Unvan' },
  { id: 'employment-types', label: 'İstihdam türleri', singular: 'İstihdam türü' },
  { id: 'facility-categories', label: 'Tesis türleri', singular: 'Tesis türü' },
]

const emptyForm = () => ({
  name: '',
  code: '',
  category: 2,
  sortOrder: 0,
  isActive: true,
})

type FormState = ReturnType<typeof emptyForm>

export function CatalogsPage() {
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const alert = useAlert()
  const canView =
    hasPermission(PermissionCodes.EmployeesView) ||
    hasPermission(PermissionCodes.OrganizationView) ||
    hasPermission(PermissionCodes.CatalogsManage)
  const canManage = hasPermission(PermissionCodes.CatalogsManage)

  const [tab, setTab] = useState<Tab>('job-duties')
  const [overview, setOverview] = useState<CatalogsOverview | null>(null)
  const [list, setList] = useState<CatalogList | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [categoryFilter, setCategoryFilter] = useState('')
  const [collapsed, setCollapsed] = useState<Record<number, boolean>>({})

  const [formMode, setFormMode] = useState<FormMode>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const loadOverview = useCallback(async () => {
    try {
      setOverview(await fetchCatalogsOverview())
    } catch {
      /* overview opsiyonel */
    }
  }, [])

  const loadList = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) setLoading(true)
    if (!opts?.silent) setError(null)
    try {
      setList(await fetchCatalogList(tab))
    } catch (err) {
      if (!opts?.silent) setList(null)
      setError(err instanceof ApiClientError ? err.message : 'Katalog yüklenemedi.')
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [tab])

  useEffect(() => {
    if (!canView) return
    void loadOverview()
  }, [canView, loadOverview])

  useEffect(() => {
    if (!canView) return
    void loadList()
  }, [canView, loadList])

  useEffect(() => {
    setSearch('')
    setStatusFilter('')
    setCategoryFilter('')
    setCollapsed({})
    setFormMode(null)
  }, [tab])

  const tabMeta = TABS.find((t) => t.id === tab)!
  const hasCategory = tab === 'job-duties'
  const hasCode = tab !== 'job-duties'
  const hasSort = tab === 'employment-types' || tab === 'facility-categories'

  const filteredGroups = useMemo(() => {
    if (!list) return []
    const q = search.trim().toLocaleLowerCase('tr-TR')
    const filterItem = (item: CatalogItem) => {
      if (statusFilter === 'active' && !item.isActive) return false
      if (statusFilter === 'passive' && item.isActive) return false
      if (categoryFilter && String(item.category) !== categoryFilter) return false
      if (!q) return true
      return (
        item.name.toLocaleLowerCase('tr-TR').includes(q) ||
        (item.code ?? '').toLocaleLowerCase('tr-TR').includes(q) ||
        (item.categoryLabel ?? '').toLocaleLowerCase('tr-TR').includes(q)
      )
    }

    if (hasCategory && list.groups.length > 0) {
      return list.groups
        .map((g) => ({ ...g, items: g.items.filter(filterItem) }))
        .filter((g) => g.items.length > 0)
    }

    const items = list.items.filter(filterItem)
    return items.length
      ? [{ category: 0, categoryLabel: tabMeta.label, items }]
      : []
  }, [list, search, statusFilter, categoryFilter, hasCategory, tabMeta.label])

  const searching = search.trim() !== '' || statusFilter !== '' || categoryFilter !== ''
  const isOpen = (category: number) => (searching || !hasCategory ? true : !collapsed[category])

  const openCreate = () => {
    setEditingId(null)
    setForm({
      ...emptyForm(),
      category: overview?.dutyCategories[0]?.value ?? 2,
      sortOrder: (list?.totalCount ?? 0) + 1,
    })
    setFormError(null)
    setFormMode('create')
  }

  const openEdit = (item: CatalogItem) => {
    setEditingId(item.id)
    setForm({
      name: item.name,
      code: item.code ?? '',
      category: item.category ?? 2,
      sortOrder: item.sortOrder ?? 0,
      isActive: item.isActive,
    })
    setFormError(null)
    setFormMode('edit')
  }

  const buildPayload = () => {
    const base: Record<string, unknown> = { name: form.name.trim() }
    if (hasCategory) base.category = form.category
    if (hasCode) base.code = form.code.trim() || null
    if (hasSort) base.sortOrder = Number(form.sortOrder) || 0
    if (formMode === 'edit') base.isActive = form.isActive
    return base
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!canManage) return
    setSaving(true)
    setFormError(null)
    try {
      const body = buildPayload()
      if (formMode === 'create') await createCatalogItem(tab, body)
      else if (editingId) await updateCatalogItem(tab, editingId, body)
      setFormMode(null)
      await loadList({ silent: true })
      await loadOverview()
    } catch (err) {
      setFormError(err instanceof ApiClientError ? err.message : 'Kayıt kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  const onToggleActive = async (item: CatalogItem) => {
    if (!canManage) return
    try {
      const body: Record<string, unknown> = {
        name: item.name,
        isActive: !item.isActive,
      }
      if (hasCategory) body.category = item.category
      if (hasCode) body.code = item.code ?? null
      if (hasSort) body.sortOrder = item.sortOrder ?? 0
      await updateCatalogItem(tab, item.id, body)
      await loadList({ silent: true })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Durum güncellenemedi.')
    }
  }

  const onDelete = async (item: CatalogItem) => {
    if (!canManage) return
    const ok = await confirm({
      title: 'Kaydı sil',
      message: `“${item.name}” silinsin mi? Kullanımdaysa silinemez.`,
      confirmLabel: 'Sil',
    })
    if (!ok) return
    try {
      await deleteCatalogItem(tab, item.id)
      await loadList({ silent: true })
      await loadOverview()
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Silinemedi.',
        tone: 'warning',
      })
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Kataloglar ekranı için yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="org-page catalog-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Tanım yönetimi</p>
          <h1>Kataloglar</h1>
          <p className="muted">
            Fiili görev, unvan, istihdam ve tesis türü tanımlarını tek yerden yönetin. Form ve
            filtreler buradaki kayıtlardan beslenir.
          </p>
        </div>
        <div className="report-hero-side">
          {overview && (
            <div className="report-hero-stats">
              <div>
                <strong>{overview.jobDutyCount}</strong>
                <span>Görev</span>
              </div>
              <div>
                <strong>{overview.jobTitleCount}</strong>
                <span>Unvan</span>
              </div>
              <div>
                <strong>{overview.employmentTypeCount}</strong>
                <span>İstihdam</span>
              </div>
              <div>
                <strong>{overview.facilityCategoryCount}</strong>
                <span>Tesis türü</span>
              </div>
            </div>
          )}
          {canManage && (
            <button type="button" className="btn-primary" onClick={openCreate}>
              + {tabMeta.singular} ekle
            </button>
          )}
        </div>
      </header>

      <div className="org-toolbar panel">
        <div className="org-views" role="tablist" aria-label="Katalog türleri">
          {TABS.map((t) => (
            <button
              key={t.id}
              type="button"
              role="tab"
              aria-selected={tab === t.id}
              className={tab === t.id ? 'is-active' : undefined}
              onClick={() => setTab(t.id)}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      <div className="org-toolbar panel skills-toolbar">
        <div className="skills-toolbar-left">
          <strong>{tabMeta.label}</strong>
          {list && (
            <span className="muted small">
              {list.activeCount} aktif · {list.inUseCount} kullanımda
            </span>
          )}
          {hasCategory && filteredGroups.length > 0 && !searching && (
            <span className="skills-toolbar-expand">
              <button type="button" className="skills-link-btn" onClick={() => setCollapsed({})}>
                Tümünü aç
              </button>
              <span aria-hidden>·</span>
              <button
                type="button"
                className="skills-link-btn"
                onClick={() =>
                  setCollapsed(Object.fromEntries(filteredGroups.map((g) => [g.category, true])))
                }
              >
                Tümünü kapat
              </button>
            </span>
          )}
        </div>
        <div className="skills-toolbar-filters">
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Ara…"
          />
          {hasCategory && (
            <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
              <option value="">Tüm kategoriler</option>
              {(overview?.dutyCategories ?? []).map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </select>
          )}
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">Tüm durumlar</option>
            <option value="active">Aktif</option>
            <option value="passive">Pasif</option>
          </select>
        </div>
      </div>

      {error && (
        <div className="panel">
          <div className="form-error">{error}</div>
        </div>
      )}

      <section className="panel dq-section">
        {loading || !list ? (
          <p className="muted">Yükleniyor…</p>
        ) : filteredGroups.length === 0 ? (
          <div className="notif-empty">
            <strong>Kayıt yok</strong>
            <p className="muted">Filtreye uyan tanım bulunamadı.</p>
          </div>
        ) : (
          <div className="skill-accordion">
            {filteredGroups.map((group) => {
              const open = isOpen(group.category)
              return (
                <div key={group.category} className={`skill-cat${open ? ' is-open' : ''}`}>
                  {hasCategory && (
                    <button
                      type="button"
                      className="skill-cat-head"
                      onClick={() =>
                        setCollapsed((c) => ({ ...c, [group.category]: !c[group.category] }))
                      }
                    >
                      <span className="skill-cat-chevron" aria-hidden>
                        ›
                      </span>
                      <span className="skill-cat-title">{group.categoryLabel}</span>
                      <span className="skill-cat-meta">
                        <strong>{group.items.length}</strong> tanım
                      </span>
                    </button>
                  )}
                  {open && (
                    <div className="skill-rows catalog-rows">
                      <div className="skill-col-head catalog-col-head" aria-hidden>
                        <span>Durum</span>
                        <span>Ad</span>
                        <span>{hasCode ? 'Kod' : 'Kategori'}</span>
                        <span>Kullanım</span>
                        <span className="skill-col-actions">İşlem</span>
                      </div>
                      {group.items.map((item) => (
                        <div
                          key={item.id}
                          className={`skill-row catalog-row${!item.isActive ? ' is-passive' : ''}`}
                        >
                          <span className={`catalog-dot${item.isActive ? '' : ' is-off'}`} />
                          <span className="skill-row-name">{item.name}</span>
                          <span className="muted small catalog-meta-cell">
                            {hasCode ? item.code || '—' : item.categoryLabel || '—'}
                          </span>
                          <span className="catalog-usage">{item.usageCount}</span>
                          <span className="skill-col-actions catalog-actions">
                            {canManage ? (
                              <div className="catalog-row-actions">
                                <button
                                  type="button"
                                  className="skills-link-btn"
                                  onClick={() => openEdit(item)}
                                >
                                  Düzenle
                                </button>
                                <button
                                  type="button"
                                  className="skills-link-btn"
                                  onClick={() => void onToggleActive(item)}
                                >
                                  {item.isActive ? 'Pasife al' : 'Aktifleştir'}
                                </button>
                                <button
                                  type="button"
                                  className="skills-link-btn catalog-danger"
                                  onClick={() => void onDelete(item)}
                                >
                                  Sil
                                </button>
                              </div>
                            ) : (
                              <span className="muted small">—</span>
                            )}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </section>

      {formMode && (
        <div className="org-modal-backdrop" onClick={() => setFormMode(null)}>
          <div
            className="org-modal panel"
            role="dialog"
            aria-modal="true"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="org-modal-head">
              <div>
                <p className="org-modal-eyebrow">Katalog</p>
                <h2>
                  {formMode === 'create'
                    ? `${tabMeta.singular} ekle`
                    : `${tabMeta.singular} düzenle`}
                </h2>
              </div>
            </div>
            <form className="org-form" onSubmit={(e) => void onSubmit(e)}>
              {formError && <div className="form-error">{formError}</div>}
              <label>
                Ad
                <input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  required
                  maxLength={200}
                  autoFocus
                />
              </label>
              {hasCategory && (
                <label>
                  Kategori
                  <select
                    value={form.category}
                    onChange={(e) => setForm((f) => ({ ...f, category: Number(e.target.value) }))}
                  >
                    {(overview?.dutyCategories ?? []).map((c) => (
                      <option key={c.value} value={c.value}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {hasCode && (
                <label>
                  Kod
                  <input
                    value={form.code}
                    onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
                    maxLength={50}
                    placeholder="İsteğe bağlı"
                  />
                </label>
              )}
              {hasSort && (
                <label>
                  Sıra
                  <input
                    type="number"
                    min={0}
                    max={9999}
                    value={form.sortOrder}
                    onChange={(e) =>
                      setForm((f) => ({ ...f, sortOrder: Number(e.target.value) || 0 }))
                    }
                  />
                </label>
              )}
              {formMode === 'edit' && (
                <label className="checkbox-row">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))}
                  />
                  Aktif
                </label>
              )}
              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setFormMode(null)}>
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
