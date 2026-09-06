import L from 'leaflet'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'

/** Vite/Webpack under Leaflet default icon paths; call once before Marker use. */
export function ensureLeafletDefaultIcon() {
  delete (L.Icon.Default.prototype as unknown as { _getIconUrl?: unknown })._getIconUrl
  L.Icon.Default.mergeOptions({
    iconRetinaUrl: markerIcon2x,
    iconUrl: markerIcon,
    shadowUrl: markerShadow,
  })
}

export function makeLeafletIcon(className = '') {
  ensureLeafletDefaultIcon()
  return new L.Icon({
    iconUrl: markerIcon,
    iconRetinaUrl: markerIcon2x,
    shadowUrl: markerShadow,
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    shadowSize: [41, 41],
    className,
  })
}

/** PNG-independent pin — reliable in mini maps / broken asset paths. */
export function makeDivPinIcon(tone: 'event' | 'facility' | 'default' = 'default') {
  return L.divIcon({
    className: `events-div-pin tone-${tone}`,
    html: '<span aria-hidden="true"></span>',
    iconSize: [22, 28],
    iconAnchor: [11, 28],
    popupAnchor: [0, -24],
  })
}
