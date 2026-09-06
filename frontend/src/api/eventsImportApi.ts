import { apiDownloadFile, apiUploadJson } from './client'
import type { EventImportResult, FacilityCoordsImportResult } from './eventsApi'

export async function downloadEventImportTemplate(): Promise<void> {
  await apiDownloadFile('/api/events/import/template')
}

export async function importEventsFromCsv(file: File): Promise<EventImportResult> {
  const form = new FormData()
  form.append('file', file)
  return apiUploadJson<EventImportResult>('/api/events/import', form)
}

export async function downloadFacilityCoordsTemplate(): Promise<void> {
  await apiDownloadFile('/api/organization/units/coords-import/template')
}

export async function importFacilityCoordsFromCsv(file: File): Promise<FacilityCoordsImportResult> {
  const form = new FormData()
  form.append('file', file)
  return apiUploadJson<FacilityCoordsImportResult>('/api/organization/units/coords-import', form)
}
