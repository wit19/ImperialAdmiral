#!/usr/bin/env python3
"""M6b authoring script for Tests/Fixtures/tacai/personas_swap.json.

Independent Python implementation of the elite_swap law (CONCEPT_personas_
v2 v2.1 Sec.1.1 / Core/PersonaSwaps.cs header): budget = sum(amount) of the
active elite_swap rows; name cap 1/mission/base-name; board blocks a name
whose elite stands; the swap consumes the elite card from hand; charged cost
= the REGULAR's printed cost. Hand-authored mission vectors (draft/board/
hand), expect vectors 'authored, not recorded' - the C# Core is NEVER
executed here; divergence surfaces as a harness FAIL, never a silent
match (same doctrine as tacai_persona_twin.py for personas.json).

The embedded persona rows mirror the M6a2 draft (tools/draft_personas_
v2.json) rank ladders so the section also pins the flat loader end-to-end
(elite_card read from a flat row via Personas.Load()).
"""
import json, re

DRAFT = '/home/open/work/ia-ai/ImperialCommander2/tools/draft_personas_v2.json'
OUT = '/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets/Tests/Fixtures/tacai/personas_swap.json'

# ------------------------------------------------------------ personas ----
draft = json.load(open(DRAFT))
personas = []
for p in draft['personas']:
    ranks = []
    for r in p['ranks']:
        effs = []
        for e in r['effects']:
            row = {'kind': e['kind']}
            if 'amount' in e: row['amount'] = e['amount']
            if 'multiplier' in e: row['multiplier'] = e['multiplier']
            if 'internalNameMatch' in e: row['internalNameMatch'] = e['internalNameMatch']
            if 'factionMatch' in e: row['factionMatch'] = e['factionMatch']
            if 'guardPercent' in e: row['guardPercent'] = e['guardPercent']
            if 'patterns' in e: row['patterns'] = e['patterns']
            if 'key' in e: row['key'] = e['key']
            if 'elite_card' in e: row['elite_card'] = e['elite_card']
            effs.append(row)
        ranks.append({'rank': r['rank'], 'minThreat': r['minThreat'], 'effects': effs})
    personas.append({'id': p['id'], 'nameKey': p['nameKey'],
                     'mottoKey': p['mottoKey'], 'ranks': ranks})

# ------------------------------------------------------------ cards -------
def card(cid, name, faction, cost, size, elite=False):
    return {'id': cid, 'name': name, 'faction': faction,
            'cost': cost, 'size': size, 'elite': 1 if elite else 0}

# CardData/enemies.json roster ground truth (02.11 probe): DG011/DG012 are
# BOTH regular Probe Droids (isElite False) - the RRG double-card case, not
# elite pairs. Elite prints are exactly DG003/DG006/DG008/DG013.

ST, ST2, STE = card('DG001','Stormtrooper','Imperial',6,3), card('DG002','Stormtrooper','Imperial',6,3), card('DG003','Stormtrooper (Elite)','Imperial',8,3,True)
OFF, OFF2, OFFE = card('DG004','Imperial Officer','Imperial',2,1), card('DG005','Imperial Officer','Imperial',2,1), card('DG006','Imperial Officer (Elite)','Imperial',5,1,True)
EWEB, EWEBE = card('DG007','E-Web Engineer','Imperial',5,1), card('DG008','E-Web Engineer (Elite)','Imperial',7,1,True)
PROBE, PROBE2, PROBEEL = card('DG011','Probe Droid','Imperial',3,1), card('DG012','Probe Droid','Imperial',3,1), card('DG013','Probe Droid (Elite)','Imperial',5,1,True)
HG, TH = card('DG063','Hired Gun','Mercenary',4,2), card('DG015','Trandoshan Hunter','Mercenary',7,2)

missions = {
  # full Imperial core draft: the swap rail must pick exactly the persona's
  # own name per mission; mercenary stays untouched.
  'core_mixed':   {'draft':[ST,OFF,PROBE,EWEB,HG],
                   'board':[],
                   'hand':[ST,ST2,STE,OFF,OFFE,PROBE,PROBE2,PROBEEL,EWEB,EWEBE,HG]},
  # two regulars of ONE name + exactly one elite card: name cap AND hand
  # consumption must both bite on the second entry.
  'storm_double': {'draft':[ST,ST2],'board':[],'hand':[ST,ST2,STE]},
  # a Hard-Roll/mission-start elite stands on the board (legacy product,
  # read-only): the persona's same-name swap is blocked defensively;
  # every other swap still runs.
  'elite_on_board':{'draft':[ST,OFF,PROBE,EWEB,HG],'board':[STE],
                    'hand':[ST,ST2,OFF,OFFE,PROBE,PROBE2,PROBEEL,EWEB,EWEBE,HG]},
  # the elite card is not (and never was) in the hand: the swap fails
  # harmlessly - the regular deploys, no crash, no fallback.
  'elite_missing': {'draft':[ST,OFF],'board':[],'hand':[ST,OFF]},
  # mercenary draft + a mercenary elite in hand: no persona row matches,
  # identity everywhere.
  'mercenary_draft':{'draft':[HG,TH],'board':[],'hand':[HG,TH]},
  # an elite print inside the draft: elite entries are never swap
  # candidates; the regular entry of the same mission still swaps.
  'elite_in_draft': {'draft':[STE,OFF],'board':[],'hand':[STE,OFF,OFFE]},
}

