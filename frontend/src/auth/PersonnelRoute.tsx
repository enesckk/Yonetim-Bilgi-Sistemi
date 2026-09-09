import { useAuth } from '@/auth/AuthContext'
import { canUsePersonnelModule } from '@/auth/roles'
import { AccessDenied } from '@/auth/AccessDenied'
import { SessionSplash } from '@/components/SessionSplash'
import type { ReactNode } from 'react'

/** Müdürlük: admin, müdür ve idari amir. Sistem ayarları bu rotada değil. */
export function PersonnelRoute({ children }: { children: ReactNode }) {
  const { user, isLoading } = useAuth()

  if (isLoading) return <SessionSplash />

  if (!canUsePersonnelModule(user)) {
    return <AccessDenied message="Müdürlük ekranları için yetkiniz yok." />
  }

  return children
}
