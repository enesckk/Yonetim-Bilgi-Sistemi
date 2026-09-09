import { apiDownloadFile, apiRequest, apiUpload } from './client'
import type { PagedResult } from './types'
import { cachedGet, invalidateCached, LOOKUP_TTL_MS } from '@/lib/lookupCache'

export type EmployeeStatus =
  | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11

export interface EmployeeListItem {
  id: string
  firstName: string
  lastName: string
  fullName: string
  employeeNumber?: string | null
  hasPhoto?: boolean
  jobTitleName?: string | null
  primaryDutyName?: string | null
  unitName?: string | null
  facilityName?: string | null
  employmentTypeName?: string | null
  status: EmployeeStatus
  statusLabel: string
  hireDate?: string | null
  profileCompletionPercent: number
  updatedAtUtc?: string | null
  personalPhone?: string | null
  corporatePhone?: string | null
  hasSpecialCondition: boolean
}

export interface EmployeeListQuery {
  search?: string
  unitId?: string
  includeSubUnits?: boolean
  facilityId?: string
  employmentTypeId?: string
  jobTitleId?: string
  jobDutyId?: string
  dutyCategory?: number | ''
  educationLevel?: number | ''
  skillId?: string
  status?: EmployeeStatus | ''
  incompleteProfileOnly?: boolean
  missingSkillsOnly?: boolean
  missingPhoneOnly?: boolean
  missingFacilityOnly?: boolean
  hasSpecialConditionOnly?: boolean
  hireYearFrom?: number | ''
  hireYearTo?: number | ''
  universityContains?: string
  graduationYearFrom?: number | ''
  graduationYearTo?: number | ''
  hasNotesOnly?: boolean
  missingCertificatesOnly?: boolean
  expiredCertificateOnly?: boolean
  page?: number
  pageSize?: number
  sortBy?: string
  sortDesc?: boolean
}

export async function fetchEmployees(query: EmployeeListQuery): Promise<PagedResult<EmployeeListItem>> {
  const params = new URLSearchParams()
  if (query.search) params.set('search', query.search)
  if (query.unitId) params.set('unitId', query.unitId)
  if (query.includeSubUnits) params.set('includeSubUnits', 'true')
  if (query.facilityId) params.set('facilityId', query.facilityId)
  if (query.employmentTypeId) params.set('employmentTypeId', query.employmentTypeId)
  if (query.jobTitleId) params.set('jobTitleId', query.jobTitleId)
  if (query.jobDutyId) params.set('jobDutyId', query.jobDutyId)
  if (query.dutyCategory) params.set('dutyCategory', String(query.dutyCategory))
  if (query.educationLevel) params.set('educationLevel', String(query.educationLevel))
  if (query.skillId) params.set('skillId', query.skillId)
  if (query.status) params.set('status', String(query.status))
  if (query.incompleteProfileOnly) params.set('incompleteProfileOnly', 'true')
  if (query.missingSkillsOnly) params.set('missingSkillsOnly', 'true')
  if (query.missingPhoneOnly) params.set('missingPhoneOnly', 'true')
  if (query.missingFacilityOnly) params.set('missingFacilityOnly', 'true')
  if (query.hasSpecialConditionOnly) params.set('hasSpecialConditionOnly', 'true')
  if (query.hireYearFrom) params.set('hireYearFrom', String(query.hireYearFrom))
  if (query.hireYearTo) params.set('hireYearTo', String(query.hireYearTo))
  if (query.universityContains) params.set('universityContains', query.universityContains)
  if (query.graduationYearFrom) params.set('graduationYearFrom', String(query.graduationYearFrom))
  if (query.graduationYearTo) params.set('graduationYearTo', String(query.graduationYearTo))
  if (query.hasNotesOnly) params.set('hasNotesOnly', 'true')
  if (query.missingCertificatesOnly) params.set('missingCertificatesOnly', 'true')
  if (query.expiredCertificateOnly) params.set('expiredCertificateOnly', 'true')
  params.set('page', String(query.page ?? 1))
  params.set('pageSize', String(query.pageSize ?? 20))
  if (query.sortBy) params.set('sortBy', query.sortBy)
  if (query.sortDesc) params.set('sortDesc', 'true')

  return apiRequest<PagedResult<EmployeeListItem>>(`/api/employees?${params.toString()}`)
}

