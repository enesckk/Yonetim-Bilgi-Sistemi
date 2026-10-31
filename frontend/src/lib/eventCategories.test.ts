import { describe, expect, it } from 'vitest'
import { resolveCategory } from './eventCategories'
import { groupEvents } from './eventGroups'
describe('editable event and project categories', () => {
  it('keeps projects classified as projects for every custom category', () => {
    for (const name of ['TÜBİTAK', 'GİZ', 'AB Projeleri', 'Özel Projemiz']) expect(resolveCategory(name, true)).toEqual({ category: 'project', categoryName: name })
  })
  it('recognizes Turkish presets but preserves arbitrary event category text', () => {
    expect(resolveCategory('  kültür / sanat ', false)).toEqual({ category: 'culture', categoryName: '' })
    expect(resolveCategory('Mahalle Buluşması', false)).toEqual({ category: 'other', categoryName: 'Mahalle Buluşması' })
    expect(resolveCategory('Proje', false).category).toBe('other')
    expect(resolveCategory('', false)).toEqual({ category: '', categoryName: '' })
  })
  it('separates custom categories when grouping identical programme names', () => {
    const base = { id: '1', title: 'Bilim', category: 'project', status: 4 as const, statusLabel: 'Yapıldı', startAtUtc: '2026-05-01T00:00:00Z' }
    const result = groupEvents([{ ...base, categoryName: 'TÜBİTAK', categoryLabel: 'TÜBİTAK' }, { ...base, id: '2', categoryName: 'AB Projeleri', categoryLabel: 'AB Projeleri' }])
    expect(result).toHaveLength(2)
    expect(result.map(g => g.categoryLabel)).toEqual(['TÜBİTAK', 'AB Projeleri'])
  })
})
