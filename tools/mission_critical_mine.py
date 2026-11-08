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
# P37a (card t_1f4d7286) FOURTH MECHANICAL RULE - the DUTY law
# (docs/CONCEPT_missions_duty.md 1a/7, golden vector Rev 3.3/3.4): every
# mission row carries a dutyAnchors block {class: duty|borderline|none}.
# The law, all structural, side-neutral:
#   CANDIDATE: a mission variable (globalTriggers) whose threshold trigger
#     (an end event's additionalTriggers, triggerValue >= 1) sits on an end
#     event (MissionManagement.endMission) of the EMPIRE-WIN page - the page
#     is decided by the producer's OWN polarity ladder (WorldEventsUnity
#     OutcomeSideOfText: fixed-order vocabulary mirror, losing side first),
#     run over the end event's name + eventText. Same machine, same words,
#     fixed order - never an ad-hoc regex.
#   CHANNELS writing the variable with a positive write (setValue>=1 or
#     modifyValue>=1): ModifyVariable events, direct button writes
#     (entityProperties.buttonActions[].triggerGUID), and bookkeeping
#     structures (defeatedTrigger/setTrigger/foundTrigger, missionProperties
#     event refs) - the structures are DEAD combat bookkeeping.
#   DEAD channel (never duty, never borderline): the setter IS an end event
#     or its name starts with "end of mission" (self-bookkeeping 7(b));
#     struct-only variables (no event channel at all).
#   BOOK channel (borderline-only, "vermutlich KEIN Duty"): setter name hits
#     the combat/round bookkeeping words (dead/defeat/wound/round/story,
#     word-boundary, NAME only - the printed rules text legitimately says
#     "mark as defeated") or the setter fires on a combat/round flag
#     (useAllHeroesWounded...useEndOfEachRound) or is referenced from
#     missionProperties (roundLimitEvent - round bookkeeping).
#   CLEAN channel + TAETER direction (7(c) "wer bewegt den Zaehler", fixed
#     order, structural evidence only - NO narrative text, EMPIRE2's story
#     prose mentions "Imperial officer" and is still defeat bookkeeping):
#       button wired to the setter: hero-glyph label ({A}... = hero action,
#       CORE16 "Slice") blocks; Door carrier is door-law (no evidence);
#       any other carrier -> IMPERIUM (Zielfeld button, HOTH10 "Cell").
#       direct trigger button: hero glyph blocks; Door carrier -> no
#       evidence; Crate/Token carrier -> IMPERIUM (object button, CORE28/
#       OTHER9); carrier label or variable name carries the side mark
#       (imperial/empire/imperium) -> IMPERIUM (CORE27 "Entrance" escape
#       field); unmarked field button -> UNRESOLVED.
#       InputPrompt inputList adjudication (type 20 dialog, CORE23 recruit
#       count) -> IMPERIUM (human bookkeeping, 1a: button/DG/extern are
#       gleichwertig).
#       useActivation -> IMPERIUM (DG activation event).
#       threshold-chain listener (setter's own additionalTriggers): qualifies
#       when one branch's trigger name is not a book word AND the trigger has
#       a clean (not dead/book) writer event or no in-mission writer at all
#       (external authoring) -> IMPERIUM (extern, HOTH12/OTHER31); all-
#       defeat chains (EMPIRE2/OTHER6) do NOT qualify -> UNRESOLVED.
#       nothing above -> UNRESOLVED (structural taeter unknown: CORE16
#       "Discard Terminal N" third mechanism, HOTH15 "Rebels Lose RL").
#   MISSION: class "duty" iff some candidate has an IMPERIUM channel;
#     "borderline" iff some candidate channel survives (book/unresolved/
#     hero/unmarked-field) but none is IMPERIUM; "none" iff no candidate or
#     every channel is DEAD (JABBA2/JABBA4/OTHER27: outcome self-flags and
#     defeat structures only - silence, not borderline).
#   FAIL-CLOSED: the shipped corpus pins DUTY=7 (CORE23, CORE27, CORE28,
#     HOTH10, HOTH12, OTHER9, OTHER31 with the 7 golden variable@threshold
#     pairs), BORDERLINE=7 (CORE10, CORE16, EMPIRE2, EMPIRE4, HOTH15,
#     OTHER6, OTHER7), RUHE=124. Any deviation = the miner exits 1; silent
#     adaptation is forbidden (concept 7 golden-vector law).
#   ANCHOR EXPORT per duty (targetFields = button carriers of the duty
#     channels; markerTokens = Token entities a duty setter or its qualifying
#     chain writer spawns via ModifyMapEntity isActive:true): HOTH10 Zielfeld
#     "Cell" with an invisible marker (authored isActive:false) is NOT an
#     error - the concept says so; CORE23's counter rides on figures, it has
#     no entity anchor and exports an empty anchor set (honest, not padded).
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
A_INPUT_PROMPT = 20   # EventActionConverter: dialog with inputList[eventGUID]

