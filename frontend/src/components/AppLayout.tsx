import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link, Outlet, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { fetchUnreadNotificationCount } from '@/api/notificationsApi'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { AccountMenu } from '@/components/AccountMenu'
import { EmployeeAvatar } from '@/components/EmployeeAvatar'
import { NotificationBell } from '@/components/NotificationBell'

const TITLES: Record<string, string> = {
  '/': 'Kontrol Paneli',
  '/employees': 'Personeller',
  '/employees/import': 'Toplu personel yükleme',
  '/movements': 'Görev geçmişi',
  '/units': 'Birimler',
  '/facilities': 'Tesisler',
  '/organization': 'Organizasyon Şeması',
  '/skills': 'Yetkinlikler',
  '/certificates': 'Sertifikalar',
  '/catalogs': 'Kataloglar',
  '/audit-logs': 'İşlem geçmişi',
  '/reports': 'Raporlar',
  '/data-quality': 'Veri Eksikleri',
  '/notifications': 'Bildirimler',
  '/settings': 'Sistem Ayarları',
  '/users': 'Kullanıcılar',
  '/roles': 'Yetki Matrisi',
  '/events': 'Genel bakış',
  '/events/list': 'Etkinlikler',
  '/events/calendar': 'Takvim',
  '/events/map': 'Harita',
  '/events/facilities-locations': 'Tesis konumları',
  '/events/import': 'Etkinlik CSV aktarımı',
  '/events/new': 'Yeni etkinlik',
}

type AppModule = 'personnel' | 'events'
const MODULE_KEY = 'py.app.module'

type NavItem = {
  to: string
  label: string
  end?: boolean
  permission?: string
  icon: ReactNode
  badge?: number
}

const SIDEBAR_KEY = 'py.sidebar.collapsed'

