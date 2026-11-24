#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Export der Doktrin-/Popup-Texte je Doktrin zur T.-Kontrolle (P46-Hop)."""
import json

R = 'ImperialCommander2/Assets/Resources/'
de = json.load(open(R + 'Languages/De/ui.json', encoding='utf-8-sig'))['uiTacticalAI']
en = json.load(open(R + 'Languages/En/ui.json', encoding='utf-8-sig'))['uiTacticalAI']
w = json.load(open(R + 'Languages/En/tactical_pattern_weights.json', encoding='utf-8-sig'))
import re as _re
def loose(p):
    return json.loads(_re.sub(r',\s*([}\]])', r'\1', open(p, encoding='utf-8-sig').read()))
deen = loose(R + 'CardData/enemies.json')
den = loose(R + 'Languages/De/DeploymentGroups/enemies.json')

def names(lst):
    m = {}
    for c in lst:
        cid = c.get('id') or c.get('groupID') or c.get('guid') or ''
        nm = c.get('name') or c.get('groupName') or ''
        if cid: m[cid] = nm
    return m
N_en = names(deen if isinstance(deen, list) else deen.get('groups', []))
N_de = names(den if isinstance(den, list) else den.get('groups', []))

DOCS = [('aggressive', 'docAggressive', 'apTreeAggressive', 'Stürmer'),
        ('guard', 'docGuard', 'apTreeGuard', 'Wächter (Wachschutz)'),
        ('blocker', 'docBlocker', 'apTreeBlocker', 'Blocker'),
        ('hunter', 'docHunter', 'apTreeHunter', 'Jäger'),
        ('support', 'docSupport', 'apTreeSupport', 'Unterstützung'),
        ('ambush', 'docAmbusher', 'apTreeAmbush', 'Hinterhalt')]

SPEC = {
 'aggressive': ['ordExplainNahe','ordExplainNaheRel','ordFocusReaktion','ordFocusBasisReaktion'],
 'guard': ['ordExplainWachzone','ordExplainWachzoneRel','ordFocusWachzone','ordFocusBasisWachzone','ordRiskGuardHold','ordInfoGuardZone'],
 'blocker': ['ordRiskGuardHold','ordExplainThreeC','ordExplainMassiveSeal'],
 'hunter': ['ordFocusFokuszone','ordExplainFokuszone'],
 'support': ['ordExplainSupportMaske','ordExplainSupportHalten','ordFocusMaskenvertrauen'],
 'ambush': [],
}
USED = set()

def esc(t): return t.replace('\n', '<br>\n> ')

def block(k, used=True):
    if k not in de: return
    if used: USED.add(k)
    print('**`%s`**  \n**DE**  \n> %s\n\n**EN**  \n> %s\n' % (k, esc(de[k]), esc(en.get(k, '—'))))

def label(k, base):
    if k == base: return 'Normale kaiserliche Figur'
    s = k[len('apTree'):]
    if 'OfficerEliteKeep' in k: return 'Offizier-ELITE, Ziel-Halte-Variante'
    if 'OfficerElite' in k: return 'Offizier-ELITE'
    if 'OfficerKeep' in k: return 'Offizier, Ziel-Halte-Variante'
    if 'Officer' in k: return 'Offizier'
    return '—'

out = []
import sys
so = sys.stdout
sys.stdout = None

def A(s): out.append(s)

A('# Doktrin-Texte der kaiserlichen KI — vollständiger Export (21.11.2026)')
A('')
A('*Zur Kontrolle nach T.-Auftrag. Quelle: `Resources/Languages/De/ui.json` Abschnitt `uiTacticalAI` (Deutsch), Original `Languages/En/ui.json`. Neu: Popup-Reihenfolge DOKTRIN zuerst, dann Primärziel; Begriff „Primärziel“ statt „Fokus-Ziel“ (Regel 21.11.). Alle Platzhalter in geschweiften Klammern füllt die Laufzeit (Zielname, Zonenname, Zahl). Die Entscheidungsbäume erscheinen bei ERKLÄRUNG = VOLL; Doktrin- und Primärziel-Zeile immer.*')
A('')
A('---')
A('')
A('## 0. Die dauerhaften Popup-Zeilen bei jeder Aktivierung (neue Reihenfolge)')
A('')
A('**Zeile 1 — Doktrin** (`doctrineHeader`, der Name trägt seine Erklärung in sich):')
A('')
A('> ' + de['doctrineHeader'].replace('{0}', '〈Doktrin-Name〉'))
A('')
A('*EN: ' + en['doctrineHeader'].replace('{0}', '〈name〉') + '*')
A('')
A('Die sechs Doktrin-Namen mit ihrer eingebauten Erklärung:')
A('')
for _, dk, _, _dn in DOCS:
    A('- **' + de[dk] + '**')
    A('  *EN:* ' + en[dk])
    USED.add(dk)
A('')
A('**Zeile 2 — Primärziel** (umbenannt von „Fokus-Ziel“):')
A('')
block('focusPrefix')
A('Zusatz-Zusätze an der Zielzeile (erscheinen je nach Tatsachenlage hintereiander):')
A('')
for k in ['focusWounded', 'focusFirepower', 'ordMarked', 'ordFocusReaktion', 'ordFocusWachzone', 'ordFocusFokuszone', 'ordFocusVergeltung', 'ordFocusMaskenvertrauen']:
    block(k)
