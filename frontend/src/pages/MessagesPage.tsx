import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import {
  downloadMessageFile,
  fetchConversations,
  fetchMessageDirectory,
  fetchThread,
  sendDirectMessage,
  type Conversation,
  type DirectMessage,
  type MessageUser,
} from '@/api/messagesApi'
import { ApiClientError, apiFetchBlob } from '@/api/client'
import { useAuth } from '@/auth/AuthContext'
import { PermissionCodes } from '@/auth/permissionCodes'
import { Link } from 'react-router-dom'

const ALLOWED_MESSAGE_FILES = '.pdf,.png,.jpg,.jpeg'
const MAX_MESSAGE_FILE_BYTES = 5 * 1024 * 1024

function initials(name: string) {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]?.toLocaleUpperCase('tr-TR') ?? '')
      .join('') || '?'
  )
}

function formatWhen(iso: string) {
  return new Date(iso).toLocaleString('tr-TR', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function formatListTime(iso: string) {
  const d = new Date(iso)
  const now = new Date()
  const sameDay =
    d.toLocaleDateString('tr-TR') === now.toLocaleDateString('tr-TR')
  return d.toLocaleTimeString('tr-TR', {
    hour: '2-digit',
    minute: '2-digit',
    ...(sameDay ? {} : { day: '2-digit', month: 'short' }),
  })
}

function isImageType(contentType?: string | null, fileName?: string | null) {
  if (contentType?.startsWith('image/')) return true
  return /\.(png|jpe?g)$/i.test(fileName ?? '')
}

function MessageAttachment({ message }: { message: DirectMessage }) {
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!message.hasAttachment || !isImageType(message.attachmentContentType, message.attachmentFileName)) {
      return
    }
    let objectUrl: string | null = null
    let cancelled = false
    void apiFetchBlob(`/api/messages/${message.id}/file`)
      .then((blob) => {
        objectUrl = URL.createObjectURL(blob)
        if (!cancelled) setUrl(objectUrl)
      })
      .catch(() => {
        if (!cancelled) setUrl(null)
      })
    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [message.attachmentContentType, message.attachmentFileName, message.hasAttachment, message.id])

  if (!message.hasAttachment) return null

  if (url) {
    return (
      <a href={url} target="_blank" rel="noreferrer" className="msg-attach-preview">
        <img src={url} alt={message.attachmentFileName ?? 'Görsel'} />
      </a>
    )
  }

  return (
    <button
      type="button"
      className="msg-attach-file"
      onClick={() => void downloadMessageFile(message.id)}
    >
      {message.attachmentFileName || 'Dosya indir'}
    </button>
  )
}

