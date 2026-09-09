export type CoverageLevel = 'none' | 'low' | 'medium' | 'good' | 'high'

export const COVERAGE_COLORS: Record<CoverageLevel, string> = {
  none: '#E8F3EC',
  low: '#E14B4B',
  medium: '#F0C94D',
  good: '#1FA97A',
  high: '#1FA97A',
}

export const COVERAGE_LEGEND: {
  level: 'none' | 'low' | 'medium' | 'good'
  label: string
  short: string
}[] = [
  { level: 'none', label: 'Etkinlik yok', short: 'Yok' },
  { level: 'low', label: '%20 altı', short: '%20−' },
  { level: 'medium', label: '%20–50', short: '%20–50' },
  { level: 'good', label: '%50 üzeri', short: '%50+' },
]

export function levelFromCoverage(
  activityCount: number | null | undefined,
  ratePercent: number | null | undefined,
): 'none' | 'low' | 'medium' | 'good' {
  const n = activityCount ?? 0
  if (n <= 0) return 'none'
  if (ratePercent == null || ratePercent < 20) return 'low'
  if (ratePercent < 50) return 'medium'
  return 'good'
}

export function coverageFill(level: CoverageLevel | string | undefined): string {
  if (level === 'low') return COVERAGE_COLORS.low
  if (level === 'medium') return COVERAGE_COLORS.medium
  if (level === 'good' || level === 'high') return COVERAGE_COLORS.good
  return COVERAGE_COLORS.none
}

export function formatRate(rate: number | null | undefined): string {
  if (rate == null || Number.isNaN(rate)) return '—'
  const capped = Math.min(100, Math.max(0, rate))
  return `%${capped.toLocaleString('tr-TR', { maximumFractionDigits: 1 })}`
}

export type DatePreset = 'month' | 'quarter' | 'year' | 'lastYear' | 'all' | 'custom'

export function dateRangeForPreset(preset: DatePreset): { fromUtc?: string; toUtc?: string } {
  const now = new Date()
  const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate())
  if (preset === 'all') return {}
  if (preset === 'month') {
    const from = startOfDay(new Date(now.getFullYear(), now.getMonth(), 1))
    return { fromUtc: from.toISOString() }
  }
  if (preset === 'quarter') {
    const from = startOfDay(new Date(now.getFullYear(), now.getMonth() - 2, 1))
    return { fromUtc: from.toISOString() }
  }
  if (preset === 'year') {
    const from = startOfDay(new Date(now.getFullYear(), 0, 1))
    return { fromUtc: from.toISOString() }
  }
  if (preset === 'lastYear') {
    const from = startOfDay(new Date(now.getFullYear() - 1, 0, 1))
    const to = new Date(now.getFullYear(), 0, 1)
    return { fromUtc: from.toISOString(), toUtc: to.toISOString() }
  }
  return {}
}
