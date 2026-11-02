#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""M6a: canonical Persona-v2 draft generator + validator (NO runtime asset edit).

Generates tools/draft_personas_v2.json for the six personas exactly per
CONCEPT_personas_v2.md section 2 (the tables are binding), and validates it
against the SHIPPED data pools. tactical_personas.json (runtime asset) is
NOT touched here -- that is M6b's job; this draft is its single source.

Schema law (why the draft is FLAT, not nested under "filter"):
  Personas.cs Load() reads effect-level "internalNameMatch"/"factionMatch"/
  "amount" directly (S(ed,...)); the shipped asset nests its filters under a
  "filter" object and uses "count" instead of "amount" -- neither the C#
  loader nor the twin wiki_data/tools/tacai_persona_twin.py unwraps that, so
  EVERY shipped filter currently matches everything at runtime. Evidence in
  the pinned harness fixture (Assets/Tests/Fixtures/tacai/personas.json):
  trooper_captain threat 2 discounts "Imperial Officer" 2->1 (stormtrooper
  filter ignored), hutt_lord threat 8 flips "Stormtrooper" to hunter
  (Mercenary filter ignored). M6b MUST author the runtime asset in the FLAT
  loader schema used by this draft; copying the nested style keeps the bug.

Usage:  python3 tools/gen_personas_v2.py [--selftest]
Exit 0 only when the draft validates GREEN (and the negative self-tests prove
the validator can actually go red). ASCII-only output, deterministic order.
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

CONCEPT = "CONCEPT_personas_v2.md (Abschnitt 2, Thomas' Beschluesse 02.11.)"

# ---------------------------------------------------------------- loading

def load_lenient(path):
    """Unity-style JSON: tolerate trailing commas (MiniJson flavour)."""
    text = path.read_text(encoding="utf-8")
    return json.loads(re.sub(r",(\s*[}\]])", r"\1", text))


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
            "is_villain": fig in villains,
        })
    return {
        "pool": pool,
        "villain_ids": {f["id"] for f in pool if f["is_villain"]},
        "villain_names": {f["name"] for f in pool if f["is_villain"]},
        "deck_card_ids": deck_card_ids,
        "deck_ids": deck_ids,
        "agenda_card_ids": agenda_card_ids,
        "agenda_set_ids": agenda_set_ids,
    }

# ---------------------------------------------------------------- vocab

CHANNELS = {"cost_reduction", "free_deployment", "pattern_weight",
            "guard_split", "signaturText"}          # fairness boundary, no new kind (Beschluss 5)
# engine stance vocabulary: Doctrine.StanceText (aggressive/guard/blocker/hunter/support/ambusher)
PATTERNS = {"aggressive", "guard", "blocker", "hunter", "support", "ambusher"}

# Section 4 taboo word list (word-boundary, case-insensitive, EN+DE forms).
TABOO = [
    "dice", "surge", "pierce", "accuracy", "reroll",
    "wound", "wounds", "fatigue", "strain", "statusphase",
    "hidden", "insight", "gambit", "vigilance", "speed", "mobile",
    "hand", "hands", "buy", "xp", "credit", "credits",
    "kredit", "kredite", "kauf", "kaufen",
    "wuerfel", "wuerfeln", "würfel", "würfeln",
    "wunde", "wunden", "erschoepfung", "erschöpfung", "versteckt",
]
ESSENCE_ONLY_KINDS = {"pattern_weight", "guard_split", "signaturText"}
TABOO_RE = re.compile(r"\b(" + "|".join(sorted(set(TABOO))) + r")\b",
                      re.IGNORECASE)

# ---------------------------------------------------------------- canon draft data
# EXACTLY the CONCEPT_personas_v2.md section 2 tables. Field names follow the
# FLAT loader schema (internalNameMatch/factionMatch/amount). Every row keeps
# its concept reference in "concept". "anchor" = the anchor-card DG id named
# by the table; "source_ids" = provenance card/set/deck ids; "source_note" =
# the human-readable derivation line the validator lints for taboo terms.

def _eff(**kw):
    return kw

