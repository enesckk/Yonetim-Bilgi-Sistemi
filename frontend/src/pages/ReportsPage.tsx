import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import {
  buildReport,
  downloadReportExcel,
  downloadReportPdf,
  fetchReportColumns,
  fetchReportsSummary,
  type ReportBuildPayload,
  type ReportColumn,
  type ReportResult,
  type ReportsSummary,
} from '@/api/reportsApi'
import {
  fetchEmployeeFormOptions,
  type EmployeeFormOptions,
} from '@/api/employeesApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

/** Tasarımcı durumu — kayıtlı rapor olarak da saklanır. */
interface ReportConfig {
  title: string
  search: string
  unitId: string
  includeSubUnits: boolean
  facilityId: string
  status: string
  employmentTypeId: string
  jobTitleId: string
  jobDutyId: string
  educationLevel: string
  skillId: string
  hireYearFrom: string
  hireYearTo: string
  universityContains: string
  movementFrom: string
  movementTo: string
  incompleteProfileOnly: boolean
  missingSkillsOnly: boolean
  hasSpecialConditionOnly: boolean
  hasCertificatesOnly: boolean
  missingCertificatesOnly: boolean
  hasMovementsOnly: boolean
  columns: string[]
  groupBy: string
  orderByColumn: string
  orderByDesc: boolean
}

interface SavedReport {
  id: string
  name: string
  createdAt: string
  config: ReportConfig
}

type ReportsView = 'presets' | 'builder' | 'saved' | 'result'

const SAVED_KEY = 'py.reports.saved.v1'

const emptyConfig = (): ReportConfig => ({
  title: '',
  search: '',
  unitId: '',
  includeSubUnits: true,
  facilityId: '',
  status: '',
  employmentTypeId: '',
  jobTitleId: '',
  jobDutyId: '',
  educationLevel: '',
  skillId: '',
  hireYearFrom: '',
  hireYearTo: '',
  universityContains: '',
  movementFrom: '',
  movementTo: '',
  incompleteProfileOnly: false,
  missingSkillsOnly: false,
  hasSpecialConditionOnly: false,
  hasCertificatesOnly: false,
  missingCertificatesOnly: false,
  hasMovementsOnly: false,
  columns: ['fullName', 'employeeNumber', 'status', 'jobTitle', 'unit'],
  groupBy: '',
  orderByColumn: '',
  orderByDesc: false,
})

interface PresetReport {
  id: string
  name: string
  description: string
  category: string
  config: Partial<ReportConfig>
}

