import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import {
  changeMyPassword,
  fetchMyAccount,
  updateMyAccount,
  type MyAccount,
} from '@/api/accountApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'

type Tab = 'profile' | 'password'

function firstError(err: unknown, fallback: string): string {
  if (err instanceof ApiClientError) {
    const fields = err.validationErrors
    if (fields) {
      const first = Object.values(fields)[0]?.[0]
      if (first) return first
    }
    return err.message
  }
  return fallback
}

export function AccountMenu({ avatar }: { avatar: ReactNode }) {
  const { user, logout, refreshUser } = useAuth()
  const [open, setOpen] = useState(false)
  const [modalTab, setModalTab] = useState<Tab | null>(null)
  const [popPos, setPopPos] = useState<{ top: number; right: number } | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const popRef = useRef<HTMLDivElement>(null)

  const placePop = useCallback(() => {
    const el = triggerRef.current
    if (!el) return
    const r = el.getBoundingClientRect()
    setPopPos({
      top: Math.round(r.bottom + 8),
      right: Math.round(window.innerWidth - r.right),
    })
  }, [])

  useEffect(() => {
    if (!open) {
      setPopPos(null)
      return
    }

    placePop()

    // Açan tıklamanın mousedown'ı menüyü hemen kapatmasın.
    let removeOutside: (() => void) | undefined
    const timer = window.setTimeout(() => {
      function onPointer(e: MouseEvent) {
        const t = e.target as Node
        if (rootRef.current?.contains(t) || popRef.current?.contains(t)) return
        setOpen(false)
      }
      function onKey(e: KeyboardEvent) {
        if (e.key === 'Escape') setOpen(false)
      }
      function onReposition() {
        placePop()
      }

      document.addEventListener('mousedown', onPointer)
      document.addEventListener('keydown', onKey)
      window.addEventListener('resize', onReposition)
      window.addEventListener('scroll', onReposition, true)
      removeOutside = () => {
        document.removeEventListener('mousedown', onPointer)
        document.removeEventListener('keydown', onKey)
        window.removeEventListener('resize', onReposition)
        window.removeEventListener('scroll', onReposition, true)
      }
    }, 0)

    return () => {
      window.clearTimeout(timer)
      removeOutside?.()
    }
  }, [open, placePop])

  const openModal = useCallback((tab: Tab) => {
    setOpen(false)
    setModalTab(tab)
  }, [])

  const roleLine = (user?.roleNames?.length ? user.roleNames : user?.roles)?.join(', ')
  const displayName = user?.displayName?.trim() || user?.userName || ''
  const showRole = Boolean(roleLine && roleLine !== displayName)

  return (
    <div className={`account-menu${open ? ' is-open' : ''}`} ref={rootRef}>
      <button
        ref={triggerRef}
        type="button"
        className="topbar-user account-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label="Hesap menüsü"
        onClick={(e) => {
          e.stopPropagation()
          if (open) {
            setOpen(false)
            return
          }
          const el = triggerRef.current
          if (el) {
            const r = el.getBoundingClientRect()
            setPopPos({
              top: Math.round(r.bottom + 8),
              right: Math.round(window.innerWidth - r.right),
            })
          }
          setOpen(true)
        }}
      >
        {avatar}
        <span className="user-meta">
          {showRole ? <span className="muted">{roleLine}</span> : null}
          <strong>{displayName}</strong>
        </span>
        <svg
          className="account-caret"
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          aria-hidden="true"
        >
          <path
            d="m6 9 6 6 6-6"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      </button>

      {open && popPos
        ? createPortal(
            <div
              ref={popRef}
              className="account-pop"
              role="menu"
              aria-label="Hesap menüsü"
              style={{ top: popPos.top, right: popPos.right }}
            >
              <div className="account-pop-head">
                <strong>{user?.displayName}</strong>
                <span className="muted">{user?.email}</span>
                {roleLine ? <span className="account-pop-role">{roleLine}</span> : null}
              </div>

              <div className="account-pop-list">
                <button type="button" role="menuitem" onClick={() => openModal('profile')}>
                  <AccountIcon d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z" />
                  Hesap bilgilerim
                </button>
                <button type="button" role="menuitem" onClick={() => openModal('password')}>
                  <AccountIcon d="M7 11V7a5 5 0 0 1 10 0v4M5 11h14v10H5V11Z" />
                  Şifre değiştir
                </button>
              </div>

              <div className="account-pop-foot">
                <button type="button" role="menuitem" onClick={() => void logout()}>
                  <AccountIcon d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
                  Çıkış yap
                </button>
              </div>
            </div>,
            document.body,
          )
        : null}

      {modalTab ? (
        <AccountModal
          initialTab={modalTab}
          onClose={() => setModalTab(null)}
          onProfileSaved={() => void refreshUser()}
          onPasswordChanged={() => void logout()}
        />
      ) : null}
    </div>
  )
}