def canon_personas(heavy, hunter):
    storm = r"^Stormtrooper$"
    officer = r"^Imperial Officer$"
    officer_elite = r"^Imperial Officer \(Elite\)$"
    probe = r"^Probe Droid$"
    # "DROID|HEAVY WEAPON|VEHICLE" (group-keyword filter of the table) cannot
    # ride the name-regex-only engine: enumerate the shipped group names whose
    # groupTraits carry Droid/HeavyWeapon/Vehicle (elite variants inherit via
    # substring match). Derived programmatically at generation time and frozen
    # into the draft so M6b can diff it.
    return [
        OrderedDict([
            ("id", "trooper_captain"),
            ("nameKey", "personaCaptTrooper"),
            ("mottoKey", "personaCaptTrooperMotto"),
            ("flavor", "Masse, die rollt. Das Sturmtruppen-Class-Deck als Persona: Truppen verbilligen, vorwaerts, Reserve."),
            ("concept", "P1 (Bestand, Quellen neu belegt; Deck military_might + precision_training; Agendas stormtrooper_support, imperial_discipline u.a.)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="cost_reduction", amount=1, per=None, internalNameMatch="stormtrooper",
                         anchor="DG001", concept="Sec.2 P1 R1 (Tabelle: cost_reduction TROOPER -1)",
                         source_ids=["military_might__endless_ranks"],
                         source_note="endless_ranks: Mechik-Anker - TROOPER-Bereitstellung verbilligt sich beim Einsetzen."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["aggressive"],
                         internalNameMatch="stormtrooper", concept="Sec.2 P1 R2 (pattern_weight TROOPER aggressive x1.2)",
                         essence=True,
                         source_ids=["military_might__combat_veterans", "military_might__shock_troopers",
                                     "imperial_discipline__fire_at_will"],
                         source_note="Essenz: die Angriffsboni-Karten des Decks wirken als Muster-Druck, nicht als Wuerfel-Manipulation."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="guard_split", guardPercent=25, internalNameMatch="stormtrooper",
                         concept="Sec.2 P1 R3 (guard_split 25 %)",
                         source_ids=["military_might__show_of_force", "military_might__sustained_fire"],
                         source_note="show_of_force + sustained_fire: Druck-Essenz - die Wache-Band der Truppen wird 75/25 zugunsten Angriff."),
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=storm,
                         anchor="DG001", concept="Sec.2 P1 R3 (free_deployment Sturmtruppe x1, Anker DG001 Core)",
                         source_ids=["military_might__endless_ranks"],
                         source_note="endless_ranks-Eskalation: eine Sturmtruppen-Bereitstellung gratis (Ein-Anker-Filter auf DG001, Elite und Twin-Varianten bewusst raus)."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="signaturText", key="personaCaptSignature",
                         concept="Sec.2 P1 R4 (signaturText 'Raenge ohne Ende')",
                         source_ids=["military_might__endless_ranks"],
                         source_note="Deck-Motto endless_ranks als Signaturtext (ui.json-Key, reine Anzeige)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "feldherr"),
            ("nameKey", "personaCaptFeldherr"),
            ("mottoKey", "personaCaptFeldherrMotto"),
            ("flavor", "Er reitet voran. Das Anfuehrungs-Deck laeuft in vorderster Linie - Offiziere verbilligt, gratis, in der Elite-Ausfuehrung, alle Figuren folgen dem Vorstoss."),
            ("concept", "P2 (NEU; inspiring_leadership + Agendas field_commander, the_art_of_war, pursuit_of_the_enemy)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="cost_reduction", amount=1, internalNameMatch="imperial officer",
                         anchor="DG004", concept="Sec.2 P2 R1 (cost_reduction OFFICER -1)",
                         source_ids=["inspiring_leadership__field_officer"],
                         source_note="field_officer: Fuehrungspraesenz - Offiziers-Bereitstellungen verbilligt."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=officer,
                         anchor="DG004", concept="Sec.2 P2 R2 (free_deployment 'Imperial Officer' x1, Anker DG004 Core)",
                         source_ids=["inspiring_leadership__supervisory_agent"],
                         source_note="supervisory_agent: 1:1-Destillation - eine regulaere Offizier-Bereitstellung ohne Bereitstellungs-Kosten."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.15, patterns=["aggressive"],
                         factionMatch="Imperial", concept="Sec.2 P2 R3 (pattern_weight alle Figuren aggressive x1.15; Beschluss 4)",
                         essence=True,
                         source_ids=["inspiring_leadership__lead_by_example", "inspiring_leadership__noble_sacrifice"],
                         source_note="lead_by_example ist Verhaltens-Anker (tabu-frei); noble_sacrifice als Zaehler-Essenz: die Gruppe rueckt geschlossen vor."),
                    _eff(kind="guard_split", guardPercent=20, factionMatch="Imperial",
                         concept="Sec.2 P2 R3 (guard_split 20; Beschluss 4: gewichtet alle Figuren)",
                         source_ids=["inspiring_leadership__lead_by_example"],
                         source_note="Vorwaerts-Identitaet ueber die ganze Garnison: Wache-Anteil 20."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=officer_elite,
                         anchor="DG006", concept="Sec.2 P2 R4 (free_deployment 'Imperial Officer (Elite)' x1, Anker DG006 Core)",
                         source_ids=["inspiring_leadership__field_general"],
                         source_note="field_general als R4-Vertreter der Deck-Familie: die Elite-Offizier-Bereitstellung gratis."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "isb_commander"),
            ("nameKey", "personaCaptIsb"),
            ("mottoKey", "personaCaptIsbMotto"),
            ("flavor", "Der Schatten vor dem Schuss. Tarn-Infrastruktur, Zermuerbung, Kopfgeld-Justiz - ISB-Agenden als schmales, scharfes Profil."),
            ("concept", "P3 (Bestand, Quellen neu belegt; imperial_black_ops + subversive_tactics + ISB-/Infiltrations-Agendas; Ziel-ID in M6c: isb_commandant)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="cost_reduction", amount=1, internalNameMatch=probe,
                         anchor="DG011", concept="Sec.2 P3 R1 (cost_reduction 'Probe Droid' -1)",
                         source_ids=["imperial_black_ops__in_the_shadows"],
                         source_note="Tarn-Infrastruktur-Pflicht der ISB-Daten: Vorpost-Droide verbilligt."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.35, patterns=["ambusher"],
                         factionMatch="Imperial", concept="Sec.2 P3 R2 (pattern_weight alle ambusher x1.35; Hidden->Hinterhalt-Essenz)",
                         essence=True,
                         source_ids=["imperial_black_ops__in_the_shadows", "imperial_black_ops__shadow_corps",
                                     "imperial_black_ops__stealth", "imperial_black_ops__surprise_attack"],
                         source_note="Hidden-Essenz: die Tarn-Karten wirken als Hinterhalt-Doktrin, keine Geheimnis-Verwaltung."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=officer,
                         anchor="DG004", concept="Sec.2 P3 R3 (free_deployment 'Imperial Officer' x1, Anker DG004 wie P2 - Beschluss 3)",
                         source_ids=["imperial_security_bureau__internal_affairs"],
                         source_note="internal_affairs: Destillation der Kernaussage - eine weitere regulaere Offizier-Bereitstellung gratis."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.25, patterns=["aggressive"],
                         factionMatch="Imperial", concept="Sec.2 P3 R4 (pattern_weight alle aggressive x1.25)",
                         essence=True,
                         source_ids=["subversive_tactics__prey_upon_doubt", "subversive_tactics__surgical_strike",
                                     "imperial_black_ops__execution_squad"],
                         source_note="Zermuerbungs-Essenz: Hinrichtungskommando und Schockangriffe wirken als Muster-Druck."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "hutt_lord"),
            ("nameKey", "personaCaptHutt"),
            ("mottoKey", "personaCaptHuttMotto"),
            ("flavor", "Kopfgeld ist Geschaeft. Boesewichte sind die Hauptware: IG-88 gratis auf die Platte, Jagddoktrin auf alles, was Beute wittert."),
            ("concept", "P4 (Bestand, Nemesis-Deck ergaenzt; nemeses + hutt_mercenaries + contract_gunmen)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="cost_reduction", amount=1, factionMatch="Mercenary",
                         anchor=None, concept="Sec.2 P4 R1 (cost_reduction Fraktion Boesewicht -1)",
                         source_ids=["nemeses__leave_them_to_me", "nemeses__powerful_foes"],
                         source_note="Nemesis-Netzwerk: Kopfgeld-Fraktion verbilligt (Core-Anker ueber DG015-DG018, erfuellbar ohne jede Expansion)."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=r"^IG-88$",
                         anchor="DG071", no_core_basis_by_design=True,
                         concept="Sec.2 P4 R2 (free_deployment IG-88 x1, Einzelnanker Kosten 10, bewusst ohne Core-Basis)",
                         source_ids=["nemeses__powerful_foes", "overwhelming_oppression__personal_flagship"],
                         source_note="powerful_foes + personal_flagship: die Boesewicht-Kampagne gibt den Assassinen gratis - auf IG-88 verankert (DG071, Kosten 10, im Pool verifiziert)."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.3, patterns=["hunter"],
                         factionMatch="Mercenary", concept="Sec.2 P4 R3 (pattern_weight Boesewicht hunter x1.3)",
                         essence=True,
                         source_ids=["hutt_mercenaries__guild_hunters", "hutt_mercenaries__most_wanted",
                                     "nemeses__ringleader"],
                         source_note="Kopfgeldjagd-Essenz: guild_hunters/most_wanted/ringleader wirken als Jagddoktrin."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["hunter"],
                         factionMatch="Imperial", concept="Sec.2 P4 R4 (pattern_weight alle hunter x1.2)",
                         essence=True,
                         source_ids=["nemeses__prepare_the_ambush", "nemeses__devastating_legion"],
                         source_note="prepare_the_ambush + devastating_legion als Essenz: die ganze Maschine jagt (Filter = imperriale Seite, damit Boesewicht-Karten nicht doppelt ueber beide Jagd-Reihen fallen)."),
                    _eff(kind="signaturText", key="personaHuttSignature",
                         concept="Sec.2 P4 R4 (signaturText)",
                         source_ids=["nemeses__ringleader"],
                         source_note="Signaturtext der Kopfgeld-Dynastie (ui.json-Key, reine Anzeige)."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "droiden_direktor"),
            ("nameKey", "personaCaptDroidenDirektor"),
            ("mottoKey", "personaCaptDroidenDirektorMotto"),
            ("flavor", "Sondroid-Pflicht. Der Direktor der Schlachtserien: Drohnen und schwere Serien verbilligt, Vorpost gratis, Panzer und Geschuetze greifen hart, die Prototyp-Einheit kommandiert mit."),
            ("concept", "P5 (NEU; technological_superiority + reactive_defenses + armored_onslaught, Technik-Cluster inkl. Panzer Beschluss 1; 88-Z->BT-1-Adaption siehe _meta.adaptations)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="cost_reduction", amount=1, internalNameMatch=probe,
                         anchor="DG011", concept="Sec.2 P5 R1 (cost_reduction 'Probe Droid' -1)",
                         source_ids=["armored_onslaught__automated_repairs", "reactive_defenses__mechanical_protocol"],
                         source_note="Wartungsprotokolle: Vorpost-Droiden verbilligt."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=probe,
                         anchor="DG011", concept="Sec.2 P5 R2 (free_deployment 'Probe Droid' x1, Anker DG011 Core)",
                         source_ids=["reactive_defenses__active_surveillance"],
                         source_note="active_surveillance: 1:1-Destillation - Vorpost-Droide bei Missionsbeginn gratis (Sondroid-Pflicht)."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="pattern_weight", multiplier=1.3, patterns=["aggressive"],
                         internalNameMatch=heavy, concept="Sec.2 P5 R3 (pattern_weight 'DROID|HEAVY WEAPON|VEHICLE' aggressive x1.3)",
                         essence=True,
                         source_ids=["armored_onslaught__rapid_dominance", "reactive_defenses__overclock",
                                     "armored_onslaught__mortar", "armored_onslaught__armor_corps",
                                     "defensive_tactics"],
                         source_note="rapid_dominance ist Verhaltens-Anker (tabu-frei); overclock/mortar/armor_corps als Zonen-Essenz: schwere Serien ruecken aggressiv nach (Filter: aus groupTraits Droid/HeavyWeapon/Vehicle abgeleitete Namens-Enumeration, see _meta.derivations)."),
                    _eff(kind="guard_split", guardPercent=30, internalNameMatch=heavy,
                         concept="Sec.2 P5 R3 (guard_split 30 schwere Einheiten)",
                         essence=True,
                         source_ids=["reactive_defenses__mechanical_protocol", "defensive_tactics"],
                         source_note="mechanical_protocol + defensive_tactics als Verteidigungs-Essenz: schwere Einheiten halten 30 Prozent Wache-Anteil."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=r"^BT-1$",
                         anchor="DG089", concept="Sec.2 P5 R4 (free_deployment BT-1 x1, Anker DG089, Kosten 6)",
                         source_ids=["reactive_defenses__active_surveillance"],
                         source_note="88-Z-Identitaet: die Karten des Decks nennen 88-Z, das im App-Pool nicht existiert - BT-1 (DG089) ist der ehrliche Ersatz, Adaption dokumentiert in _meta.adaptations."),
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=probe,
                         anchor="DG011", concept="Sec.2 P5 R4 (Hybrid-Basis: + free_deployment 'Probe Droid' x1)",
                         source_ids=["reactive_defenses__active_surveillance"],
                         source_note="Core-Hybrid-Basis daneben (Beschluss 2): ohne sie haette Rang 4 ein Geschenk, fuer das es im Core-Kampagnen-Pool keine zweite Stuetze gibt."),
                ])]),
            ]),
        ]),
        OrderedDict([
            ("id", "grossmoff"),
            ("nameKey", "personaCaptGrossmoff"),
            ("mottoKey", "personaCaptGrossmoffMotto"),
            ("flavor", "Angst. Der Groes Moff haelt Flaeche: Garnisonen bleiben, Massenabwuerfe bringen Verstaerkung, und am Ende kommandiert der Grossinquisitor persoenlich."),
            ("concept", "P6 (NEU; overwhelming_oppression + power_of_the_dark_side; Agendas inquisition u.a.)"),
            ("ranks", [
                OrderedDict([("rank", 1), ("minThreat", 2), ("effects", [
                    _eff(kind="guard_split", guardPercent=30, factionMatch="Imperial",
                         concept="Sec.2 P6 R1 (guard_split 30, Garnison haelt Gebiete)",
                         source_ids=["overwhelming_oppression__commence_landing", "overwhelming_oppression__implacable"],
                         source_note="commence_landing + implacable: Landungsdruck und Unbeugsamkeit als Garnison-Doktrin (Wache-Anteil 30 pro Tabelle)."),
                ])]),
                OrderedDict([("rank", 2), ("minThreat", 4), ("effects", [
                    _eff(kind="cost_reduction", amount=1, internalNameMatch="stormtrooper",
                         anchor="DG001", concept="Sec.2 P6 R2 (cost_reduction TROOPER -1 'Massenaushebung')",
                         source_ids=["overwhelming_oppression"],
                         source_note="Das Uberwaeltigungs-Deck als Familien-Anker: Massenaushebung - Truppen verbilligt."),
                ])]),
                OrderedDict([("rank", 3), ("minThreat", 6), ("effects", [
                    _eff(kind="free_deployment", amount=2, per="mission", internalNameMatch=storm,
                         anchor="DG001", concept="Sec.2 P6 R3 (free_deployment Sturmtruppe x2, Anker DG001 Core)",
                         source_ids=["overwhelming_oppression__tie_landers", "overwhelming_oppression__redouble_our_efforts"],
                         source_note="Massenabwurf-Essenz Destillation: die Landungsboote bringen zwei Sturmtruppen-Bereitstellungen gratis auf die Platte."),
                    _eff(kind="pattern_weight", multiplier=1.2, patterns=["guard"],
                         factionMatch="Imperial", concept="Sec.2 P6 R3 (pattern_weight guard alle x1.2)",
                         essence=True,
                         source_ids=["overwhelming_oppression__redouble_our_efforts"],
                         source_note="redouble_our_efforts als Essenz: die Garnison-Doktrin wird schwerer gewichtet."),
                ])]),
                OrderedDict([("rank", 4), ("minThreat", 8), ("effects", [
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=r"^The Grand Inquisitor$",
                         anchor="DG087", concept="Sec.2 P6 R4 (free_deployment The Grand Inquisitor x1, Anker DG087, Kosten 8)",
                         source_ids=["inquisition", "overwhelming_oppression__personal_flagship"],
                         source_note="Inquisition-Set + personal_flagship: der Grossinquisitor kommandiert die Plattform persoenlich."),
                    _eff(kind="free_deployment", amount=1, per="mission", internalNameMatch=storm,
                         anchor="DG001", concept="Sec.2 P6 R4 (Hybrid-Basis: + free_deployment Sturmtruppe x1, kumuliert mit R3 => x3)",
                         source_ids=["overwhelming_oppression__tie_landers"],
                         source_note="Core-Hybrid-Basis daneben (Beschluss 2): kumuliert mit R3 zu Sturmtruppe x3 - der Effektstapel ist im Engine-Modell (Active = Vereinigung aller Raenge) automatisch korrekt."),
                    _eff(kind="signaturText", key="personaGrossmoffSignature",
                         concept="Sec.2 P6 R4 (signaturText 'Angst')",
                         source_ids=["inquisition"],
                         source_note="Inquisition-Set-Motto 'Angst' als Signaturtext (ui.json-Key, reine Anzeige; Schluessel in M6c)."),
                ])]),
            ]),
        ]),
    ]

