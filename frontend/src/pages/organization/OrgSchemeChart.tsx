import { useCallback, useEffect, useLayoutEffect, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react'
import type { OrgNode } from '@/api/organizationApi'
import { OrgHierarchyContent } from './OrgHierarchyContent'

type Props = {
  tree: OrgNode[]
  selectedId: string | null
  onSelect: (id: string) => void
}

type FitMode = 'width' | 'all' | 'custom'
type Point = { x: number; y: number }

export function OrgSchemeChart({ tree, selectedId, onSelect }: Props) {
  return (
    <OrgSchemeViewport>
      <OrgHierarchyContent tree={tree} selectedId={selectedId} onSelect={onSelect} />
    </OrgSchemeViewport>
  )
}

function OrgSchemeViewport({ children }: { children: ReactNode }) {
  const viewRef = useRef<HTMLDivElement>(null)
  const innerRef = useRef<HTMLDivElement>(null)
  const [scale, setScale] = useState(1)
  const [tx, setTx] = useState(0)
  const [ty, setTy] = useState(0)
  const [fullscreen, setFullscreen] = useState(false)
  const [fitMode, setFitMode] = useState<FitMode>('width')
  const scaleRef = useRef(1)
  const txRef = useRef(0)
  const tyRef = useRef(0)
  const modeRef = useRef<FitMode>('width')
  const allFitRef = useRef(1)
  const drag = useRef<{ x: number; y: number; tx: number; ty: number } | null>(null)
  const dragged = useRef(false)

  function update(nextScale: number, nextTx: number, nextTy: number) {
    scaleRef.current = nextScale
    txRef.current = nextTx
    tyRef.current = nextTy
    setScale(nextScale)
    setTx(nextTx)
    setTy(nextTy)
  }

  const contentSize = useCallback(() => {
    const inner = innerRef.current
    if (!inner) return null
    return { width: inner.scrollWidth, height: inner.scrollHeight }
  }, [])

  const boundedPosition = useCallback((nextScale: number, x: number, y: number): Point => {
    const view = viewRef.current
    const size = contentSize()
    if (!view || !size) return { x, y }
    const margin = Math.min(110, view.clientWidth * 0.12)
    const bound = (pos: number, visible: number, extent: number) =>
      extent <= visible ? (visible - extent) / 2 : Math.max(visible - extent - margin, Math.min(margin, pos))
    return {
      x: bound(x, view.clientWidth, size.width * nextScale),
      y: bound(y, view.clientHeight, size.height * nextScale),
    }
  }, [contentSize])

  const applyFit = useCallback((mode: Exclude<FitMode, 'custom'>) => {
    const view = viewRef.current
    const size = contentSize()
    if (!view || !size || size.width < 32 || size.height < 32) return
    const padX = fullscreen ? 36 : 16
    const compact = view.clientWidth < 700
    const padTop = compact ? (fullscreen ? 112 : 106) : (fullscreen ? 76 : 54)
    const padBottom = fullscreen ? 24 : 16
    const boxW = Math.max(220, view.clientWidth - padX * 2)
    const boxH = Math.max(220, view.clientHeight - padTop - padBottom)
    const allFit = Math.min(1, boxW / size.width, boxH / size.height)
    allFitRef.current = allFit
    const widthFit = Math.min(1, boxW / size.width)
    const nextScale = mode === 'all' ? allFit : widthFit
    const scaledHeight = size.height * nextScale
    const x = (view.clientWidth - size.width * nextScale) / 2
    const y = mode === 'all' && scaledHeight < boxH
      ? padTop + (boxH - scaledHeight) / 2
      : padTop
    modeRef.current = mode
    setFitMode(mode)
    update(nextScale, x, y)
  }, [contentSize, fullscreen])

  const zoomTo = useCallback((nextRaw: number, origin?: Point) => {
    const view = viewRef.current
    if (!view) return
    const prev = scaleRef.current
    const next = Math.min(2.5, Math.max(allFitRef.current * 0.7, nextRaw))
    if (Math.abs(next - prev) < 0.0001) return
    const ox = origin?.x ?? view.clientWidth / 2
    const oy = origin?.y ?? view.clientHeight / 2
    const x = ox - ((ox - txRef.current) / prev) * next
    const y = oy - ((oy - tyRef.current) / prev) * next
    const bounded = boundedPosition(next, x, y)
    modeRef.current = 'custom'
    setFitMode('custom')
    update(next, bounded.x, bounded.y)
  }, [boundedPosition])

  useLayoutEffect(() => {
    const view = viewRef.current
    const inner = innerRef.current
    if (!view || !inner) return
    const observer = new ResizeObserver(() => {
      if (modeRef.current !== 'custom') applyFit(modeRef.current)
      else {
        const pos = boundedPosition(scaleRef.current, txRef.current, tyRef.current)
        update(scaleRef.current, pos.x, pos.y)
      }
    })
    observer.observe(view)
    observer.observe(inner)
    const onWheel = (event: WheelEvent) => {
      const delta = event.deltaMode === 1 ? event.deltaY * 16 : event.deltaMode === 2 ? event.deltaY * view.clientHeight : event.deltaY
      if (modeRef.current === 'width') {
        const size = contentSize()
        if (!size || size.height * scaleRef.current <= view.clientHeight) return
        event.preventDefault()
        const horizontal = event.deltaMode === 1 ? event.deltaX * 16 : event.deltaMode === 2 ? event.deltaX * view.clientWidth : event.deltaX
        const pos = boundedPosition(scaleRef.current, txRef.current - horizontal, tyRef.current - delta)
        const topPad = view.clientWidth < 700 ? (fullscreen ? 112 : 106) : (fullscreen ? 76 : 54)
        const bottomPad = fullscreen ? 24 : 16
        const bottom = view.clientHeight - size.height * scaleRef.current - bottomPad
        pos.y = Math.max(bottom, Math.min(topPad, pos.y))
        update(scaleRef.current, pos.x, pos.y)
        return
      }
      event.preventDefault()
      const rect = view.getBoundingClientRect()
      const factor = Math.exp(-Math.max(-180, Math.min(180, delta)) * 0.0015)
      zoomTo(scaleRef.current * factor, { x: event.clientX - rect.left, y: event.clientY - rect.top })
    }
    view.addEventListener('wheel', onWheel, { passive: false })
    return () => {
      observer.disconnect()
      view.removeEventListener('wheel', onWheel)
    }
  }, [applyFit, boundedPosition, contentSize, fullscreen, zoomTo])

  useLayoutEffect(() => {
    applyFit('width')
    const frame = window.requestAnimationFrame(() => applyFit('width'))
    return () => window.cancelAnimationFrame(frame)
  }, [applyFit, fullscreen])

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && fullscreen) setFullscreen(false)
    }
    window.addEventListener('keydown', onKey)
    document.body.classList.toggle('org-scheme-fs-lock', fullscreen)
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.classList.remove('org-scheme-fs-lock')
    }
  }, [fullscreen])

  function onPointerDown(event: ReactPointerEvent<HTMLDivElement>) {
    if (event.button !== 0) return
    dragged.current = false
    const target = event.target as HTMLElement
    if (target.closest('.org-scheme-zoom, .org-scheme-minimap, input, select, textarea')) return
    drag.current = { x: event.clientX, y: event.clientY, tx: txRef.current, ty: tyRef.current }
    if (!target.closest('button, a, [role="button"]')) event.currentTarget.setPointerCapture(event.pointerId)
  }

  function onPointerMove(event: ReactPointerEvent<HTMLDivElement>) {
    if (!drag.current) return
    if (event.pointerType === 'mouse' && event.buttons === 0) { onPointerUp(); return }
    if (Math.abs(event.clientX - drag.current.x) + Math.abs(event.clientY - drag.current.y) > 5) dragged.current = true
    if (!dragged.current) return
    const pos = boundedPosition(scaleRef.current,
      drag.current.tx + event.clientX - drag.current.x,
      drag.current.ty + event.clientY - drag.current.y)
    update(scaleRef.current, pos.x, pos.y)
  }

  function onPointerUp() {
    drag.current = null
  }

  const view = viewRef.current
  const size = contentSize()
  const clamp01 = (value: number) => Math.max(0, Math.min(1, value))
  const miniLeft = view && size ? clamp01(-tx / (size.width * scale)) : 0
  const miniTop = view && size ? clamp01(-ty / (size.height * scale)) : 0
  const miniRight = view && size ? clamp01((view.clientWidth - tx) / (size.width * scale)) : 1
  const miniBottom = view && size ? clamp01((view.clientHeight - ty) / (size.height * scale)) : 1

  return (
    <div
      className={`org-scheme-viewport${fullscreen ? ' is-fs' : ''}`}
      ref={viewRef}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
      onClickCapture={(event) => {
        if (dragged.current) {
          event.preventDefault()
          event.stopPropagation()
          dragged.current = false
        }
      }}
      onDoubleClick={(event) => {
        if (modeRef.current === 'width') return
        if ((event.target as HTMLElement).closest('button, a')) return
        const rect = event.currentTarget.getBoundingClientRect()
        zoomTo(scaleRef.current * 1.3, { x: event.clientX - rect.left, y: event.clientY - rect.top })
      }}
    >
      <div
        className="org-scheme-stage"
        ref={innerRef}
        style={{ transform: `translate(${tx}px, ${ty}px) scale(${scale})` }}
      >
        {children}
      </div>
      <div className="org-scheme-zoom" role="group" aria-label="Şema gezinme ve yakınlaştırma">
        <button type="button" className={fitMode === 'width' ? 'is-fit-active' : undefined} onClick={() => applyFit('width')} title="Sayfa genişliğine sığdır">Genişlik</button>
        <button type="button" className={fitMode === 'all' ? 'is-fit-active' : undefined} onClick={() => applyFit('all')} title="Şemanın tamamını göster">Tümü</button>
        <button type="button" className="is-fs-btn" onClick={() => setFullscreen((value) => !value)} title={fullscreen ? 'Tam ekrandan çık (Esc)' : 'Tam ekran'}>
          {fullscreen ? 'Çık' : 'Tam ekran'}
        </button>
      </div>
      <div className="org-scheme-hint">{fitMode === 'width' ? 'Sürükleyerek veya tekerlekle aşağı-yukarı gez' : 'Sürükleyerek gez · Tekerlekle yakınlaştır · Çift tıkla büyüt'}</div>
      {fullscreen && size && view ? (
        <button
          type="button"
          className="org-scheme-minimap"
          aria-label="Şema genel görünümünde gezin"
          title="Genel görünüm: gitmek istediğiniz bölgeye tıklayın"
          style={{ aspectRatio: `${size.width} / ${size.height}` }}
          onClick={(event) => {
            const rect = event.currentTarget.getBoundingClientRect()
            const x = (event.clientX - rect.left) / rect.width
            const y = (event.clientY - rect.top) / rect.height
            const pos = boundedPosition(scaleRef.current,
              view.clientWidth / 2 - x * size.width * scaleRef.current,
              view.clientHeight / 2 - y * size.height * scaleRef.current)
            if (modeRef.current !== 'width') {
              modeRef.current = 'custom'
              setFitMode('custom')
            }
            update(scaleRef.current, pos.x, pos.y)
          }}
        >
          <span className="org-scheme-mini-title" />
          <span className="org-scheme-mini-leaders" />
          <span className="org-scheme-mini-units" />
          <span className="org-scheme-mini-youth" />
          <span className="org-scheme-mini-footer" />
          <span
            className="org-scheme-mini-window"
            style={{ left: `${miniLeft * 100}%`, top: `${miniTop * 100}%`, width: `${(miniRight - miniLeft) * 100}%`, height: `${(miniBottom - miniTop) * 100}%` }}
          />
        </button>
      ) : null}
    </div>
  )
}
