import { canUseEventsModule, defaultHomePath } from '@/auth/roles'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { SessionSplash } from '@/components/SessionSplash'
import type { ReactNode } from 'react'

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { user, isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <SessionSplash />

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if ((location.pathname === '/events' || location.pathname.startsWith('/events/')) && !canUseEventsModule(user)) {
    return <Navigate to={defaultHomePath(user)} replace />
  }

  return children
}
