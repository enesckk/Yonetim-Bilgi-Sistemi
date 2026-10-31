import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { pointInGeometry, type MahalleCollection } from './geo'
import samples from './mapGeometry.samples.json'
import gaps from './mapGeometry.gaps.json'

const map = JSON.parse(readFileSync(new URL('../../public/geo/sehitkamil-mahalleler.geojson', import.meta.url), 'utf8')) as MahalleCollection
const activeCodes = new Set(samples.map(sample => sample.id))
const active = map.features.filter(feature => activeCodes.has(feature.properties.id))

describe('verified 120-neighborhood map', () => {
  it('keeps every active neighborhood matched to its existing unique identifier', () => {
    expect(samples).toHaveLength(120)
    expect(active).toHaveLength(120)
    expect(new Set(active.map(feature => feature.properties.id)).size).toBe(120)
  })

  it('locates each verified village/neighborhood point inside only its own boundary', () => {
    for (const sample of samples) {
      const matches = active.filter(feature => pointInGeometry(sample.lat, sample.lng, feature.geometry))
      expect(matches.map(feature => feature.properties.id), sample.name).toEqual([sample.id])
    }
  })

  it('covers all 24 previously blank interior areas with the verified neighborhood', () => {
    expect(gaps).toHaveLength(24)
    for (const point of gaps) {
      expect(active.filter(feature => pointInGeometry(point.lat, point.lng, feature.geometry))
        .map(feature => feature.properties.id)).toEqual([point.id])
    }
  })

  it('retains inactive legacy identifiers for database relationships', () => {
    expect(map.features).toHaveLength(149)
    expect(new Set(map.features.map(feature => feature.properties.id)).size).toBe(149)
  })
})