const PRESETS: PresetReport[] = [
  {
    id: 'all',
    name: 'Tüm personel listesi',
    description: 'Temel bilgilerle tam liste',
    category: 'Genel',
    config: {
      title: 'Tüm personel listesi',
      columns: ['fullName', 'employeeNumber', 'status', 'jobTitle', 'duty', 'unit', 'facility', 'employmentType', 'hireDate'],
    },
  },
  {
    id: 'unit-report',
    name: 'Birim bazlı personel raporu',
    description: 'Alt birimler dahil detaylı kadro',
    category: 'Genel',
    config: {
      title: 'Birim bazlı personel raporu',
      groupBy: 'unit',
      includeSubUnits: true,
      columns: ['fullName', 'employeeNumber', 'jobTitle', 'duty', 'employmentType', 'hireDate', 'status'],
    },
  },
  {
    id: 'facility-report',
    name: 'Tesis bazlı personel raporu',
    description: 'Tesislerde çalışanların detayı',
    category: 'Genel',
    config: {
      title: 'Tesis bazlı personel raporu',
      groupBy: 'facility',
      columns: ['fullName', 'jobTitle', 'duty', 'employmentType', 'hireDate', 'status'],
    },
  },
  {
    id: 'by-unit',
    name: 'Birimlere göre dağılım',
    description: 'Birim başlıkları altında liste',
    category: 'Dağılım',
    config: {
      title: 'Birimlere göre personel dağılımı',
      groupBy: 'unit',
      columns: ['fullName', 'employeeNumber', 'jobTitle', 'duty', 'status'],
    },
  },
  {
    id: 'by-facility',
    name: 'Tesislere göre dağılım',
    description: 'Tesis başlıkları altında liste',
    category: 'Dağılım',
    config: {
      title: 'Tesislere göre personel dağılımı',
      groupBy: 'facility',
      columns: ['fullName', 'jobTitle', 'duty', 'unit', 'status'],
    },
  },
  {
    id: 'by-title',
    name: 'Unvanlara göre dağılım',
    description: 'Unvan başlıkları altında liste',
    category: 'Dağılım',
    config: {
      title: 'Unvanlara göre personel dağılımı',
      groupBy: 'jobTitle',
      columns: ['fullName', 'unit', 'facility', 'status'],
    },
  },
  {
    id: 'by-duty',
    name: 'Görev türlerine göre dağılım',
    description: 'Fiili görev başlıkları altında',
    category: 'Dağılım',
    config: {
      title: 'Görev türlerine göre personel dağılımı',
      groupBy: 'duty',
      columns: ['fullName', 'jobTitle', 'unit', 'status'],
    },
  },
  {
    id: 'by-employment',
    name: 'İstihdam türüne göre dağılım',
    description: 'Kadrolu, sözleşmeli, şirket…',
    category: 'Dağılım',
    config: {
      title: 'İstihdam türüne göre personel dağılımı',
      groupBy: 'employmentType',
      columns: ['fullName', 'jobTitle', 'unit', 'status'],
    },
  },
  {
    id: 'by-hire-year',
    name: 'İşe giriş yılına göre liste',
    description: 'Yıl başlıkları altında liste',
    category: 'Dağılım',
    config: {
      title: 'İşe giriş yılına göre personel listesi',
      groupBy: 'hireYear',
      columns: ['fullName', 'jobTitle', 'unit', 'hireDate'],
    },
  },
  {
    id: 'by-service',
    name: 'Hizmet süresine göre dağılım',
    description: '5’er yıllık hizmet aralıkları',
    category: 'Dağılım',
    config: {
      title: 'Hizmet süresine göre personel dağılımı',
      groupBy: 'serviceBand',
      columns: ['fullName', 'jobTitle', 'unit', 'hireDate', 'serviceYears'],
    },
  },
  {
    id: 'by-education',
    name: 'Eğitim seviyesine göre dağılım',
    description: 'Lise, lisans, yüksek lisans…',
    category: 'Eğitim ve yetkinlik',
    config: {
      title: 'Eğitim seviyesine göre personel dağılımı',
      groupBy: 'educationLevel',
      columns: ['fullName', 'jobTitle', 'unit', 'university', 'department'],
    },
  },
  {
    id: 'by-department',
    name: 'Mezun olunan bölümlere göre',
    description: 'Bölüm başlıkları altında liste',
    category: 'Eğitim ve yetkinlik',
    config: {
      title: 'Mezun olunan bölümlere göre personel listesi',
      groupBy: 'department',
      columns: ['fullName', 'jobTitle', 'unit', 'university', 'graduationYear'],
    },
  },
  {
    id: 'by-skill',
    name: 'Yetkinliklere göre liste',
    description: 'Yetkinlik sütunuyla tam liste',
    category: 'Eğitim ve yetkinlik',
    config: {
      title: 'Yetkinliklere göre personel listesi',
      columns: ['fullName', 'jobTitle', 'unit', 'skills'],
    },
  },
  {
    id: 'certified',
    name: 'Sertifikalı personeller',
    description: 'En az bir sertifikası olanlar',
    category: 'Eğitim ve yetkinlik',
    config: {
      title: 'Sertifikalı personeller',
      hasCertificatesOnly: true,
      columns: ['fullName', 'jobTitle', 'unit', 'certificates', 'certificateCount'],
    },
  },
  {
    id: 'incomplete',
    name: 'Eksik bilgili personeller',
    description: 'Profil tamamlanması %80 altı',
    category: 'Durum ve hareket',
    config: {
      title: 'Eksik bilgisi bulunan personeller',
      incompleteProfileOnly: true,
      columns: ['fullName', 'employeeNumber', 'unit', 'profileCompletion', 'status'],
      orderByColumn: 'profileCompletion',
    },
  },
  {
    id: 'moved',
    name: 'Görev yeri değişenler',
    description: 'Hareket kaydı olan personeller',
    category: 'Durum ve hareket',
    config: {
      title: 'Görev yeri değişen personeller',
      hasMovementsOnly: true,
      columns: ['fullName', 'unit', 'facility', 'lastMovement', 'movementCount'],
      orderByColumn: 'lastMovement',
      orderByDesc: true,
    },
  },
  {
    id: 'passive',
    name: 'Pasif personeller',
    description: 'Durumu pasif olanlar',
    category: 'Durum ve hareket',
    config: {
      title: 'Pasif personeller',
      status: '2',
      columns: ['fullName', 'employeeNumber', 'jobTitle', 'unit', 'status'],
    },
  },
  {
    id: 'left',
    name: 'İşten ayrılan personeller',
    description: 'Durumu “işten ayrıldı” olanlar',
    category: 'Durum ve hareket',
    config: {
      title: 'İşten ayrılan personeller',
      status: '6',
      columns: ['fullName', 'employeeNumber', 'jobTitle', 'unit', 'hireDate', 'lastMovement'],
    },
  },
]

