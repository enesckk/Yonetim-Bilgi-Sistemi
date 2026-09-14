import { describe, expect, it } from 'vitest'
import type { ChartPerson, OrgNode } from '@/api/organizationApi'
import { buildOrgSheet, DUTY_GROUPS } from './orgSheetModel'

function node(partial: Partial<OrgNode> & Pick<OrgNode, 'id' | 'name' | 'type'>): OrgNode {
  return {
    typeLabel: '', status: 1, statusLabel: '', children: [], ...partial,
  }
}

function person(id: string, dutyCategory: number, dutyName: string): ChartPerson {
  return { id, fullName: `Person ${id}`, dutyCategory, dutyName }
}

describe('organization print sheet', () => {
  it('counts managers once, groups youth sites under one unit, and totals by actual duty category', () => {
    const youthSite = node({
      id: 'lib', name: 'Aktoprak Gençlik Kütüphanesi', code: 'GK_AKTOPRAK', type: 6,
      managerEmployeeId: 'lib-manager', managerName: 'Library Manager',
      managerDutyName: 'Kütüphane Personeli', managerDutyCategory: 6,
      chartPersonnel: [person('lib-manager', 6, 'Kütüphane Personeli'), person('cleaner', 8, 'Temizlik Personeli')],
    })
    const youth = node({
      id: 'youth', name: 'Gençlik Kütüphaneleri', code: 'GENCLIK_KUT', type: 4,
      chartPersonnel: [person('coordinator', 1, 'Koordinatör')], children: [youthSite],
    })
    const kkm = node({
      id: 'kkm', name: 'Kültür ve Kongre Merkezi', code: 'KKM', type: 4,
      managerEmployeeId: 'kkm-manager', managerName: 'KKM Manager', managerDutyCategory: 1,
      chartPersonnel: [person('technician', 4, 'Teknik Personel')],
    })
    const directorate = node({
      id: 'dir', name: 'Kültür Müdürlüğü', type: 3,
      managerEmployeeId: 'director', managerName: 'Director', managerDutyCategory: 1,
      chartPersonnel: [person('assistant', 1, 'Müdür Yardımcısı')],
      children: [kkm, youth],
    })
    const deputy = node({
      id: 'deputy', name: 'Başkan Yardımcılığı', type: 2,
      managerEmployeeId: 'deputy-person', managerName: 'Deputy', managerDutyCategory: 1,
      children: [directorate],
    })

    const sheet = buildOrgSheet([deputy])!
    expect(sheet.leaders.map((leader) => leader.label)).toEqual([
      'BAŞKAN YARDIMCISI', 'MÜDÜR', 'MÜDÜR YARDIMCISI',
    ])
    expect(sheet.units.map((unit) => unit.node.code)).toEqual(['KKM'])
    expect(sheet.units[0].count).toBe(2)
    expect(sheet.youth?.count).toBe(3)
    expect(sheet.youth?.children).toHaveLength(1)
    expect(sheet.total).toBe(8)
    expect(sheet.areaManagerCount).toBe(2)
    expect(sheet.categoryCounts.get(1)).toBe(5)
    expect(sheet.categoryCounts.get(6)).toBe(1)
    expect(sheet.categoryCounts.get(8)).toBe(1)
    expect(sheet.categoryCounts.get(4)).toBe(1)
    expect([...sheet.categoryCounts.values()].reduce((sum, value) => sum + value, 0)).toBe(sheet.total)
    expect(DUTY_GROUPS).toHaveLength(12)
  })

  it('does not print unstaffed facilities but keeps staffed ones even when out of use', () => {
    const staffed = node({
      id: 'staffed', name: 'Staffed Library', type: 6, status: 8,
      chartPersonnel: [person('one', 6, 'Kütüphane Personeli')],
    })
    const empty = node({ id: 'empty', name: 'Empty Library', type: 6 })
    const youth = node({ id: 'youth', name: 'Gençlik Kütüphaneleri', code: 'GENCLIK_KUT', type: 4,
      children: [staffed, empty] })
    const directorate = node({ id: 'dir', name: 'Directorate', type: 3, children: [youth] })
    const sheet = buildOrgSheet([directorate])!
    expect(sheet.youth?.children.map((child) => child.node.id)).toEqual(['staffed'])
    expect(sheet.total).toBe(1)
  })
})
