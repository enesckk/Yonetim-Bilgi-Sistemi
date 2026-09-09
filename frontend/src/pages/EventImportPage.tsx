import { useMemo, useRef, useState, type DragEvent, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import {
  downloadEventImportTemplate,
  importEventsFromCsv,
} from '@/api/eventsImportApi'
import type { EventImportResult } from '@/api/eventsApi'
import { ApiClientError } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { canImportEvents } from '@/auth/roles'
import { PageBackLink } from '@/components/PageBackLink'

const TEMPLATE_COLUMNS: { name: string; hint: string; required?: boolean }[] = [
  { name: 'Baslik', hint: 'Etkinlik adı', required: true },
  { name: 'Baslangic', hint: 'Tarih: 2026-03-12 10:00', required: true },
  { name: 'Bitis', hint: 'Opsiyonel bitiş' },
  { name: 'Durum', hint: 'Planlandı veya Yapıldı' },
  { name: 'Mahalle', hint: 'Mahalle adı (haritaya düşer)', required: true },
  { name: 'Kategori', hint: 'Eğitim, Sağlık, Spor…' },
  { name: 'Katilim', hint: 'Katılan kişi sayısı' },
  { name: 'Aciklama', hint: 'Opsiyonel' },
  { name: 'TesisKodu', hint: 'Varsa tesis kodu' },
  { name: 'Adres', hint: 'Serbest metin' },
]

function formatBytes(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function EventImportPage() {
  const { user, hasPermission } = useAuth()
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const canImport = canManage && canImportEvents(user)

  const [file, setFile] = useState<File | null>(null)
  const [busy, setBusy] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<EventImportResult | null>(null)
  const [dragOver, setDragOver] = useState(false)
  const [onlyErrors, setOnlyErrors] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  const visibleRows = useMemo(() => {
    if (!result) return []
    return onlyErrors ? result.rows.filter((r) => !r.success) : result.rows
  }, [result, onlyErrors])

  const successRate = useMemo(() => {
    if (!result || result.totalRows === 0) return 0
    return Math.round((result.successCount / result.totalRows) * 100)
  }, [result])

  if (!canImport) {
    return (
      <div className="org-page">
        <div className="panel">
          <h1>Excel ile etkinlik yükle</h1>
          <p className="muted">
            {canManage
              ? 'Bu ekran müdür hesabında kullanılmaz. Etkinliği takvimden tarih seçerek ekleyebilirsiniz.'
              : 'Bu ekranı kullanmak için etkinlik yönetme yetkisi gerekir.'}
          </p>
          <PageBackLink to={canManage ? '/events/calendar' : '/events/list'}>
            {canManage ? 'Takvim' : 'Etkinlikler'}
          </PageBackLink>
        </div>
      </div>
    )
  }

  function pickFile(f: File | null) {
    if (!f) return
    const name = f.name.toLowerCase()
    if (
      !name.endsWith('.xlsx') &&
      !name.endsWith('.csv') &&
      f.type !== 'text/csv' &&
      f.type !== 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    ) {
      setError('Excel (.xlsx) veya CSV yükleyin.')
      return
    }
    setError(null)
    setResult(null)
    setFile(f)
  }

  function onDrop(e: DragEvent<HTMLDivElement>) {
    e.preventDefault()
    setDragOver(false)
    pickFile(e.dataTransfer.files?.[0] ?? null)
  }

  async function onDownloadTemplate() {
    setDownloading(true)
    setError(null)
    try {
      await downloadEventImportTemplate()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Şablon indirilemedi.')
    } finally {
      setDownloading(false)
    }
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (!file) {
      setError('Bir Excel veya CSV dosyası seçin.')
      return
    }
    setBusy(true)
    setError(null)
    setResult(null)
    try {
      const res = await importEventsFromCsv(file)
      setResult(res)
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.validationErrors?.file?.join(' ') ?? err.message)
      } else {
        setError('Aktarım başarısız oldu.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="org-page import-page">
      <header className="org-hero panel">
        <div>
          <p className="org-eyebrow">Veri aktarımı</p>
          <h1>Excel ile etkinlik yükle</h1>
          <p className="muted">
            Şablonu indirip mahalle, tarih ve Planlandı/Yapıldı bilgilerini doldurun. Geçerli satırlar
            kaydedilir, hatalılar listelenir.
          </p>
        </div>
        <div className="report-hero-side">
          <button
            type="button"
            className="btn-secondary"
            onClick={() => void onDownloadTemplate()}
            disabled={downloading}
          >
            {downloading ? 'Hazırlanıyor…' : 'Şablonu indir (.xlsx)'}
          </button>
          <Link to="/events/list" className="btn-secondary">
            Etkinlikler
          </Link>
        </div>
      </header>

      <section className="panel import-steps-card">
        <ol className="import-flow">
          <li>
            <span className="import-flow-no">1</span>
            <div>
              <strong>Şablonu indirin</strong>
              <p className="muted small">Excel şablonunda örnek satır hazır gelir.</p>
            </div>
          </li>
          <li>
            <span className="import-flow-no">2</span>
            <div>
              <strong>Satırları doldurun</strong>
              <p className="muted small">Mahalle adı haritadaki yerleşimle eşleşmelidir. Durum: Planlandı veya Yapıldı.</p>
            </div>
          </li>
          <li>
            <span className="import-flow-no">3</span>
            <div>
              <strong>Yükleyin ve kontrol edin</strong>
              <p className="muted small">Sonuçta her satırın durumunu görürsünüz.</p>
            </div>
          </li>
        </ol>
      </section>

      <section className="panel import-upload-card">
        <form onSubmit={(e) => void onSubmit(e)}>
          <div
            className={`import-dropzone${dragOver ? ' is-drag' : ''}${file ? ' has-file' : ''}`}
            onDragOver={(e) => {
              e.preventDefault()
              setDragOver(true)
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={onDrop}
            onClick={() => inputRef.current?.click()}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                inputRef.current?.click()
              }
            }}
          >
            <input
              ref={inputRef}
              type="file"
              accept=".xlsx,.csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv"
              hidden
              onChange={(e) => pickFile(e.target.files?.[0] ?? null)}
            />
            <svg className="import-dropzone-icon" viewBox="0 0 24 24" fill="none" aria-hidden>
              <path
                d="M12 16V4m0 0L8 8m4-4 4 4M4 16v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2"
                stroke="currentColor"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
            {file ? (
              <div className="import-file-chip">
                <span className="import-file-name">{file.name}</span>
                <span className="muted small">{formatBytes(file.size)}</span>
                <button
                  type="button"
                  className="import-file-clear"
                  onClick={(e) => {
                    e.stopPropagation()
                    setFile(null)
                    setResult(null)
                    if (inputRef.current) inputRef.current.value = ''
                  }}
                >
                  Kaldır
                </button>
              </div>
            ) : (
              <>
                <p className="import-dropzone-title">
                  Dosyayı buraya sürükleyin veya seçmek için tıklayın
                </p>
                <p className="muted small">Excel (.xlsx) veya CSV</p>
              </>
            )}
          </div>

          {error ? (
            <div className="form-error import-error" role="alert">
              {error}
            </div>
          ) : null}

          <div className="import-upload-actions">
            <button type="submit" className="btn-primary" disabled={busy || !file}>
              {busy ? 'Aktarılıyor…' : 'Aktarımı başlat'}
            </button>
          </div>
        </form>
      </section>

      <section className="panel import-columns-card">
        <div className="dq-section-head">
          <h2>Şablon sütunları</h2>
          <p className="muted small">Beklenen alanlar (şablondaki başlıklar geçerli kaynaktır)</p>
        </div>
        <div className="import-columns-grid">
          {TEMPLATE_COLUMNS.map((c) => (
            <div key={c.name} className="import-column">
              <span className="import-column-name">
                {c.name}
                {c.required ? <span className="import-column-req">•</span> : null}
              </span>
              <span className="muted small">{c.hint}</span>
            </div>
          ))}
        </div>
      </section>

      {result ? (
        <section className="panel import-result-card">
          <div className="import-result-head">
            <div>
              <h2>Aktarım sonucu</h2>
              <p className="muted small">
                {result.failureCount === 0
                  ? 'Tüm satırlar başarıyla kaydedildi.'
                  : 'Hatalı satırları düzeltip yeniden yükleyebilirsiniz.'}
              </p>
            </div>
            <div className="import-result-stats">
              <div className="import-stat">
                <strong>{result.totalRows}</strong>
                <span>Toplam satır</span>
              </div>
              <div className="import-stat is-ok">
                <strong>{result.successCount}</strong>
                <span>Başarılı</span>
              </div>
              <div className="import-stat is-fail">
                <strong>{result.failureCount}</strong>
                <span>Hatalı</span>
              </div>
            </div>
          </div>

          <div className="import-progress" aria-hidden>
            <div className="import-progress-bar" style={{ width: `${successRate}%` }} />
          </div>

          <div className="import-result-toolbar">
            <span className="muted small">%{successRate} başarı oranı</span>
            {result.failureCount > 0 ? (
              <label className="import-only-errors">
                <input
                  type="checkbox"
                  checked={onlyErrors}
                  onChange={(e) => setOnlyErrors(e.target.checked)}
                />
                <span>Yalnızca hatalı satırlar</span>
              </label>
            ) : null}
          </div>

          <div className="import-result-table-wrap">
            <table className="import-result-table">
              <thead>
                <tr>
                  <th>Satır</th>
                  <th>Başlık</th>
                  <th>Durum</th>
                  <th>Detay</th>
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((r) => (
                  <tr key={r.rowNumber} className={r.success ? undefined : 'is-error'}>
                    <td className="import-row-no">{r.rowNumber}</td>
                    <td>
                      {r.title || '—'}
                      {r.eventId ? (
                        <>
                          {' '}
                          <Link to={`/events/${r.eventId}`} className="import-row-link">
                            aç
                          </Link>
                        </>
                      ) : null}
                    </td>
                    <td>
                      <span className={`import-pill ${r.success ? 'is-ok' : 'is-fail'}`}>
                        {r.success ? 'Kaydedildi' : 'Hata'}
                      </span>
                    </td>
                    <td className="import-row-detail">
                      {r.success ? (
                        <span className="muted small">Sisteme eklendi</span>
                      ) : (
                        <ul className="import-error-list">
                          {r.errors.map((msg, i) => (
                            <li key={i}>{msg}</li>
                          ))}
                        </ul>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}
    </div>
  )
}
