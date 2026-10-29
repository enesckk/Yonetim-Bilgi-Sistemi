import { apiFetchBlob, apiRequest, apiUploadJson } from './client'
export type EventMedia = {
  id: string
  kind: 'poster' | 'photo'
  fileName: string
  contentType: string
  size: number
}
const root = (id: string) => `/api/events/${id}/media`
export const fetchEventMedia = (id: string) => apiRequest<EventMedia[]>(root(id))
export const fetchEventMediaBlob = (id: string, file: string) =>
  apiFetchBlob(`${root(id)}/${file}/file`)
export const removeEventMedia = (id: string, file: string) =>
  apiRequest(`${root(id)}/${file}`, { method: 'DELETE' })
export function uploadEventMedia(id: string, kind: 'poster' | 'photo', files: File[]) {
  const body = new FormData()
  body.append('kind', kind)
  for (const file of files) body.append('files', file)
  return apiUploadJson(root(id), body)
}
export function mediaSelectionError(files: File[]) {
  if (
    files.some((f) => !/\.(jpe?g|png|webp)$/i.test(f.name) || !f.size || f.size > 8 * 1024 * 1024)
  )
    return 'JPG, PNG veya WebP seçin. Her görsel en fazla 8 MB olabilir.'
  if (files.length > 20) return 'En fazla 20 fotoğraf seçebilirsiniz.'
  if (files.reduce((n, f) => n + f.size, 0) > 40 * 1024 * 1024)
    return 'Bir yüklemenin toplamı en fazla 40 MB olabilir. Görselleri gruplar halinde yükleyin.'
  return null
}
