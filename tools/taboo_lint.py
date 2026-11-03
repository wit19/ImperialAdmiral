#!/usr/bin/env python3
"""M6e Tabu-Lint (CONCEPT_personas_v2.md Sec.4, Beschluss 6) - RED gate lint.

Fairness-bar law: persona AND player text may never cite a mechanic the
personas must not automate. ANY hit here means a NON-ZERO exit, which the
established gate runner (tools/TacticalAiGate/TabooLintGate.cs -> gate.csproj
-> the QA trio and every run_gates caller) surfaces as a RED gate, stopping
the build chain like every other live gate.

What is checked, exactly per Sec.4:
  1. SHIPPED persona asset Assets/Resources/Languages/<loc>/tactical_personas.json:
     persona-level prose (name/flavor/motto/concept) lints with NO exception
     (same law as validator R4 persona.flavor); every effect's own text
     (name/flavor/source_note/source_ids/concept) plus its machine strings
     (internalNameMatch/factionMatch/elite_card/key) may only cite a taboo
     term under the ESSENCE EXCEPTION: essence:true AND kind inside
     pattern_weight/guard_split/signaturText/elite_swap - the identical rule
     the M6a validator enforces on the draft. The word list is NOT repeated
     here: it is imported from tools/gen_personas_v2.py (single source -
     Beschluss 6 "keine zweite Liste").
  2. Delta-0 statics (Beschluss 10/11): every use of a retired gift kind
     (free_deployment/cost_reduction) inside the personas subtree is RED -
     kind field AND prose. _meta is exempt: it is the retirement DOCUMENTATION
     citing the banned names to forbid them (self-test pins both sides).
     Every elite_swap must hit a Core regular->Core elite pair (validator R2
     via the shared swap_target_bases()) and its elite_card must be a Core
     elite card in the shipped pool; the summed swap amount per figure name
     per persona must stay <= 1 (validator R3 name cap, RRG S.12).
  3. PLAYER TEXT: every persona-owned key value in ALL 11 ui.json (key names
     carrying the persona token - personaCapt*, personaRank*, setPersona, the
     ERKLÄRUNG/DescribeLine/QM vocabulary - plus every signaturText key the
     asset references). Same word list, NO essence exception: player text
     cites no sources.
     Scope law (pinned by self-test): a key is persona-owned when 'persona'
     stands as a whole camelCase token ('persona' + uppercase/digit/end, or a
     capital 'Persona' inside the name); 'personalUC' is prose-personal, not a
     persona key, and stays out.

Self-test (runs on EVERY invocation, on in-memory COPIES - shipped files are
never touched, convention of gen_personas_v2): proves each RED rule can go
red and the two near-miss counter-cases stay green. Exit != 0 on any error,
any self-test failure, or a corpus that is not exactly 11 ui.json.
"""
import copy
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_personas_v2 as G  # noqa: E402 - SHARED word list + swap law (no 2nd list)

APP = Path(__file__).resolve().parent.parent / "ImperialCommander2"
LANG = APP / "Assets" / "Resources" / "Languages"

# Word-boundary scan for the two banned gift kinds (Beschluss 10/11, Delta-0).
GIFT_RE = re.compile(r"\b(free_deployment|cost_reduction)\b")

# persona-owned ui key: 'persona' as a camelCase token start or a capital
# 'Persona' word inside the key name ('setPersona'). 'personalUC' excluded.
PERSONA_KEY_RE = re.compile(r"persona(?=[A-Z0-9_]|$)|Persona")

# The prose + machine string fields of one effect row (asset is FLAT schema).
EFFECT_TEXT_FIELDS = ("name", "flavor", "source_note", "source_ids",
                      "concept", "internalNameMatch", "factionMatch",
                      "elite_card", "key")
PERSONA_TEXT_FIELDS = ("name", "flavor", "motto", "concept")


def _text(value):
    if isinstance(value, list):
        return " ".join(str(v) for v in value)
    return str(value) if value is not None else ""


