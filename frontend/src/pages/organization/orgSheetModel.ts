import type { ChartPerson, OrgNode } from '@/api/organizationApi'

const TYPE = { Deputy: 2, Directorate: 3, Main: 4, Sub: 5, Facility: 6 } as const
const OUT_OF_USE = 8
const ORDER = ['KKM', 'SANAT', 'NIKAH', 'DTSS', 'SAMI', 'BILIM', 'AGROPARK', 'GEZI', 'GENCLIK_KUT']
const YOUTH_ORDER = [
  'GK_AKTOPRAK', 'GK_ALINACAR', 'GK_BASAK', 'GK_BELKIS', 'GK_EYUPSULTAN',
  'GK_KUZEYSEHIR', 'GK_MAE', 'GK_MERVE', 'GK_MUTERIM', 'GK_NURTEPE',
  'GK_OKTAY', 'GK_PIRSULTAN', 'GK_ZEYTINLI', 'GK_AKSU', 'GK_NFK',
  'GK_YESILOVA', 'GK_KARACAGOLAN', 'GK_KOCATEPE', 'GK_SIRINEVLER',
  'GK_SEYRANTEPE', 'GK_ERBAKAN',
]

export const DUTY_GROUPS = [
  { category: 1, label: 'Yönetim görevi' },
  { category: 2, label: 'İdari / nikâh' },
  { category: 3, label: 'Eğitmen' },
  { category: 4, label: 'Teknik' },
  { category: 5, label: 'Danışma' },
  { category: 6, label: 'Kütüphane' },
  { category: 7, label: 'Yardımcı personel' },
  { category: 8, label: 'Temizlik' },
  { category: 9, label: 'Proje / program' },
  { category: 10, label: 'Sosyal medya' },
  { category: 11, label: 'Halkla ilişkiler' },
  { category: 99, label: 'Diğer' },
] as const

const knownCategories = new Set<number>(DUTY_GROUPS.map((group) => group.category))

export interface SheetPerson {
  id: string
  name: string
  role: string
  category: number
  isAreaManager: boolean
}

export interface SheetUnit {
  node: OrgNode
  people: SheetPerson[]
  children: SheetUnit[]
  count: number
}

export interface SheetLeader {
  nodeId: string
  person: SheetPerson
  label: string
}

export interface OrgSheetModel {
  titleNode: OrgNode
  leaders: SheetLeader[]
  directorateStaff: SheetPerson[]
  units: SheetUnit[]
  youth: SheetUnit | null
  total: number
  areaManagerCount: number
  categoryCounts: Map<number, number>
}

function normalizeCategory(value?: number | null): number {
  return value != null && knownCategories.has(value) ? value : 99
}

function managerCategory(node: OrgNode): number {
  if (node.managerDutyCategory != null) return normalizeCategory(node.managerDutyCategory)
  // Supports the old API response during a rolling deployment.
  const duty = (node.managerDutyName ?? '').toLocaleLowerCase('tr-TR')
  if (duty.includes('kütüphane')) return 6
  if (duty.includes('temizlik')) return 8
  if (duty.includes('teknik') || duty.includes('mühendis')) return 4
  if (duty.includes('danışma')) return 5
  if (duty.includes('eğitmen')) return 3
  if (duty.includes('yardımcı')) return 7
  if (duty.includes('proje') || duty.includes('program')) return 9
  if (duty.includes('memur') || duty.includes('büro') || duty.includes('nikâh')) return 2
  return 1
}

function roleOf(person: ChartPerson): string {
  return person.dutyName || person.jobTitleName || 'Görev tanımlanmamış'
}

function managerRole(node: OrgNode): string {
  if (node.managerDutyName) return node.managerDutyName
  if (node.type === TYPE.Deputy) return 'Başkan Yardımcısı'
  if (node.type === TYPE.Directorate) return 'Müdür'
  if (node.type === TYPE.Facility) return 'Tesis amiri'
  return 'Birim amiri'
}

function managerOf(node: OrgNode, isAreaManager: boolean): SheetPerson | null {
  if (!node.managerEmployeeId || !node.managerName) return null
  return {
    id: node.managerEmployeeId,
    name: node.managerName,
    role: managerRole(node),
    category: managerCategory(node),
    isAreaManager,
  }
}

function ownPeople(node: OrgNode, isAreaManager = true): SheetPerson[] {
  const people: SheetPerson[] = []
  const manager = managerOf(node, isAreaManager)
  if (manager) people.push(manager)
  for (const person of node.chartPersonnel ?? []) {
    if (people.some((item) => item.id === person.id)) continue
    people.push({
      id: person.id,
      name: person.fullName,
      role: roleOf(person),
      category: normalizeCategory(person.dutyCategory),
      isAreaManager: false,
    })
  }
  return people
}

