#!/usr/bin/env python3
# P38 (card t_11ebb843) INDEPENDENT Python twin of the NEUTRALE-BEUTE quota
# law (tools/TacticalAiHarness parity discipline, P37c twin pattern): re-
# derives EVERY dice value, the seed ranking and the Explain disclosure of
# Assets/Tests/Fixtures/tacai/prey_core.json quota_vectors from the law text
# (C# doc-strings in Assets/Scripts/TacticalAI/Core/PreyQuota.cs - fnv1a64,
# fmix64, k band, tie law) and cross-checks them against the C#-generated
# golden pins. Agreement of two independent derivations is the proof; the
# fixture alone never justifies itself.
#
#   python3 tools/p38_prey_twin.py            # exit 0 = all vectors MATCH
#
# Law (PreyQuota.Compute):
#   seed   = fnv1a64(sessionKey)      (0 -> 0xA5A5A5A5A5A5A5A5, P37 seam)
#   rank   = fmix64(fnv1a64("preyrank|" + id + "|r" + round) ^ seed)
#            ranking order: rank ascending, tie -> id ordinal ascending
#   k      = 1 + fmix64(fnv1a64("preyq|r" + round) ^ seed) mod ceil(n/2)
#   chosen = first k of the ranking;  n = 0 -> no quota law at all
# Only hunter/aggressive patterns ride the cohort (the Muster-Gatter set).
import json
import os
import sys

M64 = (1 << 64) - 1


def fnv1a64(s):
    # Mirrors ImperialDecks.Fnv1a64: per UTF-16 code unit. All keys this
    # law hashes are ASCII (session keys, card ids, round salts), so code
    # points and code units coincide; asserted below.
    h = 1469598103934665603
    for ch in s:
        cp = ord(ch)
        assert cp < 0x80, "non-ASCII key would need UTF-16 pairing: " + s
        h = ((h ^ cp) * 1099511628211) & M64
    return h


def fmix64(h):
    h ^= h >> 30
    h = (h * 0xBF58476D1CE4E5B9) & M64
    h ^= h >> 27
    h = (h * 0x94D049BB133111EB) & M64
    return h ^ (h >> 31)


def session_seed(key):
    h = fnv1a64(key if key else "solo")
    return h if h != 0 else 0xA5A5A5A5A5A5A5A5


def rank_hash(seed, card_id, rnd):
    return fmix64(fnv1a64("preyrank|" + card_id + "|r" + str(rnd)) ^ seed)


CHASE = {"hunter", "aggressive"}


def compute(session, rnd, members):
    hs = [m for m in members if m.get("pattern") in CHASE]
    n = len(hs)
    if n == 0:
        return None
    cap = (n + 1) // 2
    seed = session_seed(session)
    ids = [str(m["id"]) for m in hs]
    ranking = sorted(ids, key=lambda i: (rank_hash(seed, i, rnd), i))
    k = 1 + fmix64(fnv1a64("preyq|r" + str(rnd)) ^ seed) % cap
    chosen = ranking[:k]
    explain = ("prey r" + str(rnd) + " n=" + str(n) + " cap=" + str(cap)
               + " k=" + str(k) + " ranking=" + ",".join(ranking)
               + " chosen=" + ",".join(chosen))
    return {"n": n, "cap": cap, "k": k,
            "ranking": ranking, "chosen": chosen, "explain": explain}


def main():
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    fx = os.path.join(repo, "ImperialCommander2", "Assets", "Tests",
                      "Fixtures", "tacai", "prey_core.json")
    doc = json.load(open(fx, encoding="utf-8"))
    cohorts = {c["name"]: c for c in doc["quota_cohorts"]}
    fails = 0
    seen = 0
    ks = []
    for pin in doc["quota_vectors"]:
        parts = pin.split("|")
        name = parts[0]
        cohort = cohorts[name]
        session = str(cohort["session"])
        rnd = int(parts[-2])
        prefix = name + "|" + session + "|" + str(rnd) + "|"
        assert pin.startswith(prefix), "pin prefix drift: " + pin
        want = pin[len(prefix):]
        got = compute(session, rnd, cohort["members"])
        got_str = "" if got is None else str(got["explain"])
        seen += 1
        ok = got_str == want
        if not ok:
            fails += 1
            print("FAIL " + pin)
            print("  twin: " + got_str)
        if got is not None:
            n, cap, k = int(got["n"]), int(got["cap"]), int(got["k"])
            ranking, chosen = got["ranking"], got["chosen"]
            ks.append((k, n, cap))
            # band law + never-all + chosen-prefix re-asserted independently
            assert 1 <= k <= cap, pin
            assert cap == (n + 1) // 2, pin
            assert n < 2 or k < n, pin
            assert chosen == ranking[:k], pin
        else:
            assert want == "", pin
    # session salt moves the dice (same cohort, different key)
    c9 = cohorts["em3_n9"]["members"]
    sa = compute("saga|EMPIRE3", 1, c9)
    sb = compute("solo|EMPIRE3", 1, c9)
    assert sa is not None and sb is not None
    a, b = str(sa["explain"]), str(sb["explain"])
    if a == b:
        fails += 1
        print("FAIL salt does not move the dice")
    # seed-stability: the law is pure - replay equals replay
    for rnd in range(1, 6):
        for cname in ("em3_n9", "em3_n2", "salt"):
            co = cohorts[cname]
            assert (compute(co["session"], rnd, co["members"])
                    == compute(co["session"], rnd, co["members"]))
    print(("TWIN OK" if fails == 0 else "TWIN FAILURES"),
          "| vectors=%d matches=%d fails=%d" % (seen, seen - fails, fails))
    if ks:
        print("k distribution over pinned rounds:",
              " ".join("k=%d/n=%d(cap=%d)" % t for t in ks))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
