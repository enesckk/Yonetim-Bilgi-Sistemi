export const eventCategoryOptions = [
  { value: 'education', label: 'Eğitim' }, { value: 'health', label: 'Sağlık' },
  { value: 'social_support', label: 'Sosyal Destek' }, { value: 'culture', label: 'Kültür / Sanat' },
  { value: 'trip', label: 'Gezi' }, { value: 'sports', label: 'Spor' },
  { value: 'youth', label: 'Çocuk / Gençlik' }, { value: 'women', label: 'Kadın' },
  { value: 'elderly', label: 'Yaşlı' }, { value: 'other', label: 'Diğer' },
]
export const projectCategoryOptions = ['TÜBİTAK', 'GİZ', 'AB Projeleri']
export const categoryKey = (s: string) => s.normalize('NFC').trim().replace(/\s+/g, ' ').toLocaleLowerCase('tr-TR')
export function resolveCategory(text: string, project: boolean) {
  if (project) return { category: 'project', categoryName: text }
  const preset = eventCategoryOptions.find(c => categoryKey(c.label) === categoryKey(text))
  return { category: preset?.value ?? (text.trim() ? 'other' : ''), categoryName: preset ? '' : text }
}
