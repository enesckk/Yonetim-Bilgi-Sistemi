import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { fetchDashboardSummary, type DashboardCards, type DashboardSummary } from '@/api/dashboardApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

function fmt(n: number | null | undefined) {
  return (n ?? 0).toLocaleString('tr-TR')
}

function statusCaption(cards?: DashboardCards | null) {
  if (!cards) return 'Sayılar yükleniyor'
  const other = Math.max(0, cards.totalEmployees - cards.activeEmployees - cards.passiveEmployees)
  const parts = [`${fmt(cards.activeEmployees)} aktif`]
  if (cards.passiveEmployees > 0) parts.push(`${fmt(cards.passiveEmployees)} pasif`)
  if (other > 0) parts.push(`${fmt(other)} diğer`)
  return parts.join(' · ')
}

function Icon({ d }: { d: string }) {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d={d} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

export function StaffDashboardPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.DashboardView)
  const canEvents = hasPermission(PermissionCodes.EventsView)
  const [data, setData] = useState<DashboardSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!canView) {
      setLoading(false)
      return
    }
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const summary = await fetchDashboardSummary()
        if (!cancelled) {
          setData(summary)
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiClientError ? err.message : 'Özet yüklenemedi.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [canView])

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">Müdürlük özetini görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  const cards = data?.cards
  const byUnit = (data?.byUnit ?? []).slice(0, 8)
  const movements = data?.attention.recentMovements ?? []
  const employment = (data?.byEmploymentType ?? []).filter((x) => x.count > 0)
  const unitMax = Math.max(1, ...byUnit.map((x) => x.count))

  return (
    <div className="employees-page staff-dash">
      <section className="panel">
        <div className="employees-toolbar">
          <div className="employees-toolbar-title">
            <div className="employees-toolbar-title-row">
              <h2>Müdürlük özeti</h2>
              <div className="stat-chip mobile-inline-chip">
                {loading ? '…' : `${fmt(cards?.totalEmployees)} kadro`}
              </div>
            </div>
            <p className="muted small employees-toolbar-lead">
              Personel, tesis, etkinlik ve proje özeti. Kartlara tıklayınca ilgili liste açılır.
            </p>
          </div>
          <div className="employees-toolbar-actions">
            <div className="stat-chip desktop-inline-chip">
              {loading ? '…' : `${fmt(cards?.totalEmployees)} kadro`}
            </div>
            <Link to="/employees" className="btn-secondary">
              Personeller
            </Link>
            {canEvents ? <Link to="/events/list?kind=event" className="btn-secondary">Etkinlikler</Link> : null}
            <Link to="/organization" className="btn-secondary">
              Şema
            </Link>
            {hasPermission(PermissionCodes.TasksView) ? (
              <Link to="/tasks" className="btn-secondary">
                İş ataması
              </Link>
            ) : null}
          </div>
        </div>

        {error ? <p className="form-error">{error}</p> : null}

        <div className="staff-dash-cards">
          <Link to="/employees" className="staff-dash-card is-people">
            <span>
              <Icon d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
              Personel
            </span>
            <strong className={loading ? 'is-skeleton' : undefined}>
              {loading ? '—' : fmt(cards?.totalEmployees)}
            </strong>
            <small>{statusCaption(loading ? null : cards)}</small>
          </Link>
          <Link to="/facilities" className="staff-dash-card is-facilities">
            <span>
              <Icon d="M3 21h18M5 21V8h14v13M8 11h3v3H8v-3ZM13 11h3v3h-3v-3Z" />
              Tesis
            </span>
            <strong className={loading ? 'is-skeleton' : undefined}>
              {loading ? '—' : fmt(cards?.totalFacilities)}
            </strong>
            <small>
              {loading ? 'Sayılar yükleniyor' : `${fmt(cards?.activeFacilities)} aktif tesiste faaliyet`}
            </small>
          </Link>
          {canEvents ? (
            <>
              <Link to="/events/list?kind=event" className="staff-dash-card is-events">
                <span><Icon d="M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14H3V6a2 2 0 0 1 2-2Z" />Etkinlik</span>
                <strong className={loading ? 'is-skeleton' : undefined}>{loading ? '—' : fmt(cards?.totalEvents)}</strong>
                <small>Gezi dahil tüm etkinlikler</small>
              </Link>
              <Link to="/events/list?kind=project" className="staff-dash-card is-projects">
                <span><Icon d="M3 7h7l2 2h9v11H3V7ZM3 7V4h7l2 3" />Projeler</span>
                <strong className={loading ? 'is-skeleton' : undefined}>{loading ? '—' : fmt(cards?.totalProjects)}</strong>
                <small>Kayıtlı projeler</small>
              </Link>
            </>
          ) : null}
        </div>

        <div className="staff-dash-grid">
          <section className="staff-dash-panel">
            <header>
              <h3>Birimlere göre personel</h3>
              <Link to="/organization">Şema ›</Link>
            </header>
            {loading ? (
              <div className="ui-skeleton-stack" aria-hidden="true">
                <span className="ui-skeleton" />
                <span className="ui-skeleton" />
                <span className="ui-skeleton" />
              </div>
            ) : byUnit.length === 0 ? (
              <p className="staff-dash-empty">Birim dağılımı henüz yok.</p>
            ) : (
              <ul className="staff-dash-bars">
                {byUnit.map((row) => {
                  const width = Math.max(8, Math.round((row.count / unitMax) * 100))
                  return (
                    <li key={row.name}>
                      <span>{row.name}</span>
                      <div className="staff-dash-track" aria-hidden="true">
                        <div style={{ width: `${width}%` }} />
                      </div>
                      <strong>{fmt(row.count)}</strong>
                    </li>
                  )
                })}
              </ul>
            )}
          </section>

          <section className="staff-dash-panel">
            <header>
              <h3>Son hareketler</h3>
              <Link to="/employees">Liste ›</Link>
            </header>
            {loading ? (
              <div className="ui-skeleton-stack" aria-hidden="true">
                <span className="ui-skeleton" />
                <span className="ui-skeleton" />
              </div>
            ) : movements.length === 0 ? (
              <p className="staff-dash-empty">Son 30 günde hareket kaydı yok.</p>
            ) : (
              <ul className="staff-dash-feed">
                {movements.slice(0, 6).map((m) => (
                  <li key={m.id}>
                    <Link to={`/employees/${m.employeeId}`}>
                      <strong>{m.employeeName}</strong>
                      <em>
                        {m.movementTypeLabel}
                        {m.summary ? ` · ${m.summary}` : ''}
                      </em>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </div>

        {employment.length > 0 ? (
          <section className="staff-dash-panel">
            <header>
              <h3>İstihdam türü</h3>
            </header>
            <ul className="staff-dash-pills">
              {employment.map((row) => (
                <li key={row.name}>
                  <span>{row.name}</span>
                  <strong>{fmt(row.count)}</strong>
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </section>
    </div>
  )
}
