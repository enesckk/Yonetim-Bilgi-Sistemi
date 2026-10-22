import { useEffect, useRef, useState } from 'react'
import { ApiClientError, apiFetchBlob } from '@/api/client'
import { deleteStockPhoto, uploadStockPhoto } from '@/api/stockApi'
import { useConfirm } from './ConfirmDialog'
import { StockIcon } from './StockIcon'

export function StockItemPhoto({ itemId, name, hasPhoto, version, large = false }: {
  itemId: string; name: string; hasPhoto: boolean; version?: string | null; large?: boolean
}) {
  const trigger = useRef<HTMLButtonElement>(null)
  const closeButton = useRef<HTMLButtonElement>(null)
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  const [expanded, setExpanded] = useState(false)
  useEffect(() => {
    let cancelled = false
    let created: string | null = null
    setUrl(null)
    setFailed(false)
    setExpanded(false)
    if (hasPhoto) void apiFetchBlob(`/api/stock/items/${itemId}/photo`).then(blob => {
      if (cancelled) return
      created = URL.createObjectURL(blob)
      setUrl(created)
    }).catch(() => { if (!cancelled) setFailed(true) })
    return () => { cancelled = true; if (created) URL.revokeObjectURL(created) }
  }, [itemId, hasPhoto, version])
  useEffect(() => {
    if (!expanded) return
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setExpanded(false)
      if (e.key === 'Tab') { e.preventDefault(); closeButton.current?.focus() }
    }
    window.addEventListener('keydown', onKey)
    return () => { window.removeEventListener('keydown', onKey); document.body.style.overflow = previousOverflow; trigger.current?.focus() }
  }, [expanded])
  if (!url || failed) return <span className={`custody-item-symbol${large ? ' stock-photo-placeholder' : ''}`} title={failed ? 'Görsel yüklenemedi' : undefined}><StockIcon name="box" />{large && <span>{hasPhoto ? failed ? 'Görsel yüklenemedi.' : 'Görsel yükleniyor…' : 'Henüz görsel eklenmedi.'}</span>}</span>
  return <>
    <button ref={trigger} type="button" className={`stock-photo-thumb${large ? ' is-large' : ''}`} aria-label={`${name} görselini büyüt`} onClick={() => setExpanded(true)}><img src={url} alt={name} onError={() => setFailed(true)} /></button>
    {expanded && <div className="stock-photo-overlay" onClick={e => { e.stopPropagation(); setExpanded(false) }}>
      <div className="stock-photo-preview" role="dialog" aria-modal="true" aria-label={`${name} görseli`} onClick={e => e.stopPropagation()}>
        <div><strong>{name}</strong><button type="button" className="btn-secondary" ref={closeButton} autoFocus onClick={() => setExpanded(false)}>Kapat</button></div>
        <img src={url} alt={name} />
      </div>
    </div>}
  </>
}

export function StockPhotoEditor({ itemId, name, hasPhoto, onChanged }: {
  itemId: string; name: string; hasPhoto: boolean; onChanged: () => Promise<void>
}) {
  const confirm = useConfirm()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function upload(file: File | undefined) {
    if (!file || busy) return
    setError('')
    if (!/\.(jpe?g|png)$/i.test(file.name)) { setError('JPG veya PNG görsel seçin.'); return }
    if (file.size === 0 || file.size > 5 * 1024 * 1024) { setError('Görsel boş olmamalı ve en fazla 5 MB olmalıdır.'); return }
    setBusy(true)
    try { await uploadStockPhoto(itemId, file); await onChanged() }
    catch (err) { setError(err instanceof ApiClientError ? err.validationErrors ? Object.values(err.validationErrors).flat().join(' ') : err.message : 'Görsel kaydedilemedi. Tekrar deneyin.') }
    finally { setBusy(false) }
  }
  async function remove() {
    if (busy || !await confirm({ title: 'Malzeme görselini kaldır', message: `${name} görseli kaldırılacak.`, confirmLabel: 'Görseli kaldır' })) return
    setBusy(true)
    setError('')
    try { await deleteStockPhoto(itemId); await onChanged() }
    catch (err) { setError(err instanceof ApiClientError ? err.message : 'Görsel kaldırılamadı.') }
    finally { setBusy(false) }
  }
  return <div className="stock-photo-editor">
    <label className="stock-photo-upload">{busy ? 'Kaydediliyor…' : hasPhoto ? 'Görseli değiştir' : 'Görsel ekle'}<input aria-label={`${name} için görsel seç`} type="file" accept="image/jpeg,image/png,.jpg,.jpeg,.png" disabled={busy} onChange={e => { const file = e.target.files?.[0]; e.target.value = ''; void upload(file) }} /></label>
    {hasPhoto && <button className="custody-secondary" type="button" disabled={busy} onClick={() => void remove()}>Görseli kaldır</button>}
    <span>JPG veya PNG · En fazla 5 MB</span>
    {error && <p className="form-error" role="alert">{error}</p>}
  </div>
}
