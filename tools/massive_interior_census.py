#!/usr/bin/env python3
# P41a H1 CENSUS (card t_cf2ff993; docs/CONCEPT_massive_interior_v1.md v1.3
# sections D1, D2, D6, 5-H1, 7, 9). READ-ONLY tool: reads shipped data,
# writes exactly two generated files:
#   docs/MASSIVE_interior_census.md              (EN, generated, no dates)
#   tools/massive_interior_crosswalk_candidates.json  (Core-hop table source)
# Touches NO Assets/, NO ui.json, NO fixtures. Regenerate:
#   python3 tools/massive_interior_census.py     (from repo root)
#
# IDEMPOTENCY LAW (card): a second run reproduces BOTH files BYTE-IDENTICAL.
# Consequences honoured here: no timestamps, no absolute paths, every
# collection sorted before emission, JSON dumped with sort_keys=True, files
# written LF-only. The tool prints the sha256 of both outputs at the end.
#
# FAIL-CLOSED LAW (spec D1/7): "H1 laeuft gegen alle shipped Missions und
# FAILT CLOSED, wenn es eine einzige unmatching enumerierte id gibt - das
# Gesetz verweigert den Ship, wenn die Enumeration Luecken hat." Implemented
# as exit(1) on: mission count != 138; any placed tile without a dimensions
# descriptor; any RR-enumerated core id (19..36 both sides, 38/39 both sides)
# missing from dimensions.json; an authored DP reference whose GUID resolves
# neither to a DeploymentPoint entity nor to a known editor sentinel; a tile
# expansion index outside the Expansion enum; duplicate card ids.
#
# THE PINNED RR ENUMERATION (D1 - the tile enumeration is the LAW, the biome
# field is only evidence; "RR ist die Mutter aller Interior-Regeln", Thomas
# 09.11). The rulebook's Core-tile interior list, quoted into the tool as the
# only authority for the core ban:
#   RR interior (BANNED):  19A-36A and 19B-36B  -> 18 tiles x 2 sides = 36
#   RR exterior (OPEN):    38A/38B, 39A/39B
# Scope of the enumeration is the CORE expansion (the physical base-game
# tiles). Expansion sets that NUMERICALLY overlap (Hoth ids 19..28) are NOT
# covered by this enumeration - the pin is keyed (expansion=="Core", id).
# The MassiveInteriorGate hop (H2) re-pins the enumeration inside TacticalAI
# and fails the build on an unmatched id; this census is the data-side twin
# of that gate.
#
# BIOME CANDIDATE ROWS (D2, spec 5-D2): the expansion crosswalk rows are
# candidates ONLY, pending Thomas signature per row. Candidate reading of
# the BiomeType evidence (Interfaces-Enums.cs:86 order
# Desert, Forest, Halls, Ice, Imperial, Interior, Jabba, Plains, Snow,
# Streets, None, Menu):
#   Interior                 -> BANNED_CANDIDATE (literal interior biome)
#   Streets/Desert/Plains/
#   Snow/Ice/Forest          -> OPEN candidate (outdoor biomes)
#   Halls/Jabba/Imperial/
#   None/Menu                -> AMBIGUOUS candidate
# The runtime reading of an UNVERIFIED row is fail-OPEN + one forensic log
# line per activation (D2). Evidence recorded in the census doc: core
# interior tiles 19..36 all carry biomeA=="Imperial" while the RR pins BOTH
# sides interior - so the label "Imperial" is NOT outdoor evidence, and any
# expansion (Imperial, X) row is exactly the Halls-class ambiguity.
#
# MASSIVE SOURCE (census-level = shipped card data): the live law is
# Core/DeploymentCard.cs:49  IsMassive => Keywords.Contains("Massive");
# keywords are injected from the printed card data (M13t law), i.e. the
# CardData/*.json "keywords" array is the census-level equivalent.
# RUNTIME OVERLAY, measured at this commit (Frischer-Blick 2nd round
# correction - an earlier draft of this header cited a phantom anchor):
# the shipped app tree has NO runtime keyword add/remove at all - grep
# "Massive" across Assets/Scripts outside TacticalAI hits nothing, and
# Saga/Managers/DeploymentManager.cs (591 lines) contains no keyword
# logic. The M13t injection is card-BUILD time. What remains runtime is
# the deployLedger (M14e origins) and manual figure moves - live-session
# facts this census NEVER reads (card law): authored truth is read from
# the mission file directly (initialDeploymentGroups[].pointList and
# Specific event DPs; M14e fills the ledger FROM the same file rows, so
# the file is the single census source).
# CUSTOM-LANE DIVERGENCE (disclosed, not resolved here - P41a ships no
# law): live custom-card keywords take the path Models/
# DeploymentCard.cs:116 keywords = ced.keywords.Split('\n') WITHOUT
# trim, and the two shipped custom Massive strings carry CRLF
# ("Massive\r\n+3 Accuracy"), so the live token is "Massive\r" and
# runtime IsMassive is FALSE for both shipped custom groups today. The
# census reads the AUTHORED INTENT (split on newlines/semicolons,
# trimmed) - the conservative census direction: it marks what the author
# wrote, ships nothing, and stays correct if the \r gap is ever fixed
# (then the already-listed rows arm themselves; if the gap is fixed by
# changing the authored strings, re-running the census shows it).
#
# SCHEMA TRAPS (spec 9, verified against EventActionConverter.cs):
#   * event actions live under "eventActions[]" with "eventActionType"
#     NUMERIC - there is no top-level "actions"/"actionType" assumption here.
#     Deploy types: EnemyDeployment=6, AllyDeployment=7, OptionalDeployment=8,
#     RandomDeployment=9, AddGroupDeployment=10 (converter cases 6..10).
#   * "deploymentPoint" is NUMERICAL in the file: enum DeploymentSpot
#     { Active=0, Specific=1, None=2 } (Interfaces-Enums.cs:64).
#   * ModifyMapEntity=15 carries isActive inside entitiesToModify[]
#     .sourceGUID + .entityProperties - an event can toggle a Deployment
#     Point's visibility at runtime. The census does not filter authored
#     placements by isActive (authored GUID is authored GUID); it reports
#     event-driven DP activation as context rows only.
#   * events appear in several containers (globalEvents[], eventGroups[]
#     .missionEvents[], mapSections[].missionEvents[]); this tool walks the
#     whole mission tree recursively for "eventActions" arrays - structural
#     coverage without container assumptions.
#
# SENTINEL GUIDs: the mission editor writes two placeholder GUIDs into
# pointList/specificDeploymentPoint slots: the null GUID and
# 11111111-1111-1111-1111-111111111111 (which also doubles as the default
# first mapSection GUID, e.g. CORE1). These are UNATTRIBUTED authored slots
# (measured, not guessed): counted and reported, never treated as a tile
# placement. A GUID that is neither a real DeploymentPoint entity nor a
# sentinel fails the run closed.
#
# DP->TILE ASSIGNMENT METHOD (deliverable: "DP-GUID-Zuordnungs-Methode" in
# the doc). No runtime code maps a Deployment Point to a tile (the app works
# through mapSectionOwner only); the census defines the mapping and reports
# its own resolution statistics honestly:
#   tile rect  = Unity corner-anchor AABB around the authored entityPosition
#                with pivot at the corner and rotation about the pivot:
#                  rot 0:   [x, x+w]  x [y, y+h]
#                  rot 90:  [x-h, x]  x [y, y+w]
#                  rot 180: [x-w, x]  x [y-h, y]
#                  rot 270: [x, x+h]  x [y-w, y]
#                w,h from dimensions.json * PX_PER_HEX=10 (same loader and
#                same px/hex constant as tools/section_geometry_census.py,
#                the 07.11 pattern this tool extends).
#   assignment = DP point (entityPosition) inside at least one tile rect
#                (inclusive bounds). Single hit -> resolved. Multi hit ->
#                tiebreak by strict interior, then by the DP's own
#                mapSectionOwner matching the tile's section. If several
#                candidates remain and ALL share the same status in the
#                reading being reported, the DP's STATUS is still resolved
#                (what D3-B needs); identity stays ambiguous. Mixed-status
#                survivors and zero-rect DPs are AMBIGUOUS, reported, and
#                never silently defaulted (spec 9.6: DPs that land on NO
#                tile rect become Ambiguous DPs).
import json
import glob
import os
import re
import sys
import hashlib
from typing import NoReturn

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS_GLOB = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                             "SagaMissions", "*", "*.json")
DIMENSIONS = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                          "dimensions.json")
CARDDATA = [os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                         "CardData", f + ".json")
            for f in ("enemies", "villains", "allies", "heroes")]
