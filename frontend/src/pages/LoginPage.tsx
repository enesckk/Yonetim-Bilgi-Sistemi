import { useEffect, useId, useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import {
  ApiClientError,
  AUTH_NOTICE_KEY,
  AUTH_NOTICE_SESSION_EXPIRED,
  SESSION_EXPIRED_MESSAGE,
} from '@/api/client'

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

export function LoginPage() {
  const { login, isAuthenticated, isLoading } = useAuth()
  const navigate = useNavigate()
  const userId = useId()
  const passwordId = useId()

  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    try {
      const flag = sessionStorage.getItem(AUTH_NOTICE_KEY)
      if (flag === AUTH_NOTICE_SESSION_EXPIRED) {
        sessionStorage.removeItem(AUTH_NOTICE_KEY)
        setNotice(SESSION_EXPIRED_MESSAGE)
      }
    } catch {
      /* ignore */
    }
  }, [])

  if (!isLoading && isAuthenticated) {
    return <Navigate to="/" replace />
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setNotice(null)
    setSubmitting(true)
    try {
      await login(userName.trim(), password)
      navigate('/', { replace: true })
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
      } else {
        setError('Giriş yapılamadı. API çalışıyor mu kontrol edin.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-shell">
        <aside className="login-brand">
          <div className="login-brand-inner">
            <p className="login-brand-city">Şehitkamil Belediyesi</p>
            <h1 className="login-brand-title">Personel Bilgi ve Yönetim Sistemi</h1>
            <p className="login-brand-dept">Kültür, Sanat ve Sosyal İşler Müdürlüğü</p>
            <p className="login-brand-lead">
              Personel kayıtları, birim yapısı ve kurumsal raporlara güvenli erişim.
            </p>
          </div>
        </aside>

        <main className="login-panel">
          <header className="login-header">
            <p className="brand-kicker">Kurumsal giriş</p>
            <h2>Hesabınıza giriş yapın</h2>
            <p className="muted">Yetkili kullanıcı adı ve şifrenizle devam edin.</p>
          </header>

          <form className="login-form" onSubmit={onSubmit} noValidate>
            <div className="login-field">
              <label htmlFor={userId} className="visually-hidden">
                Kullanıcı adı
              </label>
              <input
                id={userId}
                name="username"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                placeholder="Kullanıcı adı"
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                required
                disabled={submitting}
              />
            </div>

            <div className="login-field">
              <label htmlFor={passwordId} className="visually-hidden">
                Şifre
              </label>
              <div className="login-password-field">
                <input
                  id={passwordId}
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  placeholder="Şifre"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  disabled={submitting}
                />
                <button
                  type="button"
                  className="login-password-toggle"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-pressed={showPassword}
                  aria-label={showPassword ? 'Şifreyi gizle' : 'Şifreyi göster'}
                  disabled={submitting}
                >
                  <EyeIcon open={showPassword} />
                </button>
              </div>
            </div>

            {notice && !error && (
              <div className="form-error login-session-notice" role="status">
                {notice}
              </div>
            )}

            {error && (
              <div className="form-error" role="alert">
                {error}
              </div>
            )}

            <button type="submit" className="login-submit" disabled={submitting}>
              {submitting ? 'Giriş yapılıyor…' : 'Giriş yap'}
            </button>
          </form>
        </main>
      </div>
    </div>
  )
}
