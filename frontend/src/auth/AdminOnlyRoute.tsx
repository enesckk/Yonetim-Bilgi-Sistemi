import { useAuth } from '@/auth/AuthContext'
import { isSystemAdmin } from '@/auth/roles'
import { AccessDenied } from '@/auth/AccessDenied'
import { SessionSplash } from '@/components/SessionSplash'
import type { ReactNode } from 'react'

export function AdminOnlyRoute({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth()

  if (isLoading) return <SessionSplash />

  if (!isSystemAdmin(user)) {
    return <AccessDenied message="Bu sayfa yalnızca sistem yöneticisine açıktır." />
  }

  return children
}
