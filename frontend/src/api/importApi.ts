import { apiDownloadFile, apiUploadJson } from './client'

export interface EmployeeImportRowResult {
  rowNumber: number
  success: boolean
  employeeId?: string | null
  fullName?: string | null
  errors: string[]
}

export interface EmployeeImportResult {
  totalRows: number
  successCount: number
  failureCount: number
  rows: EmployeeImportRowResult[]
}

export async function downloadEmployeeImportTemplate(): Promise<void> {
  await apiDownloadFile('/api/import/employees/template')
}

export async function importEmployeesFromExcel(file: File): Promise<EmployeeImportResult> {
  const form = new FormData()
  form.append('file', file)
  return apiUploadJson<EmployeeImportResult>('/api/import/employees', form)
}
