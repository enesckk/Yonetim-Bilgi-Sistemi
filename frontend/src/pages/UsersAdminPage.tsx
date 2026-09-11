import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  createUser,
  fetchRoles,
  fetchUsers,
  resetUserPassword,
  updateUser,
  type RoleListItem,
  type UserListItem,
} from '@/api/adminApi'
import { fetchEmployees, type EmployeeListItem } from '@/api/employeesApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

type FormMode = 'create' | 'edit'

function EyeIcon({ open }: { open: boolean }) {
  if (open) {
    return (
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path
          d="M3 3l18 18M10.6 10.6a2 2 0 0 0 2.8 2.8M9.9 5.1A10.4 10.4 0 0 1 12 5c5 0 9.3 3.1 11 7.5a12.3 12.3 0 0 1-4.2 5.1M6.7 6.7A12.4 12.4 0 0 0 1 12.5C2.7 16.9 7 20 12 20c1.6 0 3.1-.3 4.5-.9"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
    )
  }

  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M2 12.5C3.7 8.1 8 5 12 5s8.3 3.1 10 7.5c-1.7 4.4-6 7.5-10 7.5S3.7 16.9 2 12.5Z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12.5" r="2.5" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  )
}

function formatLastLogin(value?: string | null) {
  if (!value) return 'Hiç giriş yok'
  return new Date(value).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function roleTone(code?: string | null) {
  switch (code) {
    case 'SYSTEM_ADMIN':
      return 'admin'
    case 'DEPUTY_MAYOR':
      return 'exec'
    case 'DIRECTOR':
      return 'director'
    case 'DEPUTY_DIRECTOR':
      return 'deputy'
    case 'ADMINISTRATIVE_OFFICER':
      return 'idari'
    case 'UNIT_MANAGER':
      return 'unit'
    case 'DATA_ENTRY':
      return 'entry'
    case 'VIEWER':
      return 'viewer'
    default:
      return 'default'
  }
}

function slugUserName(fullName: string) {
  return fullName
    .toLocaleLowerCase('tr-TR')
    .replaceAll('ı', 'i')
    .replaceAll('ğ', 'g')
    .replaceAll('ü', 'u')
    .replaceAll('ş', 's')
    .replaceAll('ö', 'o')
    .replaceAll('ç', 'c')
    .replace(/[^a-z0-9]+/g, '.')
    .replace(/^\.+|\.+$/g, '')
    .slice(0, 40)
}

function roleNeedsPerson(code?: string | null) {
  return code === 'ADMINISTRATIVE_OFFICER' || code === 'UNIT_MANAGER'
}

const ROLE_CREATE_ORDER = [
  'ADMINISTRATIVE_OFFICER',
  'DIRECTOR',
  'UNIT_MANAGER',
  'DEPUTY_DIRECTOR',
  'DATA_ENTRY',
  'VIEWER',
  'DEPUTY_MAYOR',
  'SYSTEM_ADMIN',
]

function accessPreview(roleCode: string | undefined, person: EmployeeListItem | null) {
  const place = person?.facilityName || person?.unitName || 'bağlı birimi'
  if (roleCode === 'DIRECTOR' || roleCode === 'SYSTEM_ADMIN' || roleCode === 'DEPUTY_MAYOR') {
    return {
      title: 'Müdürlük geneli',
      lines: ['Tüm tesis, personel, stok ve takvim.'],
    }
  }
  if (roleCode === 'ADMINISTRATIVE_OFFICER') {
    return {
      title: `${place} idari amiri`,
      lines: ['Yalnızca bu bina, kadro, stok ve takvim.'],
    }
  }
  if (roleCode === 'UNIT_MANAGER') {
    return {
      title: `${place} birim amiri`,
      lines: ['Yalnızca bu birim ve personeli.'],
    }
  }
  return {
    title: 'Seçilen role göre erişim',
    lines: ['Menü atanan yetkiye göre açılır.'],
  }
}

function roleScopeHint(code?: string | null) {
  if (code === 'DIRECTOR' || code === 'SYSTEM_ADMIN' || code === 'DEPUTY_MAYOR') return 'Müdürlük geneli'
  if (roleNeedsPerson(code)) return 'Kendi binası'
  return 'Yetkiye göre'
}

export function UsersAdminPage() {
  const { hasPermission, user: me } = useAuth()
  const canManage = hasPermission(PermissionCodes.UsersManage)
  const canSeeMatrix = hasPermission(PermissionCodes.RolesManage)

  const [users, setUsers] = useState<UserListItem[]>([])
  const [roles, setRoles] = useState<RoleListItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [showForm, setShowForm] = useState(false)
  const [mode, setMode] = useState<FormMode>('create')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState('')
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'passive'>('all')

  const [userName, setUserName] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isActive, setIsActive] = useState(true)
  const [roleCodes, setRoleCodes] = useState<string[]>([])
  const [employeeId, setEmployeeId] = useState('')
  const [personQuery, setPersonQuery] = useState('')
  const [people, setPeople] = useState<EmployeeListItem[]>([])
  const [resetPwd, setResetPwd] = useState('')
  const [showResetPwd, setShowResetPwd] = useState(false)

  const load = useCallback(async () => {
    if (!canManage) return
    setLoading(true)
    setError(null)
    try {
      const [u, r, staff] = await Promise.all([
        fetchUsers(),
        fetchRoles(),
        fetchEmployees({ page: 1, pageSize: 250 }).catch(
          () => ({ items: [] as EmployeeListItem[] }),
        ),
      ])
      setUsers(u)
      setRoles(r)
      setPeople(staff.items ?? [])
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canManage])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!showForm) return

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !saving) closeForm()
    }

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.body.style.overflow = previousOverflow
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [showForm, saving])

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return users.filter((u) => {
      if (statusFilter === 'active' && !u.isActive) return false
      if (statusFilter === 'passive' && u.isActive) return false
      if (roleFilter && !u.roleCodes.includes(roleFilter)) return false
      if (!q) return true
      return (
        u.displayName.toLowerCase().includes(q) ||
        u.userName.toLowerCase().includes(q) ||
        u.email.toLowerCase().includes(q) ||
        u.roleNames.some((n) => n.toLowerCase().includes(q))
      )
    })
  }, [users, search, roleFilter, statusFilter])

  const stats = useMemo(
    () => ({
      total: users.length,
      active: users.filter((u) => u.isActive).length,
      passive: users.filter((u) => !u.isActive).length,
      roles: roles.length,
    }),
    [users, roles],
  )

  const selectedPerson = useMemo(
    () => people.find((p) => p.id === employeeId) ?? null,
    [people, employeeId],
  )

  const personChoices = useMemo(() => {
    const q = personQuery.trim().toLocaleLowerCase('tr-TR')
    const linked = new Set(
      users.filter((u) => u.employeeId && u.id !== editingId).map((u) => u.employeeId as string),
    )
    return people
      .filter((p) => !linked.has(p.id) || p.id === employeeId)
      .filter((p) =>
        !q
          ? true
          : p.fullName.toLocaleLowerCase('tr-TR').includes(q)
            || (p.facilityName ?? '').toLocaleLowerCase('tr-TR').includes(q)
            || (p.unitName ?? '').toLocaleLowerCase('tr-TR').includes(q),
      )
      .slice(0, 8)
  }, [people, personQuery, users, editingId, employeeId])

  const preview = accessPreview(roleCodes[0], selectedPerson)
  const sortedRoles = useMemo(() => {
    const rank = (code?: string | null) => {
      const i = ROLE_CREATE_ORDER.indexOf(code ?? '')
      return i === -1 ? 99 : i
    }
    return [...roles].sort((a, b) => rank(a.code) - rank(b.code))
  }, [roles])
  const missingPerson = roleNeedsPerson(roleCodes[0]) && !employeeId

  if (!canManage) {
    return (
      <div className="org-page">
        <div className="panel">
          <h1>Kullanıcı yönetimi</h1>
          <p className="muted">Bu ekranı görüntüleme yetkiniz bulunmuyor.</p>
        </div>
      </div>
    )
  }

  function openCreate() {
    setMode('create')
    setEditingId(null)
    setUserName('')
    setDisplayName('')
    setEmail('')
    setPassword('')
    setShowPassword(false)
    setIsActive(true)
    setRoleCodes(['ADMINISTRATIVE_OFFICER'])
    setEmployeeId('')
    setPersonQuery('')
    setResetPwd('')
    setShowResetPwd(false)
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function openEdit(u: UserListItem) {
    setMode('edit')
    setEditingId(u.id)
    setUserName(u.userName)
    setDisplayName(u.displayName)
    setEmail(u.email)
    setPassword('')
    setShowPassword(false)
    setIsActive(u.isActive)
    setRoleCodes([...u.roleCodes])
    setEmployeeId(u.employeeId ?? '')
    setPersonQuery(u.employeeName ?? '')
    setResetPwd('')
    setShowResetPwd(false)
    setFieldErrors({})
    setError(null)
    setShowForm(true)
  }

  function closeForm() {
    setShowForm(false)
    setFieldErrors({})
  }

  function selectRole(code: string) {
    setRoleCodes([code])
  }

  function pickPerson(person: EmployeeListItem) {
    setEmployeeId(person.id)
    setPersonQuery(person.fullName)
    setDisplayName(person.fullName)
    if (mode === 'create') {
      const slug = slugUserName(person.fullName)
      if (slug) setUserName(slug)
      setEmail(`${slug}@sehitkamil.local`)
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    setFieldErrors({})
    try {
      if (mode === 'create') {
        await createUser({
          userName,
          displayName,
          email,
          password,
          roleCodes,
          employeeId: employeeId || null,
        })
      } else if (editingId) {
        await updateUser(editingId, {
          displayName,
          email,
          isActive,
          roleCodes,
          employeeId: employeeId || null,
        })
        if (resetPwd.trim()) await resetUserPassword(editingId, resetPwd.trim())
      }
      setShowForm(false)
      await load()
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
        if (err.validationErrors) setFieldErrors(err.validationErrors)
      } else setError('Kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  function fieldError(name: string) {
    const msgs = fieldErrors[name]
    if (!msgs?.length) return null
    return <span className="field-error">{msgs.join(' ')}</span>
  }

  return (
    <div className="org-page iam-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Yetkilendirme</p>
          <h1>Kullanıcı yönetimi</h1>
          <p className="muted">Kim girecek, ne görecek.</p>
        </div>
        <div className="report-hero-side">
          <div className="report-hero-stats">
            <div>
              <strong>{stats.total}</strong>
              <span>Hesap</span>
            </div>
            <div>
              <strong>{stats.active}</strong>
              <span>Aktif</span>
            </div>
            <div>
              <strong>{stats.passive}</strong>
              <span>Pasif</span>
            </div>
            <div>
              <strong>{stats.roles}</strong>
              <span>Rol</span>
            </div>
          </div>
          {canSeeMatrix ? (
            <Link to="/roles" className="btn-secondary">
              Rolleri düzenle
            </Link>
          ) : null}
          <button type="button" className="btn-primary" onClick={openCreate} disabled={showForm && mode === 'create'}>
            Yeni kullanıcı
          </button>
        </div>
      </header>

      {error && !showForm ? (
        <div className="panel form-error" role="alert">
          {error}
        </div>
      ) : null}

      {showForm ? (
        <div
          className="org-modal-backdrop"
          role="presentation"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget && !saving) closeForm()
          }}
        >
          <form
            className="org-modal panel iam-form iam-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="user-form-title"
            onSubmit={(e) => void onSubmit(e)}
          >
          <div className="org-modal-head">
            <p className="org-modal-eyebrow">Kullanıcı</p>
            <h2 id="user-form-title">
              {mode === 'create' ? 'Yeni kullanıcı' : 'Kullanıcıyı düzenle'}
            </h2>
          </div>

          {error ? (
            <div className="form-error" role="alert">
              {error}
            </div>
          ) : null}

          <section className="iam-form-section">
            <header className="iam-form-section-head">
              <h2>Bu kim?</h2>
            </header>
            <label>
              Personel
              <input
                value={personQuery}
                onChange={(e) => {
                  setPersonQuery(e.target.value)
                  if (employeeId) setEmployeeId('')
                }}
                placeholder="Ad veya tesis ara… örn. Tarık, DT"
                autoComplete="off"
                autoFocus
              />
              {fieldError('employeeId')}
            </label>
            {selectedPerson ? (
              <p className="iam-person-picked">
                <strong>{selectedPerson.fullName}</strong>
                <span>
                  {[selectedPerson.primaryDutyName, selectedPerson.facilityName || selectedPerson.unitName]
                    .filter(Boolean)
                    .join(' · ')}
                </span>
                <button type="button" className="btn-secondary" onClick={() => { setEmployeeId(''); setPersonQuery('') }}>
                  Değiştir
                </button>
              </p>
            ) : (
              <ul className="iam-person-choices">
                {personChoices.map((p) => (
                  <li key={p.id}>
                    <button type="button" onClick={() => pickPerson(p)}>
                      <strong>{p.fullName}</strong>
                      <span>
                        {[p.primaryDutyName, p.facilityName || p.unitName].filter(Boolean).join(' · ') || 'Birim yok'}
                      </span>
                    </button>
                  </li>
                ))}
                {personChoices.length === 0 ? <li className="muted">Eşleşen personel yok.</li> : null}
              </ul>
            )}
          </section>

          <section className="iam-form-section">
            <header className="iam-form-section-head">
              <h2>Bu ne olacak?</h2>
            </header>
            <div className="iam-role-pick iam-role-pick-grid">
              {sortedRoles.map((r) =>
                r.code ? (
                  <label
                    key={r.id}
                    className={`iam-role-option tone-${roleTone(r.code)} ${
                      roleCodes.includes(r.code) ? 'is-on' : ''
                    }`}
                  >
                    <input
                      type="radio"
                      name="user-role"
                      checked={roleCodes.includes(r.code)}
                      onChange={() => selectRole(r.code!)}
                    />
                    <span>
                      <strong>{r.name}</strong>
                      <small>{roleScopeHint(r.code)}</small>
                    </span>
                  </label>
                ) : null,
              )}
            </div>
            {fieldError('roleCodes')}
            {roleCodes[0] ? (
              <aside className="iam-access-preview" aria-live="polite">
                <strong>{preview.title}</strong>
                <ul>
                  {preview.lines.map((line) => (
                    <li key={line}>{line}</li>
                  ))}
                </ul>
                {missingPerson ? (
                  <p className="form-error">Bu görev için yukarıdan personel seçin.</p>
                ) : null}
              </aside>
            ) : null}
          </section>

          <section className="iam-form-section">
            <header className="iam-form-section-head">
              <h2>Hesap bilgileri</h2>
            </header>
            <div className="form-grid">
              {mode === 'create' ? (
                <label>
                  Giriş adı *
                  <input
                    value={userName}
                    onChange={(e) => setUserName(e.target.value)}
                    autoComplete="off"
                  />
                  {fieldError('userName')}
                </label>
              ) : (
                <label>
                  Giriş adı
                  <input value={userName} disabled readOnly />
                </label>
              )}
              <label>
                Görünen ad *
                <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
                {fieldError('displayName')}
              </label>
              <label>
                E-posta *
                <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
                {fieldError('email')}
              </label>
              {mode === 'create' ? (
                <label>
                  Şifre *
                  <div className="login-password-field">
                    <input
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      autoComplete="new-password"
                    />
                    <button
                      type="button"
                      className="login-password-toggle"
                      onClick={() => setShowPassword((v) => !v)}
                      aria-pressed={showPassword}
                      aria-label={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                    >
                      <EyeIcon open={showPassword} />
                    </button>
                  </div>
                  {fieldError('password')}
                </label>
              ) : (
                <>
                  <label className="checkbox-row iam-active-row">
                    <input
                      type="checkbox"
                      checked={isActive}
                      disabled={me?.id === editingId}
                      onChange={(e) => setIsActive(e.target.checked)}
                    />
                    <span>
                      <strong>Hesap aktif</strong>
                      <small>
                        {me?.id === editingId
                          ? 'Kendi hesabınızı pasife alamazsınız.'
                          : 'Pasif hesaplarla sisteme giriş yapılamaz.'}
                      </small>
                    </span>
                  </label>
                  <label>
                    Yeni şifre (opsiyonel)
                    <div className="login-password-field">
                      <input
                        type={showResetPwd ? 'text' : 'password'}
                        value={resetPwd}
                        onChange={(e) => setResetPwd(e.target.value)}
                        placeholder="Boş bırakırsanız değişmez"
                        autoComplete="new-password"
                      />
                      <button
                        type="button"
                        className="login-password-toggle"
                        onClick={() => setShowResetPwd((v) => !v)}
                        aria-pressed={showResetPwd}
                        aria-label={showResetPwd ? 'Şifreyi gizle' : 'Şifreyi göster'}
                      >
                        <EyeIcon open={showResetPwd} />
                      </button>
                    </div>
                    {fieldError('newPassword')}
                  </label>
                </>
              )}
            </div>
          </section>

          <div className="iam-form-actions">
            <button type="submit" className="btn-primary" disabled={saving || missingPerson}>
              {saving
                ? 'Kaydediliyor…'
                : mode === 'create'
                  ? 'Kullanıcıyı oluştur'
                  : 'Değişiklikleri kaydet'}
            </button>
            <button type="button" className="btn-secondary" onClick={closeForm}>
              İptal
            </button>
          </div>
          </form>
        </div>
      ) : null}

      <section className="panel">
        <div className="iam-toolbar">
          <label className="iam-search">
            <span className="sr-only">Ara</span>
            <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden>
              <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
              <path d="m20 20-3.5-3.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Ad, giriş adı veya e-posta ara…"
            />
          </label>
          <select value={roleFilter} onChange={(e) => setRoleFilter(e.target.value)}>
            <option value="">Tüm roller</option>
            {roles.map((r) =>
              r.code ? (
                <option key={r.id} value={r.code}>
                  {r.name}
                </option>
              ) : null,
            )}
          </select>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as 'all' | 'active' | 'passive')}
          >
            <option value="all">Tüm durumlar</option>
            <option value="active">Aktif</option>
            <option value="passive">Pasif</option>
          </select>
          <span className="iam-toolbar-count">{filtered.length} kayıt</span>
        </div>

        {loading ? (
          <p className="muted">Yükleniyor…</p>
        ) : filtered.length === 0 ? (
          <div className="iam-empty">
            <strong>Kayıt bulunamadı</strong>
            <p className="muted">Arama veya filtreleri gevşetin.</p>
          </div>
        ) : (
          <div className="table-wrap">
            <table className="data-table iam-table">
              <thead>
                <tr>
                  <th>Ad</th>
                  <th>E-posta</th>
                  <th>Rol</th>
                  <th>Durum</th>
                  <th>Son giriş</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {filtered.map((u) => (
                  <tr key={u.id}>
                    <td>
                      <div className="iam-person">
                        <span className="iam-avatar" aria-hidden="true">
                          {(u.displayName || '?')
                            .split(/\s+/)
                            .filter(Boolean)
                            .slice(0, 2)
                            .map((p) => p[0]?.toLocaleUpperCase('tr-TR') ?? '')
                            .join('')}
                        </span>
                        <span>
                          <strong>{u.displayName}</strong>
                          <div className="iam-meta">
                            <span>{u.userName}</span>
                            {u.employeeName ? <span>{u.employeeName}</span> : null}
                          </div>
                        </span>
                      </div>
                    </td>
                    <td>{u.email}</td>
                    <td>
                      <div className="iam-role-chips">
                        {u.roleNames.length === 0 ? (
                          <span className="muted">—</span>
                        ) : (
                          u.roleNames.map((name, i) => (
                            <span
                              key={`${u.id}-${name}`}
                              className={`iam-chip tone-${roleTone(u.roleCodes[i])}`}
                            >
                              {name}
                            </span>
                          ))
                        )}
                      </div>
                    </td>
                    <td>
                      <span className={`iam-status ${u.isActive ? 'is-active' : 'is-passive'}`}>
                        {u.isActive ? 'Aktif' : 'Pasif'}
                      </span>
                    </td>
                    <td className="iam-login">{formatLastLogin(u.lastLoginAtUtc)}</td>
                    <td>
                      <button type="button" className="btn-secondary" onClick={() => openEdit(u)}>
                        Düzenle
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="panel">
        <header className="iam-section-head">
          <div>
            <h2>Kurumsal roller</h2>
          </div>
          {canSeeMatrix ? (
            <Link to="/roles" className="btn-ghost">
              Detaylı ayar ›
            </Link>
          ) : null}
        </header>
        <div className="iam-role-cards">
          {roles.map((r) => (
            <article key={r.id} className={`iam-role-card tone-${roleTone(r.code)}`}>
              <header>
                <strong>{r.name}</strong>
              </header>
              <p>{roleScopeHint(r.code)}</p>
              <footer>
                <span>{r.userCount} kullanıcı</span>
                <span>{r.permissionCount} erişim hakkı</span>
              </footer>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
