import { useEffect, useState } from 'react'
import { apiFetchBlob } from '@/api/client'

type EmployeeAvatarProps = {
  employeeId: string
  name: string
  hasPhoto?: boolean
  className?: string
  size?: 'sm' | 'lg'
}

export function EmployeeAvatar({
  employeeId,
  name,
  hasPhoto = false,
  className = '',
  size = 'sm',
}: EmployeeAvatarProps) {
  const [objectUrl, setObjectUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  const initials = getInitials(name)
  const sizeClass = size === 'lg' ? 'emp-avatar-lg' : ''

  useEffect(() => {
    let cancelled = false
    let createdUrl: string | null = null

    setObjectUrl(null)
    setFailed(false)

    if (!hasPhoto || !employeeId) {
      return
    }

    void (async () => {
      try {
        const blob = await apiFetchBlob(`/api/employees/${employeeId}/photo`)
        if (cancelled) return
        createdUrl = URL.createObjectURL(blob)
        setObjectUrl(createdUrl)
      } catch {
        if (!cancelled) {
          setFailed(true)
        }
      }
    })()

    return () => {
      cancelled = true
      if (createdUrl) URL.revokeObjectURL(createdUrl)
    }
  }, [employeeId, hasPhoto])

  if (objectUrl && !failed) {
    return (
      <img
        className={['emp-avatar', 'emp-avatar-img', sizeClass, className].filter(Boolean).join(' ')}
        src={objectUrl}
        alt=""
        onError={() => setFailed(true)}
      />
    )
  }

  return (
    <span
      className={['emp-avatar', 'emp-avatar-fallback', sizeClass, className]
        .filter(Boolean)
        .join(' ')}
      aria-hidden="true"
    >
      {initials}
    </span>
  )
}

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '?'
  if (parts.length === 1) return parts[0].slice(0, 2).toLocaleUpperCase('tr-TR')
  return (parts[0][0] + parts[parts.length - 1][0]).toLocaleUpperCase('tr-TR')
}
