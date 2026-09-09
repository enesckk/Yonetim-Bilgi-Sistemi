import { useAuth } from '@/auth/AuthContext'
import { AccessDenied } from '@/auth/AccessDenied'
import { SessionSplash } from '@/components/SessionSplash'
import type { ReactNode } from 'react'

export function PermissionRoute({
  permission,
  message,
  children,
}: {
  permission: string
  message?: string
  children: ReactNode
}) {
  const { isLoading, hasPermission } = useAuth()

  if (isLoading) return <SessionSplash />
  if (!hasPermission(permission)) {
    return <AccessDenied message={message ?? 'Bu sayfa için yetkiniz yok.'} />
  }
  return children
}
