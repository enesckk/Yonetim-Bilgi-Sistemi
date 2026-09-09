import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { SessionSplash } from '@/components/SessionSplash'
import type { ReactNode } from 'react'

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <SessionSplash />

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}
