import { useCallback, useEffect, useMemo, useState, type Dispatch, type FormEvent, type SetStateAction } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  createOrganizationUnit,
  deleteOrganizationUnit,
  fetchOrganizationFormOptions,
  fetchOrganizationTree,
  fetchOrganizationUnitDetail,
  updateOrganizationUnit,
  type OrgFormOptions,
  type OrgNode,
  type OrgUnitDetail,
  type UpsertOrgUnitPayload,
} from '@/api/organizationApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useConfirm, useAlert } from '@/components/ConfirmDialog'
import { OrgSchemeChart } from './organization/OrgSchemeChart'
import { flattenOrg } from './organization/orgChartLayout'

type ViewId = 'hierarchy' | 'cards' | 'personnel'
type FormMode = 'create' | 'edit' | null
type OrganizationSection = 'units' | 'facilities' | 'chart'
type UnitLayout = 'cards' | 'tree'

const emptyForm = () => ({
  name: '',
  code: '',
  type: 4,
  status: 1,
  parentId: '',
  description: '',
  phone: '',
  email: '',
  facilityCategoryId: '',
  address: '',
  capacity: '',
  workingHours: '',
  idealStaffCount: '',
  managerEmployeeId: '',
  openedOn: '',
  closedOn: '',
})

type FormState = ReturnType<typeof emptyForm>

function emptyToNull(v: string): string | null {
  const t = v.trim()
  return t === '' ? null : t
}

function amirTitle(isFacility: boolean) {
  return isFacility ? 'Tesis amiri' : 'Birim amiri'
}

function amirText(isFacility: boolean, name?: string | null) {
  return name ? `${amirTitle(isFacility)}: ${name}` : 'Amir yok'
}

function orgById(flat: OrgNode[]) {
  return new Map(flat.map((n) => [n.id, n]))
}

function isKkmHall(n: OrgNode, byId: Map<string, OrgNode>) {
  if (n.type !== 6 || !n.parentId) return false
  return (byId.get(n.parentId)?.code ?? '').toUpperCase() === 'KKM'
}

function isYouthFacility(n: OrgNode, byId: Map<string, OrgNode>) {
  if (!n.parentId) return false
  return (byId.get(n.parentId)?.code ?? '').toUpperCase() === 'GENCLIK_KUT'
}

