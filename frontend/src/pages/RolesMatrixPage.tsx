import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  fetchRolePermissionMatrix,
  resetRolePermissionsToSeed,
  updateRolePermissions,
  type RoleMatrixRow,
  type RolePermissionMatrix,
} from '@/api/adminApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { useConfirm } from '@/components/ConfirmDialog'

const SENSITIVE_CODES = new Set([
  'Employees.ViewPhone',
  'Employees.ViewAddress',
  'Employees.ViewNationalId',
  'Employees.ViewSpecialConditions',
  'Employees.ManageSpecialConditions',
  'Notes.ViewManager',
  'Notes.ViewDirectorate',
  'Notes.CreateManager',
])

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
    case 'UNIT_MANAGER':
      return 'unit'
    case 'ADMINISTRATIVE_OFFICER':
      return 'unit'
    case 'DATA_ENTRY':
      return 'entry'
    case 'VIEWER':
      return 'viewer'
    default:
      return 'default'
  }
}

function summarizeRole(codes: string[]) {
  const tags: string[] = []
  if (
    codes.some(
      (c) =>
        c.includes('.Create') ||
        c.includes('.Update') ||
        c.includes('.Manage') ||
        c.includes('.SetStatus'),
    )
  ) {
    tags.push('Düzenleme')
  } else {
    tags.push('Yalnızca görüntüleme')
  }
  if (codes.some((c) => SENSITIVE_CODES.has(c))) tags.push('Hassas bilgiler')
  if (codes.includes('Employees.ViewAllUnits')) tags.push('Tüm birimler')
  if (
    codes.includes('Users.Manage') ||
    codes.includes('Roles.Manage') ||
    codes.includes('Settings.Manage')
  ) {
    tags.push('Sistem yönetimi')
  }
  if (codes.includes('Reports.View')) tags.push('Raporlar')
  return tags
}

const GROUP_ICONS: Record<string, string> = {
  Dashboard: 'M4 4h7v7H4zM13 4h7v4h-7zM13 10h7v10h-7zM4 13h7v7H4z',
  Personel:
    'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
  'Hassas Alan': 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
  Görev: 'M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2M9 5a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2M9 12h6M9 16h6',
  Profil: 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z',
  Notlar:
    'M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6M16 13H8M16 17H8M10 9H8',
  Organizasyon: 'M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2zM9 22V12h6v10',
  Raporlar: 'M18 20V10M12 20V4M6 20v-6',
  'Veri Kalitesi': 'M22 11.08V12a10 10 0 1 1-5.93-9.14M22 4 12 14.01l-3-3',
  Dosyalar: 'M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z',
  Sistem: 'M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6',
}

function groupIconPath(group: string) {
  return GROUP_ICONS[group] ?? 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z'
}