# Derivations computed from the shipped pools (frozen into the draft's _meta
# so the enumeration is auditable, and into the filter strings themselves).

def derive_enums(pools):
    src_en = load_lenient(ENEMIES)
    heavy = sorted({re.sub(r"\s*\(Elite\)$", "", e["name"]) for e in src_en
                    if {"Droid", "HeavyWeapon", "Vehicle"} & set(e.get("groupTraits", []))})
    hunter = sorted({re.sub(r"\s*\(Elite\)$", "", e["name"]) for e in src_en
                     if "Hunter" in set(e.get("groupTraits", []))})
    # .NET (Personas.cs Regex) treats Python-style \\- as an unknown escape and
    # the loader fail-safes a broken regex to NO match - join the names RAW
    # (they contain no .NET metacharacters beyond the literal hyphen).
    heavy_rx = "|".join(heavy)
    hunter_rx = "|".join(hunter)
    assert "\\" not in heavy_rx and "\\" not in hunter_rx \
        and not re.search(r"[\[\](){}+*?^$.|]", heavy_rx.replace("|", "")), \
        "derived enumerations must stay escape-free for the .NET regex loader"
    return heavy, hunter, heavy_rx, hunter_rx

# ---------------------------------------------------------------- build

def build_draft(pools):
    heavy, hunter, heavy_rx, hunter_rx = derive_enums(pools)
    draft = OrderedDict()
    meta = OrderedDict()
    meta["file"] = "draft_personas_v2.json"
    meta["schema"] = "tacai.personas_v2_draft/1"
    meta["status"] = ("ENTWURF M6a - kanonischer Entwurf fuer M6b; KEIN Runtime-Asset, "
                      "tactical_personas.json wird erst in M6b aufgehoben.")
    meta["purpose"] = ("Datenbasis der sechs imperialen Personas gemaess "
                       + CONCEPT + "; die Raenge folgen automatisch der Threat-Stufe 2/4/6/8.")
    meta["generated_by"] = "tools/gen_personas_v2.py (M6a)"
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
         "daher runtime ALLES (Belege in Assets/Tests/Fixtures/tacai/personas.json: "
         "trooper_captain@2 verbilligt 'Imperial Officer' 2->1; hutt_lord@8 flipp't 'Stormtrooper'->hunter). "
         "M6b darf das nested-Schema NICHT kopieren."),
        ("amount", "free_deployment-Anzahl heisst 'amount' (Loader-Lesart), nicht 'count'."),
        ("dev_fields", "anchor/source_ids/source_note/essence/concept sind Dev-Felder; "
                       "Personas.Load() ignoriert Unbekanntes, M6b kann sie im Asset lassen."),
    ])
    meta["adaptations"] = [
        "88-Z -> BT-1 (Sec.2 P5, Beschluss 2): Die Karten reactive_defenses__active_surveillance/"
        "mechanical_protocol/overclock drucken 88-Z; ein 88-Z gibt es im App-Pool (CardData "
        "enemies+villains) nicht - BT-1 (DG089, Kosten 6) ist der ehrliche Ersatz. P5 R4 schenkt "
        "BT-1 x1 plus Hybrid-Basis Vorpost-Droide x1; P5 R2 haelt den Core-Anker DG011.",
        "armored_battle_group (Quellenzeile Sec.2 P5 R3) existiert nicht in den digitalisierten "
        "Decks (Prüfung: kein Kartenname mit battle/group in 109 Deck- + 126 Agendakarten). "
        "Ersatz: armored_onslaught__armor_corps ('Armor Corps', Panzer-Verband im selben Deck).",
        "pattern_weight-Filter 'DROID|HEAVY WEAPON|VEHICLE' und 'alle hunter': Die Engine matcht "
        "nur interne Kartennamen, keine Traits - die Gruppen-Keywords sind in den Namen nicht "
        "enthalten. Die Filter sind daher als Namens-Enumeration aus den shipped groupTraits "
        "abgeleitet (siehe _meta.derivations); Elite-Varianten erben per Substring-Match.",
        "Sec.2 P4 R4 'alle hunter x1.2': Filter factionMatch Imperial - sonst wuerden Boesewicht-"
        "Karten ueber R3 (1.3) UND R4 (1.2) fallen (PatternFor kettet Active-Reihen), und die "
        "Flip-Wahrscheinlichkeit stapelte sich.",
        "Sec.2 P3 R2 'ambusher': Token existiert in der Engine (Doctrine.StanceText + "
        "OrderPlan-Gewichte); die pattern_weights-Assetzeilen kennen bisher nur "
        "aggressive/guard/blocker/hunter - M6f-prueft, ob die Wuerfelreihen ambusher anwerfen, "
        "sonst flippt die Persona trotzdem direkt darauf (PatternFor braucht keine Wuerfelzeile).",
        "Sec.2 P4 R1 'Fraktion Boesewicht -1': die Engine kennt nur die Fraktionen Imperial und "
        "Mercenary (Personas.cs factionMatch) - umgesetzt als factionMatch Mercenary, genau wie "
        "der Bestandseffekt; ein eigener 'Boesewicht'-Filter-String existiert nicht in den "
        "CardData (IG-88 traegt Mercenary, BT-1/Grossinquisitor Imperial).",
        "Alte->neue Persona-IDs (M6a aendert nichts an Save-Tokens): trooper_captain bleibt, "
        "isb_commander bleibt (Konzept-Ziel-ID isb_commandant erst in M6c), hutt_lord bleibt; "
        "neu: feldherr, droiden_direktor, grossmoff.",
    ]
    meta["persona_id_migration"] = OrderedDict([
        ("trooper_captain", "trooper_captain (P1, erweitert)"),
        ("isb_commander", "isb_commander (P3, Quellen neu belegt; Ziel-ID isb_commandant in M6c)"),
        ("hutt_lord", "hutt_lord (P4, Nemesis-Deck ergaenzt)"),
        ("(neu)", "feldherr (P2), droiden_direktor (P5), grossmoff (P6)"),
    ])
    meta["m6c_ui_keys_pending"] = [
        "personaCaptFeldherr + personaCaptFeldherrMotto",
        "personaCaptDroidenDirektor + personaCaptDroidenDirektorMotto",
        "personaCaptGrossmoff + personaCaptGrossmoffMotto",
        "personaGrossmoffSignature (P6 R4 signaturText)",
        "bestehende Keys bleiben: personaCaptTrooper/Motto, personaCaptSignature, "
        "personaCaptIsb/Motto, personaCaptHutt/Motto, personaHuttSignature",
    ]
    deriv = OrderedDict()
    deriv["heavy_droid_hw_vehicle_names"] = heavy
    deriv["hunter_trait_names"] = hunter
    meta["derivations"] = deriv
    meta["fairness_law"] = ("Nur die vier Mechanik-Kanaele + signaturText (Beschluss 5: KEIN neuer "
                            "Effect-kind; Zonentoken bleiben Wuerfel-Tabu). Kein Zufall auesser der "
                            "geseedeten pattern_weight-Entscheidung. Alles oeffentlich sichtbar.")
    draft["_meta"] = meta
    draft["personas"] = canon_personas(heavy_rx, hunter_rx)
    return draft

