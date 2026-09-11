const store = new Map<string, { exp: number; value: unknown }>()
const inflight = new Map<string, Promise<unknown>>()
const epoch = new Map<string, number>()
let generation = 0

function matchesPrefix(key: string, prefix: string) {
  return key === prefix || key.startsWith(`${prefix}:`)
}

function bump(key: string) {
  epoch.set(key, (epoch.get(key) ?? 0) + 1)
}

export function cachedGet<T>(key: string, ttlMs: number, loader: () => Promise<T>): Promise<T> {
  const now = Date.now()
  const hit = store.get(key)
  if (hit && hit.exp > now) return Promise.resolve(hit.value as T)

  const pending = inflight.get(key)
  if (pending) return pending as Promise<T>

  const gen = epoch.get(key) ?? 0
  const wave = generation
  const run = loader()
    .then((value) => {
      if ((epoch.get(key) ?? 0) === gen && generation === wave)
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
    if (matchesPrefix(key, prefix)) {
      store.delete(key)
      bump(key)
    }
  }
  for (const key of [...inflight.keys()]) {
    if (matchesPrefix(key, prefix)) {
      inflight.delete(key)
      bump(key)
    }
  }
}

export function clearLookupCache() {
  store.clear()
  inflight.clear()
  epoch.clear()
  generation += 1
}

export const LOOKUP_TTL_MS = 2 * 60_000
export const MAP_TTL_MS = 90_000
export const SETTLEMENT_TTL_MS = 3 * 60_000
