import { ApiClientError } from '@/api/client'
import { useRef, useState } from 'react'
import { downloadParticipants, downloadParticipantsTemplate, previewEventParticipants, type ParticipantPreview } from '@/api/eventsApi'
import { useConfirm } from '@/components/ConfirmDialog'

export function EventParticipants({ names, onChange, onBusyChange, eventId, disabled = false }: {
  names: string[]
  onChange?: (names: string[]) => void
  onBusyChange?: (busy: boolean) => void
  eventId?: string
  disabled?: boolean
}) {
  const [preview, setPreview] = useState<ParticipantPreview | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showAll, setShowAll] = useState(false)
  const input = useRef<HTMLInputElement>(null)
  const confirm = useConfirm()

  async function upload(file?: File) {
    if (!file) return
    setError(null)
    if (!file.name.toLowerCase().endsWith('.xlsx') || file.size === 0 || file.size > 10 * 1024 * 1024) {
      setError('Boş olmayan, en fazla 10 MB büyüklüğünde bir .xlsx dosyası seçin.')
      if (input.current) input.current.value = ''
      return
    }
    setBusy(true)
    onBusyChange?.(true)
    try { setPreview(await previewEventParticipants(file)) }
    catch (err) {
      setError(err instanceof ApiClientError ? [...new Set(Object.values(err.validationErrors ?? {}).flat())].join(' ') || err.message : err instanceof Error ? err.message : 'Liste okunamadı.')
      onBusyChange?.(false)
    } finally {
      setBusy(false)
      if (input.current) input.current.value = ''
    }
  }

  async function download(template: boolean) {
    setError(null)
    try {
      if (template) await downloadParticipantsTemplate()
      else if (eventId) await downloadParticipants(eventId)
    } catch (err) { setError(err instanceof Error ? err.message : 'Dosya indirilemedi.') }
  }

  async function clear() {
    if (await confirm({ title: 'Katılımcı listesini kaldır', message: 'İsim listesi formdan kaldırılacak. Değişiklik Kaydet düğmesine bastığında uygulanır.', confirmLabel: 'Listeden kaldır', tone: 'danger' })) onChange?.([])
  }

  const visible = preview?.names ?? names
  return (
    <section className="event-participants span-2" aria-label="Katılımcı isim listesi">
      <header>
        <div><h3>Katılımcı isim listesi</h3><p className="muted small">{names.length.toLocaleString('tr-TR')} kişi kayıtlı</p></div>
        <div className="event-participants-actions">
          {onChange ? <button type="button" className="btn-secondary" disabled={disabled || busy || !!preview} onClick={() => void download(true)}>Excel şablonu</button> : null}
          {eventId && names.length > 0 ? <button type="button" className="btn-secondary" onClick={() => void download(false)}>İsim listesini indir</button> : null}
          {onChange && names.length > 0 ? <button type="button" className="btn-secondary" disabled={disabled || busy || !!preview} onClick={() => void clear()}>Listeyi kaldır</button> : null}
        </div>
      </header>
      {onChange ? <>
        <p className="muted small">Etkinlik, gezi veya proje için .xlsx yükleyin. İlk sayfada “Ad Soyad” ya da ayrı “Ad” ve “Soyad” sütunları olmalıdır. Yalnızca isimler alınır. En fazla 5000 kişi, 10 MB.</p>
        <label className="event-participants-upload">Excel isim listesi
          <input ref={input} type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" disabled={disabled || busy || !!preview} onChange={e => void upload(e.target.files?.[0])} />
        </label>
        <p className="muted small">Listeyi kabul ettikten sonra Kaydet düğmesine basın. Gerçekleşen katılım sayısı ayrıca girilir.</p>
      </> : null}
      {busy ? <p role="status">Excel kontrol ediliyor…</p> : null}
      {error ? <p className="form-error" role="alert">{error}</p> : null}
      {preview ? <div className="event-participants-preview" role="status">
        <strong>{preview.names.length.toLocaleString('tr-TR')} isim hazır</strong>
        <p>{names.length > 0 ? 'Bu liste mevcut isim listesinin yerine geçecek.' : 'Listeyi kontrol edip kabul edin.'}</p>
        {preview.duplicateNameCount > 0 ? <p>{preview.duplicateNameCount} tekrar eden isim var. Aynı isimli farklı kişiler olabileceğinden bu satırlar korunur.</p> : null}
        <div className="event-participants-actions">
          <button type="button" className="btn-primary" disabled={disabled} onClick={() => { onChange?.(preview.names); setPreview(null); onBusyChange?.(false); setShowAll(false) }}>Listeyi kullan</button>
          <button type="button" className="btn-secondary" disabled={disabled} onClick={() => { setPreview(null); onBusyChange?.(false) }}>Vazgeç</button>
        </div>
      </div> : null}
      {visible.length > 0 ? <>
        <ol className="event-participants-list">{(showAll ? visible : visible.slice(0, 30)).map((name, i) => <li key={i}>{name}</li>)}</ol>
        {visible.length > 30 ? <button type="button" className="btn-secondary" onClick={() => setShowAll(v => !v)}>{showAll ? 'Daha az göster' : `Tüm ${visible.length} ismi göster`}</button> : null}
      </> : !onChange ? <p className="muted small">Henüz isim listesi eklenmedi.</p> : null}
    </section>
  )
}
