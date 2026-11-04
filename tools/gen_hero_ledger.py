#!/usr/bin/env python3
# R-B hero_ledger.json generator: hand-authored EVENTS (expect blocks are
# produced once via --record and reviewed - record discipline like door_feed).
import json, collections

def ms(obj="Defeat the rebel cell.", pt="", rl=-1):
    return {"kind": "MissionStart", "round": 1, "payload": {
        "objective": obj, "priorityTarget": pt, "roundLimit": rl,
        "mission": "rb", "missionType": "Story"}}

def ev(kind, rnd, **payload):
    return {"kind": kind, "round": rnd, "payload": payload}

cases = []

cases.append(collections.OrderedDict([
    ("name", "rb_hero_at_objective"),
    ("comment", "Acceptance (a): ObjectiveChange inside Lokaku's OPEN window -> "
                "zone obj:<objective text>, kind fact (public M10b/M6s-B6 "
                "attribution places the actor there). Anchor survives the "
                "window's close - it is history, not a live-position claim."),
    ("events", [ms("Open the door to the command center."),
                ev("HeroActivationStart", 2, hero="Lokaku"),
                ev("ObjectiveChange", 2, objective="Secure the canteen."),
                ev("HeroActivated", 2, hero="Lokaku")]),
]))

cases.append(collections.OrderedDict([
    ("name", "rb_hero_at_door"),
    ("comment", "Acceptance (b): DoorOpen inside the open window -> zone "
                "door:<GUID>, kind fact. A flip OUTSIDE any window (seq5) "
                "anchors nobody - absence writes nothing."),
    ("events", [ms(),
                ev("HeroActivationStart", 2, hero="Han"),
                ev("DoorOpen", 2, door="d-hangar-west", name="Hangar West"),
                ev("HeroActivated", 2, hero="Han"),
                ev("DoorClosed", 3, door="d-hangar-west", name="Hangar West")]),
]))

cases.append(collections.OrderedDict([
    ("name", "rb_shot_is_hypothesis"),
    ("comment", "Acceptance (c): GroupDamage inside the open window anchors "
                "the shooter at at:<group> with kind HYPOTHESIS - a hit "
                "proves weapon-range proximity, never a placed position. "
                "The kind rides in canon AND ledger_text; the objective "
                "change AFTER the window closes leaves the anchor untouched "
                "(the legacy retro channel is never mirrored into the ledger)."),
    ("events", [ms(),
                ev("Spawn", 1, group="STORM", size=2, name="Stormtroopers"),
                ev("HeroActivationStart", 2, hero="Lokaku"),
                ev("GroupDamage", 2, group="STORM", figure=0, wheel=1, health=4),
                ev("HeroActivated", 2, hero="Lokaku"),
                ev("ObjectiveChange", 3, objective="Secure the canteen.")]),
]))

cases.append(collections.OrderedDict([
    ("name", "rb_spawn_in_window"),
    ("comment", "Acceptance (d1): Spawn falls inside the open window -> "
                "SpawnedInHeroWindow (the intruder was pulled in by hero "
                "activity); the G row grows |in_window ONLY there."),
    ("events", [ms(),
                ev("HeroActivationStart", 2, hero="Han"),
                ev("Spawn", 2, group="GIVAN", size=1, name="Grand Inquisitor"),
                ev("HeroActivated", 2, hero="Han")]),
]))

cases.append(collections.OrderedDict([
    ("name", "rb_spawn_out_window"),
    ("comment", "Acceptance (d2): every Spawn here is outside any window "
                "(baseline spawn, then a spawn between windows after the "
                "close) -> no marker, G row byte-identical to pre-R-B."),
    ("events", [ms(),
                ev("Spawn", 1, group="STORM", size=2, name="Stormtroopers"),
                ev("HeroActivationStart", 2, hero="Han"),
                ev("HeroActivated", 2, hero="Han"),
                ev("Spawn", 3, group="BOSSK", size=1, name="Boba Fett")]),
]))

cases.append(collections.OrderedDict([
    ("name", "rb_last_window_wins"),
    ("comment", "Acceptance (e): two anchoring events for the SAME hero in "
                "two windows - the LATER seq wins: fact obj:Secure the "
                "canteen. (seq3) is overwritten by hypothesis at:STORM "
                "(seq5); the shot in window 2 also credits the group row "
                "(R-A column stays consistent with the ledger)."),
    ("events", [ms(),
                ev("Spawn", 1, group="STORM", size=2, name="Stormtroopers"),
                ev("HeroActivationStart", 2, hero="Lokaku"),
                ev("ObjectiveChange", 2, objective="Secure the canteen."),
                ev("HeroActivated", 2, hero="Lokaku"),
                ev("HeroActivationStart", 3, hero="Lokaku"),
                ev("GroupDamage", 3, group="STORM", figure=0, wheel=1, health=4),
                ev("HeroActivated", 3, hero="Lokaku")]),
]))

cases.append(collections.OrderedDict([
    ("name", "rb_two_windows_two_heroes"),
    ("comment", "Identity separation: Lokaku's window shot anchors LOKAKU "
                "(hypothesis at:STORM); Han's later objective change anchors "
                "HAN (fact obj:...) and leaves Lokaku's anchor untouched. "
                "'latest wins' is per hero, never global."),
    ("events", [ms("Defeat the rebel cell.", pt=""),
                ev("Spawn", 1, group="STORM", size=2, name="Stormtroopers"),
                ev("HeroActivationStart", 2, hero="Lokaku"),
                ev("GroupDamage", 2, group="STORM", figure=0, wheel=1, health=4),
                ev("HeroActivated", 2, hero="Lokaku"),
                ev("HeroActivationStart", 3, hero="Han"),
                ev("ObjectiveChange", 3, objective="Escape to the transports."),
                ev("HeroActivated", 3, hero="Han")]),
]))

doc = collections.OrderedDict([
    ("_comment",
     "R-B Last-Seen-Ledger vectors (concept v1.2 §5 B): SYMBOLIC presence "
     "anchors on the REAL WorldEventFeed.Append path. Zones are event-anchor "
     "identities (obj:<text> / door:<GUID> / at:<group>) - NO coordinates, "
     "NO distances, NO movement commitment. KIND is first-class: fact = the "
     "public M10b attribution law places the actor there (objective, door "
     "flip inside an open window); hypothesis = a hit inside the window "
     "proves weapon-range proximity only - consumers must never render it "
     "like a fact. Latest anchoring event wins PER HERO; nothing is ever "
     "written from absence. expect blocks recorded once via "
     "`dotnet run -- <fixtures> --record` and reviewed; the law checks live "
     "in SectionsHeroLedger.cs as hand-pinned ground truth."),
    ("cases", cases),
])

path = "ImperialCommander2/Assets/Tests/Fixtures/tacai/hero_ledger.json"
with open(path, "w", encoding="utf-8") as fh:
    json.dump(doc, fh, indent=2, ensure_ascii=False)
    fh.write("\n")
print("wrote", path, "cases:", len(cases))
