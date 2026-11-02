import { useState } from 'react'

const key = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
export function EventDateRange({ start, end, onChange }: { start: string; end: string; onChange: (start: string, end: string) => void }) {
  const [month, setMonth] = useState(() => new Date(`${start || key(new Date())}T12:00:00`))
  const [anchor, setAnchor] = useState<string | null>(null)
  const first = new Date(month.getFullYear(), month.getMonth(), 1, 12)
  const offset = (first.getDay() + 6) % 7
  const count = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate()
  const days = start && end && end >= start ? Math.round((Date.parse(`${end}T00:00:00Z`) - Date.parse(`${start}T00:00:00Z`)) / 86400000) + 1 : 1
  return <div className="event-range span-2">
    <div className="event-range-head"><strong>Takvimden tarih aralığı seçin</strong><span>{days} gün</span></div>
    <p className="muted small" aria-live="polite">{anchor ? 'Şimdi son günü seçin. Tek günlük etkinlik için aynı güne tekrar basın.' : 'Önce ilk, sonra son güne basın. Saatleri aşağıdan belirleyebilirsiniz.'}</p>
    <div className="event-range-head"><button type="button" className="btn-secondary" aria-label="Önceki ay" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() - 1, 1, 12))}>‹</button><strong aria-live="polite">{month.toLocaleDateString('tr-TR', { month: 'long', year: 'numeric' })}</strong><button type="button" className="btn-secondary" aria-label="Sonraki ay" onClick={() => setMonth(new Date(month.getFullYear(), month.getMonth() + 1, 1, 12))}>›</button></div>
    <div className="event-range-grid">{['Pt', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct', 'Pa'].map(d => <span key={d}>{d}</span>)}{Array.from({ length: offset }, (_, i) => <span key={`empty-${i}`} />)}{Array.from({ length: count }, (_, i) => {
      const date = key(new Date(month.getFullYear(), month.getMonth(), i + 1, 12))
      const selected = date >= start && date <= (end || start)
      return <button type="button" key={date} aria-label={new Date(`${date}T12:00:00`).toLocaleDateString('tr-TR')} aria-pressed={selected} className={selected ? 'is-selected' : ''} onClick={() => {
        if (!anchor) { setAnchor(date); onChange(date, '') }
        else { onChange(date < anchor ? date : anchor, date < anchor ? anchor : date); setAnchor(null) }
      }}>{i + 1}</button>
    })}</div>
    <div className="event-range-head"><button type="button" className="btn-secondary" disabled={!start} onClick={() => { const d = new Date(`${start}T12:00:00`); d.setDate(d.getDate() + 2); onChange(start, key(d)); setAnchor(null) }}>3 günlük etkinlik</button>{end && <button type="button" className="btn-secondary" onClick={() => { onChange(start, ''); setAnchor(null) }}>Bitiş tarihini temizle</button>}</div>
  </div>
}
