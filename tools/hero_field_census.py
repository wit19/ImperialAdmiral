#!/usr/bin/env python3
"""P33-H2 hero start-field census (findings_Player-33 fix A, step 1).

Question answered with numbers, over ALL shipped missions:
  * Coverage   - how many missions carry a blue hero-deploy field at all?
  * Field type - which entityType do those fields actually carry?
  * Geometry   - does every field own a live section (the ONLY spatial claim
                 the app is allowed to make), and do multi-field missions
                 COLLAPSE (all fields in one section -> fact anchor allowed)
                 or SPREAD (several sections -> hypothesis set only)?
  * Zero list  - which missions carry NO field (Null-Law: no field, no
                 anchor - the adapter must stay inert there, never guess).

The field law is the SAME law as tools/hero_entry_mine.py (v2): entityType
5 (Highlight) + Blue (entityColor or deploymentColor) + a deploy verb AND a
hero word in the text, or empty text with authored name "Entrance". This
census verifies the SHAPE of what that law returns; the miner pins the
content. Both fail-closed on the pinned inventory (same law as
section_geometry_census.py).

Measured 07.11 over the shipped tree. If the shipped data changes, the
FAIL lines force a deliberate re-measurement - never silent absorption.
"""
import glob
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                        "SagaMissions")

HIGHLIGHT = 5
DEPLOY_RE = re.compile(r"deploy|place", re.I)
HERO_RE = re.compile(r"hero|helden|rebel", re.I)
ZERO_GUID = "00000000-0000-0000-0000-000000000000"

# Pinned census outcome (measured 07.11, law v2 data). A change in the
# shipped missions must re-measure deliberately and re-pin here.
PIN_MISSIONS = 138
PIN_HITS = 134          # missions with >= 1 hero-deploy field
PIN_MULTI = 8           # missions with > 1 field
PIN_FIELDS = 143        # total fields
PIN_COLLAPSE = 5        # multi missions whose fields share ONE section
PIN_SPREAD = 3          # multi missions whose fields span >1 section
PIN_ZERO = ["CORE11.json", "CORE28.json", "EMPIRE13.json", "OTHER11.json"]


def load(p):
    return json.load(open(p, encoding="utf-8-sig"))


def is_hero_field(e):
    ep = e.get("entityProperties") or {}
    if e.get("entityType") != HIGHLIGHT:
        return False
    if ep.get("entityColor") != "Blue" and e.get("deploymentColor") != "Blue":
        return False
    txt = (ep.get("theText") or "").strip()
    name = (e.get("name") or "").strip()
    if txt:
        return bool(DEPLOY_RE.search(txt) and HERO_RE.search(txt))
    return name.lower() == "entrance"


def main():
    paths = sorted(glob.glob(os.path.join(MISSIONS, "*", "*.json")))
    if len(paths) != PIN_MISSIONS:
        sys.exit("FAIL: %d missions, expected %d (pinned inventory)"
                 % (len(paths), PIN_MISSIONS))

    hits = fields = 0
    collapse = spread = 0
    type_hist = {}
    dual_blue = 0
    no_section = []
    dup = 0
    zero = []
    named = 0
    for p in paths:
        fn = os.path.basename(p)
        d = load(p)
        section_names = {str(s.get("GUID") or s.get("guid") or "")
                         for s in d.get("mapSections") or []
                         if (s.get("name") or s.get("sectionName") or "").strip()}
        rows = []
        seen = set()
        for e in d.get("mapEntities") or []:
            if not is_hero_field(e):
                continue
            ep = e.get("entityProperties") or {}
            if ep.get("entityColor") == "Blue" and e.get("deploymentColor") == "Blue":
                dual_blue += 1
            t = e.get("entityType")
            type_hist[t] = type_hist.get(t, 0) + 1
            g = str(e.get("GUID") or e.get("guid") or "")
            if g in seen:
                dup += 1
            seen.add(g)
            own = str(e.get("mapSectionOwner") or "")
            if own in ("", ZERO_GUID):
                no_section.append((fn, e.get("name"), g[:8]))
            if own in section_names:
                named += 1
            rows.append(own)
        fields += len(rows)
        if rows:
            hits += 1
            if len(rows) > 1:
                if len(set(rows)) == 1:
                    collapse += 1
                else:
                    spread += 1
        else:
            zero.append(fn)

    # ---- fail-closed pins (every claim of the handoff is a gate here) ----
    if type_hist != {HIGHLIGHT: fields}:
        sys.exit("FAIL: hero fields carry types %s, expected exclusively "
                 "entityType=%d (Highlight) - a new field type needs a law "
                 "review, not a silent widen" % (type_hist, HIGHLIGHT))
    if dual_blue != fields:
        sys.exit("FAIL: only %d/%d fields carry BOTH entityColor and "
                 "deploymentColor Blue - color-source law drifted" % (dual_blue, fields))
    if no_section:
        sys.exit("FAIL: %d fields without a live section owner %s - the "
                 "geometry claim (section) is mandatory, such fields must "
                 "be dropped by the adapter and re-counted here" % (len(no_section), no_section[:5]))
    if dup:
        sys.exit("FAIL: %d duplicate field GUIDs inside one mission" % dup)
    if named != fields:
        sys.exit("FAIL: only %d/%d owner sections carry an authored name - "
                 "zone naming would fall back to ids" % (named, fields))
    if hits != PIN_HITS or fields != PIN_FIELDS:
        sys.exit("FAIL: %d missions with fields (%d fields), expected "
                 "%d (%d) - re-measure deliberately" % (hits, fields, PIN_HITS, PIN_FIELDS))
    if collapse != PIN_COLLAPSE or spread != PIN_SPREAD:
        sys.exit("FAIL: multi-field split collapse=%d spread=%d, expected "
                 "%d/%d" % (collapse, spread, PIN_COLLAPSE, PIN_SPREAD))
    if zero != PIN_ZERO:
        sys.exit("FAIL: zero-field inventory %s, expected %s" % (zero, PIN_ZERO))

    fact_missions = hits - spread
    print("OK hero-deploy-field census: %d/%d missions carry fields, "
          "%d fields total (all entityType=%d Highlight, all dual-Blue, "
          "%d/%d carry section+authored name, %d dup GUIDs)"
          % (hits, PIN_MISSIONS, fields, HIGHLIGHT, named, fields, dup))
    print("   anchor classes: %d fact-capable (single-field or collapsed), "
          "%d hypothesis-set only (spread), %d inert (no field)"
          % (fact_missions, spread, len(zero)))
    print("   multi-field: %d COLLAPSE (one section => fact anchor), "
          "%d SPREAD (section set => no anchor)" % (collapse, spread))
    print("   zero-field Null-Law list: %s" % ", ".join(zero))


if __name__ == "__main__":
    main()
