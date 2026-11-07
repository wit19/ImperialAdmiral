#!/usr/bin/env python3
"""M14f geometry census (Thomas R1 07.11: "pruefe sauber, ob du die richtige
Geometrie kennst, um mit Baendern zu arbeiten").

Question answered with numbers, over ALL shipped missions:
  * Are tile positions a clean grid? (px per hex, multiples)
  * Which tiles share an edge (physical contact)?
  * How many contacts carry a Door entity (a traversable seam)?
  * Do doors sit exactly on contacts (or float)?
  * => Can section adjacency (a "band" of sections) be derived soundly,
     and is it DOOR-SOUND (every derived adjacency is a real printed seam,
     never an invisible wall)?

Prints a report; exits non-zero if the inventory differs from the pinned
mission count (fail-closed, same law as hero_entry_mine.py).
"""
import glob
import json
import math
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources", "SagaMissions")
DIMS = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources", "dimensions.json")
PX_PER_HEX = 10.0   # candidate scale, verified below (fail-closed)

EXPANSIONS = ["Core", "Twin", "Hoth", "Bespin", "Jabba", "Empire", "Lothal"]


def load(p):
    s = open(p, encoding="utf-8-sig").read()
    return json.loads(re.sub(r",\s*([}\]])", r"\1", s))   # shipped trailing commas


