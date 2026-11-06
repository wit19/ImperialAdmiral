#!/usr/bin/env python3
# M21 second implementation (Thomas rulings 06.11., Fenn playtest),
# carried to M25 canon (Thomas ruling 06.11., Karten-Audit 2.): printed
# STATIC accuracy rides the pairing-law acc operand (weapon line both
# states + healthy hero-card side + accuracy cards) and the printed
# EXTRA-ATTACK/Verstaerkung cards ride the M21 flat addend seam
# (angriffs_bonus fractions + Loku's Battle Vision, healthy side only).
# Every pre-M25 hero parses bit-identical (no fields = zero = old law).
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

def hero_acc(card, skills, wounded):
    a = int(card.get('waffenGenauigkeit', 0) or 0)
    if not wounded: a += int(card.get('genauigkeitGesund', 0) or 0)
    for pin in effekte(card).get('genauigkeit_pins', []):
        if pin.get('skill') in (skills or []):
            a += int(pin.get('delta', 0))
    return a

def flat_bonus(card, skills, wounded):
    b = F(0)
    for pin in effekte(card).get('angriffs_bonus', []):
        if pin.get('skill') in (skills or []):
            b += F(pin.get('wert', '0'))
    if not wounded:
        b += F(str(card.get('verstaerkungGesund', 0) or 0))
    return b

def threat21(hero, wounded, loadout, posture, defense, engage=None):
    """M19 band-average threat + M21 effekte, independently computed."""
    card = CARDS.get(hero['profile'] if 'profile' in hero else hero['hero'])
    if card is None: return None
    skills = (loadout or {}).get('skills', [])
    # --- damage operand via the M19 stats machinery (shared dice tables) ---
    items = (loadout or {}).get('items', [])
    kw_acc = sum(int(V.ITEMS[i].get('genauigkeit', 0) or 0) for i in items if i in V.ITEMS)
    kw_acc += hero_acc(card, skills, wounded)   # M25 printed static accuracy
    kw_dmg = sum(int(V.ITEMS[i].get('schaden', 0) or 0) for i in items if i in V.ITEMS)
    pierce = sum(int(V.ITEMS[i].get('durchschlag', 0) or 0) for i in items if i in V.ITEMS)
    engagePierce = (engage or {}).get('pierce', 0)
    engageAcc = (engage or {}).get('acc_delta', 0)
    pools = [(card['basisAngriffswuerfel'], card['angriffsTyp'] != 'P', F(0))]
    pool_is_melee_item = [False]   # M26: coverage of every pool
    has_ranged = False
    for iid in items:
        e = V.ITEMS.get(iid)
        if e is None: continue
        ranged = e['typ'] == 'O'
        b = V.weapon_bonus(iid)
        if e.get('wuerfel'):
            pools.append((e['wuerfel'], ranged, b)); pool_is_melee_item.append(not ranged)
            has_ranged = has_ranged or ranged
        if e.get('attribut'):
            ap = V.attr_pool(card, e['attribut'], wounded)
            if ap:
                pools.append((ap, ranged, b)); pool_is_melee_item.append(not ranged)
                has_ranged = has_ranged or ranged
    # M26 (Player-29 "Jyn hat den DL-44"): carried ranged weapon replaces the
    # printed standard outright; {{P}} melee standard is never replaced.
    base_replaced = has_ranged and card['angriffsTyp'] != 'P'
    range_ = card['reichweite']
    for iid in items:
        e = V.ITEMS.get(iid)
        if e is None or e['typ'] != 'O': continue
        letters = e.get('wuerfel') or (V.attr_pool(card, e['attribut'], wounded) if e.get('attribut') else e.get('wuerfel'))
        if not letters: continue
        ka = int(e.get('genauigkeit', 0) or 0)
        st = V.Stats(letters, ka, 0, True, None)
        if st.gate > range_: range_ = st.gate
    stats = []
    pool_gate = [card['reichweite'] if card['reichweite'] >= 1 else 1]
    for idx, (letters, ranged, b) in enumerate(pools):
        st = V.Stats(letters, kw_acc, kw_dmg, ranged, defense)
        if engageAcc:
            st.acc = max(F(0), st.acc + engageAcc)
            st.gate = int(st.acc + F(1, 2))
        stats.append((st, b))
        # M26: own coverage per pool (HeroCards.cs poolGate[i]).
        if idx > 0:
            pool_gate.append(1 if pool_is_melee_item[idx] else (st.gate if st.gate >= 1 else 1))
    total = F(0)
    for d in range(1, range_ + 1):
        best = F(0)
        for idx, (st, b) in enumerate(stats):
            if idx == 0 and base_replaced: continue
            if d > pool_gate[idx]: continue
            v = st.ev(d, pierce + engagePierce) + b
            if v > best: best = v
        total += best
    bav = total / range_
    # --- M21 (a): flat blast increment on the damage operand -------------
    bav += EXPLOSION_SHARE * blast_wert(card, skills)
    # --- M25: printed extra-attack/Verstaerkung flat, SAME seam as the
    #     blast (real damage, never percentage-bent twice) ----------------
    bav += flat_bonus(card, skills, wounded)
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

# ------------------------------------------------- M25 print-law anchors
LOKU = {'hero': 'Loku Kanoloa'}
GIDEON = {'hero': 'Gideon Argus'}
g_base = threat21(GIDEON, False, None, 'eliminate', None)
anchor('GIDEON elim +gideon07 = base + 1/2 (Extra-Angriff, je 1/2)',
       threat21(GIDEON, False, {'skills': ['gideon07']}, 'eliminate', None) - g_base, '1/2')