# ------------------------------------------------- the rule (python twin) --
def resolve(pid, threat):
    if not pid: return []
    for p in personas:
        if p['id'] != pid: continue
        act = []
        rank = 0
        for r in p['ranks']:
            if r['minThreat'] <= threat:
                act.extend(r['effects']); rank = r['rank']
        return act if rank > 0 and act else []
    return []

def matches(e, name, faction):
    if e.get('factionMatch') is not None and (faction or '') != e['factionMatch']:
        return False
    if e.get('internalNameMatch') is not None:
        try:
            if not re.search(e['internalNameMatch'], name or '', re.IGNORECASE):
                return False
        except re.error:
            return False
    return True

def base_of(name):
    return re.sub(r' \(Elite\)$', '', name, flags=re.IGNORECASE)

def plan(view, m):
    budget = sum(max(1, e.get('amount', 1)) for e in view if e['kind'] == 'elite_swap')
    rowswap = [e for e in view if e['kind'] == 'elite_swap']
    hand_ids = {c['id'] for c in m['hand']}
    board_bases = {base_of(c['name']) for c in m['board'] if c['elite']}
    used_names = set()
    swaps = 0
    out = []
    for c in m['draft']:
        final = c['id']; swapped = False
        if (not c['elite']) and swaps < budget:
            row = next((e for e in rowswap if matches(e, c['name'], c['faction'])), None)
            if row is not None and row.get('elite_card'):
                eid = row['elite_card']; b = base_of(c['name'])
                if b not in used_names and b not in board_bases and eid in hand_ids:
                    final = eid; swapped = True
                    used_names.add(b); hand_ids.discard(eid)
                    board_bases.add(base_of(final))
                    swaps += 1
        out.append((final, swapped))
    return out

# ------------------------------------------------------------ cases -------
cases = []
for pid in ['trooper_captain','feldherr','isb_commander','hutt_lord',
            'droiden_direktor','grossmoff']:
    for t in (2, 4, 6, 8):
        view = resolve(pid, t)
        for mname, m in missions.items():
            res = plan(view, m)
            cases.append({
                'persona': pid, 'threat': t, 'mission': mname,
                'expect_out': [r[0] for r in res],
                'expect_swaps': sum(1 for r in res if r[1]),
            })

# Hand-pinned swap budget per persona at T2/4/6/8: sum(amount) of the active
# elite_swap rows - the rank rows stack additively (Sec.1.1: N swaps per
# mission). grossmoff unlocks at R3/R4, droiden_direktor gains E-Web at R4.
budgets = {
  'trooper_captain':    [1, 1, 1, 1],
  'feldherr':           [1, 1, 1, 1],
  'isb_commander':      [1, 1, 1, 1],
  'hutt_lord':          [0, 0, 0, 0],   # pure behavior channel - no swaps
  'droiden_direktor':   [1, 1, 1, 2],
  'grossmoff':          [0, 0, 1, 2],
}
# cross-check against the twin's own resolve():
for pid, bs in budgets.items():
    for t, want in zip((2, 4, 6, 8), bs):
        got = sum(max(1, e.get('amount', 1)) for e in resolve(pid, t)
                  if e['kind'] == 'elite_swap')
        assert got == want, (pid, t, want, got)

doc = {
 '_comment': ('M6b elite_swap canon - HAND-AUTHORED (same documented route '
   'as action_tree.json / optional_deploy.json: no --record, the C# Core is '
   'never executed while authoring; the expect vectors were derived by an '
   'independent Python re-implementation of the Sec.1.1 law - this very '
   'script, tools/gen_personas_swap_fixture.py - and hand-verified against the concept '
   'tables). Canon law (CONCEPT_personas_v2 v2.1 Sec.1.1/Sec.5-2c): every '
   'persona x test mission x T2/4/6/8 vector pins (a) the Delta-0 parity - '
   'figure count AND booked cost of the persona plan equal the baseline '
   '(persona=none) plan EXACTLY, checked by the harness against a second '
   'plan run, never trusted from the expect alone; (b) double-elite '
   'non-existence - never two models of one figure name with an elite print '
   'among them; (c) the swap proof - the plan carries the elite card-set '
   'identity (DG003/DG006/DG013/DG008) wherever the persona is entitled to '
   'swap, including stacked budgets >1 across rank rows (droiden_direktor/ '
   'grossmoff at T8). swap_personas mirrors the M6a2 draft rank ladders so '
   'the flat loader schema (elite_card) is exercised end-to-end; the LEGACY '
   'elite rails are read-only inputs (elite_on_board), never modified '
   '(Sec.1.2-2, Thomas 02.11).'),
 'swap_personas': personas,
 'budgets': budgets,
 'missions': {k: {'draft': m['draft'], 'board': m['board'], 'hand': m['hand']}
              for k, m in missions.items()},
 'cases': cases,
}
open(OUT, 'w').write(json.dumps(doc, indent=1, ensure_ascii=False) + '\n')

# --------------------------------------------------- report for the hand --
print('cases:', len(cases))
import collections
by = collections.Counter()
for c in cases:
    if c['expect_swaps']:
        by[(c['persona'], c['threat'])] += 1
for k, v in sorted(by.items()):
    print('swapping vectors', k, v)
print('identity-only cases:', sum(1 for c in cases if not c['expect_swaps']))
