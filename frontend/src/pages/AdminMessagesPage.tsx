import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiClientError, apiDownloadFile } from '@/api/client'
import { fetchAdminConversations, fetchAdminThread, type AdminConversation, type DirectMessage, type MessageArchivePage } from '@/api/messagesApi'

const when = (iso: string) => new Date(iso).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul', dateStyle: 'short', timeStyle: 'short' })
const messageError = (err: unknown) => err instanceof ApiClientError ? err.message : 'Mesajlar yüklenemedi. Tekrar deneyin.'
const pairKey = (row: AdminConversation) => `${row.firstUser.id}/${row.secondUser.id}`

export function AdminMessagesPage() {
  const threadHeading = useRef<HTMLHeadingElement>(null)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [list, setList] = useState<MessageArchivePage<AdminConversation> | null>(null)
  const [selected, setSelected] = useState<AdminConversation | null>(null)
  const [threadPage, setThreadPage] = useState(1)
  const [thread, setThread] = useState<MessageArchivePage<DirectMessage> | null>(null)
  const [loading, setLoading] = useState(true)
  const [threadLoading, setThreadLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [threadError, setThreadError] = useState<string | null>(null)
  const [revision, setRevision] = useState(0)

  useEffect(() => {
    let cancelled = false
    setLoading(true); setError(null); setList(null)
    const timer = window.setTimeout(() => {
      void fetchAdminConversations(search.trim(), page).then(data => { if (!cancelled) setList(data) })
        .catch(err => { if (!cancelled) setError(messageError(err)) })
        .finally(() => { if (!cancelled) setLoading(false) })
    }, 200)
    return () => { cancelled = true; window.clearTimeout(timer) }
  }, [search, page, revision])

  useEffect(() => {
    setThread(null); setThreadError(null)
    if (!selected) { setThreadLoading(false); return }
    let cancelled = false
    setThreadLoading(true)
    void fetchAdminThread(selected.firstUser.id, selected.secondUser.id, threadPage)
      .then(data => { if (!cancelled) setThread(data) })
      .catch(err => { if (!cancelled) setThreadError(messageError(err)) })
      .finally(() => { if (!cancelled) setThreadLoading(false) })
    return () => { cancelled = true }
  }, [selected, threadPage, revision])

  useEffect(() => {
    if (selected && window.matchMedia('(max-width: 900px)').matches) {
      threadHeading.current?.focus({ preventScroll: true })
      threadHeading.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [selected])

  return <div className="admin-messages-page">
    <header className="admin-messages-heading"><div><h1>Sohbet arşivi</h1><p>Kullanıcılar arasındaki tüm sohbetleri ve mesaj geçmişini inceleyin.</p></div>
      <div><Link className="btn-secondary" to="/messages">Mesajlarım</Link><button className="btn-secondary" onClick={() => setRevision(v => v + 1)}>Yenile</button></div>
    </header>
    <div className="admin-message-layout">
      <section className="panel admin-conversation-list" aria-label="Sohbetler">
        <label className="admin-message-search">Katılımcı ara<input type="search" placeholder="Ad veya kullanıcı adı…" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <p className="muted small" role="status">{loading ? 'Sohbetler yükleniyor…' : list ? `${list.totalCount} sohbet` : ''}</p>
        {error ? <p className="form-error" role="alert">{error}</p> : null}
        {list?.items.length === 0 ? <p className="muted">{search.trim() ? 'Aramaya uygun sohbet yok.' : 'Henüz kayıtlı sohbet yok.'}</p> : null}
        <ul>{list?.items.map(row => <li key={pairKey(row)}><button className={selected && pairKey(selected) === pairKey(row) ? 'is-active' : ''}
          onClick={() => { setThread(null); setSelected(row); setThreadPage(1) }}>
          <strong>{row.firstUser.displayName} ↔ {row.secondUser.displayName}</strong>
          <span>{row.firstUser.userName} · {row.secondUser.userName}</span>
          <p>{row.lastSenderUserId === row.firstUser.id ? row.firstUser.displayName : row.secondUser.displayName}: {row.lastBody}</p>
          <small>{row.messageCount} mesaj · {when(row.lastAtUtc)}</small>
        </button></li>)}</ul>
        {list && list.totalCount > list.pageSize ? <div className="admin-message-pager">
          <button className="btn-secondary" disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Önceki</button>
          <span>{page} / {Math.ceil(list.totalCount / list.pageSize)}</span>
          <button className="btn-secondary" disabled={page * list.pageSize >= list.totalCount || loading} onClick={() => setPage(p => p + 1)}>Sonraki</button>
        </div> : null}
      </section>
      <section className="panel admin-message-thread" aria-label="Sohbet mesajları" aria-busy={threadLoading}>
        {!selected ? <div className="admin-message-empty"><h2>Sohbet seçin</h2><p>Katılımcı adlarına tıklayarak mesaj geçmişini görüntüleyin.</p></div> : <>
          <header><h2 ref={threadHeading} tabIndex={-1}>{selected.firstUser.displayName} ↔ {selected.secondUser.displayName}</h2><p>Salt okunur inceleme · Alıcıların okundu bilgisi değişmez.</p></header>
          {threadLoading ? <p role="status">Mesajlar yükleniyor…</p> : null}
          {threadError ? <p className="form-error" role="alert">{threadError}</p> : null}
          {thread && thread.totalCount > thread.pageSize ? <div className="admin-message-pager">
            <button className="btn-secondary" disabled={threadPage * thread.pageSize >= thread.totalCount} onClick={() => setThreadPage(p => p + 1)}>Daha eski mesajlar</button>
            <span>{thread.totalCount} mesaj · {threadPage} / {Math.ceil(thread.totalCount / thread.pageSize)}</span>
            <button className="btn-secondary" disabled={threadPage === 1} onClick={() => setThreadPage(p => p - 1)}>Daha yeni mesajlar</button>
          </div> : null}
          {thread?.items.length === 0 ? <p>Bu sohbette mesaj bulunamadı.</p> : null}
          <ol className="admin-message-entries">{thread?.items.map(m => {
            const sender = m.senderUserId === selected.firstUser.id ? selected.firstUser : selected.secondUser
            const recipient = m.recipientUserId === selected.firstUser.id ? selected.firstUser : selected.secondUser
            return <li key={m.id} className={m.senderUserId === selected.secondUser.id ? 'from-second' : ''}>
              <div><strong>{sender.displayName}</strong><span> → {recipient.displayName}</span></div>
              {m.body ? <p>{m.body}</p> : null}
              {m.hasAttachment ? <button className="btn-secondary" onClick={() => void apiDownloadFile(`/api/admin/messages/${m.id}/file`).catch(err => setThreadError(messageError(err)))}>Ek: {m.attachmentFileName ?? 'Dosya'}</button> : null}
              {m.relatedEventId ? <Link to={`/events/${m.relatedEventId}`}>İlgili etkinlik</Link> : null}
              <small><time dateTime={m.sentAtUtc}>{when(m.sentAtUtc)}</time> · {m.readAtUtc ? `Okundu: ${when(m.readAtUtc)}` : 'Henüz okunmadı'}</small>
            </li>
          })}</ol>
        </>}
      </section>
    </div>
  </div>
}
