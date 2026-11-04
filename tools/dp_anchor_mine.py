#!/usr/bin/env python3
# M14c P25-C2 (card t_15ea0fba, fixplan_Player-25_v2 C2): the DP-anchor table.
#
# WHAT IT DOES: mines the reviewable anchor table
#   ImperialCommander2/Assets/Resources/Data/taktische_mission_critical.json
# (schema tacai.dp_anchor/1) from the SHIPPED data only:
#   * which entities are mission-critical  -> the S-A mined table
#     Languages/En/tactical_mission_critical.json (reason terminal /
#     objective-event, mechanical law, never hand-guessing), and
#   * position + section + GUID existence  -> the shipped mission JSONs
#     (Resources/SagaMissions/**/<file>), data-exists law: every row must
#     re-resolve against the real mission file or this script FAILS.
#
# TABLE FORMAT (card t_15ea0fba law): header row = column names, first
# column = the mission JSON FILENAME ("EMPIRE11.json") - the adapter matches
# the loaded mission by file id, OrdinalIgnoreCase, NEVER by a string title.
# Each hit row carries: entity GUID, entity position, section GUID.
# Positions-Subtask-IDs (buttonActions and friends) are deliberately NOT
# columns of this table and the reader ignores unknown columns: subtask ids
# are not anchor data.
#
# ANCHOR LAW (mirrors SectionTrust.MissionCriticalSections, the shipped
# precedent): type Terminal/Token/Crate only (a DeploymentPoint or Door is
# not a mission OBJECTIVE), runtimeSwapped=false only (ConfigureEntityGroups
# swaps position+section at load: the authored row is not runtime truth; the
# runtime adapter re-reads the LIVE object by GUID anyway and drops rows the
# live map does not carry - data-exists, never an invented anchor).
#
# LANGUAGE NEUTRALITY: GUIDs and coordinates only - no name column at all
# (a name can be localized, a GUID cannot).
#
# Regenerate:  python3 tools/dp_anchor_mine.py   (from repo root)
# Exit code 1 on any invariant violation (fail-closed).
import json
import glob
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CRITICAL = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                        "Languages", "En", "tactical_mission_critical.json")
MISSIONS_GLOB = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                             "SagaMissions", "*", "*.json")
OUT = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources", "Data",
                   "taktische_mission_critical.json")

ANCHOR_TYPES = {"Terminal", "Token", "Crate"}   # SectionTrust.MissionCriticalSections law
COLUMNS = ["missionFile", "entityGuid", "entityPosition", "sectionGuid"]

META = {
    "file": "taktische_mission_critical.json",
    "schema": "tacai.dp_anchor/1",
    "generated_by": "tools/dp_anchor_mine.py - regenerate, never hand-edit",
    "note": ("DP-deployment anchors (card t_15ea0fba, fixplan_Player-25_v2 C2): "
             "per mission FILE, the mission-critical objective entities the DP "
             "ranker keeps near (header row = columns, first column = mission "
             "JSON filename, OrdinalIgnoreCase match, never a string title). "
             "Critical set from tactical_mission_critical.json (S-A mechanical "
             "mining), type law Terminal/Token/Crate, runtimeSwapped rows "
             "skipped (authored section is not runtime truth - the runtime "
             "adapter re-reads the LIVE mapEntity by GUID and drops what the "
             "live map does not carry: data-exists, never an invented anchor). "
             "entityPosition is the authored review copy; runtime truth is the "
             "live IMapEntity.entityPosition. Positions-Subtask-IDs are not "
             "anchor data and are deliberately absent; readers ignore unknown "
             "columns. Language neutrality: GUIDs + coordinates only, no names."),
}


def fail(msg):
    print("INVARIANT: " + msg)
    sys.exit(1)


def main():
    if not os.path.exists(CRITICAL):
        fail("shipped tactical_mission_critical.json missing (nothing to mine)")
    with open(CRITICAL, encoding="utf-8") as f:
        critical = json.load(f)

    # mission filename -> list of (entityGuid, entityPosition, sectionGuid)
    rows = []
    seen_files = set()
    for m in critical.get("missions", []):
        mfile = m.get("file", "")
        if not mfile:
            fail("mission row without file id in critical table")
        if mfile in seen_files:
            fail("duplicate mission file id " + mfile)
        seen_files.add(mfile)
        hits = []
        for ent in m.get("criticalEntities", []):
            if ent.get("runtimeSwapped"):
                continue
            if ent.get("entityType") not in ANCHOR_TYPES:
                continue
            hits.append((ent["entity"].lower(), ent["entityType"],
                         ent.get("section", "")))
        if not hits:
            continue
        # data-exists: re-resolve every GUID against the shipped mission file
        matches = [p for p in glob.glob(MISSIONS_GLOB)
                   if os.path.basename(p).lower() == mfile.lower()]
        if len(matches) != 1:
            fail("mission file %s resolves to %d shipped files" % (mfile, len(matches)))
        with open(matches[0], encoding="utf-8") as f:
            mission = json.load(f)
        if not isinstance(mission, dict):
            fail("%s: not a mission object" % mfile)
        ents = {str(e.get("GUID", "")).lower(): e
                for e in mission.get("mapEntities", []) if isinstance(e, dict)}
        for guid, etype, section in hits:
            e = ents.get(guid)
            if e is None:
                fail("%s: critical entity %s not in mission mapEntities" % (mfile, guid))
            pos = e.get("entityPosition")
            if not isinstance(pos, str) or "," not in pos:
                fail("%s: entity %s has no authored entityPosition" % (mfile, guid))
            live_sec = str(e.get("mapSectionOwner", "")).lower()
            if live_sec != section.lower():
                fail("%s: table section %s != live mapSectionOwner %s for %s"
                     % (mfile, section, live_sec, guid))
            rows.append([mfile, guid, pos, live_sec])

    rows.sort(key=lambda r: (r[0].lower(), r[1]))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out = {"_meta": META, "columns": COLUMNS, "rows": rows}
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
        f.write("\n")
    per = {}
    for r in rows:
        per[r[0]] = per.get(r[0], 0) + 1
    print("wrote %s: %d rows across %d missions" % (
        os.path.relpath(OUT, REPO), len(rows), len(per)))
    for mf in sorted(per):
        print("  %-20s %d" % (mf, per[mf]))


if __name__ == "__main__":
    main()
