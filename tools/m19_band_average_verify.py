#!/usr/bin/env python3
# Independent Python port of the M19 band-average threat law.
# Verifies every drifted vector row of overlord_focus.json and
# imperial_card_effects.json against the harness --record output.
# This is the M16-flow Handrechnungs-Pflicht, done as a second
# implementation (not the C# code under test).
import json, io, sys, glob, os
from fractions import Fraction as F

REPO = '/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets'
FIX  = REPO + '/Tests/Fixtures/tacai'
LANG = REPO + '/Resources/Languages/En'

def load(p):
    with io.open(p, encoding='utf-8-sig') as f: return json.load(f)

# --- dice faces (dmg, acc) per side; surge irrelevant (threat passes none)
FACES = {
 'Green':  [(1,2),(1,2),(2,1),(2,2),(2,2),(0,1)],
 'Blue':   [(2,2),(2,2),(1,3),(0,2),(0,4),(0,6)],
 'Yellow': [(2,0),(1,1),(1,1),(0,2),(0,1),(0,0)],
 'Red':    [(0,0),(2,0),(2,0),(2,0),(3,0),(3,0)],
 'Grey':   [(2,2),(2,2),(1,3),(0,2),(0,4),(0,6)],
}
DEF = {'Black': [0,0,1,2,2,3], 'White': [0,0,1,1,2,2]}  # white dodge face(5)=2
def blocks_ev(dice):
    t=0
    for d in dice:
        faces=DEF[d]
        if d=='White':
            faces=list(faces); faces[5]=2
        t+=sum(faces)
    return F(t, 6*len(dice)) if dice else F(0)

CHAR = {'G':'Green','B':'Blue','Y':'Yellow','R':'Red'}

class Stats:
    def __init__(self, letters, kw_acc, kw_dmg, ranged, defdice):
        core = letters.split('+')[0]   # engine BasisWuerfel: '+Reach' suffix stripped
        cols=[CHAR[c] for c in core if c in CHAR]
        self.raw = sum(F(sum(f[0] for f in FACES[c]),6) for c in cols) + kw_dmg
        self.acc = sum(F(sum(f[1] for f in FACES[c]),6) for c in cols) + kw_acc
        self.ranged = ranged
        self.gate = int((self.acc + F(1,2)))
        self.blocks = blocks_ev(defdice) if defdice is not None else F(0)
    def ev(self, d, pierce):
        accok = (not self.ranged) or self.gate >= d
        avail = self.acc - (d if self.ranged else 0)
        valid = min(self.raw, max(F(0), avail))
        blocks = self.blocks if accok else F(0)
        if pierce > 0: blocks = max(F(0), blocks - pierce)
        return max(F(0), valid - blocks)

def frac(s):
    if s is None: return None
    if isinstance(s,int): return F(s)
    s=str(s);
    return F(s) if '/' in s else F(int(s))

# --- assets
cards_doc = load(LANG + '/tactical_hero_cards.json')
CARDS = {h['name']: h for h in cards_doc['heroes']}
ITEMS = {i['id']: i for i in cards_doc['items']}
prof_doc = load(LANG + '/tactical_hero_profiles.json')
evm = prof_doc['ev_model']
WEP_K = frac(evm['weapon_ev_per_1000_cost'])
TYPE_TABLE = prof_doc.get('type_table', {})
camp = load(LANG + '/CampaignData/items.json')
CAMP = {}
for it in (camp if isinstance(camp,list) else camp.get('items',[])):
    CAMP[it['id']] = it

def weapon_bonus(iid):
    ci = CAMP.get(iid)
    if not ci: return F(0)
    cls = TYPE_TABLE.get(ci.get('type'))
    if not cls or not cls.startswith('weapon'): return F(0)
    return WEP_K * (int(ci.get('tier',1)) * int(ci.get('cost',0)))

def attr_pool(card, attr, wounded):
    key = 'attributeVerwundet' if wounded else 'attribute'
    letters = (card.get(key) or {}).get(attr)
    return letters