def lint_asset(asset, pools, where):
    """One shipped tactical_personas.json -> (errors, checks)."""
    errors = []
    checks = 0

    # -- Delta-0 static: gift kinds inside the PERSONAS subtree (raw scan:
    #    catches both a kind field and prose citing the kind as live). _meta
    #    is exempt - it documents the ban (see module docstring).
    raw = json.dumps(asset.get("personas", []), ensure_ascii=False)
    for token in sorted(G.RETIRED_KINDS):
        checks += 1
        n = len(re.findall(r"\b%s\b" % token, raw))
        if n:
            errors.append("DELTA0: %s: retired gift kind '%s' used %d time(s) "
                          "inside personas (Beschluss 10/11, Delta-0)"
                          % (where, token, n))

    for p in asset.get("personas", []):
        pid = p.get("id", "?")
        # persona-level prose: NO essence exception (validator R4 law)
        for field in PERSONA_TEXT_FIELDS:
            value = _text(p.get(field, ""))
            if not value:
                continue
            checks += 1
            hits = sorted(set(h.lower() for h in G.TABOO_RE.findall(value)))
            if hits:
                errors.append("TABOO: %s: persona %s .%s cites taboo %s "
                              "(persona text has no essence exception)"
                              % (where, pid, field, hits))

        swap_totals = {}
        for r in p.get("ranks", []):
            for eff in r.get("effects", []):
                checks += 1
                kind = eff.get("kind", "")
                loc = "%s: %s R%s [%s]" % (where, pid, r.get("rank"), kind)

                # -- taboo lint with the M6a essence exception
                blob = " ".join(_text(eff.get(f, ""))
                                for f in EFFECT_TEXT_FIELDS)
                hits = sorted(set(h.lower() for h in G.TABOO_RE.findall(blob)))
                checks += 1
                if hits:
                    if not eff.get("essence"):
                        errors.append("TABOO: %s: effect cites taboo %s "
                                      "without essence:true" % (loc, hits))
                    elif kind not in G.ESSENCE_ONLY_KINDS:
                        errors.append("TABOO: %s: essence:true but kind %s is "
                                      "no essence channel %s"
                                      % (loc, kind, sorted(G.ESSENCE_ONLY_KINDS)))
                if eff.get("essence") and kind not in G.ESSENCE_ONLY_KINDS:
                    checks += 1
                    errors.append("TABOO: %s: essence:true on a non-essence "
                                  "kind %s (M6a R4 mirror)" % (loc, kind))

                # -- elite_swap laws (shared validator functions - no 2nd rule)
                if kind == "elite_swap":
                    checks += 1
                    paired, hit_bases = G.swap_target_bases(eff, pools)
                    if not paired:
                        errors.append("SWAP: %s: elite_swap hits %s - no Core "
                                      "regular->Core elite pair (Sec.1.1-5)"
                                      % (loc, sorted(hit_bases) or "NO figure"))
                    checks += 1
                    eid = eff.get("elite_card")
                    card = next((f for f in pools["pool"] if f["id"] == eid), None)
                    if card is None:
                        errors.append("SWAP: %s: elite_card '%s' is not a "
                                      "shipped figure id" % (loc, eid))
                    elif card["expansion"] != "Core" or \
                            not card["name"].endswith("(Elite)"):
                        errors.append("SWAP: %s: elite_card '%s' (%s / %s) is "
                                      "not a Core elite card"
                                      % (loc, eid, card["expansion"], card["name"]))
                    amt = eff.get("amount", 1)
                    if not isinstance(amt, int) or isinstance(amt, bool):
                        amt = 0
                    for base in (paired or hit_bases):
                        swap_totals[base] = swap_totals.get(base, 0) + amt
        # name cap per persona (RRG S.12 / Beschluss 12, validator R3 mirror)
        for base, amt in sorted(swap_totals.items()):
            checks += 1
            if amt > 1:
                errors.append("SWAP: %s: persona %s sums swap amount %d for "
                              "figure name '%s' - name cap is 1"
                              % (where, pid, amt, base))
    return errors, checks


