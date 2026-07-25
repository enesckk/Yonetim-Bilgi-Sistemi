import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import type { ReactNode } from 'react'

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="center-screen">
        <p className="muted">Oturum kontrol ediliyor…</p>
      </div>
    )
  }

  if (!isAuthenticated) {
    // Giriş sonrası geri dönmek için geldiği sayfayı sakla
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}
