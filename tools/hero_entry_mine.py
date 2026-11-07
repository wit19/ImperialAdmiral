#!/usr/bin/env python3
# M14e (CONCEPT_standort_ledger_v2.md, Thomas 07.11): the HERO-ENTRY table.
#
# The blue hero-deploy fields are shipped mission data: entityType 5
# (Highlight) + entityColor "Blue" + theText matching /deploy/i AND
# /hero|helden|rebel/i. Measured over the 138 shipped missions: exactly
# 133 missions carry at least one such field, 0 false positives (the other
# blue highlights - T-16, getaway shuttle, fires, exit - carry no "deploy"
# word), 6 missions carry 2-3 fields ("Deploy HALF of the heroes here" -
# the entry is a SET of possible sections, never a point), 5 missions carry
# none (CORE2, CORE11, CORE28, EMPIRE13, OTHER11 - no row, adapter inert).
#
# WHY A TABLE AND NOT A RUNTIME TEXT SCAN: mission entity text is replaced
# by the language overlay at mission load (TranslationController.
# SetMissionTranslation) and custom/embedded missions carry author text in
# any language - a text law at runtime is language-fragile. The shipped
# English JSON is mined ONCE here; the runtime adapter matches the loaded
# mission by FILE NAME (same law as the DP-anchor table), re-resolves every
# GUID through MapEntityManager.GetEntity and drops what the live map does
# not carry (data-exists law: no live entity, no hero entry).
#
# LANGUAGE NEUTRALITY: GUIDs and the authored NAME only (the name is audit
# sugar for the log, never a match key). No coordinates (the section GUID
# is the spatial claim the app is allowed to make).
#
# Regenerate:  python3 tools/hero_entry_mine.py   (from repo root)
# Exit code 1 on any invariant violation (fail-closed).
import json
import glob
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                        "SagaMissions")
OUT = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources", "Data",
                   "taktische_hero_entries.json")

DEPLOY_RE = re.compile(r"deploy|place", re.I)
HERO_RE = re.compile(r"hero|helden|rebel", re.I)

HIGHLIGHT = 5


def main():
    rows = []
    total = 0
    hits = 0
    multi = 0
    for path in sorted(glob.glob(os.path.join(MISSIONS, "*", "*.json"))):
        total += 1
        fname = os.path.basename(path)
        with open(path, encoding="utf-8-sig") as fh:
            d = json.load(fh)
        ents = d.get("mapEntities") or []
        entry_rows = []
        for e in ents:
            ep = e.get("entityProperties") or {}
            if e.get("entityType") != HIGHLIGHT:
                continue
            blue = (ep.get("entityColor") == "Blue"
                    or e.get("deploymentColor") == "Blue")
            if not blue:
                continue
            txt = (ep.get("theText") or "").strip()
            name = (e.get("name") or "").strip()
            # Law v2 (Thomas 07.11 'schau genau'): a deployment VERB and a
            # hero word in the field text (word order free - CORE29 says
            # "This is the heroes' cell. Deploy them here."), OR a field
            # with NO text named just "Entrance" (CORE2: the shipped data
            # left theText empty, the name law is the BESPIN one).
            # Negative classes stay out WITHOUT a blacklist: getaway
            # shuttles/exits/fires never say deploy|place (verified over
            # all 25 remaining blue fields, 0 false positives, 07.11).
            if txt:
                if not DEPLOY_RE.search(txt) or not HERO_RE.search(txt):
                    continue
            elif name.lower() != "entrance":
                continue
            guid = e.get("GUID") or e.get("guid")
            if not guid:
                sys.exit("FAIL %s: hero-entry entity without GUID" % fname)
            if any(r["guid"] == guid for r in entry_rows):
                sys.exit("FAIL %s: duplicate hero-entry GUID %s" % (fname, guid))
            entry_rows.append({
                "guid": guid,
                "name": e.get("name") or "",
                "section": str(e.get("mapSectionOwner") or ""),
            })
        if entry_rows:
            hits += 1
            if len(entry_rows) > 1:
                multi += 1
            rows.append({"mission": fname, "entries": entry_rows})

    if total != 138:
        sys.exit("FAIL: shipped mission count is %d, expected 138 "
                 "(the gate and the concept pin this inventory)" % total)
    # Pin the measured law outcome (measured 07.11 over the shipped tree,
    # law v2). If the shipped data changes, this fails CLOSED and the
    # numbers must be re-measured deliberately, never silently absorbed.
    if hits != 134 or multi != 8:
        sys.exit("FAIL: hero-entry law v2 hits %d missions (%d multi), "
                 "expected 134 (8 multi)" % (hits, multi))

    table = {"schema": "tacai.hero_entry/2",
             "law": "entityType==Highlight && blue (entityColor|deploymentColor)"
                    " && ((theText ~ /deploy|place/i && theText ~ /hero|helden|rebel/i)"
                    " || (theText empty && name=='Entrance'))"
                    " (mined from the shipped EN mission JSON; runtime"
                    " re-resolves GUIDs against the live map)",
             "rows": rows}
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(table, fh, indent=1)
        fh.write("\n")
    entries = sum(len(r["entries"]) for r in rows)
    multi = sum(1 for r in rows if len(r["entries"]) > 1)
    print("OK %s: %d missions with entries (%d multi), %d entries total, "
          "%d/%d missions" % (os.path.relpath(OUT, REPO), hits, multi,
                              entries, total, total))


if __name__ == "__main__":
    main()