function Icon({ d, size = 18 }: { d: string; size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d={d} stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

const I = {
  home: 'M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1v-9.5Z',
  users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
  upload: 'M12 16V5M8 9l4-4 4 4M4 19h16',
  org: 'M3 21h18M6 21V10M12 21V3M18 21V14',
  building: 'M4 21V6l8-3v18M4 9h8M7 12h2M7 15h2M15 21V10h5v11M17 13h1M17 16h1',
  facility: 'M3 21h18M5 21V8h14v13M8 11h3v3H8v-3ZM13 11h3v3h-3v-3ZM9 21v-4h6v4',
  chart: 'M12 5v4M5 13V9h14v4M3 13h4v4H3v-4ZM10 9h4v4h-4V9ZM17 13h4v4h-4v-4',
  search: 'M21 21l-4.35-4.35M19 11a8 8 0 1 1-16 0 8 8 0 0 1 16 0Z',
  plus: 'M12 5v14M5 12h14',
  audit: 'M12 8v4l3 2M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
  history: 'M3 12a9 9 0 1 0 3-6.7M3 4v5h5M12 7v5l3 2',
  reports: 'M4 19V5M4 19h16M8 15v4M12 11v8M16 8v11',
  quality: 'M12 3l8 4.5v9L12 21l-8-4.5v-9L12 3ZM9.5 12l1.8 1.8L15 10',
  skill: 'M12 15a5 5 0 1 0 0-10 5 5 0 0 0 0 10ZM8.5 13.5 7 21l5-3 5 3-1.5-7.5',
  certificate: 'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8l-6-6ZM14 2v6h6M9 13h6M9 17h4',
  catalog: 'M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2ZM8 7h8M8 11h8M8 15h5',
  bell: 'M6 9a6 6 0 1 1 12 0c0 7 3 7 3 7H3s3 0 3-7ZM10 19a2 2 0 0 0 4 0',
  settings: 'M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7ZM19.4 15a1 1 0 0 0 .2 1.1l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1 1 0 0 0-1.1-.2 1 1 0 0 0-.6.9V20a2 2 0 1 1-4 0v-.1a1 1 0 0 0-.6-.9 1 1 0 0 0-1.1.2l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1 1 0 0 0 .2-1.1 1 1 0 0 0-.9-.6H4a2 2 0 1 1 0-4h.1a1 1 0 0 0 .9-.6 1 1 0 0 0-.2-1.1l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1 1 0 0 0 1.1.2 1 1 0 0 0 .6-.9V4a2 2 0 1 1 4 0v.1a1 1 0 0 0 .6.9 1 1 0 0 0 1.1-.2l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1 1 0 0 0-.2 1.1 1 1 0 0 0 .9.6H20a2 2 0 1 1 0 4h-.1a1 1 0 0 0-.9.6Z',
  roles: 'M12 3 4 7v5c0 5 3.4 8.4 8 9 4.6-.6 8-4 8-9V7l-8-4Z',
  menu: 'M4 7h16M4 12h16M4 17h16',
  collapse: 'M15 6l-6 6 6 6',
  expand: 'M9 6l6 6-6 6',
  map: 'M9 20l-6-3V4l6 3 6-3 6 3v13l-6-3-6 3ZM9 7v13M15 4v13',
  calendar: 'M8 2v3M16 2v3M4 9h16M6 5h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Z',
  pin: 'M12 21s7-5.2 7-11a7 7 0 1 0-14 0c0 5.8 7 11 7 11ZM12 10.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Z',
}

export function AppLayout() {
  const { user, logout, hasPermission } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const canNotifications = hasPermission(PermissionCodes.NotificationsView)
  const [unread, setUnread] = useState(0)
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(SIDEBAR_KEY) === '1'
    } catch {
      return false
    }
  })
  const [mobileOpen, setMobileOpen] = useState(false)
  const [globalSearch, setGlobalSearch] = useState('')
  const topbarRef = useRef<HTMLElement>(null)

  // Sayfa içi yapışkan başlıklar sabit topbar'ın altında kalmasın diye
  // gerçek yükseklik CSS değişkenine yazılır.
  useEffect(() => {
    const el = topbarRef.current
    if (!el) return

    const apply = () => {
      document.documentElement.style.setProperty(
        '--topbar-h',
        `${Math.round(el.getBoundingClientRect().height)}px`,
      )
    }

    apply()
    const observer = new ResizeObserver(apply)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  const refreshUnread = useCallback(async () => {
    if (!canNotifications) {
      setUnread(0)
      return
    }
    try {
      setUnread(await fetchUnreadNotificationCount())
    } catch {
      // rozet kritik değil
    }
  }, [canNotifications])

  useEffect(() => {
    void refreshUnread()
    const onChange = () => void refreshUnread()
    window.addEventListener('notifications:changed', onChange)
    const timer = window.setInterval(() => void refreshUnread(), 60_000)
    return () => {
      window.removeEventListener('notifications:changed', onChange)
      window.clearInterval(timer)
    }
  }, [refreshUnread])

  useEffect(() => {
    if (location.pathname === '/notifications') void refreshUnread()
    setMobileOpen(false)
  }, [location.pathname, refreshUnread])

  useEffect(() => {
    try {
      localStorage.setItem(SIDEBAR_KEY, collapsed ? '1' : '0')
    } catch {
      // ignore
    }
  }, [collapsed])

  const title =
    location.pathname === '/employees/import'
      ? 'Toplu personel yükleme'
      : location.pathname.startsWith('/employees/') && location.pathname !== '/employees'
        ? 'Personel Detayı'
        : location.pathname.startsWith('/events/') &&
            location.pathname !== '/events/list' &&
            location.pathname !== '/events/calendar' &&
            location.pathname !== '/events/map' &&
            location.pathname !== '/events/facilities-locations' &&
            location.pathname !== '/events/import' &&
            location.pathname !== '/events/new'
          ? location.pathname.endsWith('/edit')
            ? 'Etkinlik düzenle'
            : 'Etkinlik detayı'
          : (TITLES[location.pathname] ?? 'Yönetim')

  const moduleFromPath: AppModule = location.pathname.startsWith('/events') ? 'events' : 'personnel'
  const [appModule, setAppModule] = useState<AppModule>(() => {
    try {
      const saved = localStorage.getItem(MODULE_KEY)
      if (saved === 'events' || saved === 'personnel') return saved
    } catch {
      /* ignore */
    }
    return moduleFromPath
  })

  useEffect(() => {
    setAppModule(moduleFromPath)
    try {
      localStorage.setItem(MODULE_KEY, moduleFromPath)
    } catch {
      /* ignore */
    }
  }, [moduleFromPath])

  function switchModule(next: AppModule) {
    setAppModule(next)
    try {
      localStorage.setItem(MODULE_KEY, next)
    } catch {
      /* ignore */
    }
    navigate(next === 'events' ? '/events' : '/')
  }

  function onGlobalSearch(e: FormEvent) {
    e.preventDefault()
    const value = globalSearch.trim()
    if (!value) return
    if (appModule === 'events') {
      navigate(`/events/list?search=${encodeURIComponent(value)}`)
    } else {
      navigate(`/employees?search=${encodeURIComponent(value)}`)
    }
  }

  const sharedSystemNav: NavItem[] = [
    {
      to: '/notifications',
      label: 'Bildirimler',
      icon: <Icon d={I.bell} />,
      permission: PermissionCodes.NotificationsView,
      badge: unread,
    },
    { to: '/users', label: 'Kullanıcı yönetimi', icon: <Icon d={I.users} />, permission: PermissionCodes.UsersManage },
    { to: '/roles', label: 'Yetki matrisi', icon: <Icon d={I.roles} />, permission: PermissionCodes.RolesManage },
    { to: '/settings', label: 'Ayarlar', icon: <Icon d={I.settings} />, permission: PermissionCodes.SettingsManage },
  ]

  const personnelNav: NavItem[] = [
    { to: '/', label: 'Genel bakış', end: true, icon: <Icon d={I.home} />, permission: PermissionCodes.DashboardView },
    { to: '/employees', label: 'Personeller', icon: <Icon d={I.users} />, permission: PermissionCodes.EmployeesView },
    { to: '/movements', label: 'Görev geçmişi', icon: <Icon d={I.history} />, permission: PermissionCodes.MovementsView },
    { to: '/units', label: 'Birimler', icon: <Icon d={I.building} />, permission: PermissionCodes.OrganizationView },
    { to: '/facilities', label: 'Tesisler', icon: <Icon d={I.facility} />, permission: PermissionCodes.OrganizationView },
    { to: '/organization', label: 'Organizasyon şeması', icon: <Icon d={I.chart} />, permission: PermissionCodes.OrganizationView },
    { to: '/skills', label: 'Yetkinlikler', icon: <Icon d={I.skill} />, permission: PermissionCodes.EmployeesView },
    { to: '/certificates', label: 'Sertifikalar', icon: <Icon d={I.certificate} />, permission: PermissionCodes.EmployeesView },
    { to: '/catalogs', label: 'Kataloglar', icon: <Icon d={I.catalog} />, permission: PermissionCodes.EmployeesView },
    { to: '/reports', label: 'Raporlar', icon: <Icon d={I.reports} />, permission: PermissionCodes.ReportsView },
    { to: '/data-quality', label: 'Veri eksikleri', icon: <Icon d={I.quality} />, permission: PermissionCodes.DataQualityView },
    { to: '/audit-logs', label: 'İşlem geçmişi', icon: <Icon d={I.audit} />, permission: PermissionCodes.AuditLogsView },
    ...sharedSystemNav,
  ]

  const eventsNav: NavItem[] = [
    { to: '/events', label: 'Genel bakış', end: true, icon: <Icon d={I.home} />, permission: PermissionCodes.EventsView },
    { to: '/events/map', label: 'Harita', icon: <Icon d={I.map} />, permission: PermissionCodes.EventsView },
    { to: '/events/calendar', label: 'Takvim', icon: <Icon d={I.calendar} />, permission: PermissionCodes.EventsView },
    { to: '/events/list', label: 'Etkinlikler', icon: <Icon d={I.catalog} />, permission: PermissionCodes.EventsView },
    {
      to: '/events/facilities-locations',
      label: 'Tesis konumları',
      icon: <Icon d={I.pin} />,
      permission: PermissionCodes.OrganizationView,
    },
    ...sharedSystemNav,
  ]

  const navItems = appModule === 'events' ? eventsNav : personnelNav
  const brandTitle = appModule === 'events' ? 'Etkinlik Yönetim Takip' : 'Personel Yönetim Takip'
  const brandShort = appModule === 'events' ? 'Etkinlik' : 'Personel'

  const shellClass = [
    'app-shell',
    collapsed ? 'sidebar-collapsed' : '',
    mobileOpen ? 'sidebar-mobile-open' : '',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <div className={shellClass}>
      {mobileOpen ? (
        <button
          type="button"
          className="sidebar-backdrop"
          aria-label="Menüyü kapat"
          onClick={() => setMobileOpen(false)}
        />
      ) : null}

      <aside className="sidebar" aria-label="Ana menü">
        <div className="sidebar-top">
          <div className="sidebar-brand">
            <div className="sidebar-brand-mark" aria-hidden="true">
              {brandShort.slice(0, 1)}
            </div>
            <div className="sidebar-brand-text">
              <strong title={brandTitle}>{brandTitle}</strong>
              <span className="muted">Tek platform</span>
            </div>
          </div>
          <button
            type="button"
            className="sidebar-collapse-btn desktop-only"
            onClick={() => setCollapsed((v) => !v)}
            aria-pressed={collapsed}
            aria-label={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
            title={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
          >
            <Icon d={collapsed ? I.expand : I.collapse} size={16} />
          </button>
        </div>

        <div className="module-switcher" role="group" aria-label="Uygulama modülü">
          <button
            type="button"
            className={appModule === 'personnel' ? 'is-active' : ''}
            aria-pressed={appModule === 'personnel'}
            title="Personel Yönetim Takip"
            onClick={() => switchModule('personnel')}
          >
            <span className="module-switcher-icon" aria-hidden="true">
              P
            </span>
            <span className="module-switcher-full">Personel Yönetim Takip</span>
            <span className="module-switcher-short">Personel</span>
          </button>
          <button
            type="button"
            className={appModule === 'events' ? 'is-active' : ''}
            aria-pressed={appModule === 'events'}
            title="Etkinlik Yönetim Takip"
            onClick={() => switchModule('events')}
          >
            <span className="module-switcher-icon" aria-hidden="true">
              E
            </span>
            <span className="module-switcher-full">Etkinlik Yönetim Takip</span>
            <span className="module-switcher-short">Etkinlik</span>
          </button>
        </div>

        <nav className="sidebar-nav">
          {navItems.map((item) => {
            const allowed = !item.permission || hasPermission(item.permission)
            if (!allowed) {
              return (
                <span
                  key={item.to}
                  className="nav-disabled"
                  title={collapsed ? item.label : undefined}
                >
                  <span className="nav-icon">{item.icon}</span>
                  <span className="nav-label">{item.label}</span>
                </span>
              )
            }

            return (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                title={collapsed ? item.label : undefined}
                className={({ isActive }) =>
                  ['nav-link', isActive ? 'active' : '', item.badge ? 'nav-with-badge' : '']
                    .filter(Boolean)
                    .join(' ')
                }
              >
                <span className="nav-icon">{item.icon}</span>
                <span className="nav-label">{item.label}</span>
                {item.badge && item.badge > 0 ? (
                  <span className="nav-badge">{item.badge > 99 ? '99+' : item.badge}</span>
                ) : null}
              </NavLink>
            )
          })}
        </nav>
      </aside>

      <div className="main-column">
        <header className="topbar" ref={topbarRef}>
          <div className="topbar-left">
            <button
              type="button"
              className="topbar-icon-btn mobile-only"
              onClick={() => setMobileOpen(true)}
              aria-label="Menüyü aç"
            >
              <Icon d={I.menu} />
            </button>
            <button
              type="button"
              className="topbar-icon-btn desktop-only"
              onClick={() => setCollapsed((v) => !v)}
              aria-pressed={collapsed}
              aria-label={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
              title={collapsed ? 'Menüyü genişlet' : 'Menüyü daralt'}
            >
              <Icon d={collapsed ? I.expand : I.collapse} />
            </button>
            <div className="topbar-titles">
              <h1 className="page-title">{title}</h1>
            </div>
          </div>

          <form className="topbar-search" role="search" onSubmit={onGlobalSearch}>
            <Icon d={I.search} size={17} />
            <input
              type="search"
              value={globalSearch}
              onChange={(e) => setGlobalSearch(e.target.value)}
              placeholder={
                appModule === 'events'
                  ? 'Etkinlik ara…'
                  : 'Personel, sicil veya birim ara…'
              }
              aria-label="Genel arama"
            />
          </form>

          <div className="topbar-actions">
            {appModule === 'events' ? (
              hasPermission(PermissionCodes.EventsManage) ? (
                <Link to="/events/new" className="topbar-quick-add">
                  <Icon d={I.plus} size={17} />
                  <span>Etkinlik ekle</span>
                </Link>
              ) : null
            ) : hasPermission(PermissionCodes.EmployeesCreate) ? (
              <Link to="/employees/new" className="topbar-quick-add">
                <Icon d={I.plus} size={17} />
                <span>Personel ekle</span>
              </Link>
            ) : null}

            {canNotifications ? (
              <NotificationBell
                unread={unread}
                icon={<Icon d={I.bell} />}
                onChanged={() => void refreshUnread()}
              />
            ) : null}

            <AccountMenu
              avatar={
                user?.employeeId && user.hasPhoto ? (
                  <EmployeeAvatar
                    employeeId={user.employeeId}
                    name={user.displayName || user.userName}
                    hasPhoto
                    className="user-avatar-img"
                  />
                ) : (
                  <span className="user-avatar" aria-hidden="true">
                    {(user?.displayName?.trim() || user?.userName || '?')
                      .split(/\s+/)
                      .filter(Boolean)
                      .slice(0, 2)
                      .map((p) => p[0]?.toLocaleUpperCase('tr-TR') ?? '')
                      .join('') || '?'}
                  </span>
                )
              }
            />

            <button type="button" className="btn-logout" onClick={() => void logout()}>
              <span className="logout-full">Çıkış yap</span>
              <span className="logout-short">Çıkış</span>
            </button>
          </div>
        </header>

        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
