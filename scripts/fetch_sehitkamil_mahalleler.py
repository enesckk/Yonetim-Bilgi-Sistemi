#!/usr/bin/env python3
"""Fetch Şehitkamil neighbourhood polygons and write GeoJSON."""
from __future__ import annotations

import json
import ssl
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] if False else Path.cwd()
OUT = Path("frontend/public/geo/sehitkamil-mahalleler.geojson")
CTX = ssl.create_default_context()


def fetch(url: str, data: bytes | None = None, timeout: int = 120) -> bytes:
    req = urllib.request.Request(url, data=data, method="POST" if data else "GET")
    if data:
        req.add_header("Content-Type", "application/x-www-form-urlencoded")
    with urllib.request.urlopen(req, context=CTX, timeout=timeout) as r:
        return r.read()


def try_overpass() -> dict | None:
    query = """
[out:json][timeout:120];
area["name"="Şehitkamil"]["boundary"="administrative"]["admin_level"="6"]->.a;
(
  relation["boundary"="administrative"]["admin_level"="8"](area.a);
);
out geom;
""".strip()
    body = ("data=" + urllib.request.quote(query)).encode()
    endpoints = [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    ]
    for ep in endpoints:
        try:
            print("OVERPASS", ep)
            raw = fetch(ep, body)
            data = json.loads(raw)
            rels = [e for e in data.get("elements", []) if e.get("type") == "relation"]
            print("  relations", len(rels))
            if rels:
                return data
        except Exception as e:
            print("  FAIL", type(e).__name__, e)
    return None


def try_territorykit() -> dict | None:
    # Generated Gaziantep ADM3 full.geojson from TerritoryKit (if published)
    urls = [
        "https://raw.githubusercontent.com/mberatkaya/TerritoryKit/main/datasets/generated/countries/TR/levels/ADM3/full.geojson",
        "https://cdn.jsdelivr.net/gh/mberatkaya/TerritoryKit@main/datasets/generated/countries/TR/levels/ADM3/full.geojson",
    ]
    for url in urls:
        try:
            print("TK", url[:90])
            raw = fetch(url, timeout=180)
            print("  bytes", len(raw))
            return json.loads(raw)
        except Exception as e:
            print("  FAIL", type(e).__name__, e)
    return None


def try_open_data_kml() -> bytes | None:
    urls = [
        "https://acikveri.gaziantep.bel.tr/dataset/6c8f0f0f-0000-0000-0000-000000000000",  # placeholder
    ]
    # Known Ulusal Akıllı Şehir download often looks like:
    candidates = [
        "https://www.turkiye.gov.tr/",  # skip
    ]
    return None


def overpass_to_geojson(data: dict) -> dict:
    """Convert Overpass relations-with-geometry to FeatureCollection."""
    features = []
    for el in data.get("elements", []):
        if el.get("type") != "relation":
            continue
        tags = el.get("tags") or {}
        name = tags.get("name") or tags.get("name:tr") or f"OSM {el['id']}"
        # Collect outer rings from members with geometry
        outers: list[list[list[float]]] = []
        inners: list[list[list[float]]] = []
        for m in el.get("members", []):
            if m.get("type") != "way" or "geometry" not in m:
                continue
            coords = [[p["lon"], p["lat"]] for p in m["geometry"]]
            if len(coords) < 3:
                continue
            # close ring
            if coords[0] != coords[-1]:
                coords.append(coords[0])
            role = m.get("role") or "outer"
            if role == "inner":
                inners.append(coords)
            else:
                outers.append(coords)
        if not outers:
            continue
        if len(outers) == 1:
            geom = {"type": "Polygon", "coordinates": [outers[0], *inners]}
        else:
            # MultiPolygon: attach inners to first outer as approximation
            polys = [[outers[0], *inners]] + [[o] for o in outers[1:]]
            geom = {"type": "MultiPolygon", "coordinates": polys}
        features.append(
            {
                "type": "Feature",
                "properties": {
                    "id": str(el["id"]),
                    "name": name,
                    "admin_level": tags.get("admin_level"),
                    "source": "osm",
                },
                "geometry": geom,
            }
        )
    return {"type": "FeatureCollection", "features": features}


