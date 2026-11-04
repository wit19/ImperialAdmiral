#!/usr/bin/env python3
# Generator for Assets/Tests/Fixtures/tacai/dp_anchors.json (M14c P25-C2,
# card t_15ea0fba). INDEPENDENT Python mirror of the Core rank law in
# DeploymentAnchors.cs (same discipline as the sim-exported vectors): the
# expect blocks here are computed by this second implementation, so the C#
# Core and this file only agree if the LAW agrees.
#
# Mirrored laws (fixplan_Player-25_v2 C2):
#   TieKey(name, seed, round) = Ord64(name) ^ (seed ^ max(1,round)*G mod 2^64)
#   Ord64: FNV-1a 64, 0 -> GOLDEN sentinel
#   Choose: sequential challenge in caller order by
#     tier(0 = DP section carries an anchor) > same-section intra dist > TieKey > name > guid
#
# Regenerate: python3 tools/gen_dp_anchor_fixture.py  (from repo root)
import json
import math
import os
import random
import struct

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "ImperialCommander2", "Assets", "Tests", "Fixtures",
                   "tacai", "dp_anchors.json")

M64 = (1 << 64) - 1
G = 0x2545F4914F6CDD1D
SENT = 0x9E3779B97F4A7C15
EPS = 1e-9


def ord64(s):
    h = 1469598103934665603
    for ch in (s or ""):
        h ^= ord(ch)
        h = (h * 1099511628211) & M64
    return SENT if h == 0 else h


def tie_key(name, seed, rnd):
    return ord64(name) ^ ((seed ^ ((max(1, rnd) * G) & M64)) & M64)


def legacy_winner(cands, seed, rnd):
    """Today's single OrderBy chain in the bridge."""
    return min(cands, key=lambda c: (tie_key(c["name"], seed, rnd),
                                     c["name"], c["guid"]))


def section_anchor(c, anchors):
    best, bd = None, float("inf")
    for a in anchors:
        if a["section"].lower() != c["section"].lower():
            continue
        d = (c["x"] - a["x"]) ** 2 + (c["y"] - a["y"]) ** 2
        if d < bd - EPS or (abs(d - bd) <= EPS and best is not None
                            and a["entity"] < best["entity"]):
            best, bd = a, d
    return best, bd


def nearest_any(c, anchors):
    best, bd = None, float("inf")
    for a in anchors:
        d = (c["x"] - a["x"]) ** 2 + (c["y"] - a["y"]) ** 2
        if d < bd - EPS or (abs(d - bd) <= EPS and best is not None
                            and a["entity"] < best["entity"]):
            best, bd = a, d
    return best, bd


def ranked_winner(cands, anchors, seed, rnd):
    """Sequential challenge mirror of Choose()."""
    def key_of(c):
        a, d = section_anchor(c, anchors)
        return (0 if a else 1, d, tie_key(c["name"], seed, rnd), c["name"], c["guid"], a, d)
    best = key_of(cands[0])
    for c in cands[1:]:
        k = key_of(c)
        cmp = 0
        if k[0] != best[0]:
            cmp = k[0] - best[0]
        # same tier 0 and the SAME section: intra distance decides; across
        # sections distances are worthless (fixplan C2) => fall to the seed.
        if cmp == 0 and k[0] == 0 and best[5] is not None and \
                k[5]["section"].lower() == best[5]["section"].lower():
            if k[1] < best[1] - EPS:
                cmp = -1
            elif k[1] > best[1] + EPS:
                cmp = 1
        if cmp == 0 and k[2] != best[2]:
            cmp = -1 if k[2] < best[2] else 1
        if cmp == 0 and k[3] != best[3]:
            cmp = -1 if k[3] < best[3] else 1
        if cmp == 0 and k[4] != best[4]:
            cmp = -1 if k[4] < best[4] else 1
        if cmp < 0:
            best = k
    # rebuild winner by identity: guids are unique in every case below
    win = next(c for c in cands if c["guid"] == best[4])
    if best[5]:
        a, d = best[5], best[6]
        dist = math.sqrt(d)
    else:
        a, d = nearest_any(win, anchors)
        dist = math.sqrt(d) if a else None
    return win, (a["entity"] if a else None), (a["section"] if a else None), dist


