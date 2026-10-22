import { describe, expect, it } from 'vitest'
import { employeeRoleLabel } from './employeeRoleLabel'

describe('employee profile role label', () => {
  it('keeps Abdullah Orhan’s complete duty without repeating his formal title', () => {
    expect(employeeRoleLabel('Teknik Personel Şefi', 'Teknik Personel Şefi - Ses, Işık Sorumlusu')).toBe('Teknik Personel Şefi - Ses, Işık Sorumlusu')
  })

  it.each(['Teknik Personel Şefi', 'Yardımcı Personel Şefi', 'Teknik Personel', 'Yardımcı Personel', 'Yönetici', 'İdari Amir', 'Danışma Personeli', 'Nikah Memuru', 'Büro Personeli', 'Başkan Yardımcısı'])(
    'shows identical title and duty once: %s', title => {
      expect(employeeRoleLabel(title, title)).toBe(title)
    },
  )

  it('handles Turkish casing and extra whitespace', () => {
    expect(employeeRoleLabel('  İDARİ   AMİR ', 'İdari Amir')).toBe('İDARİ AMİR')
  })

  it.each(['-', '–', '—', ':', '|', '·'])('recognizes an expanded duty separated by %s', separator => {
    expect(employeeRoleLabel('Danışma Personeli', `Danışma Personeli ${separator} Salon Tahsis`)).toBe(`Danışma Personeli ${separator} Salon Tahsis`)
  })

  it('preserves distinct titles and duties, including shared word prefixes', () => {
    expect(employeeRoleLabel('Memur', 'Nikah Defteri Takibi - Kurum Dışı Nikah')).toBe('Memur · Nikah Defteri Takibi - Kurum Dışı Nikah')
    expect(employeeRoleLabel('Teknik Personel', 'Teknik Personel Şefi')).toBe('Teknik Personel · Teknik Personel Şefi')
  })

  it('handles missing fields and an expanded formal title', () => {
    expect(employeeRoleLabel(null, 'Danışma Personeli')).toBe('Danışma Personeli')
    expect(employeeRoleLabel('Memur', null)).toBe('Memur')
    expect(employeeRoleLabel(null, null)).toBe('')
    expect(employeeRoleLabel('Teknik Personel Şefi - Ses, Işık Sorumlusu', 'Teknik Personel Şefi')).toBe('Teknik Personel Şefi - Ses, Işık Sorumlusu')
  })
})