export function MessagesPage() {
  const { user, hasPermission } = useAuth()
  const canUse =
    hasPermission(PermissionCodes.MessagesUse) || hasPermission(PermissionCodes.NotificationsView)
  const [search, setSearch] = useState('')
  const [directory, setDirectory] = useState<MessageUser[]>([])
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [activeId, setActiveId] = useState<string | null>(null)
  const [thread, setThread] = useState<DirectMessage[]>([])
  const [draft, setDraft] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [directoryLoading, setDirectoryLoading] = useState(false)
  const [sending, setSending] = useState(false)
  const fileRef = useRef<HTMLInputElement>(null)
  const threadEndRef = useRef<HTMLDivElement>(null)

  const loadLists = useCallback(async (silent = false) => {
    if (!canUse) return
    if (!silent) setLoading(true)
    setError(null)
    try {
      setConversations(await fetchConversations())
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Mesajlar yüklenemedi.')
    } finally {
      if (!silent) setLoading(false)
    }
  }, [canUse])

  useEffect(() => {
    void loadLists()
  }, [loadLists])

  useEffect(() => {
    const query = search.trim()
    setDirectory([])
    if (!canUse || !query) {
      setDirectoryLoading(false)
      return
    }
    let cancelled = false
    setError(null)
    setDirectoryLoading(true)
    const timer = window.setTimeout(() => {
      void fetchMessageDirectory(query)
        .then((rows) => { if (!cancelled) setDirectory(rows) })
        .catch((err) => {
          if (!cancelled) setError(err instanceof ApiClientError ? err.message : 'Kişiler aranamadı.')
        })
        .finally(() => { if (!cancelled) setDirectoryLoading(false) })
    }, 150)
    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [canUse, search])

  useEffect(() => {
    if (!activeId || !canUse) {
      setThread([])
      return
    }
    let cancelled = false
    ;(async () => {
      try {
        const rows = await fetchThread(activeId)
        if (!cancelled) setThread(rows)
        void loadLists(true)
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof ApiClientError ? err.message : 'Konuşma yüklenemedi.')
        }
      }
    })()
    return () => {
      cancelled = true
    }
  }, [activeId, canUse])

  useEffect(() => {
    threadEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [thread.length, activeId])

  async function onSend(e: FormEvent) {
    e.preventDefault()
    if (!activeId || sending) return
    if (!draft.trim() && !file) return
    if (file && file.size > MAX_MESSAGE_FILE_BYTES) {
      setError('Dosya en fazla 5 MB olabilir.')
      return
    }
    setSending(true)
    setError(null)
    try {
      const sent = await sendDirectMessage(activeId, draft.trim(), file)
      setThread((prev) => [...prev, sent])
      setDraft('')
      setFile(null)
      if (fileRef.current) fileRef.current.value = ''
      window.dispatchEvent(new Event('messages:changed'))
      void loadLists(true)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : 'Mesaj gönderilemedi.')
    } finally {
      setSending(false)
    }
  }

  if (!canUse) {
    return (
      <div className="panel">
        <p className="form-error">Mesajları görüntüleme yetkiniz yok.</p>
      </div>
    )
  }

  const directoryHits = directory.filter(
    (u) => u.id !== user?.id && !conversations.some((c) => c.otherUserId === u.id),
  )
  const searchTerm = search.trim().toLocaleLowerCase('tr-TR')
  const visibleConversations = searchTerm
    ? conversations.filter((c) => c.otherDisplayName.toLocaleLowerCase('tr-TR').includes(searchTerm))
    : conversations

  const activeConv = conversations.find((c) => c.otherUserId === activeId)
  const activeDir = directory.find((d) => d.id === activeId)
  const activeName = activeConv?.otherDisplayName || activeDir?.displayName || 'Konuşma'

  return (
    <div className="msg-shell">
      <aside className="msg-sidebar">
        <header className="msg-sidebar-head">
          <div>
            <p className="msg-kicker">İletişim</p>
            <h2>Mesajlar</h2>
          </div>
        </header>
        <label className="msg-search">
          <span className="sr-only">Kişi ara</span>
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Kişi veya konuşma ara…"
          />
        </label>
        {loading ? (
          <div className="ui-skeleton-stack msg-status" aria-hidden="true">
            <span className="ui-skeleton" />
            <span className="ui-skeleton" />
            <span className="ui-skeleton" />
          </div>
        ) : null}
        {error ? <p className="form-error msg-status">{error}</p> : null}

        <div className="msg-list-scroll">
          {conversations.length === 0 && !loading && !searchTerm ? (
            <p className="muted small msg-status">Henüz konuşma yok. İsim yazarak başlatın.</p>
          ) : null}
          <ul className="msg-conv-list">
            {visibleConversations.map((c) => (
              <li key={c.otherUserId}>
                <button
                  type="button"
                  className={c.otherUserId === activeId ? 'is-active' : ''}
                  onClick={() => setActiveId(c.otherUserId)}
                >
                  <span className="msg-avatar" aria-hidden="true">
                    {initials(c.otherDisplayName)}
                  </span>
                  <span className="msg-conv-text">
                    <strong>{c.otherDisplayName}</strong>
                    <em>{c.lastBody || 'Dosya eki'}</em>
                  </span>
                  <span className="msg-conv-meta">
                    {c.lastAtUtc ? <time>{formatListTime(c.lastAtUtc)}</time> : null}
                    {c.unreadCount > 0 ? <span className="msg-unread">{c.unreadCount > 9 ? '9+' : c.unreadCount}</span> : null}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          {directoryHits.length > 0 ? (
            <div className="msg-directory">
              <p>Yeni konuşma</p>
              <ul>
                {directoryHits.map((u) => (
                  <li key={u.id}>
                    <button type="button" onClick={() => setActiveId(u.id)}>
                      <span className="msg-avatar is-ghost" aria-hidden="true">
                        {initials(u.displayName)}
                      </span>
                      <span className="msg-conv-text">
                        <strong>{u.displayName}</strong>
                        <em>{u.userName}</em>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          ) : searchTerm && !directoryLoading && !loading && visibleConversations.length === 0 ? (
            <p className="muted small msg-status">Eşleşen kişi yok.</p>
          ) : null}
        </div>
      </aside>

      <section className="msg-thread">
        {!activeId ? (
          <div className="msg-empty">
            <span className="msg-empty-mark" aria-hidden="true">
              ✉
            </span>
            <h3>Konuşma seçin</h3>
            <p>Soldan bir kişi arayın veya mevcut konuşmayı açın. Aynı mesaj kutusu mahalle ve müdürlükte ortaktır.</p>
          </div>
        ) : (
          <>
            <header className="msg-thread-head">
              <span className="msg-avatar is-lg" aria-hidden="true">
                {initials(activeName)}
              </span>
              <div>
                <h3>{activeName}</h3>
                <p>Kurum içi mesaj</p>
              </div>
            </header>
            <ul className="msg-bubbles">
              {thread.map((m) => {
                const mine = m.mine || m.senderUserId === user?.id
                return (
                  <li key={m.id} className={mine ? 'is-mine' : ''}>
                    {!mine ? (
                      <span className="msg-avatar is-sm" aria-hidden="true">
                        {initials(activeName)}
                      </span>
                    ) : null}
                    <div className="msg-bubble">
                      {m.body ? <p>{m.body}</p> : null}
                      <MessageAttachment message={m} />
                      {m.relatedEventId ? (
                        <Link to={`/events/${m.relatedEventId}`} className="msg-event-link">
                          Etkinliğe git
                        </Link>
                      ) : null}
                      <time>{formatWhen(m.sentAtUtc)}</time>
                    </div>
                  </li>
                )
              })}
              <div ref={threadEndRef} />
            </ul>
            <form onSubmit={onSend} className="msg-compose">
              {file ? (
                <div className="msg-file-chip">
                  {file.name}
                  <button type="button" onClick={() => setFile(null)} aria-label="Dosyayı kaldır">
                    ×
                  </button>
                </div>
              ) : null}
              <div className="msg-compose-row">
                <input
                  value={draft}
                  onChange={(e) => setDraft(e.target.value)}
                  placeholder="Mesaj yazın…"
                  aria-label="Mesaj"
                />
                <input
                  ref={fileRef}
                  type="file"
                  accept={ALLOWED_MESSAGE_FILES}
                  className="sr-only"
                  onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                />
                <button
                  type="button"
                  className="msg-icon-btn"
                  onClick={() => fileRef.current?.click()}
                  title="PDF veya görsel ekle"
                >
                  +
                </button>
                <button
                  type="submit"
                  className="msg-send"
                  disabled={sending || (!draft.trim() && !file)}
                >
                  Gönder
                </button>
              </div>
            </form>
          </>
        )}
      </section>
    </div>
  )
}
