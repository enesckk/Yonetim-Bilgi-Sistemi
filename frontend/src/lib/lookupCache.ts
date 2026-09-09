const store = new Map<string, { exp: number; value: unknown }>()
const inflight = new Map<string, Promise<unknown>>()

export function cachedGet<T>(key: string, ttlMs: number, loader: () => Promise<T>): Promise<T> {
  const now = Date.now()
  const hit = store.get(key)
  if (hit && hit.exp > now) return Promise.resolve(hit.value as T)

  const pending = inflight.get(key)
  if (pending) return pending as Promise<T>

  const run = loader()
    .then((value) => {
      store.set(key, { exp: Date.now() + ttlMs, value })
      return value
    })
    .finally(() => {
      inflight.delete(key)
    })

  inflight.set(key, run)
  return run
}

export function invalidateCached(prefix: string) {
  for (const key of [...store.keys()]) {
    if (key === prefix || key.startsWith(`${prefix}:`) || key.startsWith(prefix)) store.delete(key)
  }
  for (const key of [...inflight.keys()]) {
    if (key === prefix || key.startsWith(`${prefix}:`) || key.startsWith(prefix)) inflight.delete(key)
  }
}

export function clearLookupCache() {
  store.clear()
  inflight.clear()
}

export const LOOKUP_TTL_MS = 2 * 60_000
export const MAP_TTL_MS = 90_000
export const SETTLEMENT_TTL_MS = 3 * 60_000