# ---------------------------------------------------------------- validator

def _norm_anchor(pool, anchor_id):
    for f in pool:
        if f["id"] == anchor_id:
            return f
    return None

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
            continue          # unfiltered gift/pattern row would match the WHOLE pool
        hits.append(f)
    return hits

def validate(draft, pools):
    """Returns (errors, checks). Errors carry an R<n> rule code; R1-R5 are the
    five card rules, R0 the structural law, R6 dead-filter guard."""
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

        for r in ranks:
            rno = r["rank"]
            seen_guard = False
            for eff in r["effects"]:
                checks += 1
                kind = eff.get("kind", "")
                loc = "%s R%d [%s]" % (pid, rno, kind)
                if kind not in CHANNELS:
                    errors.append("R0: %s: kind outside the four channels (Beschluss 5)" % loc)
                    continue
                if kind == "pattern_weight":
                    checks += 1
                    pats = eff.get("patterns", [])
                    if not pats or not set(pats) <= PATTERNS:
                        errors.append("R0: %s: patterns %r not in engine vocabulary %s"
                                      % (loc, pats, sorted(PATTERNS)))
                    mult = eff.get("multiplier", 1.0)
                    checks += 1
                    if not (1.0 < mult <= 1.5):
                        errors.append("R0: %s: multiplier %s outside (1.0, 1.5]" % (loc, mult))
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

                # -- rule 5: source ids must exist in the shipped assets
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

                # -- rule 4: taboo term in the effect's own cited text
                note = eff.get("source_note", "")
                hits = TABOO_RE.findall(note)
                checks += 1
                if hits:
                    if not eff.get("essence"):
                        errors.append("R4: %s: source cites taboo term %s without essence:true"
                                      % (loc, sorted(set(h.lower() for h in hits))))
                    elif kind not in ESSENCE_ONLY_KINDS:
                        errors.append("R4: %s: essence effect must be pattern_weight/guard_split/"
                                      "signaturText, found kind %s" % (loc, kind))
                if eff.get("essence") and kind not in ESSENCE_ONLY_KINDS:
                    checks += 1
                    errors.append("R4: %s: essence:true but kind %s is not an essence channel"
                                  % (loc, kind))

                # -- regex sanity (fail-safe: broken regex matches NOTHING at runtime)
                if eff.get("internalNameMatch") is not None:
                    checks += 1
                    try:
                        re.compile(eff["internalNameMatch"], re.IGNORECASE)
                    except re.error as exc:
                        errors.append("R0: %s: internalNameMatch does not compile: %s" % (loc, exc))
                        continue

                # -- rule 1 + rule 2 for free_deployment gifts; rule 2 for discounts
                if kind == "free_deployment":
                    hits_f = _filter_matches(eff, pools["pool"])
                    checks += 1
                    if not hits_f:
                        errors.append("R1: %s: filter matches NO shipped figure (dead gift)" % loc)
                        continue
                    names = {f["name"] for f in hits_f}
                    costs = {f["cost"] for f in hits_f}
                    if len(names) > 1 and len(costs) > 1:
                        errors.append("R1: %s: mixed-cost filter %s -> gift size would depend on "
                                      "the random hand (one-anchor law Sec.1.1)" % (loc, sorted(names)))
                    checks += 1
                    anchor = _norm_anchor(pools["pool"], eff.get("anchor"))
                    if anchor is None:
                        errors.append("R2: %s: anchor %r not in shipped enemies/villains"
                                      % (loc, eff.get("anchor")))
                        continue
                    checks += 1
                    if anchor["name"].split(" (Elite)")[0] not in \
                            {n.split(" (Elite)")[0] for n in names}:
                        errors.append("R2: %s: anchor name %r not matched by its own filter"
                                      % (loc, anchor["name"]))
                    if anchor["id"] in pools["villain_ids"]:
                        # rule 3: villain anchors must exist in villains.json (already true
                        # by construction of the merged pool) and follow the table law
                        checks += 1
                        rank_frees = [e for e in r["effects"] if e.get("kind") == "free_deployment"]
                        others = [e for e in rank_frees if e is not eff]
                        if not others and not eff.get("no_core_basis_by_design"):
                            errors.append("R3: %s: villain gift without core hybrid basis and "
                                          "without no_core_basis_by_design (Beschluss 2)" % loc)
                    else:
                        checks += 1
                        if anchor["expansion"] != "Core":
                            errors.append("R2: %s: non-villain anchor %s(%s) expansion '%s' != Core "
                                          "(Sec.1.2a: non-core cards are never gift anchors)"
                                          % (loc, anchor["id"], anchor["name"], anchor["expansion"]))
                    checks += 1
                    if eff.get("amount", 1) < 1:
                        errors.append("R0: %s: amount must be >= 1" % loc)
                elif kind == "cost_reduction":
                    hits_f = _filter_matches(eff, pools["pool"])
                    checks += 1
                    if not hits_f:
                        errors.append("R6: %s: filter matches NO shipped figure (dead discount)" % loc)
                    else:
                        realizable = [f for f in hits_f
                                      if f["expansion"] == "Core" or f["id"] in pools["villain_ids"]]
                        checks += 1
                        if not realizable:
                            errors.append("R2: %s: no Core-or-villain target - discount unfeasible "
                                          "in a Core-only campaign (Sec.1.2a anchor)" % loc)
                elif kind in ("pattern_weight", "guard_split"):
                    if eff.get("internalNameMatch") is not None:
                        checks += 1
                        if not _filter_matches(eff, pools["pool"]):
                            errors.append("R6: %s: filter matches NO shipped figure (dead filter)" % loc)

    # -- rule 3 spot laws from the table (explicit villain roster)
    for want_id, want_cost, want_where in (("DG071", 10, "P4/hutt_lord R2"),
                                           ("DG089", 6, "P5/droiden_direktor R4"),
                                           ("DG087", 8, "P6/grossmoff R4")):
        checks += 1
        fig = _norm_anchor(pools["pool"], want_id)
        if fig is None or fig["id"] not in pools["villain_ids"]:
            errors.append("R3: %s: villain anchor %s missing from villains.json" % (want_where, want_id))
        elif fig["cost"] != want_cost:
            errors.append("R3: %s: %s printed cost %s != table cost %s" % (want_where, want_id, fig["cost"], want_cost))

    # hybrid bases required by the table on P5 R4 / P6 R4 (second free effect)
    checks += 1
    for pid, rno, second_anchor in (("droiden_direktor", 4, "DG011"), ("grossmoff", 4, "DG001")):
        p = next((x for x in personas if x["id"] == pid), None)
        r = next((x for x in (p or {}).get("ranks", []) if x["rank"] == rno), None) if p else None
        frees = [e for e in (r or {}).get("effects", []) if e.get("kind") == "free_deployment"]
        if len(frees) != 2 or not any(e.get("anchor") == second_anchor for e in frees):
            errors.append("R3: %s R%d: table hybrid basis (second free effect on %s) missing"
                          % (pid, rno, second_anchor))
    return errors, checks