WIKI_UNITS = os.path.join(REPO, "wiki_data", "imperial_units.json")
OUT_DOC = os.path.join(REPO, "docs", "MASSIVE_interior_census.md")
OUT_JSON = os.path.join(REPO, "tools", "massive_interior_crosswalk_candidates.json")

MISSIONS_EXPECTED = 138
PX_PER_HEX = 10.0
# Expansion enum order, Common.cs:8: Core, Twin, Hoth, Bespin, Jabba, Empire,
# Lothal, Other. (The 07.11 geometry census list omitted Other - index 7
# tiles exist; this census pins the full enum and fails closed out of range.)
EXPANSIONS = ["Core", "Twin", "Hoth", "Bespin", "Jabba", "Empire", "Lothal", "Other"]
RR_INTERIOR_IDS = set(range(19, 37))          # RR: 19A-36A and 19B-36B
RR_EXTERIOR_IDS = {38, 39}                    # RR: 38A/38B, 39A/39B
OPEN_BIOMES = {"Streets", "Desert", "Plains", "Snow", "Ice", "Forest"}
AMBIG_BIOMES = {"Halls", "Jabba", "Imperial", "None", "Menu"}
NULL_GUID = "00000000-0000-0000-0000-000000000000"
SECTION_SENTINEL = "11111111-1111-1111-1111-111111111111"
SENTINELS = {NULL_GUID, SECTION_SENTINEL}
DEPLOY_ACTION_TYPES = {6: "EnemyDeployment", 7: "AllyDeployment",
                       8: "OptionalDeployment", 9: "RandomDeployment",
                       10: "AddGroupDeployment",
                       # Frischer-Blick review (P41a): converter case 21 is
                       # CustomEnemyDeployment - a REAL deployment channel
                       # whose card is authored INSIDE the action (the
                       # "keywords" string is the live law's card keywords,
                       # so Massive-ness of these custom groups is decided
                       # by the authored string, not by CardData). Missing
                       # it would lose a genuine D6 authored placement
                       # (measured: CORE4 "Custom Deployment: Hijacked
                       # AT-ST", deploymentPoint=1, keyword string "Massive
                       # \r\n+3 Accuracy", DP on a RR-BANNED core tile).
                       21: "CustomEnemyDeployment"}
EPS = 1e-6

# conservative text-permit audit (D6 residual): interior noun AND a
# movement/permit verb within 60 chars of it, same field, EN canonical text.
# Audit-only: log lines + census entries, zero runtime effect (D6 law).
INDOOR_NOUN = (r"(interiors?\b|indoors?\b|throne\s+room|rooms?\b|chambers?\b|"
               r"corridors?|hallways?|compounds?\b|buildings?\b|structures?\b|"
               r"halls?\b|palace|market)")
PERMIT_VERB = (r"(may\s+(?:enter|move|go|deploy|place|use|traverse|pass)|"
               r"can\s+(?:enter|move|go|deploy|place|use|traverse|pass)|"
               r"allowed\s+to|ignore[sd]?(?:\s+\w+){0,3}\s+(?:the\s+)?|"
               r"need\s+not|do(?:es)?\s+not\s+need|enters?\b|entering|"
               r"deploy(?:s|ed|ment)?\s+(?:in|into|inside|on|at)|"
               r"place(?:s|d|ment)?\s+(?:in|into|inside|on|at)|"
               r"move[sd]?\s+(?:in|into|inside|through|onto))")


def fail(msg) -> NoReturn:
    print("INVARIANT: " + msg)
    sys.exit(1)


def load(p):
    """Same loader as the 07.11 geometry census: utf-8-sig + tolerate the
    trailing commas the mission files carry."""
    with open(p, encoding="utf-8-sig") as f:
        s = f.read()
    return json.loads(re.sub(r",\s*([}\]])", r"\1", s))


def rel(p):
    return os.path.relpath(p, REPO).replace(os.sep, "/")


def rect_of(x, y, rot, w, h):
    r = rot % 360
    if abs(r) < EPS:
        return (x, x + w, y, y + h)
    if abs(r - 90) < EPS:
        return (x - h, x, y, y + w)
    if abs(r - 180) < EPS:
        return (x - w, x, y - h, y)
    if abs(r - 270) < EPS:
        return (x, x + h, y - w, y)
    return None


def tile_status(expansion, tid, biome):
    """Return (pinned_status, candidate_status). pinned_status only knows the
    RR enumeration (everything else is UNKNOWN - the law has no row yet);
    candidate_status is the fail-open reading the census reports per D2."""
    pinned = "UNKNOWN"
    candidate = "UNKNOWN"
    if expansion == "Core" and tid in RR_INTERIOR_IDS:
        return ("BANNED", "BANNED")
    if expansion == "Core" and tid in RR_EXTERIOR_IDS:
        return ("OPEN", "OPEN")
    # Strict D1/9.1 reading: the law rows are the RR enumeration (core
    # 19..36 both sides -> Interior; 38/39 -> Outdoor) and SIGNED crosswalk
    # rows only. Every OTHER id - core 1..18/37 included - has NO law row:
    # pinned stays UNKNOWN and the candidate reading is the biome evidence
    # below (runtime: fail-OPEN + forensic log, D2). The census does not
    # widen the enumeration beyond what the RR/Thomas text names.
    if biome == "Interior":
        return ("UNKNOWN", "BANNED_CANDIDATE")
    if biome in OPEN_BIOMES:
        return ("UNKNOWN", "OPEN")
    if biome in AMBIG_BIOMES:
        return ("UNKNOWN", "AMBIGUOUS")
    return ("UNKNOWN", "AMBIGUOUS")


def walk_event_actions(node, out):
    """Recursive structural coverage - see SCHEMA TRAPS header."""
    if isinstance(node, dict):
        ea = node.get("eventActions")
        if isinstance(ea, list):
            for a in ea:
                if isinstance(a, dict):
                    out.append(a)
        for v in node.values():
            walk_event_actions(v, out)
    elif isinstance(node, list):
        for v in node:
            walk_event_actions(v, out)


def text_fields(d):
    """Prose fields of the canonical mission file (EN shipped lane)."""
    out = []
    mp = d.get("missionProperties") or {}
    for k in ("missionDescription", "missionInfo", "additionalMissionInfo",
              "startingObjective"):
        v = mp.get(k)
        if isinstance(v, str) and v.strip():
            out.append(("missionProperties." + k, v))
    seen = set()

    def w(node, path):
        if isinstance(node, dict):
            for k, v in node.items():
                if k in ("eventText", "customText", "repositionInstructions",
                         "theText") and isinstance(v, str) and v.strip():
                    out.append((path + "." + k, v))
                w(v, path + "." + k)
        elif isinstance(node, list):
            for i, v in enumerate(node):
                w(v, path + "[%d]" % i)
    for key in ("globalEvents", "eventGroups", "mapSections",
                "initialDeploymentGroups", "reservedDeploymentGroups"):
        w(d.get(key), key)
    return out


def audit_hit(text):
    """Conservative proximity pattern: noun AND verb within 60 chars."""
    low = re.sub(r"\s+", " ", text)
    for m in re.finditer(INDOOR_NOUN, low, re.I):
        lo = max(0, m.start() - 60)
        hi = min(len(low), m.end() + 60)
        window = low[lo:hi]
        if re.search(PERMIT_VERB, window, re.I):
            snippet = low[max(0, m.start() - 40):m.end() + 40]
            return snippet.strip()
    return None


