export type SpecialDayKind = 'holiday' | 'observance'

export type SpecialDay = {
  key: string
  name: string
  kind: SpecialDayKind
}

function pad(n: number) {
  return String(n).padStart(2, '0')
}

export function istanbulDayKey(value: Date | string) {
  const d = typeof value === 'string' ? new Date(value) : value
  return d.toLocaleDateString('en-CA', { timeZone: 'Europe/Istanbul' })
}

function shiftDayKey(day: string, delta: number) {
  const [y, m, d] = day.split('-').map(Number)
  const next = new Date(Date.UTC(y, m - 1, d + delta))
  return `${next.getUTCFullYear()}-${pad(next.getUTCMonth() + 1)}-${pad(next.getUTCDate())}`
}

/** Başlangıç–bitiş aralığındaki İstanbul günlerini üretir (çok günlük etkinlik). */
export function istanbulKeysBetween(startAt: string, endAt?: string | null) {
  const start = istanbulDayKey(startAt)
  const end = istanbulDayKey(endAt || startAt)
  const keys: string[] = []
  let cur = start
  for (let i = 0; i < 62; i++) {
    keys.push(cur)
    if (cur >= end) break
    cur = shiftDayKey(cur, 1)
  }
  return keys
}

function key(year: number, month: number, day: number) {
  return `${year}-${pad(month)}-${pad(day)}`
}

function addRange(
  map: Map<string, SpecialDay>,
  year: number,
  month: number,
  from: number,
  to: number,
  name: string,
  kind: SpecialDayKind,
) {
  for (let d = from; d <= to; d++) {
    map.set(key(year, month, d), { key: key(year, month, d), name, kind })
  }
}

/** Türkiye resmi tatilleri ve kurumsal özel günler (İstanbul tarihi). */
export function specialDaysForYear(year: number): Map<string, SpecialDay> {
  const map = new Map<string, SpecialDay>()

  const national: Array<[number, number, string]> = [
    [1, 1, 'Yılbaşı'],
    [4, 23, 'Ulusal Egemenlik ve Çocuk Bayramı'],
    [5, 1, 'Emek ve Dayanışma Günü'],
    [5, 19, 'Atatürk’ü Anma, Gençlik ve Spor Bayramı'],
    [7, 15, 'Demokrasi ve Millî Birlik Günü'],
    [8, 30, 'Zafer Bayramı'],
    [10, 29, 'Cumhuriyet Bayramı'],
  ]
  for (const [m, d, name] of national) {
    map.set(key(year, m, d), { key: key(year, m, d), name, kind: 'holiday' })
  }

  // Diyanet/resmi ilanlara yakın bayram aralıkları
  if (year === 2025) {
    addRange(map, 2025, 3, 30, 31, 'Ramazan Bayramı', 'holiday')
    addRange(map, 2025, 4, 1, 1, 'Ramazan Bayramı', 'holiday')
    addRange(map, 2025, 6, 6, 9, 'Kurban Bayramı', 'holiday')
  } else if (year === 2026) {
    addRange(map, 2026, 3, 20, 22, 'Ramazan Bayramı', 'holiday')
    addRange(map, 2026, 5, 27, 30, 'Kurban Bayramı', 'holiday')
  } else if (year === 2027) {
    addRange(map, 2027, 3, 10, 12, 'Ramazan Bayramı', 'holiday')
    addRange(map, 2027, 5, 16, 19, 'Kurban Bayramı', 'holiday')
  }

  const observances: Array<[number, number, string]> = [
    [3, 8, 'Dünya Kadınlar Günü'],
    [3, 18, 'Çanakkale Zaferi'],
    [3, 21, 'Nevruz'],
    [4, 23, 'Ulusal Egemenlik ve Çocuk Bayramı'],
    [11, 10, 'Atatürk’ü Anma Günü'],
    [11, 24, 'Öğretmenler Günü'],
    [12, 3, 'Dünya Engelliler Günü'],
    [12, 5, 'Kadın Hakları Günü'],
    [12, 10, 'İnsan Hakları Günü'],
  ]
  for (const [m, d, name] of observances) {
    const k = key(year, m, d)
    if (!map.has(k)) map.set(k, { key: k, name, kind: 'observance' })
  }

  return map
}

export function specialDayOn(dayKey: string): SpecialDay | undefined {
  const year = Number(dayKey.slice(0, 4))
  if (!Number.isFinite(year)) return undefined
  return specialDaysForYear(year).get(dayKey)
}
