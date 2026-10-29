import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { fetchInformationNotes, type InformationNote } from '@/api/informationNotesApi'
export function EventInformationNotes({ eventId }: { eventId: string }) {
  const { hasPermission } = useAuth()
  const wide = hasPermission(PermissionCodes.EmployeesViewAllUnits)
  const [notes, setNotes] = useState<InformationNote[]>([])
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
    <section className="event-information-notes">
      <header>
        <div>
          <h3>Bilgi notları</h3>
          <p className="muted small">Bu kayıtla ilişkili bilgi notları ve kaynak belgeler.</p>
        </div>
        <Link className="btn-secondary" to={'/information-notes?eventId=' + eventId}>
          Bilgi notlarını aç
        </Link>
      </header>
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
            <li key={n.id}>
              <Link to={'/information-notes?note=' + n.id}>
                <strong>{n.title}</strong>
                <span>{n.dateText}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