def lint_ui(ui, loc, extra_keys):
    """One shipped ui.json -> (errors, checks). No essence exception here."""
    errors = []
    checks = 0
    for section, kv in ui.items():
        if not isinstance(kv, dict):
            continue
        for key, value in kv.items():
            owned = PERSONA_KEY_RE.search(key) or key in extra_keys
            if not owned or not isinstance(value, str):
                continue
            checks += 1
            hits = sorted(set(h.lower() for h in G.TABOO_RE.findall(value)))
            if hits:
                errors.append("TABOO: %s: %s.%s cites taboo %s (player text "
                              "cites no sources - no essence exception)"
                              % (loc, section, key, hits))
    return errors, checks


def run_lint(pools):
    """Full corpus over the SHIPPED files -> (errors, checks, summary)."""
    errors, checks, summary = [], 0, []

    # signaturText keys the asset references are persona-owned by definition
    signature_keys = set()
    asset_paths = sorted(LANG.glob("*/tactical_personas.json"))
    if not asset_paths:
        return (["ASSET: no tactical_personas.json found under Languages/"],
                0, summary)
    for path in asset_paths:
        rel = path.relative_to(APP)
        asset = G.load_lenient(path)
        asset_errors, asset_checks = lint_asset(asset, pools, str(rel))
        errors += asset_errors
        checks += asset_checks
        for p in asset.get("personas", []):
            for r in p.get("ranks", []):
                for eff in r.get("effects", []):
                    if eff.get("kind") == "signaturText" and eff.get("key"):
                        signature_keys.add(eff["key"])
    summary.append("assets: %d file(s)" % len(asset_paths))

    ui_paths = sorted(LANG.glob("*/ui.json"))
    if len(ui_paths) != 11:
        errors.append("UI: expected exactly 11 language ui.json, found %d"
                      % len(ui_paths))
    ui_checks = 0
    for path in ui_paths:
        rel = path.relative_to(APP)
        ui = G.load_lenient(path)
        ui_errors, one = lint_ui(ui, str(rel), signature_keys)
        errors += ui_errors
        ui_checks += one
    checks += ui_checks
    summary.append("ui.json: %d/11 languages, %d persona-owned key values"
                   % (len(ui_paths), ui_checks))
    return errors, checks, summary


# ---------------------------------------------------------------- self-test
# Every rule must be able to go RED; both near-miss counter-cases stay GREEN.
# Mutations run on deep COPIES - the shipped files on disk are never touched.

def _find_effect(asset, pid, kind):
    p = next(x for x in asset["personas"] if x["id"] == pid)
    for r in p["ranks"]:
        for eff in r["effects"]:
            if eff["kind"] == kind:
                return p, eff
    raise AssertionError("no %s effect in %s" % (kind, pid))


