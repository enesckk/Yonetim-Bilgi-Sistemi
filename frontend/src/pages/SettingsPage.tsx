import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  fetchAppSettings,
  updateAppSetting,
  type AppSetting,
  type AppSettingValueType,
} from '@/api/settingsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

const FRIENDLY: Record<string, { description?: string; hint?: string }> = {
  'Organization.DisplayName': {
    description: 'PDF başlıkları ve kurumsal çıktılarda görünen ad.',
    hint: 'Örn. Şehitkamil Kültür Müdürlüğü',
  },
  'Reports.ExcelMaxRows': {
    description: 'Excel dışa aktarımında tek seferde indirilebilecek en fazla satır sayısı.',
    hint: '1 – 100.000',
  },
  'Reports.PdfMaxRows': {
    description: 'PDF dışa aktarımında tek seferde indirilebilecek en fazla satır sayısı.',
    hint: 'PDF daha ağırdır; Excel’den düşük tutun',
  },
  'DataQuality.ActiveOnlyDefault': {
    description: 'Veri kalitesi ekranı açıldığında yalnızca aktif personeli listelesin.',
  },
  'Notifications.EmailEnabled': {
    description:
      'Kritik uyarılar e-posta ile de gönderilsin. Sunucuda e-posta (SMTP) ayarı gerekir.',
  },
}

function typeLabel(t: AppSettingValueType): string {
  switch (t) {
    case 2:
      return 'Sayı'
    case 3:
      return 'Açık / Kapalı'
    default:
      return 'Metin'
  }
}

function settingDescription(s: AppSetting): string {
  return FRIENDLY[s.key]?.description ?? s.description ?? ''
}

function settingHint(s: AppSetting): string | undefined {
  return FRIENDLY[s.key]?.hint
}

