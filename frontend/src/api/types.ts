/** Standart API zarfı — backend ApiResponse ile birebir. */
export interface ApiError {
  code: string
  message: string
  validationErrors?: Record<string, string[]>
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  error?: ApiError
  traceId?: string
  timestampUtc: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface CurrentUser {
  id: string
  userName: string
  displayName: string
  email: string
  employeeId?: string | null
  hasPhoto?: boolean
  roles: string[]
  roleNames?: string[]
  permissions: string[]
}

export interface SessionPolicy {
  idleTimeoutMinutes: number
  accessTokenMinutes: number
  absoluteSessionDays: number
}

export interface LoginResponse {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: CurrentUser
  sessionPolicy: SessionPolicy
}
