import { apiDownloadFile, apiRequest, apiUploadJson } from './client'
export interface InformationNote {
  id: string
  eventId: string | null
  eventTitle: string | null
  title: string
  dateText: string
  organizer: string
  subject: string
  preparedBy: string
  body: string
  fileName: string | null
  createdAtUtc: string
}
export type NoteInput = Pick<
  InformationNote,
  'title' | 'eventId' | 'dateText' | 'organizer' | 'subject' | 'preparedBy' | 'body'
>
export function fetchInformationNotes(eventId?: string) {
  return apiRequest<InformationNote[]>(
    '/api/information-notes' + (eventId ? '?eventId=' + encodeURIComponent(eventId) : ''),
  )
}
export function fetchInformationNote(id: string) {
  return apiRequest<InformationNote>('/api/information-notes/' + id)
}
export function previewInformationNote(file: File) {
  const f = new FormData()
  f.append('file', file)
  return apiUploadJson<NoteInput>('/api/information-notes/preview', f)
}
export function saveInformationNote(input: NoteInput, file: File | null, id?: string) {
  const f = new FormData()
  f.append('metadata', JSON.stringify(input))
  if (file) f.append('file', file)
  return apiUploadJson<InformationNote>('/api/information-notes' + (id ? '/' + id : ''), f)
}
export function deleteInformationNote(id: string) {
  return apiRequest('/api/information-notes/' + id, { method: 'DELETE' })
}
export function downloadInformationNote(id: string) {
  return apiDownloadFile('/api/information-notes/' + id + '/file')
}
