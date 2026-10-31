import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { createEvent, fetchEvents, type EventListItem } from '@/api/eventsApi'
import {
  deleteInformationNote,
  downloadInformationNote,
  fetchInformationNote,
  fetchInformationNotes,
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
  const [newEvent, setNewEvent] = useState(false)
  const [eventTitle, setEventTitle] = useState('')
  const [eventKind, setEventKind] = useState('project')
  const [eventDate, setEventDate] = useState('')
  const sequence = useRef(0)
  const detailSequence = useRef(0)
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
  useEffect(() => {
    const firstNote = items.find((note) => note.eventId === eventId)
    if (eventId && !selectedId && !loading && firstNote) {
      setParams((previous) => {
        const next = new URLSearchParams(previous)
        next.set('note', firstNote.id)
        return next
      }, { replace: true })
    }
  }, [eventId, selectedId, loading, items, setParams])
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
    setNewEvent(false)
    setEventTitle('')
    setEventDate('')
    setEventKind('project')
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
  async function addEvent() {
    if (!eventTitle.trim() || !eventDate || busy) return
    setBusy(true)
    setError(null)
    try {
      const created = await createEvent({
        title: eventTitle.trim(),
        category: eventKind || null,
        startAtUtc: new Date(eventDate + 'T00:00:00+03:00').toISOString(),
        isDateOnly: true,
        status: 1,
      })
      // Keep the created record selected even if refreshing the list fails.
      setEvents((previous) => [...previous, {
        id: created.id, title: eventTitle.trim(), status: 1, statusLabel: 'Taslak',
        startAtUtc: eventDate, category: eventKind || null,
      }])
      setInput((previous) => ({ ...previous, eventId: created.id }))
      setNewEvent(false)
      setEventTitle('')
      setEventDate('')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Proje / etkinlik eklenemedi.')
    } finally {
      setBusy(false)
    }
  }
  async function submit(e: FormEvent) {
    e.preventDefault()
    if (busy) return
    if (newEvent) {
      setError('Önce yeni proje / etkinliği oluşturup seçin veya yeni kayıt alanını kapatın.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const n = await saveInformationNote({ ...input, title: input.subject.trim().slice(0, 200) }, null, editing ?? undefined)
      setEditing(undefined)
      await load()
      pick(n.id)
      setSelected(n)
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
  async function download() {
    if (!selected) return
    try {
      await downloadInformationNote(selected.id)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Dosya indirilemedi.')
    }
  }
  return (
    <div className="information-notes-page">
      <section className={`panel information-notes-head${editing !== undefined ? ' information-notes-head--editing' : ''}`}>
        <div>
          <p className="information-notes-eyebrow">Müdürlük</p>
          <h1>Bilgi notları</h1>
          <p className="muted">
            Proje ve etkinlikler için kurum şablonunda bilgi notları.
          </p>
        </div>
        {canManage && editing === undefined && (
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
            <div className="note-form-title">
              <span className="note-form-icon" aria-hidden="true">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9Z" />
                  <path d="M14 3v6h6M8 13h8M8 17h5" />
                </svg>
              </span>
              <div>
                <p className="note-form-eyebrow">BİLGİ NOTU OLUŞTURUCU</p>
                <h2>{editing ? 'Bilgi notunu düzenle' : 'Yeni bilgi notu'}</h2>
                <p className="note-form-subtitle">Bilgileri tamamlayın, kurum şablonunda belgeniz hazır olsun.</p>
              </div>
            </div>
            <button type="button" className="btn-secondary note-form-cancel" disabled={busy} onClick={() => setEditing(undefined)}>
              Vazgeç
            </button>
          </div>
          <fieldset aria-label="Bilgi notu alanları" disabled={busy} className="information-note-edit-fields">
            <section className="note-project-section" aria-labelledby="note-project-heading">
              <div className="note-section-heading">
                <div>
                  <h3 id="note-project-heading">Proje ve etkinlik bağlantısı</h3>
                  <p>Mevcut bir kaydı seçin veya buradan yeni bir kayıt oluşturun.</p>
                </div>
                <button
                  type="button"
                  className="note-inline-add"
                  aria-expanded={newEvent}
                  aria-controls="note-new-event"
                  onClick={() => setNewEvent(!newEvent)}
                >
                  <span aria-hidden="true">{newEvent ? '−' : '+'}</span>
                  {newEvent ? 'Yeni kaydı kapat' : 'Proje / etkinlik ekle'}
                </button>
              </div>
              <label>
                <span className="note-field-label">Proje / etkinlik <span className="note-optional">İsteğe bağlı</span></span>
                <select aria-label="Proje / etkinlik" value={input.eventId ?? ''} onChange={(e) => setInput((p) => ({ ...p, eventId: e.target.value || null }))}>
                  <option value="">Bağımsız bilgi notu</option>
                  {events.map((e) => (
                    <option key={e.id} value={e.id}>{e.category === 'project' ? 'Proje' : 'Etkinlik'}: {e.title}</option>
                  ))}
                </select>
              </label>
              {newEvent && (
                <div className="note-new-event" id="note-new-event">
                  <div className="note-new-event-heading">
                    <strong>Yeni proje / etkinlik</strong>
                    <span>Oluşturduğunuz kayıt otomatik olarak seçilir.</span>
                  </div>
                  <div className="note-new-event-fields">
                    <label>Tür
                      <select value={eventKind} onChange={(e) => setEventKind(e.target.value)}>
                        <option value="project">Proje</option><option value="">Etkinlik</option>
                      </select>
                    </label>
                    <label>Proje / etkinlik adı
                      <input maxLength={200} value={eventTitle} placeholder="Kaydın adını yazın" onChange={(e) => setEventTitle(e.target.value)} />
                    </label>
                    <label>Başlangıç tarihi
                      <input type="date" value={eventDate} onChange={(e) => setEventDate(e.target.value)} />
                    </label>
                  </div>
                  <div className="note-new-event-actions">
                    <span>Taslak olarak eklenir; ayrıntılarını daha sonra düzenleyebilirsiniz.</span>
                    <button type="button" className="btn-secondary" disabled={!eventTitle.trim() || !eventDate} onClick={() => void addEvent()}>Oluştur ve seç</button>
                  </div>
                </div>
              )}
            </section>
            <section className="note-content-section" aria-labelledby="note-content-heading">
              <div className="note-section-heading">
                <div>
                  <h3 id="note-content-heading">Bilgi notunun içeriği</h3>
                  <p>Bu bilgiler doğrudan belgenizde yer alır.</p>
                </div>
                <span className="note-required-hint">Tüm alanlar zorunludur</span>
              </div>
              <div className="note-content-fields">
                <label>Konu
                  <input required aria-label="Konu" value={input.subject} maxLength={500} placeholder="Bilgi notunun konusunu yazın" onChange={(e) => field('subject', e.target.value)} />
                </label>
                <label>Tarih
                  <input required aria-label="Tarih" value={input.dateText} maxLength={100} onChange={(e) => field('dateText', e.target.value)} placeholder="Örn. 15–17 Ekim 2026" />
                </label>
              </div>
              <label className="note-description-field">
                <span className="note-field-label">Açıklama <span className="note-character-count">{input.body.length.toLocaleString('tr-TR')} / 30.000</span></span>
                <textarea aria-label="Açıklama" required maxLength={30000} rows={7} value={input.body} placeholder="Aktarmak istediğiniz bilgileri, yapılan çalışmaları ve önemli ayrıntıları yazın…" onChange={(e) => field('body', e.target.value)} />
              </label>
            </section>
          </fieldset>
          <div className="information-note-form-actions">
            <div className="note-template-hint">
              <span aria-hidden="true">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"><path d="m5 12 4 4L19 6" /></svg>
              </span>
              <div><strong>Kurum şablonuna uygun</strong><p>Kaydettikten sonra Word belgenizi indirebilirsiniz.</p></div>
            </div>
            <button type="submit" className="btn-primary note-save-button" disabled={busy}>
              {busy ? 'İşleniyor…' : 'Bilgi notunu kaydet'}
              {!busy && <span aria-hidden="true">→</span>}
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
                    <button className="btn-secondary" onClick={() => void download()}>
                      Bilgi notu indir
                    </button>
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
                    ['Konu', selected.subject],
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
                <p>Detaylarını okumak ve bilgi notunu indirmek için listeden bir kayıt açın.</p>
              </div>
            )}
          </section>
        </div>
      )}
    </div>
  )
}
