import { apiDownloadFile, apiRequest, apiUploadJson } from './client'

export interface MessageUser {
  id: string
  userName: string
  displayName: string
}

export interface Conversation {
  otherUserId: string
  otherUserName: string
  otherDisplayName: string
  lastBody: string
  lastAtUtc: string
  unreadCount: number
}

export interface DirectMessage {
  id: string
  senderUserId: string
  recipientUserId: string
  body: string
  sentAtUtc: string
  readAtUtc?: string | null
  mine: boolean
  hasAttachment: boolean
  attachmentFileName?: string | null
  attachmentContentType?: string | null
  relatedEventId?: string | null
}

export async function fetchMessageDirectory(search?: string): Promise<MessageUser[]> {
  const q = search ? `?search=${encodeURIComponent(search)}` : ''
  return apiRequest<MessageUser[]>(`/api/messages/directory${q}`)
}

export async function fetchConversations(): Promise<Conversation[]> {
  return apiRequest<Conversation[]>('/api/messages/conversations')
}

export async function fetchUnreadMessageCount(): Promise<number> {
  const data = await apiRequest<{ count: number }>('/api/messages/unread-count')
  return data.count
}

export async function fetchThread(userId: string): Promise<DirectMessage[]> {
  return apiRequest<DirectMessage[]>(`/api/messages/with/${userId}`)
}

export async function sendDirectMessage(
  recipientUserId: string,
  body: string,
  file?: File | null,
): Promise<DirectMessage> {
  if (file) {
    const form = new FormData()
    form.append('recipientUserId', recipientUserId)
    form.append('body', body)
    form.append('file', file)
    return apiUploadJson<DirectMessage>('/api/messages/with-file', form)
  }
  return apiRequest<DirectMessage>('/api/messages', {
    method: 'POST',
    body: { recipientUserId, body },
  })
}

export async function downloadMessageFile(messageId: string): Promise<void> {
  await apiDownloadFile(`/api/messages/${messageId}/file`)
}
