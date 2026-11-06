#!/usr/bin/env python3
"""M22 second implementation: Diala Passil's printed skill pins + the
supporter chip law, computed INDEPENDENTLY of the C# port. Shares only the
M19 dice statistics (m19_band_average_verify is definitions-only, import
safe); the threat math is re-typed here on purpose (second engine).

Anchors:
  A1-A4  supporter truth table from the effekte asset (identity Davith/MHD,
         Diala conditional diala02/diala03, diala04 inert)
  A5-A7  diala05 "Apply +1 Speed to your hero": healthy tier 2->3 (72->85),
         wounded tier 1->2 (+13), no-pin neutrality (pre-M22 values)
  A8-A10 the three woundAll chip cases re-derived: class key decides; the
         negative control proves the threat still decides WITHOUT the pin
  A11    Fenn regression: the M21 pins survived the asset edit
Exit code 0 = zero deviations.
"""
import json, os, sys
from fractions import Fraction as F
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import m19_band_average_verify as V

LANG = V.LANG                                   # .../Assets/Resources/Languages/En
ROOT = os.path.abspath(os.path.join(LANG, '..', '..', '..', '..'))
FIX = os.path.join(ROOT, 'Assets', 'Tests', 'Fixtures', 'tacai')
CARDS = V.CARDS
ROLES = {h['name']: h.get('role', '') for h in
         json.load(open(os.path.join(LANG, 'tactical_hero_profiles.json')))['heroes']}

def effekte(card):
    return card.get('effekte') or {}

def blast_wert(card, skills):
    e = effekte(card).get('explosion')
    if not e: return 0
    w = int(e.get('wert', 0))
    for u in e.get('upgrades', []):
        if u.get('skill') in (skills or []):
            w = max(w, int(u.get('wert', 0)))
    return w

def tempo_delta(card, skills):
    return sum(int(p.get('delta', 0)) for p in effekte(card).get('tempo_pins', [])
               if p.get('skill') in (skills or []))

def is_supporter(card, skills):
    sup = effekte(card).get('supporter') or {}
    if sup.get('always'): return True
    return any(s in (skills or []) for s in sup.get('skills', []))

def threat(hero, wounded, loadout, posture, defense=None):
    card = CARDS[hero['profile'] if 'profile' in hero else hero['hero']]
    skills = (loadout or {}).get('skills', [])
    items = (loadout or {}).get('items', [])
    kw_acc = sum(int(V.ITEMS[i].get('genauigkeit', 0) or 0) for i in items if i in V.ITEMS)
    kw_dmg = sum(int(V.ITEMS[i].get('schaden', 0) or 0) for i in items if i in V.ITEMS)
    pierce = sum(int(V.ITEMS[i].get('durchschlag', 0) or 0) for i in items if i in V.ITEMS)
    pools = [(card['basisAngriffswuerfel'], card['angriffsTyp'] != 'P', F(0))]
    for iid in items:
        e = V.ITEMS.get(iid)
        if e is None: continue
        ranged = e['typ'] == 'O'
        b = V.weapon_bonus(iid)
        if e.get('wuerfel'): pools.append((e['wuerfel'], ranged, b))
        if e.get('attribut'):
            ap = V.attr_pool(card, e['attribut'], wounded)
            if ap: pools.append((ap, ranged, b))
    rng = card['reichweite']
    for iid in items:
        e = V.ITEMS.get(iid)
        if e is None or e['typ'] != 'O': continue
        letters = e.get('wuerfel') or (V.attr_pool(card, e.get('attribut'), wounded)
                                       if e.get('attribut') else None)
        if not letters: continue
        st = V.Stats(letters, int(e.get('genauigkeit', 0) or 0), 0, True, None)
        if st.gate > rng: rng = st.gate
    stats = [(V.Stats(letters, kw_acc, kw_dmg, ranged, defense), b)
             for letters, ranged, b in pools]
    total = F(0)
    for d in range(1, rng + 1):
        total += max((st.ev(d, pierce) + b for st, b in stats), default=F(0))
    bav = total / rng + F(1, 2) * blast_wert(card, skills)
    if posture in ('eliminate', 'attrition'):
        return bav
    band_t = (card['tempoVerwundet'] if wounded else card['tempo']) + tempo_delta(card, skills)
    band = 1 if band_t <= 3 else 2 if band_t == 4 else 3
    return int(100 * (F(3, 5) * bav / F(8, 3) + F(2, 5) * band / 3) + F(1, 2))

