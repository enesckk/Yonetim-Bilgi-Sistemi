/** Minimal GeoJSON helpers (point-in-polygon) without extra deps. */

export type GeoRing = [number, number][]
export type GeoPolygon = GeoRing[]
export type GeoMultiPolygon = GeoPolygon[]

export type SimpleGeometry =
  | { type: 'Polygon'; coordinates: GeoPolygon }
  | { type: 'MultiPolygon'; coordinates: GeoMultiPolygon }

export interface MahalleFeatureProps {
  id: string
  name: string
}

export interface MahalleFeature {
  type: 'Feature'
  properties: MahalleFeatureProps
  geometry: SimpleGeometry
}

export interface MahalleCollection {
  type: 'FeatureCollection'
  features: MahalleFeature[]
}

function pointInRing(lng: number, lat: number, ring: GeoRing): boolean {
  let inside = false
  for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
    const [xi, yi] = ring[i]
    const [xj, yj] = ring[j]
    const intersect =
      yi > lat !== yj > lat && lng < ((xj - xi) * (lat - yi)) / (yj - yi + Number.EPSILON) + xi
    if (intersect) inside = !inside
  }
  return inside
}

export function pointInGeometry(lat: number, lng: number, geometry: SimpleGeometry): boolean {
  if (geometry.type === 'Polygon') {
    const [outer, ...holes] = geometry.coordinates
    if (!pointInRing(lng, lat, outer)) return false
    return !holes.some((hole) => pointInRing(lng, lat, hole))
  }
  return geometry.coordinates.some((poly) => {
    const [outer, ...holes] = poly
    if (!pointInRing(lng, lat, outer)) return false
    return !holes.some((hole) => pointInRing(lng, lat, hole))
  })
}

/** Stable pastel-ish fill from id/name for choropleth tiles. */
export function mahalleFillColor(seed: string): string {
  let h = 0
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0
  const hue = h % 360
  const sat = 42 + (h % 18)
  const light = 58 + (h % 12)
  return `hsl(${hue} ${sat}% ${light}%)`
}

const SCHOOL_RE = /\b(okul|i\.?\s*ö\.?\s*o|ortaokul|lise|anaokulu|kreş|kres)\b/i
const CULTURE_RE = /\b(kültür|kultur|sanat|tiyatro|müze|muze|sahne|kütüphane|kutuphane)\b/i

export function isLikelySchool(title: string, subtitle?: string | null, categoryName?: string | null): boolean {
  if (categoryName && /okul/i.test(categoryName)) return true
  const text = `${title} ${subtitle ?? ''}`
  return SCHOOL_RE.test(text)
}

export function isLikelyCulture(
  title: string,
  subtitle?: string | null,
  categoryName?: string | null,
): boolean {
  if (categoryName && CULTURE_RE.test(categoryName)) return true
  const text = `${title} ${subtitle ?? ''}`
  return CULTURE_RE.test(text)
}

