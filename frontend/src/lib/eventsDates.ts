/** Shared date helpers for events module */

export function startOfDay(d = new Date()) {
  const x = new Date(d)
  x.setHours(0, 0, 0, 0)
  return x
}

export function endOfDay(d = new Date()) {
  const x = new Date(d)
  x.setHours(23, 59, 59, 999)
  return x
}

export function startOfWeek(d = new Date()) {
  const x = startOfDay(d)
  const day = x.getDay() // 0 Sun
  const diff = day === 0 ? -6 : 1 - day // Monday start
  x.setDate(x.getDate() + diff)
  return x
}

export function endOfWeek(d = new Date()) {
  const s = startOfWeek(d)
  const e = endOfDay(s)
  e.setDate(e.getDate() + 6)
  return e
}

export function startOfMonth(d = new Date()) {
  return startOfDay(new Date(d.getFullYear(), d.getMonth(), 1))
}

export function endOfMonth(d = new Date()) {
  return endOfDay(new Date(d.getFullYear(), d.getMonth() + 1, 0))
}

export function toIso(d: Date) {
  return d.toISOString()
}

export function toDateInput(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** A date input represents a local calendar day, inclusive through its last millisecond. */
export function eventDateRange(from: string, to: string): { fromUtc?: string; toUtc?: string } {
  function parse(value: string): Date {
    const date = new Date(`${value}T00:00:00`)
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || !Number.isFinite(date.getTime()) || toDateInput(date) !== value)
      throw new Error('Geçerli bir tarih seçin.')
    return date
  }
  if (from && to && from > to) throw new Error('Bitiş tarihi başlangıç tarihinden önce olamaz.')
  return {
    fromUtc: from ? toIso(startOfDay(parse(from))) : undefined,
    toUtc: to ? toIso(endOfDay(parse(to))) : undefined,
  }
}

export function formatEventWhen(iso: string, isDateOnly = false): string {
  const date = new Date(iso)
  if (isDateOnly) return date.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Europe/Istanbul' })
  return date.toLocaleString('tr-TR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul' })
}

export type EventExportRow = {
  title: string
  statusLabel: string
  startAtUtc: string
  isDateOnly?: boolean
  endAtUtc?: string | null
  facilityName?: string | null
  address?: string | null
  organizingUnitName?: string | null
  responsibleEmployeeName?: string | null
  expectedAttendees?: number | null
  actualAttendees?: number | null
  attendanceCount?: number | null
  latitude?: number | null
  longitude?: number | null
}

export function downloadEventsCsv(rows: EventExportRow[], filename = 'etkinlikler.csv') {
  const header = [
    'Başlık',
    'Durum',
    'Başlangıç',
    'Bitiş',
    'Tesis',
    'Adres',
    'Birim',
    'Sorumlu',
    'Katılımcı',
    'Enlem',
    'Boylam',
  ]
  const escape = (v: string) => `"${v.replace(/"/g, '""')}"`
  const lines = [
    header.join(';'),
    ...rows.map((r) =>
      [
        r.title,
        r.statusLabel,
        formatEventWhen(r.startAtUtc, r.isDateOnly),
        r.endAtUtc ? formatEventWhen(r.endAtUtc, r.isDateOnly) : '',
        r.facilityName ?? '',
        r.address ?? '',
        r.organizingUnitName ?? '',
        r.responsibleEmployeeName ?? '',
        (r.actualAttendees ?? r.attendanceCount ?? r.expectedAttendees) != null ? String(r.actualAttendees ?? r.attendanceCount ?? r.expectedAttendees) : '',
        r.latitude != null ? String(r.latitude) : '',
        r.longitude != null ? String(r.longitude) : '',
      ]
        .map((c) => escape(String(c)))
        .join(';'),
    ),
  ]
  const blob = new Blob(['\uFEFF' + lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export async function downloadEventsPdf(
  rows: EventExportRow[],
  filename = 'etkinlikler.pdf',
) {
  const { jsPDF } = await import('jspdf')
  const doc = new jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4', compress: true })
  const margin = 36
  const pageW = doc.internal.pageSize.getWidth()
  const pageH = doc.internal.pageSize.getHeight()
  const usable = pageW - margin * 2

  doc.setFont('helvetica', 'bold')
  doc.setFontSize(14)
  doc.text('Etkinlik listesi', margin, margin)
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(9)
  doc.setTextColor(90)
  doc.text(
    `${rows.length} kayıt · ${new Date().toLocaleString('tr-TR')}`,
    margin,
    margin + 16,
  )
  doc.setTextColor(20)

  const cols = [
    { key: 'title', label: 'Baslik', w: usable * 0.22 },
    { key: 'status', label: 'Durum', w: usable * 0.09 },
    { key: 'start', label: 'Baslangic', w: usable * 0.14 },
    { key: 'facility', label: 'Tesis', w: usable * 0.18 },
    { key: 'unit', label: 'Birim', w: usable * 0.16 },
    { key: 'owner', label: 'Sorumlu', w: usable * 0.14 },
    { key: 'cap', label: 'Kat.', w: usable * 0.07 },
  ] as const

  let y = margin + 36
  const rowH = 16

  const drawHeader = () => {
    doc.setFont('helvetica', 'bold')
    doc.setFontSize(8)
    let x = margin
    for (const c of cols) {
      doc.text(c.label, x, y)
      x += c.w
    }
    y += 6
    doc.setDrawColor(200)
    doc.line(margin, y, pageW - margin, y)
    y += 12
    doc.setFont('helvetica', 'normal')
  }

  drawHeader()

  for (const r of rows) {
    if (y > pageH - margin) {
      doc.addPage()
      y = margin
      drawHeader()
    }
    const cells = [
      r.title,
      r.statusLabel,
      formatEventWhen(r.startAtUtc, r.isDateOnly),
      r.facilityName ?? '',
      r.organizingUnitName ?? '',
      r.responsibleEmployeeName ?? '',
      (r.actualAttendees ?? r.attendanceCount ?? r.expectedAttendees) != null ? String(r.actualAttendees ?? r.attendanceCount ?? r.expectedAttendees) : '',
    ]
    let x = margin
    cells.forEach((text, i) => {
      const clipped = doc.splitTextToSize(text || '—', cols[i].w - 4)[0] as string
      doc.text(clipped, x, y)
      x += cols[i].w
    })
    y += rowH
  }

  doc.save(filename)
}
