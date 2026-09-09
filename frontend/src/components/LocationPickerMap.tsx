import { useEffect, useMemo } from 'react'
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import { makeDivPinIcon } from '@/lib/leafletIcon'

const pinIcon = makeDivPinIcon('facility')
const SEHITKAMIL_CENTER: [number, number] = [37.17, 37.35]

type Props = {
  latitude: number | null
  longitude: number | null
  onPick: (latitude: number, longitude: number) => void
  className?: string
  height?: number
}

function ClickCapture({ onPick }: { onPick: (lat: number, lng: number) => void }) {
  useMapEvents({
    click(e) {
      onPick(e.latlng.lat, e.latlng.lng)
    },
  })
  return null
}

function SyncView({ lat, lng }: { lat: number; lng: number }) {
  const map = useMap()
  useEffect(() => {
    map.setView([lat, lng], Math.max(map.getZoom(), 15), { animate: true })
    const id = window.setTimeout(() => map.invalidateSize(), 80)
    return () => window.clearTimeout(id)
  }, [lat, lng, map])
  return null
}

export function LocationPickerMap({
  latitude,
  longitude,
  onPick,
  className,
  height = 240,
}: Props) {
  const hasPoint = latitude != null && longitude != null && Number.isFinite(latitude) && Number.isFinite(longitude)
  const center: [number, number] = useMemo(
    () => (hasPoint ? [latitude!, longitude!] : SEHITKAMIL_CENTER),
    [hasPoint, latitude, longitude],
  )

  return (
    <div
      className={className ?? 'location-picker-map'}
      style={{ height }}
      aria-label="Haritadan konum seç"
    >
      <MapContainer
        center={center}
        zoom={hasPoint ? 15 : 12}
        scrollWheelZoom
        className="location-picker-leaflet"
        style={{ height: '100%', width: '100%' }}
      >
        <TileLayer
          attribution='&copy; OpenStreetMap'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <ClickCapture onPick={onPick} />
        {hasPoint ? (
          <>
            <SyncView lat={latitude!} lng={longitude!} />
            <Marker position={[latitude!, longitude!]} icon={pinIcon} />
          </>
        ) : null}
      </MapContainer>
      <p className="location-picker-hint muted small">İsterseniz haritaya tıklayarak da pin koyabilirsiniz.</p>
    </div>
  )
}
