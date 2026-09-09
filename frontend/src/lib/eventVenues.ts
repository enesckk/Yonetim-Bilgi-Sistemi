import type { OrgNode } from '@/api/organizationApi'

const ORG_FACILITY = 6
const CLOSED = 3
const OUT_OF_USE = 8

export type VenueGroup = {
  label: string
  parent: OrgNode
  halls: OrgNode[]
}

export type EventVenueCatalog = {
  groups: VenueGroup[]
  others: OrgNode[]
  flat: OrgNode[]
}

export function isLibraryVenue(n: OrgNode): boolean {
  const name = n.name.toLocaleLowerCase('tr-TR')
  return name.includes('kütüphane')
}

export function collectFacilities(nodes: OrgNode[], acc: OrgNode[] = []): OrgNode[] {
  for (const n of nodes) {
    if (n.type === ORG_FACILITY) acc.push(n)
    if (n.children?.length) collectFacilities(n.children, acc)
  }
  return acc
}

function isKkm(n: OrgNode): boolean {
  const code = (n.code ?? '').toUpperCase()
  if (code === 'KKM' || code === 'FAC_KKM') return true
  const name = n.name.toLocaleLowerCase('tr-TR')
  return name.includes('kültür ve kongre') || name.includes('kultur ve kongre')
}

function byTrName(a: OrgNode, b: OrgNode) {
  return a.name.localeCompare(b.name, 'tr')
}

export function eventVenueCatalog(tree: OrgNode[]): EventVenueCatalog {
  const facilities = collectFacilities(tree).filter(
    (n) => n.status !== CLOSED && n.status !== OUT_OF_USE && !isLibraryVenue(n),
  )
  const kkm = facilities.find(isKkm)
  const halls = kkm ? facilities.filter((n) => n.parentId === kkm.id).sort(byTrName) : []
  const skip = new Set<string>(halls.map((h) => h.id))
  if (kkm && halls.length > 0) skip.add(kkm.id)
  const others = facilities.filter((n) => !skip.has(n.id)).sort(byTrName)
  const groups: VenueGroup[] =
    kkm && halls.length > 0 ? [{ label: kkm.name, parent: kkm, halls }] : []
  const flat = [...(kkm && halls.length > 0 ? [kkm, ...halls] : []), ...others]
  return { groups, others, flat }
}

export function venueLabel(catalog: EventVenueCatalog, id: string): string | null {
  for (const g of catalog.groups) {
    if (g.parent.id === id) return g.parent.name
    const hall = g.halls.find((h) => h.id === id)
    if (hall) return `${g.parent.name} · ${hall.name}`
  }
  return catalog.others.find((n) => n.id === id)?.name ?? catalog.flat.find((n) => n.id === id)?.name ?? null
}

export type VenueCard = {
  id: string
  name: string
  subtitle: string
  ids: string[]
}

export function venueCards(catalog: EventVenueCatalog): VenueCard[] {
  const cards: VenueCard[] = catalog.groups.map((g) => ({
    id: g.parent.id,
    name: g.parent.name,
    subtitle: g.halls.map((h) => h.name.replace(/ Salonu$/i, '')).join(' · '),
    ids: [g.parent.id, ...g.halls.map((h) => h.id)],
  }))
  for (const f of catalog.others) {
    cards.push({
      id: f.id,
      name: f.name,
      subtitle: f.facilityCategoryName || f.typeLabel,
      ids: [f.id],
    })
  }
  return cards
}

export function matchesVenue(eventFacilityId: string | null | undefined, ids: string[]) {
  return Boolean(eventFacilityId && ids.includes(eventFacilityId))
}
