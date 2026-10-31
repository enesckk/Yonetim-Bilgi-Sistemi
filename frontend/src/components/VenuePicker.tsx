import { useEffect, useRef, useState } from 'react'
import { venueLabel, type EventVenueCatalog } from '@/lib/eventVenues'

export function VenuePicker({ catalog, value, onChange }: { catalog: EventVenueCatalog; value: string; onChange: (id: string) => void }) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const root = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const searchInput = useRef<HTMLInputElement>(null)
  useEffect(() => {
    if (!open) return
    searchInput.current?.focus()
    const outside = (e: PointerEvent) => { if (e.target instanceof Node && !root.current?.contains(e.target)) setOpen(false) }
    const escape = (e: KeyboardEvent) => { if (e.key === 'Escape') { setOpen(false); trigger.current?.focus() } }
    document.addEventListener('pointerdown', outside)
    document.addEventListener('keydown', escape)
    return () => { document.removeEventListener('pointerdown', outside); document.removeEventListener('keydown', escape) }
  }, [open])
  const q = search.trim().toLocaleLowerCase('tr-TR')
  const matches = (name: string) => name.toLocaleLowerCase('tr-TR').includes(q)
  const choose = (id: string) => { onChange(id); setOpen(false); setSearch(''); trigger.current?.focus() }
  const option = (id: string, name: string) => <button type="button" key={id} className={value === id ? 'is-selected' : ''} aria-pressed={value === id} onClick={() => choose(id)}><span>{name}</span><span aria-hidden="true">{value === id ? '✓' : ''}</span></button>
  const groups = catalog.groups.filter((g) => matches(g.label) || g.halls.some((h) => matches(h.name)))
  const others = catalog.others.filter((f) => matches(f.name))
  return (
    <div className="venue-picker" ref={root}>
      <button ref={trigger} type="button" className="venue-picker-trigger" aria-label="Takvim birimi" aria-expanded={open} aria-controls="calendar-venue-options" aria-haspopup="dialog" onClick={() => setOpen((v) => !v)}>
        <span>{value ? venueLabel(catalog, value) ?? 'Tesis seçin' : 'Tüm birimler'}</span><span aria-hidden="true">⌄</span>
      </button>
      {open && <div className="venue-picker-popover" id="calendar-venue-options" role="dialog" aria-label="Birim veya salon seçin">
        <input ref={searchInput} aria-label="Birim veya salon ara" type="search" placeholder="Birim veya salon ara…" value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="venue-picker-options">
          {!q && option('', 'Tüm birimler')}
          {groups.map((g) => <div key={g.parent.id} className="venue-picker-group"><p>{g.label}</p>{option(g.parent.id, 'Tüm salonlar')}{g.halls.filter((h) => matches(g.label) || matches(h.name)).map((h) => option(h.id, h.name))}</div>)}
          {others.length > 0 && <div className="venue-picker-group"><p>Diğer tesisler</p>{others.map((f) => option(f.id, f.name))}</div>}
          {q && groups.length === 0 && others.length === 0 && <p className="venue-picker-empty">Eşleşen birim veya salon yok.</p>}
        </div>
      </div>}
    </div>
  )
}
