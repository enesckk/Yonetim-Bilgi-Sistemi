import { Link } from 'react-router-dom'
import type { ChartPerson, OrgNode } from '@/api/organizationApi'

const TYPE = {
  Municipality: 1,
  DeputyPresidency: 2,
  Directorate: 3,
  MainUnit: 4,
  SubUnit: 5,
  Facility: 6,
} as const

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

function personRole(p: ChartPerson): string {
  return p.dutyName || p.jobTitleName || 'Görev tanımlanmamış'
}

function managerRole(node: OrgNode): string {
  // Fiili görev varsa onu göster; "Birim/tesis sorumlusu" gibi genel etiket kullanma.
  if (node.managerDutyName) return node.managerDutyName
  if (node.type === TYPE.DeputyPresidency) return 'Başkan Yardımcısı'
  return node.typeLabel
}

/** Referans müdürlük şeması: yönetim omurgası + birim kolonları + personel kartları */
export function OrgSchemeChart({ tree, selectedId, onSelect }: Props) {
  if (tree.length === 0) {
    return (
      <div className="org-scheme-empty">
        <p>Organizasyon şeması için henüz birim yok.</p>
        <p className="muted small">Veriler eklendikçe şema otomatik oluşur.</p>
      </div>
    )
  }

  const directorate =
    findFirst(tree, TYPE.Directorate) ??
    findFirst(tree, TYPE.DeputyPresidency) ??
    findFirst(tree, TYPE.Municipality) ??
    tree[0]

  const leadership: OrgNode[] = []
  const deputy = findFirst(tree, TYPE.DeputyPresidency)
  if (deputy) leadership.push(deputy)
  if (directorate && directorate.type === TYPE.Directorate) leadership.push(directorate)

  // Müdür yardımcısı / yönetici katmanı: müdürlük altındaki özel düğümler yoksa manager satırı
  const directorateChildren = directorate?.children ?? []
  const columns = directorateChildren.filter(
    (c) => c.type === TYPE.MainUnit || c.type === TYPE.Facility || c.type === TYPE.SubUnit,
  )
  const fallbackColumns =
    columns.length > 0
      ? columns
      : collectByType(tree, TYPE.MainUnit).concat(collectByType(tree, TYPE.Facility))

  const titleNode = directorate

  return (
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
          {leadership.length === 0 && (
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
  const subUnits = (unit.children ?? []).filter(
    (c) => c.type === TYPE.SubUnit || c.type === TYPE.Facility || c.type === TYPE.MainUnit,
  )
  const hasSubs = subUnits.length > 0
  const directPeople = unit.chartPersonnel ?? []

  return (
    <article
      className={`org-scheme-col${selectedId === unit.id ? ' is-selected' : ''}`}
    >
      <button
        type="button"
        className="org-scheme-col-head"
        onClick={() => onSelect(unit.id)}
      >
        {unit.name}
      </button>

      <div className="org-scheme-manager">
        {unit.managerName ? (
          <>
            <strong>{unit.managerName}</strong>
            <span>{managerRole(unit)}</span>
          </>
        ) : (
          <span className="is-empty">Sorumlu atanmamış</span>
        )}
      </div>

      {/* Alt birim/tesis olsa bile bu birime doğrudan bağlı personel burada kalsın */}
      {(directPeople.length > 0 || !hasSubs) && (
        <PersonGrid people={directPeople} emptyHint="Personel eklenecek" />
      )}

      {hasSubs ? (
        <div className={`org-scheme-subs${subUnits.length > 4 ? ' is-wide' : ''}`}>
          {subUnits.map((sub) => (
            <SchemeSubNode key={sub.id} node={sub} selectedId={selectedId} onSelect={onSelect} />
          ))}
        </div>
      ) : null}

      {!hasSubs &&
        directPeople.length === 0 &&
        (unit.activeEmployeeCount ?? unit.employeeCount ?? 0) === 0 && (
        <p className="org-scheme-col-foot muted small">Kadro verisi sonradan bağlanacak</p>
      )}
    </article>
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
  const nested = (node.children ?? []).filter(
    (c) => c.type === TYPE.SubUnit || c.type === TYPE.Facility || c.type === TYPE.MainUnit,
  )

  return (
    <div className={`org-scheme-sub${selectedId === node.id ? ' is-selected' : ''}`}>
      <button type="button" className="org-scheme-sub-head" onClick={() => onSelect(node.id)}>
        {node.name}
      </button>
      {node.managerName ? (
        <div className="org-scheme-manager is-compact">
          <strong>{node.managerName}</strong>
          <span>{managerRole(node)}</span>
        </div>
      ) : null}
      <PersonGrid people={node.chartPersonnel ?? []} emptyHint="Personel eklenecek" />
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
  emptyHint,
}: {
  people: ChartPerson[]
  emptyHint: string
}) {
  if (people.length === 0) {
    return <p className="org-scheme-people-empty muted small">{emptyHint}</p>
  }

  return (
    <ul className="org-scheme-people">
      {people.map((p) => (
        <li key={p.id}>
          <Link to={`/employees/${p.id}`} className="org-scheme-person">
            <strong>{p.fullName}</strong>
            <span>{personRole(p)}</span>
          </Link>
        </li>
      ))}
    </ul>
  )
}