function visibleChildren(children: OrgNode[]): OrgNode[] {
  return children.filter((child) => {
    if (![TYPE.Main, TYPE.Sub, TYPE.Facility].includes(child.type as 4 | 5 | 6)) {
      return false
    }
    if (child.type === TYPE.Facility || child.status === OUT_OF_USE) return hasPeople(child)
    return true
  })
}

function hasPeople(node: OrgNode): boolean {
  return Boolean(node.managerEmployeeId && node.managerName) ||
    Boolean(node.chartPersonnel?.length) ||
    (node.children ?? []).some(hasPeople)
}

function makeUnit(node: OrgNode): SheetUnit {
  const children = visibleChildren(node.children ?? []).map(makeUnit)
  const people = ownPeople(node)
  const ids = new Set(people.map((p) => p.id))
  const walk = (unit: SheetUnit) => {
    for (const person of unit.people) ids.add(person.id)
    unit.children.forEach(walk)
  }
  children.forEach(walk)
  return { node, people, children, count: ids.size }
}

function codeOrder(node: OrgNode, order: string[]): number {
  const index = order.indexOf((node.code ?? '').toUpperCase())
  return index === -1 ? order.length : index
}

function orderNodes(nodes: OrgNode[], order: string[]) {
  return [...nodes].sort((a, b) =>
    codeOrder(a, order) - codeOrder(b, order) || a.name.localeCompare(b.name, 'tr'),
  )
}

function findFirst(nodes: OrgNode[], type: number): OrgNode | null {
  for (const node of nodes) {
    if (node.type === type) return node
    const nested = findFirst(node.children ?? [], type)
    if (nested) return nested
  }
  return null
}

export function buildOrgSheet(tree: OrgNode[]): OrgSheetModel | null {
  if (tree.length === 0) return null
  const directorate = findFirst(tree, TYPE.Directorate) ?? findFirst(tree, TYPE.Deputy) ?? tree[0]
  const deputy = findFirst(tree, TYPE.Deputy)
  const leaders: SheetLeader[] = []
  for (const [node, label] of [[deputy, 'BAŞKAN YARDIMCISI'], [directorate, 'MÜDÜR']] as const) {
    if (!node || (label === 'MÜDÜR' && node.type !== TYPE.Directorate)) continue
    const person = managerOf(node, false)
    if (person) leaders.push({ nodeId: node.id, person, label })
  }

  const directorateStaff = ownPeople(directorate, false).filter(
    (person) => !leaders.some((lead) => lead.person.id === person.id),
  )
  const assistant = directorateStaff.filter((person) =>
    /müdür yardımcısı|müdür yardimcisi/i.test(person.role),
  )
  for (const person of assistant) leaders.push({ nodeId: directorate.id, person, label: 'MÜDÜR YARDIMCISI' })
  const otherDirectorateStaff = directorateStaff.filter(
    (person) => !assistant.some((item) => item.id === person.id),
  )

  let unitNodes = visibleChildren(directorate.children ?? [])
  if (unitNodes.length === 0) {
    unitNodes = (directorate.children ?? []).filter((node) => node.type === TYPE.Main && node.status !== OUT_OF_USE)
  }
  const ordered = orderNodes(unitNodes, ORDER)
  const youthNode = ordered.find((node) => (node.code ?? '').toUpperCase() === 'GENCLIK_KUT')
  const units = ordered.filter((node) => node.id !== youthNode?.id).map(makeUnit)
  const youth = youthNode ? makeUnit(youthNode) : null
  if (youth) youth.children = orderNodes(youth.children.map((child) => child.node), YOUTH_ORDER).map(
    (node) => youth.children.find((child) => child.node.id === node.id)!,
  )

  const all = new Map<string, SheetPerson>()
  const add = (person: SheetPerson) => {
    if (!all.has(person.id)) all.set(person.id, person)
  }
  leaders.forEach((leader) => add(leader.person))
  otherDirectorateStaff.forEach(add)
  const visit = (unit: SheetUnit) => {
    unit.people.forEach(add)
    unit.children.forEach(visit)
  }
  units.forEach(visit)
  if (youth) visit(youth)
  const categoryCounts = new Map<number, number>(DUTY_GROUPS.map((group) => [group.category, 0]))
  for (const person of all.values()) {
    categoryCounts.set(person.category, (categoryCounts.get(person.category) ?? 0) + 1)
  }
  const areaManagerIds = new Set<string>()
  const collectAreaManagers = (unit: SheetUnit) => {
    unit.people.filter((person) => person.isAreaManager).forEach((person) => areaManagerIds.add(person.id))
    unit.children.forEach(collectAreaManagers)
  }
  units.forEach(collectAreaManagers)
  if (youth) collectAreaManagers(youth)
  return {
    titleNode: directorate,
    leaders,
    directorateStaff: otherDirectorateStaff,
    units,
    youth,
    total: all.size,
    areaManagerCount: areaManagerIds.size,
    categoryCounts,
  }
}