export async function fetchEmployeeById(id: string): Promise<EmployeeDetail> {
  return apiRequest<EmployeeDetail>(`/api/employees/${id}`)
}

export interface LookupItem {
  id: string
  name: string
  code?: string | null
  parentId?: string | null
  type?: number | null
}

export interface EnumOption {
  value: number
  label: string
}

export interface EmployeeFormOptions {
  units: LookupItem[]
  facilities: LookupItem[]
  employmentTypes: LookupItem[]
  jobTitles: LookupItem[]
  duties: LookupItem[]
  skills: LookupItem[]
  managers?: LookupItem[]
  dutyCategories: EnumOption[]
  educationLevels: EnumOption[]
  genders: EnumOption[]
  statuses: EnumOption[]
}

export interface EmployeeEdit {
  id: string
  firstName: string
  lastName: string
  employeeNumber?: string | null
  birthDate?: string | null
  gender: number
  status: EmployeeStatus
  personalPhone?: string | null
  corporatePhone?: string | null
  personalEmail?: string | null
  corporateEmail?: string | null
  address?: string | null
  emergencyContactName?: string | null
  emergencyContactPhone?: string | null
  nationalId?: string | null
  canEditNationalId: boolean
  canEditAddress: boolean
  canEditPhone: boolean
  canChangeStatus: boolean
  unitId?: string | null
  facilityId?: string | null
  employmentTypeId?: string | null
  jobTitleId?: string | null
  managerEmployeeId?: string | null
  primaryJobDutyId?: string | null
  hireDate?: string | null
  directorateStartDate?: string | null
  unitStartDate?: string | null
  dutyStartDate?: string | null
}

export interface EmployeeWritePayload {
  firstName: string
  lastName: string
  employeeNumber?: string | null
  birthDate?: string | null
  gender: number
  status: EmployeeStatus
  personalPhone?: string | null
  corporatePhone?: string | null
  personalEmail?: string | null
  corporateEmail?: string | null
  address?: string | null
  emergencyContactName?: string | null
  emergencyContactPhone?: string | null
  nationalId?: string | null
  nationalIdProvided?: boolean
  unitId?: string | null
  facilityId?: string | null
  employmentTypeId?: string | null
  jobTitleId?: string | null
  managerEmployeeId?: string | null
  primaryJobDutyId?: string | null
  hireDate?: string | null
  directorateStartDate?: string | null
  unitStartDate?: string | null
  dutyStartDate?: string | null
}

export async function fetchEmployeeFormOptions(): Promise<EmployeeFormOptions> {
  return cachedGet('emp:form-options', LOOKUP_TTL_MS, () =>
    apiRequest<EmployeeFormOptions>('/api/employees/form-options'),
  )
}

export async function fetchEmployeeForEdit(id: string): Promise<EmployeeEdit> {
  return apiRequest<EmployeeEdit>(`/api/employees/${id}/edit`)
}

export async function createEmployee(payload: EmployeeWritePayload): Promise<{ id: string }> {
  const result = await apiRequest<{ id: string }>('/api/employees', {
    method: 'POST',
    body: payload,
  })
  invalidateCached('emp')
  invalidateCached('org')
  return result
}

export async function updateEmployee(id: string, payload: EmployeeWritePayload): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${id}`, {
    method: 'PUT',
    body: payload,
  })
  invalidateCached('emp')
  invalidateCached('org')
}

export interface SetEmployeeStatusPayload {
  status: EmployeeStatus
  effectiveDate: string
  reason: string
}

export async function setEmployeeStatus(
  id: string,
  payload: SetEmployeeStatusPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${id}/status`, {
    method: 'PATCH',
    body: payload,
  })
}

