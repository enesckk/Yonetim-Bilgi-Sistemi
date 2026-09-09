import { Link } from 'react-router-dom'
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
} from 'react'
import type { ChartPerson, OrgNode } from '@/api/organizationApi'

const TYPE = {
  Municipality: 1,
  DeputyPresidency: 2,
  Directorate: 3,
  MainUnit: 4,
  SubUnit: 5,
  Facility: 6,
} as const

const STATUS = {
  Closed: 3,
  UnderRenovation: 4,
  OutOfUse: 8,
} as const

const COLUMN_ORDER = [
  'KKM',
  'SANAT',
  'NIKAH',
  'DTSS',
  'SAMI',
  'GENCLIK_KUT',
  'BILIM',
  'AGROPARK',
  'GEZI',
]

const YOUTH_ORDER = [
  'GK_AKTOPRAK',
  'GK_ALINACAR',
  'GK_BASAK',
  'GK_BELKIS',
  'GK_EYUPSULTAN',
  'GK_KUZEYSEHIR',
  'GK_MAE',
  'GK_MERVE',
  'GK_MUTERIM',
  'GK_NURTEPE',
  'GK_OKTAY',
  'GK_PIRSULTAN',
  'GK_ZEYTINLI',
  'GK_AKSU',
  'GK_NFK',
  'GK_YESILOVA',
  'GK_KARACAGOLAN',
  'GK_KOCATEPE',
  'GK_SIRINEVLER',
  'GK_SEYRANTEPE',
  'GK_ERBAKAN',
]

const LEGEND: { tone: string; label: string }[] = [
  { tone: 'lead', label: 'Amir / yönetici / şef' },
  { tone: 'technical', label: 'Teknik personel' },
  { tone: 'reception', label: 'Danışma' },
  { tone: 'instructor', label: 'Eğitmen' },
  { tone: 'library', label: 'Kütüphane' },
  { tone: 'admin', label: 'İdari / nikah' },
  { tone: 'project', label: 'Proje / program' },
  { tone: 'comms', label: 'İletişim' },
  { tone: 'auxiliary', label: 'Yardımcı personel' },
  { tone: 'cleaning', label: 'Temizlik' },
]

type Props = {
  tree: OrgNode[]
  selectedId: string | null
  onSelect: (id: string) => void
}

function collectByType(nodes: OrgNode[], type: number): OrgNode[] {
  const out: OrgNode[] = []
  const walk = (list: OrgNode[]) => {
    for (const n of list) {
      if (n.type === type) out.push(n)
      if (n.children?.length) walk(n.children)
    }
  }
  walk(nodes)
  return out
}

function findFirst(nodes: OrgNode[], type: number): OrgNode | null {
  return collectByType(nodes, type)[0] ?? null
}

function stripOutOfUse(nodes: OrgNode[]): OrgNode[] {
  return nodes
    .filter((n) => n.status !== STATUS.OutOfUse)
    .map((n) => ({ ...n, children: stripOutOfUse(n.children ?? []) }))
}

/** Salon / sahne: personel şemasında boş kutu olarak uzamasın. KKM salonları takvimde. */
function isVenueChild(parent: OrgNode, child: OrgNode) {
  return (parent.code ?? '').toUpperCase() === 'KKM' && child.type === TYPE.Facility
}

function peopleOf(node: OrgNode) {
  return node.chartPersonnel ?? []
}

function hasOwnStaff(node: OrgNode) {
  return Boolean(node.managerName) || peopleOf(node).length > 0
}

function schemeChildren(node: OrgNode) {
  return (node.children ?? []).filter(
    (c) =>
      (c.type === TYPE.SubUnit || c.type === TYPE.Facility || c.type === TYPE.MainUnit) &&
      !isVenueChild(node, c),
  )
}

function isSchemeVisible(node: OrgNode): boolean {
  if (hasOwnStaff(node) || node.status === STATUS.Closed || node.status === STATUS.UnderRenovation) {
    return true
  }
  return schemeChildren(node).some(isSchemeVisible)
}

function staffCount(node: OrgNode): number {
  return peopleOf(node).length + schemeChildren(node).reduce((sum, child) => sum + staffCount(child), 0)
}

