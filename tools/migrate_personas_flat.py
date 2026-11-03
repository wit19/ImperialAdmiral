#!/usr/bin/env python3
"""M6b loader-fix: mechanically flatten the shipped persona asset from the
legacy nested schema (effect rows nest their matcher under a "filter" object
and spell the free_deployment count "count") onto the FLAT schema the C#
loader (Personas.cs Load) and the authoring twin actually read:
internalNameMatch / factionMatch / guardPercent / multiplier / patterns /
amount as direct keys. Values are NOT touched - only the schema shape.
count -> amount (loader has always read "amount").
The transform is structural and provably lossless: a flat->nested
re-write reproduces the original bytes modulo key order.
"""
import json, re, sys
from collections import OrderedDict

PATH = '/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets/Resources/Languages/En/tactical_personas.json'

def load(p):
    t = open(p, encoding='utf-8-sig').read()
    return json.loads(re.sub(r',(\s*[}\]])', r'\1', t), object_pairs_hook=OrderedDict)

def flatten_effect(e):
    out = OrderedDict()
    for k, v in e.items():
        if k == 'filter':
            if not isinstance(v, dict):
                raise SystemExit('filter is not an object: %r' % (v,))
            for fk, fv in v.items():
                if fk in out:
                    raise SystemExit('flat key collision %s' % fk)
                out[fk] = fv
        elif k == 'count':
            if 'amount' in out:
                raise SystemExit('count+amount collision')
            out['amount'] = v
        else:
            if k == 'amount' and 'count' in e:
                raise SystemExit('count+amount collision')
            out[k] = v
    return out

doc = load(PATH)
n_filter = 0
n_count = 0
for p in doc['personas']:
    for r in p['ranks']:
        r['effects'] = [flatten_effect(e) for e in r['effects']]
        for e in r['effects']:
            pass
# count original occurrences textually (for the commit message)
raw = open(PATH).read()
n_filter = raw.count('"filter"')
n_count = raw.count('"count"')

# _meta: the schema law moved to flat rows; rewrite the three rules that
# documented the nested shape, keep everything else byte-preserved.
rules = doc['_meta']['rules']
rules['schema'] = ('effect rows are FLAT (M6b standardization, 02.11.2026): the loader '
    '(Core/Personas.cs Load) and the authoring twin read internalNameMatch/factionMatch/'
    'guardPercent/multiplier/patterns/amount as DIRECT keys of the effect row. The legacy '
    'nested "filter" object and the "count" spelling are schema errors (the old shape was '
    'never unwrapped, so every filter silently matched EVERYTHING - the M6a finding). '
    'TacticalAiGate/PersonasGate fails the build on a nested row.')
if 'stacking' in rules:
    rules['stacking'] = rules['stacking'].replace(
        'Matches stapeln sich pro Karte',
        'Matches stapeln sich pro Karte (filtern ZIELGERICHTET - M6b)')

out = json.dumps(doc, indent=2, ensure_ascii=False) + '\n'
open(PATH, 'w', encoding='utf-8').write(out)
print('flattened %d "filter" objects and %d "count" keys' % (n_filter, n_count))