export function SettingsPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(PermissionCodes.SettingsManage)

  const [items, setItems] = useState<AppSetting[]>([])
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(true)
  const [savingKey, setSavingKey] = useState<string | null>(null)
  const [savingAll, setSavingAll] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)
  const [activeGroup, setActiveGroup] = useState<string>('all')

  const load = useCallback(async (opts?: { silent?: boolean }) => {
    if (!canManage) return
    if (!opts?.silent) setLoading(true)
    setError(null)
    try {
      const list = await fetchAppSettings()
      setItems(list)
      setDrafts((prev) => {
        const next: Record<string, string> = {}
        for (const s of list) {
          // Kaydedilmemiş taslağı koru; yeni/değişmeyen değerleri sunucudan al
          const dirty = prev[s.key] !== undefined && prev[s.key] !== s.value
          next[s.key] = dirty ? prev[s.key]! : s.value
        }
        return next
      })
    } catch (err) {
      setItems([])
      setError(err instanceof ApiClientError ? err.message : 'Ayarlar yüklenemedi.')
    } finally {
      if (!opts?.silent) setLoading(false)
    }
  }, [canManage])

  useEffect(() => {
    void load()
  }, [load])

  const groups = useMemo(() => {
    const names = [...new Set(items.map((s) => s.groupName))]
    return names.sort((a, b) => a.localeCompare(b, 'tr'))
  }, [items])

  const dirtyKeys = useMemo(() => {
    return items.filter((s) => !s.isReadOnly && (drafts[s.key] ?? '') !== s.value).map((s) => s.key)
  }, [items, drafts])

  const visible = useMemo(() => {
    if (activeGroup === 'all') return items
    return items.filter((s) => s.groupName === activeGroup)
  }, [items, activeGroup])

  const groupCounts = useMemo(() => {
    const map = new Map<string, number>()
    for (const s of items) map.set(s.groupName, (map.get(s.groupName) ?? 0) + 1)
    return map
  }, [items])

  function setDraft(key: string, value: string) {
    setDrafts((prev) => ({ ...prev, [key]: value }))
    setInfo(null)
  }

  function revert(key: string) {
    const original = items.find((s) => s.key === key)?.value
    if (original === undefined) return
    setDraft(key, original)
  }

  async function save(key: string) {
    const value = drafts[key] ?? ''
    const label = items.find((s) => s.key === key)?.displayName ?? 'Ayar'
    setSavingKey(key)
    setError(null)
    setInfo(null)
    try {
      await updateAppSetting(key, value)
      setItems((prev) => prev.map((s) => (s.key === key ? { ...s, value } : s)))
      setDrafts((prev) => ({ ...prev, [key]: value }))
      setInfo(`“${label}” kaydedildi.`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Kaydedilemedi.')
    } finally {
      setSavingKey(null)
    }
  }

  async function saveAll() {
    if (dirtyKeys.length === 0) return
    setSavingAll(true)
    setError(null)
    setInfo(null)
    let ok = 0
    try {
      for (const key of dirtyKeys) {
        await updateAppSetting(key, drafts[key] ?? '')
        ok += 1
      }
      await load({ silent: true })
      setInfo(
        ok === 1
          ? '1 ayar kaydedildi.'
          : `${ok} ayar kaydedildi.`,
      )
    } catch (err) {
      setError(
        err instanceof ApiClientError
          ? err.message
          : `${ok} ayar kaydedildi; kalanlar kaydedilemedi.`,
      )
      await load({ silent: true })
    } finally {
      setSavingAll(false)
    }
  }

  function revertAll() {
    const next: Record<string, string> = {}
    for (const s of items) next[s.key] = s.value
    setDrafts(next)
    setInfo(null)
    setError(null)
  }

  if (!canManage) {
    return (
      <div className="org-page settings-page">
        <header className="org-hero panel">
          <div>
            <p className="org-eyebrow">Sistem yönetimi</p>
            <h1>Ayarlar</h1>
            <p className="muted">Bu ekranı görüntüleme yetkiniz bulunmuyor.</p>
          </div>
        </header>
        <div className="panel">
          <p className="form-error">Sistem ayarlarını yalnızca yetkili yöneticiler yönetebilir.</p>
        </div>
      </div>
    )
  }

  const busy = savingKey !== null || savingAll

  return (
    <div className="org-page settings-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Sistem yönetimi</p>
          <h1>Ayarlar</h1>
          <p className="muted">
            Kurum görünümü, rapor limitleri ve bildirim tercihlerini buradan yönetin. Değişiklikler
            anında uygulanır; yeniden kurulum gerekmez.
          </p>
        </div>
        <div className="report-hero-side">
          <div className="report-hero-stats">
            <div>
              <strong>{items.length}</strong>
              <span>Ayar</span>
            </div>
            <div>
              <strong>{groups.length}</strong>
              <span>Grup</span>
            </div>
            <div>
              <strong>{dirtyKeys.length}</strong>
              <span>Değişiklik</span>
            </div>
          </div>
          <div className="report-hero-actions">
            <button
              type="button"
              className="btn-secondary"
              disabled={busy || loading}
              onClick={() => void load()}
            >
              Yenile
            </button>
            {dirtyKeys.length > 0 && (
              <>
                <button type="button" className="btn-secondary" disabled={busy} onClick={revertAll}>
                  Geri al
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  disabled={busy}
                  onClick={() => void saveAll()}
                >
                  {savingAll ? 'Kaydediliyor…' : `Tümünü kaydet (${dirtyKeys.length})`}
                </button>
              </>
            )}
          </div>
        </div>
      </header>

      <div className="org-toolbar panel">
        <div className="org-views" role="tablist" aria-label="Ayar grupları">
          <button
            type="button"
            role="tab"
            aria-selected={activeGroup === 'all'}
            className={activeGroup === 'all' ? 'is-active' : undefined}
            onClick={() => setActiveGroup('all')}
          >
            Tümü
            <em className="settings-tab-count">{items.length}</em>
          </button>
          {groups.map((g) => (
            <button
              key={g}
              type="button"
              role="tab"
              aria-selected={activeGroup === g}
              className={activeGroup === g ? 'is-active' : undefined}
              onClick={() => setActiveGroup(g)}
            >
              {g}
              <em className="settings-tab-count">{groupCounts.get(g) ?? 0}</em>
            </button>
          ))}
        </div>
      </div>

      {error && <div className="form-error">{error}</div>}
      {info && <div className="form-success">{info}</div>}

      <section className={`panel settings-panel${loading ? ' is-loading' : ''}`}>
        {loading && items.length === 0 ? (
          <p className="muted settings-empty">Ayarlar yükleniyor…</p>
        ) : visible.length === 0 ? (
          <p className="muted settings-empty">Bu grupta ayar bulunmuyor.</p>
        ) : (
          <div className="settings-list">
            {visible.map((s) => {
              const draft = drafts[s.key] ?? s.value
              const dirty = !s.isReadOnly && draft !== s.value
              const desc = settingDescription(s)
              const hint = settingHint(s)
              const saving = savingKey === s.key

              return (
                <article
                  key={s.key}
                  className={`settings-card${dirty ? ' is-dirty' : ''}${s.isReadOnly ? ' is-readonly' : ''}`}
                >
                  <div className="settings-card-main">
                    <div className="settings-card-head">
                      <strong>{s.displayName}</strong>
                      <span className="settings-pill">{typeLabel(s.valueType)}</span>
                      {s.isReadOnly && <span className="settings-pill is-muted">Salt okunur</span>}
                      {dirty && <span className="settings-pill is-dirty">Kaydedilmedi</span>}
                    </div>
                    {desc && <p className="settings-card-desc">{desc}</p>}
                    {activeGroup === 'all' && (
                      <span className="settings-card-group">{s.groupName}</span>
                    )}
                  </div>

                  <div className="settings-card-edit">
                    {s.valueType === 3 ? (
                      <button
                        type="button"
                        className={`settings-switch${draft === 'true' ? ' is-on' : ''}`}
                        role="switch"
                        aria-checked={draft === 'true'}
                        disabled={s.isReadOnly || busy}
                        onClick={() => setDraft(s.key, draft === 'true' ? 'false' : 'true')}
                      >
                        <span className="settings-switch-knob" />
                        <span className="settings-switch-label">
                          {draft === 'true' ? 'Açık' : 'Kapalı'}
                        </span>
                      </button>
                    ) : (
                      <label className="settings-field">
                        <span className="sr-only">{s.displayName}</span>
                        <input
                          type={s.valueType === 2 ? 'number' : 'text'}
                          value={draft}
                          placeholder={hint}
                          disabled={s.isReadOnly || busy}
                          min={s.valueType === 2 ? 1 : undefined}
                          max={s.valueType === 2 ? 100000 : undefined}
                          onChange={(e) => setDraft(s.key, e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter' && dirty && !busy) {
                              e.preventDefault()
                              void save(s.key)
                            }
                          }}
                        />
                        {hint && <small className="muted">{hint}</small>}
                      </label>
                    )}

                    {!s.isReadOnly && (
                      <div className="settings-card-actions">
                        {dirty && (
                          <button
                            type="button"
                            className="btn-secondary"
                            disabled={busy}
                            onClick={() => revert(s.key)}
                          >
                            Geri al
                          </button>
                        )}
                        <button
                          type="button"
                          className="btn-primary"
                          disabled={!dirty || busy}
                          onClick={() => void save(s.key)}
                        >
                          {saving ? '…' : 'Kaydet'}
                        </button>
                      </div>
                    )}
                  </div>
                </article>
              )
            })}
          </div>
        )}
      </section>
    </div>
  )
}