# --- P37a duty law constants (mirrors, never memory) ------------------------
# WorldEventsUnity.cs OutcomeSideOfText vocabulary (fixed order = the ladder):
LOSE_REBELS = ["rebels lose", "rebel lose", "rebellen verlieren"]
LOSE_EMPIRE = ["empire lose", "imperial lose", "imperials lose",
               "imperium verliert", "imperium verlieren"]
WIN_REBELS = ["rebels win", "rebel win", "rebellen gewinn"]
WIN_EMPIRE = ["empire win", "imperial win", "imperials win", "imperium gewinn"]
CLAIM_WORDS = ["claim", "beanspruch"]
CLAIM_EMPIRE_MARKS = ["empire", "imperium"]
CLAIM_REBEL_MARKS = ["rebel", "rebellen"]
# 7(b) combat/round bookkeeping words (word-boundary; setter NAME only):
BOOK_RE = __import__("re").compile(r"\b(dead|defeat\w*|wound\w*|round\w*|story|stories)\b", __import__("re").I)
# perpetrator side mark (button label / variable name only - never prose):
MARK_RE = __import__("re").compile(r"\b(imperial|empire|imperium)\b", __import__("re").I)
# hero action glyph on a button label ({A}, {M}, {I}, {K}...):
GLYPH_RE = __import__("re").compile(r"\{[A-Za-z0-9]\}")
COMBAT_FLAGS = ["useAllHeroesWounded", "useAnyHeroWounded", "useAllGroupsDefeated",
                "useAnyHeroDefeated", "useAllyDefeated", "useHeroWounded",
                "useHeroWithdraws", "useStartOfRound", "useEndOfRound",
                "useStartOfEachRound", "useEndOfEachRound"]


def outcome_side_of_text(text):
    """Python SECONDARY machine of WorldEventsUnity.OutcomeSideOfText: the
    fixed-order ladder returning the LOSING side ("rebels"/"empire"), "" when
    no outcome sentence. Same vocabulary, same order, no creative regex."""
    t = (text or "").lower()
    if not t:
        return ""
    for s in LOSE_REBELS:
        if s in t:
            return "rebels"
    for s in LOSE_EMPIRE:
        if s in t:
            return "empire"
    for s in WIN_REBELS:
        if s in t:
            return "empire"
    for s in WIN_EMPIRE:
        if s in t:
            return "rebels"
    if any(s in t for s in CLAIM_WORDS):
        if any(s in t for s in CLAIM_EMPIRE_MARKS):
            return "rebels"
        if any(s in t for s in CLAIM_REBEL_MARKS):
            return "empire"
    return ""


