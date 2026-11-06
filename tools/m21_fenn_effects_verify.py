#!/usr/bin/env python3
# M21 second implementation (Thomas rulings 06.11., Fenn playtest):
#   (a) blast increment: flat + (1/2 x effective printed blast value) on the
#       damage operand; effective value = explosion.wert, ABSORBED to the
#       upgrade value when the hero carries an upgrade skill id (fenn07).
#   (b) tempo pins: band tempo = printed tempo + sum(delta of carried
#       tempo_pin skills), applied HEALTHY AND WOUNDED alike.
# Re-uses the M19 port's dice/pairing stats (same table the harness pins)
# but re-derives the effekte parsing from the asset INDEPENDENTLY of
# HeroCards.cs. Sweeps EVERY card-basis row of the focus fixtures plus the
# hand-calc anchors (m21_fenn_handrechnerisch below).
import json, io, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import m19_band_average_verify as V
from fractions import Fraction as F

LANG = V.LANG
CARDS = V.CARDS   # name -> hero dict incl. optional "effekte"

EXPLOSION_SHARE = F(1, 2)

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
    d = 0
    for p in effekte(card).get('tempo_pins', []):
        if p.get('skill') in (skills or []):
            d += int(p.get('delta', 0))
    return d

def threat21(hero, wounded, loadout, posture, defense, engage=None):
    """M19 band-average threat + M21 effekte, independently computed."""
    card = CARDS.get(hero['profile'] if 'profile' in hero else hero['hero'])
    if card is None: return None
    skills = (loadout or {}).get('skills', [])
    # --- damage operand via the M19 stats machinery (shared dice tables) ---
    items = (loadout or {}).get('items', [])
    kw_acc = sum(int(V.ITEMS[i].get('genauigkeit', 0) or 0) for i in items if i in V.ITEMS)
    kw_dmg = sum(int(V.ITEMS[i].get('schaden', 0) or 0) for i in items if i in V.ITEMS)
    pierce = sum(int(V.ITEMS[i].get('durchschlag', 0) or 0) for i in items if i in V.ITEMS)
    engagePierce = (engage or {}).get('pierce', 0)
    engageAcc = (engage or {}).get('acc_delta', 0)
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
    range_ = card['reichweite']
    for iid in items:
        e = V.ITEMS.get(iid)
        if e is None or e['typ'] != 'O': continue
        letters = e.get('wuerfel') or (V.attr_pool(card, e.get('attribut'), wounded) if e.get('attribut') else e.get('wuerfel'))
        if not letters: continue
        ka = int(e.get('genauigkeit', 0) or 0)
        st = V.Stats(letters, ka, 0, True, None)
        if st.gate > range_: range_ = st.gate
    stats = []
    for letters, ranged, b in pools:
        st = V.Stats(letters, kw_acc, kw_dmg, ranged, defense)
        if engageAcc:
            st.acc = max(F(0), st.acc + engageAcc)
            st.gate = int(st.acc + F(1, 2))
        stats.append((st, b))
    total = F(0)
    for d in range(1, range_ + 1):
        best = F(0)
        for st, b in stats:
            v = st.ev(d, pierce + engagePierce) + b
            if v > best: best = v
        total += best
    bav = total / range_
    # --- M21 (a): flat blast increment on the damage operand -------------
    bav += EXPLOSION_SHARE * blast_wert(card, skills)
    if posture in ('eliminate', 'attrition'):
        return bav
    # --- M21 (b): band tempo with the pin deltas, BOTH states ------------
    band_t = (card['tempoVerwundet'] if wounded else card['tempo']) + tempo_delta(card, skills)
    band = 1 if band_t <= 3 else 2 if band_t == 4 else 3
    th = 100 * (F(3, 5) * bav / F(8, 3) + F(2, 5) * band / 3)
    return int(th + F(1, 2))

def fstr(x):
    if x is None: return None
    if isinstance(x, int): return str(x)
    return str(x.numerator) if x.denominator == 1 else f"{x.numerator}/{x.denominator}"

