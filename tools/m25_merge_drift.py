#!/usr/bin/env python3
# M25 surgical canon merge (M21 discipline): replace ONLY the expect values of
# the adjudicated drifted cases, whole-value, byte-style-verified on untouched
# neighbours first. No reformat, no key reorder.
import json, sys

F = "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets/Tests/Fixtures/tacai/"

OF = ["m10a_campaign_loadout_breaks_row_queue",
      "m15_wounded_loses_to_firepower",
      "m11d_strain_below_buffer_and_capped",
      "m19_band_switch_stall",
      "m19_band_switch_eliminate",
      "m20_reach_not_a_bonus"]
ICE = ["threat_basics_armored_onslaught", "threat_basics_hutt_mercenaries",
       "threat_basics_imperial_black_ops", "threat_basics_inspiring_leadership",
       "threat_basics_military_might", "threat_basics_nemeses"]
ICE_MORE = None  # resolved at runtime: every threat_basics_* the record differs on

def dump(v):
    # fixture style: json.dumps(indent=1) shifted +3 spaces (the value lives
    # at nesting level 3 in the case object) - byte-identical to the file's
    # own dumper, proven per-case by the style-proof assert
    s = json.dumps(v, indent=1, ensure_ascii=False)
    return "\n".join([s.split("\n")[0]] + ["   " + ln for ln in s.split("\n")[1:]])

def merge(fname, names):
    path = F + fname
    raw = open(path, encoding="utf-8").read()
    rec = json.load(open(F + fname.replace(".json", "_record.json"), encoding="utf-8"))
    n = 0
    for name in names:
        i = raw.index('"%s"' % name)
        j = raw.index('"expect": ', i)
        # value extent: balanced bracket scan from the opening token
        k = j + len('"expect": ')
        open_ch = raw[k]
        close_ch = ']' if open_ch == '[' else '}'
        depth = 0; p = k
        while True:
            if raw[p] == open_ch: depth += 1
            elif raw[p] == close_ch:
                depth -= 1
                if depth == 0: break
            p += 1
        old_text = raw[j:p+1]
        old_val = json.loads(old_text[len('"expect": '):])
        new_val = rec[name]
        # adjudication: the merge must change SOMETHING (drifted case)
        assert old_val != new_val, (fname, name, "no drift in record?")
        # style proof: the untouched old value rebuilt the same way must be
        # the exact file slice (guarantees byte format for the new one)
        rebuilt = '"expect": ' + dump(old_val)
        assert rebuilt == old_text, (fname, name, "format style mismatch")
        raw = raw[:j] + '"expect": ' + dump(new_val) + raw[p+1:]
        n += 1
    open(path, "w", encoding="utf-8").write(raw)
    json.load(open(path, encoding="utf-8"))  # validity
    print(fname, "->", n, "expect values merged")

if __name__ == "__main__":
    # ICE: the drifted cases are exactly those whose record differs from the
    # current expect (the harness truncates detail lines; the record is truth).
    doc = json.load(open(F + "imperial_card_effects.json", encoding="utf-8"))
    rec = json.load(open(F + "imperial_card_effects_record.json", encoding="utf-8"))
    diff = [c["name"] for c in doc["cases"]
            if c["name"] in rec and c.get("expect") != rec[c["name"]]]
    print("ICE drifted:", sorted(diff))
    docof = json.load(open(F + "overlord_focus.json", encoding="utf-8"))
    recof = json.load(open(F + "overlord_focus_record.json", encoding="utf-8"))
    diffof = [c["name"] for c in docof["cases"]
              if c["name"] in recof and c.get("expect") != recof[c["name"]]]
    print("OF drifted:", sorted(diffof))
    if "--apply" not in sys.argv:
        sys.exit(0)
    assert set(diffof) == set(OF), diffof          # the six reviewed rank flips
    # adjudication gate: only Saska print-accuracy cases may drift here
    assert all(n.startswith("threat_basics_") or
               n in ("threat_mixed_reactive", "threat_mixed_pierce",
                     "threat_precision_pierce") for n in diff), diff
    # (Saska's printed weapon accuracy '+1 Accuracy' is the sole cause in all
    #  ICE cases - base 1/6 -> 1/2 vs White, pierce case rides along 2/3->4/3)
    merge("overlord_focus.json", OF)
    merge("imperial_card_effects.json", sorted(diff))