export async function archiveEmployee(id: string, reason: string): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${id}/archive`, {
    method: 'POST',
    body: { reason },
  })
}

export interface EducationFormOptions {
  levels: EnumOption[]
  completionStatuses: EnumOption[]
}

export interface UpsertEducationPayload {
  level: number
  university?: string | null
  faculty?: string | null
  school?: string | null
  department?: string | null
  program?: string | null
  graduationYear?: number | null
  completionStatus: number
  diplomaNumber?: string | null
  description?: string | null
}

export async function fetchEducationFormOptions(): Promise<EducationFormOptions> {
  return apiRequest<EducationFormOptions>('/api/employees/education-form-options')
}

export async function createEducation(
  employeeId: string,
  payload: UpsertEducationPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/education`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateEducation(
  employeeId: string,
  educationId: string,
  payload: UpsertEducationPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/education/${educationId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteEducation(employeeId: string, educationId: string): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/education/${educationId}`, {
    method: 'DELETE',
  })
}

export interface SkillLookup {
  id: string
  name: string
  categoryLabel: string
}

export interface SkillFormOptions {
  skills: SkillLookup[]
  levels: EnumOption[]
}

export interface UpsertEmployeeSkillPayload {
  skillId: string
  level: number
  experienceDuration?: string | null
  hasCertificate: boolean
  certificateDate?: string | null
  certificateIssuer?: string | null
  description?: string | null
}

export async function fetchSkillFormOptions(): Promise<SkillFormOptions> {
  return apiRequest<SkillFormOptions>('/api/employees/skill-form-options')
}

export async function createEmployeeSkill(
  employeeId: string,
  payload: UpsertEmployeeSkillPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/skills`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateEmployeeSkill(
  employeeId: string,
  employeeSkillId: string,
  payload: UpsertEmployeeSkillPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/skills/${employeeSkillId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteEmployeeSkill(
  employeeId: string,
  employeeSkillId: string,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/skills/${employeeSkillId}`, {
    method: 'DELETE',
  })
}

export interface DutyLookup {
  id: string
  name: string
  categoryLabel: string
}

export interface AssignmentFormOptions {
  duties: DutyLookup[]
}

export interface UpsertAssignmentPayload {
  jobDutyId: string
  isPrimary: boolean
  startDate: string
  endDate?: string | null
  description?: string | null
}

export async function fetchAssignmentFormOptions(): Promise<AssignmentFormOptions> {
  return apiRequest<AssignmentFormOptions>('/api/employees/assignment-form-options')
}

export async function createAssignment(
  employeeId: string,
  payload: UpsertAssignmentPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/assignments`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateAssignment(
  employeeId: string,
  assignmentId: string,
  payload: UpsertAssignmentPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/assignments/${assignmentId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteAssignment(employeeId: string, assignmentId: string): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/assignments/${assignmentId}`, {
    method: 'DELETE',
  })
}

export interface MovementFormOptions {
  movementTypes: EnumOption[]
  units: LookupItem[]
  facilities: LookupItem[]
  jobTitles: LookupItem[]
  duties: DutyLookup[]
}

export interface CreateMovementPayload {
  movementType: number
  oldUnitId?: string | null
  newUnitId?: string | null
  oldFacilityId?: string | null
  newFacilityId?: string | null
  oldJobTitleId?: string | null
  newJobTitleId?: string | null
  oldJobDutyId?: string | null
  newJobDutyId?: string | null
  startDate: string
  endDate?: string | null
  reason?: string | null
  description?: string | null
  approvedBy?: string | null
}

export async function fetchMovementFormOptions(): Promise<MovementFormOptions> {
  return apiRequest<MovementFormOptions>('/api/employees/movement-form-options')
}

export async function createMovement(
  employeeId: string,
  payload: CreateMovementPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/movements`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateMovement(
  employeeId: string,
  movementId: string,
  payload: CreateMovementPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/movements/${movementId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteMovement(employeeId: string, movementId: string): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/movements/${movementId}`, {
    method: 'DELETE',
  })
}

export interface CertificateFormOptions {
  definitions: LookupItem[]
  skills: LookupItem[]
}

export interface UpsertCertificatePayload {
  certificateDefinitionId?: string | null
  name: string
  issuer?: string | null
  category?: string | null
  issuedOn?: string | null
  expiresOn?: string | null
  documentNumber?: string | null
  description?: string | null
  relatedSkillId?: string | null
}

export async function fetchCertificateFormOptions(): Promise<CertificateFormOptions> {
  return apiRequest<CertificateFormOptions>('/api/employees/certificate-form-options')
}

export async function createCertificate(
  employeeId: string,
  payload: UpsertCertificatePayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/certificates`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateCertificate(
  employeeId: string,
  certificateId: string,
  payload: UpsertCertificatePayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/certificates/${certificateId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteCertificate(employeeId: string, certificateId: string): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/certificates/${certificateId}`, {
    method: 'DELETE',
  })
}

export interface NoteFormOptions {
  categories: EnumOption[]
  visibilities: EnumOption[]
}

export interface UpsertNotePayload {
  title: string
  category: number
  content: string
  visibility: number
  reminderDate?: string | null
}

export async function fetchNoteFormOptions(): Promise<NoteFormOptions> {
  return apiRequest<NoteFormOptions>('/api/employees/note-form-options')
}

export async function createNote(
  employeeId: string,
  payload: UpsertNotePayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/notes`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateNote(
  employeeId: string,
  noteId: string,
  payload: UpsertNotePayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/notes/${noteId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteNote(employeeId: string, noteId: string): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/notes/${noteId}`, {
    method: 'DELETE',
  })
}

export interface SpecialConditionFormOptions {
  suggestedTypes: string[]
}

export interface UpsertSpecialConditionPayload {
  conditionType: string
  description: string
  startDate?: string | null
  endDate?: string | null
  isPermanent: boolean
  requiresDutyAdjustment: boolean
  requiresWorkspaceAdjustment: boolean
}

export async function fetchSpecialConditionFormOptions(): Promise<SpecialConditionFormOptions> {
  return apiRequest<SpecialConditionFormOptions>('/api/employees/special-condition-form-options')
}

export async function createSpecialCondition(
  employeeId: string,
  payload: UpsertSpecialConditionPayload,
): Promise<{ id: string }> {
  return apiRequest<{ id: string }>(`/api/employees/${employeeId}/special-conditions`, {
    method: 'POST',
    body: payload,
  })
}

export async function updateSpecialCondition(
  employeeId: string,
  conditionId: string,
  payload: UpsertSpecialConditionPayload,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/special-conditions/${conditionId}`, {
    method: 'PUT',
    body: payload,
  })
}

export async function deleteSpecialCondition(
  employeeId: string,
  conditionId: string,
): Promise<void> {
  await apiRequest<unknown>(`/api/employees/${employeeId}/special-conditions/${conditionId}`, {
    method: 'DELETE',
  })
}

export async function uploadSpecialConditionDocument(
  employeeId: string,
  conditionId: string,
  file: File,
): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  await apiUpload(`/api/employees/${employeeId}/special-conditions/${conditionId}/document`, form)
}

export async function removeSpecialConditionDocument(
  employeeId: string,
  conditionId: string,
): Promise<void> {
  await apiRequest<unknown>(
    `/api/employees/${employeeId}/special-conditions/${conditionId}/document`,
    { method: 'DELETE' },
  )
}

export async function downloadSpecialConditionDocument(
  employeeId: string,
  conditionId: string,
): Promise<void> {
  await apiDownloadFile(
    `/api/employees/${employeeId}/special-conditions/${conditionId}/document`,
  )
}

export async function uploadEmployeePhoto(employeeId: string, file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  await apiUpload(`/api/employees/${employeeId}/photo`, form)
}

export async function deleteEmployeePhoto(employeeId: string): Promise<void> {
  await apiRequest(`/api/employees/${employeeId}/photo`, { method: 'DELETE' })
}

export interface EmployeeDetail {
  id: string
  firstName: string
  lastName: string
  fullName: string
  employeeNumber?: string | null
  hasPhoto?: boolean
  status: EmployeeStatus
  statusLabel: string
  profileCompletionPercent: number
  general: {
    birthDate?: string | null
    genderLabel: string
    personalPhone?: string | null
    corporatePhone?: string | null
    personalEmail?: string | null
    corporateEmail?: string | null
    address?: string | null
    emergencyContactName?: string | null
    emergencyContactPhone?: string | null
    nationalIdDisplay?: string | null
    nationalIdIsMasked: boolean
  }
  corporate: {
    unitId?: string | null
    directorateName?: string | null
    mainUnitName?: string | null
    subUnitName?: string | null
    unitName?: string | null
    facilityId?: string | null
    facilityName?: string | null
    employmentTypeName?: string | null
    jobTitleName?: string | null
    primaryDutyName?: string | null
    primaryDutyCategoryLabel?: string | null
    managerName?: string | null
    unitSupervisorName?: string | null
    hireDate?: string | null
    directorateStartDate?: string | null
    unitStartDate?: string | null
    dutyStartDate?: string | null
  }
  assignments: Array<{
    id: string
    jobDutyId: string
    dutyName: string
    categoryLabel: string
    isPrimary: boolean
    startDate: string
    endDate?: string | null
    description?: string | null
  }>
  education: Array<{
    id: string
    level: number
    levelLabel: string
    university?: string | null
    faculty?: string | null
    school?: string | null
    department?: string | null
    program?: string | null
    graduationYear?: number | null
    completionStatus: number
    completionStatusLabel: string
    diplomaNumber?: string | null
    description?: string | null
  }>
  skills: Array<{
    id: string
    skillId: string
    name: string
    categoryLabel: string
    level: number
    levelLabel: string
    experienceDuration?: string | null
    hasCertificate: boolean
    certificateDate?: string | null
    certificateIssuer?: string | null
    description?: string | null
  }>
  certificates: Array<{
    id: string
    certificateDefinitionId?: string | null
    name: string
    issuer?: string | null
    category?: string | null
    issuedOn?: string | null
    expiresOn?: string | null
    documentNumber?: string | null
    description?: string | null
    relatedSkillId?: string | null
    relatedSkillName?: string | null
  }>
  movements: Array<{
    id: string
    movementType: number
    movementTypeLabel: string
    oldUnitId?: string | null
    oldUnitName?: string | null
    newUnitId?: string | null
    newUnitName?: string | null
    oldFacilityId?: string | null
    oldFacilityName?: string | null
    newFacilityId?: string | null
    newFacilityName?: string | null
    oldJobTitleId?: string | null
    oldJobTitleName?: string | null
    newJobTitleId?: string | null
    newJobTitleName?: string | null
    oldJobDutyId?: string | null
    oldJobDutyName?: string | null
    newJobDutyId?: string | null
    newJobDutyName?: string | null
    startDate: string
    endDate?: string | null
    reason?: string | null
    description?: string | null
    approvedBy?: string | null
    createdBy?: string | null
  }>
  notes: Array<{
    id: string
    title: string
    category: number
    categoryLabel: string
    content: string
    noteDateUtc: string
    visibility: number
    visibilityLabel: string
    reminderDate?: string | null
    createdBy?: string | null
    canModify: boolean
  }>
  hasSpecialCondition: boolean
  specialConditions?: Array<{
    id: string
    conditionType: string
    description: string
    startDate?: string | null
    endDate?: string | null
    isPermanent: boolean
    requiresDutyAdjustment: boolean
    requiresWorkspaceAdjustment: boolean
    hasDocument: boolean
  }> | null
}
