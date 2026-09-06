import { apiRequest } from '@/api/client'

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
  const data = await apiRequest<GeocodeLookupDto>(`/api/map/geocode?q=${encodeURIComponent(q)}`)
  return mapHit(data)
}

/** Koordinat → adres. */
export async function reverseGeocode(latitude: number, longitude: number): Promise<GeocodeResult | null> {
  if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) return null
  const qs = new URLSearchParams({
    lat: String(latitude),
    lng: String(longitude),
  })
  const data = await apiRequest<GeocodeLookupDto>(`/api/map/reverse?${qs}`)
  return mapHit(data)
}
