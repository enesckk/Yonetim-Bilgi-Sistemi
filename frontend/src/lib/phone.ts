export function digitsPhone(value?: string | null) {
  return (value ?? '').replace(/\D/g, '')
}

export function formatPhone(value?: string | null) {
  const d = digitsPhone(value)
  if (d.length === 11 && d.startsWith('0')) {
    return `${d.slice(0, 4)} ${d.slice(4, 7)} ${d.slice(7, 9)} ${d.slice(9)}`
  }
  if (d.length === 10) {
    return `0${d.slice(0, 3)} ${d.slice(3, 6)} ${d.slice(6, 8)} ${d.slice(8)}`
  }
  return value?.trim() || ''
}

export function telHref(value?: string | null) {
  const raw = value?.trim() ?? ''
  // Do not turn masked numbers, extensions or arbitrary text into a different number.
  if (!/^(?:\+|00)?[\d\s().-]+$/.test(raw)) return null
  const d = digitsPhone(raw)
  if (d.length < 7 || d.length > 15) return null
  if (raw.startsWith('+')) return d.startsWith('0') ? null : `tel:+${d}`
  if (raw.startsWith('00')) return d.length > 8 ? `tel:+${d.slice(2)}` : null
  if (d.length === 11 && d.startsWith('0')) return `tel:+90${d.slice(1)}`
  if (d.length === 10) return `tel:+90${d}`
  if (d.length === 12 && d.startsWith('90')) return `tel:+${d}`
  return `tel:${d}`
}
