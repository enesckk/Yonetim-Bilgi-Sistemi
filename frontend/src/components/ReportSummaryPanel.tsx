import { useEffect, useState } from 'react'
import { downloadSummaryWord, getReportSummary, type ReportSummary, type SummaryKind, type SummaryType } from '@/api/reportSummariesApi'
import { currentEventYear } from './EventYearOverview'

const num = (value: number) => value.toLocaleString('tr-TR')
export function ReportSummaryPanel({ type }: { type: SummaryType }) {
  const current = currentEventYear()
  const [from, setFrom] = useState(`${current}-01-01`)
  const [to, setTo] = useState(`${current}-12-31`)
  const [kind, setKind] = useState<SummaryKind>('all')
  const [data, setData] = useState<ReportSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  const facility = type === 'facility'
  const filter = { fromDate: from, toDate: to, kind }
  useEffect(() => {
    const controller = new AbortController()
    setData(null); setError(''); setLoading(true)
    if (!from || !to || from > to) {
      setError('Başlangıç ve bitiş dönemini kontrol edin.'); setLoading(false)
      return () => controller.abort()
    }
    void getReportSummary(type, { fromDate: from, toDate: to, kind }, controller.signal).then(value => { if (!controller.signal.aborted) setData(value) }).catch((e: unknown) => {
      if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Rapor alınamadı.')
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [type, from, to, kind, retry])
  const selectPeriod = (value: string) => {
    if (!value) return
    setFrom(`${value === 'all' ? '2024' : value}-01-01`)
    setTo(`${value === 'all' ? current : value}-12-31`)
  }
  async function download() {
    if (!data || busy || loading) return
    setBusy(true); setError('')
    try { await downloadSummaryWord(type, filter) } catch (e) { setError(e instanceof Error ? e.message : 'Word dosyası indirilemedi.') } finally { setBusy(false) }
  }
  return <section className="report-summary-panel" aria-label={facility ? 'Tesis raporu özeti' : 'Etkinlik ve proje raporu'}>
    <div className="report-workspace-heading"><div><h2>{facility ? 'Tesis raporu özeti' : 'Etkinlik ve proje raporu'}</h2><p>{facility ? 'Aylık tesis kullanımını ve tesislere göre katılım dağılımını inceleyin.' : 'Gerçekleşen programları, projeleri ve kayıtlı katılımları birlikte inceleyin.'}</p></div><button className="btn-primary" type="button" disabled={!data || loading || busy || data.recordCount === 0} onClick={() => void download()}>{busy ? 'Word hazırlanıyor…' : 'Word indir'}</button></div>
    <div className="report-period-bar">
      <label>Dönem<select aria-label="Rapor dönemi" value={from.slice(0, 4) === to.slice(0, 4) && from.endsWith('-01-01') && to.endsWith('-12-31') ? from.slice(0, 4) : from === '2024-01-01' && to === `${current}-12-31` ? 'all' : ''} onChange={e => selectPeriod(e.target.value)}><option value="" disabled>Özel dönem</option><option value="all">Tüm dönemler · 2024–{current}</option>{Array.from({ length: current - 2024 + 1 }, (_, i) => current - i).map(y => <option key={y} value={y}>{y}</option>)}</select></label>
      <label>{facility ? 'Başlangıç ayı' : 'Başlangıç tarihi'}<input type={facility ? 'month' : 'date'} aria-label={facility ? 'Başlangıç ayı' : 'Başlangıç tarihi'} value={facility ? from.slice(0, 7) : from} min={facility ? '2017-01' : '2017-01-01'} max={facility ? '2100-12' : '2100-12-31'} onChange={e => setFrom(e.target.value ? facility ? `${e.target.value}-01` : e.target.value : '')} /></label>
      <label>{facility ? 'Bitiş ayı' : 'Bitiş tarihi'}<input type={facility ? 'month' : 'date'} aria-label={facility ? 'Bitiş ayı' : 'Bitiş tarihi'} value={facility ? to.slice(0, 7) : to} min={facility ? from.slice(0, 7) : from} max={facility ? '2100-12' : '2100-12-31'} onChange={e => { const v = e.target.value; if (!v) { setTo(''); return }; const [y, m] = v.split('-').map(Number); setTo(facility ? `${v}-${String(new Date(y, m, 0).getDate()).padStart(2, '0')}` : v) }} /></label>
      {!facility && <label>Kayıt türü<select aria-label="Özet kayıt türü" value={kind} onChange={e => setKind(e.target.value as SummaryKind)}><option value="all">Etkinlikler ve projeler</option><option value="event">Etkinlikler ve geziler</option><option value="project">Projeler</option></select></label>}
    </div>
    {error && <div className="form-error" role="alert">{error} <button className="btn-secondary" type="button" onClick={() => setRetry(n => n + 1)}>Tekrar dene</button></div>}
    {loading ? <div className="report-summary-loading" role="status">Dönem verileri hazırlanıyor…</div> : data && <>
      <div className="report-summary-metrics">
        <div className="is-primary"><span>Toplam kayıtlı katılım</span><strong>{num(data.attendance)}</strong><small>Seçilen dönem</small></div>
        <div><span>{facility ? 'Katılım kaydı bulunan tesis' : 'Yapılan etkinlik'}</span><strong>{num(facility ? data.rows.length : data.eventCount)}</strong><small>{facility ? 'Dönemde verisi bulunanlar' : 'Gezi ve programlar dahil'}</small></div>
        <div><span>{facility ? 'Aylık kaynak kaydı' : 'Tamamlanan proje'}</span><strong>{num(facility ? data.recordCount : data.projectCount)}</strong><small>{facility ? `${data.months.length} ayda kayıt var` : 'Seçilen dönemde'}</small></div>
      </div>
      <div className="report-distribution-heading"><h3>{facility ? 'Tesislere göre dağılım' : 'Programlara göre dağılım'}</h3><span>{num(data.rows.length)} {facility ? 'tesis' : 'program grubu'}</span></div>
      {data.rows.length ? <div className="report-summary-table-wrap"><table className="report-summary-table"><thead><tr><th>{facility ? 'Tesis' : 'Program'}</th><th>{facility ? 'Aylık kayıt' : 'Kayıt'}</th><th>Kayıtlı katılım</th><th>Dağılım</th></tr></thead><tbody>{data.rows.map(r => <tr key={r.key}><td><strong>{r.name}</strong><small className="report-row-count">{num(r.recordCount)} {facility ? 'aylık kayıt' : 'kayıt'}</small>{!facility && <small>{r.category}</small>}</td><td>{num(r.recordCount)}</td><td className="report-count">{r.attendance === null ? <span className="muted">Belirtilmemiş</span> : num(r.attendance)}</td><td><div className="report-share"><span aria-hidden="true"><i style={{ width: `${data.attendance ? Math.min(100, (r.attendance ?? 0) / data.attendance * 100) : 0}%` }} /></span><small>{data.attendance && r.attendance !== null ? `${(r.attendance / data.attendance * 100).toLocaleString('tr-TR', { maximumFractionDigits: 1 })}%` : '—'}</small></div></td></tr>)}</tbody></table></div> : <div className="activity-report-empty">Bu dönemde kayıt bulunmuyor. Başka bir yıl veya tarih aralığı seçebilirsiniz.</div>}
      <p className="report-source-note">{facility ? 'Kaynak sistemdeki aylık tesis kullanım kayıtları esas alınır. Aynı ayda bulunan ayrı kaynak kayıtları toplamda korunur.' : 'Başlangıç tarihi seçilen dönemde olan gerçekleşmiş kayıtlar esas alınır. Beklenen katılım, taslak ve iptal edilen kayıtlar dahil edilmez.'} Katılım toplamı tekil kişi sayısı değildir.{!facility && data.recordsWithAttendance < data.recordCount && ` ${num(data.recordCount - data.recordsWithAttendance)} kayıtta katılım bilgisi yok.`}{facility && data.sourceUpdatedAtUtc && ` Kaynak aktarımı: ${new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeZone: 'Europe/Istanbul' }).format(new Date(data.sourceUpdatedAtUtc))}.`}</p>
    </>}
  </section>
}