def duty_mine(m):
    """The P37a fourth mechanical rule for one mission (law text: header)."""
    evs = events_of(m)
    byg = {(e.get("GUID") or "").lower(): e for e in evs}
    ents = m.get("mapEntities") or []
    eidx = {(e.get("GUID") or "").lower(): e for e in ents}

    # buttons: wired to an event / writing a trigger directly
    btn_by_event, btn_by_trig = {}, {}
    for en in ents:
        for b in ((en.get("entityProperties") or {}).get("buttonActions") or []):
            eg = (b.get("eventGUID") or "").lower()
            tg = (b.get("triggerGUID") or "").lower()
            if eg and eg != ZERO and eg in byg:
                btn_by_event.setdefault(eg, []).append((en, b.get("buttonText") or ""))
            if tg and tg != ZERO:
                btn_by_trig.setdefault(tg, []).append((en, b.get("buttonText") or ""))

    # events referenced as an InputPrompt (type 20) dialog outcome
    input_ref = {}
    for e in evs:
        for a in (e.get("eventActions") or []):
            for opt in (a.get("inputList") or []):
                g = (opt.get("eventGUID") or "").lower()
                if g and g != ZERO:
                    input_ref[g] = e.get("name") or ""

    # missionProperties event references (roundLimitEvent etc.)
    prop_refs = set()
    for v in (m.get("missionProperties") or {}).values():
        if isinstance(v, str) and len(v) == 36:
            prop_refs.add(v.lower())

    # positive event writers + dead combat structures per trigger GUID
    evt_writers, struct_writers = {}, set()

    def collect_struct(o, evname):
        if isinstance(o, dict):
            for k, v in o.items():
                if k in ("defeatedTrigger", "setTrigger", "foundTrigger") \
                        and isinstance(v, str) and v.lower() not in ("", ZERO):
                    struct_writers.add(v.lower())
                elif k != "additionalTriggers":
                    collect_struct(v, evname)
        elif isinstance(o, list):
            for v in o:
                collect_struct(v, evname)

    for e in evs:
        for a in (e.get("eventActions") or []):
            if a.get("eventActionType") == A_MODIFY_VARIABLE:
                for t in (a.get("triggerList") or []):
                    g = (t.get("triggerGUID") or "").lower()
                    if (t.get("setValue") or 0) >= 1 or (t.get("modifyValue") or 0) >= 1:
                        evt_writers.setdefault(g, []).append(e)
        collect_struct(e.get("eventActions") or [], e.get("name") or "")
    collect_struct(m.get("initialDeploymentGroups") or [], "initialDG")
    collect_struct(m.get("reservedDeploymentGroups") or [], "reservedDG")

    def has_end(e):
        return any(a.get("endMission") for a in (e.get("eventActions") or []))

    def canon(e):
        return (e.get("name") or "") + ". " + \
            (e.get("sourceEventText") or e.get("eventText") or "")

    # --- candidates: variables thresholded on the empire-win page ----------
    cands = {}   # trigger guid -> [name, max threshold, [end event dicts]]
    for e in evs:
        if not has_end(e):
            continue
        if outcome_side_of_text(canon(e)) != "rebels":
            continue
        for t in (e.get("additionalTriggers") or []):
            v = t.get("triggerValue") or 0
            if v < 1:
                continue
            g = (t.get("triggerGUID") or "").lower()
            cur = cands.setdefault(g, [t.get("triggerName") or "", 0, []])
            cur[1] = max(cur[1], v)
            cur[2].append({"event": e.get("GUID") or "", "name": e.get("name") or ""})
    if not cands:
        return {"class": "none", "duties": [], "candidates": []}

    def dead_event(e):
        return has_end(e) or (e.get("name") or "").lower().startswith("end of mission")

    def name_is_book(e):
        return bool(BOOK_RE.search(e.get("name") or ""))

    def flags_book(e):
        return any(e.get(f) for f in COMBAT_FLAGS)

    def chain_qual(s):
        """threshold-chain listener: one clean branch qualifies (extern)."""
        for t in (s.get("additionalTriggers") or []):
            tn = t.get("triggerName") or ""
            if BOOK_RE.search(tn):
                continue
            tg = (t.get("triggerGUID") or "").lower()
            writers = [w for w in evt_writers.get(tg, []) if w is not s]
            if not writers:
                # no in-mission event writer: externally booked variable
                return "chain-external:" + tn, None
            for w in writers:
                if dead_event(w) or name_is_book(w) or flags_book(w):
                    continue
                return "chain:" + (w.get("name") or ""), w
        return None, None

    def setter_class(s):
        """returns (class, evidence, chain_writer_event_or_None)."""
        gid = (s.get("GUID") or "").lower()
        if dead_event(s):
            return "DEAD", "self-end-event (Selbst-Buchhaltung)", None
        if name_is_book(s):
            return "BOOK", "book-word in setter name", None
        if flags_book(s):
            return "BOOK", "combat/round flag on setter", None
        if gid in prop_refs:
            return "BOOK", "missionProperties bookkeeping reference", None
        for (en, label) in btn_by_event.get(gid, []):
            if GLYPH_RE.search(label):
                return "HERO", "hero-glyph button '%s' on %s" % (label, en.get("name") or ""), None
            if en.get("entityType") == 6:
                continue  # door law: a door button is door class, no duty evidence
            return ("IMPERIUM",
                    "button '%s' on %s" % (label, en.get("name") or ""),
                    None)
        if gid in input_ref:
            return "IMPERIUM", "input-prompt adjudication via '%s'" % input_ref[gid], None
        if s.get("useActivation"):
            return "IMPERIUM", "dg-activation event", None
        cq, writer = chain_qual(s)
        if cq:
            return "IMPERIUM", cq, writer
        return "UNRESOLVED", "no structural taeter", None

    duties, candidates = [], []
    any_live = False
    for g, (vname, thr, endevs) in sorted(cands.items()):
        live, duty_setters, carriers, chain_writers = [], [], [], []
        for s in evt_writers.get(g, []):
            cls, why, writer = setter_class(s)
            if isinstance(cls, tuple):  # defensive: never silent
                print("FAIL: duty law tuple leak in %s" % vname)
                raise SystemExit(1)
            if cls == "DEAD":
                continue
            live.append({"event": s.get("GUID") or "", "name": s.get("name") or "",
                         "class": cls, "evidence": why})
            if cls == "IMPERIUM":
                duty_setters.append({"event": s.get("GUID") or "",
                                      "name": s.get("name") or "", "evidence": why})
                if writer is not None:
                    chain_writers.append(writer)
                gid = (s.get("GUID") or "").lower()
                for (en, label) in btn_by_event.get(gid, []):
                    if en.get("entityType") != 6 and not GLYPH_RE.search(label):
                        carriers.append((en, label))
        for (en, label) in btn_by_trig.get(g, []):
            if GLYPH_RE.search(label):
                live.append({"event": "", "name": "button:" + label,
                             "class": "HERO", "evidence": "hero-glyph direct button"})
                continue
            if en.get("entityType") == 6:
                live.append({"event": "", "name": "button:" + label,
                             "class": "DOOR", "evidence": "door law - not duty evidence"})
                continue
            if en.get("entityType") in (2, 4) or MARK_RE.search(label) \
                    or MARK_RE.search(vname or ""):
                live.append({"event": "", "name": "button:" + label,
                             "class": "IMPERIUM",
                             "evidence": "direct field/object button on %s" % (en.get("name") or "")})
                duty_setters.append({"event": "", "name": "button:" + label,
                                      "evidence": "button-direct on " + (en.get("name") or "")})
                carriers.append((en, label))
            else:
                live.append({"event": "", "name": "button:" + label,
                             "class": "UNRESOLVED",
                             "evidence": "direct button on unmarked field %s" % (en.get("name") or "")})
        if not live:
            continue  # every channel DEAD (self-end / struct-only) -> silence
        any_live = True
        if any(c["class"] == "IMPERIUM" for c in live):
            # anchor export: carriers + tokens spawned (isActive:true) by the
            # duty setters and their qualifying chain writers
            scan = [s for s in evs
                    if (s.get("GUID") or "").lower() in
                    {d["event"].lower() for d in duty_setters if d["event"]}]
            scan += chain_writers
            markers, seen = [], set()
            for s in scan:
                for a in (s.get("eventActions") or []):
                    if a.get("eventActionType") != A_MODIFY_MAP_ENTITY:
                        continue
                    for mod in (a.get("entitiesToModify") or []):
                        src = (mod.get("sourceGUID") or "").lower()
                        en = eidx.get(src)
                        if en is None or en.get("entityType") != 4:
                            continue
                        if not (mod.get("entityProperties") or {}).get("isActive"):
                            continue
                        if src in seen:
                            continue
                        seen.add(src)
                        markers.append({"entity": en.get("GUID") or "",
                                        "name": en.get("name") or ""})
            tf, tfseen = [], set()
            for (en, label) in carriers:
                gu = en.get("GUID") or ""
                if gu in tfseen:
                    continue
                tfseen.add(gu)
                tf.append({"entity": gu, "name": en.get("name") or "",
                           "entityType": ENTITY_TYPES.get(en.get("entityType"), ""),
                           "buttonText": label})
            laws = {d["evidence"].split(" ")[0] for d in duty_setters}
            law = ("button" if laws & {"button", "button-direct", "direct"} else
                   "input-prompt" if laws & {"input-prompt"} else
                   "chain" if laws & {"chain:", "chain-external:"} or any(
                       d["evidence"].startswith("chain") for d in duty_setters) else
                   "dg-activation" if any(d["evidence"].startswith("dg")
                                          for d in duty_setters) else "structural")
            duties.append({"variable": vname, "triggerGuid": g, "threshold": thr,
                           "endEvents": endevs,
                           "setterEvents": duty_setters,
                           "targetFields": tf, "markerTokens": markers,
                           "law": law})
        else:
            candidates.append({"variable": vname, "triggerGuid": g, "threshold": thr,
                               "endEvents": endevs, "channels": live})

    if duties:
        return {"class": "duty", "duties": duties, "candidates": candidates}
    if any_live:
        return {"class": "borderline", "duties": [], "candidates": candidates}
    return {"class": "none", "duties": [], "candidates": []}


