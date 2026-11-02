#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""M6a v2.1: canonical Persona-v2 draft generator + validator (NO runtime asset edit).

Generates tools/draft_personas_v2.json for the six personas exactly per
CONCEPT_personas_v2.md v2.1 section 2 (the tables are binding), and validates
it against the SHIPPED data pools. tactical_personas.json (runtime asset) is
NOT touched here -- that is M6b's job; this draft is its single source.

v2.1 law (Beschluesse 10-12, Geschenke-Exit):
  Four legal channels only: pattern_weight, guard_split, signaturText
  (all shipped since M4) and elite_swap (new; engine seam lands in M6b).
  free_deployment and cost_reduction are RETIRED: every occurrence in a
  draft/asset is a validator error (rule R1). Delta-0 law (Beschluss 11):
  no persona may ever put more figures on the board than baseline -- the
  elite_swap is a count- and budget-NEUTRAL candidate transformation
  (Sec.1.1): regular card -> printed elite counterpart of the SAME name,
  priced at the regular cost. Static guards, each mutation-tested:
    R1  retired gift kind present                 (Rot)
    R2  swap filter must match >=1 Core regular that HAS a Core elite
        counterpart (verified pool: Sturmtruppe DG001/DG002->DG003,
        Imperial Officer DG004/DG005->DG006, Probe Droid DG011/DG012->DG013,
        E-Web Engineer DG007->DG008; AT-ST/AT-DP/Sentry/Death Trooper and
        every non-Core name are forbidden swap targets)
    R3  name cap: sum of elite_swap amounts per persona per figure name
        must be <= 1 (RRG S.12 "ELITE FIGURE" + Beschluss 12: never two
        elites of one name on the board)
    R7  Delta-0 static: total elite_swap amount per persona per rank must
        stay <= 2 (drift guard; every shipped rank uses 1)

Schema law (why the draft is FLAT, not nested under "filter"):
  Personas.cs Load() reads effect-level "internalNameMatch"/"factionMatch"/
  "amount" directly (S(ed,...)); the shipped asset nests its filters under a
  "filter" object and uses "count" instead of "amount" -- neither the C#
  loader nor the twin wiki_data/tools/tacai_persona_twin.py unwraps that, so
  EVERY shipped filter currently matches everything at runtime (M6a finding,
  Sec.1.2-1). M6b fixes the loader or standardises flat -- in both cases the
  draft here stays FLAT and may not be copied in nested style. The loader
  bug is deliberately NOT fixed in this hop.