function codeIndex(code: string | null | undefined, order: string[]) {
  if (!code) return 900
  const i = order.indexOf(code)
  return i < 0 ? 800 : i
}

function sortColumns(nodes: OrgNode[]) {
  return [...nodes].sort((a, b) => codeIndex(a.code, COLUMN_ORDER) - codeIndex(b.code, COLUMN_ORDER))
}

function sortYouth(nodes: OrgNode[]) {
  return [...nodes].sort((a, b) => {
    const ai = codeIndex(a.code, YOUTH_ORDER)
    const bi = codeIndex(b.code, YOUTH_ORDER)
    if (ai !== bi) return ai - bi
    return a.name.localeCompare(b.name, 'tr')
  })
}

function columnAccent(node: OrgNode): string {
  const code = (node.code ?? '').toUpperCase()
  if (node.status === STATUS.Closed) return 'closed'
  if (node.status === STATUS.UnderRenovation) return 'reno'
  if (code === 'KKM') return 'kkm'
  if (code === 'SANAT' || code === 'NIKAH' || code === 'DTSS') return 'venue'
  if (code === 'GENCLIK_KUT') return 'youth'
  if (code === 'BILIM') return 'bilim'
  if (code === 'AGROPARK') return 'agro'
  if (code === 'GEZI') return 'gezi'
  if (code === 'SAMI' || code.startsWith('GK_') || code === 'SAMI_COCUK') return 'library'
  if (code === 'IDARI') return 'admin'
  return 'default'
}

function personRole(p: ChartPerson): string {
  return p.dutyName || p.jobTitleName || 'Görev tanımlanmamış'
}

function managerRole(node: OrgNode): string {
  if (node.managerDutyName) return node.managerDutyName
  if (node.type === TYPE.DeputyPresidency) return 'Başkan Yardımcısı'
  if (node.type === TYPE.Directorate) return 'Müdür'
  if (node.type === TYPE.Facility) return 'Tesis amiri'
  if (node.type === TYPE.MainUnit || node.type === TYPE.SubUnit) return 'Birim amiri'
  return node.typeLabel
}

function emptyAmir(node: OrgNode): string {
  if (node.status === STATUS.Closed) return 'Kapalı'
  if (node.status === STATUS.UnderRenovation) return 'Tadilatta'
  return 'Amir yok'
}

function statusBadge(node: OrgNode): string | null {
  if (node.status === STATUS.Closed) return 'Kapalı'
  if (node.status === STATUS.UnderRenovation) return 'Tadilat'
  return null
}

function isMemur(p: ChartPerson) {
  return (p.employmentTypeName ?? '').toLocaleLowerCase('tr-TR').includes('memur')
}

