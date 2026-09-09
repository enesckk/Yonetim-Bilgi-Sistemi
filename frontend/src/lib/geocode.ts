import { apiRequest } from '@/api/client'
import { cachedGet } from '@/lib/lookupCache'

const GEO_TTL_MS = 6 * 60 * 60 * 1000

export type GeocodeResult = {
  latitude: number
  longitude: number
  displayName: string
}

type GeocodeLookupDto = {
  found: boolean
  result?: {
    latitude: number
    longitude: number
    displayName: string
  } | null
}

function mapHit(dto: GeocodeLookupDto): GeocodeResult | null {
  if (!dto.found || !dto.result) return null
  return {
    latitude: dto.result.latitude,
    longitude: dto.result.longitude,
    displayName: dto.result.displayName,
  }
}

/** Adres → koordinat (API → Nominatim). */
export async function geocodeAddress(query: string): Promise<GeocodeResult | null> {
  const q = query.trim()
  if (q.length < 3) return null
  const data = await cachedGet(`geo:q:${q.toLocaleLowerCase('tr-TR')}`, GEO_TTL_MS, () =>
    apiRequest<GeocodeLookupDto>(`/api/map/geocode?q=${encodeURIComponent(q)}`),
  )
  return mapHit(data)
}

/** Yazarak konum önerileri. Hata durumunda boş dizi döner. */
export async function suggestPlaces(query: string): Promise<GeocodeResult[]> {
  const q = query.trim()
  if (q.length < 3) return []
  try {
    const data = await cachedGet(`geo:suggest:${q.toLocaleLowerCase('tr-TR')}`, GEO_TTL_MS, () =>
      apiRequest<{ results?: GeocodeResult[] }>(
        `/api/map/geocode/suggest?q=${encodeURIComponent(q)}`,
      ),
    )
    return (data.results ?? []).filter(
      (x) => Number.isFinite(x.latitude) && Number.isFinite(x.longitude) && x.displayName,
    )
  } catch {
    return []
  }
}

/** Koordinat → adres. */
export async function reverseGeocode(latitude: number, longitude: number): Promise<GeocodeResult | null> {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null
  const qs = new URLSearchParams({
    lat: String(latitude),
    lng: String(longitude),
  })
  const data = await cachedGet(`geo:rev:${qs}`, GEO_TTL_MS, () =>
    apiRequest<GeocodeLookupDto>(`/api/map/reverse?${qs}`),
  )
  return mapHit(data)
}