def selftest(pools):
    results = []

    def case(name, expect_red, fn):
        asset = G.load_lenient(LANG / "En" / "tactical_personas.json")
        ui = {"uiTacticalAI": {
            "personaCaptFeldherrMotto": "Ordnung durch Uberlegenheit",
            "setExplain": "Erklarung"}}
        try:
            fn(asset, ui)
        except Exception as exc:  # noqa: BLE001 - self-test harness
            results.append((name + " (mutation setup: %s)" % exc, False))
            return
        errs, _ = lint_asset(asset, pools, "selftest")
        errs += lint_ui(ui, "selftest/ui.json", {"personaCaptFeldherrMotto"})[0]
        results.append((name, bool(errs) == expect_red))

    def inject_effect_text(asset, ui):
        _, eff = _find_effect(asset, "trooper_captain", "elite_swap")
        eff["essence"] = False
        eff["source_note"] = "Pierce dice surge reroll"

    def inject_persona_flavor(asset, ui):
        p = next(x for x in asset["personas"] if x["id"] == "feldherr")
        p["flavor"] = "Er sammelt Wuerfel und Wunden."

    def inject_ui_value(asset, ui):
        ui["uiTacticalAI"]["personaCaptFeldherrMotto"] = "Pierce durch jede Deckung"

    def essence_green(asset, ui):  # near-miss: cited source under the exception
        _, eff = _find_effect(asset, "isb_commander", "pattern_weight")
        eff["source_note"] += " (Hidden and gambit cited as source only)"

    def gift_kind(asset, ui):
        _, eff = _find_effect(asset, "hutt_lord", "pattern_weight")
        eff["kind"] = "free_deployment"
        eff["essence"] = True
        eff["internalNameMatch"] = "^Stormtrooper$"
        eff.setdefault("source_note", "")
        eff["concept"] = "gift"

    def gift_prose(asset, ui):
        p = next(x for x in asset["personas"] if x["id"] == "feldherr")
        p["flavor"] = "cost_reduction applies to every unit"

    def gift_meta_green(asset, ui):  # near-miss: retirement docs are exempt
        asset["_meta"]["channels"] = "banned: free_deployment / cost_reduction"

    def unpaired_swap(asset, ui):
        _, eff = _find_effect(asset, "trooper_captain", "elite_swap")
        eff["internalNameMatch"] = "^AT-ST$"
        eff["elite_card"] = "DG003"

    def name_cap(asset, ui):
        _, eff = _find_effect(asset, "trooper_captain", "elite_swap")
        eff["amount"] = 2

    def elite_card_regular(asset, ui):
        _, eff = _find_effect(asset, "trooper_captain", "elite_swap")
        eff["elite_card"] = "DG001"

    def non_persona_key_green(asset, ui):  # near-miss: scope discipline
        ui["uiTitle"] = {"personalUC": "speed dice surge"}
        ui["uiTacticalAI"]["setExplain"] = "pierce reroll"

    case("effect source cites taboo without essence -> RED", True, inject_effect_text)
    case("persona flavor cites taboo -> RED", True, inject_persona_flavor)
    case("ui persona key cites taboo -> RED", True, inject_ui_value)
    case("essence:true pattern_weight cites source -> GREEN", False, essence_green)
    case("retired gift kind as effect kind -> RED", True, gift_kind)
    case("retired gift kind cited in persona prose -> RED", True, gift_prose)
    case("gift kinds inside _meta retirement docs -> GREEN", False, gift_meta_green)
    case("elite_swap filter without Core elite pair -> RED", True, unpaired_swap)
    case("elite_swap amount 2 on one figure name -> RED", True, name_cap)
    case("elite_card pointing at a regular card -> RED", True, elite_card_regular)
    case("taboo in NON-persona ui keys -> GREEN (scope law)", False, non_persona_key_green)
    case("clean shipped asset + ui -> GREEN", False, lambda a, u: None)
    return results


# ---------------------------------------------------------------- main

def main():
    pools = G.load_pools()
    errors, checks, summary = run_lint(pools)

    print("corpus  : %s" % LANG.relative_to(APP))
    for line in summary:
        print("          %s" % line)
    print("wordlist: tools/gen_personas_v2.py TABOO (%d forms) + essence "
          "allowlist - single source" % len(set(G.TABOO)))
    print("lint checks: %d  errors: %d" % (checks, len(errors)))
    for e in errors:
        print("  ERROR " + e)
    verdict = "GRUEN" if not errors else "ROT"
    print("lint: %s" % verdict)

    st = selftest(pools)
    print("self-tests (every RED rule must be able to fire):")
    st_fail = 0
    for name, ok in st:
        print("  %s %s" % ("ok  " if ok else "FAIL", name))
        if not ok:
            st_fail += 1
    print("self-tests: %d/%d ok" % (len(st) - st_fail, len(st)))

    if errors or st_fail:
        sys.exit(1)


if __name__ == "__main__":
    main()