A('**Zeile 3 — die Handlungsblöcke** (aus der gedruckten Befehlskarte, lokalisiert):')
A('')
for k in ['apHead', 'apHeadAssault', 'apRule', 'ordInfoAttack', 'ordInfoDoor', 'ordInfoFlip', 'ordInfoGuardZone', 'ordInfoMassiveInterior', 'ordInfoRule', 'interactLine', 'specialAsPrinted', 'restLine', 'ordOptionPrinted', 'ordAbilitiesLane']:
    block(k)
A('**Duty-/Beute-Zeilen** (Pflichtkontingent, Muster-Cohort):')
A('')
for k in ['apDutyEscortTree', 'apDutyEscortTreeNameless', 'apDutyQuotaLine', 'overlordFallback']:
    block(k)
A('---')
A('')
A('## 1. Je Doktrin: der vollständige Entscheidungsbaum-Text')
A('')
for patid, dk, base, dn in DOCS:
    A('### ' + dn + ' — Muster `' + patid + '`')
    A('')
    A('Doktrin-Zeile (Name+Erklärung): **' + de[dk] + '**')
    A('')
    ks = [k for k in de if k.startswith('apTree') and (k == base or (k.endswith(base.replace('apTree', '')) and 'Officer' in k) or (base == 'apTreeAmbush' and k.endswith('Ambush') and 'Officer' in k))]
    order = [base]
    for k in [base.replace('apTree', 'apTreeOfficer'), base.replace('apTree', 'apTreeOfficerElite'),
              base.replace('apTree', 'apTreeOfficerKeep'), base.replace('apTree', 'apTreeOfficerEliteKeep')]:
        if k in de and k not in order: order.append(k)
    for k in order:
        if k not in de: continue
        USED.add(k)
        A('#### ' + label(k, base) + '  \n`' + k + '`')
        A('')
        A('**DE**  \n> ' + esc(de[k]))
        A('')
        A('**EN**  \n> ' + esc(en.get(k, '—')))
        A('')
    if SPEC.get(patid):
        A('#### Zusätzliche Zeilen, die bei dieser Doktrin dazukommen können')
        A('')
        for k in SPEC[patid]:
            block(k)
    A('')
A('---')
A('')
A('## 2. Gemeinsame Baum-Texte (doktrinunabhängige Sonderfälle)')
A('')
for k in ['apTreeTripod', 'apTreeNoTarget', 'apTreeOfficerNotfall']:
    block(k)
A('---')
A('')
A('## 3. Risiko-, Phasen- und Bedrohungszeilen (EXPLAIN = VOLL)')
A('')
for k in ['ordRiskLabel', 'ordRiskPress', 'ordRiskHold', 'ordRiskGuardHold', 'ordRiskDisengage',
          'ordPatternRoll', 'ordPhaseShiftQM', 'ordPhaseExplain', 'ordPhaseDefend', 'ordPhaseHunt',
          'ordExplainHead', 'ordExplainFocusWhy', 'ordFocusDetail', 'ordFocusMeasureDamage',
          'ordFocusMeasureRunner', 'ordThreat', 'ordThreatCalcRunner', 'ordThreatEnergy',
          'ordThreatExplosion', 'ordThreatBonus', 'ordStrainPressure', 'ordStrainFuzzy',
          'ordTargetObjekt', 'ordExplainObjekt', 'ordPolarJagt', 'ordPolarSchuetzt', 'ordPolarNeutral',
          'ordExplainVergeltung', 'ordExplainClamp', 'ordExplainWachzoneRel']:
    block(k)
A('### Entscheidungs-Begründungen (die „basis“-Zeilen der Primärziel-Wahl)')
A('')
for k in sorted(de):
    if k.startswith('ordFocusBasis'):
        block(k)
A('---')
A('')
A('## 4. Rest des KI-Textbestands (Vollständigkeit)')
A('')
for k in sorted(de):
    if k in USED or k.startswith('persona') or k.startswith('set') or k.startswith('skill') or k.startswith('badge'):
        continue
    block(k)
A('---')
A('')
A('## 5. Wer würfelt welche Doktrin? (Kuratierte Würfel-Gewichte)')
A('')
A('*Die Doktrin wird je Aktivierung gewürfelt; die Gewichte stehen pro Gruppe fest (`tactical_pattern_weights.json`, öffentliche Datei). Gelistete Gruppen haben eine kuratierte Verteilung; alle übrigen Pool-Figuren laufen über den alternden Legacy-Zweig (Rollen-Klassifikation nach Haltung) — derselbe Text, andere Würfel. Haltungs-Modifikatoren unten.Additive Verschiebungen können Muster sogar neu öffnen (defend + Wache).*')
A('')
for r in w['rows']:
    ids = r.get('ids') or ([r['id']] if r.get('id') else [])
    for gid in ids:
        A('- **%s** (%s) — %s' % (N_de.get(gid, 'Erweiterungs-Pool (DLC — Name im Kern-Repo nicht geführt)'), gid,
          ', '.join('%s %d%%' % (k, v) for k, v in sorted(r['weights'].items(), key=lambda x: -x[1]))))
    if r.get('name'):
        A('- **Name „%s“** — %s' % (r['name'], ', '.join('%s %d%%' % (k, v) for k, v in sorted(r['weights'].items(), key=lambda x: -x[1]))))
A('')
A('*Haltungs-Modifikatoren (additiv, Klammerung bei 0):*')
A('')
for post, mods in w['postureModifiers'].items():
    A('- ' + post + ': ' + ', '.join('%s %+d' % (k, v) for k, v in sorted(mods.items())))
A('')
open('docs/EXPORT_doktrin_texte_2026-11-21.md', 'w').write('\n'.join(out))
sys.stdout = so
print('written', sum(len(x) for x in out), 'chars')
