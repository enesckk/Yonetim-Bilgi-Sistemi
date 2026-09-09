import { useEffect, useId, useRef, useState } from 'react'
import { suggestPlaces, type GeocodeResult } from '@/lib/geocode'

type Props = {
  value: string
  placeholder?: string
  disabled?: boolean
  onChangeQuery: (value: string) => void
  onSelect: (hit: GeocodeResult) => void
}

export function PlaceSearchField({ value, placeholder, disabled, onChangeQuery, onSelect }: Props) {
  const listId = useId()
  const wrapRef = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [hits, setHits] = useState<GeocodeResult[]>([])
  const [active, setActive] = useState(0)
  const pickedRef = useRef<string | null>(null)

  useEffect(() => {
    const q = value.trim()
    if (pickedRef.current && q === pickedRef.current) {
      setHits([])
      setLoading(false)
      setOpen(false)
      return
    }
    if (q.length < 3) {
      setHits([])
      setLoading(false)
      return
    }
    let cancelled = false
    setLoading(true)
    const timer = window.setTimeout(() => {
      void suggestPlaces(q).then((next) => {
        if (cancelled) return
        setHits(next)
        setActive(0)
        setLoading(false)
        setOpen(next.length > 0)
      })
    }, 320)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [value])

  useEffect(() => {
    function onDoc(e: MouseEvent) {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [])

  function pick(hit: GeocodeResult) {
    pickedRef.current = hit.displayName
    onSelect(hit)
    setOpen(false)
    setHits([])
  }

  return (
    <div className="place-search" ref={wrapRef}>
      <input
        value={value}
        disabled={disabled}
        placeholder={placeholder}
        autoComplete="off"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        onChange={(e) => {
          pickedRef.current = null
          onChangeQuery(e.target.value)
          setOpen(true)
        }}
        onFocus={() => {
          if (hits.length) setOpen(true)
        }}
        onKeyDown={(e) => {
          if (!open || hits.length === 0) return
          if (e.key === 'ArrowDown') {
            e.preventDefault()
            setActive((i) => Math.min(hits.length - 1, i + 1))
          } else if (e.key === 'ArrowUp') {
            e.preventDefault()
            setActive((i) => Math.max(0, i - 1))
          } else if (e.key === 'Enter') {
            e.preventDefault()
            pick(hits[active] ?? hits[0])
          } else if (e.key === 'Escape') {
            setOpen(false)
          }
        }}
      />
      {loading ? <span className="place-search-status">Aranıyor…</span> : null}
      {open && hits.length > 0 ? (
        <ul className="place-search-list" id={listId} role="listbox">
          {hits.map((hit, i) => (
            <li key={`${hit.latitude}-${hit.longitude}-${i}`}>
              <button
                type="button"
                role="option"
                aria-selected={i === active}
                className={i === active ? 'is-on' : ''}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => pick(hit)}
              >
                {hit.displayName}
              </button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  )
}
