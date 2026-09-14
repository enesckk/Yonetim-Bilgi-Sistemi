import { Link } from 'react-router-dom'
import type { OrgNode } from '@/api/organizationApi'
import { buildOrgSheet, DUTY_GROUPS, type SheetPerson, type SheetUnit } from './orgSheetModel'

type Props = {
  tree: OrgNode[]
  selectedId: string | null
  onSelect: (id: string) => void
}

const REFERENCE_COLUMNS = [
  ['GK_AKTOPRAK', 'GK_ALINACAR', 'GK_BASAK'],
  ['GK_BELKIS', 'GK_EYUPSULTAN', 'GK_KUZEYSEHIR'],
  ['GK_MAE', 'GK_MERVE', 'GK_MUTERIM'],
  ['GK_NURTEPE', 'GK_OKTAY', 'GK_PIRSULTAN', 'GK_ZEYTINLI'],
]

function personCard(person: SheetPerson) {
  return (
    <Link key={person.id} to={`/employees/${person.id}`} className={`org-sheet-person cat-${person.category}`}>
      <strong>{person.name}</strong>
      <span>{person.role}</span>
      {person.isAreaManager && <em title="Birim veya tesis amiri">A</em>}
    </Link>
  )
}

function peopleGrid(people: SheetPerson[]) {
  return people.length > 0 ? <div className="org-sheet-people">{people.map(personCard)}</div> : null
}

function childUnit(unit: SheetUnit, selectedId: string | null, onSelect: (id: string) => void) {
  return (
    <section key={unit.node.id} className="org-sheet-child">
      <button
        type="button"
        className={`org-sheet-child-head${selectedId === unit.node.id ? ' is-selected' : ''}`}
        onClick={() => onSelect(unit.node.id)}
      >
        <span>{unit.node.name}</span>
        <b>{unit.count}</b>
      </button>
      {peopleGrid(unit.people)}
      {unit.children.map((child) => childUnit(child, selectedId, onSelect))}
    </section>
  )
}

function unitCard(unit: SheetUnit, selectedId: string | null, onSelect: (id: string) => void) {
  return (
    <article key={unit.node.id} className="org-sheet-unit">
      <button
        type="button"
        className={`org-sheet-unit-head${selectedId === unit.node.id ? ' is-selected' : ''}`}
        onClick={() => onSelect(unit.node.id)}
      >
        <span>{unit.node.name}</span>
        <b>{unit.count}</b>
      </button>
      <div className="org-sheet-unit-body">
        {peopleGrid(unit.people)}
        {unit.children.map((child) => childUnit(child, selectedId, onSelect))}
        {unit.people.length === 0 && unit.children.length === 0 && (
          <p className="org-sheet-empty">Bu birimde gösterilecek aktif personel yok.</p>
        )}
      </div>
    </article>
  )
}

function youthColumns(children: SheetUnit[]): SheetUnit[][] {
  const columns: SheetUnit[][] = [[], [], [], []]
  for (const child of children) {
    const code = (child.node.code ?? '').toUpperCase()
    const fixed = REFERENCE_COLUMNS.findIndex((list) => list.includes(code))
    const index = fixed >= 0
      ? fixed
      : columns.reduce((best, column, i) => {
          const weight = column.reduce((sum, unit) => sum + Math.ceil(unit.count / 2) + 1, 0)
          const bestWeight = columns[best].reduce((sum, unit) => sum + Math.ceil(unit.count / 2) + 1, 0)
          return weight < bestWeight ? i : best
        }, 0)
    columns[index].push(child)
  }
  return columns
}

