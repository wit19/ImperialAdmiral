#!/usr/bin/env python3
# M24 Zweitimplementierung (Thomas-Ruling 06.11.: Heiler-Kartenpin wird
# ZUERST gechippt - Class 3 ueber dem Support). Die C#-Kette HeroCard.
# HeilerEffektiv -> HeroProfiles.ChipKey -> OverlordFocus.CompareChip wird
# hier auf eigenen Beinen nachgerechnet (nur stdlib + Fixture-Daten); die
# Anker vergleichen ASSET-Wahrheit, Klassenschluessel und die Chip-
# Reihenfolge der drei m24-Cases gegen die Fixtures.
import json, sys, os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
FIX = os.path.join(ROOT, 'ImperialCommander2/Assets/Tests/Fixtures/tacai')
EN = os.path.join(ROOT, 'ImperialCommander2/Assets/Resources/Languages/En')

fails = []
def anchor(name, got, want):
    ok = got == want
    print(('  OK  ' if ok else '  FAIL') + f' {name}: got={got!r} want={want!r}')
    if not ok:
        fails.append(name)

cards = json.load(open(os.path.join(EN, 'tactical_hero_cards.json'), encoding='utf-8'))
heroes = {h['appHeroId']: h for h in cards['heroes']}

# --- Anker 1: Schema und MHD-Kit sind im Asset verankert ---
anchor('schema/5', cards['schema'], 'tacai.hero_cards/5')
mhd = heroes['H10']
anchor('mhd-kit', mhd['effekte']['healer']['skills'],
       ['mhd01', 'mhd03', 'mhd05', 'mhd06', 'mhd07'])
# Die Kit-IDs sind die ECHLEN Spiel-IDs aus CampaignData/skills.json:
skills = json.load(open(os.path.join(ROOT,
    'ImperialCommander2/Assets/Resources/Languages/De/CampaignData/skills.json'), encoding='utf-8'))
srows = skills['skills'] if isinstance(skills, dict) and 'skills' in skills else skills
sids = {r.get('id') for r in srows}
anchor('kit-ids-exist', all(k in sids for k in mhd['effekte']['healer']['skills']), True)
# Kein anderer Held traegt (heute) einen healer-Pin:
others = [h['appHeroId'] for h in cards['heroes'] if 'healer' in (h.get('effekte') or {})]
anchor('only-mhd-pinned', others, ['H10'])
# Gaarkhan-Selbstheilung pinnt NICHT (Ruling: selbst != Heiler):
anchor('gaarkhan-inert', 'healer' in (heroes['H3'].get('effekte') or {}), False)

# --- Anker 2: Gesetz als reine Funktion (Spiegel von HeilerEffektiv/ChipKey) ---
def heiler_effektiv(hero, loadout):
    kit = (hero.get('effekte') or {}).get('healer', {}).get('skills', [])
    return any(s in (loadout or []) for s in kit)

def supporter_effektiv(hero, loadout):
    sup = (hero.get('effekte') or {}).get('supporter') or {}
    if sup.get('always'): return True
    return any(s in (loadout or []) for s in sup.get('skills', []))

def chip_class(hero, role, loadout):
    hp = heiler_effektiv(hero, loadout)
    sp = supporter_effektiv(hero, loadout)
    return 3 if hp else (0 if role == 'healer' else (2 if (role == 'support' or sp) else 1))

prof = json.load(open(os.path.join(EN, 'tactical_hero_profiles.json'), encoding='utf-8'))
role_of = {p['name']: p['role'] for p in prof['heroes']}

anchor('mhd-klasse-ohne-kit', chip_class(mhd, role_of['MHD-19'], []), 2)
anchor('mhd-klasse-mit-mhd07', chip_class(mhd, role_of['MHD-19'], ['mhd07']), 3)
anchor('mhd-klasse-mit-fremdkarte', chip_class(mhd, role_of['MHD-19'], ['mhd02']), 2)
anchor('gaarkhan-selbstheilung-1', chip_class(heroes['H3'], role_of['Gaarkhan'], ['gaarkhan01']), 1)
anchor('fenn-identitaet-2', chip_class(heroes['H2'], role_of['Fenn Signis'], []), 2)

# --- Anker 3: Chip-Vergleich (CompareChip-Spiegel: absteigend lexikografisch) ---
def cmp_chip(a, b):  # a,b = class lists; return -1 => a fuehrt
    for x, y in zip(a, b):
        if x != y: return -1 if x > y else 1
    return 0
# Runner/ev der neutralen Prior-Kuratur (mob 2 x rng 5, ev 1) fuer beide:
RUN, EV = 10, 1
anchor('hammer-first-reihenfolge',
       cmp_chip([3, RUN, EV], [2, RUN, EV]) < 0, True)
anchor('baseline-gleichstand',
       cmp_chip([2, RUN, EV], [2, RUN, EV]), 0)
anchor('identity-healer-letzt',
       cmp_chip([1, RUN, EV], [0, RUN, EV]) < 0, True)

# --- Anker 4: die drei Fixtures replizieren exakt diese Reihenfolge ---
fx = json.load(open(os.path.join(FIX, 'overlord_focus.json'), encoding='utf-8'))
cases = {c['name']: c for c in fx['cases']}
def order_of(case):
    return [e['hero'] for e in case['expect']]
anchor('fixture-hammer-first', order_of(cases['m24_healer_hammer_first']), ['MHD-19', 'Fenn Signis'])
anchor('fixture-baseline-flip', order_of(cases['m24_healer_pin_flip_baseline']), ['Fenn Signis', 'MHD-19'])
anchor('fixture-selfheal-inert', order_of(cases['m24_selfheal_not_healer']), ['MHD-19', 'Gaarkhan'])
# Gewinner hat je die NIEDRIGERE Threat -> Chip schlaegt Threat (M22-Falle):
def ev(x):
    from fractions import Fraction
    return Fraction(x)
w, l = cases['m24_healer_hammer_first']['expect']
anchor('hammer-gewinner-droht-weniger', ev(w['threat']) < ev(l['threat']), True)
# Selbstheilungs-Fall: sein eigentlicher Beweis ist die INERTIE. Diskrimi-
# nierend ist der Klassenvergleich: haette gaarkhan01 false einen Heiler-Pin
# (Klasse 3), Ruecke Gaarkhan vor MHD (Klasse 2) — die actual-Reihenfolge
# ist genau umgekehrt. Threat alone waehlt hier dasselbe Bild (1 > 5/6),
# die Threat-Ueberbietung ist NICHT die Aussage dieses Falls (die liefert
# der hammer-first-Fall mit MHD 1 gegen Fenn 19/10).
g = cases['m24_selfheal_not_healer']
wrong_order = ['Gaarkhan', 'MHD-19'] if cmp_chip([3, RUN, EV], [2, RUN, EV]) < 0 else ['Gaarkhan', 'MHD-19']
anchor('selfheal-keine-falsche-pin-hebung', order_of(g) != wrong_order, True)

print()
print('M24-Zweitengine:', 'ALLE ANKER OK' if not fails else f'{len(fails)} FEHLER: {fails}')
sys.exit(1 if fails else 0)
