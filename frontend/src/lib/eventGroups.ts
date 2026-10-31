import type { EventListItem } from '@/api/eventsApi'

export function recordedAttendance(event: EventListItem): number | null {
  return event.recordedAttendanceCount ?? event.actualAttendees ?? null
}
export interface EventGroup {
  key: string
  title: string
  year: string
  categoryLabel: string
  items: EventListItem[]
  attendance: number
  recordsWithAttendance: number
}
export function groupEvents(items: EventListItem[]): EventGroup[] {
  const groups = new Map<string, EventGroup>()
  for (const event of items) {
    const year = new Intl.DateTimeFormat('en', { year: 'numeric', timeZone: 'Europe/Istanbul' }).format(new Date(event.startAtUtc))
    const title = event.title.normalize('NFC').trim().replace(/\s+/g, ' ')
    const baseTitle = title.replace(new RegExp(`\\s+${year}$`), '').trim() || title
    const key = JSON.stringify([event.category ?? '', baseTitle.toLocaleLowerCase('tr-TR'), event.categoryName?.normalize('NFC').trim().toLocaleLowerCase('tr-TR') ?? '', year])
    let group = groups.get(key)
    if (!group) {
      group = { key, title: baseTitle, year, categoryLabel: event.categoryLabel || (event.category === 'project' ? 'Proje' : 'Etkinlik'), items: [], attendance: 0, recordsWithAttendance: 0 }
      groups.set(key, group)
    }
    group.items.push(event)
    const attendance = recordedAttendance(event)
    if (attendance !== null) { group.attendance += attendance; group.recordsWithAttendance++ }
  }
  return [...groups.values()]
}
