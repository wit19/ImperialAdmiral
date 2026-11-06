#!/usr/bin/env python3
# S-A (t_00fda230): trigger-mining + Missionskritisch-Heuristik generator.
#
# WHAT IT DOES: reads the 138 shipped mission files (Resources/SagaMissions)
# and generates the reviewable data table
#   ImperialCommander2/Assets/Resources/Languages/En/tactical_mission_critical.json
# (schema tacai.mission_critical/1, format like tactical_pattern_weights.json:
# public reviewable data, _meta carries the normative law text).
#
# AUTHORING LAW (CONCEPT_positionierung_v1.md 5.3 + 8.1, review III):
#   * An entity is MISSION-CRITICAL when
#       reason "terminal"            - entityType Terminal (the app's own
#                                      console semantics; no guessing), OR
#       reason "objective-event"     - the entity is a sourceGUID of a
#                                      ModifyMapEntity action inside an event
#                                      body that ALSO carries a MapManagement
#                                      or ChangeObjective action (concept 5.3
#                                      co-occurrence law, mechanical), OR
#       reason "objective-variable"  - same sourceGUID law, but the event
#                                      carries a ModifyVariable action
#                                      (Thomas 06.11 DP-anchor coverage gap:
#                                      interaction objectives like saga
#                                      clue tokens are CLAIMED via
#                                      ModifyMapEntity(isActive:false) +
#                                      ModifyVariable in one event - the two
#                                      laws above never saw them, so whole
#                                      clue-bearing sections carried no
#                                      deployment anchor).
#   * sectionHypotheses: every event body that carries BOTH a section-
#     activation (MapManagement.mapSection != empty - the exact lane the app
#     itself runs, MissionEventHandler.cs MapManagement branch) AND entity
#     references (ModifyMapEntity.sourceGUID) yields a HYPOTHESIS edge
#     entity<->section. Hypothesis, never wall: a missing edge never
#     blocks (Grundgesetz - through-pass default).
#   * runtimeSwapped marks entityGroups members: ConfigureEntityGroups
#     (MapEntityManager.cs) swaps their positions AND mapSectionOwner at
#     mission load, so their authored section is NOT runtime truth. The
#     live EntityInteract feed reads the live object and is swap-correct;
#     only this static table degrades.
#   * Hole law: no coordinates anywhere in the asset - entity/section
#     GUIDs + app-authored names only.
#   * Loader mirror: eventActionType ints come from EventActionConverter.cs
#     (2=ChangeObjective, 14=MapManagement, 15=ModifyMapEntity); entity
#     types from EntityType (Tile=0 Terminal=1 Crate=2 DeploymentPoint=3
#     Token=4 Highlight=5 Door=6). Data-evidence law: nothing here reads a
#     memory or a guess - every row is a field of the shipped JSON.
#
# A2 evidence (Plan S-A Abbruch-Klausel): the report prints, per mission,
# whether terminals are EVER referenced by a ModifyMapEntity action - a
# terminal no event ever touches never flips, so its Interact-Anker stays
# empty while section anchors still run (degradation, not off).
#
# Regenerate:  python3 tools/mission_critical_mine.py   (from repo root)
# Exit code 1 on any invariant violation (fail-closed).
import json
import glob
import os
import sys
from collections import Counter

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS_GLOB = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                             "SagaMissions", "*", "*.json")
OUT = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                   "Languages", "En", "tactical_mission_critical.json")

ZERO = "00000000-0000-0000-0000-000000000000"
ENTITY_TYPES = {0: "Tile", 1: "Terminal", 2: "Crate", 3: "DeploymentPoint",
                4: "Token", 5: "Highlight", 6: "Door"}
# EventActionConverter.cs lane numbers (mirror, not memory):
A_CHANGE_OBJECTIVE = 2
A_MODIFY_VARIABLE = 3
A_MAP_MANAGEMENT = 14
A_MODIFY_MAP_ENTITY = 15


def events_of(mission):
    evs = list(mission.get("globalEvents") or [])
    for sec in mission.get("mapSections") or []:
        evs += list(sec.get("missionEvents") or [])
    return evs


