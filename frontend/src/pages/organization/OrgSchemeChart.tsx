import { useCallback, useEffect, useLayoutEffect, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react'
import type { OrgNode } from '@/api/organizationApi'
import { OrgHierarchyContent } from './OrgHierarchyContent'

type Props = {
  tree: OrgNode[]
  selectedId: string | null
  onSelect: (id: string) => void
}

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
  const scaleRef = useRef(1)
  const txRef = useRef(0)
  const tyRef = useRef(0)
  const fitRef = useRef(1)
  const userZoom = useRef(false)
  const drag = useRef<{ x: number; y: number; tx: number; ty: number } | null>(null)

  scaleRef.current = scale
  txRef.current = tx
  tyRef.current = ty

  const applyFit = useCallback((force = false) => {
    const view = viewRef.current
    const inner = innerRef.current
    if (!view || !inner) return
    const padX = fullscreen ? 40 : 20
    const padTop = fullscreen ? 72 : 20
    const padBottom = fullscreen ? 28 : 20
    const boxW = Math.max(220, view.clientWidth - padX * 2)
    const boxH = Math.max(220, view.clientHeight - padTop - padBottom)
    const prev = inner.style.transform
    inner.style.transform = 'none'
    const cw = Math.max(inner.scrollWidth, inner.offsetWidth)
    const ch = Math.max(inner.scrollHeight, inner.offsetHeight)
    inner.style.transform = prev
    if (cw < 32 || ch < 32) return
    const next = Math.min(1.05, boxW / cw, boxH / ch)
    fitRef.current = next
    if (!force && userZoom.current) return
    userZoom.current = false
    const scaledW = cw * next
    const scaledH = ch * next
    const nextTx = (view.clientWidth - scaledW) / 2
    const nextTy =
      fullscreen && scaledH <= boxH
        ? padTop + (boxH - scaledH) / 2
        : padTop
    scaleRef.current = next
    txRef.current = nextTx
    tyRef.current = nextTy
    setScale(next)
    setTx(nextTx)
    setTy(nextTy)
  }, [fullscreen])

  const zoomTo = useCallback((nextRaw: number, origin?: { x: number; y: number }) => {
    const view = viewRef.current
    if (!view) return
    const min = fitRef.current
    const max = Math.max(2.8, fitRef.current * 4)
    const prev = scaleRef.current
    const next = Math.min(max, Math.max(min, nextRaw))
    if (Math.abs(next - prev) < 0.0001) return
    const ox = origin?.x ?? view.clientWidth / 2
    const oy = origin?.y ?? view.clientHeight / 2
    userZoom.current = true
    scaleRef.current = next
    const nextTx = ox - ((ox - txRef.current) / prev) * next
    const nextTy = oy - ((oy - tyRef.current) / prev) * next
    txRef.current = nextTx
    tyRef.current = nextTy
    setScale(next)
    setTx(nextTx)
    setTy(nextTy)
  }, [])

  useLayoutEffect(() => {
    const view = viewRef.current
    const inner = innerRef.current
    if (!view || !inner) return
    const ro = new ResizeObserver(() => applyFit(false))
    ro.observe(view)
    ro.observe(inner)
    const onWheelNative = (e: WheelEvent) => {
      e.preventDefault()
      const rect = view.getBoundingClientRect()
      const step = e.deltaY < 0 ? 1.05 : 1 / 1.05
      zoomTo(scaleRef.current * step, { x: e.clientX - rect.left, y: e.clientY - rect.top })
    }
    view.addEventListener('wheel', onWheelNative, { passive: false })
    return () => {
      ro.disconnect()
      view.removeEventListener('wheel', onWheelNative)
    }
  }, [applyFit, zoomTo])

  useLayoutEffect(() => {
    userZoom.current = false
    applyFit(true)
    const fitIfIdle = () => {
      if (!userZoom.current) applyFit(true)
    }
    let inner = 0
    const outer = window.requestAnimationFrame(() => {
      inner = window.requestAnimationFrame(fitIfIdle)
    })
    const t1 = window.setTimeout(fitIfIdle, 80)
    const t2 = window.setTimeout(fitIfIdle, 280)
    const t3 = window.setTimeout(fitIfIdle, 520)
    return () => {
      window.cancelAnimationFrame(outer)
      window.cancelAnimationFrame(inner)
      window.clearTimeout(t1)
      window.clearTimeout(t2)
      window.clearTimeout(t3)
    }
  }, [applyFit, fullscreen])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && fullscreen) setFullscreen(false)
    }
    window.addEventListener('keydown', onKey)
    document.body.classList.toggle('org-scheme-fs-lock', fullscreen)
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.classList.remove('org-scheme-fs-lock')
    }
  }, [fullscreen])

  function onPointerDown(e: ReactPointerEvent<HTMLDivElement>) {
    if (e.button !== 0) return
    const el = e.target as HTMLElement
    if (el.closest('button, a, input, select, textarea, [role="button"]')) return
    drag.current = { x: e.clientX, y: e.clientY, tx: txRef.current, ty: tyRef.current }
    e.currentTarget.setPointerCapture(e.pointerId)
  }

  function onPointerMove(e: ReactPointerEvent<HTMLDivElement>) {
    if (!drag.current) return
    setTx(drag.current.tx + (e.clientX - drag.current.x))
    setTy(drag.current.ty + (e.clientY - drag.current.y))
  }

  function onPointerUp() {
    drag.current = null
  }

  return (
    <div
      className={`org-scheme-viewport${fullscreen ? ' is-fs' : ''}`}
      ref={viewRef}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
    >
      <div
        className="org-scheme-stage"
        ref={innerRef}
        style={{ transform: `translate(${tx}px, ${ty}px) scale(${scale})` }}
      >
        {children}
      </div>
      <div className="org-scheme-zoom" role="group" aria-label="Şema yakınlaştırma">
        <button type="button" onClick={() => zoomTo(scaleRef.current / 1.12)} title="Uzaklaştır">
          −
        </button>
        <button type="button" onClick={() => zoomTo(scaleRef.current * 1.12)} title="Yakınlaştır">
          +
        </button>
        <button
          type="button"
          className="is-reset"
          onClick={() => {
            userZoom.current = false
            applyFit(true)
          }}
          title="Sığdır"
        >
          Sığdır
        </button>
        <button
          type="button"
          className="is-fs-btn"
          onClick={() => setFullscreen((v) => !v)}
          title={fullscreen ? 'Tam ekrandan çık (Esc)' : 'Tam ekran'}
        >
          {fullscreen ? 'Çık' : 'Tam ekran'}
        </button>
      </div>
    </div>
  )
}