Usage:  python3 tools/gen_personas_v2.py [--selftest]
Exit 0 only when the draft validates GREEN (and the negative self-tests prove
the validator can actually go red on every v2.1 rule). ASCII-only output,
deterministic order.
"""
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path

HERE = Path(__file__).resolve().parent            # tools/
REPO = HERE.parent                               # repo root
DRAFT_PATH = HERE / "draft_personas_v2.json"

CARDATA = REPO / "ImperialCommander2" / "Assets" / "Resources" / "CardData"
LANG_EN = REPO / "ImperialCommander2" / "Assets" / "Resources" / "Languages" / "En"

ENEMIES = CARDATA / "enemies.json"
VILLAINS = CARDATA / "villains.json"
DECKS = LANG_EN / "tactical_imperial_decks.json"
AGENDAS = LANG_EN / "tactical_agenda_cards.json"

CONCEPT = "CONCEPT_personas_v2.md v2.1 (Abschnitt 2 + Beschluesse 10-12, Thomas 02.11.)"

# ---------------------------------------------------------------- loading

def load_lenient(path):
    """Unity-style JSON: tolerate trailing commas (MiniJson flavour)."""
    text = path.read_text(encoding="utf-8")
    return json.loads(re.sub(r",(\s*[}\]])", r"\1", text))


def base_name(name):
    """'Stormtrooper (Elite)' -> 'Stormtrooper' (the RRG name identity)."""
    return re.sub(r"\s*\(Elite\)$", "", name)


def load_pools():
    enemies = load_lenient(ENEMIES)
    villains = load_lenient(VILLAINS)
    decks_doc = load_lenient(DECKS)
    agenda_doc = load_lenient(AGENDAS)
    deck_card_ids = set()
    deck_ids = set()
    for deck in decks_doc["decks"]:
        deck_ids.add(deck["id"])
        for card in deck["cards"]:
            deck_card_ids.add(card["id"])
    agenda_card_ids = {c["id"] for c in agenda_doc["cards"]}
    agenda_set_ids = {s["id"] for s in agenda_doc["sets"]}
    pool = []  # merged enemies+villains figures, same field shape
    for fig in enemies + villains:
        pool.append({
            "id": fig["id"],
            "name": fig["name"],
            "cost": int(fig["cost"]),
            "expansion": fig.get("expansion", ""),
            "faction": fig.get("faction", ""),
            "traits": sorted(set(fig.get("groupTraits") or [])),
            "is_villain": fig in villains,
        })
    # Core regular -> Core elite counterpart bases (swap-legal target set).
    core_regular = {base_name(f["name"]) for f in pool
                    if f["expansion"] == "Core" and "(Elite)" not in f["name"]}
    core_elite = {base_name(f["name"]) for f in pool
                  if f["expansion"] == "Core" and f["name"].endswith("(Elite)")}
    return {
        "pool": pool,
        "enemies": enemies,
        "villain_ids": {f["id"] for f in pool if f["is_villain"]},
        "villain_names": {f["name"] for f in pool if f["is_villain"]},
        "core_pair_bases": core_regular & core_elite,
        "deck_card_ids": deck_card_ids,
        "deck_ids": deck_ids,
        "agenda_card_ids": agenda_card_ids,
        "agenda_set_ids": agenda_set_ids,
    }

# ---------------------------------------------------------------- vocab

# The four v2.1 channels (Sec.1): the three shipped ones + the new elite_swap.
CHANNELS = {"pattern_weight", "guard_split", "signaturText", "elite_swap"}
# Beschluss 10 (Geschenke-Exit): retired gift kinds -- ANY occurrence is a
# build error (validator RED), also inside future drafts and the M6b asset.
RETIRED_KINDS = {"free_deployment", "cost_reduction"}
# Delta-0 drift guard ceiling per persona per rank (every shipped rank uses 1).
RANK_SWAP_CEILING = 2
# engine stance vocabulary: Doctrine.StanceText (aggressive/guard/blocker/hunter/support/ambusher)
PATTERNS = {"aggressive", "guard", "blocker", "hunter", "support", "ambusher"}

# Section 4 taboo word list (word-boundary, case-insensitive, EN+DE forms).
TABOO = [
    "dice", "surge", "pierce", "accuracy", "reroll",
    "wound", "wounds", "fatigue", "strain", "statusphase",
    "hidden", "insight", "gambit", "vigilance", "speed", "mobile",
    "hand", "hands", "buy", "xp", "credit", "credits",
    "kredit", "kredite", "kauf", "kaufen",
    "wuerfel", "wuerfeln", "wuerfel", "wuerfeln",
    "wunde", "wunden", "erschoepfung", "erschöpfung", "versteckt",
]
# Sec.4 essence allowlist v2.1: essence effects may only ride these kinds --
# elite_swap JOINED the list (Beschluss 10-12 Korrekturlauf).
ESSENCE_ONLY_KINDS = {"pattern_weight", "guard_split", "signaturText", "elite_swap"}
TABOO_RE = re.compile(r"\b(" + "|".join(sorted(set(TABOO))) + r")\b",
                      re.IGNORECASE)

# ---------------------------------------------------------------- canon draft data
# EXACTLY the CONCEPT_personas_v2.md v2.1 section 2 tables. Field names follow
# the FLAT loader schema (internalNameMatch/factionMatch/amount). Every row
# keeps its concept reference in "concept". "source_ids" = provenance card/set/
# deck ids; "source_note" = the human-readable derivation line the validator
# lints for taboo terms; "elite_card" = the printed elite DG id the swap turns
# to (dev documentation, derived from the shipped pool - not engine data).

def _eff(**kw):
    return kw

def canon_personas(heavy, droid_eweb, villain_names_rx, merc_names_rx):
    storm = r"^Stormtrooper$"
    officer = r"^Imperial Officer$"
    probe = r"^Probe Droid$"
    eweb = r"^E-Web Engineer$"
    return [
        OrderedDict([
            ("id", "trooper_captain"),
            ("nameKey", "personaCaptTrooper"),
            ("mottoKey", "personaCaptTrooperMotto"),
            ("flavor", "Masse, die rollt. Das Sturmtruppen-Class-Deck als Persona: Veteranen ersetzen die Billig-Masse, vorwaerts, die Reserve haelt."),
            ("concept", "P1 v2.1 (Bestand, auf swap umgebaut; Deck military_might + precision_training; Agendas stormtrooper_support, imperial_discipline u.a.)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=storm,
                         elite_card="DG003", essence=True,
                         concept="Sec.2 P1 R1 v2.1 (elite_swap Sturmtruppe x1, DG003 zum Regular-Preis)",
                         source_ids=["military_might__endless_ranks"],
                         source_note="endless_ranks-Essenz: 'Raenge ohne Ende' = Veteranen statt Billig-Masse - eine Sturmtruppe wird zur Elite (DG003), die Differenz zahlt die Persona."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["aggressive"],
                         internalNameMatch="stormtrooper", essence=True,
                         concept="Sec.2 P1 R2 v2.1 (pattern_weight Sturmtruppe aggressive x1.2)",
                         source_ids=["military_might__combat_veterans", "military_might__shock_troopers",
                                     "imperial_discipline__fire_at_will"],
                         source_note="Essenz: die Angriffsboni-Karten des Decks wirken als Muster-Druck, nicht als Manipulation der Zufallsmechanik."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="guard_split", guardPercent=25, internalNameMatch="stormtrooper",
                         concept="Sec.2 P1 R3 v2.1 (guard_split 25,_scope Sturmtruppe)",
                         source_ids=["military_might__show_of_force", "military_might__sustained_fire"],
                         source_note="show_of_force + sustained_fire: Druck-Essenz - die Wache-Band der Truppen wird 75/25 zugunsten Angriff."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="guard_split", guardPercent=40, internalNameMatch="stormtrooper",
                         concept="Sec.2 P1 R4 v2.1 (guard_split 40, scope Sturmtruppe)",
                         source_ids=["military_might__show_of_force", "military_might__sustained_fire"],
                         source_note="Eskalation der Wache-Band-Reihe auf 60/40 - dieselbe Deck-Familie wie R3."),
                    _eff(kind="signaturText", key="personaCaptSignature",
                         concept="Sec.2 P1 R4 v2.1 (signaturText 'Raenge ohne Ende')",
                         source_ids=["military_might__endless_ranks"],
                         source_note="Deck-Motto endless_ranks als Signaturtext (ui.json-Key, reine Anzeige)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "feldherr"),
            ("nameKey", "personaCaptFeldherr"),
            ("mottoKey", "personaCaptFeldherrMotto"),
            ("flavor", "Er reitet voran. Das Anfuehrungs-Deck laeuft in vorderster Linie - Offiziere ruecken vor, der Elite-Kommandeur kommt zum Regular-Preis, alle Figuren folgen dem Vorstoss."),
            ("concept", "P2 v2.1 (NEU; inspiring_leadership - reichster Pool mit 6 Verhaltenskarten; Agendas field_commander, the_art_of_war, pursuit_of_the_enemy)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=officer,
                         elite_card="DG006", essence=True,
                         concept="Sec.2 P2 R1 v2.1 (elite_swap Imperial Officer x1, DG006 = Executive-Order-Baum)",
                         source_ids=["inspiring_leadership__supervisory_agent", "inspiring_leadership__field_officer"],
                         source_note="supervisory_agent + field_officer-Essenz: der vormalige 'gratis Offizier' wird der Elite-Kommandeur zum Regular-Preis - DG006 traegt den Executive-Order-Baum, sofort sichtbar."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.25, patterns=["aggressive"],
                         internalNameMatch=officer, essence=True,
                         concept="Sec.2 P2 R2 v2.1 (pattern_weight Offizier aggressive x1.25)",
                         source_ids=["inspiring_leadership__field_officer", "inspiring_leadership__noble_sacrifice"],
                         source_note="field_officer + noble_sacrifice-Essenz: die Fuehrungspraesenz zieht die Angriffsmuster an."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.15, patterns=["aggressive"],
                         factionMatch="Imperial",
                         concept="Sec.2 P2 R3 v2.1 (pattern_weight alle Figuren aggressive x1.15; Beschluss 4)",
                         source_ids=["inspiring_leadership__lead_by_example"],
                         source_note="lead_by_example ist Verhaltens-Anker (tabu-frei): breite Wirkung ueber alle Figuren = Beschluss 4."),
                    _eff(kind="guard_split", guardPercent=20, factionMatch="Imperial",
                         concept="Sec.2 P2 R3 v2.1 (guard_split 20; gewichtet alle Figuren)",
                         source_ids=["inspiring_leadership__lead_by_example"],
                         source_note="Vorwaerts-Identitaet ueber die ganze Garnison: Wache-Anteil 20. Playtest-Korridor 15-25 (Sec.6)."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="guard_split", guardPercent=30, factionMatch="Imperial",
                         concept="Sec.2 P2 R4 v2.1 (guard_split 30)",
                         source_ids=["inspiring_leadership__field_general"],
                         source_note="field_general: die Reserve wird groesser, die Linie bleibt vorn."),
                    _eff(kind="signaturText", key="personaFeldherrSignature",
                         concept="Sec.2 P2 R4 v2.1 (signaturText)",
                         source_ids=["inspiring_leadership__field_general", "the_art_of_war"],
                         source_note="field_general (Executive Order) + the_art_of_war als Signaturtext (ui.json-Key, reine Anzeige; Schluessel in M6c)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "isb_commander"),
            ("nameKey", "personaCaptIsb"),
            ("mottoKey", "personaCaptIsbMotto"),
            ("flavor", "Der Schatten vor dem Schuss. Tarn-Infrastruktur, Zermuerbung, Hinterhalt-Doktrin - ISB-Agenden als schmales, scharfes Profil."),
            ("concept", "P3 v2.1 (Bestand, auf swap umgebaut; imperial_black_ops + subversive_tactics + ISB-/Infiltrations-Agendas; Ziel-ID in M6c: isb_commandant)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=probe,
                         elite_card="DG013", essence=True,
                         concept="Sec.2 P3 R1 v2.1 (elite_swap Probe Droid x1, DG013)",
                         source_ids=["imperial_black_ops__in_the_shadows"],
                         source_note="in_the_shadows-Essenz: Ueberwachung bester Guete - der Vorpost-Droide kommt als Elite-Modell (DG013) zum Regular-Preis."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.35, patterns=["ambusher"],
                         factionMatch="Imperial", essence=True,
                         concept="Sec.2 P3 R2 v2.1 (pattern_weight alle ambusher x1.35; Hidden->Hinterhalt-Essenz)",
                         source_ids=["imperial_black_ops__in_the_shadows", "imperial_black_ops__shadow_corps",
                                     "imperial_black_ops__stealth", "imperial_black_ops__surprise_attack"],
                         source_note="Hidden-Essenz: die Tarn-Karten wirken als Hinterhalt-Doktrin, keine Geheimnis-Verwaltung."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="guard_split", guardPercent=30, factionMatch="Imperial",
                         concept="Sec.2 P3 R3 v2.1 (guard_split 30)",
                         source_ids=["security_protocols"],
                         source_note="security_protocols (Die Apparat-Hueterin): die Apparate halten Gebiete - Wache-Anteil 30."),
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["blocker"],
                         internalNameMatch=droid_eweb,
                         concept="Sec.2 P3 R3 v2.1 (pattern_weight Droid blocker x1.2)",
                         source_ids=["security_protocols"],
                         source_note="security_protocols: die Droiden-Apparate blocken (Filter: aus groupTraits Droid + E-Web abgeleitete Namens-Enumeration, siehe _meta.derivations)."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.25, patterns=["aggressive"],
                         factionMatch="Imperial", essence=True,
                         concept="Sec.2 P3 R4 v2.1 (pattern_weight alle aggressive x1.25)",
                         source_ids=["subversive_tactics__prey_upon_doubt", "subversive_tactics__surgical_strike",
                                     "imperial_black_ops__execution_squad"],
                         source_note="Zermuerbungs-Essenz: Hinrichtungskommando und Schockangriffe wirken als Muster-Druck."),
                    _eff(kind="signaturText", key="personaIsbSignature",
                         concept="Sec.2 P3 R4 v2.1 (signaturText)",
                         source_ids=["imperial_security_bureau__internal_affairs"],
                         source_note="internal_affairs als Signaturtext der Behoerde (ui.json-Key, reine Anzeige; Schluessel in M6c)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "hutt_lord"),
            ("nameKey", "personaCaptHutt"),
            ("mottoKey", "personaCaptHuttMotto"),
            ("flavor", "Kopfgeld ist Geschaeft. Die Jagddoktrin liegt ueber allem, was Beute wittert - der Hutt eskaliert ueber Verhalten, nicht ueber Figuren."),
            ("concept", "P4 v2.1 (Bestand, rein verhaltens-getragen; nemeses + hutt_mercenaries + contract_gunmen u.a.; kein swap-Ziel: Soeldner-/Boesewicht-Namen haben kein Core-Elite-Pendant - bewusst ehrlich)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.15, patterns=["hunter"],
                         internalNameMatch=villain_names_rx,
                         concept="Sec.2 P4 R1 v2.1 (pattern_weight Boesewicht hunter x1.15)",
                         source_ids=["nemeses__leave_them_to_me", "nemeses__powerful_foes"],
                         source_note="leave_them_to_me + powerful_foes: die Boesewichte sind die Hauptware - Jagddoktrin auf die Boesewichts-Riege (Filter: Namens-Enumeration villains.json, siehe _meta.derivations)."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.3, patterns=["hunter"],
                         internalNameMatch=merc_names_rx, essence=True,
                         concept="Sec.2 P4 R2 v2.1 (pattern_weight Mercenary hunter x1.3)",
                         source_ids=["hutt_mercenaries__guild_hunters", "hutt_mercenaries__most_wanted",
                                     "nemeses__ringleader"],
                         source_note="Kopfgeldjagd-Essenz: guild_hunters/most_wanted/ringleader wirken als Jagddoktrin (Filter: Nicht-Boesewicht-Soeldner-Namen, damit R1 und R2 nicht auf dieselben Karten stapeln - _meta.adaptations)."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["hunter"],
                         factionMatch="Imperial", essence=True,
                         concept="Sec.2 P4 R3 v2.1 (pattern_weight alle hunter x1.2)",
                         source_ids=["nemeses__prepare_the_ambush", "nemeses__devastating_legion"],
                         source_note="prepare_the_ambush + devastating_legion als Essenz: die ganze Maschine jagt (Filter = imperriale Seite, damit Boesewicht-Karten nicht ueber R1/R2/R3 dreifach ueber die Jagd-Reihen fallen)."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["ambusher"],
                         factionMatch="Imperial", essence=True,
                         concept="Sec.2 P4 R4 v2.1 (pattern_weight alle ambusher x1.2)",
                         source_ids=["ohnaka_gang", "punishing_tactics"],
                         source_note="ohnaka_gang + punishing_tactics als Essenz: Hinterhalt ueber die imperriale Seite (Filter-Adaption 'alle' = factionMatch Imperial, siehe _meta.adaptations)."),
                    _eff(kind="signaturText", key="personaHuttSignature",
                         concept="Sec.2 P4 R4 v2.1 (signaturText)",
                         source_ids=["nemeses__ringleader"],
                         source_note="Signaturtext der Kopfgeld-Dynastie (ui.json-Key, reine Anzeige)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "droiden_direktor"),
            ("nameKey", "personaCaptDroidenDirektor"),
            ("mottoKey", "personaCaptDroidenDirektorMotto"),
            ("flavor", "Sondroid-Pflicht, Stahl gehorcht. Der Direktor der Schlachtserien: Drohnen und schwere Serien ruecken hart vor, die Elite-Modelle ersetzen die Regular zum selben Preis."),
            ("concept", "P5 v2.1 (NEU; technological_superiority + reactive_defenses + armored_onslaught, Technik-Cluster inkl. Panzer Beschluss 1; 88-Z->Probe-Droid-Adaption siehe _meta.adaptations)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=probe,
                         elite_card="DG013",
                         concept="Sec.2 P5 R1 v2.1 (elite_swap Probe Droid x1, DG013 - '88-Z-Qualitaet')",
                         source_ids=["reactive_defenses__active_surveillance"],
                         source_note="active_surveillance druckt 88-Z ('At the start of each mission, deploy 88-Z'); 88-Z existiert nicht im App-Pool - die Vorpost-Droide-Elite (DG013) ist der ehrliche Ersatz, Adaption dokumentiert in _meta.adaptations."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="guard_split", guardPercent=25, internalNameMatch=droid_eweb,
                         concept="Sec.2 P5 R2 v2.1 (guard_split 25, scope DROID|E-Web)",
                         source_ids=["reactive_defenses__mechanical_protocol", "defensive_tactics"],
                         source_note="mechanical_protocol + defensive_tactics: die Serien halten 25 Prozent Wache-Anteil."),
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["blocker"],
                         internalNameMatch=droid_eweb,
                         concept="Sec.2 P5 R2 v2.1 (pattern_weight 'DROID|E-Web' blocker x1.2)",
                         source_ids=["reactive_defenses__mechanical_protocol", "defensive_tactics"],
                         source_note="Die Apparate blocken (Filter-Enumeration aus groupTraits Droid + E-Web Engineer, siehe _meta.derivations)."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.3, patterns=["aggressive"],
                         internalNameMatch=heavy, essence=True,
                         concept="Sec.2 P5 R3 v2.1 (pattern_weight 'DROID|HEAVY WEAPON|VEHICLE' aggressive x1.3)",
                         source_ids=["armored_onslaught__rapid_dominance", "reactive_defenses__overclock",
                                     "armored_onslaught__armor_corps", "defensive_tactics"],
                         source_note="rapid_dominance ist Verhaltens-Anker (tabu-frei); overclock/armor_corps als Zonen-Essenz: schwere Serien ruecken aggressiv nach (Filter: aus groupTraits Droid/HeavyWeapon/Vehicle abgeleitete Namens-Enumeration, siehe _meta.derivations)."),
                    _eff(kind="guard_split", guardPercent=30, internalNameMatch=heavy, essence=True,
                         concept="Sec.2 P5 R3 v2.1 (guard_split 30 schwere Einheiten)",
                         source_ids=["reactive_defenses__mechanical_protocol", "defensive_tactics"],
                         source_note="mechanical_protocol + defensive_tactics als Verteidigungs-Essenz: schwere Einheiten halten 30 Prozent Wache-Anteil."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=eweb,
                         elite_card="DG008",
                         concept="Sec.2 P5 R4 v2.1 (elite_swap E-Web Engineer x1, DG008)",
                         source_ids=["weapons_division", "imperial_industry_imperial_agenda"],
                         source_note="weapons_division + imperial_industry: die Geschuetz-Series laeuft in Elite-Ausfuehrung (DG008) zum Regular-Preis."),
                    _eff(kind="signaturText", key="personaDroidenDirektorSignature",
                         concept="Sec.2 P5 R4 v2.1 (signaturText 'Stahl gehorcht')",
                         source_ids=["weapons_division", "imperial_industry_imperial_agenda"],
                         source_note="'Stahl gehorcht' als Signaturtext (ui.json-Key, reine Anzeige; Schluessel in M6c)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "grossmoff"),
            ("nameKey", "personaCaptGrossmoff"),
            ("mottoKey", "personaCaptGrossmoffMotto"),
            ("flavor", "Angst. Der Grosse Moff haelt Flaeche: Garnisonen bleiben, Veteranen uebernehmen die Posten, und am Ende zieht die Furcht selbst ein."),
            ("concept", "P6 v2.1 (NEU; overwhelming_oppression + power_of_the_dark_side; Agendas inquisition u.a.)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="guard_split", guardPercent=30, factionMatch="Imperial",
                         concept="Sec.2 P6 R1 v2.1 (guard_split 30, Garnison haelt Gebiete)",
                         source_ids=["overwhelming_oppression__commence_landing", "overwhelming_oppression__implacable"],
                         source_note="commence_landing + implacable: Landungsdruck und Unbeugsamkeit als Garnison-Doktrin (Wache-Anteil 30 pro Tabelle)."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["guard"],
                         factionMatch="Imperial",
                         concept="Sec.2 P6 R2 v2.1 (pattern_weight Garnison (guard) x1.2 - siehe _meta.adaptations zur Lesart)",
                         source_ids=["overwhelming_oppression"],
                         source_note="Das Ueberwaeltigungs-Deck als Familien-Anker: die Garnison haelt die Flaeche - Wache-Muster x1.2. Tabellenzeile 'Garnison (guard) aggressive' konservativ als guard-Muster gelesen (R1-Wording 'Garnison haelt Gebiete', v2-Zeile war 'guard alle x1.2')."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=storm,
                         elite_card="DG003",
                         concept="Sec.2 P6 R3 v2.1 (elite_swap Sturmtruppe x1, Veteranen der Okkupation)",
                         source_ids=["overwhelming_oppression__tie_landers", "overwhelming_oppression__redouble_our_efforts"],
                         source_note="tie_landers + redouble_our_efforts: die Landungsboote bringen Veteranen - eine Sturmtruppe wird zur Elite (DG003) zum Regular-Preis."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="elite_swap", amount=1, internalNameMatch=eweb,
                         elite_card="DG008",
                         concept="Sec.2 P6 R4 v2.1 (elite_swap E-Web Engineer x1)",
                         source_ids=["inquisition", "the_emperor_s_plots"],
                         source_note="Inquisition-Set + the_emperor_s_plots: schwere Geschuetze in Elite-Ausfuehrung (DG008) auf den Okkupationsposten. Tabellenquelle the_emperor_s_wrath existiert nicht in den digitalisierten Karten - Adaption siehe _meta.adaptations."),
                    _eff(kind="guard_split", guardPercent=40, factionMatch="Imperial",
                         concept="Sec.2 P6 R4 v2.1 (guard_split 40)",
                         source_ids=["inquisition", "the_emperor_s_plots"],
                         source_note="Die Besatzung verdichtet sich: Wache-Anteil 40 ueber die ganze Garnison."),
                    _eff(kind="signaturText", key="personaGrossmoffSignature",
                         concept="Sec.2 P6 R4 v2.1 (signaturText 'Angst')",
                         source_ids=["inquisition"],
                         source_note="Inquisition-Set-Motto 'Angst' als Signaturtext (ui.json-Key, reine Anzeige; Schluessel in M6c)."),
                ])]),
            ]),
        ]),
    ]

# Derivations computed from the shipped pools (frozen into the draft's _meta
# so the enumeration is auditable, and into the filter strings themselves).

def _assert_escape_free(rx, what):
    # .NET (Personas.cs Regex) treats Python-style \\- as an unknown escape
    # and the loader fail-safes a broken regex to NO match - join the names
    # RAW (they carry no .NET metacharacters beyond the literal hyphen).
    assert "\\" not in rx and not re.search(r"[\[\](){}+*?^$.|]", rx.replace("|", "")), \
        "derived enumeration %s must stay escape-free for the .NET regex loader" % what

def derive_enums(pools):
    src_en = pools["enemies"]
    by_trait = lambda want: sorted({base_name(e["name"]) for e in src_en
                                    if set(e.get("groupTraits") or []) & want})
    heavy = by_trait({"Droid", "HeavyWeapon", "Vehicle"})
    # 'DROID|E-Web': the droid-trait roster plus the E-Web Engineer by name
    # (the table quotes the group keyword E-Web; the shipped E-Web Engineer
    # carries Trooper/HeavyWeapon, not Droid).
    droid_eweb = sorted(set(by_trait({"Droid"})) | {"E-Web Engineer"})
    # P4 split: 'Boesewicht' = the villains.json roster; 'Mercenary' = the
    # Mercenary-faction enemy names WITHOUT the villains (the engine only
    # knows factions Imperial|Mercenary, so the two table rows would else
    # stack multiplicatively on every villain card).
    villain_names = sorted(pools["villain_names"])
    merc = sorted({base_name(f["name"]) for f in pools["pool"]
                   if f["faction"] == "Mercenary" and not f["is_villain"]})
    heavies = {"heavy": heavy, "droid_eweb": droid_eweb,
               "villain": villain_names, "merc": merc}
    for what, names in heavies.items():
        _assert_escape_free("|".join(names), what)
    return (heavy, droid_eweb, villain_names, merc)

# ---------------------------------------------------------------- build

def build_draft(pools):
    heavy, droid_eweb, villain_names, merc = derive_enums(pools)
    draft = OrderedDict()
    meta = OrderedDict()
    meta["file"] = "draft_personas_v2.json"
    meta["schema"] = "tacai.personas_v2_draft/2"
    meta["status"] = ("ENTWURF M6a v2.1 - kanonischer Entwurf fuer M6b; KEIN Runtime-Asset, "
                      "tactical_personas.json wird erst in M6b aufgehoben.")
    meta["purpose"] = ("Datenbasis der sechs imperialen Personas gemaess "
                       + CONCEPT + "; die Raenge folgen automatisch der Threat-Stufe 2/4/6/8.")
    meta["generated_by"] = "tools/gen_personas_v2.py (M6a v2.1)"
    meta["generated_from"] = OrderedDict([
        ("enemies", str(ENEMIES.relative_to(REPO))),
        ("villains", str(VILLAINS.relative_to(REPO))),
        ("decks", str(DECKS.relative_to(REPO))),
        ("agendas", str(AGENDAS.relative_to(REPO))),
    ])
    meta["schema_law"] = OrderedDict([
        ("flat_filters",
         "internalNameMatch/factionMatch/amount stehen FLACH auf dem Effekt - genau so liest "
         "Personas.cs Load(). Das bestehende Runtime-Asset nestet unter 'filter' und nutzt 'count'; "
         "weder Loader noch tacai_persona_twin.py wickeln das ab, alle bestehenden Filter treffen "
         "daher runtime ALLES (M6a-Befund Sec.1.2-1, Belege in Assets/Tests/Fixtures/tacai/personas.json). "
         "Der Loader-Bug wird NICHT in diesem Hop behoben (M6b); M6b darf das nested-Schema "
         "NICHT kopieren."),
        ("amount", "elite_swap-Anzahl heisst 'amount' (Loader-Lesart), nicht 'count'."),
        ("dev_fields", "elite_card/source_ids/source_note/essence/concept sind Dev-Felder; "
                       "Personas.Load() ignoriert Unbekanntes, M6b kann sie im Asset lassen."),
    ])
    meta["delta_zero_law"] = ("Beschluss 10-12 (Geschenke-Exit + Delta-0 + RRG-Elitenregel): "
                              "free_deployment/cost_reduction sind retirement-ROT (R1); keine Persona "
                              "bringt je eine Figur mehr aufs Brett als baseline. elite_swap ist "
                              "count- und budget-neutral by construction (Sec.1.1): Regular-Karte -> "
                              "gedrucktes Elite-Pendant gleichen Namens zum Regular-Preis, "
                              "Namens-Cap 1/Mission (Statik-Regeln R2/R3/R7 im Validator, jede mit "
                              "Mutationstest).")
    meta["adaptations"] = [
        "Geschenke-Exit (Beschluss 10): der v2-Entwurf nutzte free_deployment/cost_reduction "
        "(Commit 66e4cc5). Beide Kinds sind ersatzlos geflogen und werden vom Validator ROT "
        "geworfen; die Geschenke-Raenge sind gemaess Sec.2-v2.1-Tabelle auf die vier legalen "
        "Raenge umbaut (Delta-0: swap ersetzt eine Figur, bringt nie eine neue).",
        "88-Z -> elite_swap Probe Droid (Sec.2 P5 R1): Die Karte reactive_defenses__"
        "active_surveillance druckt 88-Z ('At the start of each mission, deploy 88-Z'); ein "
        "88-Z gibt es im App-Pool (CardData enemies+villains) nicht - die Probe-Droid-Elite "
        "(DG013) ist der ehrliche Ersatz ('88-Z-Qualitaet'), dokumentierte Adaption statt "
        "erfundener Karte. Der v2-Ersatz BT-1 (DG089, gratis) ist mit den Geschenken geflogen.",
        "armored_battle_group (Quellenzeile Sec.2 P5 R3) existiert nicht in den digitalisierten "
        "Decks (Pruefung 02.11: kein Kartenname mit battle/group in 109 Deck- + 126 Agendakarten). "
        "Ersatz: armored_onslaught__armor_corps ('Armor Corps', Panzer-Verband im selben Deck).",
        "the_emperor_s_wrath (Quellenzeile Sec.2 P6 R4) existiert nicht in den digitalisierten "
        "Inhalten (Pruefung 02.11: kein Deck/Agenda-Set/Agenda-Karte mit 'wrath'; die "
        "Emperor's-Wrath-Erweiterung ist nicht digitalisiert). Ersatz: the_emperor_s_plots "
        "('The Emperor's Plots', naechster echter Kaiser-Agenda-Satz) neben inquisition.",
        "pattern_weight-Filter 'DROID|E-Web', 'DROID|HEAVY WEAPON|VEHICLE', 'Boesewicht' und "
        "'Mercenary': Die Engine matcht nur interne Kartennamen, keine Traits - die Filter sind "
        "daher als Namens-Enumeration aus den shipped groupTraits bzw. villains.json bzw. der "
        "Mercenary-Fraktion abgeleitet (siehe _meta.derivations); Elite-Varianten erben per "
        "Substring-Match.",
        "'alle hunter'/'alle ambusher'/'alle Figuren': die Engine kennt als Fraktion nur Imperial "
        "und Mercenary; 'alle' ist als factionMatch Imperial gesetzt. Bei den Jagd-Reihen des "
        "Hutt (P4 R1 villains-Riege, R2 Soeldner, R3 Imperial) verhindert die Aufteilung "
        "multiplikatives Stapeln derselben Karte ueber mehrere Active-Raenge (PatternFor kettet).",
        "Sec.2 P6 R2 'pattern_weight Garnison (guard) aggressive x1.2': konservativ als "
        "guard-Muster (patterns=[guard]) gelesen - R1 heisst 'Garnison haelt Gebiete', die "
        "v2-Zeile war 'guard alle x1.2', und die Persona-Identitaet (guard_split 30/40) ist "
        "Defensive. Die literal 'aggressive'-Lesart waere ein aggressiver Einschlag in einer "
        "rein defensiven Raenfreihe; abweichende Lesart bitte als Playtest-Korridor in M6f.",
        "Alte->neue Persona-IDs (M6a v2.1 aendert nichts an Save-Tokens): trooper_captain bleibt, "
        "isb_commander bleibt (Konzept-Ziel-ID isb_commandant erst in M6c), hutt_lord bleibt; "
        "neu: feldherr, droiden_direktor, grossmoff.",
    ]
    meta["persona_id_migration"] = OrderedDict([
        ("trooper_captain", "trooper_captain (P1, Geschenke->swap)"),
        ("isb_commander", "isb_commander (P3, Geschenke->swap; Ziel-ID isb_commandant in M6c)"),
        ("hutt_lord", "hutt_lord (P4, rein verhaltens-getragen)"),
        ("(neu)", "feldherr (P2), droiden_direktor (P5), grossmoff (P6)"),
    ])
    meta["m6c_ui_keys_pending"] = [
        "personaCaptFeldherr + personaCaptFeldherrMotto",
        "personaCaptDroidenDirektor + personaCaptDroidenDirektorMotto",
        "personaCaptGrossmoff + personaCaptGrossmoffMotto",
        "NEU signaturText-Keys: personaFeldherrSignature (P2 R4), personaIsbSignature (P3 R4), "
        "personaDroidenDirektorSignature (P5 R4), personaGrossmoffSignature (P6 R4)",
        "bestehende Keys bleiben: personaCaptTrooper/Motto, personaCaptSignature, "
        "personaCaptIsb/Motto, personaCaptHutt/Motto, personaHuttSignature",
    ]
    deriv = OrderedDict()
    deriv["heavy_droid_hw_vehicle_names"] = heavy
    deriv["droid_eweb_names"] = droid_eweb
    deriv["villain_roster_names"] = villain_names
    deriv["mercenary_non_villain_names"] = merc
    deriv["core_swap_pairs"] = sorted(pools["core_pair_bases"])
    meta["derivations"] = deriv
    meta["fairness_law"] = ("Nur die vier v2.1-Kanaele pattern_weight/guard_split/signaturText/"
                            "elite_swap; Geschenke-Kinds sind ROT (Beschluss 10). elite_swap ist "
                            "count- und budget-neutral (Delta-0, Beschluss 11) und respektiert die "
                            "RRG-Elitenregel (Beschluss 12: hochstens eine Elite pro Figurennamen). "
                            "Kein Zufall ausser der geseedeten pattern_weight-Entscheidung. Alles "
                            "oeffentlich sichtbar.")
    draft["_meta"] = meta
    draft["personas"] = canon_personas("|".join(heavy), "|".join(droid_eweb),
                                       "|".join(villain_names), "|".join(merc))
    return draft

# ---------------------------------------------------------------- validator

def _filter_matches(eff, pool):
    pat = eff.get("internalNameMatch")
    fac = eff.get("factionMatch")
    hits = []
    rx = re.compile(pat, re.IGNORECASE) if pat else None
    for f in pool:
        if fac is not None and f["faction"] != fac:
            continue
        if rx is not None and not rx.search(f["name"]):
            continue
        if pat is None and fac is None:
            continue          # unfiltered row would match the WHOLE pool
        hits.append(f)
    return hits

def swap_target_bases(eff, pools):
    """Core-regular base names hit by the filter that HAVE a Core elite
    counterpart (the only swap-legal targets, Sec.1.1-5)."""
    hits = _filter_matches(eff, pools["pool"])
    hit_bases = {base_name(f["name"]) for f in hits}
    return hit_bases & pools["core_pair_bases"], hit_bases

def validate(draft, pools):
    """Returns (errors, checks). Rule codes v2.1:
    R0 structural law | R1 retired gift kind (Geschenke-ROT, Beschluss 10)
    R2 swap targets a Core regular with Core elite counterpart (Sec.1.1-5)
    R3 name cap: <=1 elite_swap per figure name per persona (Beschluss 12)
    R4 taboo/essence allowlist | R5 source ids exist | R6 dead filter
    R7 Delta-0 static: <=2 elite_swap amount per persona per rank."""
    errors = []
    checks = 0

    personas = draft.get("personas", [])
    checks += 1
    if len(personas) != 6:
        errors.append("R0: expected exactly 6 personas, found %d" % len(personas))

    all_source_ids = pools["deck_card_ids"] | pools["agenda_card_ids"] \
        | pools["agenda_set_ids"] | pools["deck_ids"]

    for p in personas:
        pid = p.get("id", "?")
        # persona-level taboo lint (flavor is shipped-adjacent prose)
        checks += 1
        if TABOO_RE.search(p.get("flavor", "")):
            errors.append("R4: persona %s flavor cites a taboo term" % pid)

        ranks = p.get("ranks", [])
        checks += 1
        if [(r["rank"], r["minThreat"]) for r in ranks] != [(1, 2), (2, 4), (3, 6), (4, 8)]:
            errors.append("R0: %s rank ladder must be exactly 1/2/3/4 at minThreat 2/4/6/8" % pid)

        swap_totals = {}                     # base name -> summed amount (R3)
        for r in ranks:
            rno = r["rank"]
            seen_guard = False
            rank_swap_amount = 0             # R7 drift guard accumulator
            for eff in r["effects"]:
                checks += 1
                kind = eff.get("kind", "")
                loc = "%s R%d [%s]" % (pid, rno, kind)
                retired = kind in RETIRED_KINDS
                if retired:
                    errors.append("R1: %s: retired gift kind - free_deployment/cost_reduction "
                                  "flew out without replacement (Geschenke-Exit, Beschluss 10; "
                                  "Delta-0, Beschluss 11)" % loc)
                elif kind not in CHANNELS:
                    errors.append("R0: %s: kind outside the four v2.1 channels %s"
                                  % (loc, sorted(CHANNELS)))
                    continue

                if kind == "elite_swap" and not retired:
                    checks += 1
                    if eff.get("internalNameMatch") is None and eff.get("factionMatch") is None:
                        errors.append("R0: %s: elite_swap without any filter would hit the whole pool" % loc)
                    amt = eff.get("amount", 0)
                    checks += 1
                    if not isinstance(amt, int) or isinstance(amt, bool) or amt < 1:
                        errors.append("R0: %s: amount must be an integer >= 1, found %r" % (loc, amt))
                        amt = 0
                    rank_swap_amount += amt

                if kind == "pattern_weight":
                    checks += 1
                    pats = eff.get("patterns", [])
                    if not pats or not set(pats) <= PATTERNS:
                        errors.append("R0: %s: patterns %r not in engine vocabulary %s"
                                      % (loc, pats, sorted(PATTERNS)))
                    mult = eff.get("multiplier", 1.0)
                    checks += 1
                    if not (1.0 < mult <= 1.5):
                        errors.append("R0: %s: multiplier %s outside (1.0, 1.5] (Beschluss 4 "
                                      "measure 1.15-1.35)" % (loc, mult))
                if kind == "guard_split":
                    checks += 1
                    if seen_guard:
                        errors.append("R0: %s: two guard_split rows in one rank (last-wins ambiguity)" % loc)
                    seen_guard = True
                    gp = eff.get("guardPercent", -1)
                    if not (0 <= gp <= 100):
                        errors.append("R0: %s: guardPercent %s outside 0..100" % (loc, gp))
                if kind == "signaturText":
                    checks += 1
                    if not eff.get("key"):
                        errors.append("R0: %s: missing ui.json key" % loc)

                # -- rule R5: source ids must exist in the shipped assets
                sids = eff.get("source_ids", [])
                checks += 1
                if not sids:
                    errors.append("R5: %s: no source card id" % loc)
                for sid in sids:
                    checks += 1
                    if sid not in all_source_ids:
                        errors.append("R5: %s: source id '%s' exists in neither "
                                      "tactical_imperial_decks.json nor tactical_agenda_cards.json"
                                      % (loc, sid))

                # -- rule R4: taboo term in the effect's own cited text
                note = eff.get("source_note", "")
                hits = TABOO_RE.findall(note)
                checks += 1
                if hits:
                    if not eff.get("essence"):
                        errors.append("R4: %s: source cites taboo term %s without essence:true"
                                      % (loc, sorted(set(h.lower() for h in hits))))
                    elif kind not in ESSENCE_ONLY_KINDS:
                        errors.append("R4: %s: essence effect must be pattern_weight/guard_split/"
                                      "signaturText/elite_swap, found kind %s" % (loc, kind))
                if eff.get("essence") and kind not in ESSENCE_ONLY_KINDS:
                    checks += 1
                    errors.append("R4: %s: essence:true but kind %s is not an essence channel "
                                  "(v2.1 allowlist)" % (loc, kind))

                # -- regex sanity (fail-safe: broken regex matches NOTHING at runtime)
                if eff.get("internalNameMatch") is not None:
                    checks += 1
                    try:
                        re.compile(eff["internalNameMatch"], re.IGNORECASE)
                    except re.error as exc:
                        errors.append("R0: %s: internalNameMatch does not compile: %s" % (loc, exc))
                        continue

                # -- swap legality (R2 targets, R3 name cap) / dead filters (R6)
                if kind == "elite_swap" and not retired:
                    paired, hit_bases = swap_target_bases(eff, pools)
                    checks += 1
                    if not hit_bases:
                        errors.append("R2: %s: filter matches NO shipped figure (dead swap)" % loc)
                    else:
                        checks += 1
                        if not paired:
                            errors.append("R2: %s: swap filter hits only %s - names without a "
                                          "Core regular->Core elite pair are forbidden swap "
                                          "targets (Sec.1.1-5; verified pool: Sturmtruppe DG001/"
                                          "DG002->DG003, Imperial Officer DG004/DG005->DG006, "
                                          "Probe Droid DG011/DG012->DG013, E-Web Engineer "
                                          "DG007->DG008)" % (loc, sorted(hit_bases)))
                        amt = eff.get("amount", 1) if isinstance(eff.get("amount", 1), int) else 0
                        for b in paired:
                            swap_totals[b] = swap_totals.get(b, 0) + amt
                elif retired:
                    pass        # gift kinds: R1 already fired; no further matching law
                elif eff.get("internalNameMatch") is not None:
                    checks += 1
                    if not _filter_matches(eff, pools["pool"]):
                        errors.append("R6: %s: filter matches NO shipped figure (dead filter)" % loc)

            checks += 1
            if rank_swap_amount > RANK_SWAP_CEILING:
                errors.append("R7: %s R%d: elite_swap total %d exceeds ceiling %d per rank "
                              "(Delta-0 drift guard, Beschluss 11 - every shipped rank uses 1)"
                              % (pid, rno, rank_swap_amount, RANK_SWAP_CEILING))

        # -- rule R3: name cap (RRG S.12 ELITE FIGURE + Beschluss 12)
        for base, amt in sorted(swap_totals.items()):
            checks += 1
            if amt > 1:
                errors.append("R3: %s: elite_swap amount %d for figure name '%s' exceeds the "
                              "name cap 1 (RRG S.12/ Beschluss 12: never two elites of one "
                              "name on the board)" % (pid, amt, base))

    return errors, checks

# ---------------------------------------------------------------- self-tests

def selftest(draft, pools):
    """Prove the v2.1 validator goes RED on each rule class and stays GREEN on
    the near-miss counter-cases (mutated COPIES; the draft on disk is never
    touched). Returns list of (name, ok)."""
    import copy

    def mutate(fn):
        d = copy.deepcopy(draft)
        if fn:
            fn(d)
        errs, _ = validate(d, pools)
        return errs

    def find_p(d, pid, rank, kind, idx=0):
        p = next(x for x in d["personas"] if x["id"] == pid)
        r = next(x for x in p["ranks"] if x["rank"] == rank)
        same = [e for e in r["effects"] if e["kind"] == kind]
        return same[idx]

    def rank_of(d, pid, rank):
        p = next(x for x in d["personas"] if x["id"] == pid)
        return next(x for x in p["ranks"] if x["rank"] == rank)

    def counter_swap(internal_match, source_note):
        return OrderedDict([("kind", "elite_swap"), ("amount", 1),
                            ("internalNameMatch", internal_match),
                            ("concept", "selftest counter-swap"),
                            ("source_ids", ["reactive_defenses__active_surveillance"]),
                            ("source_note", source_note)])

    cases = []

    # -- R1 gift kinds -------------------------------------------------------
    def gift_red():
        def fn(draft):
            e = find_p(draft, "feldherr", 3, "guard_split")
            e["kind"] = "free_deployment"
            e["amount"] = 1
        return mutate(fn)
    cases.append(("R1 free_deployment is RED (gifts flew out)",
                  lambda: any("R1:" in x and "free_deployment" in x for x in gift_red())))

    def cost_red():
        def fn(draft):
            e = find_p(draft, "grossmoff", 2, "pattern_weight")
            e["kind"] = "cost_reduction"
            e["amount"] = 1
        return mutate(fn)
    cases.append(("R1 cost_reduction is RED (gifts flew out)",
                  lambda: any("R1:" in x and "cost_reduction" in x for x in cost_red())))

    def gift_green():
        # legal replacement shape: the same row as an elite_swap stays GREEN
        # (green counter-test for the kind law).
        def fn(draft):
            e = find_p(draft, "trooper_captain", 2, "pattern_weight")
            e["kind"] = "elite_swap"
            e["internalNameMatch"] = r"^Probe Droid$"
            e["amount"] = 1
            e.pop("patterns", None)
            e.pop("multiplier", None)
            e["source_note"] = "Gegentest: legaler Kinds-Wechsel auf elite_swap bleibt gruen."
            e["essence"] = False
        return not mutate(fn)
    cases.append(("R1 elite_swap instead of pattern_weight stays GREEN", gift_green))

    # -- R2 swap target set --------------------------------------------------
    for name, pid, rank, match in (
            ("AT-ST", "trooper_captain", 1, r"^AT-ST$"),          # Core, no elite pair
            ("AT-DP", "grossmoff", 3, r"^AT-DP$"),                # exp Empire, no elite pair
            ("Death Trooper", "droiden_direktor", 1, r"^Death Trooper$"),  # Lothal only
    ):
        def factory(pid=pid, rank=rank, match=match):
            def fn(draft):
                find_p(draft, pid, rank, "elite_swap")["internalNameMatch"] = match
            return mutate(fn)
        cases.append(("R2 degenerate swap target %s is RED" % name,
                      (lambda f: lambda: any("R2:" in x for x in f()))(factory)))

    def swap_green():
        def fn(draft):
            find_p(draft, "trooper_captain", 1, "elite_swap")["internalNameMatch"] = r"^E-Web Engineer$"
        return not mutate(fn)
    cases.append(("R2 any other verified Core pair (E-Web) stays GREEN", swap_green))

    # -- R3 name cap ---------------------------------------------------------
    def cap_red():
        def fn(draft):
            find_p(draft, "trooper_captain", 1, "elite_swap")["amount"] = 2
        return mutate(fn)
    def cap_errors():
        errs = cap_red()
        return [x for x in errs if "R3:" in x]
    cases.append(("R3 two swaps onto one figure name is RED",
                  lambda: bool(cap_errors())))

    def cap_green():
        # second swap on the SAME rank, different name: cap stays quiet and
        # the rank total (2) sits exactly on the R7 ceiling -> all green.
        def fn(draft):
            rank_of(draft, "trooper_captain", 1)["effects"].append(
                counter_swap(r"^Probe Droid$", "Gegentest: zweiter Swap gleicher Rang, anderer Name."))
        return not mutate(fn)
    cases.append(("R3 second swap with a different name stays GREEN", cap_green))

    # -- R7 Delta-0 static ceiling --------------------------------------------
    def delta_red():
        def fn(draft):
            r = rank_of(draft, "trooper_captain", 1)
            r["effects"].append(counter_swap(r"^Probe Droid$", "Drift-Test 2."))
            r["effects"].append(counter_swap(r"^Imperial Officer$", "Drift-Test 3."))
        return mutate(fn)
    def delta_errors():
        errs = delta_red()
        return [x for x in errs if "R7:" in x], [x for x in errs if "R3:" in x]
    cases.append(("R7 three swaps (total 3) on one rank is RED, cap stays quiet",
                  lambda: bool(delta_errors()[0]) and not delta_errors()[1]))

    # -- R4 essence allowlist / taboo ------------------------------------------
    def essence_red():
        def fn(draft):
            e = find_p(draft, "feldherr", 1, "elite_swap")
            e["kind"] = "cost_reduction"
            e["amount"] = 1
            e["source_note"] = "Hidden-Essenz zitiert"
        return mutate(fn)
    def essence_errors():
        errs = essence_red()
        return any("R1:" in x for x in errs), any("essence:true but kind cost_reduction" in x
                                                  for x in errs)
    cases.append(("R4 essence:true on a retired gift kind is RED (both rules fire)",
                  lambda: essence_errors()[0] and essence_errors()[1]))

    def essence_swap_green():
        # elite_swap joined the essence allowlist (v2.1): citing the Hidden
        # essence on a swap must now stay clean.
        def fn(draft):
            e = find_p(draft, "feldherr", 1, "elite_swap")
            e["essence"] = True
            e["source_note"] = "Hidden-Essenz als Quelle auf elite_swap - Allowlist v2.1."
        return not any("R4:" in x for x in mutate(fn))
    cases.append(("R4 essence elite_swap with Hidden-citation stays GREEN (new allowlist)",
                  essence_swap_green))

    def taboo_red():
        def fn(draft):
            e = find_p(draft, "feldherr", 3, "pattern_weight")
            e["essence"] = False
            e["source_note"] = "Druck wie im Original: reroll erlaubt"
        return mutate(fn)
    cases.append(("R4 taboo without essence still RED",
                  lambda: any("R4:" in x for x in taboo_red())))

    # -- R5 source ids ---------------------------------------------------------
    def fake_source():
        def fn(draft):
            find_p(draft, "grossmoff", 3, "elite_swap")["source_ids"] = \
                ["overwhelming_oppression__tie_lander"]
        return mutate(fn)
    cases.append(("R5 fake source id still RED",
                  lambda: any("R5:" in x for x in fake_source())))

    # -- R6 dead filter --------------------------------------------------------
    def dead_filter():
        def fn(draft):
            find_p(draft, "isb_commander", 2, "pattern_weight")["internalNameMatch"] = r"^Unicorn$"
        return mutate(fn)
    cases.append(("R6 dead pattern_weight filter still RED",
                  lambda: any("R6:" in x for x in dead_filter())))

    # -- canon green baseline ---------------------------------------------------
    cases.append(("canon draft validates GREEN (0 errors)", lambda: not mutate(None)))

    results = []
    for name, fn in cases:
        try:
            ok = bool(fn())
        except Exception as exc:      # noqa: BLE001 - self-test harness
            ok = False
            name += " (exception: %s)" % exc
        results.append((name, ok))
    return results

# ---------------------------------------------------------------- main

def main():
    selftest_only = "--selftest" in sys.argv[1:]
    pools = load_pools()
    draft = build_draft(pools)

    text = json.dumps(draft, indent=2, ensure_ascii=True) + "\n"
    assert text.isascii(), "draft must stay pure ASCII (asset convention)"
    if not selftest_only:
        DRAFT_PATH.write_text(text, encoding="utf-8")

    # validate the WRITTEN file (not the in-memory object) - the file is the pin
    on_disk = json.loads(DRAFT_PATH.read_text(encoding="utf-8"))
    errors, checks = validate(on_disk, pools)

    n_personas = len(on_disk["personas"])
    n_ranks = sum(len(p["ranks"]) for p in on_disk["personas"])
    n_effects = sum(len(r["effects"]) for p in on_disk["personas"] for r in p["ranks"])
    n_swaps = sum(1 for p in on_disk["personas"] for r in p["ranks"]
                  for e in r["effects"] if e.get("kind") == "elite_swap")

    print("draft   : %s" % DRAFT_PATH.relative_to(REPO))
    print("personas: %d  ranges/ranks: %d  effects: %d  elite_swaps: %d"
          % (n_personas, n_ranks, n_effects, n_swaps))
    print("validator checks: %d  errors: %d" % (checks, len(errors)))
    for e in errors:
        print("  ERROR " + e)
    status = "GRUEN" if not errors else "ROT"
    print("validator: %s" % status)

    st = selftest(on_disk, pools)
    print("self-tests (v2.1 rules must be able to go red):")
    st_fail = 0
    for name, ok in st:
        print("  %s %s" % ("ok  " if ok else "FAIL", name))
        if not ok:
            st_fail += 1

    if errors or st_fail or n_personas != 6 or n_ranks != 24:
        sys.exit(1)

if __name__ == "__main__":
    main()
