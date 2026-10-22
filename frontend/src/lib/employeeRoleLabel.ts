/** Combine formal title and actual duty without repeating the same title in a profile heading. */
export function employeeRoleLabel(jobTitle?: string | null, primaryDuty?: string | null): string {
  const clean = (value?: string | null) => (value ?? '').normalize('NFC').trim().replace(/\s+/gu, ' ')
  const title = clean(jobTitle)
  const duty = clean(primaryDuty)
  if (!title) return duty
  if (!duty) return title
  const normalizedTitle = title.toLocaleLowerCase('tr-TR')
  const normalizedDuty = duty.toLocaleLowerCase('tr-TR')
  if (normalizedTitle === normalizedDuty) return title
  // Keep the complete description, e.g. "Teknik Personel Şefi - Ses, Işık Sorumlusu".
  const extendsLabel = (long: string, short: string) => long.startsWith(short) && /^\s*[-–—:|·]/u.test(long.slice(short.length))
  if (extendsLabel(normalizedDuty, normalizedTitle)) return duty
  if (extendsLabel(normalizedTitle, normalizedDuty)) return title
  return `${title} · ${duty}`
}