# The golden vector (docs/CONCEPT_missions_duty.md 7, Rev 3.3/3.4 + card
# t_1f4d7286): any deviation FAILS the miner - silent adaptation forbidden.
DUTY_GOLDEN = {
    "CORE23.json": ("Imperial Recruits", 16),
    "CORE27.json": ("Imperials escape", 1),
    "CORE28.json": ("Caches destroyed", 5),
    "HOTH10.json": ("Prisoners claimed", 4),
    "HOTH12.json": ("Draylen executed", 1),
    "OTHER9.json": ("Wookiees Bound", 6),
    "OTHER31.json": ("Rebels Lose", 1),
}
BORDERLINE_GOLDEN = {"CORE10.json", "CORE16.json", "EMPIRE2.json", "EMPIRE4.json",
                     "HOTH15.json", "OTHER6.json", "OTHER7.json"}


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
        "dutyAnchors": duty_mine(m),
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

    # P37a fail-closed duty checks (golden vector, concept 7): deviation =
    # exit 1, silent adaptation forbidden. data-exists: every anchor/anchor
    # setter/end GUID of a duty row must live in its own mission file.
    duty_files, borderline_files, none_count = [], [], 0
    for p, row in zip(paths, rows):
        d = row["dutyAnchors"]
        if d["class"] == "duty":
            duty_files.append(row["file"])
        elif d["class"] == "borderline":
            borderline_files.append(row["file"])
        elif d["class"] == "none":
            none_count += 1
        else:
            print("FAIL: %s duty class %r not in {duty,borderline,none}"
                  % (row["file"], d["class"]))
            return 1
        m = json.load(open(p, encoding="utf-8"))
        eg = {e["GUID"].lower() for e in (m.get("mapEntities") or [])}
        evg = {(e.get("GUID") or "").lower() for e in events_of(m)}
        for duty in d["duties"]:
            for tf in duty["targetFields"]:
                if tf["entity"].lower() not in eg:
                    print("FAIL: %s duty targetField %s not in mission"
                          % (row["file"], tf["entity"]))
                    return 1
            for mk in duty["markerTokens"]:
                if mk["entity"].lower() not in eg:
                    print("FAIL: %s duty markerToken %s not in mission"
                          % (row["file"], mk["entity"]))
                    return 1
            for se in duty["setterEvents"]:
                if se["event"] and se["event"].lower() not in evg:
                    print("FAIL: %s duty setterEvent %s not in mission"
                          % (row["file"], se["event"]))
                    return 1
            for ee in duty["endEvents"]:
                if ee["event"].lower() not in evg:
                    print("FAIL: %s duty endEvent %s not in mission"
                          % (row["file"], ee["event"]))
                    return 1
    if sorted(duty_files) != sorted(DUTY_GOLDEN):
        print("FAIL: duty set %s != golden %s"
              % (sorted(duty_files), sorted(DUTY_GOLDEN)))
        return 1
    if sorted(borderline_files) != sorted(BORDERLINE_GOLDEN):
        print("FAIL: borderline set %s != golden %s"
              % (sorted(borderline_files), sorted(BORDERLINE_GOLDEN)))
        return 1
    for row in rows:
        if row["file"] in DUTY_GOLDEN:
            want_var, want_thr = DUTY_GOLDEN[row["file"]]
            got = [(x["variable"], x["threshold"]) for x in row["dutyAnchors"]["duties"]]
            if got != [(want_var, want_thr)]:
                print("FAIL: %s duty %s != golden %s"
                      % (row["file"], got, [(want_var, want_thr)]))
                return 1
    if none_count != 138 - 7 - 7:
        print("FAIL: ruhe count %d != 124" % none_count)
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
                     "Hole law: GUIDs and app-authored names only, no coordinates. "
                     "P37a dutyAnchors (fourth mechanical rule, card t_1f4d7286): "
                     "per mission class duty|borderline|none - a mission variable "
                     "thresholded (additionalTriggers) on an end event of the "
                     "empire-win page (producer OutcomeSideOfText mirror, fixed "
                     "order) whose setter channel is a button / direct field button "
                     "/ DG activation / input-prompt / threshold-chain with the "
                     "imperial taeter direction; combat/round bookkeeping (dead- "
                     "defeat-wound-round-story words, self-end events, defeat "
                     "structures) never reaches duty; unresolved taeter lands in "
                     "borderline (Text-QA list, concept 7). Golden vector fail- "
                     "closed: 7 duty + 7 borderline + 124 ruhe."),
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
    anchor_rows = sum(len(x["targetFields"]) + len(x["markerTokens"])
                      for row in rows for x in row["dutyAnchors"]["duties"])
    print("duty/borderline/ruhe:     %d/%d/%d (golden 7/7/124)"
          % (len(duty_files), len(borderline_files), none_count))
    print("duty anchor entities:     %d targetField+markerToken rows across %d duty missions"
          % (anchor_rows, len(duty_files)))
    print("wrote: %s" % OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