def main():
    # ---------------- dimensions (biome table, shipped, live) -------------
    if not os.path.exists(DIMENSIONS):
        fail("shipped dimensions.json missing: " + rel(DIMENSIONS))
    dims = {}
    for e in load(DIMENSIONS):
        key = (e["expansion"], int(e["id"]))
        if key in dims:
            fail("duplicate dimensions descriptor " + str(key))
        dims[key] = (int(e["width"]), int(e["height"]),
                     e.get("biomeA") or "None", e.get("biomeB") or "None")
    # RR fail-closed existence: every enumerated id must have a descriptor.
    for i in sorted(RR_INTERIOR_IDS | RR_EXTERIOR_IDS):
        if ("Core", i) not in dims:
            fail("RR-enumerated core tile id %d has NO dimensions descriptor "
                 "- the law refuses to ship on a hole in the enumeration" % i)

    # ---------------- card data (shipped, live IsMassive source) ----------
    cards = {}
    for path in CARDDATA:
        if not os.path.exists(path):
            fail("shipped card data missing: " + rel(path))
        lane = os.path.basename(path)[:-5]
        for c in load(path):
            cid = c.get("id")
            if cid in cards:
                fail("duplicate card id across CardData lanes: " + str(cid))
            kw = c.get("keywords") or []
            cards[cid] = {"lane": lane, "name": c.get("name"),
                          "massive": "Massive" in kw}
    massive_ids = sorted(i for i, c in cards.items() if c["massive"])
    massive_names = {cards[i]["name"] for i in massive_ids}

    # optional wiki cross-check (spec 5-H1); wiki_data/ is git-excluded, so
    # absence is honest-reported in the doc, never guessed around.
    wiki_state = "ABSENT"
    wiki_massive = set()
    if os.path.exists(WIKI_UNITS):
        wiki_state = "PRESENT"
        for u in load(WIKI_UNITS):
            blob = json.dumps(u)
            if "Massive" in blob:
                wiki_massive.add(u.get("name"))

    # ---------------- missions (138 shipped canonical files) --------------
    paths = sorted(glob.glob(MISSIONS_GLOB))
    if len(paths) != MISSIONS_EXPECTED:
        fail("mission count %d != %d shipped" % (len(paths), MISSIONS_EXPECTED))

    # evidence accumulators
    identity_evidence = {}   # (exp,id,side) -> counters
    dp_identity_kind = {"single": 0, "strict": 0, "section": 0,
                        "status_uniform": 0, "ambiguous_mixed": 0,
                        "tileless": 0}
    campaigns = {}           # campaign -> counters
    per_mission = []
    override_rows = []       # authored indoor Massive placements
    massive_channel_rows = []
    reserved_massive_rows = []
    text_audit_rows = []
    event_dp_ref_no_card = []   # optional/random specific-DP refs, info rows
    dp_event_activation = []    # ModifyMapEntity=15 rows touching DPs
    action_type_hist = {}
    authored_slot_stats = {"initial_total": 0, "initial_with_dp": 0,
                           "initial_sentinel": 0,
                           "reserved_total": 0, "reserved_with_dp": 0,
                           "reserved_sentinel": 0, "stale_reserved": 0,
                           "event_real": 0, "event_sentinel": 0,
                           "event_leftover": 0}
    sentinel_hits = {"initial": 0, "reserved": 0, "event": 0}
    massive_missions = {}    # (campaign, mission) -> channels set
    warnings = []

    for p in paths:
        camp = rel(p).split("SagaMissions/")[1].split("/")[0]
        mfile = os.path.basename(p)
        d = load(p)
        c = campaigns.setdefault(camp, {
            "missions": 0, "sections": 0, "tiles": 0, "dps": 0,
            "dp_pinned": {}, "dp_cand": {}, "identity": {},
            "sec_pinned": {}, "sec_cand": {}, "ambig_tiles": 0,
            "mixed_sections": 0, "zero_sections": 0})
        c["missions"] += 1

        # ---- tiles ---------------------------------------------------------
        tiles = []   # (rect, section_guid, exp, tid, side, biome, pinned, cand)
        sections = d.get("mapSections") or []
        sec_tile_sets = {}
        zero_tile_sections = 0
        for sec in sections:
            secguid = str(sec.get("GUID") or "").lower()
            if not (sec.get("mapTiles") or []):
                # D1/9.2: a section with NO tile under it is Unknown ->
                # AMBIGUOUS, never silently OPEN (fail-open is the RUNTIME
                # reading of AMBIGUOUS, the census reading is AMBIGUOUS).
                sec_tile_sets[secguid] = []
                zero_tile_sections += 1
            for t in sec.get("mapTiles") or []:
                try:
                    ei = int(t.get("expansion") or 0)
                except (TypeError, ValueError):
                    fail("%s: tile %s expansion not an enum index: %r"
                         % (mfile, t.get("name"), t.get("expansion")))
                if ei < 0 or ei >= len(EXPANSIONS):
                    fail("%s: tile %s expansion index %d outside the "
                         "Expansion enum (fail-closed)" % (mfile, t.get("name"), ei))
                exp = EXPANSIONS[ei]
                side = "A" if (t.get("tileSide") or "A") == "A" else "B"  # GetBiomeType law
                if t.get("tileSide") not in ("A", "B", None):
                    warnings.append("%s: tile %s tileSide=%r mapped by the "
                                    "GetBiomeType law (non-'A' reads B)"
                                    % (mfile, t.get("name"), t.get("tileSide")))
                tid = str(t.get("tileID"))
                if not tid.isdigit():
                    fail("%s: tile %s has non-numeric tileID %r"
                         % (mfile, t.get("name"), tid))
                tid = int(tid)
                if (exp, tid) not in dims:
                    fail("%s: placed tile %s %d has no dimensions descriptor"
                         % (mfile, exp, tid))
                w, h, ba, bb = dims[(exp, tid)]
                biome = ba if side == "A" else bb
                try:
                    x, y = [float(v) for v in str(t["entityPosition"]).split(",")]
                    rot = float(t.get("entityRotation") or 0.0)
                except (TypeError, ValueError):
                    fail("%s: tile %s has malformed position/rotation: %r %r"
                         % (mfile, t.get("name"), t.get("entityPosition"),
                            t.get("entityRotation")))
                r = rect_of(x, y, rot, w * PX_PER_HEX, h * PX_PER_HEX)
                if r is None:
                    warnings.append("%s: tile %s rotation %s not a 90 deg "
                                    "multiple - rect skipped" % (mfile, t.get("name"), rot))
                    continue
                pinned, cand = tile_status(exp, tid, biome)
                tiles.append((r, secguid, exp, tid, side, biome, pinned, cand))
                sec_tile_sets.setdefault(secguid, []).append((pinned, cand))
                ev = identity_evidence.setdefault((exp, tid, side), {
                    "placements": 0, "dp_anchor": 0, "massive_authored": 0,
                    "biome": biome})
                ev["placements"] += 1
                c["tiles"] += 1
        c["sections"] += len(sections)
        c["zero_sections"] += zero_tile_sections

        # section four-state (spec 9.2) per view
        def sec_state(states, banned_label):
            if not states:
                return "AMBIGUOUS"     # unknown section (no tile under it)
            has_b = any(s[0] == banned_label for s in states)
            has_o = any(s[0] == "OPEN" for s in states)
            has_u = any(s[0] not in ("OPEN", banned_label) for s in states)
            if has_b and has_o:
                return "MIXED"
            if has_u and (has_b or has_o):
                return "MIXED"          # mixed with unknown - MIXED, counted ambig too
            if has_u:
                return "AMBIGUOUS"
            if has_b:
                return "BANNED"
            return "OPEN"
        for sg, states in sec_tile_sets.items():
            sp = sec_state(states, "BANNED")
            # candidate view: interior set = BANNED + BANNED_CANDIDATE
            sc_state = [("BANNED" if cd == "BANNED_CANDIDATE" else cd)
                        for _, cd in states]
            sc = sec_state([(s,) for s in sc_state], "BANNED")
            c["sec_pinned"][sp] = c["sec_pinned"].get(sp, 0) + 1
            c["sec_cand"][sc] = c["sec_cand"].get(sc, 0) + 1
            if sp == "MIXED":
                c["mixed_sections"] += 1
        c["ambig_tiles"] += sum(1 for t in tiles if t[7] == "AMBIGUOUS")

        # ---- Deployment Points (entityType 3) ------------------------------
        dp_entities = {}
        for e in (d.get("mapEntities") or []):
            if e.get("entityType") != 3:
                continue
            try:
                float(str(e["entityPosition"]).split(",")[0])
                float(str(e["entityPosition"]).split(",")[1])
            except (TypeError, ValueError, IndexError):
                fail("%s: DeploymentPoint %s has malformed entityPosition %r"
                     % (mfile, e.get("name"), e.get("entityPosition")))
            dp_entities[str(e["GUID"]).lower()] = e
        dp_rows = {}   # guid -> dict for the per-mission table
        for guid, e in dp_entities.items():
            x, y = [float(v) for v in str(e["entityPosition"]).split(",")]
            dpsec = str(e.get("mapSectionOwner") or "").lower()
            cands = [t for t in tiles
                     if t[0][0] - EPS <= x <= t[0][1] + EPS
                     and t[0][2] - EPS <= y <= t[0][3] + EPS]
            kind = None
            chosen = None
            if len(cands) == 0:
                kind = "tileless"
            elif len(cands) == 1:
                kind, chosen = "single", cands[0]
            else:
                strict = [t for t in cands
                          if t[0][0] + EPS < x < t[0][1] - EPS
                          and t[0][2] + EPS < y < t[0][3] - EPS]
                if len(strict) == 1:
                    kind, chosen = "strict", strict[0]
                else:
                    secf = [t for t in (strict or cands) if t[1] == dpsec]
                    if len(secf) == 1:
                        kind, chosen = "section", secf[0]
                    else:
                        pool = strict or cands
                        if len({t[6] for t in pool}) == 1 and \
                                len({t[7] for t in pool}) == 1:
                            # status resolved in BOTH readings, identity not
                            kind, chosen = "status_uniform", pool[0]
                        else:
                            kind = "ambiguous_mixed"
            dp_identity_kind[kind] += 1
            if chosen is not None:
                identity_evidence[(chosen[2], chosen[3], chosen[4])]["dp_anchor"] += 1
            if kind == "ambiguous_mixed":
                pinned, cand = "AMBIG_MIXED", "AMBIG_MIXED"
            elif kind == "tileless" or chosen is None:
                pinned, cand = "TILELESS", "TILELESS"
            else:
                pinned, cand = chosen[6], chosen[7]
            dp_rows[guid] = {"kind": kind, "pinned": pinned, "cand": cand,
                             "tile": ("%s%s%s" % (chosen[2], chosen[3], chosen[4]))
                                     if chosen else None}
            c["dps"] += 1
            c["dp_pinned"][pinned] = c["dp_pinned"].get(pinned, 0) + 1
            c["dp_cand"][cand] = c["dp_cand"].get(cand, 0) + 1
            c["identity"][kind] = c["identity"].get(kind, 0) + 1

        # per-mission DP mixed-status detail (honest recompute for reporting)
        # ---- authored initial/reserved groups ------------------------------
        def authored(groups_key, total_key, with_key, sent_key):
            rows = []
            for g in d.get(groups_key) or []:
                authored_slot_stats[total_key] += 1
                cid = g.get("cardID")
                name = g.get("cardName")
                if cid and cid not in cards:
                    fail("%s: %s group references card id %r absent from "
                         "shipped CardData (fail-closed - Massive-ness "
                         "cannot be determined)" % (mfile, groups_key, cid))
                massive = bool(cid and cards[cid]["massive"]) or (
                    name in massive_names and not cid)
                pl = [str((g.get("pointList") or [{}])[i].get("GUID", "")).lower()
                      for i in range(len(g.get("pointList") or []))]
                pl = [g2 for g2 in pl if g2]
                real = [g2 for g2 in pl if g2 in dp_entities]
                sent = [g2 for g2 in pl if g2 in SENTINELS]
                bad = [g2 for g2 in pl if g2 not in dp_entities and g2 not in SENTINELS]
                if bad and groups_key.startswith("initial"):
                    fail("%s: %s group %s pointList GUID %s resolves to no "
                         "DeploymentPoint entity and is no sentinel"
                         % (mfile, groups_key, cid, bad[0]))
                # reserved lane measured: 42 shipped files carry stale
                # editor-remnant pointList GUIDs (entity absent from the
                # mission at all). Reserved groups are NOT a D6 channel, so
                # this pre-existing data state is counted and reported, not
                # a run-killer; a stale reference in the INITIAL channel (a
                # D6 channel) still fails closed above.
                for g2 in bad:
                    authored_slot_stats["stale_reserved"] += 1
                    warnings.append(
                        "%s: reserved group %s pointList GUID %s is stale "
                        "(no such entity in the mission) - unattributed"
                        % (mfile, cid, g2))
                if real:
                    # "carrying a real DP GUID" (not merely a slot) - the
                    # shipped fleet measures 293/297 initial groups with a
                    # resolvable DP, matching spec D6's evidence line
                    authored_slot_stats[with_key] += 1
                authored_slot_stats[sent_key] += len(sent)
                if sent:
                    sentinel_hits["initial" if groups_key.startswith("initial") else "reserved"] += len(sent)
                rows.append({"card": (cards[cid]["name"] if cid in cards else name),
                             "cardID": cid, "massive": massive,
                             "dps": real, "sentinels": len(sent)})
            return rows
        init_rows = authored("initialDeploymentGroups", "initial_total",
                             "initial_with_dp", "initial_sentinel")
        res_rows = authored("reservedDeploymentGroups", "reserved_total",
                            "reserved_with_dp", "reserved_sentinel")
        for r in res_rows:
            if r["massive"]:
                for dg in r["dps"]:
                    reserved_massive_rows.append({
                        "campaign": camp, "mission": mfile, "card": r["card"],
                        "tile": dp_rows[dg]["tile"], "pinned": dp_rows[dg]["pinned"],
                        "cand": dp_rows[dg]["cand"]})

        # ---- events --------------------------------------------------------
        actions = []
        walk_event_actions(d, actions)
        for a in actions:
            t = a.get("eventActionType")
            action_type_hist[t] = action_type_hist.get(t, 0) + 1
            if t in (6, 7, 21):
                egd = a.get("enemyGroupData") or {}
                if t == 21:
                    # custom card authored INSIDE the action - Massive-ness
                    # read from the AUTHORED keywords string (trimmed
                    # line-split). The live lane splits on '\n' without
                    # trim, so the shipped CRLF strings currently read
                    # "Massive\r" -> live IsMassive FALSE (disclosed in
                    # the doc, section 1; the census reports authored
                    # intent and ships no law).
                    cid = egd.get("cardID")
                    name = (a.get("customName") or egd.get("cardName")
                            or a.get("displayName") or "CustomEnemyDeployment")
                    kw_lines = [ln.strip() for ln in
                                re.split(r"[\r\n;,]+", a.get("keywords") or "")
                                if ln.strip()]
                    massive = "Massive" in kw_lines
                else:
                    cid = egd.get("cardID") or a.get("allyID")
                    name = a.get("enemyName") or egd.get("cardName") or a.get("allyName")
                    if cid and cid not in cards:
                        fail("%s: event deployment references card id %r absent "
                             "from shipped CardData" % (mfile, cid))
                    massive = bool(cid and cards[cid]["massive"]) or (
                        name in massive_names and not cid)
                spot = a.get("deploymentPoint")
                sdp = str(a.get("specificDeploymentPoint") or "").lower()
                guids = []
                if sdp and sdp != NULL_GUID:
                    if sdp in SENTINELS:
                        authored_slot_stats["event_sentinel"] += 1
                        sentinel_hits["event"] += 1
                    elif sdp in dp_entities:
                        authored_slot_stats["event_real"] += 1
                        guids.append(sdp)
                    else:
                        fail("%s: event specificDeploymentPoint %s resolves to "
                             "no DeploymentPoint entity" % (mfile, sdp))
                # pointList repeats the same DP once per authored figure -
                # set semantics below keep ONE placement per (action, DP)
                for g2 in (egd.get("pointList") or []):
                    gu = str(g2.get("GUID") or "").lower()
                    if not gu:
                        continue
                    if gu in SENTINELS:
                        authored_slot_stats["event_sentinel"] += 1
                        sentinel_hits["event"] += 1
                    elif gu in dp_entities:
                        authored_slot_stats["event_real"] += 1
                        guids.append(gu)
                    else:
                        fail("%s: event enemyGroupData pointList GUID %s "
                             "resolves to no DeploymentPoint entity" % (mfile, gu))
                guids = sorted(set(guids))
                # D6 authored channel = Spot Specific (deploymentPoint==1).
                # A real DP GUID authored while the spot says Active is an
                # editor leftover (runtime deploys it on Active DPs -
                # ImperialPopup.cs:93 clears the override for Active):
                # counted and reported, never an override row.
                if guids and spot != 1:
                    authored_slot_stats["event_leftover"] += len(guids)
                    guids_override = []
                else:
                    guids_override = guids
                if massive:
                    ch = "event%d:%s" % (t, "Specific" if spot == 1 else "Active")
                    massive_channel_rows.append(
                        {"campaign": camp, "mission": mfile, "channel": ch,
                         "card": (cards[cid]["name"] if cid and cid in cards else name),
                         "event": "%s @%s" % (a.get("displayName") or "",
                                              str(a.get("GUID") or "")[:8]),
                         "dps": sorted(set(guids))})
                for dg in guids_override:
                    if massive:
                        override_rows.append({
                            "campaign": camp, "mission": mfile,
                            "channel": "event%d" % t,
                            "card": (cards[cid]["name"] if cid and cid in cards else name),
                            "cardID": cid,
                            "event": "%s @%s" % (a.get("displayName") or "",
                                                 str(a.get("GUID") or "")[:8]),
                            "dp": dg, "tile": dp_rows[dg]["tile"],
                            "dp_kind": dp_rows[dg]["kind"],
                            "pinned": dp_rows[dg]["pinned"],
                            "cand": dp_rows[dg]["cand"]})
            elif t in (8, 9):
                sdp = str(a.get("specificDeploymentPoint") or "").lower()
                if sdp and sdp not in SENTINELS and sdp in dp_entities:
                    event_dp_ref_no_card.append({"campaign": camp, "mission": mfile,
                                                 "type": DEPLOY_ACTION_TYPES[t],
                                                 "tile": dp_rows[sdp]["tile"],
                                                 "pinned": dp_rows[sdp]["pinned"]})
            elif t == 10:
                for g2 in a.get("groupsToAdd") or []:
                    cid = g2.get("id")
                    if cid and cid in cards and cards[cid]["massive"]:
                        massive_channel_rows.append(
                            {"campaign": camp, "mission": mfile,
                             "channel": "event10:AddGroup (no authored DP)",
                             "card": cards[cid]["name"],
                             "event": a.get("displayName") or "", "dps": []})
            elif t == 15:
                for em in a.get("entitiesToModify") or []:
                    src = str(em.get("sourceGUID") or "").lower()
                    if src in dp_entities:
                        dp_event_activation.append(
                            {"campaign": camp, "mission": mfile,
                             "isActive": (em.get("entityProperties") or {}).get("isActive"),
                             "tile": dp_rows[src]["tile"]})
        for r in init_rows:
            if r["massive"]:
                for dg in r["dps"]:
                    override_rows.append({
                        "campaign": camp, "mission": mfile,
                        "channel": "initial",
                        "card": r["card"], "cardID": r["cardID"],
                        "event": "", "dp": dg, "tile": dp_rows[dg]["tile"],
                        "dp_kind": dp_rows[dg]["kind"],
                        "pinned": dp_rows[dg]["pinned"],
                        "cand": dp_rows[dg]["cand"]})
                if not r["dps"]:
                    override_rows.append({
                        "campaign": camp, "mission": mfile, "channel": "initial",
                        "card": r["card"], "cardID": r["cardID"], "event": "",
                        "dp": None, "tile": None, "dp_kind": "sentinel-only"
                        if r["sentinels"] else "no-DP",
                        "pinned": "-", "cand": "-"})

        # massive channel presence per mission (for zero-usable-DP count)
        def note(card, ch):
            if card:
                key = (camp, mfile)
                massive_missions.setdefault(key, set()).add(ch)
        for r in init_rows:
            if r["massive"]:
                note(r["card"], "initial")
        for r in res_rows:
            if r["massive"]:
                note(r["card"], "reserved-pool")
        for r in massive_channel_rows:
            if r["mission"] == mfile:
                note(r["card"], r["channel"].split(":")[0])

        # per-mission row
        pinned_v = c["dp_pinned"]
        mrow = {"campaign": camp, "mission": mfile,
                "sections": len(sections), "tiles": len(sec_tile_sets),
                "dps": len(dp_entities),
                "dp_banned_pinned": sum(1 for v in dp_rows.values() if v["pinned"] == "BANNED"),
                "dp_open_pinned": sum(1 for v in dp_rows.values() if v["pinned"] == "OPEN"),
                "dp_unknown_pinned": sum(1 for v in dp_rows.values()
                                         if v["pinned"] in ("UNKNOWN", "AMBIG_MIXED", "TILELESS")),
                "dp_banned_cand": sum(1 for v in dp_rows.values()
                                      if v["cand"] in ("BANNED", "BANNED_CANDIDATE")),
                "dp_unknown_cand": sum(1 for v in dp_rows.values()
                                       if v["cand"] in ("AMBIGUOUS", "UNKNOWN", "AMBIG_MIXED", "TILELESS")),
                }
        # honest mixed-status detail recompute
        per_mission.append(mrow)

        # ---- text permit audit (audit-only) ---------------------------------
        for field, txt in text_fields(d):
            s = audit_hit(txt)
            if s:
                low = (txt or "").lower()
                rel_massive = ("massive" in low or
                               any(n.lower() in low for n in massive_names))
                text_audit_rows.append({"campaign": camp, "mission": mfile,
                                        "field": field, "snippet": s,
                                        "massive": rel_massive})

    # ---------------- zero-usable-DP missions (spec 5-H1) -------------------
    zero_usable_pinned = []
    zero_usable_cand = []
    for (camp, mfile), chans in sorted(massive_missions.items()):
        # recompute per mission from per_mission rows
        mrow = next(m for m in per_mission
                    if m["campaign"] == camp and m["mission"] == mfile)
        if mrow["dp_banned_pinned"] == mrow["dps"] and mrow["dps"] > 0:
            zero_usable_pinned.append((camp, mfile, sorted(chans)))
        if mrow["dp_banned_cand"] == mrow["dps"] and mrow["dps"] > 0:
            zero_usable_cand.append((camp, mfile, sorted(chans)))

    # ---------------- crosswalk candidate JSON ------------------------------
    rows = []
    for (exp, tid), (w, h, ba, bb) in dims.items():
        for side, biome in (("A", ba), ("B", bb)):
            pinned, cand = tile_status(exp, tid, biome)
            ev = identity_evidence.get((exp, tid, side))
            rr = exp == "Core" and (tid in RR_INTERIOR_IDS or tid in RR_EXTERIOR_IDS)
            rows.append({
                "biome": biome,
                "candidate_status": cand,
                "expansion": exp,
                "evidence": {
                    "authored_massive_placements": (ev or {}).get("massive_authored", 0),
                    "dp_anchor_hits": (ev or {}).get("dp_anchor", 0),
                    "shipped_placements": (ev or {}).get("placements", 0),
                },
                "pinned_status": pinned if rr or exp == "Core" else "UNPINNED",
                "rr_enumerated": rr,
                "side": side,
                "thomas_ruling": None,
                "tileID": tid,
            })
    rows.sort(key=lambda r: (r["expansion"], r["tileID"], r["side"]))
    # authored massive evidence onto the crosswalk rows
    for r in rows:
        ev = identity_evidence.get((r["expansion"], r["tileID"], r["side"]))
        if ev:
            n = sum(1 for o in override_rows
                    if o["tile"] == "%s%s%s" % (r["expansion"], r["tileID"], r["side"]))
            r["evidence"]["authored_massive_placements"] = n

    doc = build_doc(campaigns, per_mission, identity_evidence, dp_identity_kind,
                    massive_ids, cards, wiki_state, wiki_massive,
                    authored_slot_stats, sentinel_hits, override_rows,
                    massive_channel_rows, reserved_massive_rows,
                    event_dp_ref_no_card, dp_event_activation,
                    text_audit_rows, zero_usable_pinned, zero_usable_cand,
                    action_type_hist, warnings)

    os.makedirs(os.path.dirname(OUT_DOC), exist_ok=True)
    with open(OUT_DOC, "w", encoding="utf-8", newline="\n") as f:
        f.write(doc)
    payload = {"generated_by": "tools/massive_interior_census.py - regenerate, "
                               "never hand-edit",
               "law": ("rows are CROSSWALK CANDIDATES only (spec D1/D2): the "
                       "runtime crosswalk may hold RR core enumerations plus "
                       "Thomas-SIGNED rows alone; candidate_status is the "
                       "census reading, thomas_ruling is the signature slot "
                       "(null until signed). An unverified expansion row "
                       "reads fail-OPEN at runtime with a forensic log line "
                       "(D2). Unmatched expansion ids must not enter the "
                       "crosswalk - the MassiveInteriorGate hop ships a "
                       "closed enum (D1)."),
               "schema": "wa.massive_crosswalk/1",
               "rows": rows}
    with open(OUT_JSON, "w", encoding="utf-8", newline="\n") as f:
        json.dump(payload, f, indent=1, sort_keys=True)
        f.write("\n")

    for p in (OUT_DOC, OUT_JSON):
        h = hashlib.sha256(open(p, "rb").read()).hexdigest()
        print("SHA256 %s %s" % (h, rel(p)))
    print("CENSUS OK missions=%d tiles=%d dps=%d massive_cards=%d "
          "override_rows=%d text_hits=%d"
          % (len(paths), sum(c["tiles"] for c in campaigns.values()),
             sum(c["dps"] for c in campaigns.values()), len(massive_ids),
             len(override_rows), len(text_audit_rows)))


