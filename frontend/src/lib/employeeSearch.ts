function normalized(value: string): string {
  return value.toLocaleLowerCase('tr-TR').normalize('NFD').replace(/\p{M}/gu, '').replace(/ı/g, 'i')
}

export function matchesEmployeeSearch(employee: { name: string; employeeNumber?: string | null }, query: string): boolean {
  const terms = normalized(query).trim().split(/\s+/u).filter(Boolean)
  const number = employee.employeeNumber ?? ''
  const text = normalized(`${employee.name} ${number} ${number.replace(/[\s-]/gu, '')}`)
  return terms.every(term => text.includes(term))
}