def point_in_ring(x: float, y: float, ring: list[list[float]]) -> bool:
    inside = False
    n = len(ring)
    j = n - 1
    for i in range(n):
        xi, yi = ring[i]
        xj, yj = ring[j]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / (yj - yi + 1e-30) + xi):
            inside = not inside
        j = i
    return inside


def centroid(geom: dict) -> tuple[float, float]:
    coords = geom["coordinates"]
    if geom["type"] == "Polygon":
        ring = coords[0]
    else:
        ring = coords[0][0]
    xs = [p[0] for p in ring]
    ys = [p[1] for p in ring]
    return sum(xs) / len(xs), sum(ys) / len(ys)


def filter_sehitkamil(fc: dict, district_path: Path) -> dict:
    district = json.loads(district_path.read_text(encoding="utf-8"))
    dgeom = district["features"][0]["geometry"]

    def in_district(lon: float, lat: float) -> bool:
        if dgeom["type"] == "Polygon":
            return point_in_ring(lon, lat, dgeom["coordinates"][0])
        if dgeom["type"] == "MultiPolygon":
            return any(point_in_ring(lon, lat, poly[0]) for poly in dgeom["coordinates"])
        return False

    kept = []
    for f in fc.get("features", []):
        props = f.get("properties") or {}
        # TerritoryKit may have parent / ILCE hints
        name_parent = str(props.get("parentName") or props.get("ilce") or props.get("ILCE") or "")
        adm2 = str(props.get("parentId") or props.get("adm2") or "")
        if "şehitkamil" in name_parent.casefold() or "sehitkamil" in name_parent.casefold():
            kept.append(f)
            continue
        # ILCEID mapping unknown — use centroid PIP
        try:
            lon, lat = centroid(f["geometry"])
        except Exception:
            continue
        if in_district(lon, lat):
            kept.append(f)
    return {"type": "FeatureCollection", "features": kept}


def normalize_props(fc: dict) -> dict:
    out = []
    for i, f in enumerate(fc["features"]):
        p = dict(f.get("properties") or {})
        name = p.get("name") or p.get("AD") or p.get("ad") or p.get("NAME") or f"Mahalle {i+1}"
        fid = str(p.get("id") or p.get("KIMLIKNO") or p.get("kimlikno") or p.get("osm_id") or i + 1)
        out.append(
            {
                "type": "Feature",
                "properties": {"id": fid, "name": str(name).strip()},
                "geometry": f["geometry"],
            }
        )
    out.sort(key=lambda x: x["properties"]["name"].casefold())
    return {"type": "FeatureCollection", "features": out}


def main() -> None:
    district = Path("frontend/public/geo/sehitkamil-boundary.geojson")
    fc = None

    ov = try_overpass()
    if ov:
        fc = overpass_to_geojson(ov)
        print("from overpass features", len(fc["features"]))

    if not fc or len(fc["features"]) < 5:
        tk = try_territorykit()
        if tk:
            filtered = filter_sehitkamil(tk, district)
            print("from territorykit filtered", len(filtered["features"]))
            if len(filtered["features"]) > len(fc["features"] if fc else []):
                fc = filtered

    if not fc or not fc["features"]:
        raise SystemExit("No mahalle polygons found")

    fc = normalize_props(fc)
    # Prefer features whose centroid is in district
    fc = filter_sehitkamil(fc, district)
    fc = normalize_props(fc)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(fc, ensure_ascii=False), encoding="utf-8")
    print("wrote", OUT, "features", len(fc["features"]), "bytes", OUT.stat().st_size)
    print("sample", [f["properties"]["name"] for f in fc["features"][:12]])


if __name__ == "__main__":
    main()