SECX = "aaaaaaaa-1111-1111-1111-111111111111"
SECY = "bbbbbbbb-1111-1111-1111-111111111111"
SECC = "cccccccc-1111-1111-1111-111111111111"


def cand(guid, name, section, x, y):
    return {"guid": guid, "name": name, "section": section, "x": x, "y": y}


def anchor(entity, section, x, y):
    return {"entity": entity, "section": section, "x": x, "y": y}


def seed_favoring(winner_name, loser_name, rnd, start=1):
    """A seed that makes winner_name beat loser_name on the LEGACY key, so
    the fixture really pins 'rank beats seed', not a coincidence.
    Closed form: let k be the top bit where the two Ord64 hashes differ;
    A^S < B^S is decided by S's bit k alone (S[k] == A[k]). Higher bits are
    free, so any legal (63-bit, JSON-long friendly) value can be reached by
    choosing free higher bits. seed = S ^ (round*GOLDEN mod 2^64)."""
    A, B = ord64(winner_name), ord64(loser_name)
    x = (A ^ B) & M64
    k = x.bit_length() - 1
    if k < 0:
        raise ValueError("names must not hash equal")
    s = 0
    if (A >> k) & 1:
        s |= 1 << k                      # S[k] == A[k] -> A^S bit k = 0
    for h in range(62, k, -1):           # fill free high bits, keep S < 2^63
        s |= 1 << h
    s0 = (s ^ ((max(1, rnd) * G) & M64)) & (0x7FFFFFFFFFFFFFFF)
    if tie_key(winner_name, s0, rnd) < tie_key(loser_name, s0, rnd):
        return s0
    # masked candidate missed (bit 63 was a decision bit): bounded random
    # retry in the legal 63-bit window - expected ~2 draws.
    rng = random.Random((A ^ B ^ max(1, rnd)) & M64)
    for _ in range(1000):
        cand = rng.getrandbits(63)
        if tie_key(winner_name, cand, rnd) < tie_key(loser_name, cand, rnd):
            return cand
    raise RuntimeError("no seed found")


def rank_case(name, cands, anchors, seed, rnd, note):
    legacy = legacy_winner(cands, seed, rnd)
    win, a_ent, a_sec, dist = ranked_winner(cands, anchors, seed, rnd)
    return {
        "name": name,
        "note": note,
        "seed": seed,
        "round": rnd,
        "table": {"columns": ["missionFile", "entityGuid", "entityPosition", "sectionGuid"],
                  "rows": [["M.json", a["entity"], "%g,%g" % (a["x"], a["y"]), a["section"]]
                           for a in anchors]},
        "missionFile": "M.json",
        "cands": cands,
        "expect": {"guid": win["guid"],
                   "anchorEntity": a_ent, "anchorSection": a_sec,
                   "dist": dist,
                   "legacyGuid": legacy["guid"]},
    }


