import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { downloadInformationNote, fetchInformationNotes, type InformationNote } from '@/api/informationNotesApi'
export function EventInformationNotes({ eventId }: { eventId: string }) {
  const { hasPermission } = useAuth()
  const wide = hasPermission(PermissionCodes.EmployeesViewAllUnits)
  const [notes, setNotes] = useState<InformationNote[]>([])
  const [downloadError, setDownloadError] = useState('')
  const [downloading, setDownloading] = useState<string | null>(null)
  async function download(id: string) {
    setDownloading(id); setDownloadError('')
    try { await downloadInformationNote(id) }
    catch { setDownloadError('Bilgi notu indirilemedi. Tekrar deneyin.') }
    finally { setDownloading(null) }
  }
  const [error, setError] = useState(false)
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    let active = true
    if (wide) {
      setLoading(true)
      fetchInformationNotes(eventId)
        .then((n) => {
          if (active) {
            setNotes(n)
            setError(false)
          }
        })
        .catch(() => {
          if (active) setError(true)
        })
        .finally(() => {
          if (active) setLoading(false)
        })
    }
    return () => {
      active = false
    }
  }, [eventId, wide])
  if (!wide) return null
  return (
    <section id="event-notes" className="event-information-notes">
      <header>
        <div>
          <h3>Bilgi notları</h3>
          <p className="muted small">Bu kayıtla ilişkili bilgi notları ve kaynak belgeler.</p>
        </div>
        <Link className="btn-secondary" to={'/information-notes?eventId=' + eventId}>
          Bilgi notlarını aç
        </Link>
      </header>
      {downloadError && <p role="alert" className="form-error">{downloadError}</p>}
      {loading ? (
        <p className="muted small">Yükleniyor…</p>
      ) : error ? (
        <p role="alert" className="form-error">
          Bilgi notları yüklenemedi. Bölüm bağlantısından yeniden açabilirsiniz.
        </p>
      ) : notes.length === 0 ? (
        <p className="muted small">Henüz bilgi notu eklenmemiş.</p>
      ) : (
        <ul>
          {notes.map((n) => (
            <li key={n.id} className="event-note-card">
              <details open={notes.length === 1}>
                <summary><span className="event-note-mark" aria-hidden="true">≡</span><div><strong>{n.title}</strong><span>{[n.dateText, n.organizer].filter(Boolean).join(' · ') || 'Etkinlik bilgi notu'}</span></div><b>Görüntüle</b></summary>
                <div className="event-note-content">
                  {n.subject && <p><strong>Konu:</strong> {n.subject}</p>}
                  <div className="event-note-body">{n.body}</div>
                  {n.preparedBy && <p className="muted small">Hazırlayan: {n.preparedBy}</p>}
                  <div className="event-note-actions"><button type="button" className="btn-secondary" disabled={downloading !== null} onClick={() => void download(n.id)}>{downloading === n.id ? 'İndiriliyor…' : 'Belgeyi indir'}</button><Link to={'/information-notes?note=' + n.id} className="btn-link">Bilgi notunu aç ›</Link></div>
                </div>
              </details>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