const PRESET_CATEGORIES = ['Genel', 'Dağılım', 'Eğitim ve yetkinlik', 'Durum ve hareket']

function loadSaved(): SavedReport[] {
  try {
    const raw = localStorage.getItem(SAVED_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw) as SavedReport[]
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

function toPayload(cfg: ReportConfig): ReportBuildPayload {
  return {
    title: cfg.title.trim() || undefined,
    search: cfg.search.trim() || undefined,
    unitId: cfg.unitId || undefined,
    includeSubUnits: cfg.unitId ? cfg.includeSubUnits : undefined,
    facilityId: cfg.facilityId || undefined,
    status: cfg.status ? (Number(cfg.status) as ReportBuildPayload['status']) : undefined,
    employmentTypeId: cfg.employmentTypeId || undefined,
    jobTitleId: cfg.jobTitleId || undefined,
    jobDutyId: cfg.jobDutyId || undefined,
    educationLevel: cfg.educationLevel
      ? (Number(cfg.educationLevel) as ReportBuildPayload['educationLevel'])
      : undefined,
    skillId: cfg.skillId || undefined,
    hireYearFrom: cfg.hireYearFrom ? Number(cfg.hireYearFrom) : undefined,
    hireYearTo: cfg.hireYearTo ? Number(cfg.hireYearTo) : undefined,
    universityContains: cfg.universityContains.trim() || undefined,
    incompleteProfileOnly: cfg.incompleteProfileOnly || undefined,
    missingSkillsOnly: cfg.missingSkillsOnly || undefined,
    hasSpecialConditionOnly: cfg.hasSpecialConditionOnly || undefined,
    missingCertificatesOnly: cfg.missingCertificatesOnly || undefined,
    hasCertificatesOnly: cfg.hasCertificatesOnly || undefined,
    hasMovementsOnly: cfg.hasMovementsOnly || undefined,
    movementFrom: cfg.movementFrom || undefined,
    movementTo: cfg.movementTo || undefined,
    columns: cfg.columns,
    groupBy: cfg.groupBy || undefined,
    orderByColumn: cfg.orderByColumn || undefined,
    orderByDesc: cfg.orderByDesc || undefined,
  }
}

function buildCsv(result: ReportResult): string {
  const escape = (v: string | null) => {
    const s = v ?? ''
    return /[";\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
  }
  const lines = [result.columns.map((c) => escape(c.label)).join(';')]
  for (const row of result.rows) lines.push(row.map(escape).join(';'))
  return '\uFEFF' + lines.join('\r\n')
}

export function ReportsPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.ReportsView)
  const canEmployees = hasPermission(PermissionCodes.EmployeesView)
  const canExportExcel = hasPermission(PermissionCodes.ReportsExportExcel)
  const canExportPdf = hasPermission(PermissionCodes.ReportsExportPdf)

  const [summary, setSummary] = useState<ReportsSummary | null>(null)
  const [columns, setColumns] = useState<ReportColumn[]>([])
  const [options, setOptions] = useState<EmployeeFormOptions | null>(null)
  const [error, setError] = useState<string | null>(null)

  const [view, setView] = useState<ReportsView>('presets')
  const [config, setConfig] = useState<ReportConfig>(emptyConfig)
  const [result, setResult] = useState<ReportResult | null>(null)
  const [running, setRunning] = useState(false)
  const [exportBusy, setExportBusy] = useState<'excel' | 'pdf' | 'csv' | null>(null)

  const [quickFilter, setQuickFilter] = useState('')
  const [clientSort, setClientSort] = useState<{ index: number; desc: boolean } | null>(null)

  const [saved, setSaved] = useState<SavedReport[]>(loadSaved)
  const topRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    if (!canView) return
    void (async () => {
      try {
        const [s, cols] = await Promise.all([fetchReportsSummary(), fetchReportColumns()])
        setSummary(s)
        setColumns(cols)
      } catch (err) {
        setError(err instanceof ApiClientError ? err.message : 'Rapor altyapısı yüklenemedi.')
      }
    })()
  }, [canView])

  useEffect(() => {
    if (!canEmployees) return
    void (async () => {
      try {
        setOptions(await fetchEmployeeFormOptions())
      } catch {
        /* filtre lookuplarında sorun — form yine çalışır */
      }
    })()
  }, [canEmployees])

  const columnGroups = useMemo(() => {
    const groups = new Map<string, ReportColumn[]>()
    for (const c of columns) {
      const list = groups.get(c.group)
      if (list) list.push(c)
      else groups.set(c.group, [c])
    }
    return [...groups.entries()]
  }, [columns])

  const columnLabel = useCallback(
    (key: string) => columns.find((c) => c.key === key)?.label ?? key,
    [columns],
  )

  const activeFilterCount = useMemo(() => {
    let n = 0
    if (config.search.trim()) n++
    if (config.unitId) n++
    if (config.facilityId) n++
    if (config.status) n++
    if (config.employmentTypeId) n++
    if (config.jobTitleId) n++
    if (config.jobDutyId) n++
    if (config.educationLevel) n++
    if (config.skillId) n++
    if (config.hireYearFrom || config.hireYearTo) n++
    if (config.movementFrom || config.movementTo) n++
    if (config.incompleteProfileOnly) n++
    if (config.missingSkillsOnly) n++
    if (config.hasSpecialConditionOnly) n++
    if (config.hasCertificatesOnly) n++
    if (config.missingCertificatesOnly) n++
    if (config.hasMovementsOnly) n++
    return n
  }, [config])

  const persistSaved = (list: SavedReport[]) => {
    setSaved(list)
    try {
      localStorage.setItem(SAVED_KEY, JSON.stringify(list))
    } catch {
      /* depolama dolu olabilir */
    }
  }

  const run = useCallback(async (cfg: ReportConfig) => {
    setRunning(true)
    setError(null)
    setQuickFilter('')
    setClientSort(null)
    try {
      const r = await buildReport(toPayload(cfg))
      setResult(r)
      setView('result')
      window.setTimeout(
        () => topRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' }),
        50,
      )
    } catch (err) {
      setResult(null)
      setError(err instanceof ApiClientError ? err.message : 'Rapor oluşturulamadı.')
    } finally {
      setRunning(false)
    }
  }, [])

  const applyPreset = (preset: PresetReport) => {
    const cfg: ReportConfig = { ...emptyConfig(), ...preset.config }
    setConfig(cfg)
    void run(cfg)
  }

  const runSavedReport = (item: SavedReport) => {
    const cfg: ReportConfig = { ...emptyConfig(), ...item.config }
    setConfig(cfg)
    void run(cfg)
  }

  const saveCurrent = () => {
    const name = window.prompt('Kayıtlı rapor adı:', config.title.trim() || 'Özel rapor')
    if (!name?.trim()) return
    const item: SavedReport = {
      id: crypto.randomUUID(),
      name: name.trim(),
      createdAt: new Date().toISOString(),
      config,
    }
    persistSaved([item, ...saved].slice(0, 30))
  }

  const onExport = async (format: 'excel' | 'pdf' | 'csv') => {
    setExportBusy(format)
    setError(null)
    try {
      if (format === 'csv') {
        if (!result) return
        const blob = new Blob([buildCsv(result)], { type: 'text/csv;charset=utf-8' })
        const url = URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.download = `rapor-${new Date().toISOString().slice(0, 10)}.csv`
        a.click()
        URL.revokeObjectURL(url)
      } else if (format === 'excel') {
        await downloadReportExcel(toPayload(config))
      } else {
        await downloadReportPdf(toPayload(config))
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Dışa aktarım başarısız.')
    } finally {
      setExportBusy(null)
    }
  }

  // Sonuç tablosu: hızlı filtre + istemci tarafı sıralama
  const displayRows = useMemo(() => {
    if (!result) return []
    let rows = result.rows
    const q = quickFilter.trim().toLowerCase()
    if (q) {
      rows = rows.filter((r) => r.some((cell) => (cell ?? '').toLowerCase().includes(q)))
    }
    if (clientSort) {
      const { index, desc } = clientSort
      rows = [...rows].sort((a, b) => {
        const av = a[index] ?? ''
        const bv = b[index] ?? ''
        const an = Number(av.replace(',', '.'))
        const bn = Number(bv.replace(',', '.'))
        const cmp =
          av !== '' && bv !== '' && !Number.isNaN(an) && !Number.isNaN(bn)
            ? an - bn
            : av.localeCompare(bv, 'tr', { sensitivity: 'base' })
        return desc ? -cmp : cmp
      })
    }
    return rows
  }, [result, quickFilter, clientSort])

  const groupIndex = useMemo(() => {
    if (!result?.groupByKey || clientSort) return -1
    return result.columns.findIndex((c) => c.key === result.groupByKey)
  }, [result, clientSort])

  const set = <K extends keyof ReportConfig>(key: K, value: ReportConfig[K]) =>
    setConfig((c) => ({ ...c, [key]: value }))

  const toggleColumn = (key: string) =>
    setConfig((c) => {
      const removing = c.columns.includes(key)
      return {
        ...c,
        columns: removing ? c.columns.filter((k) => k !== key) : [...c.columns, key],
        orderByColumn: removing && c.orderByColumn === key ? '' : c.orderByColumn,
        groupBy: removing && c.groupBy === key ? '' : c.groupBy,
      }
    })

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Raporlar ekranı için yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="org-page reports-page" ref={topRef}>
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Raporlama</p>
          <h1>Raporlar</h1>
          <p className="muted">
            Hazır bir raporu tek tıkla çalıştırın veya tasarımcıda sütun, filtre ve gruplamayı
            kendiniz seçin. Sonuçları Excel, PDF, CSV olarak indirin ya da yazdırın.
          </p>
        </div>
        <div className="report-hero-side">
          {summary && (
            <div className="report-hero-stats">
              <div>
                <strong>{summary.totalEmployees}</strong>
                <span>Personel</span>
              </div>
              <div>
                <strong>{summary.activeEmployees}</strong>
                <span>Aktif</span>
              </div>
              <div>
                <strong>{summary.passiveEmployees}</strong>
                <span>Pasif</span>
              </div>
              <div>
                <strong>{summary.organizationUnitCount}</strong>
                <span>Birim / tesis</span>
              </div>
            </div>
          )}
          <button
            type="button"
            className="btn-primary"
            onClick={() => {
              setConfig(emptyConfig())
              setView('builder')
            }}
          >
            + Özel rapor oluştur
          </button>
        </div>
      </header>

      <div className="org-toolbar panel">
        <div className="org-views" role="tablist" aria-label="Rapor görünümleri">
          {(
            [
              ['presets', 'Hazır raporlar'],
              ['builder', 'Rapor tasarımcısı'],
              ['saved', `Kayıtlı raporlarım${saved.length ? ` (${saved.length})` : ''}`],
            ] as const
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              role="tab"
              className={view === id ? 'is-active' : ''}
              aria-selected={view === id}
              onClick={() => setView(id)}
            >
              {label}
            </button>
          ))}
          {result && (
            <button
              type="button"
              role="tab"
              className={view === 'result' ? 'is-active' : ''}
              aria-selected={view === 'result'}
              onClick={() => setView('result')}
            >
              Sonuç · {result.totalCount} kayıt
            </button>
          )}
        </div>
        {running && <span className="muted small">Rapor hazırlanıyor…</span>}
      </div>

      {error && <div className="form-error panel">{error}</div>}

      {view === 'presets' && (
        <section className="panel">
          {PRESET_CATEGORIES.map((cat) => (
            <div key={cat} className="report-preset-group">
              <div className="report-preset-cat">
                <span>{cat}</span>
                <i />
              </div>
              <div className="report-presets">
                {PRESETS.filter((p) => p.category === cat).map((p) => (
                  <button
                    key={p.id}
                    type="button"
                    className="report-preset-card"
                    disabled={running}
                    onClick={() => applyPreset(p)}
                  >
                    <span className="report-preset-body">
                      <strong>{p.name}</strong>
                      <span>{p.description}</span>
                    </span>
                    <span className="report-preset-run" aria-hidden>
                      →
                    </span>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </section>
      )}

      {view === 'saved' && (
        <section className="panel">
          {saved.length === 0 ? (
            <div className="report-empty">
              <h3>Henüz kayıtlı rapor yok</h3>
              <p className="muted">
                Tasarımcıda bir rapor hazırlayıp “Raporu kaydet” dediğinizde burada listelenir ve
                tek tıkla yeniden çalıştırılır.
              </p>
              <button type="button" className="btn-primary" onClick={() => setView('builder')}>
                Tasarımcıyı aç
              </button>
            </div>
          ) : (
            <ul className="report-saved-list">
              {saved.map((s) => (
                <li key={s.id}>
                  <div>
                    <strong>{s.name}</strong>
                    <span className="muted small">
                      {new Date(s.createdAt).toLocaleDateString('tr-TR')}
                      {s.config.groupBy ? ` · Gruplama: ${columnLabel(s.config.groupBy)}` : ''}
                      {` · ${s.config.columns.length} sütun`}
                    </span>
                  </div>
                  <div className="report-saved-actions">
                    <button
                      type="button"
                      className="btn-primary"
                      disabled={running}
                      onClick={() => runSavedReport(s)}
                    >
                      Çalıştır
                    </button>
                    <button
                      type="button"
                      className="btn-secondary"
                      onClick={() => {
                        setConfig({ ...emptyConfig(), ...s.config })
                        setView('builder')
                      }}
                    >
                      Düzenle
                    </button>
                    <button
                      type="button"
                      className="org-card-btn danger"
                      onClick={() => persistSaved(saved.filter((x) => x.id !== s.id))}
                    >
                      Sil
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      {view === 'builder' && (
        <section className="panel report-builder">
          <div className="report-step">
            <div className="report-step-head">
              <span className="report-step-no">1</span>
              <div>
                <h3>Kapsam ve filtreler</h3>
                <p className="muted small">
                  Rapora dahil edilecek personelleri belirleyin.
                  {activeFilterCount > 0 ? ` ${activeFilterCount} filtre etkin.` : ' Filtre seçilmezse tüm kapsam dahildir.'}
                </p>
              </div>
            </div>
            <div className="form-grid">
              <label className="span-2">
                Rapor başlığı
                <input
                  value={config.title}
                  onChange={(e) => set('title', e.target.value)}
                  maxLength={200}
                  placeholder="Örn. Bilim Şehitkamil aktif personel listesi"
                />
              </label>
              <label>
                Arama
                <input
                  value={config.search}
                  onChange={(e) => set('search', e.target.value)}
                  placeholder="Ad, sicil, unvan…"
                />
              </label>
              <label>
                Durum
                <select value={config.status} onChange={(e) => set('status', e.target.value)}>
                  <option value="">Tümü</option>
                  {(options?.statuses ?? []).map((s) => (
                    <option key={s.value} value={s.value}>
                      {s.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Birim
                <select value={config.unitId} onChange={(e) => set('unitId', e.target.value)}>
                  <option value="">Tümü</option>
                  {(options?.units ?? []).map((u) => (
                    <option key={u.id} value={u.id}>
                      {u.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="checkbox-label report-inline-check">
                <input
                  type="checkbox"
                  checked={config.includeSubUnits}
                  onChange={(e) => set('includeSubUnits', e.target.checked)}
                  disabled={!config.unitId}
                />
                Alt birimler dahil
              </label>
              <label>
                Tesis
                <select value={config.facilityId} onChange={(e) => set('facilityId', e.target.value)}>
                  <option value="">Tümü</option>
                  {(options?.facilities ?? []).map((f) => (
                    <option key={f.id} value={f.id}>
                      {f.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Unvan
                <select value={config.jobTitleId} onChange={(e) => set('jobTitleId', e.target.value)}>
                  <option value="">Tümü</option>
                  {(options?.jobTitles ?? []).map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Fiili görev
                <select value={config.jobDutyId} onChange={(e) => set('jobDutyId', e.target.value)}>
                  <option value="">Tümü</option>
                  {(options?.duties ?? []).map((d) => (
                    <option key={d.id} value={d.id}>
                      {d.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                İstihdam türü
                <select
                  value={config.employmentTypeId}
                  onChange={(e) => set('employmentTypeId', e.target.value)}
                >
                  <option value="">Tümü</option>
                  {(options?.employmentTypes ?? []).map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Eğitim seviyesi
                <select
                  value={config.educationLevel}
                  onChange={(e) => set('educationLevel', e.target.value)}
                >
                  <option value="">Tümü</option>
                  {(options?.educationLevels ?? []).map((l) => (
                    <option key={l.value} value={l.value}>
                      {l.label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Yetkinlik
                <select value={config.skillId} onChange={(e) => set('skillId', e.target.value)}>
                  <option value="">Tümü</option>
                  {(options?.skills ?? []).map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                İşe giriş yılı (başlangıç)
                <input
                  type="number"
                  min={1950}
                  max={2100}
                  value={config.hireYearFrom}
                  onChange={(e) => set('hireYearFrom', e.target.value)}
                />
              </label>
              <label>
                İşe giriş yılı (bitiş)
                <input
                  type="number"
                  min={1950}
                  max={2100}
                  value={config.hireYearTo}
                  onChange={(e) => set('hireYearTo', e.target.value)}
                />
              </label>
              <label>
                Hareket tarihi (başlangıç)
                <input
                  type="date"
                  value={config.movementFrom}
                  onChange={(e) => set('movementFrom', e.target.value)}
                />
              </label>
              <label>
                Hareket tarihi (bitiş)
                <input
                  type="date"
                  value={config.movementTo}
                  onChange={(e) => set('movementTo', e.target.value)}
                />
              </label>
            </div>
            <div className="report-flags">
              {(
                [
                  ['incompleteProfileOnly', 'Eksik profil bilgisi olanlar'],
                  ['missingSkillsOnly', 'Yetkinliği girilmemiş olanlar'],
                  ['hasSpecialConditionOnly', 'Özel durumu olanlar'],
                  ['hasCertificatesOnly', 'Sertifikası olanlar'],
                  ['missingCertificatesOnly', 'Sertifikası olmayanlar'],
                  ['hasMovementsOnly', 'Görev yeri/görevi değişenler'],
                ] as const
              ).map(([key, label]) => (
                <label key={key} className={`report-flag${config[key] ? ' is-on' : ''}`}>
                  <input
                    type="checkbox"
                    checked={config[key]}
                    onChange={(e) => set(key, e.target.checked)}
                  />
                  {label}
                </label>
              ))}
            </div>
          </div>

          <div className="report-step">
            <div className="report-step-head">
              <span className="report-step-no">2</span>
              <div>
                <h3>Gösterilecek sütunlar</h3>
                <p className="muted small">{config.columns.length} sütun seçili — tıklayarak ekleyin / çıkarın.</p>
              </div>
            </div>
            <div className="report-columns">
              {columnGroups.map(([group, cols]) => (
                <div key={group} className="report-column-group">
                  <span className="report-column-group-label">{group}</span>
                  <div className="report-column-options">
                    {cols.map((c) => {
                      const on = config.columns.includes(c.key)
                      return (
                        <button
                          key={c.key}
                          type="button"
                          className={`report-col-pill${on ? ' is-on' : ''}`}
                          aria-pressed={on}
                          onClick={() => toggleColumn(c.key)}
                        >
                          {on ? '✓ ' : ''}
                          {c.label}
                        </button>
                      )
                    })}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="report-step">
            <div className="report-step-head">
              <span className="report-step-no">3</span>
              <div>
                <h3>Gruplama ve sıralama</h3>
                <p className="muted small">Tablo düzenini belirleyin.</p>
              </div>
            </div>
            <div className="form-grid report-shape">
              <label>
                Gruplama
                <select value={config.groupBy} onChange={(e) => set('groupBy', e.target.value)}>
                  <option value="">Gruplama yok</option>
                  {columns
                    .filter((c) => config.columns.includes(c.key))
                    .map((c) => (
                      <option key={c.key} value={c.key}>
                        {c.label}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                Sıralama
                <select
                  value={config.orderByColumn}
                  onChange={(e) => set('orderByColumn', e.target.value)}
                >
                  <option value="">Soyad / Ad (varsayılan)</option>
                  {columns
                    .filter((c) => config.columns.includes(c.key))
                    .map((c) => (
                      <option key={c.key} value={c.key}>
                        {c.label}
                      </option>
                    ))}
                </select>
              </label>
              <label>
                Sıralama yönü
                <select
                  value={config.orderByDesc ? 'desc' : 'asc'}
                  onChange={(e) => set('orderByDesc', e.target.value === 'desc')}
                  disabled={!config.orderByColumn}
                >
                  <option value="asc">Artan (A→Z)</option>
                  <option value="desc">Azalan (Z→A)</option>
                </select>
              </label>
            </div>
          </div>

          <div className="report-builder-footer">
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setConfig(emptyConfig())}
              disabled={running}
            >
              Temizle
            </button>
            <div className="report-builder-footer-main">
              <button type="button" className="btn-secondary" onClick={saveCurrent}>
                Raporu kaydet
              </button>
              <button
                type="button"
                className="btn-primary"
                disabled={running || config.columns.length === 0}
                onClick={() => void run(config)}
              >
                {running ? 'Oluşturuluyor…' : 'Raporu oluştur'}
              </button>
            </div>
          </div>
        </section>
      )}

      {view === 'result' && result && (
        <section className="panel report-result report-print-area">
          <div className="report-result-head">
            <div className="report-result-title">
              <p className="org-eyebrow">Rapor sonucu</p>
              <h2>{result.title}</h2>
              <p className="muted small">
                {new Date(result.generatedAtUtc).toLocaleString('tr-TR')} · Oluşturan:{' '}
                {result.generatedBy} · {result.totalCount} kayıt
                {result.groupByLabel ? ` · Gruplama: ${result.groupByLabel}` : ''}
              </p>
              <div className="report-filter-chips">
                {result.appliedFilters.length === 0 ? (
                  <span className="skill-chip empty">Filtre yok (kapsam içi tümü)</span>
                ) : (
                  result.appliedFilters.map((f) => (
                    <span key={f} className="skill-chip">
                      {f}
                    </span>
                  ))
                )}
              </div>
            </div>
            <div className="report-result-actions">
              <button type="button" className="btn-secondary" onClick={() => setView('builder')}>
                Tasarımcıda düzenle
              </button>
              {canExportExcel && (
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={exportBusy !== null}
                  onClick={() => void onExport('excel')}
                >
                  {exportBusy === 'excel' ? 'Hazırlanıyor…' : 'Excel'}
                </button>
              )}
              {canExportPdf && (
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={exportBusy !== null || config.columns.length > 12}
                  title={config.columns.length > 12 ? 'PDF için en fazla 12 sütun' : undefined}
                  onClick={() => void onExport('pdf')}
                >
                  {exportBusy === 'pdf' ? 'Hazırlanıyor…' : 'PDF'}
                </button>
              )}
              <button
                type="button"
                className="btn-secondary"
                disabled={exportBusy !== null}
                onClick={() => void onExport('csv')}
              >
                CSV
              </button>
              <button type="button" className="btn-secondary" onClick={() => window.print()}>
                Yazdır
              </button>
            </div>
          </div>

          <div className="report-table-toolbar">
            <input
              type="search"
              placeholder="Tablo içinde ara…"
              value={quickFilter}
              onChange={(e) => setQuickFilter(e.target.value)}
            />
            {quickFilter.trim() && result.rows.length !== displayRows.length && (
              <span className="muted small">
                {displayRows.length} / {result.rows.length} kayıt gösteriliyor
              </span>
            )}
            {clientSort && (
              <button type="button" className="report-clear-sort" onClick={() => setClientSort(null)}>
                Sıralamayı sıfırla
              </button>
            )}
          </div>

          <div className="report-table-wrap">
            <table className="data-table report-table">
              <thead>
                <tr>
                  {result.columns.map((c, i) => (
                    <th
                      key={c.key}
                      onClick={() =>
                        setClientSort((s) =>
                          s?.index === i
                            ? s.desc
                              ? null
                              : { index: i, desc: true }
                            : { index: i, desc: false },
                        )
                      }
                      className="report-th"
                      title="Sıralamak için tıklayın"
                    >
                      {c.label}
                      {clientSort?.index === i ? (clientSort.desc ? ' ▾' : ' ▴') : ''}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {displayRows.length === 0 && (
                  <tr>
                    <td colSpan={result.columns.length} className="muted">
                      Kayıt bulunamadı.
                    </td>
                  </tr>
                )}
                {displayRows.map((row, ri) => {
                  const cells = row.map((cell, ci) => <td key={ci}>{cell ?? '—'}</td>)
                  if (groupIndex >= 0) {
                    const value = row[groupIndex] ?? '—'
                    const prev = ri > 0 ? (displayRows[ri - 1][groupIndex] ?? '—') : null
                    if (value !== prev) {
                      const count = displayRows.filter(
                        (r) => (r[groupIndex] ?? '—') === value,
                      ).length
                      return (
                        <FragmentRow
                          key={`g-${ri}`}
                          groupLabel={`${result.groupByLabel}: ${value} (${count} kayıt)`}
                          colSpan={result.columns.length}
                          cells={cells}
                        />
                      )
                    }
                  }
                  return <tr key={ri}>{cells}</tr>
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}

function FragmentRow({
  groupLabel,
  colSpan,
  cells,
}: {
  groupLabel: string
  colSpan: number
  cells: ReactNode
}) {
  return (
    <>
      <tr className="report-group-row">
        <td colSpan={colSpan}>{groupLabel}</td>
      </tr>
      <tr>{cells}</tr>
    </>
  )
}
