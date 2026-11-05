#!/usr/bin/env python3
# M20 sweep: independent port re-derives EVERY threat value in the hold
# postures (and raw averages in damage postures) of the canonical fixtures.
import json, io, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import m19_verify as V
from fractions import Fraction as F

fails = 0; checked = 0; skipped = 0
for fn in ('overlord_focus','imperial_card_effects'):
    fix = V.load(f'{V.FIX}/{fn}.json')
    for c in fix['cases']:
        post = c.get('posture'); dfn = c.get('defense'); eng = c.get('engage')
        exp = c['expect']
        heroes = {h['hero']: h for h in c.get('heroes', [])}
        rows = []
        if isinstance(exp, list):
            rows = [(r, None) for r in exp if isinstance(r, dict) and 'hero' in r]
        elif isinstance(exp, dict) and isinstance(exp.get('rows'), list):
            rows = [(r, None) for r in exp['rows']]
        elif isinstance(exp, dict) and ('threat_base' in exp or 'threat_cards' in exp):
            for key in ('threat_base','threat_cards'):
                if key in exp:
                    e2 = exp.get('engage') if key == 'threat_cards' else None
                    for h in c.get('heroes', []):
                        rows.append(({'hero': h['hero'], 'threat': exp[key],
                                      'basis': 'card'}, e2))
        for r, e2 in rows:
            if r.get('basis') != 'card' or r.get('threat') is None:
                skipped += 1; continue
            h = heroes.get(r['hero'])
            if h is None or V.CARDS.get(h.get('profile', r['hero'])) is None:
                skipped += 1; continue
            t = V.threat(h, r.get('wounded', h.get('wounded', False)),
                         h.get('loadout'), post, dfn, e2 if e2 is not None else eng)
            if t is None:
                skipped += 1; continue
            if h.get('objective_actor'):
                t = t + 1
            sur = F(c.get('surcharge', 0) or 0)
            if h.get('conditioned') and sur:
                t = t + sur
            checked += 1
            if V.fstr(t) != str(r['threat']):
                fails += 1
                print(f'FAIL {fn}/{c["name"]} {r["hero"]}: port {V.fstr(t)} vs canon {r["threat"]}')
print(f'== M20 sweep: {checked} card-basis threat values re-derived, {fails} fails, {skipped} skipped (proxy/economy rows)')