anchor('GIDEON elim +gideon07+gideon08 = base + 1',
       threat21(GIDEON, False, {'skills': ['gideon07', 'gideon08']}, 'eliminate', None) - g_base, '1')
def anchor_near(desc, got, want, tol=1):
    # quantized stall percent law: a flat +1/2 damage addend moves the
    # percentage by 100 x (3/5) x (1/2) / (8/3) = 11.25, +-1 by quantize
    global fails
    ok = abs(F(got) - F(want)) <= tol
    if not ok: fails += 1
    print(('OK  ' if ok else 'FAIL') + f' {desc}: port {got} ~ {want}')
anchor_near('GIDEON stall +gideon07: Pauschale wird auf 60%% gebeugt (11.25)',
       threat21(GIDEON, False, {'skills': ['gideon07']}, 'stall', None)
       - threat21(GIDEON, False, None, 'stall', None), '45/4', 1)
anchor('GIDEON elim gideon03 (Negative Control, kein Extra-Angriff)',
       threat21(GIDEON, False, {'skills': ['gideon03']}, 'eliminate', None) - g_base, '0')
l_h = threat21(LOKU, False, None, 'eliminate', None)
l_w = threat21(LOKU, True, None, 'eliminate', None)
anchor('LOKU elim gesund = verwundet + 5/6 (1/2 Verstarkung + 1/3 Zielgenauigkeit +2)', l_h - l_w, '5/6')
anchor_near('LOKU stall: Verstarkung-Flat gebeugt (11.25, Quantize-Toleranz)',
       threat21(LOKU, False, None, 'stall', None) - (
       int(F(100) * (F(3,5) * (l_h - F(1,2)) / F(8,3) + F(2,5) * 3 / 3) + F(1,2))), '45/4', 1)
anchor('LOKU elim loku03 Negative Control (kein Pin, kein Print-Effekt)',
       threat21(LOKU, False, {'skills': ['loku03']}, 'eliminate', None) - l_h, '0')
anchor('LOKU Zielgenauigkeit gesund (Print +2 Faehigkeit, Waffe +1)', hero_acc(CARDS['Loku Kanoloa'], [], False), '3')
anchor('LOKU Zielgenauigkeit verwundet (Waffen-Print bleibt)', hero_acc(CARDS['Loku Kanoloa'], [], True), '1')
anchor('LOKU Zielgenauigkeit +loku08 Pin', hero_acc(CARDS['Loku Kanoloa'], ['loku08'], True), '2')
anchor('SASKA Zielgenauigkeit (Waffen-Print +1)', hero_acc(CARDS['Saska Teft'], [], False), '1')
anchor('CT-1701 Zielgenauigkeit (CT-1701-Waffe +2)', hero_acc(CARDS['CT-1701'], [], True), '2')
anchor('FENN Zielgenauigkeit 0 (kein Print)', hero_acc(CARDS['Fenn Signis'], [], False), '0')
anchor('SASKA elim gegen weiss = 1/2 (gedrucktes +1 Ziel, Kanon-Anker)',
       threat21({'hero': 'Saska Teft'}, False, None, 'eliminate', ['White']), '1/2')
anchor('SASKA ohne Waffen-Print waere 1/6 (Drift erklaert)',
       threat21({'hero': 'Saska Teft'}, False, None, 'eliminate', ['White'], {'acc_delta': -1}), '1/6')
anchor('UNBEKANNTE-ID-Gesetz: nix01 match nie (Gideon)',
       threat21(GIDEON, False, {'skills': ['nix01']}, 'eliminate', None) - g_base, '0')
anchor('GAARKHAN stall +gaarkhan08 gesund = 59 (Band 3, Kanon-Fall)',
       threat21({'hero': 'Gaarkhan'}, False, {'skills': ['gaarkhan08']}, 'stall', None), '59')
anchor('GAARKHAN stall +gaarkhan08 verwundet = 45 (Band 2, Kanon-Fall)',
       threat21({'hero': 'Gaarkhan'}, True, {'skills': ['gaarkhan08']}, 'stall', None), '45')
anchor('LOKU elim gesund = 8/3 (Kanon-Fall)', threat21(LOKU, False, None, 'eliminate', None), '8/3')
anchor('LOKU elim verwundet = 11/6 (Kanon-Fall)', threat21(LOKU, True, None, 'eliminate', None), '11/6')
anchor('SASKA elim verwundet gegen weiss = 1/2 (Waffen-Print reitet die Wunde)',
       threat21({'hero': 'Saska Teft'}, True, None, 'eliminate', ['White']), '1/2')

for nm, sid in (('GAARKHAN gaarkhan08', 'gaarkhan08'), ('MHD mhd04', 'mhd04'),
                ('VINTO vinto02', 'vinto02'), ('JARROD jarrod06', 'jarrod06'),
                ('KOTUN kotun06', 'kotun06'), ('CT ct05', 'ct05')):
    prof = {'GAARKHAN gaarkhan08': 'Gaarkhan', 'MHD mhd04': 'MHD-19',
            'VINTO vinto02': 'Vinto Hreeda', 'JARROD jarrod06': 'Jarrod Kelvin',
            'KOTUN kotun06': 'Ko-Tun Feralo', 'CT ct05': 'CT-1701'}[nm]
    anchor(nm + ' Tempo-Pin delta', tempo_delta(CARDS[prof], [sid]), '1')
anchor('FENN fenn01 auf GAARKHAN fremder Pin = 0', tempo_delta(CARDS['Gaarkhan'], ['fenn01']), '0')

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