export function OrgHierarchyContent({ tree, selectedId, onSelect }: Props) {
  const model = buildOrgSheet(tree)
  if (!model) {
    return (
      <div className="org-scheme-empty">
        <p>Organizasyon şeması için henüz birim yok.</p>
        <p className="muted small">Veriler eklendikçe şema otomatik oluşur.</p>
      </div>
    )
  }

  const { leaders, directorateStaff, units, youth } = model
  const unitCount = units.length + (youth ? 1 : 0)
  const columns = youth ? youthColumns(youth.children) : []
  return (
    <div className="org-sheet" id="organization-scheme-export">
      <header
        className={`org-sheet-title${selectedId === model.titleNode.id ? ' is-selected' : ''}`}
      >
        <button type="button" onClick={() => onSelect(model.titleNode.id)}>
          {model.titleNode.name.toLocaleUpperCase('tr-TR')}
        </button>
      </header>

      <div className="org-sheet-chain" aria-label="Yönetim zinciri">
        {leaders.length ? leaders.map((leader) => (
          <button
            type="button"
            key={leader.person.id}
            className={`org-sheet-leader${leader.label === 'MÜDÜR YARDIMCISI' ? ' is-assistant' : ''}${selectedId === leader.nodeId ? ' is-selected' : ''}`}
            onClick={() => onSelect(leader.nodeId)}
          >
            <small>{leader.label}</small>
            <strong>{leader.person.name}</strong>
          </button>
        )) : <p className="org-sheet-empty">Yönetim personeli henüz atanmadı.</p>}
      </div>

      {directorateStaff.length > 0 && (
        <div className="org-sheet-directorate-staff">
          <strong>MÜDÜRLÜK PERSONELİ</strong>
          {peopleGrid(directorateStaff)}
        </div>
      )}

      <div className="org-sheet-branch">
        <strong>MÜDÜR YARDIMCISINA BAĞLI BİRİMLER</strong>
        <span>{unitCount} ALAN</span>
      </div>

      <div className="org-sheet-unit-grid">
        {units.map((unit) => unitCard(unit, selectedId, onSelect))}
      </div>

      {youth && (
        <article className="org-sheet-youth">
          <button
            type="button"
            className={`org-sheet-unit-head${selectedId === youth.node.id ? ' is-selected' : ''}`}
            onClick={() => onSelect(youth.node.id)}
          >
            <span>{youth.node.name} · {youth.children.length} BAĞLI BİRİM</span>
            <b>{youth.count}</b>
          </button>
          <div className="org-sheet-youth-grid">
            {columns.map((column, index) => (
              <div key={index} className="org-sheet-youth-column">
                {index === 0 && youth.people.length > 0 && (
                  <section className="org-sheet-child">
                    <div className="org-sheet-child-head is-static">
                      <span>Koordinasyon</span><b>{youth.people.length}</b>
                    </div>
                    {peopleGrid(youth.people)}
                  </section>
                )}
                {column.map((child) => childUnit(child, selectedId, onSelect))}
              </div>
            ))}
          </div>
        </article>
      )}

      <footer className="org-sheet-footer">
        <section className="org-sheet-legend" aria-label="Görev renkleri">
          <h3>GÖREV RENKLERİ</h3>
          <p>Personel kartının zemini ve sol şeridi görev grubunu gösterir.</p>
          <ul>
            {DUTY_GROUPS.map((group) => (
              <li key={group.category}>
                <i className={`org-sheet-swatch cat-${group.category}`} />{group.label}
              </li>
            ))}
          </ul>
          <p className="org-sheet-amir-note"><b>A</b> Birim / tesis amiri. Bu işaret görev grubuna ek bilgidir.</p>
        </section>
        <section className="org-sheet-summary" aria-label="Genel personel bilgileri">
          <h3>GENEL PERSONEL BİLGİLERİ</h3>
          <div className="org-sheet-keyfigures">
            <strong>TOPLAM {model.total}</strong>
            <strong>{unitCount} ALAN</strong>
            <strong>{model.areaManagerCount} ALAN AMİRİ</strong>
          </div>
          <ul>
            {DUTY_GROUPS.map((group) => (
              <li key={group.category}>
                <i className={`org-sheet-swatch cat-${group.category}`} />
                <span>{group.label}</span>
                <b>{model.categoryCounts.get(group.category) ?? 0}</b>
              </li>
            ))}
          </ul>
          <small>Aktif personel · Güncel organizasyon verisi</small>
        </section>
      </footer>
    </div>
  )
}