# ---------------------------------------------------------------- anchors
FENN = {'hero': 'Fenn Signis'}
fails = 0
def anchor(desc, got, want):
    global fails
    g = fstr(got) if not isinstance(got, int) else str(got)
    ok = g == want
    if not ok: fails += 1
    print(('OK  ' if ok else 'FAIL') + f' {desc}: port {g} vs hand {want}')

anchor('Fenn elim (Blast 1/2-Pauschale)', threat21(FENN, False, None, 'eliminate', None), '19/10')
anchor('Fenn stall Band2 + Pauschale', threat21(FENN, False, None, 'stall', None), '69')
anchor('Fenn stall fenn01 (Band 3)', threat21(FENN, False, {'skills': ['fenn01']}, 'stall', None), '83')
anchor('Fenn elim fenn01 (Band irrelevant)', threat21(FENN, False, {'skills': ['fenn01']}, 'eliminate', None), '19/10')
anchor('Fenn elim fenn07 (Blast 2 -> +1)', threat21(FENN, False, {'skills': ['fenn07']}, 'eliminate', None), '12/5')
anchor('Fenn stall fenn07', threat21(FENN, False, {'skills': ['fenn07']}, 'stall', None), '81')
anchor('Fenn stall fenn01+fenn07', threat21(FENN, False, {'skills': ['fenn01', 'fenn07']}, 'stall', None), '94')
anchor('Fenn verw. elim (Print 19/10, Wirtschaft x2 = 19/5)', threat21(FENN, True, None, 'eliminate', None), '19/10')
anchor('Fenn verw. stall fenn01 (Band 2)', threat21(FENN, True, {'skills': ['fenn01']}, 'stall', None), '69')
anchor('Fenn verw. stall ohne Pin (Band 1)', threat21(FENN, True, None, 'stall', None), '56')
anchor('JYN elim unchanged (kein effekte)', threat21({'hero': 'Jyn Odan'}, False, None, 'eliminate', None), '4/3')
anchor('JYN stall unchanged', threat21({'hero': 'Jyn Odan'}, False, None, 'stall', None), '70')
anchor('DAVITH stall unchanged', threat21({'hero': 'Davith Elso'}, False, None, 'stall', None), '72')

# ------------------------------------------------------ fixture sweep
FIX = V.FIX
checked = 0
for fn in ('overlord_focus', 'imperial_card_effects'):
    try:
        fix = V.load(f'{FIX}/{fn}.json')
    except FileNotFoundError:
        continue
    for c in fix['cases']:
        post = c.get('posture'); dfn = c.get('defense'); eng = c.get('engage')
        exp = c['expect']
        heroes = {h['hero']: h for h in c.get('heroes', [])}
        rows = [(r, None) for r in (exp if isinstance(exp, list) else (exp.get('rows') or []))
                if isinstance(r, dict) and 'hero' in r] \
               if isinstance(exp, list) or (isinstance(exp, dict) and isinstance(exp.get('rows'), list)) else []
        for r, e2 in rows:
            if r.get('basis') != 'card' or r.get('threat') is None: continue
            h = heroes.get(r['hero'])
            if h is None or CARDS.get(h.get('profile', r['hero'])) is None: continue
            t = threat21(h, r.get('wounded', h.get('wounded', False)),
                         h.get('loadout'), post, dfn, e2 if e2 is not None else eng)
            if t is None: continue
            if h.get('objective_actor'): t = t + 1
            sur = F(c.get('surcharge', 0) or 0)
            if h.get('conditioned') and sur: t = t + sur
            checked += 1
            if fstr(t) != str(r['threat']):
                fails += 1
                print(f'FAIL {fn}/{c["name"]} {r["hero"]}: port {fstr(t)} vs canon {r["threat"]}')
print(f'== M21 sweep: {checked} card-basis rows re-derived, {fails} fails total')
sys.exit(1 if fails else 0)
