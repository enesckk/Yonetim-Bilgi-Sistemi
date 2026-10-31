import { useEffect, useId, useRef, useState } from 'react'

const key = (s: string) => s.normalize('NFC').trim().replace(/\s+/g, ' ').toLocaleLowerCase('tr-TR')
export function CategoryCombobox({ label, value, options, onChange, placeholder }: { label: string; value: string; options: string[]; onChange: (value: string) => void; placeholder: string }) {
  const id = useId()
  const root = useRef<HTMLDivElement>(null)
  const input = useRef<HTMLInputElement>(null)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(-1)
  const [editing, setEditing] = useState(false)
  const choices = options.filter((s, i) => options.findIndex(other => key(other) === key(s)) === i).filter(s => !editing || key(s).includes(key(value)))
  const custom = value.trim() && !options.some(s => key(s) === key(value))
  const entries = [...choices, ...(custom ? [value.trim()] : [])]
  useEffect(() => {
    if (!open) return
    const close = (e: PointerEvent) => { if (e.target instanceof Node && !root.current?.contains(e.target)) setOpen(false) }
    document.addEventListener('pointerdown', close)
    return () => document.removeEventListener('pointerdown', close)
  }, [open])
  useEffect(() => {
    if (open && active >= 0) document.getElementById(`${id}-${active}`)?.scrollIntoView({ block: 'nearest' })
  }, [active, open, id])
  function choose(text: string) { setEditing(false); onChange(text); setOpen(false); setActive(-1); input.current?.focus() }
  return <div className="category-combobox" ref={root}>
    <label htmlFor={id}>{label}<span className="event-field-optional">Seçin veya yazın</span></label>
    <div className="category-combobox-control">
      <input ref={input} id={id} role="combobox" aria-label={label} aria-autocomplete="list" aria-expanded={open} aria-controls={`${id}-list`} aria-activedescendant={open && active >= 0 && entries[active] ? `${id}-${active}` : undefined} autoComplete="off" maxLength={120} value={value} placeholder={placeholder} onChange={e => { onChange(e.target.value); setEditing(true); setOpen(true); setActive(-1) }} onFocus={() => { setEditing(false); setOpen(true); setActive(-1) }} onClick={() => { setEditing(false); setOpen(true); setActive(-1) }} onKeyDown={e => {
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); setOpen(true); setActive(n => e.key === 'ArrowDown' ? Math.min(n + 1, entries.length - 1) : Math.max(0, n - 1)) }
        if (e.key === 'Enter' && open) { e.preventDefault(); if (active >= 0 && entries[active]) choose(entries[active]); else setOpen(false) }
        if (e.key === 'Escape' || e.key === 'Tab') setOpen(false)
      }} />
      <button type="button" tabIndex={-1} aria-label={`${label} seçeneklerini göster`} onClick={() => { input.current?.focus(); setEditing(false); setOpen(!open); setActive(-1) }}><svg aria-hidden="true" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.5"><path d="m5 8 5 5 5-5" /></svg></button>
    </div>
    {open && <div className="category-combobox-popover"><p>{editing && value ? 'Eşleşen kategoriler' : 'Önerilen kategoriler'}</p><div id={`${id}-list`} role="listbox" aria-label={`${label} seçenekleri`}>
      {entries.map((text, i) => <button type="button" role="option" aria-selected={i === active} id={`${id}-${i}`} key={`${text}-${i}`} className={i === active ? 'is-active' : ''} onMouseDown={e => e.preventDefault()} onClick={() => choose(text)}>{custom && i === entries.length - 1 ? <><span className="category-add-mark" aria-hidden="true">+</span><span>“{text}” olarak kullan</span></> : <span>{text}</span>}</button>)}
      {!entries.length && <span className="category-combobox-empty">Kategori adını doğrudan yazabilirsiniz.</span>}
    </div></div>}
    <small className="category-field-help">Listeden seçebilir veya kendi kategori adınızı yazabilirsiniz.</small>
  </div>
}
