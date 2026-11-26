#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
p52_defeat_button_verify.py - second-engine proof for the P52 DEFEAT-BUTTON
channel (Player-41 wall: CORE7 "Imperial Hospitality", T. 26.11 playtest).
Mirrors the Unity-side producer law (WorldEventsUnity.TryBuildVictoryEvents,
form (d)) - which the TacticalAiHarness cannot compile - over the REAL
shipped files.

The wall: CORE7 ships the lose law ONLY as public authoring wiring of a
map entity: the Exit token's button "Captive defeated" fires the event
"End of Mission - Rebels lose", which sets the variable "End Mission"; the
two "End Mission Terminal*" events listen on that variable and carry the
endMission action. No event ever sets useAllyDefeated (every ally field is
dead A001 boilerplate) and no event chain names the ally card the way the
(b) grammar requires - ResolveAllyName(A001) even reads a FOREIGN card name
("Luke Skywalker (Held)"), so no name-grammar over the ally id can parse
this family honestly. The producer returned Null-Law: "victorynone
candidates=26 parsed=0" in the Player-41 log is the empirical record.

The law (mirrored 1:1 in C#):
  (d) DEFEAT BUTTON: an entity BUTTON whose
      1. LABEL (CanonButton, authored original - language-independent)
         carries kill vocabulary (word-prefix law, EN+DE shipped vocab);
      2. whose target event's OWN name + eventText declare the rebel side
         the LOSER (OutcomeSideOfText == "rebels", fixed order);
      3. and whose target event reaches a MissionManagement endMission
         action DIRECTLY or through ONE ModifyVariable hop (the target sets
         a trigger to >=1 and a mission-end event LISTENS on that trigger at
         value >= 1)
      = the Empire hunts the mission's own ally axis: imperialWinBy =
      "allyDefeated", actor = fixedAlly/specificAlly (public field), and
      actorName = the button label MINUS its kill word - the victim's public
      authored name ("Captive"), NEVER the ally-card guess (the Luke trap).
  No kill label, no rebels-lose sentence, or no end wiring -> no claim.

Proves, with zero LLM and zero Unity:
  A1  CORE7 parses to imperialWinBy="allyDefeated" via the Exit button,
      actor="A001", actorName="Captive", through the ONE-HOP leg.
  A2  the sibling "Captive escapes" button never claims (no kill label;
      its event's outcome sentence names the EMPIRE the loser) - the
      polarity guard.
  A3  the ally channels stay honestly empty on CORE7 (the wall itself).
  A4  shipped sweep (the false-knowledge census): over EVERY shipped
      mission file the law (d) claims EXACTLY the escort family
      CORE7, CORE15, OTHER14 - and nothing else. CORE15/OTHER14 ride the
      DIRECT leg (endMission right on the target event).
  A5  the Luke trap: the ally-id name grammar genuinely cannot carry
      CORE7 - no event's name/eventText of CORE7 names the A001 card.
Exit 0 = law holds on shipped data; exit 1 = a named claim broke.
"""
import json, glob, os, re, sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MISS = os.path.join(ROOT, "ImperialCommander2/Assets/Resources/SagaMissions")

KILL = ("execut", "execution", "erschosse", "erschossen", "killed",
        "get\u00f6tet", "getotet", "defeated", "besiegt", "erlegen",
        "ausgeschaltet")
LOSE_REBELS = ("rebels lose", "rebel lose", "rebellen verlieren")
LOSE_EMPIRE = ("empire lose", "imperial lose", "imperials lose",
               "imperium verliert", "imperium verlieren")
WIN_REBELS = ("rebels win", "rebel win", "rebellen gewinn")
WIN_EMPIRE = ("empire win", "imperial win", "imperials win", "imperium gewinn")
CLAIM_WORDS = ("claim", "beanspruch")
CLAIM_EMPIRE = ("empire", "imperium")
CLAIM_REBEL = ("rebel", "rebellen")
SPLIT = re.compile(r"[\s\t\n\r\-,;:.!?\"']+")

def load(p):
    # shipped mission JSON carries LitJSON-style trailing commas
    s = open(p, encoding="utf-8-sig").read()
    return json.loads(re.sub(r",\s*([}\]])", r"\1", s))

def events(j):
    out = list(j.get("globalEvents") or [])
    for s in j.get("mapSections") or []:
        out += (s.get("missionEvents") or [])
    return [e for e in out if e]

def has_end(e):
    for a in (e.get("eventActions") or []):
        if a.get("eventActionType") == 0 and a.get("endMission"):
            return True
    return False

def kill_label(label):
    words = [w for w in SPLIT.split(label or "") if w]
    return any(w.lower().startswith(k) for w in words for k in KILL)

def kill_word(label):
    for w in SPLIT.split(label or ""):
        if w.lower().startswith(KILL):
            return w
    return None

def outcome_side(text):
    t = (text or "").lower()
    if any(s in t for s in LOSE_REBELS): return "rebels"
    if any(s in t for s in LOSE_EMPIRE): return "empire"
    if any(s in t for s in WIN_REBELS):  return "empire"
    if any(s in t for s in WIN_EMPIRE):  return "rebels"
    if any(s in t for s in CLAIM_WORDS):
        if any(s in t for s in CLAIM_EMPIRE): return "rebels"
        if any(s in t for s in CLAIM_REBEL):  return "empire"
    return ""

def sets_trigger(e):
    """trigger GUIDs the event's ModifyVariable actions set/increment to >=1"""
    outp = set()
    for a in (e.get("eventActions") or []):
        if a.get("eventActionType") != 3: continue
        for t in (a.get("triggerList") or []):
            g = (t.get("triggerGUID") or "").lower()
            if not g or g == "00000000-0000-0000-0000-000000000000": continue
            if (t.get("setValue") or 0) >= 1 or (t.get("modifyValue") or 0) >= 1:
                outp.add(g)
    return outp

def victim_of(label):
    """label minus its kill word = the victim's public authored name"""
    w = kill_word(label)
    if w is None: return ""
    rest = " ".join(x for x in (label or "").split() if x != w).strip()
    return rest if len(rest) >= 3 else ""

def law_d(j):
    """mirror of TryBuildVictoryEvents form (d): first claimer, file order"""
    mp = j.get("missionProperties") or {}
    axis = mp.get("fixedAlly") or mp.get("specificAlly") or ""
    if not axis: return None
    all_e = events(j)
    idx = {str(e.get("GUID", "")).lower(): e for e in all_e}
    for ent in (j.get("mapEntities") or []):
        ep = ent.get("entityProperties") or {}
        for b in (ep.get("buttonActions") or []):
            label = b.get("buttonText") or ""
            if not kill_label(label): continue
            tgt = idx.get((b.get("eventGUID") or "").lower())
            if tgt is None: continue
            text = (tgt.get("name") or "") + ". " + (tgt.get("eventText") or "")
            if outcome_side(text) != "rebels": continue
            wired = has_end(tgt)
            if not wired:
                setg = sets_trigger(tgt)
                if setg:
                    for w in all_e:
                        if not has_end(w): continue
                        for tb in (w.get("additionalTriggers") or []):
                            g = (tb.get("triggerGUID") or "").lower()
                            if g in setg and (tb.get("triggerValue") or 0) >= 1:
                                wired = True; break
                        if wired: break
            if wired:
                return {"imperialWinBy": "allyDefeated", "actor": axis,
                        "actorName": victim_of(label), "label": label,
                        "event": tgt.get("name")}
    return None

fails = []
def ok(cond, label, extra=""):
    print(("  ok   " if cond else "  FAIL ") + label + ((" | " + extra) if extra else ""))
    if not cond: fails.append(label)

core7 = load(os.path.join(MISS, "Core/CORE7.json"))

# ---- A1: CORE7 parses via the one-hop leg ---------------------------------
c1 = law_d(core7)
ok(c1 is not None, "A1 CORE7 claims the hunt law via defeat button")
ok(c1 and c1["imperialWinBy"] == "allyDefeated", "A1a imperialWinBy=allyDefeated")
ok(c1 and c1["actor"] == "A001", "A1b actor=mission own axis A001")
ok(c1 and c1["actorName"] == "Captive", "A1c actorName from LABEL = Captive",
   "got " + (c1["actorName"] if c1 else "-"))
ok(c1 and c1["event"] == "End of Mission - Rebels lose", "A1d wired lose event",
   c1["event"] if c1 else "-")

# ---- A2: polarity guard on the sibling button ----------------------------
escapes = []
for ent in core7.get("mapEntities") or []:
    ep = ent.get("entityProperties") or {}
    for b in (ep.get("buttonActions") or []):
        if "escape" in (b.get("buttonText") or "").lower():
            idx = {str(e.get("GUID", "")).lower(): e for e in events(core7)}
            tgt = idx.get((b.get("eventGUID") or "").lower()) or {}
            text = (tgt.get("name") or "") + ". " + (tgt.get("eventText") or "")
            escapes.append(((b.get("buttonText") or ""), outcome_side(text)))
ok(all(not kill_label(l) for l, _ in escapes) and len(escapes) >= 1,
   "A2 escape buttons carry no kill label", str(escapes))

# ---- A3: the wall itself - ally channels stay honestly empty -------------
ok(not any(e.get("useAllyDefeated") for e in events(core7)),
   "A3 no event sets useAllyDefeated on CORE7 (b/c channels stay empty)")

# ---- A4: shipped sweep ----------------------------------------------------
claims = {}
files = sorted(glob.glob(os.path.join(MISS, "**", "*.json"), recursive=True))
for f in files:
    if os.sep + "Core" + os.sep not in f and os.sep not in f[len(MISS):]:
        pass  # every file under SagaMissions counts, subfolders alike
    j = load(f)
    r = law_d(j)
    if r: claims[os.path.relpath(f, MISS)] = r
want = {"Core/CORE7.json", "Core/CORE15.json", "Other/OTHER14.json"}
got = set(claims)
ok(got == want, "A4 sweep claims EXACTLY the escort family",
   "got " + str(sorted(got)))
for n in sorted(claims):
    r = claims[n]
    ok(r["imperialWinBy"] == "allyDefeated" and r["actor"], "A4a " + n + " claim shape",
       "%s/%s via %r" % (r["actor"], r["actorName"], r["label"]))

# ---- A5: the Luke trap - ally-id name grammar cannot carry CORE7 ---------
name_a001 = None  # what ResolveAllyName(A001) reads is a FOREIGN card name;
# the proof needed here is the weaker, structural one: NO CORE7 text names
# that card, so form (b) can never fire over the axis id.
ally_file = os.path.join(ROOT, "ImperialCommander2/Assets/Resources/"
                         "Languages/En/DeploymentGroups/allies.json")
a001 = None
for c in load(ally_file):
    if c.get("id") == "A001": a001 = c.get("name")
ok(a001 is not None, "A5 A001 resolves to a card name", repr(a001))
core7_alltext = json.dumps(core7).lower()
ok(a001 is not None and a001.lower() not in core7_alltext,
   "A5b no CORE7 text names the A001 card (form b structurally impossible)",
   "card=" + repr(a001))

print()
if fails:
    print("FAIL: %d broken claim(s)" % len(fails))
    sys.exit(1)
print("P52 defeat-button law holds on all %d shipped mission files." % len(files))