def mine(path):
    m = json.load(open(path, encoding="utf-8"))
    mp = m.get("missionProperties") or {}
    sections = {s["GUID"]: s for s in (m.get("mapSections") or [])}

    swapped = set()
    for g in (m.get("entityGroups") or []):
        swapped |= set(g.get("missionEntities") or [])

    ents = m.get("mapEntities") or []
    ent_by_guid = {e["GUID"]: e for e in ents}

    # --- event mining -----------------------------------------------------
    modify_refs = {}          # entity guid -> count of ModifyMapEntity refs
    activate_targets = Counter()  # "Type->active" -> authored command count
    orphan_refs = 0           # sourceGUIDs not in this mission's mapEntities
    hypotheses = []
    objvar_guids = set()   # ModifyMapEntity + ModifyVariable co-occurrence law
    for e in events_of(m):
        acts = e.get("eventActions") or []
        mm_sections, has_objective, has_variable, refs = [], False, False, []
        for a in acts:
            t = a.get("eventActionType")
            if t == A_CHANGE_OBJECTIVE:
                has_objective = True
            elif t == A_MODIFY_VARIABLE:
                has_variable = True
            elif t == A_MAP_MANAGEMENT:
                s = a.get("mapSection") or ""
                if s and s != ZERO:
                    mm_sections.append(s)
            elif t == A_MODIFY_MAP_ENTITY:
                for mod in (a.get("entitiesToModify") or []):
                    src = mod.get("sourceGUID") or ""
                    if not src or src == ZERO:
                        continue
                    live = ent_by_guid.get(src)
                    if live is None:
                        # dangling authored reference - the app itself can
                        # never resolve it (GetEntity finds no prefab), so
                        # it anchors nothing; counted in the report, kept out
                        # of the table.
                        orphan_refs += 1
                        continue
                    refs.append(src)
                    modify_refs[src] = modify_refs.get(src, 0) + 1
                    props = mod.get("entityProperties") or {}
                    activate_targets["%s->%s" % (
                        ENTITY_TYPES.get(live.get("entityType"), "?"),
                        "active" if props.get("isActive") else "inactive")] += 1
        if (mm_sections or has_objective) and refs:
            for s in (mm_sections or [None]):
                hypotheses.append({
                    "event": e.get("GUID") or "",
                    "eventName": e.get("name") or "",
                    "section": s or "",
                    "sectionName": (sections[s].get("name") or "") if s in sections else "",
                    "via": "mapSection+modify" if s else "changeObjective+modify",
                    "entities": [{"entity": r,
                                  "name": ent_by_guid.get(r, {}).get("name") or "",
                                  "entityType": ENTITY_TYPES.get(
                                      ent_by_guid.get(r, {}).get("entityType"), "")}
                                 for r in dict.fromkeys(refs)],
                })
        if has_variable and refs:
            # interaction-objective law: claiming this entity changes the
            # mission variable economy -> objective-critical (see header).
            objvar_guids.update(dict.fromkeys(refs))

    # --- critical entities (concept 5.3 law) ------------------------------
    critical = []
    seen_guids = set()
    obj_event_guids = {en["entity"] for h in hypotheses for en in h["entities"]}
    for e in ents:
        t = ENTITY_TYPES.get(e.get("entityType"), "")
        reason = None
        if t == "Terminal":
            reason = "terminal"
        elif e["GUID"] in obj_event_guids:
            reason = "objective-event"
        elif e["GUID"] in objvar_guids:
            reason = "objective-variable"
        if reason is None or e["GUID"] in seen_guids:
            continue
        seen_guids.add(e["GUID"])
        sec = e.get("mapSectionOwner") or ""
        critical.append({
            "entity": e["GUID"],
            "name": e.get("name") or "",
            "entityType": t,
            "reason": reason,
            "section": sec if sec and sec != ZERO else "",
            "sectionName": sections.get(sec, {}).get("name") or "",
            "runtimeSwapped": e["GUID"] in swapped,
        })

    term_guids = [e["GUID"] for e in ents if e.get("entityType") == 1]
    terminal_flip_capable = any(g in modify_refs for g in term_guids)

    return ({
        "mission": mp.get("missionID") or os.path.splitext(os.path.basename(path))[0],
        "missionGuid": m.get("missionGUID") or "",
        "file": os.path.basename(path),
        "expansion": os.path.basename(os.path.dirname(path)),
        "sections": [{"section": s["GUID"], "name": s.get("name") or "",
                      "invisibleUntilActivated": bool(s.get("invisibleUntilActivated"))}
                     for s in (m.get("mapSections") or [])],
        "criticalEntities": critical,
        "sectionHypotheses": hypotheses,
    }), modify_refs, activate_targets, orphan_refs, len(m.get("mapSections") or []), \
        len(term_guids), terminal_flip_capable


