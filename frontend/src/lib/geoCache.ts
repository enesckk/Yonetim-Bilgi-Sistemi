import type { MahalleCollection } from '@/lib/geo'

type BoundaryGeo = GeoJSON.GeoJsonObject

let boundaryPromise: Promise<BoundaryGeo | null> | null = null
let mahallePromise: Promise<MahalleCollection | null> | null = null

async function fetchJson<T>(url: string): Promise<T | null> {
  const res = await fetch(url, { cache: 'force-cache' })
  if (!res.ok) return null
  return (await res.json()) as T
}

/** GeoJSON bir kez çekilir; harita sayfaları arasında bellekte kalır. */
export function loadDistrictBoundary() {
  boundaryPromise ??= fetchJson<BoundaryGeo>('/geo/sehitkamil-boundary.geojson')
  return boundaryPromise
}

export function loadMahalleler() {
  mahallePromise ??= fetchJson<MahalleCollection>('/geo/sehitkamil-mahalleler.geojson')
  return mahallePromise
}
