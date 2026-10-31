# Şehitkamil neighborhood boundaries

The 120 active neighborhood geometries were verified against the authenticated
`https://ybs.sehitkamil.bel.tr/api/api/Neighborhoods/geojson` response on 2026-10-31.
The source identifies OpenStreetMap contributors and ODbL as the data origin/license.
Only geometry and existing public neighborhood identifiers/names are included here;
source contact details are not copied into the public GeoJSON.

Existing identifiers are preserved so population, events and other database
relationships stay linked. The file also retains 29 inactive legacy geometries
because database initialization and historical references still use their codes.
`EventsMapPage` draws only identifiers returned by the active settlement catalog;
inactive legacy shapes must not be drawn over the verified boundaries.

Geometry QA: 120 valid active geometries, no overlapping interior areas and no
interior gaps in their combined coverage. All 120 verified interior points and
24 formerly blank locations have regression checks in `mapGeometry.test.ts`.
This comparison verifies agreement with the supplied source, not a cadastral survey.

After changing geometry, update the GeoJSON cache version in `geoCache.ts` so
existing browser caches cannot continue displaying old boundaries.
