#!/usr/bin/env python3
# P38 (card t_11ebb843): assemble docs/CENSUS_beute_objekte.md - hand-written
# decision text + the machine table extracted from a `gate -- census` run.
# Re-run after law changes:  gate -- census > /tmp/census.log && this script.
import re, io

log = open('/tmp/p38_gate_census.log', encoding='utf-8').read()
m = re.search(r'== CENSUS TABLE[^\n]*\n(.*?)\n== rows: (\d+) ==', log, re.S)
assert m, 'census table not found in log'
rows = [l.strip() for l in m.group(1).strip().split('\n') if l.startswith('|')]
total = int(m.group(2))
assert len(rows) == total, (len(rows), total)

header = """# Zensus: NEUTRALE Beute-Objekte (P38)

Karte: t_11ebb843 · Playtest-Grund: Empire3 — alle imperialen Figuren hatten
Ziel „Demonstrant" (OBJEKT-Ziel ohne Mengenbegrenzung, Kanonenfutter-Effekt).
Dieses Dokument ist der pflichtige Zensus zu Card-§Scope-Sperre: Durchlauf
durch die ECHTE Polaritäts-Ableitung (MissionFacts-Code: Producer-Grammatik
TryBuildObjectEvents gespiegelt + Core-WorldEventVector-Leiter P1–P4 +
Core NeutralPrey) ueber ALLE ausgelieferten Missionen. Verrechnet wird er
fail-closed vom NeutralPreyGate (Gate-Lauf, Default-Modus `verify`): jede
CENSUS-Zeile muss zur Live-Ableitung passen; Geister und fehlende Zeilen
sind FAIL. Tabelle erzeugen: `dotnet run --project tools/TacticalAiGate/gate.csproj -- census`.

## Entschlossene INTENSION (Bystander-Kriterium)

Ein Feed-Objekt ist ein NEUTRALES BYSTANDER-Objekt, wenn seine oeffentliche
Interaktions-Verdrahtung PERSON-Vokabel traegt: Die Knopftexte / verdrahteten
Event-Namen enthalten einen Personen-Stamm — `claim`/besetz/Anspruch,
`rescue`/rette/befrei, `defeat`/besieg/vernicht, `reveal`/enthuell
(Core/NeutralPrey.cs; dieselbe EN + verfasste-DE-Zwei-Vokabular-Disziplin wie
die Leiter P34-A1/A2). Gedruckte Personen-Semantik bestaetigt: Personen wird
zugesprochen, man rettet, besiegt und enthuellt sie; GERAETE werden
ZERSTOERT — und das ausgelieferte Korpus haelt die Law 47/47 auf allen
Token-Zeilen exakt. Das Anwendungsfeld (Card §Scope-Sperre) ist die
Schnittmenge: POLAR-Beutezeile (polaritaet `jagt`) UND entityType `token`
UND Bystander.

## Pruengende Merkmals-Messung (warum die Alternativen verwerfen)

| Merkmal | Messung | Urteil |
|---|---|---|
| entityType (4 = Token) | Protester und Barricade sind alike Token; Tueren=Door, Terminals=Terminal, Warship=Highlight | harte Typ-Schiene: laesst Tueren/Terminals/Warship NIE durch (Card-Befund bestaetigt) |
| markerType | Neutral auf BEIDEN Seiten (Protester Neutral wie Barricade/Siege Wall) | trennt nicht — verworfen |
| tokenColor/entityColor | Gray auf beiden Seiten; Checkpoint Green ist ein Geraet in Person-Farbe | trennt nicht — verworfen |
| Fraktion im Angriffssatz | „A figure can attack" auch auf Security Station (OTHER37, Geraet); „Imperial figures can attack refugees" (Refugee, Person) — die Fraktion waehlt die Seite, nicht die Personlichkeit | trennt nicht — verworfen |
| Anspruch-Wort im Text/Buttons (1. Entwurf) | WIDERLEGT (Card-Befund): Refugee Tassi/Camsir/Dumudu/Sheedro sind eindeutig neutrale Bystander OHNE claim-Wort (Personen-Text, Knöpfe „Defeat"/„Reveal") | verworfen — ersetzt durch die Personen-Vokabel-Familie |
| Verdrahtungsmuster (Personen-Interaktion) | DRIN: Protester „Claim/{A} Rescue"+"Claim for Empire", Refugee „Defeat"+"Reveal"; RAUS: alle 38 Geraet-Zeilen nur „Destroy" („destroy / {a} destroy") | EXAKT 47/47 — entschiedene Intension |

Extensionale Vorgabe der Card, numerisch reproduziert (Tabelle unten):
DRIN Protester x9 (EMPIRE3) und Refugee Tassi/Camsir/Dumudu/Sheedro
(OTHER11); RAUS Checkpoint R/G/B, Siege Wall, Weak Point, Support, Beacon,
Cell, Engine, Data Core, Barricade, Window, Security Station, saemtliche
Tueren/Terminals/Warship. GRENZFALL-Klaerung (HOTH2): die Refugee 1-8 von
HOTH2 treten ins FEED gar nicht ein — ihr gedruckter Satz „A refugee can be
attacked" erfuellt die Angriffssatz-Grammatik („can attack … Health: N")
nicht (Null-Law, gemessen 0 Zeilen). Sie sind damit ausserhalb jedes
Anwendungsbereichs; ihre Neutralitaet bleibt unbestritten.
Duty-Anker (Gefangene, Cache, Wookiee …) stehen per Gate-Beweis mit 0 Namen
im Feed (kein parsebarer Angriffssatz) und sind schon deshalb nie
Anwendungsbereich; bystander-Zeilen unter den 7 Duty-Missionen: 0 (INFO-
Zeile des Gates). Kein Duty-Anker-Name ist Feed-Zeile (Gate bleed=0 ueber
alle 138 Missionen).

## Neue Messungen aus dem Zensuslauf (fuer die QA relevant)

- HOTH8 „Siege Wall 1/2" sind POLAR `jagt` (P1) und trotzdem NICHT Bystander
  (Destroy-Verdrahtung, Geraet) — das Typ+Personen-Gatter haelt auch gegen
  eine jagende Geraet-Zeile; die P36-Regel bleibt dort bit-identisch.
- CORE24 „Data Core" ist `jagt` P1, NOT bystander (Destroy) — ebenso RAUS.
- CORE5 „Power Station 1-4" sind `schuetzt` P2 — ausserhalb der Beuteklasse.
- Die Anwendungsmenge der Klasse ist im ausgelieferten Korpus exakt:
  EMPIRE3 Protester x9 (jagt+token+bystander). Die OTHER11-Refugees x4 sind
  Bystander, aber polaritaet `neutral` (P4) — Beutezeile fehlt, Anwendung
  entfaellt (dritte Schiene der Scope-Sperre), Verhalten dort unveraendert.
- Anwendungsfeld leer => alte P36-Regel unveraendert (Null-Law) — gilt fuer
  JEDE Mission ohne jagt+token+bystander-Zeile, d.h. alle 137 anderen.

## Regeltreue

Der Angriff auf Bystander bleibt regelkonformer Gewinnweg (EMPIRE3: „If a
protester is defeated, the Empire claims it"). P38 mischt nur die ANZAHL der
Jagenden (Quota-Band + Bedrohungs-Gatter) — kein Verbot, kein thematischer
Eingriff.

## Zensustabelle (117 Feed-Zeilen, echte Ableitung; Spalten:
## Mission | Name | GUID[:8] | entityType | polaritaet | Basis | neutral(Bystander))

"""

out = [header]
for r in rows:
    # | BESPIN1.json | Window 1 | 491aeaf3 | token | neutral | P4 | Nein |
    inner = r.strip('|').strip()
    mission, name, guid, etype, pol, basis, neutral = [x.strip() for x in inner.split('|')]
    out.append(f"| CENSUS {mission} | {name} | {guid} | {etype} | {pol} | {basis} | {neutral} |\n")
out.append("\n_Zeilenzahl: %d. Reihenfolge: Mission-Dateiname ordinal, danach Entity-Reihenfolge der Datei — deterministisch._\n" % total)
io.open('/home/open/work/ia-ai/ImperialCommander2/docs/CENSUS_beute_objekte.md', 'w', encoding='utf-8', newline='').write(''.join(out))
print('rows written:', total)
