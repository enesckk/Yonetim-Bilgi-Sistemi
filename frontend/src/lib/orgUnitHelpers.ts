import type { LookupItem } from '@/api/employeesApi'

export const ORG_TYPE = {
  Directorate: 3,
  MainUnit: 4,
  SubUnit: 5,
} as const

export function resolveOrgCascade(units: LookupItem[], selectedUnitId: string) {
  const empty = { directorateId: '', mainUnitId: '', subUnitId: '' }
  if (!selectedUnitId) return empty

  const byId = new Map(units.map((u) => [u.id, u]))
  const chain: LookupItem[] = []
  let current: LookupItem | undefined = byId.get(selectedUnitId)
  while (current) {
    chain.unshift(current)
    current = current.parentId ? byId.get(current.parentId) : undefined
  }

  const result = { ...empty }
  for (const u of chain) {
    if (u.type === ORG_TYPE.Directorate) result.directorateId = u.id
    if (u.type === ORG_TYPE.MainUnit) result.mainUnitId = u.id
    if (u.type === ORG_TYPE.SubUnit) result.subUnitId = u.id
  }

  if (!result.directorateId && !result.mainUnitId && !result.subUnitId) {
    const selected = byId.get(selectedUnitId)
    if (selected?.type === ORG_TYPE.Directorate) result.directorateId = selectedUnitId
    else if (selected?.type === ORG_TYPE.MainUnit) result.mainUnitId = selectedUnitId
    else if (selected?.type === ORG_TYPE.SubUnit) result.subUnitId = selectedUnitId
    else result.directorateId = selectedUnitId
  }

  return result
}

export function collectDescendantIds(units: LookupItem[], rootId: string): Set<string> {
  const children = new Map<string, string[]>()
  for (const u of units) {
    if (!u.parentId) continue
    const list = children.get(u.parentId) ?? []
    list.push(u.id)
    children.set(u.parentId, list)
  }

  const result = new Set<string>([rootId])
  const queue = [rootId]
  while (queue.length > 0) {
    const id = queue.pop()!
    for (const childId of children.get(id) ?? []) {
      if (!result.has(childId)) {
        result.add(childId)
        queue.push(childId)
      }
    }
  }
  return result
}