# ---------------------------------------------------------------- self-tests

def selftest(draft, pools):
    """Prove the validator goes RED on each rule class (mutated COPIES).
    Returns list of (name, ok, detail)."""
    import copy

    def mutate(fn):
        d = copy.deepcopy(draft)
        fn(d)
        errs, _ = validate(d, pools)
        return errs

    def find_p(d, pid, rank, kind, idx=0):
        p = next(x for x in d["personas"] if x["id"] == pid)
        r = next(x for x in p["ranks"] if x["rank"] == rank)
        same = [e for e in r["effects"] if e["kind"] == kind]
        return same[idx]

    cases = []

    def m_core_anchor(d):
        e = find_p(d, "droiden_direktor", 2, "free_deployment")
        e["internalNameMatch"] = r"^AT-DP$"
        e["anchor"] = "DG046"          # exp Empire gift anchor
    cases.append(("R2 core-anchor law", lambda: any("R2:" in x for x in mutate(m_core_anchor))))

    def m_mixed_cost(d):
        e = find_p(d, "trooper_captain", 3, "free_deployment")
        e["internalNameMatch"] = "stormtrooper"   # regular 6 vs elite 8
        e["anchor"] = "DG001"
    cases.append(("R1 mixed-cost gift", lambda: any("R1:" in x for x in mutate(m_mixed_cost))))

    def equal_cost_check():
        # two names EQUAL cost: "^Wampa$|^Tusken Raider$" (both 5) -> R1 must stay quiet,
        # while R2 still fires (Twin/other expansion) - proves the equal-cost exemption.
        errs = mutate(lambda d: (find_p(d, "trooper_captain", 3, "free_deployment").update(
            {"internalNameMatch": "^Wampa$|^Tusken Raider$", "anchor": "DG021"})))
        return not any("R1:" in x for x in errs) and any("R2:" in x for x in errs)
    cases.append(("R1 equal-cost exemption stays green", equal_cost_check))

    def m_taboo():
        d = copy.deepcopy(draft)
        e = find_p(d, "feldherr", 3, "pattern_weight")
        e["essence"] = False
        e["source_note"] = "Druck wie im Original: reroll erlaubt"
        return validate(d, pools)[0]
    cases.append(("R4 taboo without essence", lambda: any("R4:" in x for x in m_taboo())))

    def essence_on_gift():
        d = copy.deepcopy(draft)
        find_p(d, "droiden_direktor", 2, "free_deployment")["essence"] = True
        find_p(d, "droiden_direktor", 2, "free_deployment")["source_note"] = "Hidden-Essenz zitiert"
        return validate(d, pools)[0]
    cases.append(("R4 essence only on 3 kinds", lambda: any("essence:true but kind free_deployment" in x
                                                            for x in essence_on_gift())))

    def fake_source():
        d = copy.deepcopy(draft)
        find_p(d, "grossmoff", 3, "free_deployment")["source_ids"] = ["overwhelming_oppression__tie_lander"]
        return validate(d, pools)[0]
    cases.append(("R5 fake source id", lambda: any("R5:" in x for x in fake_source())))

    def villain_gone():
        pools2 = dict(pools)
        pools2["pool"] = [f for f in pools["pool"] if f["id"] != "DG087"]
        pools2["villain_ids"] = {i for i in pools["villain_ids"] if i != "DG087"}
        errs, _ = validate(draft, pools2)
        return errs
    cases.append(("R3 villain anchor missing", lambda: any("R3:" in x for x in villain_gone())))

    def hybrid_missing():
        d = copy.deepcopy(draft)
        r = next(x for x in next(p for p in d["personas"] if p["id"] == "droiden_direktor")["ranks"]
                 if x["rank"] == 4)
        r["effects"] = [e for e in r["effects"] if not (e["kind"] == "free_deployment" and e.get("anchor") == "DG011")]
        return validate(d, pools)[0]
    cases.append(("R3 hybrid basis missing", lambda: any("R3:" in x for x in hybrid_missing())))

    def dead_filter():
        d = copy.deepcopy(draft)
        find_p(d, "isb_commander", 2, "pattern_weight")["internalNameMatch"] = r"^Unicorn$"
        return validate(d, pools)[0]
    cases.append(("R6 dead filter", lambda: any("R6:" in x for x in dead_filter())))

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

    print("draft   : %s" % DRAFT_PATH.relative_to(REPO))
    print("personas: %d  ranges/ranks: %d  effects: %d" % (n_personas, n_ranks, n_effects))
    print("validator checks: %d  errors: %d" % (checks, len(errors)))
    for e in errors:
        print("  ERROR " + e)
    status = "GRUEN" if not errors else "ROT"
    print("validator: %s" % status)

    st = selftest(on_disk, pools)
    print("self-tests (validator must be able to go red):")
    st_fail = 0
    for name, ok in st:
        print("  %s %s" % ("ok  " if ok else "FAIL", name))
        if not ok:
            st_fail += 1

    if errors or st_fail or n_personas != 6 or n_ranks != 24:
        sys.exit(1)

if __name__ == "__main__":
    main()
