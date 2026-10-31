import { useEffect, useState } from 'react'
import { fetchEventYearSummary, type EventYearSummary } from '@/api/eventsApi'

export const currentEventYear = () => Number(new Intl.DateTimeFormat('en', { year: 'numeric', timeZone: 'Europe/Istanbul' }).format(new Date()))
export function EventYearOverview({ facilityId, compact = false }: { facilityId?: string; compact?: boolean }) {
  const [year, setYear] = useState(currentEventYear)
  const [summary, setSummary] = useState<EventYearSummary | null>(null)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let active = true
    setSummary(null)
    setError('')
    fetchEventYearSummary(year, facilityId).then((data) => {
      if (active) setSummary(data)
    }).catch((e) => { if (active) setError(e instanceof Error ? e.message : 'Yıllık özet yüklenemedi.') })
    return () => { active = false }
  }, [year, facilityId, retry])
  return (
    <section className={`event-year-overview${compact ? ' is-compact' : ''}`} aria-label="Yıllık faaliyet özeti">
      <header>
        <div><span className="annual-eyebrow">YILLIK GÖRÜNÜM</span><h3>{year} faaliyet özeti</h3></div>
        <label>Yıl<select aria-label="Özet yılı" value={year} onChange={(e) => setYear(Number(e.target.value))}>
          {Array.from({ length: currentEventYear() - 2018 + 2 }, (_, i) => currentEventYear() + 1 - i).map((y) => <option key={y} value={y}>{y}</option>)}
        </select></label>
      </header>
      {error ? <p className="form-error" role="alert">{error} <button type="button" className="btn-secondary" onClick={() => setRetry((n) => n + 1)}>Tekrar dene</button></p> : (
        <div className="event-year-metrics" aria-busy={!summary}>
          <div><span>Yapılan etkinlik</span><strong>{summary ? summary.completedEvents.toLocaleString('tr-TR') : '—'}</strong><small>Gezi ve programlar dahil</small></div>
          <div><span>Tamamlanan proje</span><strong>{summary ? summary.completedProjects.toLocaleString('tr-TR') : '—'}</strong><small>Yıl içindeki projeler</small></div>
          <div className="is-attendance"><span>Erişilen vatandaş</span><strong>{summary ? summary.totalAttendance.toLocaleString('tr-TR') : '—'}</strong><small>Kayıtlı toplam katılım</small></div>
        </div>
      )}
      <p className="event-year-footnote">Başlangıç tarihi seçilen yılda olan gerçekleşmiş kayıtlar esas alınır. Katılım toplamı tekil kişi sayısı değildir; beklenen katılım dahil edilmez.</p>
    </section>
  )
}
