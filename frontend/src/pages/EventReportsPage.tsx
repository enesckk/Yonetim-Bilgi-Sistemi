import { useCallback, useEffect, useRef, useState, type CSSProperties, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useConfirm } from '@/components/ConfirmDialog'
import { ApiClientError } from '@/api/client'
import { mediaSelectionError } from '@/api/eventMediaApi'
import {
  downloadActivityReport,
  generateActivityReport,
  getActivityReport,
  getReportAsset,
  listActivityReports,
  saveActivityReport,
  uploadReportAsset,
  type ActivityReportDetail,
  type ActivityReportInput,
  type ActivityReportSummary,
  type ReportAsset,
  type ReportDocument,
  type ReportPage,
} from '@/api/activityReportsApi'

const message = (e: unknown, fallback: string) =>
  e instanceof ApiClientError
    ? Object.values(e.validationErrors ?? {})
        .flat()
        .join(' ') || e.message
    : e instanceof Error
      ? e.message
      : fallback
const date = (value: string) => value.split('-').reverse().join('.')
const initialYear = new Date().getFullYear()
export function EventReportsPage() {
  const { hasPermission } = useAuth()
  const manage = hasPermission(PermissionCodes.EventsManage)
  const navigate = useNavigate()
  const [reports, setReports] = useState<ActivityReportSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [year, setYear] = useState(initialYear)
  const [input, setInput] = useState<ActivityReportInput>({
    title: `${initialYear} Faaliyet Raporu`,
    fromDate: `${initialYear}-01-01`,
    toDate: `${initialYear}-12-31`,
    dateBasis: 'event',
    kind: 'all',
  })
  const [reload, setReload] = useState(0)
  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    void listActivityReports()
      .then((rows) => {
        if (active) setReports(rows)
      })
      .catch((e) => {
        if (active) setError(message(e, 'Raporlar alınamadı.'))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [reload])
  async function generate(e: FormEvent) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError('')
    if (input.fromDate > input.toDate) {
      setError('Bitiş tarihi başlangıçtan önce olamaz.')
      setBusy(false)
      return
    }
    try {
      const r = await generateActivityReport(input)
      navigate(`/events/reports/${r.id}`)
    } catch (e) {
      setError(message(e, 'Rapor oluşturulamadı.'))
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="activity-reports-page">
      <section className="panel">
        <header className="activity-report-title">
          <div>
            <span className="activity-eyebrow">ETKİNLİK VE PROJELER</span>
            <h2>Raporlar</h2>
            <p>Tarih bazlı raporlar ve düzenlenebilir yıllık faaliyet raporları</p>
          </div>
          <span className="activity-report-chip">Otomatik oluştur · Düzenle · Sun</span>
        </header>
        {error && (
          <p className="form-error" role="alert">
            {error}{' '}
            <button type="button" className="btn-secondary" onClick={() => setReload((n) => n + 1)}>
              Tekrar dene
            </button>
          </p>
        )}
        {manage && (
          <form className="activity-report-generator" onSubmit={generate}>
            <div>
              <h3>Yeni rapor oluştur</h3>
              <p>
                Seçilen dönemdeki kayıtlar, açıklamalar, bilgi notları ve rapor için işaretlenen
                görseller otomatik alınır.
              </p>
            </div>
            <fieldset disabled={busy}>
              <div className="activity-filter-grid">
                <label>
                  Yıl
                  <select
                    value={year}
                    onChange={(e) => {
                      const y = Number(e.target.value)
                      setYear(y)
                      setInput((v) => ({
                        ...v,
                        title: `${y} Faaliyet Raporu`,
                        fromDate: `${y}-01-01`,
                        toDate: `${y}-12-31`,
                      }))
                    }}
                  >
                    {Array.from(
                      { length: initialYear - 2017 + 2 },
                      (_, i) => initialYear + 1 - i,
                    ).map((y) => (
                      <option key={y}>{y}</option>
                    ))}
                  </select>
                </label>
                <label>
                  Başlangıç tarihi
                  <input
                    type="date"
                    required
                    value={input.fromDate}
                    onChange={(e) => setInput((v) => ({ ...v, fromDate: e.target.value }))}
                  />
                </label>
                <label>
                  Bitiş tarihi
                  <input
                    type="date"
                    required
                    min={input.fromDate}
                    value={input.toDate}
                    onChange={(e) => setInput((v) => ({ ...v, toDate: e.target.value }))}
                  />
                </label>
                <label>
                  Kayıtlar
                  <select
                    value={input.kind}
                    onChange={(e) =>
                      setInput((v) => ({
                        ...v,
                        kind: e.target.value as ActivityReportInput['kind'],
                      }))
                    }
                  >
                    <option value="all">Etkinlikler ve projeler</option>
                    <option value="event">Etkinlikler ve geziler</option>
                    <option value="project">Yalnızca projeler</option>
                  </select>
                </label>
                <label>
                  Tarih ölçütü
                  <select
                    value={input.dateBasis}
                    onChange={(e) =>
                      setInput((v) => ({
                        ...v,
                        dateBasis: e.target.value as ActivityReportInput['dateBasis'],
                      }))
                    }
                  >
                    <option value="event">Etkinlik başlangıç tarihi</option>
                    <option value="record">Sisteme kayıt tarihi</option>
                  </select>
                </label>
                <label className="activity-report-name">
                  Rapor başlığı
                  <input
                    value={input.title}
                    required
                    maxLength={200}
                    onChange={(e) => setInput((v) => ({ ...v, title: e.target.value }))}
                  />
                </label>
              </div>
              <div className="activity-generator-footer">
                <p>Her oluşturma ayrı bir rapor kaydeder. Mevcut düzenlemeleriniz korunur.</p>
                <button className="btn-primary" type="submit">
                  {busy ? 'Kayıtlar ve görseller hazırlanıyor…' : 'Raporu otomatik oluştur'}
                </button>
              </div>
            </fieldset>
          </form>
        )}
        <div className="activity-report-section-head">
          <h3>Kayıtlı raporlar</h3>
          <span>{reports.length} rapor</span>
        </div>
        {loading ? (
          <p role="status">Raporlar yükleniyor…</p>
        ) : reports.length === 0 ? (
          <div className="activity-report-empty">
            Henüz kayıtlı rapor yok. Tarih aralığını seçerek ilk raporu oluşturabilirsiniz.
          </div>
        ) : (
          <div className="activity-report-list">
            {reports.map((r) => (
              <Link to={`/events/reports/${r.id}`} key={r.id} className="activity-report-list-item">
                <div className="activity-report-year">
                  {r.fromDate.slice(0, 4)}
                  <small>RAPOR</small>
                </div>
                <div>
                  <h4>{r.title}</h4>
                  <p>
                    {date(r.fromDate)} – {date(r.toDate)} ·{' '}
                    {r.dateBasis === 'event' ? 'Etkinlik tarihi' : 'Kayıt tarihi'}
                  </p>
                  <span>
                    {r.sourceCount.toLocaleString('tr-TR')} kaynak kayıt ·{' '}
                    {r.sourceAttendance.toLocaleString('tr-TR')} kayıtlı katılım
                  </span>
                </div>
                <strong>Raporu aç ›</strong>
              </Link>
            ))}
          </div>
        )}
        <p className="activity-report-footnote">
          Planlanan ve iptal edilen kayıtlar raporda durumlarıyla gösterilir. Katılım toplamı tekil
          kişi sayısı değildir; beklenen katılım toplama katılmaz.
        </p>
      </section>
    </div>
  )
}
function ReportImage({ reportId, asset }: { reportId: string; asset: ReportAsset }) {
  const [url, setUrl] = useState('')
  const [error, setError] = useState(false)
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let active = true
    let objectUrl = ''
    setUrl('')
    setError(false)
    void getReportAsset(reportId, asset.id)
      .then((blob) => {
        if (active) {
          objectUrl = URL.createObjectURL(blob)
          setUrl(objectUrl)
        }
      })
      .catch(() => {
        if (active) setError(true)
      })
    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [reportId, asset.id, retry])
  return error ? (
    <button type="button" className="activity-image-error" onClick={() => setRetry((n) => n + 1)}>
      Görsel açılamadı. Tekrar dene
    </button>
  ) : url ? (
    <img src={url} alt={asset.fileName} loading="lazy" onError={() => setError(true)} />
  ) : (
    <span className="activity-image-loading">Görsel yükleniyor…</span>
  )
}
function Paper({
  detail,
  page,
  index,
}: {
  detail: ActivityReportDetail
  page: ReportPage
  index: number
}) {
  const doc = detail.document
  const paper = useRef<HTMLElement>(null)
  const [paperWidth, setPaperWidth] = useState(0)
  useEffect(() => {
    if (!paper.current) return
    const observer = new ResizeObserver((entries) => {
      const entry = entries[0]
      if (entry) setPaperWidth(entry.target.getBoundingClientRect().width)
    })
    observer.observe(paper.current)
    return () => observer.disconnect()
  }, [])
  return (
    <article
      ref={paper}
      className="activity-paper"
      style={
        {
          '--report-accent': doc.accent,
          ...(paperWidth ? { minHeight: (paperWidth * 297) / 210 } : {}),
        } as CSSProperties
      }
      aria-label={`Rapor sayfası ${index + 1}`}
    >
      <header>
        <strong>{doc.organization}</strong>
        <span>{doc.department}</span>
      </header>
      <div className="activity-paper-content">
        <span className="activity-paper-kicker">
          {detail.report.fromDate.slice(0, 4)} FAALİYET RAPORU
        </span>
        <h2>{page.title}</h2>
        {page.meta && <p className="activity-paper-meta">{page.meta}</p>}
        {page.layout !== 'text' && page.imageIds.length > 0 && (
          <div className={`activity-paper-images layout-${page.layout}`}>
            {page.imageIds.map((id) => {
              const asset = detail.assets.find((a) => a.id === id)
              return asset ? (
                <div key={id}>
                  <ReportImage reportId={detail.report.id} asset={asset} />
                </div>
              ) : null
            })}
          </div>
        )}
        <div className="activity-paper-body">
          {page.body.split('\n').map((line, i) => (
            <p key={i}>{line || '\u00a0'}</p>
          ))}
        </div>
      </div>
      {page.showMonthlyChart &&
        detail.report.fromDate.slice(0, 4) === detail.report.toDate.slice(0, 4) && (
          <section className="activity-monthly-chart">
            <h3>
              {detail.report.dateBasis === 'record'
                ? 'Kayıt tarihine göre aylık dağılım'
                : 'Etkinlik tarihlerine göre aylık kayıt dağılımı'}
            </h3>
            <div>
              {[
                'Oca',
                'Şub',
                'Mar',
                'Nis',
                'May',
                'Haz',
                'Tem',
                'Ağu',
                'Eyl',
                'Eki',
                'Kas',
                'Ara',
              ].map((label, month) => {
                const values = Array.from(
                  { length: 12 },
                  (_, n) =>
                    doc.sources.filter((s) => {
                      const d = new Date(
                        new Date(
                          detail.report.dateBasis === 'record'
                            ? (s.recordedAtUtc ?? s.dateUtc)
                            : s.dateUtc,
                        ).getTime() +
                          3 * 60 * 60 * 1000,
                      )
                      return (
                        d.getUTCFullYear() === Number(detail.report.fromDate.slice(0, 4)) &&
                        d.getUTCMonth() === n
                      )
                    }).length,
                )
                return (
                  <div key={label}>
                    <strong>{values[month]}</strong>
                    <span
                      style={{
                        height: `${Math.max(2, (values[month] / Math.max(1, ...values)) * 80)}px`,
                        background: doc.accent,
                      }}
                    />
                    <small>{label}</small>
                  </div>
                )
              })}
            </div>
          </section>
        )}
      <footer>
        <span>{detail.report.title}</span>
        <strong>{index + 1}</strong>
      </footer>
    </article>
  )
}
export function ActivityReportEditorPage() {
  const { reportId } = useParams()
  const navigate = useNavigate()
  const confirm = useConfirm()
  const { hasPermission } = useAuth()
  const manage = hasPermission(PermissionCodes.EventsManage)
  const [detail, setDetail] = useState<ActivityReportDetail | null>(null)
  const [index, setIndex] = useState(0)
  const [mode, setMode] = useState<'view' | 'edit' | 'present'>('view')
  const [dirty, setDirty] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [reload, setReload] = useState(0)
  const [notice, setNotice] = useState('')
  const [assetPickerOpen, setAssetPickerOpen] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)
  const replaceAt = useRef<number | null>(null)
  const presentation = useRef<HTMLDivElement>(null)
  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    setDirty(false)
    setIndex(0)
    void getActivityReport(reportId!)
      .then((r) => {
        if (active) setDetail(r)
      })
      .catch((e) => {
        if (active) setError(message(e, 'Rapor alınamadı.'))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [reportId, reload])
  useEffect(() => {
    if (!dirty) return
    const unload = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      e.returnValue = ''
    }
    window.addEventListener('beforeunload', unload)
    return () => window.removeEventListener('beforeunload', unload)
  }, [dirty])
  useEffect(() => {
    if (!dirty) return
    const guard = (event: MouseEvent) => {
      const link =
        event.target instanceof Element
          ? (event.target.closest('a[href]') as HTMLAnchorElement | null)
          : null
      if (
        !link ||
        link.target === '_blank' ||
        link.hasAttribute('download') ||
        event.ctrlKey ||
        event.metaKey ||
        event.shiftKey
      )
        return
      const url = new URL(link.href)
      if (
        url.origin !== location.origin ||
        (url.pathname === location.pathname && url.search === location.search)
      )
        return
      event.preventDefault()
      event.stopImmediatePropagation()
      void confirm({
        title: 'Kaydedilmemiş düzenlemeler',
        message: 'Rapordaki düzenlemeler henüz kaydedilmedi. Kaydetmeden ayrılsın mı?',
        confirmLabel: 'Kaydetmeden ayrıl',
        tone: 'danger',
      }).then((ok) => {
        if (ok) navigate(url.pathname + url.search + url.hash)
      })
    }
    document.addEventListener('click', guard, true)
    return () => document.removeEventListener('click', guard, true)
  }, [dirty, confirm, navigate])
  useEffect(() => {
    if (mode !== 'present') return
    const close = () => {
      if (!document.fullscreenElement) setMode('view')
    }
    document.addEventListener('fullscreenchange', close)
    return () => document.removeEventListener('fullscreenchange', close)
  }, [mode])
  const pages = detail?.document.pages ?? []
  const selected = pages[index] ?? pages[0]
  const go = useCallback(
    (step: number) => setIndex((n) => Math.max(0, Math.min(pages.length - 1, n + step))),
    [pages.length],
  )
  useEffect(() => {
    if (mode !== 'present') return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setMode('view')
        if (document.fullscreenElement) void document.exitFullscreen()
      }
      if (['ArrowRight', 'PageDown'].includes(e.key)) {
        e.preventDefault()
        go(1)
      }
      if (['ArrowLeft', 'PageUp'].includes(e.key)) {
        e.preventDefault()
        go(-1)
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [mode, go])
  useEffect(() => {
    if (mode === 'present') document.body.classList.add('activity-presenting')
    else document.body.classList.remove('activity-presenting')
    return () => document.body.classList.remove('activity-presenting')
  }, [mode])
  function changePage(values: Partial<ReportPage>) {
    if (!detail || !selected) return
    setDetail({
      ...detail,
      document: {
        ...detail.document,
        pages: pages.map((p) => (p.id === selected.id ? { ...p, ...values } : p)),
      },
    })
    setDirty(true)
    setNotice('')
  }
  function changeDoc(values: Partial<ReportDocument>) {
    if (!detail) return
    setDetail({ ...detail, document: { ...detail.document, ...values } })
    setDirty(true)
    setNotice('')
  }
  async function save() {
    if (!detail) return null
    const next = await saveActivityReport(detail)
    setDetail(next)
    setDirty(false)
    setNotice('Rapor kaydedildi.')
    return next
  }
  async function perform(action: () => Promise<unknown>) {
    if (busy) return
    setBusy(true)
    setError('')
    try {
      await action()
    } catch (e) {
      setError(message(e, 'İşlem tamamlanamadı.'))
    } finally {
      setBusy(false)
    }
  }
  async function exportFile(kind: 'pdf' | 'excel') {
    await perform(async () => {
      if (dirty && manage) await save()
      await downloadActivityReport(reportId!, kind)
    })
  }
  async function newCopy() {
    await perform(async () => {
      if (dirty) await save()
      const r = detail!.report
      const created = await generateActivityReport({
        title: r.title,
        fromDate: r.fromDate,
        toDate: r.toDate,
        dateBasis: r.dateBasis,
        kind: r.kind,
      })
      navigate(`/events/reports/${created.id}`)
    })
  }
  async function removePage() {
    if (!selected || pages.length <= 1) return
    if (
      !(await confirm({
        title: 'Sayfayı rapordan kaldır',
        message: 'Yalnızca rapordaki bu sayfa kaldırılır. Etkinlik kaydı korunur.',
        confirmLabel: 'Sayfayı kaldır',
        tone: 'danger',
      }))
    )
      return
    changeDoc({ pages: pages.filter((p) => p.id !== selected.id) })
    setIndex((n) => Math.max(0, n - 1))
  }
  function move(step: number) {
    const target = index + step
    if (target < 0 || target >= pages.length) return
    const next = [...pages]
    ;[next[index], next[target]] = [next[target], next[index]]
    changeDoc({ pages: next })
    setIndex(target)
  }
  function addPage() {
    const p: ReportPage = {
      id: crypto.randomUUID(),
      title: 'Yeni sayfa',
      body: '',
      meta: '',
      layout: 'pair',
      imageIds: [],
    }
    changeDoc({ pages: [...pages, p] })
    setIndex(pages.length)
  }
  function pickAsset(id: string) {
    const ids = selected.imageIds.includes(id)
      ? selected.imageIds.filter((x) => x !== id)
      : [...selected.imageIds, id]
    if (ids.length > 4) {
      setError('Bir sayfada en fazla 4 görsel kullanabilirsiniz.')
      return
    }
    changePage({ imageIds: ids, layout: selected.layout === 'text' ? 'pair' : selected.layout })
  }
  async function upload(file: File) {
    const problem = mediaSelectionError([file])
    if (problem) {
      setError(problem)
      return
    }
    const pageId = selected.id
    const replacement = replaceAt.current
    await perform(async () => {
      const asset = await uploadReportAsset(reportId!, file)
      setDetail((current) => {
        if (!current) return current
        const next = current.document.pages.map((page) => {
          if (page.id !== pageId) return page
          const ids = [...page.imageIds]
          if (replacement !== null) ids[replacement] = asset.id
          else if (ids.length < 4) ids.push(asset.id)
          return {
            ...page,
            imageIds: ids,
            layout: page.layout === 'text' ? ('pair' as const) : page.layout,
          }
        })
        return {
          ...current,
          assets: [...current.assets, asset],
          document: { ...current.document, pages: next },
        }
      })
      setDirty(true)
      setNotice('Görsel eklendi. Raporu kaydedin.')
    })
  }

  async function back() {
    if (
      dirty &&
      !(await confirm({
        title: 'Kaydedilmemiş düzenlemeler',
        message: 'Kaydedilmemiş düzenlemeler var. Kaydetmeden rapor listesine dönülsün mü?',
        confirmLabel: 'Kaydetmeden dön',
        tone: 'danger',
      }))
    )
      return
    navigate('/events/reports')
  }
  if (loading)
    return (
      <div className="panel" role="status">
        Rapor hazırlanıyor…
      </div>
    )
  if (!detail || !selected)
    return (
      <div className="panel">
        <p className="form-error" role="alert">
          {error || 'Rapor bulunamadı.'}
        </p>
        <button className="btn-secondary" onClick={() => setReload((n) => n + 1)}>
          Tekrar dene
        </button>
        <Link to="/events/reports">Raporlara dön</Link>
      </div>
    )
  return (
    <div
      className={`activity-report-editor ${mode === 'present' ? 'is-presenting' : ''}`}
      ref={presentation}
    >
      <header className="activity-editor-toolbar">
        <div>
          <button type="button" className="activity-back" onClick={() => void back()}>
            ‹ Raporlar
          </button>
          <h2>{detail.report.title}</h2>
          <span>
            {pages.length} sayfa · {detail.report.sourceCount} kaynak kayıt{' '}
            {dirty ? '· Kaydedilmemiş değişiklikler' : ''}
          </span>
        </div>
        <div className="activity-editor-actions">
          <button
            type="button"
            className="btn-secondary"
            disabled={busy}
            onClick={() => void exportFile('excel')}
          >
            Excel indir
          </button>
          <button
            type="button"
            className="btn-secondary"
            disabled={busy}
            onClick={() => void exportFile('pdf')}
          >
            {dirty ? 'Kaydet ve PDF indir' : 'PDF indir'}
          </button>
          {manage && (
            <button
              type="button"
              className="btn-primary"
              disabled={busy || !dirty}
              onClick={() => void perform(save)}
            >
              {busy ? 'İşleniyor…' : 'Kaydet'}
            </button>
          )}
        </div>
      </header>
      <div className="activity-mode-bar">
        <div role="group" aria-label="Rapor görünümü">
          <button
            type="button"
            className={mode === 'view' ? 'active' : ''}
            onClick={() => setMode('view')}
          >
            Önizleme
          </button>
          {manage && (
            <button
              type="button"
              className={mode === 'edit' ? 'active' : ''}
              onClick={() => setMode('edit')}
            >
              Düzenleme
            </button>
          )}
          <button
            type="button"
            className={mode === 'present' ? 'active' : ''}
            onClick={() => {
              setMode('present')
              void presentation.current?.requestFullscreen?.().catch(() => {})
            }}
          >
            Sunum
          </button>
        </div>
        {manage && (
          <button
            type="button"
            className="activity-copy"
            disabled={busy}
            onClick={() => void newCopy()}
          >
            Güncel verilerle yeni kopya
          </button>
        )}
      </div>
      {error && (
        <p className="form-error activity-editor-notice" role="alert">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="activity-editor-notice">
          {notice}
        </p>
      )}
      {detail.document.recordsWithoutImages > 0 && mode !== 'present' && (
        <details className="activity-source-warning">
          <summary>
            {detail.document.recordsWithoutImages} kaynak kayıtta rapor için görsel seçilmemiş
          </summary>
          <p>
            Etkinlik detayındaki “Faaliyet raporunda kullan” kutusunu işaretleyin; ardından güncel
            verilerle yeni kopya oluşturun. Bu rapora Düzenleme bölümünden ayrıca görsel
            ekleyebilirsiniz.
          </p>
          <p>
            Bu rapor kaydedilmiş bir kopyadır. Etkinliklerdeki sonraki değişiklikler mevcut raporun
            üzerine yazılmaz.
          </p>
        </details>
      )}
      <div className={`activity-workspace mode-${mode}`}>
        {mode !== 'present' && (
          <aside className="activity-page-list">
            <div>
              <h3>Sayfalar</h3>
              {manage && (
                <button type="button" disabled={busy} className="btn-secondary" onClick={addPage}>
                  + Sayfa
                </button>
              )}
            </div>
            <nav aria-label="Rapor sayfaları">
              {pages.map((p, i) => (
                <button
                  type="button"
                  key={p.id}
                  className={i === index ? 'selected' : ''}
                  onClick={() => setIndex(i)}
                >
                  <span>{i + 1}</span>
                  <strong>{p.title || 'Başlıksız sayfa'}</strong>
                  {p.imageIds.length > 0 && <small>{p.imageIds.length} görsel</small>}
                </button>
              ))}
            </nav>
          </aside>
        )}
        {mode === 'edit' && manage && (
          <section className="activity-edit-controls">
            <fieldset disabled={busy}>
              <details className="activity-theme-settings">
                <summary>Rapor başlığı ve kurumsal görünüm</summary>
                <label>
                  Rapor başlığı
                  <input
                    value={detail.report.title}
                    maxLength={200}
                    onChange={(e) => {
                      setDetail({ ...detail, report: { ...detail.report, title: e.target.value } })
                      setDirty(true)
                    }}
                  />
                </label>
                <label>
                  Kurum
                  <input
                    value={detail.document.organization}
                    maxLength={200}
                    onChange={(e) => changeDoc({ organization: e.target.value })}
                  />
                </label>
                <label>
                  Müdürlük
                  <input
                    value={detail.document.department}
                    maxLength={200}
                    onChange={(e) => changeDoc({ department: e.target.value })}
                  />
                </label>
                <label>
                  Tema rengi
                  <input
                    type="color"
                    value={detail.document.accent}
                    onChange={(e) => changeDoc({ accent: e.target.value })}
                  />
                </label>
              </details>
              <div className="activity-edit-page-heading">
                <h3>Sayfa {index + 1}</h3>
                <div>
                  <button
                    type="button"
                    className="btn-secondary"
                    disabled={index === 0}
                    onClick={() => move(-1)}
                    aria-label="Sayfayı yukarı taşı"
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="btn-secondary"
                    disabled={index === pages.length - 1}
                    onClick={() => move(1)}
                    aria-label="Sayfayı aşağı taşı"
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    className="activity-delete-page"
                    disabled={pages.length <= 1}
                    onClick={() => void removePage()}
                  >
                    Sayfayı kaldır
                  </button>
                </div>
              </div>
              <label>
                Sayfa başlığı
                <input
                  maxLength={400}
                  value={selected.title}
                  onChange={(e) => changePage({ title: e.target.value })}
                />
              </label>
              <label>
                Tarih / yer / alt başlık
                <textarea
                  rows={2}
                  maxLength={3000}
                  aria-label="Tarih ve yer metni"
                  value={selected.meta}
                  onChange={(e) => changePage({ meta: e.target.value })}
                />
              </label>
              <label>
                Açıklama metni
                <textarea
                  rows={12}
                  maxLength={20000}
                  aria-label="Rapor sayfası açıklaması"
                  value={selected.body}
                  onChange={(e) => changePage({ body: e.target.value })}
                />
              </label>
              <label>
                Sayfa düzeni
                <select
                  value={selected.layout}
                  onChange={(e) => changePage({ layout: e.target.value as ReportPage['layout'] })}
                >
                  <option value="pair">Görseller yan yana</option>
                  <option value="single">Görseller alt alta</option>
                  <option value="text">Yalnızca metin</option>
                </select>
              </label>
              <label className="activity-chart-choice">
                <input
                  type="checkbox"
                  checked={selected.showMonthlyChart ?? false}
                  onChange={(e) => changePage({ showMonthlyChart: e.target.checked })}
                />{' '}
                Aylık kayıt dağılımı grafiğini göster
              </label>
              <section className="activity-page-images">
                <h4>Sayfadaki görseller</h4>
                {selected.imageIds.length === 0 && <p>Bu sayfada görsel yok.</p>}
                {selected.imageIds.map((id, i) => {
                  const a = detail.assets.find((a) => a.id === id)
                  return (
                    a && (
                      <div key={id} className="activity-image-slot">
                        <ReportImage reportId={reportId!} asset={a} />
                        <span>{a.fileName}</span>
                        <button
                          type="button"
                          className="btn-secondary"
                          onClick={() => {
                            replaceAt.current = i
                            fileInput.current?.click()
                          }}
                        >
                          Değiştir
                        </button>
                        <button
                          type="button"
                          className="activity-delete-page"
                          onClick={() =>
                            changePage({ imageIds: selected.imageIds.filter((x) => x !== id) })
                          }
                        >
                          Kaldır
                        </button>
                      </div>
                    )
                  )
                })}
                <button
                  type="button"
                  className="btn-secondary"
                  disabled={selected.imageIds.length >= 4}
                  onClick={() => {
                    replaceAt.current = null
                    fileInput.current?.click()
                  }}
                >
                  + Görsel yükle
                </button>
                <input
                  ref={fileInput}
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  aria-label="Rapora görsel yükle"
                  className="activity-hidden-file"
                  onChange={(e) => {
                    const file = e.target.files?.[0]
                    if (file) void upload(file)
                    e.target.value = ''
                  }}
                />
                {detail.assets.length > 0 && (
                  <details onToggle={(e) => setAssetPickerOpen(e.currentTarget.open)}>
                    <summary>Rapor arşivinden görsel seç ({detail.assets.length})</summary>
                    {assetPickerOpen && (
                      <div className="activity-asset-picker">
                        {detail.assets.map((a) => (
                          <label key={a.id}>
                            <ReportImage reportId={reportId!} asset={a} />
                            <span>
                              <input
                                type="checkbox"
                                checked={selected.imageIds.includes(a.id)}
                                onChange={() => pickAsset(a.id)}
                              />
                              {a.fileName}
                            </span>
                          </label>
                        ))}
                      </div>
                    )}
                  </details>
                )}
                <p>JPG, PNG, WebP · Dosya başına 8 MB. Bir sayfada en fazla 4 görsel.</p>
              </section>
              {selected.sourceEventId && (
                <Link to={`/events/${selected.sourceEventId}`} target="_blank" rel="noreferrer">
                  Kaynak etkinliği / projeyi aç ↗
                </Link>
              )}
            </fieldset>
          </section>
        )}
        <div className="activity-preview-stage">
          <Paper detail={detail} page={selected} index={index} />
        </div>
      </div>
      <div className="activity-page-navigation">
        <button
          type="button"
          className="btn-secondary"
          disabled={index === 0}
          onClick={() => go(-1)}
        >
          ‹ Önceki
        </button>
        <span>
          {index + 1} / {pages.length}
        </span>
        <button
          type="button"
          className="btn-secondary"
          disabled={index === pages.length - 1}
          onClick={() => go(1)}
        >
          Sonraki ›
        </button>
        {mode === 'present' && (
          <button
            type="button"
            className="btn-secondary"
            onClick={() => {
              setMode('view')
              if (document.fullscreenElement) void document.exitFullscreen()
            }}
          >
            Sunumu kapat
          </button>
        )}
      </div>
    </div>
  )
}