export function OrganizationPage({ section = 'chart' }: { section?: OrganizationSection }) {
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const alert = useAlert()
  const [searchParams] = useSearchParams()
  const canView = hasPermission(PermissionCodes.OrganizationView)
  const canManage = hasPermission(PermissionCodes.OrganizationManage)

  const [view, setView] = useState<ViewId>(section === 'chart' ? 'hierarchy' : 'cards')
  const [tree, setTree] = useState<OrgNode[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState('')
  const [unitFilter, setUnitFilter] = useState('')
  const [pdfBusy, setPdfBusy] = useState(false)

  const [selectedId, setSelectedId] = useState<string | null>(() => searchParams.get('unit'))
  const [detail, setDetail] = useState<OrgUnitDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  const [formMode, setFormMode] = useState<FormMode>(null)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [options, setOptions] = useState<OrgFormOptions | null>(null)
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [expandedPersonnel, setExpandedPersonnel] = useState<Record<string, boolean>>({})
  const [unitLayout, setUnitLayout] = useState<UnitLayout>('cards')

  const loadTree = useCallback(async (opts?: { silent?: boolean }) => {
    if (!opts?.silent) setLoading(true)
    if (!opts?.silent) setError(null)
    try {
      const data = await fetchOrganizationTree()
      setTree(data)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Organizasyon yüklenemedi.')
      if (!opts?.silent) setTree([])
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!canView) return
    void loadTree()
  }, [canView, loadTree])

  const loadOptions = useCallback(async () => {
    if (!canManage) return
    try {
      setOptions(await fetchOrganizationFormOptions())
    } catch {
      /* form açılınca yeniden dener */
    }
  }, [canManage])

  useEffect(() => {
    void loadOptions()
  }, [loadOptions])

  useEffect(() => {
    if (!selectedId) {
      setDetail(null)
      return
    }
    let cancelled = false
    setDetailLoading(true)
    ;(async () => {
      try {
        const d = await fetchOrganizationUnitDetail(selectedId)
        if (!cancelled) setDetail(d)
      } catch (err) {
        if (!cancelled) {
          setDetail(null)
          setError(err instanceof ApiClientError ? err.message : 'Birim detayı yüklenemedi.')
        }
      } finally {
        if (!cancelled) setDetailLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [selectedId])

  const flat = useMemo(() => flattenOrg(tree), [tree])
  const defaultUnitParentId = useMemo(
    () => flat.find((n) => n.type === 3)?.id ?? '',
    [flat],
  )
  const defaultFacilityParentId = useMemo(
    () => flat.find((n) => n.type === 5)?.id ?? flat.find((n) => n.type === 4)?.id ?? '',
    [flat],
  )

  const parentCandidates = useMemo(() => {
    // Üst birim listesini canlı ağaçtan üret — form-options bir kez yüklenince
    // yeni eklenen birimler listede kayboluyordu.
    const fromTree = flat
      .filter((n) => n.type !== 6)
      .map((n) => ({
        id: n.id,
        name: n.name,
        code: n.code,
        type: n.type,
        typeLabel: n.typeLabel,
      }))

    const fromApi = (options?.parentCandidates ?? []).map((p) => ({
      id: p.id,
      name: p.name,
      code: p.code,
      type: p.type ?? null,
      typeLabel: null as string | null,
    }))

    const byId = new Map<string, (typeof fromTree)[number]>()
    for (const p of fromApi) {
      byId.set(p.id, {
        id: p.id,
        name: p.name,
        code: p.code ?? null,
        type: p.type ?? 0,
        typeLabel: '',
      })
    }
    for (const p of fromTree) byId.set(p.id, p)

    // Seçili üst birim listede yoksa (eski kayıt / timing) yine de göster
    if (form.parentId && !byId.has(form.parentId)) {
      const orphan = flat.find((n) => n.id === form.parentId)
      if (orphan) {
        byId.set(orphan.id, {
          id: orphan.id,
          name: orphan.name,
          code: orphan.code,
          type: orphan.type,
          typeLabel: orphan.typeLabel,
        })
      } else if (options?.parentCandidates) {
        const opt = options.parentCandidates.find((p) => p.id === form.parentId)
        if (opt) {
          byId.set(opt.id, {
            id: opt.id,
            name: opt.name,
            code: opt.code ?? null,
            type: opt.type ?? 0,
            typeLabel: '',
          })
        }
      }
    }

    let list = [...byId.values()]

    if (formMode === 'edit' && editingId) {
      const blocked = collectDescendantIds(editingId, flat)
      blocked.add(editingId)
      list = list.filter((p) => !blocked.has(p.id))
    }

    if (form.type === 6) {
      list = list.filter((p) => p.type === 4 || p.type === 5)
    } else if (form.type === 5) {
      // Alt birim → ana birim (veya başka alt birim) altında
      list = list.filter((p) => p.type === 4 || p.type === 5)
    } else if (form.type === 4) {
      list = list.filter((p) => p.type === 3)
    } else if (form.type === 3) {
      list = list.filter((p) => p.type === 2)
    } else if (form.type === 2) {
      list = list.filter((p) => p.type === 1)
    }

    return list.sort((a, b) => a.name.localeCompare(b.name, 'tr'))
  }, [flat, form.parentId, form.type, formMode, editingId, options])

  const scopedFlat = useMemo(() => {
    const byId = orgById(flat)
    if (section === 'units') return flat.filter((n) => n.type === 4 || n.type === 5)
    if (section === 'facilities') return flat.filter((n) => n.type === 6 && !isKkmHall(n, byId))
    return flat
  }, [flat, section])

  const filteredFlat = useMemo(() => {
    const q = search.trim().toLowerCase()
    return scopedFlat.filter((n) => {
      if (statusFilter && String(n.status) !== statusFilter) return false
      if (section === 'facilities' && unitFilter && n.parentId !== unitFilter) return false
      if (!q) return true
      return (
        n.name.toLowerCase().includes(q) ||
        (n.code ?? '').toLowerCase().includes(q) ||
        (n.managerName ?? '').toLowerCase().includes(q) ||
        (n.parentName ?? '').toLowerCase().includes(q) ||
        n.typeLabel.toLowerCase().includes(q)
      )
    })
  }, [scopedFlat, search, statusFilter, section, unitFilter])

  const facilityUnitOptions = useMemo(() => {
    const parentIds = new Set(
      flat.filter((n) => n.type === 6 && n.parentId).map((n) => n.parentId as string),
    )
    return flat
      .filter((n) => n.type === 4 || n.type === 5 || parentIds.has(n.id))
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name, 'tr'))
  }, [flat])

  const sectionStats = useMemo(() => {
    const dash = (n: number) => (loading ? '—' : String(n))
    const units = flat.filter((n) => n.type === 4 || n.type === 5)
    const byId = orgById(flat)
    const facilities = flat.filter((n) => n.type === 6 && !isKkmHall(n, byId))
    const staffOf = (nodes: OrgNode[]) =>
      nodes.reduce((sum, n) => sum + (n.activeEmployeeCount ?? n.employeeCount ?? 0), 0)

    if (section === 'units') {
      return [
        { value: dash(units.length), label: 'Birim' },
        { value: dash(units.filter((n) => n.status === 1).length), label: 'Aktif' },
        { value: dash(units.filter((n) => Boolean(n.managerName)).length), label: 'Amiri var' },
        { value: dash(staffOf(units)), label: 'Personel' },
      ]
    }
    if (section === 'facilities') {
      return [
        { value: dash(facilities.length), label: 'Tesis' },
        { value: dash(facilities.filter((n) => n.status === 1).length), label: 'Aktif' },
        { value: dash(facilities.filter((n) => Boolean(n.managerName)).length), label: 'Amiri var' },
        { value: dash(facilities.filter((n) => n.status !== 1).length), label: 'Pasif' },
      ]
    }
    return [
      { value: dash(units.length), label: 'Birim' },
      { value: dash(facilities.length), label: 'Tesis' },
      { value: dash(staffOf(flat)), label: 'Personel' },
      {
        value: dash(
          units.filter((n) => Boolean(n.managerName)).length +
            facilities.filter((n) => Boolean(n.managerName)).length,
        ),
        label: 'Amir',
      },
    ]
  }, [flat, section, loading])

  const filteredTree = useMemo(() => {
    const source =
      section === 'units'
        ? mainAndSubUnitsTree(tree)
        : section === 'facilities'
          ? tree
          : tree
    if (!search.trim() && !statusFilter) return source
    const allowed = new Set(filteredFlat.map((n) => n.id))
    const byId = new Map(flat.map((n) => [n.id, n]))
    for (const n of filteredFlat) {
      let p = n.parentId
      while (p) {
        allowed.add(p)
        p = byId.get(p)?.parentId ?? null
      }
    }
    const prune = (nodes: OrgNode[]): OrgNode[] =>
      nodes
        .filter((n) => allowed.has(n.id))
        .map((n) => ({
          ...n,
          children: prune(
            section === 'units'
              ? (n.children ?? []).filter((c) => c.type !== 6)
              : (n.children ?? []),
          ),
        }))
    return prune(source)
  }, [tree, flat, filteredFlat, search, statusFilter, section])

  const [expandedUnits, setExpandedUnits] = useState<Record<string, boolean>>({})

  function nextChildType(parentType: number | undefined): number {
    // Birimler ekranında tesis açılmaz; tesisler ayrı menüde.
    // Alt birim altına da alt birim eklenebilir (iç içe).
    if (section === 'facilities') return 6
    if (parentType === 5) return 5
    if (parentType === 4) return 5
    if (parentType === 3) return 4
    if (parentType === 2) return 3
    if (parentType === 1) return 2
    return 4
  }

  useEffect(() => {
    setView(section === 'chart' ? 'hierarchy' : 'cards')
    setSelectedId(null)
    setExpandedUnits({})
    setUnitFilter('')
    setSearch('')
    setStatusFilter('')
  }, [section])

  useEffect(() => {
    if (section !== 'units' || tree.length === 0) return
    setExpandedUnits((prev) => {
      if (Object.keys(prev).length > 0) return prev
      const initial: Record<string, boolean> = {}
      for (const n of flattenOrg(stripFacilities(tree))) {
        if ((n.children ?? []).some((c) => c.type !== 6)) initial[n.id] = true
      }
      return initial
    })
  }, [section, tree])

  function openCreate(parentId?: string | null, type = 4) {
    setSelectedId(null)
    // Çekmece kapanırken aynı tıklama forma düşmesin diye bir tick beklenir.
    window.setTimeout(() => {
      setFormMode('create')
      setEditingId(null)
      setForm({
        ...emptyForm(),
        parentId: parentId ?? '',
        type,
      })
      setFormError(null)
      setFieldErrors({})
    }, 0)
  }

  async function downloadSchemePdf() {
    const element = document.getElementById('organization-scheme-export')
    if (!element) {
      setError('PDF oluşturmak için önce Yönetim hiyerarşisi görünümünü açın.')
      return
    }

    setPdfBusy(true)
    setError(null)
    const viewport = document.querySelector('.org-scheme-viewport')
    viewport?.classList.add('is-exporting')
    try {
      await new Promise<void>((resolve) => {
        window.requestAnimationFrame(() => window.requestAnimationFrame(() => resolve()))
      })
      const [{ default: html2canvas }, { jsPDF }] = await Promise.all([
        import('html2canvas'),
        import('jspdf'),
      ])
      const canvas = await html2canvas(element, {
        backgroundColor: '#ffffff',
        scale: 1.6,
        useCORS: true,
        logging: false,
        windowWidth: Math.max(element.scrollWidth, element.clientWidth),
        windowHeight: Math.max(element.scrollHeight, element.clientHeight),
      })

      const pdf = new jsPDF({
        orientation: 'landscape',
        unit: 'mm',
        format: 'a2',
        compress: true,
      })
      const pageWidth = pdf.internal.pageSize.getWidth()
      const pageHeight = pdf.internal.pageSize.getHeight()
      const margin = 7
      const usableWidth = pageWidth - margin * 2
      const usableHeight = pageHeight - margin * 2
      const ratio = Math.min(usableWidth / canvas.width, usableHeight / canvas.height)
      const width = canvas.width * ratio
      const height = canvas.height * ratio
      pdf.addImage(
        canvas.toDataURL('image/jpeg', 0.92),
        'JPEG',
        (pageWidth - width) / 2,
        (pageHeight - height) / 2,
        width,
        height,
        undefined,
        'FAST',
      )
      pdf.save(`organizasyon-semasi-${new Date().toISOString().slice(0, 10)}.pdf`)
    } catch {
      setError('Organizasyon şeması PDF olarak hazırlanamadı.')
    } finally {
      document.querySelector('.org-scheme-viewport')?.classList.remove('is-exporting')
      setPdfBusy(false)
    }
  }

  function openEdit(node: OrgNode) {
    setSelectedId(null)
    window.setTimeout(() => {
      setFormMode('edit')
      setEditingId(node.id)
      setForm({
        name: node.name,
        code: node.code ?? '',
        type: node.type,
        status: node.status,
        parentId: node.parentId ?? '',
        description: node.description ?? '',
        phone: node.phone ?? '',
        email: node.email ?? '',
        facilityCategoryId: node.facilityCategoryId ?? '',
        address: node.address ?? '',
        capacity: node.capacity != null ? String(node.capacity) : '',
        workingHours: node.workingHours ?? '',
        idealStaffCount: node.idealStaffCount != null ? String(node.idealStaffCount) : '',
        managerEmployeeId: node.managerEmployeeId ?? '',
        openedOn: node.openedOn ?? '',
        closedOn: node.closedOn ?? '',
      })
      setFormError(null)
      setFieldErrors({})
    }, 0)
  }

  function buildPayload(): UpsertOrgUnitPayload {
    const ideal = form.idealStaffCount.trim()
    return {
      name: form.name.trim(),
      code: emptyToNull(form.code),
      type: form.type,
      status: form.status,
      parentId: form.type === 1 ? null : emptyToNull(form.parentId),
      description: emptyToNull(form.description),
      phone: emptyToNull(form.phone),
      email: emptyToNull(form.email),
      facilityCategoryId: form.type === 6 ? emptyToNull(form.facilityCategoryId) : null,
      address: form.type === 6 ? emptyToNull(form.address) : null,
      capacity: form.type === 6 && form.capacity.trim() ? Number(form.capacity) : null,
      workingHours: form.type === 6 ? emptyToNull(form.workingHours) : null,
      idealStaffCount: ideal === '' ? null : Number(ideal),
      managerEmployeeId: emptyToNull(form.managerEmployeeId),
      openedOn: emptyToNull(form.openedOn),
      closedOn: emptyToNull(form.closedOn),
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setFormError(null)
    setFieldErrors({})
    try {
      const payload = buildPayload()
      let createdId: string | null = null
      if (formMode === 'edit' && editingId) {
        await updateOrganizationUnit(editingId, payload)
        createdId = editingId
      } else {
        const created = await createOrganizationUnit(payload)
        createdId = created.id
      }
      const parentId = form.parentId || null
      setFormMode(null)
      setEditingId(null)
      await loadTree({ silent: true })
      void loadOptions()
      if (parentId) {
        setExpandedUnits((prev) => ({ ...prev, [parentId]: true }))
      }
      if (createdId) {
        setSelectedId(createdId)
      } else if (selectedId) {
        const d = await fetchOrganizationUnitDetail(selectedId)
        setDetail(d)
      }
    } catch (err) {
      if (err instanceof ApiClientError) {
        setFormError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else {
        setFormError('Kayıt kaydedilemedi.')
      }
    } finally {
      setSaving(false)
    }
  }

  async function onDelete(node: OrgNode) {
    const ok = await confirm({
      title: isFacilityType(node.type) ? 'Tesisi sil' : 'Birimi sil',
      message: `“${node.name}” ${isFacilityType(node.type) ? 'tesisini' : 'birimini'} silmek istiyor musunuz?`,
      confirmLabel: 'Sil',
    })
    if (!ok) return
    try {
      await deleteOrganizationUnit(node.id)
      if (selectedId === node.id) {
        setSelectedId(null)
        setDetail(null)
      }
      await loadTree({ silent: true })
      void loadOptions()
    } catch (err) {
      await alert({
        title: 'Silinemedi',
        message: err instanceof ApiClientError ? err.message : 'Silinemedi.',
        tone: 'warning',
      })
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Organizasyon ekranı için yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className={`org-page${section === 'chart' ? ' is-chart-fit' : ''}`}>
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">
            {section === 'chart' ? 'Kurumsal yapı' : section === 'units' ? 'Birim yönetimi' : 'Tesis yönetimi'}
          </p>
          <h1>
            {section === 'chart'
              ? 'Organizasyon şeması'
              : section === 'units'
                ? 'Birimler'
                : 'Tesisler'}
          </h1>
          <p className="muted">
            {section === 'chart'
              ? 'Müdürlük şeması veritabanından üretilir. Birim, tesis, amir ve personel eklendikçe otomatik güncellenir.'
              : section === 'units'
                ? canManage
                  ? 'Ana ve alt birimleri, birim amirlerini ve kadro durumunu yönetin.'
                  : 'Ana ve alt birimleri, birim amirlerini ve kadro durumunu görün.'
                : canManage
                  ? 'Birimlere bağlı tesisleri, tesis amirlerini ve kadroyu yönetin.'
                  : 'Birimlere bağlı tesisleri, tesis amirlerini ve kadroyu görün.'}
          </p>
        </div>
        <div className="report-hero-side">
          <div className="report-hero-stats">
            {sectionStats.map((s) => (
              <div key={s.label}>
                <strong>{s.value}</strong>
                <span>{s.label}</span>
              </div>
            ))}
          </div>
          {canManage && section !== 'chart' && (
            <>
              {section === 'units' ? (
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => openCreate(defaultUnitParentId, 4)}
                >
                  + Birim ekle
                </button>
              ) : (
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => openCreate(defaultFacilityParentId, 6)}
                >
                  + Tesis ekle
                </button>
              )}
            </>
          )}
          {section === 'chart' && (
            <button
              type="button"
              className="btn-secondary"
              disabled={pdfBusy || view !== 'hierarchy'}
              title={view !== 'hierarchy' ? 'Önce Yönetim hiyerarşisi görünümünü açın' : undefined}
              onClick={() => void downloadSchemePdf()}
            >
              {pdfBusy ? 'PDF hazırlanıyor…' : 'Şemayı PDF indir'}
            </button>
          )}
        </div>
      </header>

      <div className="org-toolbar panel">
        {section === 'chart' ? (
          <div className="org-views" role="tablist" aria-label="Organizasyon görünümleri">
            {(
              [
                ['hierarchy', 'Yönetim hiyerarşisi'],
                ['personnel', 'Personel görünümü'],
              ] as const
            ).map(([id, label]) => (
              <button
                key={id}
                type="button"
                role="tab"
                className={view === id ? 'is-active' : ''}
                aria-selected={view === id}
                onClick={() => {
                  setView(id)
                  setSelectedId(null)
                }}
              >
                {label}
              </button>
            ))}
          </div>
        ) : section === 'units' ? (
          <div className="org-views" role="tablist" aria-label="Birim görünümleri">
            {(
              [
                ['cards', 'Kart görünümü'],
                ['tree', 'Hiyerarşik liste'],
              ] as const
            ).map(([id, label]) => (
              <button
                key={id}
                type="button"
                role="tab"
                className={unitLayout === id ? 'is-active' : ''}
                aria-selected={unitLayout === id}
                onClick={() => {
                  setUnitLayout(id)
                  setSelectedId(null)
                }}
              >
                {label}
              </button>
            ))}
          </div>
        ) : (
          <strong>Tesis listesi</strong>
        )}
        <div className="org-filters">
          <input
            type="search"
            placeholder={`${section === 'facilities' ? 'Tesis' : 'Birim'}, kod veya amir ara…`}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {section === 'facilities' && (
            <select value={unitFilter} onChange={(e) => setUnitFilter(e.target.value)}>
              <option value="">Tüm birimler</option>
              {facilityUnitOptions.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                  {u.code ? ` (${u.code})` : ''}
                </option>
              ))}
            </select>
          )}
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
            <option value="">Tüm durumlar</option>
            <option value="1">Aktif</option>
            <option value="2">Pasif</option>
            <option value="3">Kapalı</option>
            <option value="4">Tadilatta</option>
            <option value="5">Geçici kapalı</option>
            <option value="6">Planlama</option>
            <option value="7">Devredildi</option>
            <option value="8">Kullanım dışı</option>
          </select>
        </div>
      </div>

      {error && <div className="form-error panel">{error}</div>}

      {loading ? (
        <div className="panel org-loading">
          <div className="ui-skeleton-stack" aria-hidden="true">
            <span className="ui-skeleton" />
            <span className="ui-skeleton" />
            <span className="ui-skeleton" />
          </div>
          <p className="muted">Organizasyon yükleniyor…</p>
        </div>
      ) : (
        <div className="org-workspace">
          <section className="org-main panel">
            {view === 'hierarchy' && (
              <OrgSchemeChart
                tree={filteredTree}
                selectedId={selectedId}
                onSelect={(id) => setSelectedId(id)}
              />
            )}
            {view === 'cards' && section === 'units' && unitLayout === 'cards' && (
              <OrgCardsView
                nodes={filteredFlat}
                flat={flat}
                section={section}
                selectedId={selectedId}
                canManage={canManage}
                onSelect={setSelectedId}
                onEdit={openEdit}
                onAddChild={(id) => {
                  const parent = flat.find((n) => n.id === id)
                  openCreate(id, nextChildType(parent?.type))
                }}
                onDelete={onDelete}
              />
            )}
            {view === 'cards' && section === 'units' && unitLayout === 'tree' && (
              <OrgUnitsHierarchyView
                tree={filteredTree}
                flat={flat}
                selectedId={selectedId}
                expanded={expandedUnits}
                setExpanded={setExpandedUnits}
                canManage={canManage}
                onSelect={setSelectedId}
                onEdit={openEdit}
                onAddChild={(id) => {
                  const parent = flat.find((n) => n.id === id)
                  openCreate(id, nextChildType(parent?.type))
                }}
                onDelete={onDelete}
              />
            )}
            {view === 'cards' && section === 'facilities' && (
              <OrgFacilitiesGroupedView
                nodes={filteredFlat}
                flat={flat}
                selectedId={selectedId}
                canManage={canManage}
                onSelect={setSelectedId}
                onEdit={openEdit}
                onDelete={onDelete}
              />
            )}
            {view === 'cards' && section === 'chart' && (
              <OrgCardsView
                nodes={filteredFlat}
                section={section}
                selectedId={selectedId}
                canManage={canManage}
                onSelect={setSelectedId}
                onEdit={openEdit}
                onAddChild={(id) => {
                  const parent = flat.find((n) => n.id === id)
                  openCreate(id, nextChildType(parent?.type))
                }}
                onDelete={onDelete}
              />
            )}
            {view === 'personnel' && (
              <OrgPersonnelView
                nodes={filteredFlat.filter((n) => n.type >= 3)}
                expanded={expandedPersonnel}
                setExpanded={setExpandedPersonnel}
                selectedId={selectedId}
                onSelect={setSelectedId}
                detail={detail}
                detailLoading={detailLoading}
              />
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
              <aside className="org-detail panel org-drawer" role="dialog" aria-label="Kayıt detayı">
              {detailLoading && (
                <>
                  <div className="org-detail-head">
                    <h2>Detay</h2>
                    <button type="button" className="btn-secondary" onClick={() => setSelectedId(null)}>
                      Kapat
                    </button>
                  </div>
                  <p className="muted">Detay yükleniyor…</p>
                </>
              )}
              {!detailLoading && detail && (
                <UnitDetailPanel
                  detail={detail}
                  canManage={canManage}
                  onClose={() => setSelectedId(null)}
                  onEdit={() => {
                    const node = flat.find((n) => n.id === detail.id)
                    if (node) {
                      openEdit(node)
                      return
                    }
                    openEdit({
                      id: detail.id,
                      name: detail.name,
                      code: detail.code,
                      type: detail.type,
                      typeLabel: detail.typeLabel,
                      status: detail.status,
                      statusLabel: detail.statusLabel,
                      parentId: detail.parentId,
                      parentName: detail.parentName,
                      description: detail.description,
                      phone: detail.phone,
                      email: detail.email,
                      facilityCategoryId: detail.facilityCategoryId,
                      facilityCategoryName: detail.facilityCategoryName,
                      address: detail.address,
                      capacity: detail.capacity,
                      workingHours: detail.workingHours,
                      idealStaffCount: detail.idealStaffCount,
                      managerEmployeeId: detail.managerEmployeeId,
                      managerName: detail.managerName,
                      openedOn: detail.openedOn,
                      closedOn: detail.closedOn,
                      activeEmployeeCount: detail.activeEmployeeCount,
                      missingStaffCount: detail.missingStaffCount,
                      employeeCount: detail.activeEmployeeCount,
                      children: [],
                    })
                  }}
                  onAddChild={() => {
                    openCreate(detail.id, nextChildType(detail.type))
                  }}
                  onSelectChild={(id) => setSelectedId(id)}
                />
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
            aria-labelledby="org-form-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="org-modal-head">
              <p className="org-modal-eyebrow">{isFacilityType(form.type) ? 'Tesis' : 'Birim'}</p>
              <h2 id="org-form-title">
                {formMode === 'edit'
                  ? isFacilityType(form.type)
                    ? 'Tesisi düzenle'
                    : 'Birimi düzenle'
                  : isFacilityType(form.type)
                    ? 'Yeni tesis'
                    : 'Yeni birim'}
              </h2>
            </div>
            {formError && <div className="form-error">{formError}</div>}
            <form className="form-grid" onSubmit={onSubmit}>
              <label>
                {isFacilityType(form.type) ? 'Tesis adı *' : 'Birim adı *'}
                <input
                  value={form.name}
                  onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                  required
                  maxLength={200}
                />
                {fieldError('name')}
              </label>
              <label>
                Kod
                <input
                  value={form.code}
                  onChange={(e) => setForm((f) => ({ ...f, code: e.target.value }))}
                  maxLength={50}
                />
                {fieldError('code')}
              </label>
              {!isFacilityType(form.type) && (
                <label>
                  Birim türü *
                  <select
                    value={form.type}
                    disabled={formMode === 'edit'}
                    onChange={(e) => {
                      const type = Number(e.target.value)
                      setForm((f) => ({
                        ...f,
                        type,
                        parentId:
                          type === 1
                            ? ''
                            : type === 6
                              ? defaultFacilityParentId
                              : f.parentId || defaultUnitParentId,
                      }))
                    }}
                  >
                    {(options?.types ?? [])
                      .filter((t) => t.value !== 6)
                      .map((t) => (
                        <option key={t.value} value={t.value}>
                          {t.label}
                        </option>
                      ))}
                  </select>
                  {formMode === 'edit' ? (
                    <small className="muted">Birim türü oluşturulduktan sonra değiştirilemez.</small>
                  ) : null}
                  {fieldError('type')}
                </label>
              )}
              <label>
                Durum *
                <select
                  value={form.status}
                  onChange={(e) => setForm((f) => ({ ...f, status: Number(e.target.value) }))}
                >
                  {(options?.statuses ?? []).map((s) => (
                    <option key={s.value} value={s.value}>
                      {s.label}
                    </option>
                  ))}
                </select>
                {fieldError('status')}
              </label>
              {form.type !== 1 && (
                <label className="span-2">
                  {isFacilityType(form.type) ? 'Bağlı olduğu birim *' : 'Bağlı olduğu üst birim *'}
                  <select
                    value={form.parentId}
                    required
                    onChange={(e) => setForm((f) => ({ ...f, parentId: e.target.value }))}
                  >
                    <option value="">Seçiniz</option>
                    {parentCandidates.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                        {p.typeLabel ? ` — ${p.typeLabel}` : ''}
                        {p.code ? ` (${p.code})` : ''}
                      </option>
                    ))}
                  </select>
                  {fieldError('parentId')}
                </label>
              )}
              {isFacilityType(form.type) && (
                <>
                  <label>
                    Tesis türü
                    <select
                      value={form.facilityCategoryId}
                      onChange={(e) => setForm((f) => ({ ...f, facilityCategoryId: e.target.value }))}
                    >
                      <option value="">Seçiniz</option>
                      {(options?.facilityCategories ?? []).map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    {fieldError('facilityCategoryId')}
                  </label>
                  <label>
                    Fiziksel kapasite
                    <input
                      type="number"
                      min={0}
                      max={1000000}
                      value={form.capacity}
                      onChange={(e) => setForm((f) => ({ ...f, capacity: e.target.value }))}
                      placeholder="Örn. 250"
                    />
                    {fieldError('capacity')}
                  </label>
                  <label className="span-2">
                    Adres
                    <textarea
                      rows={2}
                      maxLength={500}
                      value={form.address}
                      onChange={(e) => setForm((f) => ({ ...f, address: e.target.value }))}
                    />
                    {fieldError('address')}
                  </label>
                  <label className="span-2">
                    Çalışma saatleri
                    <input
                      maxLength={250}
                      value={form.workingHours}
                      onChange={(e) => setForm((f) => ({ ...f, workingHours: e.target.value }))}
                      placeholder="Örn. Hafta içi 08:30–17:30"
                    />
                    {fieldError('workingHours')}
                  </label>
                </>
              )}
              <label className="span-2">
                {isFacilityType(form.type) ? 'Tesis amiri' : 'Birim amiri'}
                <select
                  value={form.managerEmployeeId}
                  onChange={(e) => setForm((f) => ({ ...f, managerEmployeeId: e.target.value }))}
                >
                  <option value="">Seçiniz</option>
                  {(options?.managers ?? []).map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.name}
                      {m.code ? ` · ${m.code}` : ''}
                    </option>
                  ))}
                </select>
                {fieldError('managerEmployeeId')}
              </label>
              <label>
                Telefon
                <input
                  value={form.phone}
                  onChange={(e) => setForm((f) => ({ ...f, phone: e.target.value }))}
                />
                {fieldError('phone')}
              </label>
              <label>
                E-posta
                <input
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
                />
                {fieldError('email')}
              </label>
              <label>
                İdeal personel sayısı
                <input
                  type="number"
                  min={0}
                  value={form.idealStaffCount}
                  onChange={(e) => setForm((f) => ({ ...f, idealStaffCount: e.target.value }))}
                />
                {fieldError('idealStaffCount')}
              </label>
              <label>
                Açılış tarihi
                <input
                  type="date"
                  value={form.openedOn}
                  onChange={(e) => setForm((f) => ({ ...f, openedOn: e.target.value }))}
                />
              </label>
              <label>
                Kapanış tarihi
                <input
                  type="date"
                  value={form.closedOn}
                  onChange={(e) => setForm((f) => ({ ...f, closedOn: e.target.value }))}
                />
              </label>
              <label className="span-2">
                Açıklama
                <textarea
                  rows={3}
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                />
                {fieldError('description')}
              </label>
              <div className="form-actions span-2">
                <button type="submit" className="btn-primary" disabled={saving}>
                  {saving ? 'Kaydediliyor…' : formMode === 'edit' ? 'Güncelle' : 'Ekle'}
                </button>
                <button type="button" className="btn-secondary" onClick={() => setFormMode(null)}>
                  İptal
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}

function collectDescendantIds(rootId: string, nodes: OrgNode[]): Set<string> {
  const children = new Map<string, string[]>()
  for (const n of nodes) {
    if (!n.parentId) continue
    const list = children.get(n.parentId) ?? []
    list.push(n.id)
    children.set(n.parentId, list)
  }
  const out = new Set<string>()
  const stack = [rootId]
  while (stack.length > 0) {
    const id = stack.pop()!
    for (const childId of children.get(id) ?? []) {
      if (out.has(childId)) continue
      out.add(childId)
      stack.push(childId)
    }
  }
  return out
}

function stripFacilities(nodes: OrgNode[]): OrgNode[] {
  return nodes
    .filter((n) => n.type !== 6)
    .map((n) => ({ ...n, children: stripFacilities(n.children ?? []) }))
}

function mainAndSubUnitsTree(nodes: OrgNode[]): OrgNode[] {
  const mains: OrgNode[] = []
  const keepKids = (n: OrgNode): OrgNode => ({
    ...n,
    children: (n.children ?? [])
      .filter((c) => c.type === 4 || c.type === 5)
      .map(keepKids),
  })
  const walk = (list: OrgNode[]) => {
    for (const n of list) {
      if (n.type === 4) mains.push(keepKids(n))
      else if (n.children?.length) walk(n.children)
    }
  }
  walk(nodes)
  return mains
}

function buildParentPath(node: OrgNode, byId: Map<string, OrgNode>): string[] {
  const parts: string[] = []
  let p = node.parentId
  while (p) {
    const parent = byId.get(p)
    if (!parent) break
    parts.unshift(parent.name)
    p = parent.parentId
  }
  return parts
}

function OrgUnitsHierarchyView({
  tree,
  flat,
  selectedId,
  expanded,
  setExpanded,
  canManage,
  onSelect,
  onEdit,
  onAddChild,
  onDelete,
}: {
  tree: OrgNode[]
  flat: OrgNode[]
  selectedId: string | null
  expanded: Record<string, boolean>
  setExpanded: Dispatch<SetStateAction<Record<string, boolean>>>
  canManage: boolean
  onSelect: (id: string) => void
  onEdit: (n: OrgNode) => void
  onAddChild: (id: string) => void
  onDelete: (n: OrgNode) => void
}) {
  const byId = useMemo(() => new Map(flat.map((n) => [n.id, n])), [flat])
  const total = useMemo(() => flattenOrg(tree).length, [tree])
  const selected = selectedId ? byId.get(selectedId) : undefined
  const selectedPath = selected ? [...buildParentPath(selected, byId), selected.name] : []

  if (tree.length === 0) return <p className="muted">Birim bulunmuyor.</p>

  const renderNode = (node: OrgNode, depth: number) => {
    const children = (node.children ?? []).filter((c) => c.type !== 6)
    const hasChildren = children.length > 0
    const open = expanded[node.id] !== false
    const activeCount = node.activeEmployeeCount ?? node.employeeCount ?? 0

    return (
      <li key={node.id} className="org-tree-item">
        <div
          className={`org-tree-row${selectedId === node.id ? ' is-selected' : ''}`}
          data-depth={depth > 2 ? 3 : depth}
          onClick={() => onSelect(node.id)}
        >
          {hasChildren ? (
            <button
              type="button"
              className="org-tree-toggle"
              aria-expanded={open}
              aria-label={open ? 'Alt birimleri gizle' : 'Alt birimleri göster'}
              onClick={(e) => {
                e.stopPropagation()
                setExpanded((prev) => ({ ...prev, [node.id]: !open }))
              }}
            >
              {open ? '▾' : '▸'}
            </button>
          ) : (
            <span className="org-tree-toggle is-leaf" aria-hidden="true" />
          )}

          <div className="org-tree-copy">
            <span className="org-tree-type">{node.typeLabel}</span>
            <span className="org-tree-name">{node.name}</span>
            <span className="org-tree-sub">
              {node.code ? `${node.code} · ` : ''}
              {amirText(node.type === 6, node.managerName)}
            </span>
          </div>

          <div className="org-tree-side">
            <div className="org-tree-metrics">
              <span className="org-tree-metric" title="Aktif personel">
                {activeCount} kişi
              </span>
              {hasChildren ? (
                <span className="org-tree-metric is-soft" title="Alt birim sayısı">
                  {children.length} alt
                </span>
              ) : null}
              {(node.missingStaffCount ?? 0) > 0 ? (
                <span className="org-tree-metric is-warn" title="Eksik kadro">
                  {node.missingStaffCount} eksik
                </span>
              ) : null}
              <span className={`org-status-pill status-${node.status}`}>{node.statusLabel}</span>
            </div>

            <div className="org-tree-actions" onClick={(e) => e.stopPropagation()}>
              <button
                type="button"
                className="org-card-btn primary"
                onClick={() => onSelect(node.id)}
              >
                Detay
              </button>
              {canManage && (
                <>
                  <button type="button" className="org-card-btn" onClick={() => onEdit(node)}>
                    Düzenle
                  </button>
                  <button type="button" className="org-card-btn" onClick={() => onAddChild(node.id)}>
                    Alt ekle
                  </button>
                  <button type="button" className="org-card-btn danger" onClick={() => onDelete(node)}>
                    Sil
                  </button>
                </>
              )}
            </div>
          </div>
        </div>

        {hasChildren && open ? (
          <ul className="org-tree-children">{children.map((c) => renderNode(c, depth + 1))}</ul>
        ) : null}
      </li>
    )
  }

  const allExpandable = flattenOrg(tree).filter((n) =>
    (n.children ?? []).some((c) => c.type !== 6),
  )
  const anyCollapsed = allExpandable.some((n) => expanded[n.id] === false)

  return (
    <div className="org-units-hierarchy">
      <div className="org-tree-head">
        <div>
          <p className="org-card-section-label">Birim yapısı</p>
          <h2>Kurumsal birim ağacı</h2>
          <p className="muted small">
            {selected
              ? `Seçili yol: ${selectedPath.join(' › ')}`
              : 'Girinti üst-alt ilişkisini gösterir. Detay sağdan açılır.'}
          </p>
        </div>
        <div className="org-tree-head-actions">
          <button
            type="button"
            className="btn-viewbar"
            onClick={() => {
              const next: Record<string, boolean> = {}
              for (const n of allExpandable) next[n.id] = anyCollapsed
              setExpanded(next)
            }}
          >
            {anyCollapsed ? 'Tümünü genişlet' : 'Tümünü daralt'}
          </button>
          <span className="org-tree-count">{total} birim</span>
        </div>
      </div>
      <ul className="org-tree">{tree.map((n) => renderNode(n, 0))}</ul>
    </div>
  )
}

function OrgFacilitiesGroupedView({
  nodes,
  flat,
  selectedId,
  canManage,
  onSelect,
  onEdit,
  onDelete,
}: {
  nodes: OrgNode[]
  flat: OrgNode[]
  selectedId: string | null
  canManage: boolean
  onSelect: (id: string) => void
  onEdit: (n: OrgNode) => void
  onDelete: (n: OrgNode) => void
}) {
  const byId = useMemo(() => orgById(flat), [flat])

  const { restGroups, youthItems } = useMemo(() => {
    const visible = nodes.filter((n) => !isKkmHall(n, byId))
    const youthItems = visible
      .filter((n) => isYouthFacility(n, byId))
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name, 'tr'))
    const rest = visible.filter((n) => !isYouthFacility(n, byId))
    const map = new Map<string, { parentName: string; parentType: number; items: OrgNode[] }>()
    for (const n of rest) {
      const key = n.parentId ?? '__root__'
      const parent = n.parentId ? byId.get(n.parentId) : undefined
      const parentName = n.parentName ?? parent?.name ?? 'Bağlı birim yok'
      const existing = map.get(key)
      if (existing) existing.items.push(n)
      else map.set(key, { parentName, parentType: parent?.type ?? 0, items: [n] })
    }
    for (const group of map.values()) {
      group.items.sort((a, b) => a.name.localeCompare(b.name, 'tr'))
    }
    const restGroups = [...map.entries()].sort((a, b) => {
      const aDir = a[1].parentType === 3 ? 0 : 1
      const bDir = b[1].parentType === 3 ? 0 : 1
      if (aDir !== bDir) return aDir - bDir
      return a[1].parentName.localeCompare(b[1].parentName, 'tr')
    })
    return { restGroups, youthItems }
  }, [nodes, byId])

  if (nodes.length === 0) return <p className="muted">Tesis bulunmuyor.</p>

  function card(n: OrgNode) {
    const activeCount = n.activeEmployeeCount ?? n.employeeCount ?? 0
    return (
      <article
        key={n.id}
        className={`org-card${selectedId === n.id ? ' is-selected' : ''}`}
        onClick={() => onSelect(n.id)}
      >
        <div className="org-card-top">
          <span className="org-card-type">{n.facilityCategoryName || 'Tesis türü belirtilmemiş'}</span>
          <span className={`org-status-pill status-${n.status}`}>{n.statusLabel}</span>
        </div>
        <h3>{n.name}</h3>
        <p className="org-card-manager">{amirText(true, n.managerName)}</p>
        <p className="org-card-count">
          <strong>{activeCount}</strong> aktif personel
        </p>
        <div className="org-card-actions" onClick={(e) => e.stopPropagation()}>
          {canManage && (
            <>
              <button type="button" className="org-card-btn" onClick={() => onEdit(n)}>
                Düzenle
              </button>
              <button type="button" className="org-card-btn danger" onClick={() => onDelete(n)}>
                Sil
              </button>
            </>
          )}
        </div>
      </article>
    )
  }

  return (
    <div className="org-facility-groups">
      {restGroups.map(([key, group]) => (
        <section key={key} className="org-facility-group">
          <div className="org-card-section-head">
            <div>
              <p className="org-card-section-label">Bağlı birim</p>
              <h2>{group.parentName}</h2>
              <p className="muted small">Bu birime bağlı tesisler</p>
            </div>
            <span>{group.items.length} tesis</span>
          </div>
          <div className="org-cards">{group.items.map(card)}</div>
        </section>
      ))}
      {youthItems.length > 0 ? (
        <section className="org-facility-group org-youth-section">
          <div className="org-card-section-head">
            <div>
              <p className="org-card-section-label">Bağlı birim</p>
              <h2>Gençlik Kütüphaneleri</h2>
              <p className="muted small">Mahalle kütüphaneleri</p>
            </div>
            <span>{youthItems.length} tesis</span>
          </div>
          <div className="org-cards">{youthItems.map(card)}</div>
        </section>
      ) : null}
    </div>
  )
}

function OrgCardsView({
  nodes,
  flat,
  section,
  selectedId,
  canManage,
  onSelect,
  onEdit,
  onAddChild,
  onDelete,
}: {
  nodes: OrgNode[]
  flat?: OrgNode[]
  section: OrganizationSection
  selectedId: string | null
  canManage: boolean
  onSelect: (id: string) => void
  onEdit: (n: OrgNode) => void
  onAddChild: (id: string) => void
  onDelete: (n: OrgNode) => void
}) {
  if (nodes.length === 0) return <p className="muted">Kart gösterilecek birim yok.</p>

  const units = nodes.filter((n) => n.type !== 6)
  const facilities = nodes.filter((n) => n.type === 6)
  const byId = new Map((flat ?? nodes).map((n) => [n.id, n]))

  const renderCards = (items: OrgNode[]) => (
    <div className="org-cards">
      {items.map((n) => {
        const activeCount = n.activeEmployeeCount ?? n.employeeCount ?? 0
        return (
        <article
          key={n.id}
          className={`org-card${selectedId === n.id ? ' is-selected' : ''}`}
          onClick={() => onSelect(n.id)}
        >
          <div className="org-card-top">
            <span className="org-card-type">{n.typeLabel}</span>
            <span className={`org-status-pill status-${n.status}`}>{n.statusLabel}</span>
          </div>
          <h3>{n.name}</h3>
          <p className="org-card-manager">{amirText(n.type === 6, n.managerName)}</p>
          <p className="org-card-count">
            <strong>{activeCount}</strong> aktif personel
            {(n.missingStaffCount ?? 0) > 0 ? ` · ${n.missingStaffCount} eksik` : ''}
          </p>
          <div className="org-card-actions" onClick={(e) => e.stopPropagation()}>
            {canManage && (
              <>
                <button type="button" className="org-card-btn" onClick={() => onEdit(n)}>
                  Düzenle
                </button>
                {n.type !== 6 && (
                  <button type="button" className="org-card-btn" onClick={() => onAddChild(n.id)}>
                    Alt ekle
                  </button>
                )}
                <button type="button" className="org-card-btn danger" onClick={() => onDelete(n)}>
                  Sil
                </button>
              </>
            )}
          </div>
        </article>
        )
      })}
    </div>
  )

  if (section === 'units') {
    if (units.length === 0) return <p className="muted">Birim bulunmuyor.</p>
    const groups: Array<{ key: string; title: string; subtitle: string; items: OrgNode[] }> = []
    const grouped = new Map<string, OrgNode[]>()
    for (const n of units) {
      const key = n.parentId && byId.has(n.parentId) ? n.parentId : '__root__'
      const list = grouped.get(key)
      if (list) list.push(n)
      else grouped.set(key, [n])
    }
    for (const [key, items] of grouped) {
      if (key === '__root__') {
        groups.unshift({ key, title: 'Kurum', subtitle: 'Organizasyonun en üst kaydı', items })
      } else {
        const parent = byId.get(key)!
        const path = [...buildParentPath(parent, byId), parent.name]
        groups.push({
          key,
          title: parent.name,
          subtitle: path.length > 1 ? path.join(' › ') : `${parent.typeLabel} altındaki birimler`,
          items,
        })
      }
    }
    return (
      <div className="org-card-sections">
        {groups.map((group) => (
          <section key={group.key}>
            <div className="org-card-section-head">
              <div>
                <p className="org-card-section-label">
                  {group.key === '__root__' ? 'Kök seviye' : 'Üst birim'}
                </p>
                <h2>{group.title}</h2>
                <p className="muted small">{group.subtitle}</p>
              </div>
              <span>{group.items.length} birim</span>
            </div>
            {renderCards(group.items)}
          </section>
        ))}
      </div>
    )
  }

  if (section === 'facilities') {
    return facilities.length > 0 ? renderCards(facilities) : <p className="muted">Tesis bulunmuyor.</p>
  }

  return (
    <div className="org-card-sections">
      <section>
        <div className="org-card-section-head">
          <div>
            <h2>Birimler</h2>
            <p className="muted small">Müdürlük, ana birim ve alt birimler</p>
          </div>
          <span>{units.length} kayıt</span>
        </div>
        {units.length > 0 ? renderCards(units) : <p className="muted">Birim bulunmuyor.</p>}
      </section>
      <section>
        <div className="org-card-section-head">
          <div>
            <h2>Tesisler</h2>
            <p className="muted small">Birimlere bağlı fiziksel çalışma yerleri</p>
          </div>
          <span>{facilities.length} kayıt</span>
        </div>
        {facilities.length > 0 ? renderCards(facilities) : <p className="muted">Tesis bulunmuyor.</p>}
      </section>
    </div>
  )
}

function OrgPersonnelView({
  nodes,
  expanded,
  setExpanded,
  selectedId,
  onSelect,
  detail,
  detailLoading,
}: {
  nodes: OrgNode[]
  expanded: Record<string, boolean>
  setExpanded: Dispatch<SetStateAction<Record<string, boolean>>>
  selectedId: string | null
  onSelect: (id: string) => void
  detail: OrgUnitDetail | null
  detailLoading: boolean
}) {
  if (nodes.length === 0) return <p className="muted">Personel görünümü için birim yok.</p>

  return (
    <ul className="org-personnel-list">
      {nodes.map((n) => {
        const open = Boolean(expanded[n.id])
        return (
          <li key={n.id} className={selectedId === n.id ? 'is-selected' : ''}>
            <button
              type="button"
              className="org-personnel-toggle"
              onClick={() => {
                onSelect(n.id)
                setExpanded((prev) => ({ ...prev, [n.id]: !prev[n.id] }))
              }}
            >
              <span className="org-personnel-chevron">{open ? '▾' : '▸'}</span>
              <span>
                <strong>{n.name}</strong>
                <span className="muted">
                  {' '}
                  · {n.typeLabel} · {n.activeEmployeeCount ?? n.employeeCount ?? 0} kişi
                </span>
              </span>
            </button>
            {open && (
              <div className="org-personnel-body">
                {detailLoading && selectedId === n.id && <p className="muted small">Yükleniyor…</p>}
                {selectedId === n.id && detail && detail.id === n.id ? (
                  detail.personnelByDuty.length === 0 ? (
                    <p className="muted small">Bu birimde aktif personel yok.</p>
                  ) : (
                    detail.personnelByDuty.map((g) => (
                      <div key={g.dutyName} className="org-duty-group">
                        <h4>
                          {g.dutyName}{' '}
                          <span className="muted">({g.employees.length})</span>
                        </h4>
                        <ul>
                          {g.employees.map((e) => (
                            <li key={e.id}>
                              <Link to={`/employees/${e.id}`}>{e.fullName}</Link>
                              <span className="muted">
                                {[e.jobTitleName, e.employeeNumber, e.statusLabel]
                                  .filter(Boolean)
                                  .join(' · ')}
                              </span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ))
                  )
                ) : (
                  <p className="muted small">Personel listesi için birimi açın.</p>
                )}
              </div>
            )}
          </li>
        )
      })}
    </ul>
  )
}

function UnitDetailPanel({
  detail,
  canManage,
  onClose,
  onEdit,
  onAddChild,
  onSelectChild,
}: {
  detail: OrgUnitDetail
  canManage: boolean
  onClose: () => void
  onEdit: () => void
  onAddChild: () => void
  onSelectChild: (id: string) => void
}) {
  const [tab, setTab] = useState<'ozet' | 'personel' | 'dagilim'>('ozet')
  const isFacility = isFacilityType(detail.type)
  const kind = isFacility ? 'Tesis' : 'Birim'
  const ideal = detail.idealStaffCount != null ? `${detail.idealStaffCount}` : '—'
  const missing = detail.idealStaffCount != null ? String(detail.missingStaffCount) : '—'
  const personnelCount = detail.personnelByDuty.reduce((sum, g) => sum + g.employees.length, 0)

  useEffect(() => {
    setTab('ozet')
  }, [detail.id])

  return (
    <div className="org-detail-body">
      <div className="org-detail-head">
        <div>
          <p className="org-detail-eyebrow">{kind} detayı</p>
          <h2>{detail.name}</h2>
        </div>
        <button type="button" className="btn-secondary" onClick={onClose}>
          Kapat
        </button>
      </div>

      <div className="org-detail-hero">
        <div className="org-detail-hero-copy">
          <div className="org-detail-chips">
            <span className="org-detail-chip">{detail.typeLabel}</span>
            <span className={`org-status-pill status-${detail.status}`}>{detail.statusLabel}</span>
            {detail.code ? <span className="org-detail-chip muted-chip">{detail.code}</span> : null}
          </div>
          {detail.parentName ? (
            <p className="org-detail-parent">
              {isFacility ? 'Bağlı birim' : 'Üst birim'}: <strong>{detail.parentName}</strong>
            </p>
          ) : (
            <p className="org-detail-parent muted">Üst kayıt yok</p>
          )}
          <p className="org-detail-parent">
            {amirTitle(isFacility)}: <strong>{detail.managerName ?? 'Atanmamış'}</strong>
          </p>
        </div>
        {canManage && (
          <div className="org-detail-actions">
            <button type="button" className="btn-primary" onClick={onEdit}>
              Düzenle
            </button>
            {!isFacility && (
              <button type="button" className="btn-secondary" onClick={onAddChild}>
                Alt ekle
              </button>
            )}
          </div>
        )}
      </div>

      <div className="org-detail-stats" aria-label="Kadro özeti">
        <div className="org-detail-stat">
          <span>Aktif</span>
          <strong>{detail.activeEmployeeCount}</strong>
        </div>
        <div className="org-detail-stat">
          <span>İdeal</span>
          <strong>{ideal}</strong>
        </div>
        <div className="org-detail-stat">
          <span>Eksik</span>
          <strong className={detail.missingStaffCount > 0 ? 'is-warn' : undefined}>{missing}</strong>
        </div>
      </div>

      <div className="org-detail-tabs" role="tablist" aria-label="Detay bölümleri">
        {(
          [
            ['ozet', 'Özet'],
            ['personel', `Personel (${personnelCount})`],
            ['dagilim', 'Dağılım'],
          ] as const
        ).map(([id, label]) => (
          <button
            key={id}
            type="button"
            role="tab"
            className={tab === id ? 'is-active' : ''}
            aria-selected={tab === id}
            onClick={() => setTab(id)}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'ozet' && (
        <div className="org-detail-tab-panel">
          <section className="org-detail-section">
            <h4>{isFacility ? 'Tesis bilgileri' : 'İletişim'}</h4>
            <dl className="org-detail-meta">
              {isFacility && <DetailField label="Tesis türü" value={detail.facilityCategoryName} />}
              {isFacility && <DetailField label="Adres" value={detail.address} />}
              {isFacility && <DetailField label="Kapasite" value={detail.capacity != null ? String(detail.capacity) : null} />}
              {isFacility && <DetailField label="Çalışma saatleri" value={detail.workingHours} />}
              <DetailField label="Telefon" value={detail.phone} />
              <DetailField label="E-posta" value={detail.email} />
              <DetailField label="Açılış" value={formatDate(detail.openedOn)} />
              <DetailField label="Kapanış" value={formatDate(detail.closedOn)} />
            </dl>
          </section>

          {detail.description ? (
            <section className="org-detail-section">
              <h4>Açıklama</h4>
              <p className="org-detail-desc">{detail.description}</p>
            </section>
          ) : null}

          {detail.childFacilities.length > 0 ? (
            <section className="org-detail-section">
              <h4>{isFacility ? 'Bağlı kayıtlar' : 'Alt birimler / tesisler'}</h4>
              <ul className="org-child-list">
                {detail.childFacilities.map((c) => (
                  <li key={c.id}>
                    <button type="button" onClick={() => onSelectChild(c.id)}>
                      {c.name}
                    </button>
                    <span className="muted">
                      {c.typeLabel} · {c.activeEmployeeCount} kişi
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          ) : (
            <p className="muted small">Alt kayıt yok.</p>
          )}

          {detail.recentMovements.length > 0 ? (
            <section className="org-detail-section">
              <h4>Son hareketler</h4>
              <ul className="org-movement-list">
                {detail.recentMovements.slice(0, 5).map((m) => (
                  <li key={m.id}>
                    <strong>{formatDate(m.startDate)}</strong>
                    <span>
                      {m.employeeName} · {m.movementTypeLabel}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
        </div>
      )}

      {tab === 'personel' && (
        <div className="org-detail-tab-panel">
          {detail.personnelByDuty.length === 0 ? (
            <p className="muted">Bu {kind.toLowerCase()}de aktif personel yok.</p>
          ) : (
            detail.personnelByDuty.map((g) => (
              <div key={g.dutyName} className="org-duty-group">
                <h5>
                  {g.dutyName} <span className="muted">({g.employees.length})</span>
                </h5>
                <ul>
                  {g.employees.map((e) => (
                    <li key={e.id}>
                      <Link to={`/employees/${e.id}`}>{e.fullName}</Link>
                      <span className="muted">
                        {[e.jobTitleName, e.statusLabel].filter(Boolean).join(' · ')}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ))
          )}
        </div>
      )}

      {tab === 'dagilim' && (
        <div className="org-detail-tab-panel">
          <DetailSection title="Görev dağılımı" items={detail.dutyDistribution} emptyText="Görev dağılımı yok." />
          <DetailSection
            title="İstihdam türleri"
            items={detail.employmentTypeDistribution}
            emptyText="İstihdam dağılımı yok."
          />
          <DetailSection
            title="Yetkinlikler"
            items={detail.skills}
            emptyText="Yetkinlik kaydı yok."
          />
        </div>
      )}
    </div>
  )
}

function isFacilityType(type: number) {
  return type === 6
}

function DetailField({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="org-detail-field">
      <dt>{label}</dt>
      <dd>{value?.trim() ? value : '—'}</dd>
    </div>
  )
}

function DetailSection({
  title,
  items,
  emptyText,
}: {
  title: string
  items: Array<{ name: string; count: number }>
  emptyText?: string
}) {
  if (!items.length) {
    return emptyText ? <p className="muted small">{emptyText}</p> : null
  }
  return (
    <section className="org-detail-section">
      <h4>{title}</h4>
      <ul className="org-stat-list">
        {items.map((i) => (
          <li key={i.name}>
            <span>{i.name}</span>
            <strong>{i.count}</strong>
          </li>
        ))}
      </ul>
    </section>
  )
}

function formatDate(iso?: string | null): string | null {
  if (!iso) return null
  const [y, m, d] = iso.split('-')
  if (!y || !m || !d) return iso
  return `${d}.${m}.${y}`
}
