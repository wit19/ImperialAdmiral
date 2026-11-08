#!/usr/bin/env python3
# P38 (card t_11ebb843): append the three apPrey* keys after the
# apDutyQuotaLine line in every Languages/<L>/ui.json (append-only line
# surgery, +3/0; P37d law). De = authentic German with umlauts; the other
# ten carry the byte-exact EN copy (P37d pattern).
import io, os, sys
root='/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2/Assets/Resources/Languages'
EN_QUOTA = ('    "apPreyQuotaLine": "<i><color=#9FC6FF>Prey hunt: {0} of {1} '
            'units chase the neutral object this round (seeded draw)</color></i>",')
EN_THREAT = ('    "apPreyThreatLine": "<i><color=#9FC6FF>Threat in section {0}'
             ' - the neutral object is not chased</color></i>",')
EN_THREAT_REL = ('    "apPreyThreatRel": "<i><color=#9FC6FF>Threat in the same '
                 'section - the neutral object is not chased</color></i>",')
DE_QUOTA = ('    "apPreyQuotaLine": "<i><color=#9FC6FF>Objektjagd: {0} von {1} '
            'Einheiten jagen diese Runde das neutrale Objekt '
            '(seed-stabiler W\xfcrfel)</color></i>",')
DE_THREAT = ('    "apPreyThreatLine": "<i><color=#9FC6FF>Bedrohung in Sektion {0} '
             '- das neutrale Objekt wird nicht '
             'verfolgt</color></i>",')
DE_THREAT_REL = ('    "apPreyThreatRel": "<i><color=#9FC6FF>Bedrohung in derselben '
                 'Sektion - das neutrale Objekt wird nicht '
                 'verfolgt</color></i>",')
for lang in ['Br','De','En','Es','Fr','Hu','It','Nl','No','Pl','Ru']:
    p=os.path.join(root,lang,'ui.json')
    with io.open(p, encoding='utf-8') as f: lines=f.read().split('\n')
    if any('apPreyQuotaLine' in l for l in lines):
        print(lang, 'already has apPrey keys - SKIP'); continue
    idx=[i for i,l in enumerate(lines) if '"apDutyQuotaLine"' in l]
    if len(idx)!=1: print(lang,'ANCHOR FAIL',idx); sys.exit(1)
    i=idx[0]
    quota, thr, rel = (DE_QUOTA,DE_THREAT,DE_THREAT_REL) if lang=='De' else (EN_QUOTA,EN_THREAT,EN_THREAT_REL)
    lines[i+1:i+1]=[quota,thr,rel]
    with io.open(p,'w',encoding='utf-8',newline='') as f: f.write('\n'.join(lines))
    print(lang,'+3 lines after', i+1)
print('done')
