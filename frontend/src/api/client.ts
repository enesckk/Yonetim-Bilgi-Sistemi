import type { ApiResponse } from './types'

/**
 * Access token yalnızca bellek'te (module scope) tutulur.
 * localStorage / sessionStorage kullanılmaz → XSS ile kalıcı çalınmaz.
 * Sayfa yenilenince refresh cookie ile yeniden alınır.
 */
let accessTokenMemory: string | null = null

export const SESSION_EXPIRED_CODE = 'SESSION_EXPIRED'
export const SESSION_EXPIRED_MESSAGE =
  'Oturum süreniz doldu. Lütfen tekrar giriş yapın.'
export const AUTH_NOTICE_KEY = 'auth_notice'
export const AUTH_NOTICE_SESSION_EXPIRED = 'session_expired'

export function getAccessToken(): string | null {
  return accessTokenMemory
}

export function setAccessToken(token: string | null): void {
  accessTokenMemory = token
  if (token) sessionExpiredNotified = false
}

export class ApiClientError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly traceId?: string,
    public readonly validationErrors?: Record<string, string[]>,
  ) {
    super(message)
    this.name = 'ApiClientError'
  }
}

export function isSessionExpiredError(err: unknown): boolean {
  return err instanceof ApiClientError && err.code === SESSION_EXPIRED_CODE
}

type SessionExpiredListener = () => void
let sessionExpiredListener: SessionExpiredListener | null = null
let sessionExpiredNotified = false

/** AuthProvider oturumu temizleyip login'e düşürmek için dinler. */
export function setSessionExpiredListener(listener: SessionExpiredListener | null): void {
  sessionExpiredListener = listener
}

export function markSessionExpiredNotice(): void {
  try {
    sessionStorage.setItem(AUTH_NOTICE_KEY, AUTH_NOTICE_SESSION_EXPIRED)
  } catch {
    /* private mode vb. */
  }
}

function notifySessionExpired(): void {
  setAccessToken(null)
  markSessionExpiredNotice()
  if (sessionExpiredNotified) return
  sessionExpiredNotified = true
  sessionExpiredListener?.()
}

function throwSessionExpired(traceId?: string): never {
  notifySessionExpired()
  throw new ApiClientError(401, SESSION_EXPIRED_CODE, SESSION_EXPIRED_MESSAGE, traceId)
}

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: unknown
  skipAuth?: boolean
  /** 401 sonrası tek seferlik yenileme denemesi yapıldı mı */
  _retried?: boolean
}

let refreshPromise: Promise<boolean> | null = null

async function tryRefresh(): Promise<boolean> {
  // Aynı anda birden fazla 401 olursa tek refresh çağrısı yapılsın
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const res = await fetch('/api/auth/refresh', {
          method: 'POST',
          credentials: 'include', // HttpOnly refresh cookie gönderilir
        })
        if (!res.ok) {
          setAccessToken(null)
          return false
        }
        const json = (await res.json()) as ApiResponse<{ accessToken: string }>
        if (!json.success || !json.data?.accessToken) {
          setAccessToken(null)
          return false
        }
        setAccessToken(json.data.accessToken)
        return true
      } catch {
        setAccessToken(null)
        return false
      } finally {
        refreshPromise = null
      }
    })()
  }
  return refreshPromise
}

function isAuthEndpoint(path: string): boolean {
  return path.includes('/api/auth/login') || path.includes('/api/auth/refresh') || path.includes('/api/auth/logout')
}

function shouldAttemptRefresh(path: string, options: { skipAuth?: boolean; _retried?: boolean }): boolean {
  return !options.skipAuth && !options._retried && !isAuthEndpoint(path)
}

async function readApiError(
  response: Response,
  fallbackMessage: string,
): Promise<{ code: string; message: string; traceId?: string; validationErrors?: Record<string, string[]> }> {
  try {
    const json = (await response.json()) as ApiResponse<unknown>
    return {
      code: json.error?.code ?? 'UNEXPECTED_ERROR',
      message: json.error?.message ?? fallbackMessage,
      traceId: json.traceId,
      validationErrors: json.error?.validationErrors,
    }
  } catch {
    return { code: 'UNEXPECTED_ERROR', message: fallbackMessage }
  }
}

/**
 * 401 geldiğinde refresh dene; başarısızsa oturum düştü hatası fırlat.
 * true → çağıran isteği yeniden denemeli. false → 401 auth endpoint / skipAuth, normal hata yoluna devam.
 */