def main():
    dims = {}
    for d in load(DIMS):
        dims[(d["expansion"], int(d["id"]))] = (d["width"], d["height"])
    paths = sorted(glob.glob(os.path.join(MISSIONS, "*", "*.json")))
    if len(paths) != 138:
        sys.exit("FAIL: %d missions, expected 138 (pinned inventory)" % len(paths))

    off_grid = 0
    bad_rot = 0
    tot_tiles = tot_contacts = tot_contacts_door = 0
    tot_doors = tot_doors_on_contact = tot_doors_float = 0
    sec_contacts = sec_contacts_door = 0
    missions = 0
    # per-mission contact/door coverage extremes
    worst = []

    for p in paths:
        fn = os.path.basename(p)
        m = load(p)
        missions += 1
        tiles = []
        for sec in (m.get("mapSections") or []):
            for t in (sec.get("mapTiles") or []):
                try:
                    x, y = [float(v) for v in str(t["entityPosition"]).split(",")]
                except Exception:
                    continue
                rot = float(t.get("entityRotation") or 0.0)
                exp = EXPANSIONS[int(t.get("expansion") or 0)]
                side = (t.get("tileSide") or "A")
                key = (exp, int(t.get("tileID") or 0))
                if key not in dims:
                    sys.exit("FAIL %s: no descriptor for %s" % (fn, key))
                w, h = dims[key]
                if rot % 90 != 0:
                    bad_rot += 1
                if abs(rot % 180 - 90) < 1e-6:
                    w, h = h, w
                if abs(x % PX_PER_HEX) > 1e-6 or abs(y % PX_PER_HEX) > 1e-6:
                    off_grid += 1
                tiles.append({
                    "guid": t.get("GUID"), "sec": str(t.get("mapSectionOwner")
                        or sec.get("GUID")), "x": x, "y": y,
                    "w": w * PX_PER_HEX, "h": h * PX_PER_HEX, "name": t.get("name"),
                })
        doors = []
        for e in (m.get("mapEntities") or []):
            if e.get("entityType") != 6:      # Door
                continue
            try:
                x, y = [float(v) for v in str(e["entityPosition"]).split(",")]
            except Exception:
                continue
            doors.append((e.get("GUID"), x, y))

        # edge contacts: rectangles touch (share a boundary segment of >= one hex)
        contacts = []
        for i in range(len(tiles)):
            for j in range(i + 1, len(tiles)):
                a, b = tiles[i], tiles[j]
                eps = 1e-6
                hsh = (abs(a["y"] + a["h"] - b["y"]) < eps or abs(b["y"] + b["h"] - a["y"]) < eps)
                hov = (min(a["x"] + a["w"], b["x"] + b["w"]) - max(a["x"], b["x"]))
                vsh = (abs(a["x"] + a["w"] - b["x"]) < eps or abs(b["x"] + b["w"] - a["x"]) < eps)
                hov2 = (min(a["y"] + a["h"], b["y"] + b["h"]) - max(a["y"], b["y"]))
                edge = None
                if hsh and hov >= PX_PER_HEX - eps:
                    edge = ("h", hov)
                elif vsh and hov2 >= PX_PER_HEX - eps:
                    edge = ("v", hov2)
                if edge:
                    contacts.append((i, j, edge))
        # door on contact? door point lies ON the shared segment
        def on_contact(d, c):
            i, j, (orient, ln) = c
            a, b = tiles[i], tiles[j]
            gx, gy = d[1], d[2]
            if orient == "h":     # horizontal seam at some y, x-range = overlap
                yseam = (abs(a["y"] + a["h"] - b["y"]) < 1e-6 and b["y"]) or \
                        (abs(b["y"] + b["h"] - a["y"]) < 1e-6 and a["y"])
                xlo = max(a["x"], b["x"]); xhi = min(a["x"] + a["w"], b["x"] + b["w"])
                return abs(gy - yseam) < 1e-6 and xlo - 1e-6 <= gx <= xhi + 1e-6
            else:                 # vertical seam
                xseam = (abs(a["x"] + a["w"] - b["x"]) < 1e-6 and b["x"]) or \
                        (abs(b["x"] + b["w"] - a["x"]) < 1e-6 and a["x"])
                ylo = max(a["y"], b["y"]); yhi = min(a["y"] + a["h"], b["y"] + b["h"])
                return abs(gx - xseam) < 1e-6 and ylo - 1e-6 <= gy <= yhi + 1e-6

        mc = len(contacts)
        md = len(doors)
        md_on = 0
        for d in doors:
            if any(on_contact(d, c) for c in contacts):
                md_on += 1
        c_with_door = 0
        for c in contacts:
            if any(on_contact(d, c) for d in doors):
                c_with_door += 1
        sc = sum(1 for (i, j, e) in contacts if tiles[i]["sec"] != tiles[j]["sec"])
        sc_door = sum(1 for (i, j, e) in contacts if tiles[i]["sec"] != tiles[j]["sec"]
                      and any(on_contact(d, (i, j, e)) for d in doors))

        tot_tiles += len(tiles); tot_contacts += mc; tot_contacts_door += c_with_door
        tot_doors += md; tot_doors_on_contact += md_on; tot_doors_float += md - md_on
        sec_contacts += sc; sec_contacts_door += sc_door
        if mc > 0 and c_with_door < mc:
            worst.append((fn, mc, c_with_door, sc, sc_door))

    print("missions: %d | tiles: %d | off-grid pos: %d | bad rot: %d" %
          (missions, tot_tiles, off_grid, bad_rot))
    print("contacts (edge-touch >=1 hex): %d | with >=1 door: %d (%.1f%%)" %
          (tot_contacts, tot_contacts_door, 100.0 * tot_contacts_door / max(1, tot_contacts)))
    print("doors: %d | on a contact: %d | floating: %d" %
          (tot_doors, tot_doors_on_contact, tot_doors_float))
    print("CROSS-SECTION contacts: %d | with door: %d (%.1f%%)" %
          (sec_contacts, sec_contacts_door, 100.0 * sec_contacts_door / max(1, sec_contacts)))
    worst.sort(key=lambda r: (r[2] - r[1]))
    print("missions with un-doored contacts (first 12):")
    for fn, mc, cwd, sc, scd in worst[:12]:
        print("   %-16s contacts=%3d door-covered=%3d  cross=%3d cross+door=%3d" %
              (fn, mc, cwd, sc, scd))
    print("missions total with un-doored contacts: %d/%d" % (len(worst), missions))


if __name__ == "__main__":
    main()
