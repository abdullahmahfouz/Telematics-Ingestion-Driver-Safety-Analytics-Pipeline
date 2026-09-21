import { useEffect, useRef } from "react";
import mapboxgl from "mapbox-gl";
import "mapbox-gl/dist/mapbox-gl.css";
import type { Feature, LineString } from "geojson";
import type { TelematicsRecord } from "../types/telematics";
import { isHarshAcceleration, isHarshBraking, isHarshCornering } from "../safety/thresholds";

const MAPBOX_TOKEN = import.meta.env.VITE_MAPBOX_TOKEN as string | undefined;

interface TripMapProps {
  records: TelematicsRecord[];
}

export function TripMap({ records }: TripMapProps) {
  const mapContainerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<mapboxgl.Map | null>(null);
  const markersRef = useRef<mapboxgl.Marker[]>([]);

  useEffect(() => {
    if (!MAPBOX_TOKEN || !mapContainerRef.current || mapRef.current) return;

    mapboxgl.accessToken = MAPBOX_TOKEN;
    mapRef.current = new mapboxgl.Map({
      container: mapContainerRef.current,
      style: "mapbox://styles/mapbox/dark-v11",
      center: [-79.3832, 43.6532],
      zoom: 12,
    });

    return () => {
      mapRef.current?.remove();
      mapRef.current = null;
    };
  }, []);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || records.length === 0) return;

    markersRef.current.forEach((marker) => marker.remove());
    markersRef.current = [];

    const chronological = [...records].reverse();
    const coordinates = chronological.map((r): [number, number] => [r.longitude, r.latitude]);

    const drawTrail = () => {
      const trailGeoJson: Feature<LineString> = {
        type: "Feature",
        properties: {},
        geometry: { type: "LineString", coordinates },
      };

      if (map.getSource("trip-trail")) {
        (map.getSource("trip-trail") as mapboxgl.GeoJSONSource).setData(trailGeoJson);
      } else {
        map.addSource("trip-trail", { type: "geojson", data: trailGeoJson });
        map.addLayer({
          id: "trip-trail-line",
          type: "line",
          source: "trip-trail",
          paint: { "line-color": "#378ADD", "line-width": 3 },
        });
      }
    };

    if (map.isStyleLoaded()) {
      drawTrail();
    } else {
      map.once("load", drawTrail);
    }

    chronological.forEach((record) => {
      const isHarsh =
        isHarshBraking(record.accelerationXG) ||
        isHarshCornering(record.accelerationYG) ||
        isHarshAcceleration(record.accelerationXG);

      const marker = new mapboxgl.Marker({ color: isHarsh ? "#E24B4A" : "#5DCAA5" })
        .setLngLat([record.longitude, record.latitude])
        .setPopup(
          new mapboxgl.Popup({ offset: 12 }).setText(
            `${new Date(record.timestamp).toLocaleTimeString()} — ${record.speedKmh.toFixed(1)} km/h`,
          ),
        )
        .addTo(map);

      markersRef.current.push(marker);
    });

    const bounds = coordinates.reduce(
      (b, coord) => b.extend(coord),
      new mapboxgl.LngLatBounds(coordinates[0], coordinates[0]),
    );

    // The container may not have had its final size yet when the map was constructed
    // (e.g. data arrives before layout settles on first load) — resize before fitting
    // so Mapbox recalculates against the real canvas dimensions.
    map.resize();
    map.fitBounds(bounds, { padding: 60, maxZoom: 16 });
  }, [records]);

  if (!MAPBOX_TOKEN) {
    return (
      <div className="map-placeholder">
        <p>Map requires a Mapbox access token.</p>
        <p className="map-placeholder__hint">
          Add <code>VITE_MAPBOX_TOKEN=pk.your_token</code> to <code>client/.env.local</code> and restart the dev
          server.
        </p>
      </div>
    );
  }

  return <div ref={mapContainerRef} className="trip-map" />;
}
