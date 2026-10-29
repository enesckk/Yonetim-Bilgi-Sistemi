import { describe, expect, it } from 'vitest'
import { eventDateRange, formatEventWhen } from './eventsDates'

describe('event date filtering', () => {
  it('uses local midnight through the last millisecond of the selected day', () => {
    const range = eventDateRange('2026-10-29', '2026-10-29')
    const first = new Date(range.fromUtc!)
    const last = new Date(range.toUtc!)
    expect([first.getFullYear(), first.getMonth(), first.getDate(), first.getHours()]).toEqual([2026, 9, 29, 0])
    expect([last.getDate(), last.getHours(), last.getMinutes(), last.getSeconds(), last.getMilliseconds()]).toEqual([29, 23, 59, 59, 999])
  })
  it('allows open-ended ranges', () => {
    expect(eventDateRange('', '')).toEqual({ fromUtc: undefined, toUtc: undefined })
    expect(eventDateRange('2026-10-29', '').toUtc).toBeUndefined()
    expect(eventDateRange('', '2026-10-29').fromUtc).toBeUndefined()
  })
  it('rejects reversed and invalid calendar dates', () => {
    expect(() => eventDateRange('2026-10-30', '2026-10-29')).toThrow('Bitiş tarihi')
    expect(() => eventDateRange('2026-02-30', '')).toThrow('Geçerli bir tarih')
    expect(() => eventDateRange('', 'not-a-date')).toThrow('Geçerli bir tarih')
  })
})

it('shows the supplied Istanbul date without inventing a time', () => {
  expect(formatEventWhen('2026-04-16T21:00:00Z', true)).toBe('17.04.2026')
  expect(formatEventWhen('2026-04-17T07:00:00Z', false)).toContain('10:00')
})
