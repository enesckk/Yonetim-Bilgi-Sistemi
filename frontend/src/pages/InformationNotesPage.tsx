import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { fetchEvents, type EventListItem } from '@/api/eventsApi'
import {
  deleteInformationNote,
  downloadInformationNote,
  fetchInformationNote,
  fetchInformationNotes,
  previewInformationNote,
  saveInformationNote,
  type InformationNote,
  type NoteInput,
} from '@/api/informationNotesApi'
import { useConfirm } from '@/components/ConfirmDialog'

const blank = (eventId: string | null): NoteInput => ({
  title: '',
  eventId,
  dateText: '',
  organizer: '',
  subject: '',
  preparedBy: '',
  body: '',
})
export function InformationNotesPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission(PermissionCodes.EventsManage)
  const confirm = useConfirm()
  const [params, setParams] = useSearchParams()
  const eventId = params.get('eventId') ?? undefined
  const selectedId = params.get('note') ?? undefined
  const [items, setItems] = useState<InformationNote[]>([])
  const [selected, setSelected] = useState<InformationNote | null>(null)
  const [events, setEvents] = useState<EventListItem[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [editing, setEditing] = useState<string | null | undefined>(undefined)
  const [input, setInput] = useState<NoteInput>(blank(eventId ?? null))
  const [file, setFile] = useState<File | null>(null)
  const sequence = useRef(0)
  const detailSequence = useRef(0)
  const fileSequence = useRef(0)
  const load = useCallback(async () => {
    const s = ++sequence.current
    setLoading(true)
    setError(null)
    try {
      const data = await fetchInformationNotes(eventId)
      if (s === sequence.current) {
        setItems(data)
      }
    } catch (e) {
      if (s === sequence.current)
        setError(e instanceof Error ? e.message : 'Bilgi notları yüklenemedi.')
    } finally {
      if (s === sequence.current) setLoading(false)
    }
  }, [eventId])
  useEffect(() => {
    void load()
  }, [load])
  useEffect(() => {
    if (!selectedId) {
      setSelected(null)
      return
    }
    setError(null)
    const s = ++detailSequence.current
    setSelected(null)
    fetchInformationNote(selectedId)
      .then((n) => {
        if (s === detailSequence.current) setSelected(n)
      })
      .catch((e) => {
        if (s === detailSequence.current) setError(e.message)
      })
    return () => {
      detailSequence.current++
    }
  }, [selectedId])
  useEffect(() => {
    if (editing !== undefined)
      fetchEvents({})
        .then((d) => setEvents(d.items))
        .catch((e) => setError(e.message))
  }, [editing])
  const visible = items.filter((n) =>
    [n.title, n.subject, n.eventTitle ?? '', n.preparedBy]
      .join(' ')
      .toLocaleLowerCase('tr-TR')
      .includes(search.toLocaleLowerCase('tr-TR')),
  )
  const pick = (id: string) => {
    setEditing(undefined)
    setParams((p) => {
      const n = new URLSearchParams(p)
      n.set('note', id)
      return n
    })
  }
  const start = (note?: InformationNote) => {
    setError(null)
    setFile(null)
    fileSequence.current++
    setEditing(note?.id ?? null)
    setInput(
      note
        ? {
            title: note.title,
            eventId: note.eventId,
            dateText: note.dateText,
            organizer: note.organizer,
            subject: note.subject,
            preparedBy: note.preparedBy,
            body: note.body,
          }
        : blank(eventId ?? null),
    )
  }
  const field = (key: keyof NoteInput, value: string) => setInput((p) => ({ ...p, [key]: value }))
  async function chooseFile(next: File | null) {
    const s = ++fileSequence.current
    setFile(next)
    setError(null)
    if (!next) return
    if (next.size > 5 * 1024 * 1024) {
      setError('Dosya en fazla 5 MB olabilir.')
      setFile(null)
      return
    }
    if (!next.name.toLowerCase().endsWith('.docx')) return
    setBusy(true)
    setError(null)
    try {
      const read = await previewInformationNote(next)
      if (s === fileSequence.current) setInput((p) => ({ ...read, eventId: p.eventId }))
    } catch (e) {
      if (s === fileSequence.current) {
        setError(e instanceof Error ? e.message : 'Word dosyası okunamadı.')
        setFile(null)
      }
    } finally {
      if (s === fileSequence.current) setBusy(false)
    }
  }
  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const n = await saveInformationNote(input, file, editing ?? undefined)
      setEditing(undefined)
      await load()
      pick(n.id)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Bilgi notu kaydedilemedi.')
    } finally {
      setBusy(false)
    }
  }
  async function remove(note: InformationNote) {
    if (
      !(await confirm({
        title: 'Bilgi notunu sil',
        message: `“${note.title}” silinsin mi?`,
        confirmLabel: 'Sil',
        tone: 'danger',
      }))
    )
      return
    setBusy(true)
    try {
      await deleteInformationNote(note.id)
      setSelected(null)
      setParams((p) => {
        const n = new URLSearchParams(p)
        n.delete('note')
        return n
      })
      await load()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Silinemedi.')
    } finally {
      setBusy(false)
    }
  }
  async function download(excel = false) {
    if (!selected) return
    try {
      await downloadInformationNote(selected.id, excel)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Dosya indirilemedi.')
    }
  }
  return (
    <div className="information-notes-page">
      <section className="panel information-notes-head">
        <div>
          <p className="information-notes-eyebrow">Müdürlük</p>
          <h1>Bilgi notları</h1>
          <p className="muted">
            Proje ve etkinliklerin bilgi notları, açıklamaları ve kaynak belgeleri.
          </p>
        </div>
        {canManage && (
          <button className="btn-primary" disabled={busy} onClick={() => start()}>
            + Bilgi notu ekle
          </button>
        )}
      </section>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      {editing !== undefined ? (
        <form className="panel information-note-form" onSubmit={submit}>
          <div className="information-note-form-heading">
            <h2>{editing ? 'Bilgi notunu düzenle' : 'Yeni bilgi notu'}</h2>
            <button
              type="button"
              className="btn-secondary"
              disabled={busy}
              onClick={() => {
                fileSequence.current++
                setEditing(undefined)
              }}
            >
              Vazgeç
            </button>
          </div>
          <label className="information-note-upload">
            Word veya PDF dosyası
            <input
              type="file"
              accept=".docx,.pdf"
              disabled={busy}
              onChange={(e) => void chooseFile(e.target.files?.[0] ?? null)}
            />
            <small>
              Word dosyasındaki alanlar otomatik okunur. Kaydetmeden önce kontrol edebilirsiniz. En
              fazla 5 MB.
            </small>
          </label>
          <fieldset
            aria-label="Bilgi notu alanları"
            disabled={busy}
            className="information-note-edit-fields"
          >
            <div className="information-note-fields">
              <label>
                Başlık
                <input
                  aria-label="Başlık"
                  required
                  maxLength={200}
                  value={input.title}
                  onChange={(e) => field('title', e.target.value)}
                />
              </label>
              <label>
                Proje / etkinlik
                <select
                  aria-label="Proje / etkinlik"
                  value={input.eventId ?? ''}
                  onChange={(e) => setInput((p) => ({ ...p, eventId: e.target.value || null }))}
                >
                  <option value="">Bağımsız bilgi notu</option>
                  {events.map((e) => (
                    <option key={e.id} value={e.id}>
                      {e.title}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Tarih
                <input
                  aria-label="Tarih"
                  value={input.dateText}
                  maxLength={100}
                  onChange={(e) => field('dateText', e.target.value)}
                  placeholder="Örn. 15–17 Ekim 2026"
                />
              </label>
              <label>
                Düzenleyen kurum
                <input
                  aria-label="Düzenleyen kurum"
                  value={input.organizer}
                  maxLength={300}
                  onChange={(e) => field('organizer', e.target.value)}
                />
              </label>
              <label>
                Konu
                <input
                  aria-label="Konu"
                  value={input.subject}
                  maxLength={500}
                  onChange={(e) => field('subject', e.target.value)}
                />
              </label>
              <label>
                Hazırlayan
                <input
                  aria-label="Hazırlayan"
                  value={input.preparedBy}
                  maxLength={300}
                  onChange={(e) => field('preparedBy', e.target.value)}
                />
              </label>
            </div>
            <label>
              Açıklama
              <textarea
                aria-label="Açıklama"
                required
                maxLength={30000}
                rows={12}
                value={input.body}
                onChange={(e) => field('body', e.target.value)}
              />
            </label>
          </fieldset>
          <div className="information-note-form-actions">
            <span className="muted small">
              {file?.name ??
                (editing ? (selected?.fileName ?? '') : 'Dosya eklemek isteğe bağlıdır.')}
            </span>
            <button type="submit" className="btn-primary" disabled={busy}>
              {busy ? 'İşleniyor…' : 'Bilgi notunu kaydet'}
            </button>
          </div>
        </form>
      ) : (
        <div className="information-notes-layout">
          <section className="panel information-notes-list">
            <label>
              Bilgi notu ara
              <input
                type="search"
                placeholder="Başlık, konu veya proje…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </label>
            {eventId && (
              <Link className="information-note-all" to="/information-notes">
                Tüm bilgi notlarını göster
              </Link>
            )}
            {loading ? (
              <p className="muted">Yükleniyor…</p>
            ) : visible.length === 0 ? (
              <p className="information-note-empty">Bilgi notu bulunamadı.</p>
            ) : (
              <div className="information-note-list-items">
                {visible.map((n) => (
                  <button
                    type="button"
                    key={n.id}
                    className={selectedId === n.id ? 'is-selected' : ''}
                    onClick={() => pick(n.id)}
                  >
                    <strong>{n.title}</strong>
                    <span>{n.dateText || 'Tarih belirtilmemiş'}</span>
                    {n.eventTitle && <small>{n.eventTitle}</small>}
                  </button>
                ))}
              </div>
            )}
          </section>
          <section className="panel information-note-view">
            {selected ? (
              <>
                <header>
                  <div>
                    <p className="information-notes-eyebrow">Bilgi notu</p>
                    <h2>{selected.title}</h2>
                    {selected.eventId && (
                      <Link to={'/events/' + selected.eventId}>{selected.eventTitle} ›</Link>
                    )}
                  </div>
                  <div className="information-note-actions">
                    <button className="btn-secondary" onClick={() => void download(true)}>
                      Excel indir
                    </button>
                    {selected.fileName && (
                      <button className="btn-secondary" onClick={() => void download()}>
                        Özgün belgeyi indir
                      </button>
                    )}
                    {canManage && (
                      <>
                        <button
                          className="btn-secondary"
                          disabled={busy}
                          onClick={() => start(selected)}
                        >
                          Düzenle
                        </button>
                        <button
                          className="btn-secondary"
                          disabled={busy}
                          onClick={() => void remove(selected)}
                        >
                          Sil
                        </button>
                      </>
                    )}
                  </div>
                </header>
                <dl className="information-note-metadata">
                  {[
                    ['Tarih', selected.dateText],
                    ['Düzenleyen kurum', selected.organizer],
                    ['Konu', selected.subject],
                    ['Hazırlayan', selected.preparedBy],
                  ].map(([k, v]) => (
                    <div key={k}>
                      <dt>{k}</dt>
                      <dd>{v || 'Belirtilmemiş'}</dd>
                    </div>
                  ))}
                </dl>
                <div className="information-note-body">
                  <h3>Açıklama</h3>
                  {selected.body.split(/\n+/).map((p, i) => (
                    <p key={i}>{p}</p>
                  ))}
                </div>
              </>
            ) : (
              <div className="information-note-empty">
                <h2>{selectedId ? 'Bilgi notu yükleniyor…' : 'Bir bilgi notu seçin'}</h2>
                <p>Detaylarını okumak ve kaynak belgeyi indirmek için listeden bir kayıt açın.</p>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  )
}
