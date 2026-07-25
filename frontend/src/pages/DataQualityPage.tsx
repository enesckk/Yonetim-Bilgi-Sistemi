import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  fetchDataQualityReport,
  type DataQualityEmployee,
  type DataQualityIssueStat,
  type DataQualityKind,
  type DataQualityReport,
} from '@/api/dataQualityApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'

type DqView = 'overview' | 'missing' | 'inconsistency' | 'list'

function scoreTone(percent: number) {
  if (percent >= 80) return 'is-ok'
  if (percent >= 60) return 'is-good'
  if (percent >= 40) return 'is-warn'
  return 'is-low'
}

function initials(fullName: string) {
  return fullName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toLocaleUpperCase('tr-TR') ?? '')
    .join('')
}

function bandTone(key: string) {
  if (key === 'full') return 'ok'
  if (key === 'high') return 'good'
  if (key === 'mid') return 'warn'
  if (key === 'low') return 'alert'
  return 'danger'
}

export function DataQualityPage() {
  const { hasPermission } = useAuth()
  const canView = hasPermission(PermissionCodes.DataQualityView)

  const [report, setReport] = useState<DataQualityReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [view, setView] = useState<DqView>('overview')
  const [issueCode, setIssueCode] = useState('')
  const [kind, setKind] = useState<DataQualityKind | ''>('')
  const [completionBand, setCompletionBand] = useState('')
  const [allStatuses, setAllStatuses] = useState(false)
  const [search, setSearch] = useState('')

  const load = useCallback(async () => {
    if (!canView) return
    setLoading(true)
    setError(null)
    try {
      setReport(
        await fetchDataQualityReport({
          issueCode: issueCode || undefined,
          kind: kind || undefined,
          completionBand: completionBand || undefined,
          allStatuses,
        }),
      )
    } catch (err) {
      setReport(null)
      setError(err instanceof ApiClientError ? err.message : 'Rapor yüklenemedi.')
    } finally {
      setLoading(false)
    }
  }, [allStatuses, canView, completionBand, issueCode, kind])

  useEffect(() => {
    void load()
  }, [load])

  const missingStats = useMemo(
    () => (report?.issueStats ?? []).filter((s) => s.kind === 'missing'),
    [report],
  )
  const inconsistencyStats = useMemo(
    () => (report?.issueStats ?? []).filter((s) => s.kind === 'inconsistency'),
    [report],
  )

  const filteredEmployees = useMemo(() => {
    const list = report?.employees ?? []
    const q = search.trim().toLowerCase()
    if (!q) return list
    return list.filter(
      (e) =>
        e.fullName.toLowerCase().includes(q) ||
        (e.employeeNumber ?? '').toLowerCase().includes(q) ||
        (e.unitName ?? '').toLowerCase().includes(q),
    )
  }, [report, search])

  const selectIssue = (stat: DataQualityIssueStat) => {
    const next = issueCode === stat.code ? '' : stat.code
    setIssueCode(next)
    setKind(next ? stat.kind : '')
    setView('list')
  }

  const selectBand = (key: string) => {
    setCompletionBand((prev) => (prev === key ? '' : key))
    setView('list')
  }

  const clearFilters = () => {
    setIssueCode('')
    setKind('')
    setCompletionBand('')
    setSearch('')
  }

  if (!canView) {
    return (
      <div className="panel">
        <p className="form-error">
          Veri kalitesi için <code>DataQuality.View</code> gerekir.
        </p>
      </div>
    )
  }

  return (
    <div className="org-page dq-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Veri tamamlama</p>
          <h1>Veri eksikleri</h1>
          <p className="muted">
            Kayıt kalitesini izleyin, eksikleri ve tutarsızlıkları personel kartından düzeltin.
          </p>
        </div>
        <div className="report-hero-side">
          {report && (
            <div className="report-hero-stats">
              <div>
                <strong>{report.scopedEmployeeCount}</strong>
                <span>Personel</span>
              </div>
              <div>
                <strong className={report.employeesWithIssues > 0 ? 'is-warn' : undefined}>
                  {report.employeesWithIssues}
                </strong>
                <span>Sorunlu</span>
              </div>
              <div>
                <strong className={report.inconsistencyEmployeeCount > 0 ? 'is-danger' : undefined}>
                  {report.inconsistencyEmployeeCount}
                </strong>
                <span>Tutarsız</span>
              </div>
              <div>
                <strong>%{report.averageCompletionPercent}</strong>
                <span>Ort. skor</span>
              </div>
            </div>
          )}
          <button type="button" className="btn-secondary" onClick={() => void load()}>
            Yenile
          </button>
        </div>
      </header>

      <div className="org-toolbar panel">
        <div className="org-views" role="tablist" aria-label="Veri kalitesi görünümleri">
          {(
            [
              ['overview', 'Özet'],
              ['missing', 'Eksik alanlar'],
              ['inconsistency', 'Tutarsızlıklar'],
              ['list', 'Personel listesi'],
            ] as const
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={view === id}
              className={view === id ? 'is-active' : undefined}
              onClick={() => setView(id)}
            >
              {label}
            </button>
          ))}
        </div>
        <div className="dq-toolbar-right">
          <label className="checkbox-row">
            <input
              type="checkbox"
              checked={allStatuses}
              onChange={(e) => setAllStatuses(e.target.checked)}
            />
            Tüm durumlar
          </label>
          {(issueCode || kind || completionBand) && (
            <button type="button" className="skills-link-btn" onClick={clearFilters}>
              Filtreleri temizle
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="panel">
          <div className="form-error">{error}</div>
        </div>
      )}

      {loading || !report ? (
        <div className="panel">
          <p className="muted">Yükleniyor…</p>
        </div>
      ) : (
        <>
          {view === 'overview' && (
            <OverviewPanel
              report={report}
              issueCode={issueCode}
              completionBand={completionBand}
              onSelectIssue={selectIssue}
              onSelectBand={selectBand}
              onOpenList={(k) => {
                setKind(k)
                setIssueCode('')
                setView('list')
              }}
            />
          )}

          {view === 'missing' && (
            <IssueCardsPanel
              title="Eksik alan uyarıları"
              description="Şu personellerde zorunlu veya operasyonel alanlar boş."
              stats={missingStats}
              emptyText="Kapsamdaki personelde eksik alan bulunamadı."
              activeCode={issueCode}
              onSelect={selectIssue}
            />
          )}

          {view === 'inconsistency' && (
            <IssueCardsPanel
              title="Veri tutarsızlıkları"
              description="Çakışan, çelişen veya mantıksız kayıtlar."
              stats={inconsistencyStats}
              emptyText="Kapsamdaki personelde tutarsızlık bulunamadı."
              activeCode={issueCode}
              onSelect={selectIssue}
              danger
            />
          )}

          {view === 'list' && (
            <EmployeeListPanel
              employees={filteredEmployees}
              search={search}
              onSearch={setSearch}
              issueCode={issueCode}
              kind={kind}
              completionBand={completionBand}
              bands={report.completionBands}
              rules={report.rules}
              onIssueCode={setIssueCode}
              onKind={setKind}
              onBand={setCompletionBand}
            />
          )}
        </>
      )}
    </div>
  )
}