function AccountIcon({ d }: { d: string }) {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d={d}
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

function AccountModal({
  initialTab,
  onClose,
  onProfileSaved,
  onPasswordChanged,
}: {
  initialTab: Tab
  onClose: () => void
  onProfileSaved: () => void
  onPasswordChanged: () => void
}) {
  const [tab, setTab] = useState<Tab>(initialTab)
  const [account, setAccount] = useState<MyAccount | null>(null)
  const [loading, setLoading] = useState(true)

  const [userName, setUserName] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')

  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const data = await fetchMyAccount()
        if (cancelled) return
        setAccount(data)
        setUserName(data.userName)
        setDisplayName(data.displayName)
        setEmail(data.email)
      } catch (err) {
        if (!cancelled) setError(firstError(err, 'Hesap bilgileri yüklenemedi.'))
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  useEffect(() => {
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.body.style.overflow = previous
      document.removeEventListener('keydown', onKey)
    }
  }, [onClose])

  const profileDirty =
    !!account &&
    (userName.trim() !== account.userName ||
      displayName.trim() !== account.displayName ||
      email.trim() !== account.email)

  async function onSubmitProfile(e: FormEvent) {
    e.preventDefault()
    if (!profileDirty) return
    setSaving(true)
    setError(null)
    setInfo(null)
    try {
      await updateMyAccount({
        userName: userName.trim(),
        displayName: displayName.trim(),
        email: email.trim(),
      })
      setAccount((prev) =>
        prev
          ? { ...prev, userName: userName.trim(), displayName: displayName.trim(), email: email.trim() }
          : prev,
      )
      setInfo('Hesap bilgileriniz güncellendi.')
      onProfileSaved()
    } catch (err) {
      setError(firstError(err, 'Güncellenemedi.'))
    } finally {
      setSaving(false)
    }
  }

  async function onSubmitPassword(e: FormEvent) {
    e.preventDefault()
    if (newPassword !== confirmPassword) {
      setError('Yeni şifre tekrarı eşleşmiyor.')
      return
    }
    setSaving(true)
    setError(null)
    setInfo(null)
    try {
      await changeMyPassword({ currentPassword, newPassword })
      setInfo('Şifreniz değişti. Güvenlik için yeniden giriş yapmanız gerekiyor…')
      window.setTimeout(onPasswordChanged, 1200)
    } catch (err) {
      setError(firstError(err, 'Şifre değiştirilemedi.'))
      setSaving(false)
    }
  }

  // Topbar'da backdrop-filter var; sabit konumlu modal orada sıkışmasın diye portal.
  return createPortal(
    <div
      className="org-modal-backdrop"
      role="presentation"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div
        className="org-modal panel account-modal"
        role="dialog"
        aria-modal="true"
        aria-label="Hesabım"
      >
        <div className="org-modal-head">
          <p className="org-modal-eyebrow">Hesabım</p>
          <h2>{tab === 'profile' ? 'Hesap bilgileri' : 'Şifre değiştir'}</h2>
        </div>

        <div className="org-views account-tabs" role="tablist" aria-label="Hesap bölümleri">
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'profile'}
            className={tab === 'profile' ? 'is-active' : undefined}
            onClick={() => {
              setTab('profile')
              setError(null)
              setInfo(null)
            }}
          >
            Bilgilerim
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={tab === 'password'}
            className={tab === 'password' ? 'is-active' : undefined}
            onClick={() => {
              setTab('password')
              setError(null)
              setInfo(null)
            }}
          >
            Şifre
          </button>
        </div>

        {error ? <div className="form-error">{error}</div> : null}
        {info ? <div className="form-success">{info}</div> : null}

        {loading ? (
          <p className="muted">Yükleniyor…</p>
        ) : tab === 'profile' ? (
          <form className="form-grid" onSubmit={(e) => void onSubmitProfile(e)}>
            <label>
              <span>Kullanıcı adı</span>
              <input
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                autoComplete="username"
                required
                maxLength={100}
              />
              <small className="muted">Girişte kullandığınız ad. Harf, rakam, nokta, alt çizgi.</small>
            </label>

            <label>
              <span>Ad soyad</span>
              <input
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                required
                maxLength={150}
              />
            </label>

            <label className="span-2">
              <span>E-posta</span>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="email"
                required
                maxLength={200}
              />
            </label>

            {account?.employeeName ? (
              <p className="muted small span-2">
                Bağlı personel kaydı: <strong>{account.employeeName}</strong>
              </p>
            ) : null}

            <div className="form-actions span-2">
              <button type="submit" className="btn-primary" disabled={!profileDirty || saving}>
                {saving ? 'Kaydediliyor…' : 'Değişiklikleri kaydet'}
              </button>
              <button type="button" className="btn-secondary" onClick={onClose}>
                Kapat
              </button>
            </div>
          </form>
        ) : (
          <form className="form-grid" onSubmit={(e) => void onSubmitPassword(e)}>
            <label className="span-2">
              <span>Mevcut şifre</span>
              <input
                type="password"
                value={currentPassword}
                onChange={(e) => setCurrentPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </label>

            <label>
              <span>Yeni şifre</span>
              <input
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                autoComplete="new-password"
                required
              />
            </label>

            <label>
              <span>Yeni şifre (tekrar)</span>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                autoComplete="new-password"
                required
              />
            </label>

            <p className="muted small span-2">
              En az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter içermeli. Şifre
              değişince açık tüm oturumlar kapanır.
            </p>

            <div className="form-actions span-2">
              <button
                type="submit"
                className="btn-primary"
                disabled={saving || !currentPassword || !newPassword || !confirmPassword}
              >
                {saving ? 'Kaydediliyor…' : 'Şifreyi değiştir'}
              </button>
              <button type="button" className="btn-secondary" onClick={onClose}>
                Kapat
              </button>
            </div>
          </form>
        )}
      </div>
    </div>,
    document.body,
  )
}
