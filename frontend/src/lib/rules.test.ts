import { describe, expect, it } from 'vitest'
import { formatRate, levelFromCoverage } from '@/lib/coverage'
import { isLibraryVenue } from '@/lib/eventVenues'
import { formatPhone } from '@/lib/phone'

describe('levelFromCoverage', () => {
  it('returns none when there is no activity', () => {
    expect(levelFromCoverage(0, 80)).toBe('none')
    expect(levelFromCoverage(null, 80)).toBe('none')
  })

  it('uses rate bands when activity exists', () => {
    expect(levelFromCoverage(1, null)).toBe('low')
    expect(levelFromCoverage(1, 19)).toBe('low')
    expect(levelFromCoverage(1, 20)).toBe('medium')
    expect(levelFromCoverage(1, 49.9)).toBe('medium')
    expect(levelFromCoverage(1, 50)).toBe('good')
  })
})

describe('formatRate', () => {
  it('formats percent in tr-TR', () => {
    expect(formatRate(null)).toBe('—')
    expect(formatRate(12.5)).toBe('%12,5')
  })
})

describe('isLibraryVenue', () => {
  it('excludes libraries from event venues', () => {
    expect(isLibraryVenue({ name: 'Mehmet Akif Ersoy Gençlik Kütüphanesi' } as never)).toBe(true)
    expect(isLibraryVenue({ name: 'Mehmet Akif Salonu' } as never)).toBe(false)
  })
})

describe('formatPhone', () => {
  it('formats 11-digit numbers', () => {
    expect(formatPhone('05321234567')).toBe('0532 123 45 67')
  })
})

describe('director page permissions', () => {
  it('hides excel import and keeps admin catalog pages off the director account', async () => {
    const { canImportEvents, canImportEmployees } = await import('@/auth/roles')
    const director = { roles: ['DIRECTOR'] } as never
    const admin = { roles: ['SYSTEM_ADMIN'] } as never
    expect(canImportEvents(director)).toBe(false)
    expect(canImportEmployees(director)).toBe(false)
    expect(canImportEvents(admin)).toBe(true)
    expect(canImportEmployees(admin)).toBe(true)
  })
})
