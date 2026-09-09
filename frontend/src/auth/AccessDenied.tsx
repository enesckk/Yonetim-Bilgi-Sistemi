import { Link } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { defaultHomePath } from '@/auth/roles'

export function AccessDenied({
  message = 'Bu sayfa için yetkiniz yok.',
}: {
  message?: string
}) {
  const { user } = useAuth()
  const home = defaultHomePath(user)

  return (
    <div className="panel access-denied">
      <p className="org-eyebrow">Erişim</p>
      <h1>Sayfa açılamadı</h1>
      <p className="muted">{message}</p>
      <Link to={home} className="btn-primary">
        Ana ekrana dön
      </Link>
    </div>
  )
}
