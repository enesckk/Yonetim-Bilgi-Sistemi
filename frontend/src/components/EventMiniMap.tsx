import { useEffect } from 'react'
import { MapContainer, Marker, TileLayer, useMap } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { makeDivPinIcon } from '@/lib/leafletIcon'

const pinIcon = makeDivPinIcon('event')

type Props = {
  latitude: number
  longitude: number
  title?: string
  className?: string
}

function InvalidateSize() {
  const map = useMap()
  useEffect(() => {
    const id = window.setTimeout(() => map.invalidateSize(), 80)
    return () => window.clearTimeout(id)
  }, [map])
  return null
}

export function EventMiniMap({ latitude, longitude, title, className }: Props) {
  return (
    <div
      className={className ?? 'events-mini-map'}
      aria-label={title ? `${title} konumu` : 'Etkinlik konumu'}
    >
      <MapContainer
        center={[latitude, longitude]}
        zoom={15}
        scrollWheelZoom={false}
        dragging={false}
        doubleClickZoom={false}
        zoomControl={false}
        attributionControl={false}
        className="events-mini-map-leaflet"
      >
        <TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
        <InvalidateSize />
        <Marker position={[latitude, longitude]} icon={pinIcon} />
      </MapContainer>
    </div>
  )
}
