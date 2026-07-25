import { apiRequest } from './client'
import type { PagedResult } from './types'

export interface AuditLogItem {
  id: string
  occurredAtUtc: string
  userName?: string | null
  action: string
  entityName: string
  entityId?: string | null
  oldValuesJson?: string | null
  newValuesJson?: string | null
  ipAddress?: string | null
}

export interface AuditLogQuery {
  page?: number
  pageSize?: number
  entityName?: string
  action?: string
  userName?: string
  search?: string
  from?: string
  to?: string
}

export async function fetchAuditLogs(query: AuditLogQuery = {}): Promise<PagedResult<AuditLogItem>> {
  const params = new URLSearchParams()
  if (query.page) params.set('page', String(query.page))
  if (query.pageSize) params.set('pageSize', String(query.pageSize))
  if (query.entityName) params.set('entityName', query.entityName)
  if (query.action) params.set('action', query.action)
  if (query.userName) params.set('userName', query.userName)
  if (query.search) params.set('search', query.search)
  if (query.from) params.set('from', query.from)
  if (query.to) params.set('to', query.to)
  const qs = params.toString()
  return apiRequest<PagedResult<AuditLogItem>>(`/api/audit-logs${qs ? `?${qs}` : ''}`)
}
