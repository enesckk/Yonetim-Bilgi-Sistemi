import { Navigate } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { isSystemAdmin, defaultHomePath } from '@/auth/roles'
import { HomePage } from '@/pages/HomePage'

export function HomeEntry() {
  const { user } = useAuth()
  if (isSystemAdmin(user)) return <HomePage />
  return <Navigate to={defaultHomePath(user)} replace />
}