export function RolesMatrixPage() {
  const { hasPermission } = useAuth()
  const confirm = useConfirm()
  const canManage = hasPermission(PermissionCodes.RolesManage)

  const [data, setData] = useState<RolePermissionMatrix | null>(null)
  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null)
  const [draftCodes, setDraftCodes] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)
  const [search, setSearch] = useState('')

  const load = useCallback(async () => {
    if (!canManage) return
    setLoading(true)
    setError(null)
    try {
      const matrix = await fetchRolePermissionMatrix()
      setData(matrix)
      setSelectedRoleId((prev) => {
        if (prev && matrix.roles.some((r) => r.id === prev)) return prev
        return matrix.roles[0]?.id ?? null
      })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [canManage])

  useEffect(() => {
    void load()
  }, [load])

  const selectedRole: RoleMatrixRow | null = useMemo(() => {
    if (!data || !selectedRoleId) return null
    return data.roles.find((r) => r.id === selectedRoleId) ?? null
  }, [data, selectedRoleId])

  useEffect(() => {
    if (selectedRole) {
      setDraftCodes([...selectedRole.permissionCodes])
      setInfo(null)
      setError(null)
    }
  }, [selectedRole])

  const dirty = useMemo(() => {
    if (!selectedRole) return false
    const a = [...draftCodes].sort()
    const b = [...selectedRole.permissionCodes].sort()
    return a.length !== b.length || a.some((c, i) => c !== b[i])
  }, [draftCodes, selectedRole])

  const totalPerms = useMemo(
    () => data?.permissionGroups.reduce((s, g) => s + g.items.length, 0) ?? 0,
    [data],
  )

  const filteredGroups = useMemo(() => {
    if (!data) return []
    const q = search.trim().toLowerCase()
    if (!q) return data.permissionGroups
    return data.permissionGroups
      .map((g) => ({
        ...g,
        items: g.items.filter(
          (p) =>
            p.name.toLowerCase().includes(q) ||
            p.description.toLowerCase().includes(q) ||
            g.group.toLowerCase().includes(q),
        ),
      }))
      .filter((g) => g.items.length > 0)
  }, [data, search])

  const selectedTags = useMemo(() => summarizeRole(draftCodes), [draftCodes])

  function toggle(code: string) {
    setDraftCodes((prev) =>
      prev.includes(code) ? prev.filter((c) => c !== code) : [...prev, code],
    )
  }

  function selectAllInGroup(codes: string[]) {
    setDraftCodes((prev) => {
      const set = new Set(prev)
      const allOn = codes.every((c) => set.has(c))
      if (allOn) codes.forEach((c) => set.delete(c))
      else codes.forEach((c) => set.add(c))
      return [...set]
    })
  }

  async function save() {
    if (!selectedRole) return
    setSaving(true)
    setError(null)
    setInfo(null)
    try {
      await updateRolePermissions(selectedRole.id, draftCodes)
      setInfo(
        'Değişiklikler kaydedildi. Bu role sahip kullanıcıların yeniden giriş yapması gerekir.',
      )
      await load()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Kaydedilemedi.')
    } finally {
      setSaving(false)
    }
  }

  async function resetToSeed() {
    if (!selectedRole) return
    const ok = await confirm({
      title: 'Varsayılana döndür',
      message: `“${selectedRole.name}” rolünü kurumsal varsayılan ayarlara döndürmek istiyor musunuz?`,
      confirmLabel: 'Varsayılana döndür',
      tone: 'primary',
    })
    if (!ok) return
    setSaving(true)
    setError(null)
    setInfo(null)
    try {
      await resetRolePermissionsToSeed(selectedRole.id)
      setInfo('Rol varsayılan ayarlara döndürüldü. Kullanıcıların yeniden giriş yapması gerekir.')
      await load()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Sıfırlanamadı.')
    } finally {
      setSaving(false)
    }
  }

  if (!canManage) {
    return (
      <div className="org-page">
        <div className="panel">
          <h1>Rol ve yetkiler</h1>
          <p className="muted">Bu ekranı görüntüleme yetkiniz bulunmuyor.</p>
        </div>
      </div>
    )
  }

  return (
    <div className="org-page iam-page matrix-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Yetkilendirme</p>
          <h1>Rol ve yetkiler</h1>
          <p className="muted">
            Her rol için hangi bilgilere erişileceğini ve hangi işlemlerin yapılabileceğini
            belirleyin. Telefon, adres, TCKN ve özel durum gibi hassas alanlar ayrı ayrı açılır.
          </p>
        </div>
        <div className="report-hero-side">
          {data ? (
            <div className="report-hero-stats">
              <div>
                <strong>{data.roles.length}</strong>
                <span>Rol</span>
              </div>
              <div>
                <strong>{totalPerms}</strong>
                <span>Yetki</span>
              </div>
              <div>
                <strong>{data.roles.filter((r) => r.differsFromSeed).length}</strong>
                <span>Özel ayar</span>
              </div>
            </div>
          ) : null}
          <Link to="/users" className="btn-secondary">
            Kullanıcılar
          </Link>
        </div>
      </header>

      {error ? (
        <div className="panel form-error" role="alert">
          {error}
        </div>
      ) : null}
      {info ? <div className="panel form-success">{info}</div> : null}

      {loading || !data ? (
        <section className="panel">
          <p className="muted">Yükleniyor…</p>
        </section>
      ) : (
        <div className="matrix-layout">
          <aside className="matrix-roles panel">
            <h2>Roller</h2>
            <ul className="role-pick-list">
              {data.roles.map((r) => {
                const pct =
                  totalPerms > 0
                    ? Math.round((r.permissionCodes.length / totalPerms) * 100)
                    : 0
                return (
                  <li key={r.id}>
                    <button
                      type="button"
                      className={`role-pick tone-${roleTone(r.code)} ${
                        r.id === selectedRoleId ? 'active' : ''
                      }`}
                      onClick={() => setSelectedRoleId(r.id)}
                    >
                      <strong>{r.name}</strong>
                      <span>
                        {r.permissionCodes.length} / {totalPerms} erişim hakkı
                        {r.differsFromSeed ? ' · özel ayar' : ''}
                      </span>
                      <span
                        className="role-pick-bar"
                        aria-hidden
                      >
                        <span style={{ width: `${pct}%` }} />
                      </span>
                    </button>
                  </li>
                )
              })}
            </ul>
          </aside>

          <div className="matrix-editor panel">
            {selectedRole ? (
              <>
                <div className="matrix-editor-head">
                  <div>
                    <h2>{selectedRole.name}</h2>
                    <p className="muted iam-role-desc">{selectedRole.description}</p>
                    <div className="iam-tag-row">
                      {selectedTags.map((t) => (
                        <span key={t} className="iam-tag">
                          {t}
                        </span>
                      ))}
                      <span className="iam-tag">
                        {draftCodes.length} / {totalPerms} açık
                      </span>
                      {selectedRole.differsFromSeed ? (
                        <span className="iam-tag is-warn">Varsayılandan farklı</span>
                      ) : null}
                      {dirty ? <span className="iam-tag is-dirty">Kaydedilmedi</span> : null}
                    </div>
                  </div>
                </div>

                <div className={`matrix-sticky-actions ${dirty ? 'is-dirty' : ''}`}>
                  <p className="matrix-sticky-hint">
                    {dirty
                      ? 'Kaydedilmemiş değişiklikler var'
                      : `${draftCodes.length} / ${totalPerms} yetki açık`}
                  </p>
                  <div className="form-actions">
                    <button
                      type="button"
                      className="btn-secondary"
                      disabled={saving || !selectedRole.differsFromSeed}
                      onClick={() => void resetToSeed()}
                    >
                      Varsayılana dön
                    </button>
                    <button
                      type="button"
                      className="btn-primary"
                      disabled={saving || !dirty}
                      onClick={() => void save()}
                    >
                      {saving ? 'Kaydediliyor…' : 'Kaydet'}
                    </button>
                  </div>
                </div>

                <div className="matrix-filter">
                  <svg className="search-icon" viewBox="0 0 24 24" fill="none" aria-hidden>
                    <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" />
                    <path
                      d="m20 20-3.5-3.5"
                      stroke="currentColor"
                      strokeWidth="2"
                      strokeLinecap="round"
                    />
                  </svg>
                  <input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Yetki ara…"
                    aria-label="Yetki ara"
                  />
                </div>

                <div className="permission-groups">
                  {filteredGroups.map((g) => {
                    const codes = g.items.map((i) => i.code)
                    const onCount = codes.filter((c) => draftCodes.includes(c)).length
                    return (
                      <section key={g.group} className="perm-group">
                        <header className="perm-group-head">
                          <div className="perm-group-title">
                            <span className="perm-group-icon" aria-hidden>
                              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
                                <path d={groupIconPath(g.group)} />
                              </svg>
                            </span>
                            <div>
                              <h3>{g.group}</h3>
                              <span>
                                {onCount} / {codes.length} açık
                              </span>
                            </div>
                          </div>
                          <button
                            type="button"
                            className="link-btn"
                            onClick={() => selectAllInGroup(codes)}
                          >
                            {onCount === codes.length ? 'Tümünü kapat' : 'Tümünü aç'}
                          </button>
                        </header>
                        <div className="perm-table">
                          {g.items.map((p) => {
                            const inSeed = selectedRole.seedPermissionCodes.includes(p.code)
                            const on = draftCodes.includes(p.code)
                            const drifted = on !== inSeed
                            return (
                              <label
                                key={p.code}
                                className={`perm-row ${on ? 'is-on' : ''} ${
                                  drifted ? 'is-drift' : ''
                                } ${SENSITIVE_CODES.has(p.code) ? 'is-field' : ''}`}
                              >
                                <input
                                  type="checkbox"
                                  checked={on}
                                  onChange={() => toggle(p.code)}
                                />
                                <span className="perm-main">
                                  <strong>{p.name}</strong>
                                  {p.description ? <small>{p.description}</small> : null}
                                </span>
                                {SENSITIVE_CODES.has(p.code) ? (
                                  <span className="perm-badge">Hassas</span>
                                ) : null}
                              </label>
                            )
                          })}
                        </div>
                      </section>
                    )
                  })}
                  {filteredGroups.length === 0 ? (
                    <p className="muted">Aramayla eşleşen yetki yok.</p>
                  ) : null}
                </div>
              </>
            ) : null}
          </div>
        </div>
      )}
    </div>
  )
}