/** Referans müdürlük şeması: yönetim omurgası + birim kolonları + personel kartları */
export function OrgSchemeChart({ tree, selectedId, onSelect }: Props) {
  const liveTree = stripOutOfUse(tree)

  if (liveTree.length === 0) {
    return (
      <div className="org-scheme-empty">
        <p>Organizasyon şeması için henüz birim yok.</p>
        <p className="muted small">Veriler eklendikçe şema otomatik oluşur.</p>
      </div>
    )
  }

  const directorate =
    findFirst(liveTree, TYPE.Directorate) ??
    findFirst(liveTree, TYPE.DeputyPresidency) ??
    findFirst(liveTree, TYPE.Municipality) ??
    liveTree[0]

  const leadership: OrgNode[] = []
  const deputy = findFirst(liveTree, TYPE.DeputyPresidency)
  if (deputy) leadership.push(deputy)
  if (directorate && directorate.type === TYPE.Directorate) leadership.push(directorate)

  const extraLeads = (directorate.chartPersonnel ?? []).filter((p) => {
    const blob = `${p.dutyName ?? ''} ${p.jobTitleName ?? ''}`.toLocaleLowerCase('tr-TR')
    return blob.includes('yardımcı') || blob.includes('müdür yardımcısı')
  })

  const directorateChildren = directorate?.children ?? []
  const columns = sortColumns(
    directorateChildren.filter(
      (c) => c.type === TYPE.MainUnit || c.type === TYPE.Facility || c.type === TYPE.SubUnit,
    ),
  )
  const fallbackColumns =
    columns.length > 0
      ? columns
      : sortColumns(collectByType(liveTree, TYPE.MainUnit).concat(collectByType(liveTree, TYPE.Facility)))

  const titleNode = directorate

  return (
    <OrgSchemeViewport>
      <div className="org-scheme" id="organization-scheme-export">
        <div className="org-scheme-scroll">
          <header
            className={`org-scheme-banner${selectedId === titleNode.id ? ' is-selected' : ''}`}
            onClick={() => onSelect(titleNode.id)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') onSelect(titleNode.id)
            }}
          >
            {titleNode.name.toLocaleUpperCase('tr-TR')}
          </header>

          <div className="org-scheme-spine">
            <div className="org-scheme-vline" aria-hidden />
            {leadership.map((n) => (
              <button
                key={n.id}
                type="button"
                className={`org-scheme-lead${selectedId === n.id ? ' is-selected' : ''}`}
                onClick={() => onSelect(n.id)}
              >
                <strong>{n.managerName ?? n.name}</strong>
                <span>{n.managerName ? managerRole(n) : n.typeLabel}</span>
              </button>
            ))}
            {extraLeads.map((p) => (
              <Link key={p.id} to={`/employees/${p.id}`} className="org-scheme-lead is-deputy">
                <strong>{p.fullName}</strong>
                <span>{personRole(p)}</span>
              </Link>
            ))}
            {leadership.length === 0 && extraLeads.length === 0 && (
              <div className="org-scheme-lead is-placeholder">
                <strong>Yönetim katmanı</strong>
                <span>Sorumlu personel atanınca burada görünür</span>
              </div>
            )}
          </div>

          {fallbackColumns.length > 0 && (
            <>
              <div className="org-scheme-bridge" aria-hidden>
                <div className="org-scheme-bridge-v" />
                <div className="org-scheme-bridge-h" />
              </div>
              <div className="org-scheme-columns">
                {fallbackColumns.map((col) => (
                  <UnitColumn
                    key={col.id}
                    unit={col}
                    selectedId={selectedId}
                    onSelect={onSelect}
                  />
                ))}
              </div>
            </>
          )}

          {fallbackColumns.length === 0 && (
            <p className="org-scheme-hint muted">
              Ana birim / tesis eklendikçe şema kolonları burada oluşacak.
            </p>
          )}
        </div>

        <ul className="org-scheme-legend" aria-label="Görev renkleri">
          {LEGEND.map((item) => (
            <li key={item.tone}>
              <span className={`org-scheme-swatch tone-${item.tone}`} />
              {item.label}
            </li>
          ))}
          <li>
            <span className="org-scheme-swatch is-closed" />
            Kapalı tesis
          </li>
          <li>
            <span className="org-scheme-swatch is-reno" />
            Tadilat
          </li>
        </ul>
      </div>
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
    const cover = Math.min(boxW / cw, boxH / ch)
    const floor = Math.max(0.18, Math.min(1.05, cover))
    fitRef.current = floor
    const next = floor
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
    if (!view) return
    const ro = new ResizeObserver(() => applyFit(false))
    ro.observe(view)
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

function UnitColumn({
  unit,
  selectedId,
  onSelect,
}: {
  unit: OrgNode
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  const rawSubs = schemeChildren(unit).filter(isSchemeVisible)
  const subUnits = unit.code === 'GENCLIK_KUT' ? sortYouth(rawSubs) : rawSubs
  const hasSubs = subUnits.length > 0
  const directPeople = peopleOf(unit)
  const accent = columnAccent(unit)
  const badge = statusBadge(unit)
  const isYouth = unit.code === 'GENCLIK_KUT'
  const wide = !isYouth && (subUnits.length > 3 || staffCount(unit) > 8)

  return (
    <article
      className={`org-scheme-col accent-${accent}${wide ? ' is-wide' : ''}${isYouth ? ' is-youth' : ''}${selectedId === unit.id ? ' is-selected' : ''}`}
    >
      <button
        type="button"
        className="org-scheme-col-head"
        onClick={() => onSelect(unit.id)}
      >
        <span>{unit.name}</span>
        {badge ? <em className={`org-scheme-status is-${accent}`}>{badge}</em> : null}
      </button>

      <div className="org-scheme-manager">
        {unit.managerName ? (
          <>
            <strong>{unit.managerName}</strong>
            <span>{isYouth ? 'Koordinatör' : managerRole(unit)}</span>
          </>
        ) : (
          <span className="is-empty">{emptyAmir(unit)}</span>
        )}
      </div>

      {directPeople.length > 0 ? <PersonGrid people={directPeople} /> : null}

      {hasSubs && isYouth ? (
        <YouthLibraries
          libraries={subUnits}
          selectedId={selectedId}
          onSelect={onSelect}
        />
      ) : hasSubs ? (
        <div className={`org-scheme-subs${subUnits.length > 3 ? ' is-wide' : ''}`}>
          {subUnits.map((sub) => (
            <SchemeSubNode key={sub.id} node={sub} selectedId={selectedId} onSelect={onSelect} />
          ))}
        </div>
      ) : null}
    </article>
  )
}

function shortLibName(name: string) {
  return name.replace(/ Gençlik Kütüphanesi$/i, '').replace(/ Kütüphanesi$/i, '')
}

function YouthLibraries({
  libraries,
  selectedId,
  onSelect,
}: {
  libraries: OrgNode[]
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  return (
    <ul className="org-scheme-youth-grid">
      {libraries.map((lib) => {
        const libBadge = statusBadge(lib)
        const people = peopleOf(lib)
        return (
          <li key={lib.id} className="org-scheme-lib-card">
            <button
              type="button"
              className={`org-scheme-lib${selectedId === lib.id ? ' is-selected' : ''}${libBadge ? ` is-${columnAccent(lib)}` : ''}`}
              onClick={() => onSelect(lib.id)}
            >
              <strong>{shortLibName(lib.name)}</strong>
              <span>
                {lib.managerName
                  ? lib.managerName
                  : people.length
                    ? `${people.length} personel`
                    : 'Amir yok'}
              </span>
              {libBadge ? <em>{libBadge}</em> : null}
            </button>
            {people.length > 0 ? <PersonGrid people={people} /> : <p className="org-scheme-lib-empty">Personel yok</p>}
          </li>
        )
      })}
    </ul>
  )
}

function SchemeSubNode({
  node,
  selectedId,
  onSelect,
}: {
  node: OrgNode
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  if (!isSchemeVisible(node)) return null
  const nested = schemeChildren(node).filter(isSchemeVisible)
  const accent = columnAccent(node)
  const badge = statusBadge(node)
  const people = peopleOf(node)

  return (
    <div className={`org-scheme-sub accent-${accent}${selectedId === node.id ? ' is-selected' : ''}`}>
      <button type="button" className="org-scheme-sub-head" onClick={() => onSelect(node.id)}>
        <span>{node.name}</span>
        {badge ? <em className={`org-scheme-status is-${accent}`}>{badge}</em> : null}
      </button>
      {node.managerName ? (
        <div className="org-scheme-manager is-compact">
          <strong>{node.managerName}</strong>
          <span>{managerRole(node)}</span>
        </div>
      ) : people.length === 0 && nested.length === 0 ? (
        <div className="org-scheme-manager is-compact">
          <span className="is-empty">{emptyAmir(node)}</span>
        </div>
      ) : null}
      {people.length > 0 ? <PersonGrid people={people} /> : null}
      {nested.length > 0 ? (
        <div className="org-scheme-subs is-nested">
          {nested.map((child) => (
            <SchemeSubNode
              key={child.id}
              node={child}
              selectedId={selectedId}
              onSelect={onSelect}
            />
          ))}
        </div>
      ) : null}
    </div>
  )
}

function PersonGrid({
  people,
}: {
  people: ChartPerson[]
}) {
  if (people.length === 0) return null

  return (
    <ul className="org-scheme-people">
      {people.map((p) => (
        <li key={p.id}>
          <Link to={`/employees/${p.id}`} className={`org-scheme-person tone-${p.roleTone || 'staff'}`}>
            <strong>{p.fullName}</strong>
            <span>{personRole(p)}</span>
            {isMemur(p) ? <em className="org-scheme-type">Memur</em> : null}
          </Link>
        </li>
      ))}
    </ul>
  )
}
