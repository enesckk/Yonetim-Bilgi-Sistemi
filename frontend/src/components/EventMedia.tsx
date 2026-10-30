import { useEffect, useRef, useState } from 'react'
import {
  fetchEventMedia,
  selectEventMediaForReport,
  fetchEventMediaBlob,
  mediaSelectionError,
  removeEventMedia,
  uploadEventMedia,
  type EventMedia,
} from '@/api/eventMediaApi'
import { useConfirm } from './ConfirmDialog'

function Preview({
  file,
  eventId,
  onOpen,
}: {
  file: EventMedia | File
  eventId?: string
  onOpen: (url: string, name: string) => void
}) {
  const [url, setUrl] = useState('')
  const [error, setError] = useState(false)
  const [retry, setRetry] = useState(0)
  const name = file instanceof File ? file.name : file.fileName
  useEffect(() => {
    let active = true
    let objectUrl = ''
    setUrl('')
    setError(false)
    const load =
      file instanceof File ? Promise.resolve(file) : fetchEventMediaBlob(eventId!, file.id)
    void load
      .then((blob) => {
        if (!active) return
        objectUrl = URL.createObjectURL(blob)
        setUrl(objectUrl)
      })
      .catch(() => {
        if (active) setError(true)
      })
    return () => {
      active = false
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [eventId, file, retry])
  return error ? (
    <button
      type="button"
      className="event-media-preview event-media-retry"
      onClick={() => setRetry((n) => n + 1)}
    >
      Görsel açılamadı. Tekrar dene
    </button>
  ) : (
    <button
      type="button"
      className="event-media-preview"
      disabled={!url}
      aria-label={`${name} görselini büyüt`}
      onClick={() => onOpen(url, name)}
    >
      {url ? <img src={url} alt={name} onError={() => setError(true)} /> : <span>Yükleniyor…</span>}
    </button>
  )
}
function Viewer({
  value,
  close,
}: {
  value: { url: string; name: string } | null
  close: () => void
}) {
  const dialog = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    if (value) dialog.current?.showModal()
    else dialog.current?.close()
  }, [value])
  return (
    <dialog
      ref={dialog}
      className="event-media-dialog"
      onCancel={close}
      onClick={(e) => {
        if (e.target === e.currentTarget) close()
      }}
      aria-label="Görsel önizleme"
    >
      {value && (
        <>
          <div className="event-media-dialog-head">
            <strong>{value.name}</strong>
            <button type="button" className="btn-secondary" onClick={close}>
              Kapat
            </button>
          </div>
          <img src={value.url} alt={value.name} />
        </>
      )}
    </dialog>
  )
}
export function EventMediaPicker({
  poster,
  photos,
  onPoster,
  onPhotos,
  disabled = false,
}: {
  poster: File | null
  photos: File[]
  onPoster: (file: File | null) => void
  onPhotos: (files: File[]) => void
  disabled?: boolean
}) {
  const [error, setError] = useState('')
  const [open, setOpen] = useState<{ url: string; name: string } | null>(null)
  function select(files: File[], kind: 'poster' | 'photo') {
    const next = kind === 'poster' ? files : [...photos, ...files]
    const problem = mediaSelectionError(next)
    setError(problem ?? '')
    if (problem) return
    if (kind === 'poster') onPoster(files[0] ?? null)
    else onPhotos(next)
  }
  return (
    <section className="event-assets span-2">
      <div className="event-assets-heading">
        <div>
          <h3>Afiş ve etkinlik görselleri</h3>
          <p>
            Afişi ve etkinlikten fotoğrafları ayrı yükleyin. Seçtiğiniz dosyalar kayıtla birlikte
            kaydedilir.
          </p>
        </div>
      </div>
      <div className="event-assets-columns">
        {(['poster', 'photo'] as const).map((kind) => (
          <div className="event-assets-box" key={kind}>
            <h4>{kind === 'poster' ? 'Etkinlik afişi' : 'Etkinlik fotoğrafları'}</h4>
            <p>
              {kind === 'poster'
                ? 'Tek afiş · Yeni afiş mevcut olanın yerini alır.'
                : 'En fazla 20 fotoğraf · Çoklu seçim yapabilirsiniz.'}
            </p>
            <label className="event-file-select">
              {kind === 'poster' ? 'Afiş seç' : 'Fotoğraf seç'}
              <input
                aria-label={
                  kind === 'poster' ? 'Etkinlik afişi yükle' : 'Etkinlik fotoğrafları yükle'
                }
                type="file"
                accept="image/jpeg,image/png,image/webp"
                multiple={kind === 'photo'}
                disabled={disabled}
                onChange={(e) => {
                  select(Array.from(e.target.files ?? []), kind)
                  e.target.value = ''
                }}
              />
            </label>
            <div className="event-media-grid">
              {(kind === 'poster' ? (poster ? [poster] : []) : photos).map((file, i) => (
                <figure key={`${file.name}-${i}`}>
                  <Preview file={file} onOpen={(url, name) => setOpen({ url, name })} />
                  <figcaption>
                    <span title={file.name}>{file.name}</span>
                    <button
                      type="button"
                      className="event-media-remove"
                      disabled={disabled}
                      onClick={() =>
                        kind === 'poster'
                          ? onPoster(null)
                          : onPhotos(photos.filter((_, n) => n !== i))
                      }
                    >
                      Kaldır
                    </button>
                  </figcaption>
                </figure>
              ))}
            </div>
          </div>
        ))}
      </div>
      <p className="event-media-hint">
        JPG, PNG, WebP · Dosya başına 8 MB · Yükleme başına toplam 40 MB
      </p>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      <Viewer value={open} close={() => setOpen(null)} />
    </section>
  )
}
export function EventMediaPanel({ eventId, canManage }: { eventId: string; canManage: boolean }) {
  const [items, setItems] = useState<EventMedia[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [revision, setRevision] = useState(0)
  const [open, setOpen] = useState<{ url: string; name: string } | null>(null)
  const confirm = useConfirm()
  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    void fetchEventMedia(eventId)
      .then((rows) => {
        if (active) setItems(rows)
      })
      .catch(() => {
        if (active) setError('Etkinlik görselleri alınamadı. Tekrar deneyin.')
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => {
      active = false
    }
  }, [eventId, revision])
  async function upload(files: File[], kind: 'poster' | 'photo') {
    if (!files.length || busy) return
    const problem = mediaSelectionError(files)
    if (problem) {
      setError(problem)
      return
    }
    setBusy(true)
    setError('')
    try {
      await uploadEventMedia(eventId, kind, files)
      setRevision((n) => n + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Görseller yüklenemedi.')
    } finally {
      setBusy(false)
    }
  }
  async function selectForReport(file: EventMedia, selected: boolean) {
    setBusy(true)
    setError('')
    setItems((rows) =>
      rows.map((row) => (row.id === file.id ? { ...row, includeInReport: selected } : row)),
    )
    try {
      await selectEventMediaForReport(eventId, file.id, selected)
    } catch (e) {
      setItems((rows) =>
        rows.map((row) =>
          row.id === file.id ? { ...row, includeInReport: file.includeInReport } : row,
        ),
      )
      setError(e instanceof Error ? e.message : 'Rapor seçimi kaydedilemedi.')
    } finally {
      setBusy(false)
    }
  }
  async function remove(file: EventMedia) {
    if (
      !(await confirm({
        title: 'Görseli sil',
        message: `${file.fileName} silinsin mi?`,
        confirmLabel: 'Sil',
        tone: 'danger',
      }))
    )
      return
    setBusy(true)
    setError('')
    try {
      await removeEventMedia(eventId, file.id)
      setOpen(null)
      setRevision((n) => n + 1)
    } catch {
      setError('Görsel silinemedi. Tekrar deneyin.')
    } finally {
      setBusy(false)
    }
  }
  return (
    <section className="event-assets">
      <div className="event-assets-heading">
        <div>
          <h3>Afiş ve etkinlik görselleri</h3>
          <p>Etkinliğin afişi ve fotoğraf arşivi</p>
        </div>
        <span className="event-assets-count">
          {items.filter((i) => i.kind === 'photo').length} / 20 fotoğraf
        </span>
      </div>
      {error && (
        <p className="form-error" role="alert">
          {error}{' '}
          <button
            type="button"
            className="btn-secondary"
            disabled={busy}
            onClick={() => setRevision((n) => n + 1)}
          >
            Tekrar dene
          </button>
        </p>
      )}
      {busy && <p role="status">Dosyalar kaydediliyor…</p>}
      {loading ? (
        <p role="status">Görseller yükleniyor…</p>
      ) : (
        <div className="event-assets-columns">
          {(['poster', 'photo'] as const).map((kind) => (
            <div className="event-assets-box" key={kind}>
              <div className="event-assets-heading">
                <h4>{kind === 'poster' ? 'Etkinlik afişi' : 'Fotoğraf galerisi'}</h4>
                {canManage && (
                  <label className="event-file-select">
                    {kind === 'poster' ? 'Afiş yükle / değiştir' : 'Fotoğraf ekle'}
                    <input
                      type="file"
                      aria-label={
                        kind === 'poster' ? 'Afiş yükle veya değiştir' : 'Galeriye fotoğraf ekle'
                      }
                      accept="image/jpeg,image/png,image/webp"
                      multiple={kind === 'photo'}
                      disabled={busy}
                      onChange={(e) => {
                        void upload(Array.from(e.target.files ?? []), kind)
                        e.target.value = ''
                      }}
                    />
                  </label>
                )}
              </div>
              {items.some((i) => i.kind === kind) ? (
                <div className={`event-media-grid ${kind === 'poster' ? 'is-poster' : ''}`}>
                  {items
                    .filter((i) => i.kind === kind)
                    .map((file) => (
                      <figure key={file.id}>
                        <Preview
                          file={file}
                          eventId={eventId}
                          onOpen={(url, name) => setOpen({ url, name })}
                        />
                        <figcaption>
                          <span title={file.fileName}>{file.fileName}</span>
                          {canManage && (
                            <button
                              type="button"
                              className="event-media-remove"
                              disabled={busy}
                              onClick={() => void remove(file)}
                            >
                              Sil
                            </button>
                          )}
                        </figcaption>
                        {canManage ? (
                          <label className="event-report-image-choice">
                            <input
                              type="checkbox"
                              checked={file.includeInReport ?? false}
                              disabled={busy}
                              onChange={(e) => void selectForReport(file, e.target.checked)}
                              aria-label={`${file.fileName} faaliyet raporunda kullan`}
                            />{' '}
                            Faaliyet raporunda kullan
                          </label>
                        ) : file.includeInReport ? (
                          <p className="event-report-image-badge">Faaliyet raporu için seçildi</p>
                        ) : null}
                      </figure>
                    ))}
                </div>
              ) : (
                <p className="event-media-empty">
                  {kind === 'poster'
                    ? 'Henüz afiş eklenmedi.'
                    : 'Henüz etkinlik fotoğrafı eklenmedi.'}
                </p>
              )}
            </div>
          ))}
        </div>
      )}
      {canManage && (
        <p className="event-media-hint">
          JPG, PNG, WebP · Dosya başına 8 MB · Yükleme başına toplam 40 MB
        </p>
      )}
      <Viewer value={open} close={() => setOpen(null)} />
    </section>
  )
}
