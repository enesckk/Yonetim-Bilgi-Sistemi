import { formatPhone, telHref } from '@/lib/phone'

export function PhoneLink({ phone, name, label, className }: {
  phone?: string | null
  name: string
  label?: string
  className?: string
}) {
  const href = telHref(phone)
  if (!href) return label ? null : <>{phone?.trim() || '—'}</>
  return <a href={href} className={className ?? 'employee-phone-link'} aria-label={`${name} kişisini ara: ${formatPhone(phone)}`}>
    {label ?? formatPhone(phone)}
  </a>
}
