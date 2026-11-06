#!/usr/bin/env python3
# M23 Zweit-Engine (Thomas-Ruling "A", 06.11.): unabhaengige Nachrechnung
# aller world_feed-Kanon-Posturen inkl. der sechs neuen M23-Anker.
# Rechnet DerivePosture aus dem Gesetzestext NEU (Schutz vor dem
# Wort-Suffix-Anker, Uhr, Stall-Vokabeln, Eliminate-Fallback) und vergleicht
# gegen die im Fixtur gepinnten posture-Werte. Kein Import der Engine.
import json, re, sys, unicodedata

PROTECT = ["defend", "protect", "guard", "escort", "hold"]
STALL   = ["escape", "flee", "survive", "reach", "activate", "recover",
           "steal", "deliver", "evacuate"]
ATTACK  = ["destroy", "assault", "zerstoer"]  # 'zerstör' NFC-normalisiert

def norm(s):
    # DE-Umlaute vor dem Wort-Split normalisieren (oe->oe wie im Wort
    # 'zerstoert', das im Fixtur als 'Zerst\u00f6rt' steht); der Rest der
    # Rohtext bleibt unveraendert.
    return (s or "").replace("\u00f6", "oe").replace("\u00d6", "oe")

def derive(objective, priority, round_limit, for_empire):
    text = (norm(objective) + " " + norm(priority)).lower()
    words = [w for w in re.split(r"[ \t\n\r\-,;:.!?]+", text) if w]
    def hits(kws):
        return any(w.startswith(k) for k in kws for w in words)
    if hits(PROTECT):
        return "protect"
    if for_empire and hits(ATTACK):
        return "eliminate"
    if round_limit is not None and round_limit >= 0:
        return "stall"
    if hits(STALL):
        return "stall"
    return "eliminate"

def main(path):
    d = json.load(open(path, encoding="utf-8"))
    fails = 0; rows = 0
    for c in d["cases"]:
        if "expect" not in c or "posture" not in c["expect"]:
            continue
        # eigene Projektion: letzte MissionStart + spaetere RoundLimitChanged
        obj, prio, rl, fe = "", "", None, False
        for ev in c["events"]:
            k = ev["kind"]; p = ev.get("payload", {})
            if k == "MissionStart":
                obj, prio = p.get("objective", ""), p.get("priorityTarget", "")
                rl = p.get("roundLimit", -1); fe = p.get("objectiveForEmpire", 0) != 0
            elif k == "RoundLimitChanged":
                rl = p.get("roundLimit", -1)
        got = derive(obj, prio, rl, fe)
        want = c["expect"]["posture"]
        rows += 1
        if got != want:
            fails += 1
            print(f"MISMATCH {c['name']}: port={got} canon={want}")
        elif c["name"].startswith("m23_"):
            print(f"anchor {c['name']}: {got} OK (forEmpire={fe})")
    print(f"rows={rows} fails={fails}")
    return 1 if fails else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else
        "ImperialCommander2/Assets/Tests/Fixtures/tacai/world_feed.json"))
