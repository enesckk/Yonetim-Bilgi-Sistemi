import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  fetchNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type AppNotification,
  type NotificationSeverity,
} from '@/api/notificationsApi'

function severityTone(s: NotificationSeverity): string {
  switch (s) {
    case 2:
      return 'ok'
    case 3:
      return 'warn'
    case 4:
      return 'danger'
    default:
      return 'info'
  }
}

function formatRelative(iso: string): string {
  const date = new Date(iso)
  const mins = Math.floor((Date.now() - date.getTime()) / 60000)
  if (mins < 1) return 'Az önce'
  if (mins < 60) return `${mins} dk önce`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours} sa önce`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days} gün önce`
  return date.toLocaleDateString('tr-TR', { day: '2-digit', month: 'short' })
}

export function NotificationBell({
  unread,
  icon,
  onChanged,
}: {
  unread: number
  icon: React.ReactNode
  onChanged: () => void
}) {
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<AppNotification[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setItems(await fetchNotifications({ take: 8 }))
    } catch {
      setError('Bildirimler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!open) return
    void load()

    function onDocPointer(e: MouseEvent) {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false)
    }
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') setOpen(false)
    }

    document.addEventListener('mousedown', onDocPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDocPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open, load])

  async function openItem(n: AppNotification) {
    setOpen(false)
    if (!n.isRead) {
      try {
        await markNotificationRead(n.id)
        onChanged()
      } catch {
        // okundu işareti kritik değil
      }
    }
    if (n.linkUrl) navigate(n.linkUrl)
    else navigate('/notifications')
  }

  async function readAll() {
    try {
      await markAllNotificationsRead()
      onChanged()
      await load()
    } catch {
      setError('İşaretlenemedi.')
    }
  }

  return (
    <div className={`notif-bell${open ? ' is-open' : ''}`} ref={rootRef}>
      <button
        type="button"
        className={`topbar-icon-btn topbar-notify${unread > 0 ? ' has-unread' : ''}`}
        aria-label={unread > 0 ? `${unread} okunmamış bildirim` : 'Bildirimler'}
        aria-expanded={open}
        aria-haspopup="dialog"
        onClick={() => setOpen((v) => !v)}
      >
        {icon}
        {unread > 0 ? <span className="topbar-badge">{unread > 99 ? '99+' : unread}</span> : null}
      </button>

      {open ? (
        <div className="notif-pop" role="dialog" aria-label="Bildirimler">
          <header className="notif-pop-head">
            <div>
              <strong>Bildirimler</strong>
              <span className="muted">
                {unread > 0 ? `${unread} okunmamış` : 'Tümü okundu'}
              </span>
            </div>
            {unread > 0 ? (
              <button type="button" className="notif-pop-link" onClick={() => void readAll()}>
                Tümünü okundu yap
              </button>
            ) : null}
          </header>

          <div className="notif-pop-list">
            {loading ? (
              <p className="notif-pop-empty muted">Yükleniyor…</p>
            ) : error ? (
              <p className="notif-pop-empty form-error">{error}</p>
            ) : items.length === 0 ? (
              <div className="notif-pop-empty">
                <strong>Bildirim yok</strong>
                <p className="muted">Yeni bir hareket olduğunda burada görünür.</p>
              </div>
            ) : (
              items.map((n) => (
                <button
                  key={n.id}
                  type="button"
                  className={`notif-pop-item${n.isRead ? ' is-read' : ''}`}
                  onClick={() => void openItem(n)}
                >
                  <span className={`notif-pop-dot tone-${severityTone(n.severity)}`} aria-hidden />
                  <span className="notif-pop-body">
                    <strong>{n.title}</strong>
                    <span className="notif-pop-text">{n.body}</span>
                    <span className="notif-pop-time">{formatRelative(n.createdAtUtc)}</span>
                  </span>
                </button>
              ))
            )}
          </div>

          <footer className="notif-pop-foot">
            <Link to="/notifications" onClick={() => setOpen(false)}>
              Tümünü gör
            </Link>
          </footer>
        </div>
      ) : null}
    </div>
  )
}