function OverviewPanel({
  report,
  issueCode,
  completionBand,
  onSelectIssue,
  onSelectBand,
  onOpenList,
}: {
  report: DataQualityReport
  issueCode: string
  completionBand: string
  onSelectIssue: (s: DataQualityIssueStat) => void
  onSelectBand: (key: string) => void
  onOpenList: (kind: DataQualityKind | '') => void
}) {
  const topMissing = report.issueStats.filter((s) => s.kind === 'missing').slice(0, 6)
  const topInconsist = report.issueStats.filter((s) => s.kind === 'inconsistency').slice(0, 6)
  const maxBand = Math.max(1, ...report.completionBands.map((b) => b.count))

  return (
    <>
      <section className="panel dq-section">
        <div className="dq-section-head">
          <div>
            <h2>Tamamlanma dağılımı</h2>
            <p className="muted small">
              Her personelin bilgi tamamlama yüzdesi. Bantlara tıklayarak listeyi daraltın.
            </p>
          </div>
        </div>
        <div className="dq-completion-layout">
          <div className="dq-completion-summary">
            <span>Kurumsal veri tamamlama skoru</span>
            <strong>%{report.averageCompletionPercent}</strong>
            <div
              className="dq-completion-progress"
              role="progressbar"
              aria-label="Ortalama veri tamamlama skoru"
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={report.averageCompletionPercent}
            >
              <i style={{ width: `${report.averageCompletionPercent}%` }} />
            </div>
            <small>
              {report.scopedEmployeeCount} personelin zorunlu ve operasyonel alanları üzerinden
              hesaplandı.
            </small>
          </div>
          <div className="dq-band-list">
            {report.completionBands.map((b) => (
              <button
                key={b.key}
                type="button"
                className={`dq-band-row dq-band-${bandTone(b.key)}${
                  completionBand === b.key ? ' active' : ''
                }`}
                onClick={() => onSelectBand(b.key)}
              >
                <span className="dq-band-range">
                  %{b.minPercent === b.maxPercent ? b.minPercent : `${b.minPercent}–${b.maxPercent}`}
                </span>
                <span className="dq-band-row-label">{b.label.replace(/^%[\d–-]+\s*—\s*/, '')}</span>
                <span className="dq-band-mini">
                  <i style={{ width: `${Math.round((b.count / maxBand) * 100)}%` }} />
                </span>
                <strong>{b.count}</strong>
                <span className="dq-row-arrow" aria-hidden>→</span>
              </button>
            ))}
          </div>
        </div>
      </section>

      <div className="dq-overview-grid">
        <section className="panel dq-section">
          <div className="dq-section-head">
            <div>
              <h2>Eksik alan uyarıları</h2>
              <p className="muted small">
                {report.missingIssueEmployeeCount} personelde en az bir eksik alan var.
              </p>
            </div>
            <button type="button" className="skills-link-btn" onClick={() => onOpenList('missing')}>
              Tümünü gör
            </button>
          </div>
          {topMissing.length === 0 ? (
            <p className="muted">Eksik alan uyarısı yok.</p>
          ) : (
            <ul className="dq-alert-list">
              {topMissing.map((s) => (
                <li key={s.code}>
                  <button
                    type="button"
                    className={issueCode === s.code ? 'active' : undefined}
                    onClick={() => onSelectIssue(s)}
                  >
                    <strong>{s.count}</strong>
                    <span>personelde: {s.label.toLowerCase()}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="panel dq-section">
          <div className="dq-section-head">
            <div>
              <h2>Tutarsızlık uyarıları</h2>
              <p className="muted small">
                {report.inconsistencyEmployeeCount} personelde tutarsız kayıt tespit edildi.
              </p>
            </div>
            <button
              type="button"
              className="skills-link-btn"
              onClick={() => onOpenList('inconsistency')}
            >
              Tümünü gör
            </button>
          </div>
          {topInconsist.length === 0 ? (
            <p className="muted">Tutarsızlık uyarısı yok.</p>
          ) : (
            <ul className="dq-alert-list dq-alert-danger">
              {topInconsist.map((s) => (
                <li key={s.code}>
                  <button
                    type="button"
                    className={issueCode === s.code ? 'active' : undefined}
                    onClick={() => onSelectIssue(s)}
                  >
                    <strong>{s.count}</strong>
                    <span>{s.label}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </>
  )
}

function IssueCardsPanel({
  title,
  description,
  stats,
  emptyText,
  activeCode,
  onSelect,
  danger,
}: {
  title: string
  description: string
  stats: DataQualityIssueStat[]
  emptyText: string
  activeCode: string
  onSelect: (s: DataQualityIssueStat) => void
  danger?: boolean
}) {
  return (
    <section className="panel dq-section">
      <div className="dq-section-head">
        <div>
          <h2>{title}</h2>
          <p className="muted small">{description}</p>
        </div>
      </div>
      {stats.length === 0 ? (
        <p className="muted">{emptyText}</p>
      ) : (
        <div className={`dq-issue-list${danger ? ' is-danger' : ''}`}>
          {stats.map((s) => (
            <button
              key={s.code}
              type="button"
              className={`dq-issue-row${activeCode === s.code ? ' active' : ''}`}
              onClick={() => onSelect(s)}
            >
              <span className="dq-issue-count">{s.count}</span>
              <span className="dq-issue-row-copy">
                <strong>{s.label}</strong>
                <small>{danger ? 'Kontrol edilmesi gereken tutarsız kayıt' : 'Tamamlanması gereken personel bilgisi'}</small>
              </span>
              <span className="dq-issue-person">{s.count} personel</span>
              <span className="dq-row-arrow" aria-hidden>→</span>
            </button>
          ))}
        </div>
      )}
    </section>
  )
}

function EmployeeListPanel({
  employees,
  search,
  onSearch,
  issueCode,
  kind,
  completionBand,
  bands,
  rules,
  onIssueCode,
  onKind,
  onBand,
}: {
  employees: DataQualityEmployee[]
  search: string
  onSearch: (v: string) => void
  issueCode: string
  kind: DataQualityKind | ''
  completionBand: string
  bands: DataQualityReport['completionBands']
  rules: DataQualityReport['rules']
  onIssueCode: (v: string) => void
  onKind: (v: DataQualityKind | '') => void
  onBand: (v: string) => void
}) {
  const rulesForSelect = kind
    ? rules.filter((r) => r.kind === kind)
    : rules

  return (
    <section className="panel dq-section">
      <div className="dq-section-head">
        <div>
          <h2>Personel listesi</h2>
          <p className="muted small">
            {employees.length} kayıt gösteriliyor. Ada tıklayarak personel kartına gidin.
          </p>
        </div>
      </div>

      <div className="dq-list-filters">
        <label className="dq-filter-search">
          <span>Ara</span>
          <input
            type="search"
            value={search}
            onChange={(e) => onSearch(e.target.value)}
            placeholder="Ad, sicil, birim…"
          />
        </label>
        <label>
          <span>Tür</span>
          <select
            value={kind}
            onChange={(e) => {
              onKind(e.target.value as DataQualityKind | '')
              onIssueCode('')
            }}
          >
            <option value="">Tümü</option>
            <option value="missing">Eksik alan</option>
            <option value="inconsistency">Tutarsızlık</option>
          </select>
        </label>
        <label>
          <span>Konu</span>
          <select value={issueCode} onChange={(e) => onIssueCode(e.target.value)}>
            <option value="">Tümü</option>
            {rulesForSelect.map((r) => (
              <option key={r.code} value={r.code}>
                {r.label}
              </option>
            ))}
          </select>
        </label>
        <label>
          <span>Tamamlanma</span>
          <select value={completionBand} onChange={(e) => onBand(e.target.value)}>
            <option value="">Tümü</option>
            {bands.map((b) => (
              <option key={b.key} value={b.key}>
                {b.label} ({b.count})
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="dq-table-wrap">
        <table className="dq-table">
          <thead>
            <tr>
              <th>Personel</th>
              <th>Birim / Tesis</th>
              <th className="dq-th-score">Tamamlanma</th>
              <th>Tespit edilen sorunlar</th>
              <th aria-label="İşlem" />
            </tr>
          </thead>
          <tbody>
            {employees.length === 0 ? (
              <tr>
                <td colSpan={5} className="dq-empty">
                  Filtreye uyan kayıt yok.
                </td>
              </tr>
            ) : (
              employees.map((e) => (
                <tr key={e.id}>
                  <td>
                    <div className="dq-emp">
                      <span className="dq-emp-avatar" aria-hidden>
                        {initials(e.fullName)}
                      </span>
                      <span className="dq-emp-copy">
                        <Link to={`/employees/${e.id}`} className="dq-emp-link">
                          {e.fullName}
                        </Link>
                        <small>{e.employeeNumber ?? 'Sicil yok'}</small>
                      </span>
                    </div>
                  </td>
                  <td>
                    <div className="dq-place">
                      <span>{e.unitName ?? 'Birim atanmamış'}</span>
                      <small className={e.facilityName ? undefined : 'is-missing'}>
                        {e.facilityName ?? 'Tesis yok'}
                      </small>
                    </div>
                  </td>
                  <td>
                    <div className={`dq-score-cell ${scoreTone(e.profileCompletionPercent)}`}>
                      <span>%{e.profileCompletionPercent}</span>
                      <i>
                        <b style={{ width: `${e.profileCompletionPercent}%` }} />
                      </i>
                    </div>
                  </td>
                  <td>
                    <div className="dq-issue-summary">
                      {e.missingCodes.length > 0 && (
                        <span className="dq-count-pill">{e.missingCodes.length} eksik</span>
                      )}
                      {e.inconsistencyCodes.length > 0 && (
                        <span className="dq-count-pill is-danger">
                          {e.inconsistencyCodes.length} tutarsızlık
                        </span>
                      )}
                    </div>
                    <div className="dq-tag-row">
                      {e.issueLabels.slice(0, 3).map((label, i) => {
                        const code = e.issueCodes[i]
                        const isInconsist = e.inconsistencyCodes.includes(code)
                        return (
                          <span
                            key={`${e.id}-${code}`}
                            className={`dq-tag${isInconsist ? ' is-danger' : ''}`}
                          >
                            {label}
                          </span>
                        )
                      })}
                      {e.issueLabels.length > 3 && (
                        <span
                          className="dq-tag is-more"
                          title={e.issueLabels.slice(3).join(', ')}
                        >
                          +{e.issueLabels.length - 3}
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="dq-row-actions">
                    <Link to={`/employees/${e.id}`} className="dq-action">
                      Kart
                    </Link>
                    <Link to={`/employees/${e.id}/edit`} className="dq-action is-primary">
                      Tamamla
                    </Link>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  )
}
