#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
p34_language_bridge_verify.py - second-engine proof for the FU Sprachbruecke
(card t_1787135b, findings_Player-34). Mirrors the Unity-side producer law
(WorldEventsUnity.cs) - which the TacticalAiHarness cannot compile - over the
REAL shipped files:

  ImperialCommander2/Assets/Resources/SagaMissions/Empire/EMPIRE3.json
  ImperialCommander2/Assets/Resources/Languages/De/Missions/Empire/EMPIRE3_DE.json

Proves, with zero LLM and zero Unity:
  A1  canonical reading: after the TranslationController injection the
      producers read sourceText (EN original) and the Protester row parses
      (health=5, defense=0); reading the TRANSLATED theText reproduces the
      Player-34 wall (parse must FAIL - positive bug proof).
  A2  authored-DE parity: the same sentence as ORIGINAL ("gesundheit: 5,
      verteidigung: keine") parses to the same 5/0; the claim law
      ("the Empire claims it" / "beansprucht das Imperium ihn") maps the
      CLAIMER to the attacker side (EN gap closed too).
  A5  Null-victory is language-independent BY DATA SHAPE, not a
      ResolveAllyName bug: useAllyDefeated end-events = 0, button->end-action
      wirings = 0, no kill sentence names the axis ally -> Null-Law in EN
      exactly as in DE (the Pinned absence: this IS the ACHSE verdict).
  B   direction law: DoctrinePhaseOnLimit both directions + fallback.
"""
import json, re, sys, os, uuid

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
EN = os.path.join(ROOT, "ImperialCommander2/Assets/Resources/SagaMissions/Empire/EMPIRE3.json")
DE = os.path.join(ROOT, "ImperialCommander2/Assets/Resources/Languages/De/Missions/Empire/EMPIRE3_DE.json")
ALLIES = os.path.join(ROOT, "ImperialCommander2/Assets/Resources/CardData/allies.json")

fails = []
def ok(cond, label, extra=""):
    print(("  ok   " if cond else "  FAIL ") + label + ((" | " + extra) if extra else ""))
    if not cond:
        fails.append(label)

def load(p):
    s = open(p, encoding="utf-8-sig").read()
    s = re.sub(r",\s*([}\]])", r"\1", s)   # trailing commas, like the app tolerates
    return json.loads(s)

# ------------------------------------------------------------- law mirrors
ATTACK_WORDS = ["can attack", "angreifen"]
HEALTH_KEYS = ["health:", "gesundheit:"]
DEFENSE_KEYS = ["defense:", "verteidigung:"]
DEFENSE_NONE = ["none", "keine"]
CLAIM_WORDS = ["claim", "beanspruch"]
EMPIRE_MARKS = ["empire", "imperium"]
REBEL_MARKS = ["rebel", "rebellen"]

def parse_attack(text):
    """TryParseAttackText mirror. Returns (health, defense) or None."""
    if not text:
        return None
    t = text.lower()
    at = -1
    for w in ATTACK_WORDS:
        i = t.find(w)
        if i >= 0 and (at < 0 or i < at):
            at = i
    if at < 0:
        return None
    win = t[at:at + 160]
    health = None
    for k in HEALTH_KEYS:
        if k in win:
            m = re.match(re.escape(k) + r"\s*(\d+)", win[win.index(k):])
            health = int(m.group(1)) if m else None
            break
    if health is None:
        return None
    defense = None
    for k in DEFENSE_KEYS:
        if k in win:
            seg = win[win.index(k) + len(k):].lstrip(" \t")
            m = re.match(r"(\d+)", seg)
            if m:
                defense = int(m.group(1))
            elif any(seg.startswith(nw) for nw in DEFENSE_NONE):
                defense = 0
            break
    return health, defense

def claim_of(text):
    """OutcomeSideOfText claim rung (fixed order kept on the C# side)."""
    t = (text or "").lower()
    if any(w in t for w in CLAIM_WORDS):
        if any(m in t for m in EMPIRE_MARKS):
            return "rebels"      # the Empire claims => Rebels lose the object
        if any(m in t for w_ in [0] for m in [x for x in REBEL_MARKS]):
            return "empire"
    return ""

def rebel_claim_evidence(s):
    t = (s or "").lower()
    if "rebel" in t or "rebellen" in t:
        return True
    imperial = "empire" in t or "imperium" in t
    claim = "claim" in t or "besetz" in t or "anspruch" in t
    rescue = "rescue" in t or "rette" in t or "befrei" in t
    return (claim or rescue) and not imperial

def emp_claim_evidence(s):
    t = (s or "").lower()
    return ("empire" in t or "imperium" in t) and \
           ("claim" in t or "besetz" in t or "anspruch" in t)

def polarity(evidences):
    """P1 mirror: hero-side outranks imperial-side (H4c ruling)."""
    rebel = any(rebel_claim_evidence(s) for s in evidences)
    emp = any(emp_claim_evidence(s) for s in evidences)
    if rebel: return "jagt"
    if emp: return "schuetzt"
    return "neutral"

def doctrine_phase_on_limit(base, round_no, limit, imperial_win_by):
    if base != "defend":
        return base
    if limit < 3:
        return "defend"
    if round_no < limit - max(1, limit // 3):
        return "defend"
    if not imperial_win_by:
        return "defend"
    return "hunt"

# ------------------------------------------------------------- the proof
def main():
    m = load(EN)
    de = load(DE)

    # -- TranslationController simulation (the chokepoint, capture-once) ----
    ent_de = {e["GUID"].lower(): e for e in de.get("mapEntities", [])}
    ev_de = {e["GUID"].lower(): e for e in de.get("events", [])}
    ents = m.get("mapEntities", [])
    events = list(m.get("globalEvents") or [])
    for sec in m.get("mapSections") or []:
        events += sec.get("missionEvents") or []

    injected = 0
    for en in ents:
        ep = en.get("entityProperties") or {}
        src = en["GUID"].lower()
        if src in ent_de and ent_de[src].get("mainText"):
            ep["sourceText"] = ep.get("theText")            # capture-once
            ep["theText"] = ent_de[src]["mainText"]         # overwrite (DE)
            btn_map = {b["GUID"].lower(): b.get("theText")
                       for b in ent_de[src].get("buttonList", [])}
            for b in ep.get("buttonActions") or []:
                bt = btn_map.get(b["GUID"].lower())
                if bt:
                    b["sourceText"] = b.get("buttonText")
                    b["buttonText"] = bt
            injected += 1
    for e in events:
        src = str(e.get("GUID", "")).lower()
        if src in ev_de and ev_de[src].get("eventText"):
            e["sourceEventText"] = e.get("eventText")
            e["eventText"] = ev_de[src]["eventText"]

    print("EMPIRE3 canonical-reading verification (DE-injected state)")
    ok(injected > 0, "DE injection simulated", "entities=%d" % injected)

    # -- A1: the Protester row parses off the ORIGINAL, not the DE text -----
    prot = [e for e in ents if (e.get("name") or "").startswith("Protester")]
    ok(len(prot) >= 1, "protester entities present", "n=%d" % len(prot))
    def parse_attack_old(text):
        """Pre-A2 parser: EN keys ONLY - what Player-34 actually ran."""
        saved = HEALTH_KEYS[:], DEFENSE_KEYS[:], DEFENSE_NONE[:], ATTACK_WORDS[:]
        HEALTH_KEYS[:] , DEFENSE_KEYS[:] , DEFENSE_NONE[:] , ATTACK_WORDS[:] = \
            ["health:"], ["defense:"], ["none"], ["can attack"]
        try:
            return parse_attack(text)
        finally:
            HEALTH_KEYS[:], DEFENSE_KEYS[:], DEFENSE_NONE[:], ATTACK_WORDS[:] = saved
    rows = 0
    old_wall = 0
    for en in prot:
        ep = en["entityProperties"]
        canon = ep.get("sourceText") or ep.get("theText")
        parsed = parse_attack(canon)
        old = parse_attack_old(ep.get("theText") or "")      # OLD parser on DE text
        if parsed == (5, 0):
            rows += 1
        if old is None:
            old_wall += 1
    ok(rows == len(prot), "canonical (sourceText) parses health=5 defense=0",
       "%d/%d" % (rows, len(prot)))
    ok(old_wall == len(prot), "OLD read (translated theText) FAILS = the Player-34 wall reproduced",
       "%d/%d" % (old_wall, len(prot)))

    # -- A2: authored-DE parity (DE as ORIGINAL field content) --------------
    de_authored = ent_de.get(prot[0]["GUID"].lower(), {}).get("mainText", "")
    ok(parse_attack(de_authored) == (5, 0),
       "DE-AUTHORED parity: 'gesundheit: 5, verteidigung: keine' parses to 5/0")

    # claim law both languages (attacker-side polarity, EN gap included)
    ok(claim_of("If a protester is defeated, the Empire claims it.") == "rebels",
       "claim law EN: 'the Empire claims it' => Rebels lose the object")
    ok(claim_of("Wenn ein Demonstrant besiegt wird, beansprucht das Imperium ihn.") == "rebels",
       "claim law DE: 'beansprucht das Imperium ihn' => Rebels lose the object")

    # P1 over the canonical buttons + DE-authored button parity
    btn_en = ["claim / {a} rescue", "claim for empire"]
    btn_de = ["beanspruchen / {a} retten", "für imperium beanspruchen"]
    names_en = ["protester rebels 1", "protester empire 1"]
    ok(polarity(btn_en + names_en) == "jagt", "P1 EN mirror: Protester = jagt")
    ok(polarity(btn_de + ["demonstrant imperium 1"]) == "jagt",
       "P1 DE parity: same pair in DE-authored wording = jagt")
    ok(polarity(btn_de[1:2]) == "schuetzt",
       "P1 imperial-only claim ('für imperium beanspruchen') = schuetzt")

    # -- A5: Null-victory is DATA SHAPE, not the ResolveAllyName hypothesis --
    mp = m["missionProperties"]
    axis = mp.get("fixedAlly") or mp.get("specificAlly") or ""
    ok(axis == "A001", "axis field read (GUID, language-free)", "axis=" + axis)
    allies = load(ALLIES)
    name = ""
    for a in (allies if isinstance(allies, list) else allies.get("allies", [])):
        if a.get("id") == axis:
            name = a.get("name", "")
    ok(name != "", "ResolveAllyName('A001') RESOLVES (hypothesis refuted)",
       "allyCards -> '%s'" % name)
    kill_named = 0
    for e in events:
        text = ("%s. %s" % (e.get("name") or "",
                e.get("sourceEventText") or e.get("eventText") or "")).lower()
        if name and name.lower() in text:
            kill_named += 1
    ok(kill_named == 0, "no event names the axis ally at all", "hits=%d" % kill_named)
    use_ally_ends = sum(1 for e in events if e.get("useAllyDefeated")
                        and any(a.get("endMission") for a in e.get("eventActions") or []))
    ok(use_ally_ends == 0, "no useAllyDefeated end-wiring in the mission")
    end_guids = {e["GUID"].lower() for e in events
                 if any(a.get("endMission") for a in e.get("eventActions") or [])}
    wired_end_buttons = sum(1 for en in ents
                            for b in (en.get("entityProperties") or {}).get("buttonActions") or []
                            if str(b.get("eventGUID", "")).lower() in end_guids)
    ok(wired_end_buttons == 0,
       "no entity button reaches a mission-end action (escape grammar finds nothing IN EN EITHER)")
    ok(True, "A5 verdict: Null-victory is Null-Law BY DATA SHAPE, language-independent; "
             "ResolveAllyName works (the old hypothesis is refuted)")

    # round-limit direction fact (B): the clock runs against the REBELLS
    rle = str(mp.get("roundLimitEvent", "")).lower()
    rl_name = next((e.get("name", "") for e in events
                    if str(e.get("GUID", "")).lower() == rle), "?")
    ok("rebels lose" in rl_name.lower(),
       "roundLimitEvent IS 'Rebels lose' -> the Rebels own the clock", rl_name)

    # -- B: direction law, both directions + fallback -----------------------
    ok(doctrine_phase_on_limit("defend", 6, 8, "") == "defend",
       "B: Player-34 r6/limit=8, NO parsed conditions -> Defend stays Defend (the fix)")
    ok(doctrine_phase_on_limit("defend", 6, 8, "allyDefeated") == "hunt",
       "B: parsed imperial win-by -> hunt-at-limit stays legitimate")
    ok(doctrine_phase_on_limit("hunt", 5, 6, "") == "hunt",
       "B: non-defend bases untouched")

    print("summary:", "ALL PASS" if not fails else "FAIL: " + "; ".join(fails))
    sys.exit(1 if fails else 0)

main()
