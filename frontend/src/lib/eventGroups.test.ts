import { describe, expect, it } from 'vitest'
import type { EventListItem } from '@/api/eventsApi'
import { groupEvents } from './eventGroups'
const row = (id: string, changes: Partial<EventListItem> = {}): EventListItem => ({ id, title:'Çocuk Tiyatrosu', status:4, statusLabel:'Yapıldı', startAtUtc:'2026-04-17T00:00:00Z', category:'culture', actualAttendees:200, ...changes })
describe('programme groups', () => {
  it('groups before pagination and preserves every source record and attendance', () => {
    const rows = Array.from({ length:34 }, (_, i) => row(String(i), { actualAttendees:i < 6 ? 400 : 200 }))
    const groups = groupEvents(rows)
    expect(groups).toHaveLength(1)
    expect(groups[0].items.map(e=>e.id)).toEqual(rows.map(e=>e.id))
    expect(groups[0].attendance).toBe(8000)
    expect(groups[0].recordsWithAttendance).toBe(34)
  })
  it('separates years using Istanbul date boundaries', () => {
    const groups = groupEvents([row('a',{startAtUtc:'2025-12-31T20:59:59Z'}),row('b',{startAtUtc:'2025-12-31T21:00:00Z'})])
    expect(groups.map(g=>g.year)).toEqual(['2025','2026'])
  })
  it('normalizes Turkish case and spaces without merging different categories', () => {
    expect(groupEvents([row('a'),row('b',{title:'  çocuk   tiyatrosu  '}),row('c',{category:'project'})]).map(g=>g.items.length)).toEqual([2,1])
  })
  it('does not duplicate a matching year already in the title', () => {
    expect(groupEvents([row('a'),row('b',{title:'Çocuk Tiyatrosu 2026'})])[0].items).toHaveLength(2)
  })
  it('excludes expected counts and preserves explicit zero and settlement totals', () => {
    const group=groupEvents([row('a',{actualAttendees:null,attendanceCount:999,expectedAttendees:999}),row('b',{actualAttendees:0,expectedAttendees:500}),row('c',{actualAttendees:null,recordedAttendanceCount:300})])[0]
    expect(group.attendance).toBe(300)
    expect(group.recordsWithAttendance).toBe(2)
  })
})
