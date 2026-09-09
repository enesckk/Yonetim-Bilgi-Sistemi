import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import * as authApi from '@/api/authApi'
import type { CurrentUser, SessionPolicy } from '@/api/types'
import { ApiClientError, markSessionExpiredNotice, setSessionExpiredListener } from '@/api/client'
import { clearLookupCache } from '@/lib/lookupCache'

const DEFAULT_IDLE_MINUTES = 30

interface AuthContextValue {
  user: CurrentUser | null
  isLoading: boolean
  isAuthenticated: boolean
  login: (userName: string, password: string) => Promise<void>
  logout: () => Promise<void>
  refreshUser: () => Promise<void>
  hasPermission: (code: string) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [sessionPolicy, setSessionPolicy] = useState<SessionPolicy | null>(null)
  const idleTimerRef = useRef<number | null>(null)
  const lastActivityRef = useRef(Date.now())
  const logoutRef = useRef<() => Promise<void>>(async () => undefined)

  const clearIdleTimer = useCallback(() => {
    if (idleTimerRef.current !== null) {
      window.clearInterval(idleTimerRef.current)
      idleTimerRef.current = null
    }
  }, [])

  const clearSessionLocally = useCallback(() => {
    clearIdleTimer()
    setUser(null)
    setSessionPolicy(null)
  }, [clearIdleTimer])

  const armIdleWatcher = useCallback(() => {
    clearIdleTimer()
    if (!user) return
    const minutes = sessionPolicy?.idleTimeoutMinutes ?? DEFAULT_IDLE_MINUTES
    const limitMs = Math.max(1, minutes) * 60_000
    lastActivityRef.current = Date.now()
    idleTimerRef.current = window.setInterval(() => {
      if (Date.now() - lastActivityRef.current >= limitMs) {
        markSessionExpiredNotice()
        void logoutRef.current()
      }
    }, 15_000)
  }, [user, sessionPolicy, clearIdleTimer])

  // API 401 + refresh başarısız → oturumu düşür (ProtectedRoute login'e alır)
  useEffect(() => {
    setSessionExpiredListener(() => {
      clearSessionLocally()
    })
    return () => setSessionExpiredListener(null)
  }, [clearSessionLocally])

  // Sayfa açılışında: access token yok ama refresh cookie olabilir
  useEffect(() => {
    let cancelled = false

    ;(async () => {
      try {
        const refreshed = await authApi.refreshSession()
        if (cancelled) return
        if (refreshed) {
          setUser(refreshed.user)
          setSessionPolicy(refreshed.sessionPolicy)
        } else {
          setUser(null)
          setSessionPolicy(null)
        }
      } catch {
        if (!cancelled) {
          setUser(null)
          setSessionPolicy(null)
        }
      } finally {
        if (!cancelled) setIsLoading(false)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async (userName: string, password: string) => {
    const result = await authApi.login(userName, password)
    clearLookupCache()
    setUser(result.user)
    setSessionPolicy(result.sessionPolicy)
  }, [])

  const logout = useCallback(async () => {
    clearIdleTimer()
    try {
      await authApi.logout()
    } catch (err) {
      if (!(err instanceof ApiClientError)) {
        console.error(err)
      }
    } finally {
      clearLookupCache()
      setUser(null)
      setSessionPolicy(null)
    }
  }, [clearIdleTimer])

  logoutRef.current = logout

  const refreshUser = useCallback(async () => {
    try {
      setUser(await authApi.fetchMe())
    } catch {
      // profil tazeleme kritik değil; bir sonraki yenilemede düzelir
    }
  }, [])

  useEffect(() => {
    if (!user) {
      clearIdleTimer()
      return
    }

    const onActivity = () => {
      lastActivityRef.current = Date.now()
    }
    const events: (keyof WindowEventMap)[] = [
      'mousemove',
      'mousedown',
      'keydown',
      'scroll',
      'touchstart',
      'click',
    ]

    armIdleWatcher()
    for (const ev of events) window.addEventListener(ev, onActivity, { passive: true })
    return () => {
      clearIdleTimer()
      for (const ev of events) window.removeEventListener(ev, onActivity)
    }
  }, [user, armIdleWatcher, clearIdleTimer])

  const hasPermission = useCallback(
    (code: string) => user?.permissions.includes(code) ?? false,
    [user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      isAuthenticated: !!user,
      login,
      logout,
      refreshUser,
      hasPermission,
    }),
    [user, isLoading, login, logout, refreshUser, hasPermission],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth yalnızca AuthProvider içinde kullanılabilir.')
  return ctx
}