def threat(hero, wounded, loadout, posture, defense, engage=None):
    card = CARDS.get(hero['profile'] if 'profile' in hero else hero['hero'])
    if card is None: return None   # sim/proxy row - not card-based
    items = (loadout or {}).get('items', [])
    kw_acc = sum(int(ITEMS[i].get('genauigkeit',0) or 0) for i in items if i in ITEMS)
    kw_dmg = sum(int(ITEMS[i].get('schaden',0) or 0) for i in items if i in ITEMS)
    pierce = sum(int(ITEMS[i].get('durchschlag',0) or 0) for i in items if i in ITEMS)
    engagePierce = (engage or {}).get('pierce',0)
    engageAcc = (engage or {}).get('acc_delta',0)
    pools = [ (card['basisAngriffswuerfel'], card['angriffsTyp'] != 'P', F(0)) ]
    for iid in items:
        e = ITEMS.get(iid)
        if e is None: continue
        ranged = e['typ'] == 'O'
        b = weapon_bonus(iid)
        if e.get('wuerfel'): pools.append((e['wuerfel'], ranged, b))
        if e.get('attribut'):
            ap = attr_pool(card, e['attribut'], wounded)
            if ap: pools.append((ap, ranged, b))
    # reach
    range_ = card['reichweite']
    for iid in items:
        e = ITEMS.get(iid)
        if e is None or e['typ'] != 'O': continue
        letters = e.get('wuerfel') or attr_pool(card, e.get('attribut'), wounded) if e.get('attribut') else e.get('wuerfel')
        if not letters: continue
        ka = int(e.get('genauigkeit',0) or 0)
        st = Stats(letters, ka, 0, True, None)
        if st.gate > range_: range_ = st.gate
    stats=[]
    for letters, ranged, b in pools:
        st = Stats(letters, kw_acc, kw_dmg, ranged, defense)
        if engageAcc:
            st.acc = max(F(0), st.acc + engageAcc)
            st.gate = int(st.acc + F(1,2))
        stats.append((st,b))
    total=F(0)
    for d in range(1, range_+1):
        best=F(0)
        for st,b in stats:
            v = st.ev(d, pierce + engagePierce) + b
            if v > best: best = v
        total += best
    bav = total / range_
    if posture in ('eliminate','attrition'): return bav
    band_t = card['tempoVerwundet'] if wounded else card['tempo']
    band = 1 if band_t <= 3 else 2 if band_t == 4 else 3
    # M20 law (Thomas ruling 05.11): range cell stricken, 60/40 weights
    th = 100 * (F(3,5)*bav/F(8,3) + F(2,5)*band/3)
    return int(th + F(1,2))   # round-half-up

def fstr(x):
    if x is None: return None
    if isinstance(x,int): return str(x)
    return str(x.numerator) if x.denominator==1 else f"{x.numerator}/{x.denominator}"

# --- verify drifted rows
fails = 0; checked = 0
for fn in ('overlord_focus','imperial_card_effects'):
    if not os.path.exists(f'{FIX}/{fn}_record.json'): continue
    fix = load(f'{FIX}/{fn}.json'); rec = load(f'{FIX}/{fn}_record.json')
    for c in fix['cases']:
        name=c['name']
        if name not in rec: continue
        if json.dumps(c['expect'],sort_keys=True)==json.dumps(rec[name],sort_keys=True):
            continue
        post=c.get('posture'); defdice=c.get('defense'); engage=c.get('engage')
        exp = rec[name]
        if isinstance(exp, dict) and 'rows' in exp:   # ICE rank cases
            heroes=c.get('heroes',[]); e2=exp.get('engage',engage)
            sur=F(c.get('surcharge',0) or 0)
            for row in exp['rows']:
                h=next((x for x in heroes if x['hero']==row['hero']),None)
                t=threat(h,h.get('wounded',False),h.get('loadout'),post,defdice,e2)
                if h and h.get('conditioned') and sur: t = t + sur
                checked+=1
                got=fstr(t)
                if got!=row['threat']:
                    fails+=1; print(f'FAIL {fn}/{name} {row["hero"]}: port {got} vs rec {row["threat"]}')
        elif isinstance(exp, dict):                    # ICE threat_* cases
            for key in ('threat_base','threat_cards'):
                for variant,eng in ((False,None),(True,exp.get('engage'))):
                    pass
            heroes=c.get('heroes',[])
            for eng_key in ('threat_base','threat_cards'):
                eng=None
                e=exp.get('engage')
                if eng_key=='threat_cards' and e: eng=e
                # threat_base: without card engage; threat_cards: with
                for h in heroes:
                    t=threat(h,h.get('wounded',False),h.get('loadout'),post,defdice,eng)
                    checked+=1
                    got=fstr(t)
                    if got!=exp[eng_key]:
                        fails+=1
                        print(f'FAIL {fn}/{name} {eng_key} {h["hero"]}: port {got} vs rec {exp[eng_key]}')
        else:                                          # rank vectors
            oldmap={r['hero']:r for r in c['expect']}
            for row in exp:
                h=next((x for x in c.get('heroes',[]) if x['hero']==row['hero']),None)
                if row['threat'] is None: continue
                o=oldmap.get(row['hero'])
                if o and o.get('threat')==row['threat'] and o.get('threat_econ')==row.get('threat_econ'):
                    continue   # order-only drift: row values unchanged, not our law
                t=threat(h,row['wounded'],h.get('loadout'),post,defdice,engage)
                if h and h.get('objective_actor'): t = (t if t is not None else F(0)) + 1  # +1 actor rides the printed threat
                checked+=1
                got=fstr(t)
                if got!=row['threat']:
                    fails+=1; print(f'FAIL {fn}/{name} {row["hero"]}: port {got} vs rec {row["threat"]}')
                # econ: delta-invariant check (strain/wound additions ride unchanged)
                if row['threat_econ'] is not None:
                    old=next((r for r in c['expect'] if r['hero']==row['hero']),None)
                    if old and old['threat'] is not None and old['threat_econ'] is not None:
                        add = F(old['threat_econ']) - F(old['threat'])
                        want = F(row['threat']) + add
                        if want != F(row['threat_econ']):
                            fails+=1; print(f'FAIL {fn}/{name} {row["hero"]} econ: {fstr(want)} vs rec {row["threat_econ"]}')
                        checked+=1
print(f'== port verification: {checked} rows checked, {fails} fails')