def main():
    cases = []

    # (a) section rank beats the seed: the seed FAVORS the foreign DP here.
    a = anchor("aaaaaaaa-0000-0000-0000-0000000000000001", SECX, 10, 10)
    cands = [cand("11111111-aaaa-0000-0000-0000000000000001", "DP Far in X", SECX, 100, 100),
             cand("11111111-aaaa-0000-0000-0000000000000002", "DP Near in Y", SECY, 11, 11)]
    seed = seed_favoring("DP Near in Y", "DP Far in X", 1)  # the seed FAVORS the foreign DP
    cases.append(rank_case("A1_section_beats_seed", cands, [a], seed, 1,
                 "(a): anchor section wins even though the seed favors the foreign DP"))

    # (b) same section: nearer wins even when the seed favors the far one.
    a = anchor("aaaaaaaa-0000-0000-0000-0000000000000002", SECX, 0, 0)
    cands = [cand("11111111-aaaa-0000-0000-0000000000000003", "DP b", SECX, 3, 4),
             cand("11111111-aaaa-0000-0000-0000000000000004", "DP a", SECX, 30, 40)]
    seed = seed_favoring("DP a", "DP b", 2)  # the seed FAVORS the FAR DP
    cases.append(rank_case("B1_nearer_wins_same_section", cands, [a], seed, 2,
                 "(b): intra-section distance beats the seed"))

    # (c) equal distance: the EXISTING seed chain decides (== legacy winner).
    a = anchor("aaaaaaaa-0000-0000-0000-0000000000000003", SECX, 10, 0)
    cands = [cand("11111111-aaaa-0000-0000-0000000000000005", "DP L", SECX, 5, 0),
             cand("11111111-aaaa-0000-0000-0000000000000006", "DP R", SECX, 15, 0)]
    cases.append(rank_case("C1_dist_tie_seed_decides", cands, [a], 42, 3,
                 "(c): equal distance falls to the seed hash (legacy order)"))

    # (c2) two ANCHOR sections: distances are never compared across sections,
    # the seed decides (world distance worthless - fixplan C2 law).
    ax = anchor("aaaaaaaa-0000-0000-0000-0000000000000010", SECX, 0, 0)
    ac = anchor("aaaaaaaa-0000-0000-0000-0000000000000011", SECC, 1000, 0)
    cands = [cand("11111111-aaaa-0000-0000-0000000000000007", "DP C", SECC, 1010, 0),
             cand("11111111-aaaa-0000-0000-0000000000000008", "DP X", SECX, 300, 0)]
    # DP C is the WORLD-nearer DP (10 vs 300) but in another anchor section:
    # a ranker that compared raw world distance across sections would pick
    # C; the law forbids it, so the seed (which favors X) decides.
    seed = seed_favoring("DP X", "DP C", 1)
    cases.append(rank_case("C2_cross_section_seed_decides", cands, [ax, ac], seed, 1,
                 "world distance never compared across sections: seed law stays"))

    # (d) EMPTY anchors: bit-compat with today's single OrderBy chain
    # (Alt-Ergebnis-Pin). Empire 11's real DP names/GUIDs, real round 5.
    cands = [cand("c8062d94-a52f-4458-b2cf-3c7b515747e5", "DP Green 1", SECX, 970, 950),
             cand("5bdffef2-c7c7-4e16-ab66-dca82b0481fd", "DP Green 2", SECX, 1070, 970),
             cand("64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2", "DP Green 3", SECX, 1010, 1030),
             cand("2e35c1c1-e257-4195-ae0a-0b94cbd3e374", "DP Green 4", SECX, 960, 1070)]
    seed = ord64("11111111-2222-3333-4444-555555555555|EMPIRE11") & 0x7FFFFFFFFFFFFFFF
    win, a_ent, a_sec, dist = ranked_winner(cands, [], seed, 5)
    legacy = legacy_winner(cands, seed, 5)
    assert win["guid"] == legacy["guid"], "empty-anchor path must equal legacy"
    cases.append({
        "name": "D1_empty_anchors_legacy_bitcompat",
        "note": "(d): empty anchor list reproduces today's winner exactly",
        "seed": seed,
        "round": 5,
        "table": {"columns": ["missionFile", "entityGuid", "entityPosition", "sectionGuid"],
                  "rows": []},
        "missionFile": "M.json",
        "cands": cands,
        "expect": {"guid": legacy["guid"], "anchorEntity": None,
                   "anchorSection": None, "dist": None, "legacyGuid": legacy["guid"]},
    })

    # (e-livewire) Empire 11 RANKED with the REAL table row + real DP
    # positions: the Informant sits at (1080,1080) in Start Section, the
    # active DPs are Green 1..3 -> the near corner wins on distance.
    informant = ("576c3ba5-801c-42fb-b32f-14109702c8cc",
                 "11111111-1111-1111-1111-111111111111", 1080, 1080)
    cands = [cand("c8062d94-a52f-4458-b2cf-3c7b515747e5", "DP Green 1",
                  "11111111-1111-1111-1111-111111111111", 970, 950),
             cand("5bdffef2-c7c7-4e16-ab66-dca82b0481fd", "DP Green 2",
                  "11111111-1111-1111-1111-111111111111", 1070, 970),
             cand("64b4dbf2-4e39-4ce7-a5fe-94eed25c16d2", "DP Green 3",
                  "11111111-1111-1111-1111-111111111111", 1010, 1030)]
    a = anchor(informant[0], informant[1], informant[2], informant[3])
    cases.append(rank_case("E1_empire11_real_positions", cands, [a], seed, 5,
                 "real Empire 11 geometry: Informant anchor -> nearest active DP wins"))

    # section-foreign winner: no DP in the anchor section -> seed chain,
    # diagnostics print the nearest anchor world distance.
    a = anchor("aaaaaaaa-0000-0000-0000-0000000000000020", SECX, 10, 10)
    cands = [cand("11111111-aaaa-0000-0000-0000000000000009", "DP Q", SECY, 110, 10),
             cand("11111111-aaaa-0000-0000-0000000000000010", "DP P", SECC, 10, 30)]
    cases.append(rank_case("F1_foreign_winner_diag", cands, [a], 77, 1,
                 "no DP in anchor section: legacy chain picks, anchor log = nearest world"))

    table_cases = {
        "match_law": {
            "note": "first column = mission JSON file id, OrdinalIgnoreCase; a title never matches",
            "columns": ["missionFile", "entityGuid", "entityPosition", "sectionGuid",
                        "positionSubtaskIds"],
            "rows": [
                ["EMPIRE11.json", informant[0], "1080,1080", informant[1],
                 "f63855f1-5654-4ede-9fbe-3a13c9c1df2f"],
                ["BESPIN1.json", "6141ffd9-130b-4cca-bf14-478f7f0ab944", "1,2", SECX, ""],
            ],
            "queries": [
                {"file": "EMPIRE11.json", "expect": 1},
                {"file": "empire11.JSON", "expect": 1},
                {"file": "Empire 11", "expect": 0},
                {"file": "", "expect": 0},
            ],
        },
        "junk_law": {
            "note": "ragged/invalid rows do not exist; no position column => no table",
            "columns": ["missionFile", "entityGuid", "entityPosition", "sectionGuid"],
            "rows": [
                ["M.json", "aaaaaaaa-0000-0000-0000-00000000000000aa", "5,5", SECX],
                ["M.json", "shortrow"],
                ["M.json", "aaaaaaaa-0000-0000-0000-00000000000000ab", "abc", SECX],
                ["M.json", "", "9,9", SECX],
            ],
            "queries": [{"file": "M.json", "expect": 1}],
        },
        "missing_column_law": {
            "note": "no sectionGuid column => the whole table is unusable (fail-closed)",
            "columns": ["missionFile", "entityGuid", "entityPosition"],
            "rows": [["M.json", "aaaaaaaa-0000-0000-0000-00000000000000aa", "5,5"]],
            "queries": [{"file": "M.json", "expect": 0}],
        },
    }

    doc = {
        "_meta": {
            "file": "dp_anchors.json",
            "law": "M14c P25-C2 (card t_15ea0fba): DP ranked choice with mission-objective "
                   "anchors. Expect blocks generated INDEPENDENTLY by "
                   "tools/gen_dp_anchor_fixture.py (Python mirror of the Core law), same "
                   "discipline as sim-exported vectors; seed-hash tie chain unchanged "
                   "(TieKey -> name -> guid). Empire11 data pin (informant GUID + section) "
                   "runs against the SHIPPED table file, zero fixtures, inside the harness.",
        },
        "rank_cases": cases,
        "table_cases": table_cases,
    }
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(doc, f, indent=1)
        f.write("\n")
    print("wrote", os.path.relpath(OUT, REPO), "-", len(cases), "rank cases")
    for c in cases:
        print("  %-34s -> %s (legacy=%s)" % (c["name"], c["expect"]["guid"], c["expect"]["legacyGuid"]))


if __name__ == "__main__":
    main()
