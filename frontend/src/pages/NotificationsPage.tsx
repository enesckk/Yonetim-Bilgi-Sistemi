import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  fetchNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  runNotificationScan,
  NOTIFICATION_CATEGORIES,
  type AppNotification,
  type NotificationSeverity,
} from '@/api/notificationsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

type NotifView = 'all' | 'unread'

function severityLabel(s: NotificationSeverity): string {
  switch (s) {
    case 2:
      return 'Başarı'
    case 3:
      return 'Uyarı'
    case 4:
      return 'Kritik'
    default:
      return 'Bilgi'
  }
}

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

function categoryLabel(code: string): string {
  return NOTIFICATION_CATEGORIES.find((c) => c.key === code)?.label ?? code
}

function formatRelative(iso: string): string {
  const date = new Date(iso)
  const diffMs = Date.now() - date.getTime()
  const mins = Math.floor(diffMs / 60000)
  if (mins < 1) return 'Az önce'
  if (mins < 60) return `${mins} dk önce`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours} sa önce`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days} gün önce`
  return date.toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function NotificationsPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.NotificationsView)
  const canScan =
    hasPermission(PermissionCodes.DataQualityView) ||
    hasPermission(PermissionCodes.OrganizationView) ||
    hasPermission(PermissionCodes.SettingsManage)

  const [items, setItems] = useState<AppNotification[]>([])
  const [view, setView] = useState<NotifView>('all')
  const [category, setCategory] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [scanMsg, setScanMsg] = useState<string | null>(null)

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      setItems(
        await fetchNotifications({
          unreadOnly: view === 'unread',
          category: category || undefined,
          take: 80,
        }),
      )
    } catch (err) {
      setItems([])
      setError(err instanceof ApiClientError ? err.message : 'Bildirimler yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canView, category, view])

  useEffect(() => {
    void load()
  }, [load])

  const unreadCount = useMemo(() => items.filter((i) => !i.isRead).length, [items])
  const criticalCount = useMemo(
    () => items.filter((i) => !i.isRead && i.severity === 4).length,
    [items],
  )

  async function onRead(id: string) {
    setBusy(true)
    setError(null)
    try {
      await markNotificationRead(id)
      await load()
      window.dispatchEvent(new Event('notifications:changed'))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Okundu işaretlenemedi.')
    } finally {
      setBusy(false)
    }
  }

  async function onReadAll() {
    setBusy(true)
    setError(null)
    try {
      await markAllNotificationsRead()
      await load()
      window.dispatchEvent(new Event('notifications:changed'))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Toplu okuma başarısız.')
    } finally {
      setBusy(false)
    }
  }

  async function onScan() {
    setBusy(true)
    setError(null)
    setScanMsg(null)
    try {
      const result = await runNotificationScan()
      await load()
      window.dispatchEvent(new Event('notifications:changed'))
      if (result.createdCount === 0) {
        setScanMsg(
          result.skippedDuplicateCount > 0
            ? 'Tarama tamamlandı. Yeni uyarı yok (yakın zamanda gönderilmiş olanlar atlandı).'
            : 'Tarama tamamlandı. Şu an uyarı üretecek durum bulunamadı.',
        )
      } else {
        setScanMsg(
          `${result.createdCount} yeni bildirim oluşturuldu${
            result.summary.length ? `: ${result.summary.slice(0, 3).join('; ')}` : '.'
          }`,
        )
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Tarama başarısız.')
    } finally {
      setBusy(false)
    }
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Bildirimler için yetkiniz yok.</p>
      </div>
    )
  }

  return (
    <div className="org-page notif-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Bildirim sistemi</p>
          <h1>Bildirimler</h1>
          <p className="muted">
            Eksik veri, sertifika süresi, kadro açığı ve personel değişiklikleri size özel olarak
            burada toplanır.
          </p>
        </div>
        <div className="report-hero-side">
          <div className="report-hero-stats">
            <div>
              <strong>{items.length}</strong>
              <span>Gösterilen</span>
            </div>
            <div>
              <strong className={unreadCount > 0 ? 'is-warn' : undefined}>{unreadCount}</strong>
              <span>Okunmamış</span>
            </div>
            <div>
              <strong className={criticalCount > 0 ? 'is-danger' : undefined}>
                {criticalCount}
              </strong>
              <span>Kritik</span>
            </div>
          </div>
          <div className="notif-hero-actions">
            {canScan && (
              <button
                type="button"
                className="btn-primary"
                disabled={busy}
                onClick={() => void onScan()}
              >
                Uyarıları tara
              </button>
            )}
            <button
              type="button"
              className="btn-secondary"
              disabled={busy || unreadCount === 0}
              onClick={() => void onReadAll()}
            >
              Tümünü okundu say
            </button>
          </div>
        </div>
      </header>

      <div className="org-toolbar panel">
        <div className="org-views" role="tablist" aria-label="Bildirim görünümleri">
          {(
            [
              ['all', 'Tümü'],
              ['unread', 'Okunmamış'],
            ] as const
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={view === id}
              className={view === id ? 'is-active' : undefined}
              onClick={() => setView(id)}
            >
              {label}
            </button>
          ))}
        </div>
        <div className="notif-toolbar-right">
          <label>
            <span>Kategori</span>
            <select value={category} onChange={(e) => setCategory(e.target.value)}>
              {NOTIFICATION_CATEGORIES.map((c) => (
                <option key={c.key || 'all'} value={c.key}>
                  {c.label}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {error && (
        <div className="panel">
          <div className="form-error">{error}</div>
        </div>
      )}
      {scanMsg && !error && (
        <div className="panel">
          <p className="notif-scan-msg">{scanMsg}</p>
        </div>
      )}

      <section className="panel dq-section">
        {loading ? (
          <p className="muted">Yükleniyor…</p>
        ) : items.length === 0 ? (
          <div className="notif-empty">
            <strong>Bildirim yok</strong>
            <p className="muted">
              {view === 'unread'
                ? 'Okunmamış bildiriminiz bulunmuyor.'
                : 'Henüz bildirim oluşmamış. “Uyarıları tara” ile kontrol başlatabilirsiniz.'}
            </p>
          </div>
        ) : (
          <div className="notif-feed">
            {items.map((n) => (
              <article
                key={n.id}
                className={`notif-row notif-${severityTone(n.severity)}${n.isRead ? ' is-read' : ''}`}
              >
                <span className="notif-row-dot" aria-hidden />
                <div className="notif-row-main">
                  <div className="notif-row-meta">
                    <span className={`notif-sev-pill notif-sev-${severityTone(n.severity)}`}>
                      {severityLabel(n.severity)}
                    </span>
                    <span className="notif-cat">{categoryLabel(n.category)}</span>
                    <time dateTime={n.createdAtUtc}>{formatRelative(n.createdAtUtc)}</time>
                  </div>
                  <h3>{n.title}</h3>
                  <p>{n.body}</p>
                </div>
                <div className="notif-row-actions">
                  {n.linkUrl ? (
                    <Link
                      to={n.linkUrl}
                      className="dq-action is-primary"
                      onClick={() => {
                        if (!n.isRead) void onRead(n.id)
                      }}
                    >
                      İncele
                    </Link>
                  ) : null}
                  {!n.isRead && (
                    <button
                      type="button"
                      className="dq-action"
                      disabled={busy}
                      onClick={() => void onRead(n.id)}
                    >
                      Okundu
                    </button>
                  )}
                </div>
              </article>
            ))}
          </div>
        )}
      </section>

      <p className="notif-footnote muted small">
        Bildirimler uygulama içinde gösterilir. E-posta bildirimi Ayarlar’dan ileride açılabilir
        (varsayılan: kapalı).
      </p>
    </div>
  )
}