async function recoverFromUnauthorized(
  path: string,
  options: { skipAuth?: boolean; _retried?: boolean },
): Promise<boolean> {
  if (!shouldAttemptRefresh(path, options)) {
    // Zaten yenileme denendi veya korumalı yol — oturum düşmüş say
    if (!options.skipAuth && options._retried && !isAuthEndpoint(path)) {
      throwSessionExpired()
    }
    return false
  }
  const refreshed = await tryRefresh()
  if (refreshed) return true
  throwSessionExpired()
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body !== undefined && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  if (!options.skipAuth) {
    const token = getAccessToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, {
    ...options,
    cache: options.cache ?? 'no-store',
    headers,
    credentials: 'include',
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (response.status === 401) {
    const shouldRetry = await recoverFromUnauthorized(path, options)
    if (shouldRetry) {
      return apiRequest<T>(path, { ...options, _retried: true })
    }
  }

  const json = (await response.json().catch(() => null)) as ApiResponse<T> | null

  if (!response.ok || !json?.success) {
    // Login vb. 401: kimlik hatası mesajını koru; oturum düşmesi değil
    throw new ApiClientError(
      response.status,
      json?.error?.code ?? 'UNEXPECTED_ERROR',
      json?.error?.message ?? 'İstek başarısız oldu.',
      json?.traceId,
      json?.error?.validationErrors,
    )
  }

  return json.data as T
}

/**
 * multipart/form-data — Content-Type set etme (boundary tarayıcı ekler).
 * JSON ApiResponse bekler.
 */
export async function apiUploadJson<T>(
  path: string,
  formData: FormData,
  options: { skipAuth?: boolean; _retried?: boolean } = {},
): Promise<T> {
  const headers = new Headers()
  if (!options.skipAuth) {
    const token = getAccessToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, {
    method: 'POST',
    headers,
    credentials: 'include',
    body: formData,
  })

  if (response.status === 401) {
    const shouldRetry = await recoverFromUnauthorized(path, options)
    if (shouldRetry) {
      return apiUploadJson<T>(path, formData, { ...options, _retried: true })
    }
  }

  const json = (await response.json().catch(() => null)) as ApiResponse<T> | null
  if (!response.ok || !json?.success) {
    throw new ApiClientError(
      response.status,
      json?.error?.code ?? 'UNEXPECTED_ERROR',
      json?.error?.message ?? 'Yükleme başarısız oldu.',
      json?.traceId,
      json?.error?.validationErrors,
    )
  }

  return json.data as T
}

/**
 * multipart/form-data — Content-Type set etme (boundary tarayıcı ekler).
 */
export async function apiUpload(
  path: string,
  formData: FormData,
  options: { skipAuth?: boolean; _retried?: boolean } = {},
): Promise<void> {
  await apiUploadJson<unknown>(path, formData, options)
}

/**
 * Yetkili dosya okuma — blob URL (img src için). Çağıran revoke etmeli.
 */
export async function apiFetchBlob(
  path: string,
  options: { skipAuth?: boolean; _retried?: boolean } = {},
): Promise<Blob> {
  const headers = new Headers()
  if (!options.skipAuth) {
    const token = getAccessToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, {
    method: 'GET',
    headers,
    credentials: 'include',
  })

  if (response.status === 401) {
    const shouldRetry = await recoverFromUnauthorized(path, options)
    if (shouldRetry) {
      return apiFetchBlob(path, { ...options, _retried: true })
    }
  }

  if (!response.ok) {
    const err = await readApiError(response, 'Dosya okunamadı.')
    throw new ApiClientError(response.status, err.code, err.message, err.traceId)
  }

  return response.blob()
}

/**
 * Yetkili dosya indirme — JSON değil, blob.
 * body verilirse JSON POST olarak gönderilir (rapor oluşturucu exportları).
 */
export async function apiDownloadFile(
  path: string,
  options: { skipAuth?: boolean; _retried?: boolean; method?: string; body?: unknown } = {},
): Promise<void> {
  const headers = new Headers()
  if (options.body !== undefined) headers.set('Content-Type', 'application/json')
  if (!options.skipAuth) {
    const token = getAccessToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, {
    method: options.method ?? (options.body !== undefined ? 'POST' : 'GET'),
    headers,
    credentials: 'include',
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })

  if (response.status === 401) {
    const shouldRetry = await recoverFromUnauthorized(path, options)
    if (shouldRetry) {
      return apiDownloadFile(path, { ...options, _retried: true })
    }
  }

  if (!response.ok) {
    const err = await readApiError(response, 'Dosya indirilemedi.')
    throw new ApiClientError(response.status, err.code, err.message, err.traceId)
  }

  const blob = await response.blob()
  const disposition = response.headers.get('Content-Disposition')
  let fileName = 'belge'
  if (disposition) {
    const utf = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
    const plain = /filename="?([^";]+)"?/i.exec(disposition)
    if (utf?.[1]) fileName = decodeURIComponent(utf[1])
    else if (plain?.[1]) fileName = plain[1]
  }

  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}
