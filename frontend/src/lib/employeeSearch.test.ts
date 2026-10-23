import { describe, expect, it } from 'vitest'
import { matchesEmployeeSearch } from './employeeSearch'

describe('custody employee search', () => {
  const employee = { name: 'İbrahim Işık Öztürk', employeeNumber: 'HR-2026-006' }
  it.each(['ibrahim', 'İBRAHİM', 'isik', 'IŞIK', 'ozturk', 'ÖZTÜRK', 'öztürk ibrahim', ' HR-2026-006 ', 'HR2026006', '2026-006', 'ibrahim 006'])('finds the person with %s', query => {
    expect(matchesEmployeeSearch(employee, query)).toBe(true)
  })
  it('requires every search word to match', () => {
    expect(matchesEmployeeSearch(employee, 'ibrahim ahmet')).toBe(false)
  })
  it('handles empty searches and missing employee numbers', () => {
    expect(matchesEmployeeSearch(employee, '   ')).toBe(true)
    expect(matchesEmployeeSearch({ name: 'Ali Döner' }, 'döner')).toBe(true)
    expect(matchesEmployeeSearch({ name: 'Ali Döner' }, '006')).toBe(false)
  })
})
