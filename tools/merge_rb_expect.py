#!/usr/bin/env python3
# R-B surgical canon update: replace ONLY the six drifted "canon": "..."
# line values (whole-value, unique via the embedded case name). No reformat.
import json

F = "ImperialCommander2/Assets/Tests/Fixtures/tacai/"

drift = {"world_feed.json": ["p19_window_terminal_credit",
                              "p19_window_stays_open_late_closure",
                              "p19_window_strike_credit",
                              "ra_shot_credit",
                              "ra_shot_window_switch_overwrite"],
         "door_feed.json": ["ra_door_interleaved_with_shot"]}

for fix, names in drift.items():
    with open(F + fix, encoding="utf-8") as fh:
        text = fh.read()
    doc = json.loads(text)
    rec = json.load(open(F + fix.replace(".json", "_record.json"), encoding="utf-8"))
    cases = {c["name"]: c for c in doc["cases"]}
    for n in names:
        old_val = json.dumps(cases[n]["expect"]["canon"])       # escaped JSON literal
        new_val = json.dumps(rec[n]["canon"])
        assert old_val != new_val
        needle = '"canon": ' + old_val
        assert text.count(needle) == 1, f"{fix}/{n}: canon literal not unique ({text.count(needle)})"
        text = text.replace(needle, '"canon": ' + new_val)
    with open(F + fix, "w", encoding="utf-8") as fh:
        fh.write(text)
    print(fix, "->", len(names), "canon values replaced in place")