def main():
    paths = sorted(glob.glob(MISSIONS_GLOB))
    if len(paths) != 138:
        print("FAIL: expected 138 shipped missions, found %d" % len(paths))
        return 1

    rows = []
    total_term = 0
    missions_with_terminals = 0
    missions_terminal_flip_capable = 0
    total_edges = 0
    edges_with_section = 0
    swapped_members = 0
    sec_counts = []
    crit_terminal = crit_objevent = crit_objvar = 0
    door_edges = 0
    activate_total = Counter()
    orphan_total = 0

    for p in paths:
        row, modify_refs, activate_targets, orphan_refs, nsec, n_term, flip = mine(p)
        rows.append(row)
        activate_total += activate_targets
        orphan_total += orphan_refs
        total_term += n_term
        if n_term:
            missions_with_terminals += 1
        if flip:
            missions_terminal_flip_capable += 1
        total_edges += len(row["sectionHypotheses"])
        edges_with_section += sum(1 for h in row["sectionHypotheses"] if h["section"])
        swapped_members += sum(1 for c in row["criticalEntities"] if c["runtimeSwapped"])
        crit_terminal += sum(1 for c in row["criticalEntities"] if c["reason"] == "terminal")
        crit_objevent += sum(1 for c in row["criticalEntities"] if c["reason"] == "objective-event")
        crit_objvar += sum(1 for c in row["criticalEntities"] if c["reason"] == "objective-variable")
        door_edges += sum(1 for h in row["sectionHypotheses"] for en in h["entities"]
                          if en["entityType"] == "Door")
        sec_counts.append(nsec)

    # invariants (fail-closed): every row's GUIDs exist in its own mission
    for p, row in zip(paths, rows):
        m = json.load(open(p, encoding="utf-8"))
        eg = {e["GUID"] for e in (m.get("mapEntities") or [])}
        sg = {s["GUID"] for s in (m.get("mapSections") or [])}
        for c in row["criticalEntities"]:
            if c["entity"] not in eg:
                print("FAIL: %s critical entity %s not in mission" % (row["mission"], c["entity"]))
                return 1
        for h in row["sectionHypotheses"]:
            if h["section"] and h["section"] not in sg:
                print("FAIL: %s hypothesis edge references unknown section %s"
                      % (row["mission"], h["section"]))
                return 1

    asset = {
        "_meta": {
            "file": "tactical_mission_critical.json",
            "schema": "tacai.mission_critical/1",
            "generated_by": "tools/mission_critical_mine.py - regenerate, never hand-edit",
            "note": ("PUBLIC reviewable data (fairness rule, format law like "
                     "tactical_pattern_weights.json): per mission, the entities the "
                     "Missionskritisch-Heuristik treats as mission-critical (reason "
                     "terminal = printed console semantics; reason objective-event = "
                     "ModifyMapEntity target inside an event body that also carries "
                     "MapManagement or ChangeObjective - concept positionierung v1 "
                     "5.3, mechanical; reason objective-variable = ModifyMapEntity "
                     "target inside an event body that also carries ModifyVariable - "
                     "interaction objectives whose claim mutates the mission variable "
                     "economy (saga clue tokens), Thomas 06.11 coverage fix for the "
                     "DP-anchor mining) plus sectionHypotheses: entity<->section "
                     "hypothesis edges mined from event bodies whose MapManagement "
                     "activates a section (app precedent MissionEventHandler "
                     "MapManagement branch). HYPOTHESIS NEVER WALL (Grundgesetz): a "
                     "missing edge keeps everything through-pass. runtimeSwapped = "
                     "entityGroups member, ConfigureEntityGroups swaps its section "
                     "at load - the authored section here is NOT runtime truth, the "
                     "live EntityInteract feed reads the live object and is correct. "
                     "Hole law: GUIDs and app-authored names only, no coordinates."),
        },
        "missions": rows,
    }
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(asset, f, indent=1, ensure_ascii=False)
        f.write("\n")

    dist = Counter(sec_counts)
    print("== mission_critical mine report ==")
    print("missions:                 %d/138" % len(rows))
    print("sections total:           %d  (per-mission min=%d max=%d)" %
          (sum(sec_counts), min(sec_counts), max(sec_counts)))
    print("per-mission section dist: %s" % dict(sorted(dist.items())))
    print("terminals total:          %d in %d/138 missions" % (total_term, missions_with_terminals))
    print("missions whose terminals are EVER referenced by a ModifyMapEntity "
          "action (flip-capable): %d/138" % missions_terminal_flip_capable)
    print("critical rows:            %d terminal + %d objective-event + %d objective-variable" %
          (crit_terminal, crit_objevent, crit_objvar))
    print("hypothesis edges:         %d total, %d section-bearing, %d door-entity" %
          (total_edges, edges_with_section, door_edges))
    print("runtimeSwapped critical:  %d" % swapped_members)
    print("dangling ModifyMapEntity sourceGUIDs (not in mission, anchored "
          "nothing, excluded from table): %d" % orphan_total)
    print("authored isActive commands by target type: %s" % dict(sorted(activate_total.items())))
    print("wrote: %s" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