fails = 0
def check(desc, got, want):
    global fails
    ok = got == want
    if not ok: fails += 1
    print(('OK   ' if ok else 'FAIL ') + f'{desc}: got {got!r} want {want!r}')

def fx(name):
    doc = json.load(open(os.path.join(FIX, 'overlord_focus.json')))
    return next(c for c in doc['cases'] if c['name'] == name)

diala, davith, mhd, fenn = (CARDS['Diala Passil'], CARDS['Davith Elso'],
                            CARDS['MHD-19'], CARDS['Fenn Signis'])

# A1-A4 supporter truth table
check('A1 Diala+diala02 supporter', is_supporter(diala, ['diala02']), True)
check('A2 Diala+diala03 supporter', is_supporter(diala, ['diala03']), True)
check('A3 Diala+diala04 NO supporter', is_supporter(diala, ['diala04']), False)
check('A4 identity pins', (is_supporter(davith, []), is_supporter(mhd, []),
                           is_supporter(diala, [])), (True, True, False))

# A5-A7 diala05 tempo pin (M20 law by hand: bav=2 -> 60%*2/(8/3)=45, +40%*tier/3)
check('A5 diala05 healthy 72->85 (tier 2->3)',
      threat({'hero': 'Diala Passil'}, False, {'items': [], 'skills': ['diala05']}, 'protect'), 85)
WOPIN = threat({'hero': 'Diala Passil'}, True, {'items': [], 'skills': []}, 'protect')
WPIN = threat({'hero': 'Diala Passil'}, True, {'items': [], 'skills': ['diala05']}, 'protect')
check('A6 diala05 wounded tier 1->2 (+13)', (WOPIN, WPIN), (58, 72))
check('A7 no-pin neutrality healthy', 
      threat({'hero': 'Diala Passil'}, False, {'items': [], 'skills': []}, 'protect'), 72)

# A8-A10 woundAll chip cases: the CLASS key re-derived from the asset.
# Base-game heroes carry no hero card -> pin cannot fire (live law: chip pin
# needs c.Card). Tie-breaks inside equal class run the full Compare chain and
# are pinned by the canon itself - here we prove (a) the class of every row
# and (b) that the class-2 hero leads the recorded order.
def classes(case):
    out = []
    for h in case['heroes']:
        card = CARDS.get(h['hero'])          # base-game heroes have no hero card
        cls = 0 if ROLES[h['hero']] == 'healer' else \
              2 if (ROLES[h['hero']] == 'support'
                    or (card is not None
                        and is_supporter(card,
                                         h.get('loadout', {}).get('skills', [])))) else 1
        out.append((h['hero'], cls))
    return out

EXPECT_CLASS = {
    'm22_supporter_chip_first':  [('Han Solo', 1), ('MHD-19', 2)],
    'm22_diala_supporter_pin':   [('Han Solo', 1), ('Diala Passil', 2)],
    'm22_diala_support_inert':   [('Han Solo', 1), ('Diala Passil', 1)],
}
for name in EXPECT_CLASS:
    case = fx(name)
    check('A8-10 ' + name + ' classes', classes(case), EXPECT_CLASS[name])
    canon = [r['hero'] for r in case['expect']]
    lead = canon[0]
    cls = dict(classes(case))
    lead_ok = cls[lead] == max(cls.values())          # class-2 hero leads (or none has it)
    check('    canon ' + name + ' order consistent', (canon, lead_ok),
          (canon, True))

# A11 Fenn regression
check('A11 Fenn pins intact', effekte(fenn).get('explosion'),
      {'wert': 1, 'upgrades': [{'skill': 'fenn07', 'wert': 2}]})

print(f'== M22 sweep: {"ALL PASS" if fails == 0 else str(fails) + " FAILS"}')
sys.exit(1 if fails else 0)
