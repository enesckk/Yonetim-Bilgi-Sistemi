import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiClientError } from '@/api/client'
import {
  fetchDashboardSummary,
  type DashboardAlertItem,
  type DashboardAttention,
  type DashboardCards,
  type DashboardNamedCount,
  type DashboardSummary,
} from '@/api/dashboardApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

function DistributionChart({
  title,
  rows,
  tone = 'primary',
}: {
  title: string
  rows: DashboardNamedCount[]
  tone?: 'primary' | 'slate' | 'teal'
}) {
  const visible = rows.filter((r) => r.count > 0)
  const total = visible.reduce((s, r) => s + r.count, 0)
  const max = Math.max(1, ...visible.map((x) => x.count))

  return (
    <article className={`dash-chart tone-${tone}`}>
      <header className="dash-chart-head">
        <h3>{title}</h3>
        {total > 0 ? <span className="dash-chart-total">{total}</span> : null}
      </header>
      {visible.length === 0 ? (
        <div className="dash-empty">
          <p>Bu kapsamda henüz veri yok.</p>
        </div>
      ) : (
        <ul className="dash-bars">
          {visible.map((row, index) => {
            const pct = total > 0 ? Math.round((row.count / total) * 100) : 0
            const width = Math.max(6, Math.round((row.count / max) * 100))
            return (
              <li key={row.name} style={{ animationDelay: `${index * 40}ms` }}>
                <div className="dash-bar-meta">
                  <span className="dash-bar-name">{row.name}</span>
                  <span className="dash-bar-stats">
                    <strong>{row.count}</strong>
                    <span>{pct}%</span>
                  </span>
                </div>
                <div className="dash-bar-track" aria-hidden="true">
                  <div className="dash-bar-fill" style={{ width: `${width}%` }} />
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </article>
  )
}

function MetricIcon({ name }: { name: string }) {
  const paths: Record<string, string> = {
    users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
    check: 'M20 6 9 17l-5-5',
    pause: 'M10 4H6v16h4V4Zm8 0h-4v16h4V4Z',
    badge: 'M12 3 4 7v5c0 5 3.4 8.4 8 9 4.6-.6 8-4 8-9V7l-8-4Z',
    building: 'M3 21h18M6 21V8l6-4 6 4v13M10 21v-6h4v6',
    teach: 'M4 19.5V6a2 2 0 0 1 2-2h10v17H6a2 2 0 0 1-2-2Zm14-15h2a2 2 0 0 1 2 2v13',
    wrench: 'M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18v3h3l6.3-6.3a4 4 0 0 0 5.4-5.4l-2.5 2.5-2.5-2.5 2.5-2.5Z',
    book: 'M4 19.5V6a2 2 0 0 1 2-2h12v17H6a2 2 0 0 1-2-2Z',
    hand: 'M18 11V5a2 2 0 1 0-4 0v6M14 10V4a2 2 0 1 0-4 0v8M10 11V6a2 2 0 1 0-4 0v10a6 6 0 0 0 12 0v-5a2 2 0 1 0-4 0',
    map: 'M9 18 3 15V4l6 3 6-3 6 3v11l-6-3-6 3ZM9 7v11M15 4v11',
    alert: 'M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0ZM12 9v4M12 17h.01',
    star: 'M12 3l2.5 5.2L20 9.3l-4 4 1 5.7L12 16.5 7 19l1-5.7-4-4 5.5-1.1L12 3Z',
    move: 'M5 12h14M12 5l7 7-7 7',
    bell: 'M6 9a6 6 0 1 1 12 0c0 7 3 7 3 7H3s3 0 3-7ZM10 19a2 2 0 0 0 4 0',
    chart: 'M4 19V5M4 19h16M8 15v4M12 11v8M16 8v11',
  }
  const d = paths[name] ?? paths.users
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d={d} stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function quickIcon(href: string) {
  if (href === '/employees') return 'users'
  if (href === '/certificates') return 'badge'
  if (href === '/movements') return 'move'
  if (href === '/units') return 'map'
  if (href === '/data-quality') return 'alert'
  if (href === '/notifications') return 'bell'
  if (href === '/reports') return 'chart'
  if (href === '/facilities') return 'building'
  return 'book'
}

function formatDate(value?: string | null) {
  if (!value) return '—'
  const d = value.slice(0, 10)
  const [y, m, day] = d.split('-')
  if (!y || !m || !day) return value
  return `${day}.${m}.${y}`
}

function AlertRow({ item }: { item: DashboardAlertItem }) {
  return (
    <Link to={item.href} className={`dash-alert is-${item.severity}`}>
      <span className="dash-alert-dot" aria-hidden="true" />
      <span className="dash-alert-text">
        <strong>{item.title}</strong>
        <small>{item.description}</small>
      </span>
      <span className="dash-alert-count">{item.count.toLocaleString('tr-TR')}</span>
      <span className="dash-alert-go" aria-hidden="true">
        ›
      </span>
    </Link>
  )
}

function KpiLink({
  label,
  value,
  to,
  tone,
}: {
  label: string
  value: number
  to?: string
  tone?: 'warn' | 'ok' | 'accent'
}) {
  const className = `dash-kpi ${tone ? `is-${tone}` : ''} ${to ? 'is-link' : ''}`
  const body = (
    <>
      <span>{label}</span>
      <strong>{value.toLocaleString('tr-TR')}</strong>
    </>
  )
  if (to?.startsWith('#')) {
    return (
      <a href={to} className={className}>
        {body}
      </a>
    )
  }
  if (to) {
    return (
      <Link to={to} className={className}>
        {body}
      </Link>
    )
  }
  return <div className={className}>{body}</div>
}

function CompactStat({
  label,
  value,
  icon,
  to,
  tone,
}: {
  label: string
  value: number
  icon: string
  to?: string
  tone?: 'warn' | 'ok'
}) {
  const className = `dash-compact-stat ${tone ? `is-${tone}` : ''} ${to ? 'is-link' : ''}`
  const body = (
    <>
      <span className="dash-compact-label">
        <span className="dash-compact-icon" aria-hidden="true">
          <MetricIcon name={icon} />
        </span>
        {label}
      </span>
      <strong>{value.toLocaleString('tr-TR')}</strong>
    </>
  )
  if (to) {
    return (
      <Link to={to} className={className}>
        {body}
      </Link>
    )
  }
  return <div className={className}>{body}</div>
}

function SkeletonBlock() {
  return (
    <div className="dash-skeleton" aria-hidden="true">
      <div className="dash-skel-hero" />
      <div className="dash-skel-board" />
      <div className="dash-skel-row">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="dash-skel-chart" />
        ))}
      </div>
    </div>
  )
}

export function HomePage() {
  const { user, hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.DashboardView)
  const [data, setData] = useState<DashboardSummary | null>(null)
  const [loading, setLoading] = useState(canView)
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
        if (!cancelled) setData(summary)
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

  const greeting = useMemo(() => {
    const hour = new Date().getHours()
    if (hour < 12) return 'Günaydın'
    if (hour < 18) return 'İyi günler'
    return 'İyi akşamlar'
  }, [])

  const todayLabel = useMemo(
    () =>
      new Intl.DateTimeFormat('tr-TR', {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
        year: 'numeric',
      }).format(new Date()),
    [],
  )

  if (!canView) {
    return (
      <div className="dashboard">
        <section className="dash-denied">
          <h2>
            {greeting}, {user?.displayName}
          </h2>
          <p>Bu hesap için kontrol paneli yetkisi tanımlı değil.</p>
        </section>
      </div>
    )
  }

  const cards = data?.cards
  const alertCount = data?.attention?.alerts?.length ?? 0
  const firstName = user?.displayName?.split(' ')[0] ?? 'Yönetici'

  return (
    <div className="dashboard">
      <header className="dash-hero">
        <div className="dash-hero-main">
          <p className="dash-hero-brand">Yönetim Bilgi Sistemi</p>
          <h2>Genel bakış</h2>
          <p className="dash-hero-lead">
            <span>
              {greeting}, {firstName}
            </span>
            <span className="dash-hero-sep" aria-hidden="true">
              ·
            </span>
            <span>{todayLabel}</span>
            <span className="dash-hero-sep" aria-hidden="true">
              ·
            </span>
            <span>Kültür, Sanat ve Sosyal İşler Müdürlüğü</span>
          </p>
        </div>

        {!loading && cards ? (
          <div className="dash-hero-kpis" aria-label="Özet göstergeler">
            <KpiLink label="Personel" value={cards.totalEmployees} to="/employees" tone="accent" />
            <KpiLink label="Aktif" value={cards.activeEmployees} to="/employees?status=1" tone="ok" />
            <KpiLink label="Birim" value={cards.totalUnits} to="/units" />
            <KpiLink
              label="Dikkat"
              value={alertCount}
              to={alertCount > 0 ? '#dash-attention' : '/notifications'}
              tone={alertCount > 0 ? 'warn' : undefined}
            />
          </div>
        ) : null}
      </header>

      {error ? (
        <div className="form-error" role="alert">
          {error}
        </div>
      ) : null}

      {loading ? <SkeletonBlock /> : null}

      {!loading && data?.cards ? (
        <DashboardBody cards={data.cards} data={data} attention={data.attention} />
      ) : null}
    </div>
  )
}

function DashboardBody({
  cards,
  data,
  attention,
}: {
  cards: DashboardCards
  data: DashboardSummary
  attention: DashboardAttention
}) {
  const alerts = attention?.alerts ?? []
  const recent = attention?.recentMovements ?? []
  const quick = attention?.quickLinks ?? []

  return (
    <>
      <section id="dash-attention" className="dash-attention" aria-label="Dikkat ve hızlı erişim">
        <div className="dash-attn-cols">
          <div className="dash-attn-alerts">
            <header className="dash-attn-head">
              <div>
                <h3>Dikkat gerektirenler</h3>
                <p>Öncelikli aksiyonlar — ilgili ekrana gidin.</p>
              </div>
              {attention.unreadNotifications > 0 ? (
                <Link to="/notifications" className="dash-unread-pill">
                  {attention.unreadNotifications} okunmamış
                </Link>
              ) : null}
            </header>

            {alerts.length > 0 ? (
              <div className="dash-alert-list">
                {alerts.map((a) => (
                  <AlertRow key={a.code} item={a} />
                ))}
              </div>
            ) : (
              <p className="dash-attn-ok">Durum sakin — açık uyarı yok.</p>
            )}
          </div>

          <aside className="dash-attn-feed">
            <header className="dash-attn-head">
              <div>
                <h3>Son hareketler</h3>
                <p>
                  {attention.recentMovements30Days > 0
                    ? `Son 30 günde ${attention.recentMovements30Days.toLocaleString('tr-TR')} kayıt`
                    : 'En son görev değişiklikleri'}
                </p>
              </div>
              <Link to="/movements" className="dash-attn-more">
                Tümü ›
              </Link>
            </header>
            {recent.length > 0 ? (
              <ul className="dash-feed-list">
                {recent.slice(0, 5).map((m) => (
                  <li key={m.id}>
                    <Link to={`/employees/${m.employeeId}`} className="dash-feed-item">
                      <span className="dash-feed-date">{formatDate(m.startDate)}</span>
                      <span className="dash-feed-body">
                        <strong>{m.employeeName}</strong>
                        <em>{m.movementTypeLabel}</em>
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="dash-attn-empty">Henüz hareket kaydı yok.</p>
            )}
          </aside>
        </div>

        {quick.length > 0 ? (
          <footer className="dash-attn-quick">
            <span className="dash-attn-quick-label">Hızlı erişim</span>
            <nav className="dash-attn-quick-links">
              {quick.map((q) => (
                <Link key={q.href} to={q.href}>
                  <span className="dash-attn-quick-icon" aria-hidden="true">
                    <MetricIcon name={quickIcon(q.href)} />
                  </span>
                  <span>{q.label}</span>
                  <span className="dash-attn-quick-arrow" aria-hidden="true">
                    ›
                  </span>
                </Link>
              ))}
            </nav>
          </footer>
        ) : null}
      </section>

      <section className="dash-ops" aria-label="Operasyonel göstergeler">
        <header className="dash-block-head">
          <h3>Operasyon özeti</h3>
          <p>Kapsamınızdaki kadro, tesis ve veri kalitesi sinyalleri.</p>
        </header>
        <div className="dash-ops-grid">
          <KpiLink label="Toplam personel" value={cards.totalEmployees} to="/employees" tone="accent" />
          <KpiLink label="Aktif" value={cards.activeEmployees} to="/employees?status=1" tone="ok" />
          <KpiLink label="Pasif" value={cards.passiveEmployees} to="/employees?status=2" />
          <KpiLink
            label="Eksik bilgi"
            value={cards.incompleteProfileCount}
            to="/data-quality?kind=missing"
            tone={cards.incompleteProfileCount > 0 ? 'warn' : 'ok'}
          />
          <KpiLink
            label="Yetkinlik yok"
            value={cards.missingSkillsCount}
            to="/data-quality"
            tone={cards.missingSkillsCount > 0 ? 'warn' : 'ok'}
          />
          <KpiLink label="Görev yeri değişen" value={cards.workplaceChangedCount} to="/movements" />
        </div>
      </section>

      <section className="dash-split" aria-label="Kadro ve organizasyon">
        <div className="dash-panel">
          <header className="dash-block-head">
            <h3>Kadro dağılımı</h3>
          </header>
          <div className="dash-compact-grid cols-3">
            <CompactStat label="Memur" value={cards.civilServantCount} icon="badge" />
            <CompactStat label="Şirket" value={cards.companyStaffCount} icon="building" />
            <CompactStat label="Eğitmen" value={cards.instructorCount} icon="teach" />
            <CompactStat label="Teknik" value={cards.technicalCount} icon="wrench" />
            <CompactStat label="Kütüphane" value={cards.libraryStaffCount} icon="book" />
            <CompactStat label="Yardımcı" value={cards.auxiliaryCount} icon="hand" />
          </div>
        </div>

        <div className="dash-panel">
          <header className="dash-block-head">
            <h3>Organizasyon</h3>
          </header>
          <div className="dash-compact-grid cols-2">
            <CompactStat label="Birim" value={cards.totalUnits} icon="map" to="/units" />
            <CompactStat label="Tesis" value={cards.totalFacilities} icon="building" to="/facilities" />
            <CompactStat label="Aktif tesis" value={cards.activeFacilities} icon="check" to="/facilities" tone="ok" />
            <CompactStat
              label="Kapalı / tadilat"
              value={cards.closedOrRenovationFacilities}
              icon="alert"
              to="/facilities"
              tone={cards.closedOrRenovationFacilities > 0 ? 'warn' : undefined}
            />
          </div>
        </div>
      </section>

      <section className="dash-charts-block">
        <header className="dash-block-head">
          <h3>Dağılım görünümleri</h3>
          <p>Birim, istihdam, görev, eğitim ve hizmet süresi kırılımları.</p>
        </header>
        <div className="dash-charts-grid" aria-label="Dağılım grafikleri">
          <DistributionChart title="Birimlere göre" rows={data.byUnit} tone="primary" />
          <DistributionChart title="İstihdam türüne göre" rows={data.byEmploymentType} tone="teal" />
          <DistributionChart title="Görev türüne göre" rows={data.byDutyCategory} tone="slate" />
          <DistributionChart title="Eğitim seviyesine göre" rows={data.byEducationLevel} tone="primary" />
          <DistributionChart title="Hizmet süresine göre" rows={data.byServiceYears} tone="teal" />
        </div>
      </section>
    </>
  )
}
