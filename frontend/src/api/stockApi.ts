import { apiRequest } from './client'

export type StockCategory =
  | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 99
export type StockUnit = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9
export type StockMovementType = 1 | 2 | 3 | 4

export interface StockLookup {
  value: number
  label: string
}

export interface StockLocationOption {
  id: string
  name: string
  typeLabel: string
}

export interface StockOptions {
  categories: StockLookup[]
  units: StockLookup[]
  movementTypes: StockLookup[]
  locations: StockLocationOption[]
}

export interface StockItemRow {
  id: string
  name: string
  code?: string | null
  category: StockCategory
  categoryLabel: string
  unit: StockUnit
  unitLabel: string
  brand?: string | null
  model?: string | null
  minQuantity: number
  totalQuantity: number
  locationCount: number
  isLow: boolean
  isActive: boolean
}

export interface StockBalanceLine {
  locationId: string
  locationName: string
  locationTypeLabel: string
  quantity: number
  minQuantity: number
  isLow: boolean
  notes?: string | null
}

export interface StockItemDetail {
  id: string
  name: string
  code?: string | null
  category: StockCategory
  categoryLabel: string
  unit: StockUnit
  unitLabel: string
  brand?: string | null
  model?: string | null
  description?: string | null
  minQuantity: number
  totalQuantity: number
  isActive: boolean
  balances: StockBalanceLine[]
}

export interface StockLocationCard {
  id: string
  name: string
  code?: string | null
  typeLabel: string
  itemCount: number
  totalQuantity: number
  lowCount: number
}

export interface StockLocationLine {
  stockItemId: string
  name: string
  code?: string | null
  category: StockCategory
  categoryLabel: string
  unit: StockUnit
  unitLabel: string
  brand?: string | null
  model?: string | null
  quantity: number
  minQuantity: number
  isLow: boolean
}

export interface StockLocationDetail {
  id: string
  name: string
  code?: string | null
  typeLabel: string
  itemCount: number
  lowCount: number
  lines: StockLocationLine[]
}

export interface StockMovementRow {
  id: string
  stockItemId: string
  itemName: string
  itemCode?: string | null
  unitLabel: string
  movementType: StockMovementType
  movementTypeLabel: string
  fromLocationId?: string | null
  fromLocationName?: string | null
  toLocationId?: string | null
  toLocationName?: string | null
  quantity: number
  occurredOn: string
  reason?: string | null
  createdAtUtc: string
}

export interface StockLowLine {
  stockItemId: string
  locationId: string
  itemName: string
  locationName: string
  unitLabel: string
  quantity: number
  minQuantity: number
}

export interface StockSummary {
  catalogCount: number
  locationCount: number
  locationsWithStock: number
  lowCount: number
  locations: StockLocationCard[]
  lowStock: StockLowLine[]
  recentMovements: StockMovementRow[]
}

export interface UpsertStockItemPayload {
  name: string
  code?: string | null
  category: StockCategory
  unit: StockUnit
  brand?: string | null
  model?: string | null
  description?: string | null
  minQuantity: number
  isActive: boolean
}

export interface CreateStockMovementPayload {
  stockItemId: string
  movementType: StockMovementType
  fromLocationId?: string | null
  toLocationId?: string | null
  quantity: number
  occurredOn?: string | null
  reason?: string | null
}

export function fmtQty(n: number) {
  return n.toLocaleString('tr-TR', { maximumFractionDigits: 2 })
}

export function fetchStockOptions() {
  return apiRequest<StockOptions>('/api/stock/options')
}

export function fetchStockSummary() {
  return apiRequest<StockSummary>('/api/stock/summary')
}

export function fetchStockItems(params?: {
  search?: string
  category?: number | ''
  includeInactive?: boolean
}) {
  const q = new URLSearchParams()
  if (params?.search) q.set('search', params.search)
  if (params?.category) q.set('category', String(params.category))
  if (params?.includeInactive) q.set('includeInactive', 'true')
  const suffix = q.size ? `?${q.toString()}` : ''
  return apiRequest<StockItemRow[]>(`/api/stock/items${suffix}`)
}

export function fetchStockItem(id: string) {
  return apiRequest<StockItemDetail>(`/api/stock/items/${id}`)
}

export function createStockItem(body: UpsertStockItemPayload) {
  return apiRequest<StockItemDetail>('/api/stock/items', { method: 'POST', body })
}

export function updateStockItem(id: string, body: UpsertStockItemPayload) {
  return apiRequest<StockItemDetail>(`/api/stock/items/${id}`, { method: 'PUT', body })
}

export function archiveStockItem(id: string) {
  return apiRequest<unknown>(`/api/stock/items/${id}`, { method: 'DELETE' })
}

export function fetchStockLocation(id: string) {
  return apiRequest<StockLocationDetail>(`/api/stock/locations/${id}`)
}

export function fetchStockMovements(params?: { itemId?: string; locationId?: string; take?: number }) {
  const q = new URLSearchParams()
  if (params?.itemId) q.set('itemId', params.itemId)
  if (params?.locationId) q.set('locationId', params.locationId)
  if (params?.take) q.set('take', String(params.take))
  const suffix = q.size ? `?${q.toString()}` : ''
  return apiRequest<StockMovementRow[]>(`/api/stock/movements${suffix}`)
}

export function createStockMovement(body: CreateStockMovementPayload) {
  return apiRequest<StockMovementRow>('/api/stock/movements', { method: 'POST', body })
}