def build_doc(campaigns, per_mission, identity_evidence, dp_identity_kind,
              massive_ids, cards, wiki_state, wiki_massive,
              authored_slot_stats, sentinel_hits, override_rows,
              massive_channel_rows, reserved_massive_rows,
              event_dp_ref_no_card, dp_event_activation, text_audit_rows,
              zero_usable_pinned, zero_usable_cand, action_type_hist, warnings):
    L = []
    add = L.append
    add("# Massive Interior Census (H1) - generated")
    add("")
    add("Generated by `tools/massive_interior_census.py` - regenerate, never "
        "hand-edit. Inputs read (all shipped/live): "
        "`Assets/Resources/dimensions.json`, the 138 canonical mission files "
        "`Assets/Resources/SagaMissions/*/*.json`, shipped card data "
        "`Assets/Resources/CardData/{enemies,villains,allies,heroes}.json`; "
        "optional wiki cross-check `wiki_data/imperial_units.json` "
        "(git-excluded). No timestamps by the idempotency law: a second run "
        "reproduces this file byte-identical.")
    add("")
    add("Scope: spec `docs/CONCEPT_massive_interior_v1.md` v1.3 section 5-H1 "
        "(census only - no law ships from this document; every status below "
        "is the RR enumeration or an UNSIGNED candidate row pending a "
        "Thomas signature per row, D2).")
    add("")
    add("## 1. Massive identity (shipped card data)")
    add("")
    add("Live law: `Core/DeploymentCard.cs:49` `IsMassive => "
        "Keywords.Contains(\"Massive\")`, keywords injected from printed card "
        "data (M13t). Census-level equivalent: the CardData `keywords` array.")
    add("")
    add("| cardID | card | lane | expansion |")
    add("|---|---|---|---|")
    for cid in massive_ids:
        c = cards[cid]
        add("| %s | %s | %s | %s |" % (cid, c["name"], c["lane"], "-"))
    add("")
    if wiki_state == "PRESENT":
        cd_names = sorted({cards[i]["name"] for i in massive_ids})
        missing = sorted(set(cd_names) - wiki_massive)
        extra = sorted(wiki_massive - set(cd_names))
        add("wiki cross-check (`wiki_data/imperial_units.json`, "
            "git-excluded file): CardData Massive = %s ; wiki Massive-tagged "
            "= %s ; missing in wiki: %s ; wiki-only: %s. The live source is "
            "CardData (D5/9.0 anchor); the wiki gap is reported, not "
            "acted on." % (", ".join(cd_names), ", ".join(sorted(wiki_massive)),
                           ", ".join(missing) or "none",
                           ", ".join(extra) or "none"))
    else:
        add("wiki cross-check SKIPPED: `wiki_data/imperial_units.json` is "
            "git-excluded and absent from this tree. CardData is the live "
            "IsMassive source (9.0 anchor); the spec's wiki cross-check "
            "could not run here.")
    add("")
    add("Runtime overlay law, for the record and NOT used here (measured at "
        "this commit): the shipped app has NO runtime keyword add/remove - "
        "grep 'Massive' across Assets/Scripts outside TacticalAI hits "
        "nothing; the M13t keyword injection happens at card-BUILD time "
        "from the printed card data. What is runtime is the deployLedger "
        "(M14e origins, filled FROM the same mission-file rows this census "
        "reads directly) and manual figure moves - live-session fields the "
        "census never reads.")
    add("")
    add("Custom-lane divergence (disclosed): the live custom-card keyword "
        "path `Models/DeploymentCard.cs:116` splits the authored keywords "
        "string on '\\n' WITHOUT trimming; the two shipped custom Massive "
        "strings carry CRLF, so the LIVE token is 'Massive\\r' and runtime "
        "IsMassive is currently FALSE for the shipped custom Massive groups "
        "('Custom Deployment: Hijacked AT-ST' CORE4, \"Custom Deployment: "
        "Sorin's AT-ST\" HOTH1). This census reads the AUTHORED INTENT "
        "(trimmed line-split) "
        "- the conservative census direction: it reports what the author "
        "wrote, ships no law, and the listed rows arm themselves if the "
        "'\\r' gap is ever fixed. The gap itself is a live-code finding for "
        "a later hop, not something P41a touches.")
    add("")
    add("## 2. The pinned RR enumeration (law, not evidence)")
    add("")
    add("- **BANNED** - core expansion, tileID 19..36, both sides (RR "
        "interior list 19A-36A and 19B-36B; 36 tile sides).")
    add("- **OPEN**  - core expansion, tileID 38 and 39, both sides (RR "
        "outdoor text, Thomas 08.11 - the only outdoor text the law names).")
    add("- **UNKNOWN** - every other id (core 1..18 and 37 included): the "
        "enumeration ships no law row for them, so the pinned reading stays "
        "UNKNOWN; the candidate reading is the biome evidence (core 1..18 "
        "are Forest/Desert biomes). Runtime reading of an UNKNOWN row is "
        "fail-OPEN + forensic log (D2) - the census never widens the law "
        "beyond the enumerated text.")
    add("- Fail-closed verified: every RR-enumerated core id resolves to a "
        "dimensions descriptor in this run.")
    add("")
    add("Biome-field evidence for the D1 precedence claim: core tiles "
        "19..36 all carry `biomeA=\"Imperial\", biomeB=\"Interior\"` - the "
        "RR pins BOTH sides interior, so the label `Imperial` is NOT outdoor "
        "evidence. Core 39B is `biomeB=\"None\"` and the RR pins it outdoor, "
        "so `None` is not interior evidence either. This is why every "
        "`Halls/Jabba/Imperial/None/Menu` expansion row is an AMBIGUOUS "
        "candidate, never auto-banned.")
    add("")
    add("## 3. Tile inventory (placed in shipped missions)")
    add("")
    add("Every (expansion, tileID, side) actually placed on a shipped map, "
        "with the D2 candidate reading. `pinned` = RR enumeration only "
        "(core rows); `candidate` = census reading of the biome field "
        "(unsigned).")
    add("")
    add("| expansion | tileID | side | biome | placed | DP anchors | pinned | candidate | thomas_ruling |")
    add("|---|---|---|---|---|---|---|---|---|")
    for (exp, tid, side) in sorted(identity_evidence.keys()):
        ev = identity_evidence[(exp, tid, side)]
        pinned, cand = tile_status(exp, tid, ev["biome"])
        rr = exp == "Core" and (tid in RR_INTERIOR_IDS or tid in RR_EXTERIOR_IDS)
        sig = ("RR law (no signature needed)" if rr
               else "unsigned - census decision is Thomas' signature line")
        add("| %s | %d | %s | %s | %d | %d | %s | %s | %s |"
            % (exp, tid, side, ev["biome"], ev["placements"],
               ev["dp_anchor"], pinned, cand, sig))
    add("")
    add("## 4. Sections and DP census, per campaign")
    add("")
    add("DP status under the PINNED reading (BANNED / OPEN / UNKNOWN = no "
        "law row yet / AMBIG_MIXED = seam DPs whose surviving tile candidates "
        "disagree / TILELESS = no tile rect contains the DP - these are the "
        "Ambiguous DPs of spec 9.6, counted, never silently defaulted).")
    add("")
    add("| campaign | missions | sections | tiles | DPs | pinned: BANNED | pinned: OPEN | pinned: UNKNOWN-family | cand: BANNED(+cand) | cand: AMBIGUOUS-family | MIXED sections | AMBIGUOUS sections (cand view) | AMBIGUOUS tiles |")
    add("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    tot = dict.fromkeys(("missions", "sections", "tiles", "dps", "pb", "po",
                         "pu", "cb", "ca", "mix", "ambsec", "amb"), 0)
    for camp in sorted(campaigns):
        c = campaigns[camp]
        pb = c["dp_pinned"].get("BANNED", 0)
        po = c["dp_pinned"].get("OPEN", 0)
        pu = sum(v for k, v in c["dp_pinned"].items()
                 if k not in ("BANNED", "OPEN"))
        cb = c["dp_cand"].get("BANNED", 0) + c["dp_cand"].get("BANNED_CANDIDATE", 0)
        ca = c["dp_cand"].get("AMBIGUOUS", 0) + sum(
            v for k, v in c["dp_cand"].items() if k in ("AMBIG_MIXED", "TILELESS"))
        ambsec = c["sec_cand"].get("AMBIGUOUS", 0)
        add("| %s | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d |"
            % (camp, c["missions"], c["sections"], c["tiles"], c["dps"],
               pb, po, pu, cb, ca, c["mixed_sections"], ambsec,
               c["ambig_tiles"]))
        tot["missions"] += c["missions"]; tot["sections"] += c["sections"]
        tot["tiles"] += c["tiles"]; tot["dps"] += c["dps"]; tot["pb"] += pb
        tot["po"] += po; tot["pu"] += pu; tot["cb"] += cb; tot["ca"] += ca
        tot["mix"] += c["mixed_sections"]; tot["amb"] += c["ambig_tiles"]
        tot["ambsec"] += ambsec
    add("| **ALL** | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d | %d |"
        % (tot["missions"], tot["sections"], tot["tiles"], tot["dps"],
           tot["pb"], tot["po"], tot["pu"], tot["cb"], tot["ca"],
           tot["mix"], tot["ambsec"], tot["amb"]))
    add("")
    add("MIXED share per campaign (spec 5-H1): sections whose tiles disagree "
        "on interiority - see MIXED column. AMBIGUOUS share: AMBIGUOUS tiles "
        "and AMBIGUOUS-family DP columns. Sections with no tile authored "
        "under them (fleet-wide %d) are AMBIGUOUS by the D1 derivation law, "
        "never silently OPEN; the runtime fail-open reading of AMBIGUOUS is "
        "a runtime behaviour (D2), not a census default."
        % sum(c["zero_sections"] for c in campaigns.values()))
    add("")
    add("## 5. Per-mission DP census")
    add("")
    add("| mission | sections | DPs | BANNED | OPEN | UNKNOWN-family | BANNED(cand view) | AMBIG(cand view) |")
    add("|---|---|---|---|---|---|---|---|")
    for m in sorted(per_mission, key=lambda r: (r["campaign"], r["mission"])):
        add("| %s/%s | %d | %d | %d | %d | %d | %d | %d |"
            % (m["campaign"], m["mission"], m["sections"], m["dps"],
               m["dp_banned_pinned"], m["dp_open_pinned"],
               m["dp_unknown_pinned"], m["dp_banned_cand"],
               m["dp_unknown_cand"]))
    add("")
    add("## 6. Authored indoor Massive placements (D6 override candidates)")
    add("")
    add("Channels read from the mission FILE (card law - never the runtime "
        "ledger): `initialDeploymentGroups[].pointList` (M14e fills the "
        "origin-'authored' ledger rows from exactly these) and event "
        "deployments **with `deploymentPoint==1 (Specific)`** "
        "(EnemyDeployment=6, AllyDeployment=7, CustomEnemyDeployment=21 - "
        "converter cases; the custom lane's Massive-ness comes from the "
        "authored `keywords` string - see the custom-lane divergence "
        "disclosed in section 1) via "
        "`specificDeploymentPoint` / `enemyGroupData.pointList`, deduped to "
        "ONE placement per (event action, DP) - the shipped pointList repeats "
        "the same DP once per figure. A real DP GUID authored while the spot "
        "is Active is an editor leftover (runtime clears the override for "
        "Active, ImperialPopup.cs:93): counted, never an override row. "
        "`override_candidate` = VERIFIED-BANNED (RR-pinned) authored tile - "
        "the only row class the D6 override derives from. "
        "`override_unverified` = candidate-BANNED (expansion Interior-biome) "
        "- the ban stays and the landing law handles it until the row is "
        "signed. Slots holding only editor-sentinel GUIDs are unattributed "
        "(never a tile placement). `dp_kind` shows how the DP resolved onto "
        "its tile (section 9).")
    add("")
    add("| card | mission | channel | event | DP tile | dp_kind | pinned | candidate | verdict |")
    add("|---|---|---|---|---|---|---|---|---|")
    for r in sorted(override_rows, key=lambda r: (r["campaign"], r["mission"],
                                                  r["card"], str(r["tile"]))):
        if r["tile"] is None:
            verdict = "unattributed (%s)" % r["dp_kind"]
        elif r["pinned"] == "BANNED":
            verdict = "override_candidate"
        elif r["cand"] in ("BANNED_CANDIDATE",):
            verdict = "override_unverified"
        elif r["pinned"] == "AMBIG_MIXED" or r["pinned"] == "TILELESS":
            verdict = "ambiguous_dp"
        else:
            verdict = "outdoor - no override needed"
        add("| %s | %s/%s | %s | %s | %s | %s | %s | %s | %s |"
            % (r["card"], r["campaign"], r["mission"], r["channel"],
               r["event"][:28], r["tile"] or "-", r["dp_kind"],
               r["pinned"], r["cand"], verdict))
    add("")
    add("Massive appearances WITHOUT an authored Specific DP (context - "
        "reserved threat-buy pool and Active-spot event deploys are NOT D6 "
        "override sources; their indoor landings fall under the landing law, "
        "D3-B):")
    add("")
    add("| card | mission | channel | event |")
    add("|---|---|---|---|")
    seen = set()
    for r in sorted(massive_channel_rows, key=lambda r: (r["campaign"],
                                                         r["mission"], r["card"])):
        key = (r["mission"], r["card"], r["channel"])
        if key in seen:
            continue
        seen.add(key)
        add("| %s | %s/%s | %s | %s |" % (r["card"], r["campaign"],
                                          r["mission"], r["channel"], r["event"][:28]))
    add("")
    if reserved_massive_rows:
        add("Reserved-pool Massive groups pinned to a DP in the file (threat-"
            "purchased deployment; landing-law domain, NOT override domain):")
        add("")
        add("| card | mission | DP tile | pinned | candidate |")
        add("|---|---|---|---|---|")
        for r in sorted(reserved_massive_rows, key=lambda r: (r["campaign"],
                                                              r["mission"], r["card"])):
            add("| %s | %s/%s | %s | %s | %s |" % (r["card"], r["campaign"],
                                                    r["mission"], r["tile"] or "sentinel",
                                                    r["pinned"], r["cand"]))
        add("")
    if event_dp_ref_no_card:
        add("Optional/Random deployments authored onto a Specific DP (no "
            "named card - not override rows, context only):")
        add("")
        add("| mission | action type | DP tile | pinned |")
        add("|---|---|---|---|")
        for r in sorted(event_dp_ref_no_card, key=lambda r: (r["campaign"],
                                                             r["mission"], r["type"])):
            add("| %s/%s | %s | %s | %s |" % (r["campaign"], r["mission"],
                                              r["type"], r["tile"] or "-", r["pinned"]))
        add("")
    add("Authored-slot accounting: initial groups %d total, %d carrying a "
        "resolvable DP GUID, %d sentinel slots; reserved groups %d total, %d "
        "with DP GUIDs, %d sentinel slots, %d STALE slots (reserved-lane "
        "shipped editor remnants - counted, reported, not a run-killer; a "
        "stale reference in an initial/D6 slot would fail the run closed); "
        "event DP GUID refs (Specific and non-Specific lanes together, "
        "PER OCCURRENCE - pointList repeats per figure): %d real, %d "
        "sentinel; LEFTOVER (real GUID under an Active spot, deduped per "
        "(action, DP) - not a D6 override row): %d. (The spec D6 evidence "
        "line '293/297' is re-measured here live.)"
        % (authored_slot_stats["initial_total"],
           authored_slot_stats["initial_with_dp"],
           authored_slot_stats["initial_sentinel"],
           authored_slot_stats["reserved_total"],
           authored_slot_stats["reserved_with_dp"],
           authored_slot_stats["reserved_sentinel"],
           authored_slot_stats["stale_reserved"],
           authored_slot_stats["event_real"],
           authored_slot_stats["event_sentinel"],
           authored_slot_stats["event_leftover"]))
    add("")
    add("## 7. 'Massive has zero usable DPs' count (spec 5-H1)")
    add("")
    add("Missions whose Massive groups have NO Deployment Point outside the "
        "banned set - pinned reading first (RR-only), then candidate reading "
        "(Interior-biome included).")
    add("")
    add("pinned reading: %s" % (", ".join("%s/%s (%s)" % z for z in zero_usable_pinned) or "none"))
    add("")
    add("candidate reading: %s" % (", ".join("%s/%s (%s)" % z for z in zero_usable_cand) or "none"))
    add("")
    add("## 8. Text-permit audit (audit-only, zero runtime effect - D6)")
    add("")
    add("Conservative EN proximity pattern on the canonical shipped texts: "
        "an indoor noun (interior/room/throne room/corridor/compound/"
        "building/market/...) within 60 chars of a movement/permit verb "
        "(may/can enter, deploy in, allowed to, ignore...). Canonical "
        "SagaMissions files are the EN lane; translated `Languages/*` "
        "overlays carry the same mission identity but were NOT audited here "
        "(see section 10). A text permit NEVER creates an override (D6 "
        "residual law): these rows exist to catch missions where the printed "
        "text promises indoor Massive play but no authored indoor placement "
        "was authored. Design follow-up, not an enforcement input.")
    add("")
    add("Two tiers: **tier A** rows ALSO name Massive or one of the six "
        "Massive cards in the same field - candidates for 'text permits "
        "indoor Massive play without an authored indoor placement' (the D6 "
        "residual itself). **tier B** rows are noun+verb only (mostly hero "
        "movement flavor) - kept for reviewer completeness, not findings.")
    add("")
    tier_a = sum(1 for r in text_audit_rows if r["massive"])
    add("Tier A (Massive-relevant): %d ; tier B (pattern-only): %d."
        % (tier_a, len(text_audit_rows) - tier_a))
    add("")
    add("| tier | mission | field | excerpt |")
    add("|---|---|---|---|")
    for r in sorted(text_audit_rows, key=lambda r: (not r["massive"],
                                                    r["campaign"],
                                                    r["mission"], r["field"])):
        snip = r["snippet"].replace("|", "\\|")[:110]
        add("| %s | %s/%s | %s | %s |"
            % ("A" if r["massive"] else "B", r["campaign"], r["mission"],
               r["field"], snip))
    if not text_audit_rows:
        add("| - | (no hits) | | |")
    add("")
    add("## 9. DP identity assignment: measured resolution quality")
    add("")
    total_dp = sum(dp_identity_kind.values())
    add("| kind | count | share |")
    add("|---|---|---|")
    for k in ("single", "strict", "section", "status_uniform",
              "ambiguous_mixed", "tileless"):
        add("| %s | %d | %.1f%% |" % (k, dp_identity_kind[k],
                                       100.0 * dp_identity_kind[k] / total_dp))
    add("")
    add("Method (the DP-GUID assignment the enumeration owes): tile rects "
        "are Unity corner-anchor AABBs rotated about the authored pivot "
        "(rot 0/90/180/270 fold the width/height into [x,x+w]x[y,y+h], "
        "[x-h,x]x[y,y+w], [x-w,x]x[y-h,y], [x,x+h]x[y-w,y]; "
        "PX_PER_HEX=10, same as tools/section_geometry_census.py). A DP "
        "point resolves to the single containing rect; seams tie-break by "
        "strict interior, then by the DP's own mapSectionOwner; surviving "
        "multi-candidates with one shared status still resolve the DP "
        "STATUS (what D3-B needs) while the identity stays reported; "
        "mixed-status survivors and zero-rect DPs are AMBIGUOUS and "
        "explicitly counted (spec 9.6 - no silent default).")
    add("")
    add("## 10. What the enumeration does NOT cover")
    add("")
    add("- **RR scope**: the pinned enumeration is the CORE expansion only. "
        "Hoth tile ids 19..28 NUMERICALLY overlap the RR interior range but "
        "are different physical tiles - the crosswalk keys on "
        "(expansion, tileID, side) and a bare 'id in 19..36' rule would "
        "wrongly ban them (the one error class named in spec 7). The same "
        "keying keeps a Core-19 placed in another campaign correctly BANNED "
        "(same physical tile).")
    add("- **Biome gaps**: no shipped `Halls/Jabba/Imperial/None/Menu` row "
        "is signed. Halls (Bespin/Empire) and Jabba-palace tiles are exactly "
        "the RR 'Hallen' class the rulebook would need its own interior "
        "rules for; `Imperial` appears as the A-side of the core interior "
        "tiles themselves; `None` appears on core 38/39 which the RR pins "
        "outdoor. Every such row stays AMBIGUOUS until a Thomas signature "
        "(the signature slot is `thomas_ruling` in "
        "`tools/massive_interior_crosswalk_candidates.json`, mirrored per "
        "row in the doc's census table). Runtime consequence until signed: "
        "D2 fail-OPEN + one forensic log line per activation.")
    add("- **DP-GUID assignment method**: the app itself never maps a "
        "Deployment Point to a tile (all runtime logic rides mapSectionOwner); "
        "the assignment above is a CENSUS method, not runtime truth. Seam "
        "DPs with mixed-status candidates and tileless DPs are counted in "
        "section 9 and must be treated as Ambiguous DPs (D2 reading = OPEN "
        "+ log) by any later hop until authored data is fixed or a signed "
        "tie-break rule ships.")
    add("- **Sentinel slots**: editor placeholder GUIDs (null GUID and the "
        "default-section GUID) in authored pointList/Specific slots carry a "
        "card without a tile attribution - %d sentinel slots seen. Those "
        "placements can neither confirm nor deny an indoor placement."
        % sum(sentinel_hits.values()))
    add("- **Non-tile channels**: reserved threat-buy groups, Active-spot "
        "event deploys (deploymentPoint==0), Optional/Random deployments and "
        "AddGroupDeployment actions have no authored card x tile pair (or no "
        "named card); indoor Massive play through them is a LANDING-LAW / "
        "picker-pool question (D3-B, M13c pool rule), never a D6 override.")
    add("- **Text audit reach**: EN canonical lane only; the pattern is "
        "deliberately conservative (noun+verb proximity) - phrasings without "
        "an indoor noun (pure door/level wording) are out by design, and "
        "translated lanes can word permits differently.")
    add("- **Runtime overlays**: the runtime `deployLedger` origins (M14e) "
        "and manual figure moves are live-session facts this file-level "
        "census never sees (card law). Measured at this commit there is no "
        "runtime keyword add/remove in the shipped app at all (section 1); "
        "the custom-lane '\\r' divergence is disclosed in section 1.")
    if dp_event_activation:
        add("- **Event-toggled DPs**: ModifyMapEntity(=15) actions toggle "
            "`isActive` on %d authored Deployment Points - an initially "
            "hidden DP can open mid-mission; the census reads authored "
            "placements independent of isActive and lists the toggles as "
            "context below." % len(dp_event_activation))
        add("")
        add("| mission | DP tile | toggled isActive |")
        add("|---|---|---|")
        add("_(table truncated to the first 40 of %d toggles, lexicographic "
            "order - the count above is the census number, the table is "
            "reviewable context)_" % len(dp_event_activation))
        for r in sorted(dp_event_activation, key=lambda r: (r["campaign"],
                                                            r["mission"]))[:40]:
            add("| %s/%s | %s | %s |" % (r["campaign"], r["mission"],
                                         r["tile"] or "-", r["isActive"]))
    if warnings:
        add("")
        add("## 11. Data-quality warnings (honest log)")
        add("")
        uniq_w = sorted(set(warnings))
        add("_(%d warnings total, %d unique, showing the first 40 unique in "
            "lexicographic order)_" % (len(warnings), len(uniq_w)))
        add("")
        for w in uniq_w[:40]:
            add("- " + w)
    add("")
    add("## Appendix: eventActionType histogram (schema transparency)")
    add("")
    add("Every `eventActionType` value met while walking all 138 files - "
        "proves the deploy-type reading (6..10) and ModifyMapEntity=15 "
        "against shipped data instead of top-level guesses:")
    add("")
    add("| type | name | count |")
    add("|---|---|---|")
    names = {6: "EnemyDeployment", 7: "AllyDeployment", 8: "OptionalDeployment",
             9: "RandomDeployment", 10: "AddGroupDeployment",
             15: "ModifyMapEntity", 21: "CustomEnemyDeployment"}
    for t in sorted(action_type_hist):
        add("| %s | %s | %d |" % (t, names.get(t, ""), action_type_hist[t]))
    add("")
    return "\n".join(L)


if __name__ == "__main__":
    main()
