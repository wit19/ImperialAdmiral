#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
p45_hero_withdraw_verify.py - second-engine proof for the P45 HERO-WITHDRAW
channel (Player-40 wall: side mission CORE25 "Temptation"). Mirrors the
Unity-side producer law (WorldEventsUnity.TryBuildVictoryEvents, form (c))
- which the TacticalAiHarness cannot compile - over the REAL shipped files.

Proves, with zero LLM and zero Unity:
  A1  CORE25 parses to imperialWinBy="heroWithdraws": the end event
      "End of Mission - Rebels lose" carries useHeroWithdraws with
      heroWithdraws == missionProperties.specificHero (public fields of
      the SAME file) AND a MissionManagement endMission action.
  A2  the ally channels stay honestly empty on CORE25 (the wall): no event
      has useAllyDefeated set (every event ships dead allyDefeated="A001"
      boilerplate), so the pre-P45 producer returned Null-Law - the
      Player-40 log line "victorynone candidates=20 parsed=0" IS the
      empirical record of this shape.
  A3  polarity guard: the WIN event ("End of Mission - Rebels win")
      carries NO useHeroWithdraws wiring - it can never flip the channel
      into a false hunt claim.
  A4  shipped sweep (the false-knowledge census): over EVERY shipped
      mission file, the withdraw form (useHeroWithdraws + slot-equal
      specificHero + end action) matches EXACTLY ONE mission - CORE25 -
      and a watcher with heroWithdraws != specificHero matches 0 times
      (the slot-equality guard has nothing to reject because no mission
      ever wires a foreign hero to an end event).
Exit 0 = law holds on shipped data; exit 1 = a named claim broke.
"""
import json, glob, os, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MISS = os.path.join(ROOT, "ImperialCommander2/Assets/Resources/SagaMissions")
CORE25 = os.path.join(MISS, "Core/CORE25.json")

fails = []
def ok(cond, label, extra=""):
    print(("  ok   " if cond else "  FAIL ") + label + ((" | " + extra) if extra else ""))
    if not cond:
        fails.append(label)

def load(p):
    with open(p, encoding="utf-8-sig") as f:
        return json.load(f)

def all_events(d):
    evs = list(d.get("globalEvents") or [])
    for s in d.get("mapSections") or []:
        evs += list(s.get("missionEvents") or [])
    return evs

def has_end_action(e):
    # mirror of HasEndAction: a MissionManagement action with endMission=true
    for a in (e.get("eventActions") or []):
        if a.get("endMission") is True:
            return True
    return False

# ---------- A1/A2/A3 on the real CORE25 ----------
d = load(CORE25)
mp = d["missionProperties"]
evs = all_events(d)
axis = mp.get("fixedAlly") or mp.get("specificAlly") or ""
spec = mp.get("specificHero") or ""

ok(axis != "" and spec == "H1", "A0 axis gate + specificHero slot",
   "axis=%s specificHero=%s" % (axis, spec))

direct = [e.get("name") for e in evs
          if e.get("useAllyDefeated") and e.get("allyDefeated") == axis
          and has_end_action(e)]
ok(direct == [], "A2 ally-direct parses 0 on CORE25 (the Player-40 wall)",
   "boilerplate allyDefeated on %d events, useAllyDefeated set: %d"
   % (sum(1 for e in evs if e.get("allyDefeated")),
      sum(1 for e in evs if e.get("useAllyDefeated"))))

win_form = [e.get("name") for e in evs
            if e.get("useHeroWithdraws")
            and e.get("heroWithdraws") == spec and has_end_action(e)]
ok(win_form == ["End of Mission - Rebels lose"], "A1 withdraw form parses exactly the LOSE event",
   str(win_form))

lose = next(e for e in evs if e.get("name") == "End of Mission - Rebels lose")
win = next(e for e in evs if e.get("name") == "End of Mission - Rebels win")
ok(not lose.get("useHeroWithdraws") is False and win.get("useHeroWithdraws") is not True,
   "A3 WIN event carries no withdraw wiring (polarity guard)")

# the exact scalars the Unity producer will emit (form c identity law)
ok(win_form and spec == "H1", "A1b actor identity = the watched hero slot",
   "actor=H1 (specificHero), ally slot '%s' stays boilerplate" % axis)

# ---------- A4 shipped sweep ----------
files = sorted(glob.glob(os.path.join(MISS, "*", "*.json")))
matches, foreign = [], []
for f in files:
    try:
        dd = load(f)
    except Exception:
        continue
    m = dd.get("missionProperties") or {}
    spec2 = m.get("specificHero") or ""
    axis2 = m.get("fixedAlly") or m.get("specificAlly") or ""
    if axis2 == "" :            # producer gate: no ally axis -> no law at all
        continue
    if spec2 == "":
        continue
    for e in all_events(dd):
        if not e.get("useHeroWithdraws") or not has_end_action(e):
            continue
        if e.get("heroWithdraws") == spec2:
            matches.append((os.path.relpath(f, ROOT), e.get("name")))
        else:
            foreign.append((os.path.relpath(f, ROOT), e.get("name"),
                            e.get("heroWithdraws")))
ok([m[0].replace("\\", "/") for m in matches] ==
   ["ImperialCommander2/Assets/Resources/SagaMissions/Core/CORE25.json"],
   "A4 withdraw form matches EXACTLY ONE shipped mission",
   "%d files swept, matches: %s" % (len(files), [m[0].split("/")[-2:] for m in matches]))
ok(foreign == [], "A4b no shipped mission wires a FOREIGN hero-withdraw to an end action",
   str(foreign[:3]))

print()
if fails:
    print("P45 VERIFY FAILED: " + "; ".join(fails))
    sys.exit(1)
print("P45 VERIFY OK - hero-withdraw channel proven on shipped data, "
      "zero false-knowledge exposure across %d mission files." % len(files))
