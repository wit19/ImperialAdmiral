# Zensus: NEUTRALE Beute-Objekte (P38)

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

| CENSUS BESPIN1.json | Window 1 | 491aeaf3 | token | neutral | P4 | Nein |
| CENSUS BESPIN1.json | Window 2 | a5242b3c | token | neutral | P4 | Nein |
| CENSUS CORE1.json | Door North | 98907d92 | door | neutral | P4 | Nein |
| CENSUS CORE1.json | Door East | 628f5a4b | door | neutral | P4 | Nein |
| CENSUS CORE1.json | Door South | 7965c8a5 | door | neutral | P4 | Nein |
| CENSUS CORE11.json | Door North | e4abd589 | door | neutral | P4 | Nein |
| CENSUS CORE11.json | Door East | 2c105e11 | door | neutral | P4 | Nein |
| CENSUS CORE11.json | Door West | 71e51334 | door | neutral | P4 | Nein |
| CENSUS CORE11.json | Door South | a92d82bb | door | neutral | P4 | Nein |
| CENSUS CORE14.json | Outer Door | ef73d503 | door | neutral | P4 | Nein |
| CENSUS CORE14.json | Inner Door | 72b5929e | door | neutral | P4 | Nein |
| CENSUS CORE16.json | Door 1 | 9ed84cad | door | neutral | P4 | Nein |
| CENSUS CORE16.json | Door 2 | bef35689 | door | neutral | P4 | Nein |
| CENSUS CORE2.json | Terminal1 | 71710954 | terminal | neutral | P4 | Nein |
| CENSUS CORE2.json | Terminal2 | d90b24a6 | terminal | neutral | P4 | Nein |
| CENSUS CORE2.json | Terminal3 | 739d0d45 | terminal | neutral | P4 | Nein |
| CENSUS CORE2.json | Terminal4 | 60441375 | terminal | neutral | P4 | Nein |
| CENSUS CORE24.json | Data Core | d524289c | token | jagt | P1 | Nein |
| CENSUS CORE24.json | Archive Door | 3779f3aa | door | neutral | P4 | Nein |
| CENSUS CORE27.json | Northern Door | 595ecbe0 | door | neutral | P4 | Nein |
| CENSUS CORE27.json | Southern Door | aaf59fb6 | door | neutral | P4 | Nein |
| CENSUS CORE28.json | Door West | 0f3225ba | door | neutral | P4 | Nein |
| CENSUS CORE28.json | Door East | 81443b1e | door | neutral | P4 | Nein |
| CENSUS CORE28.json | Door West 2 | c16c6300 | door | neutral | P4 | Nein |
| CENSUS CORE28.json | Door East 2 | c8bad807 | door | neutral | P4 | Nein |
| CENSUS CORE3.json | Terminal 1 | b78a226e | terminal | neutral | P4 | Nein |
| CENSUS CORE3.json | Terminal 2 | f65eef08 | terminal | neutral | P4 | Nein |
| CENSUS CORE3.json | Terminal 3 | da1e1588 | terminal | neutral | P4 | Nein |
| CENSUS CORE4.json | Hangar Door 1 | 5abf38ac | door | neutral | P4 | Nein |
| CENSUS CORE4.json | Hangar Door 2 | efaabec4 | door | neutral | P4 | Nein |
| CENSUS CORE5.json | Power Station 1 | 07f25dcd | terminal | schuetzt | P2 | Nein |
| CENSUS CORE5.json | Power Station 2 | 722cee71 | terminal | schuetzt | P2 | Nein |
| CENSUS CORE5.json | Power Station 3 | fe29cf6a | terminal | schuetzt | P2 | Nein |
| CENSUS CORE5.json | Power Station 4 | 4685a0ed | terminal | schuetzt | P2 | Nein |
| CENSUS CORE7.json | Door 1 | 3dfb60c9 | door | neutral | P4 | Nein |
| CENSUS CORE7.json | Door 2 | 3d7c1c49 | door | neutral | P4 | Nein |
| CENSUS CORE7.json | Cell Door | 37062af8 | door | neutral | P4 | Nein |
| CENSUS CORE7.json | Transmission Terminal | 438f6719 | terminal | neutral | P4 | Nein |
| CENSUS EMPIRE1.json | Shield Generator 1 | db3f6b8d | terminal | neutral | P4 | Nein |
| CENSUS EMPIRE1.json | Shield Generator 2 | b2138e00 | terminal | neutral | P4 | Nein |
| CENSUS EMPIRE10.json | Door 1 | 6c68ff60 | door | neutral | P4 | Nein |
| CENSUS EMPIRE10.json | Door 2 | 978b0eb1 | door | neutral | P4 | Nein |
| CENSUS EMPIRE3.json | Protester 1 | b76939b9 | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 2 | 94d905c8 | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 3 | c998787d | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 4 | 0b7b8b29 | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 5 | 3bed2537 | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 6 | 11a6d3ac | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 7 | 438f5870 | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 8 | a939b77e | token | jagt | P1 | Ja |
| CENSUS EMPIRE3.json | Protester 9 | 40e7a655 | token | jagt | P1 | Ja |
| CENSUS HOTH2.json | Door Storehouse | fc54b861 | door | neutral | P4 | Nein |
| CENSUS HOTH2.json | Door Workshop | c36b9dcb | door | neutral | P4 | Nein |
| CENSUS HOTH2.json | Door Control Room | 27be102f | door | neutral | P4 | Nein |
| CENSUS HOTH2.json | Door Clinic | 52172669 | door | neutral | P4 | Nein |
| CENSUS HOTH2.json | Barricade 1 | 949fd1bf | token | neutral | P4 | Nein |
| CENSUS HOTH2.json | Barricade 2 | cbe25f45 | token | neutral | P4 | Nein |
| CENSUS HOTH2.json | Barricade 3 | 5a73ec1b | token | neutral | P4 | Nein |
| CENSUS HOTH2.json | Barricade 4 | 6592be36 | token | neutral | P4 | Nein |
| CENSUS HOTH4.json | Control System 1 | f15d54d3 | terminal | neutral | P4 | Nein |
| CENSUS HOTH4.json | Control System 2 | 9b7e7a58 | terminal | neutral | P4 | Nein |
| CENSUS HOTH8.json | Checkpoint Red | 593b9726 | token | neutral | P4 | Nein |
| CENSUS HOTH8.json | Checkpoint Blue | 03185daa | token | neutral | P4 | Nein |
| CENSUS HOTH8.json | Checkpoint Green | 238c4c68 | token | neutral | P4 | Nein |
| CENSUS HOTH8.json | Siege Wall 1 | a575d3e8 | token | jagt | P1 | Nein |
| CENSUS HOTH8.json | Siege Wall 2 | b3de9b8b | token | jagt | P1 | Nein |
| CENSUS HOTH9.json | Door NE | 30631406 | door | neutral | P4 | Nein |
| CENSUS HOTH9.json | Door N | 12b2f0b8 | door | neutral | P4 | Nein |
| CENSUS HOTH9.json | Door S | 10df348c | door | neutral | P4 | Nein |
| CENSUS HOTH9.json | Door SE | 7903bdc0 | door | neutral | P4 | Nein |
| CENSUS JABBA1.json | Beacon 1 | 2c71f9b4 | token | neutral | P4 | Nein |
| CENSUS JABBA1.json | Beacon 2 | 5545e865 | token | neutral | P4 | Nein |
| CENSUS JABBA10.json | Barricade 1 | 63717e9f | token | neutral | P4 | Nein |
| CENSUS JABBA10.json | Barricade 2 | 1e149c77 | token | neutral | P4 | Nein |
| CENSUS JABBA10.json | Barricade 3 | a0f25169 | token | neutral | P4 | Nein |
| CENSUS JABBA11.json | Cell 1 | 7a5d07ac | token | neutral | P4 | Nein |
| CENSUS JABBA11.json | Cell 2 | 1f8aed36 | token | neutral | P4 | Nein |
| CENSUS JABBA12.json | Support 1 | 1d6c37bb | token | neutral | P4 | Nein |
| CENSUS JABBA12.json | Support 2 | 723ddaaf | token | neutral | P4 | Nein |
| CENSUS JABBA12.json | Support 3 | 9737f1e5 | token | neutral | P4 | Nein |
| CENSUS JABBA12.json | Support 4 | 2cda8b11 | token | neutral | P4 | Nein |
| CENSUS JABBA12.json | Support 5 | d14f3b91 | token | neutral | P4 | Nein |
| CENSUS JABBA2.json | Engine | 83dcc253 | token | neutral | P4 | Nein |
| CENSUS JABBA3.json | Terminal 1 | e0fbdfeb | terminal | neutral | P4 | Nein |
| CENSUS JABBA3.json | Terminal 2 | 684488d2 | terminal | neutral | P4 | Nein |
| CENSUS JABBA3.json | Terminal 3 | 3e429cf7 | terminal | neutral | P4 | Nein |
| CENSUS JABBA4.json | Door 1 | 262971f6 | door | neutral | P4 | Nein |
| CENSUS JABBA4.json | Door 2 | edc3fc97 | door | neutral | P4 | Nein |
| CENSUS JABBA4.json | Door 3 | 259f458b | door | neutral | P4 | Nein |
| CENSUS JABBA6.json | Door 1 | 3f498bdd | door | neutral | P4 | Nein |
| CENSUS JABBA6.json | Door 2 | 9054c033 | door | neutral | P4 | Nein |
| CENSUS JABBA6.json | Weak Point 1 | f3221333 | token | neutral | P4 | Nein |
| CENSUS JABBA6.json | Weak Point 2 | c21c2c86 | token | neutral | P4 | Nein |
| CENSUS JABBA6.json | Weak Point 3 | 2ac55005 | token | neutral | P4 | Nein |
| CENSUS JABBA6.json | Weak Point 4 | e85e3543 | token | neutral | P4 | Nein |
| CENSUS JABBA6.json | Weak Point 5 | 189b93c1 | token | neutral | P4 | Nein |
| CENSUS JABBA6.json | Weak Point 6 | 54543b1d | token | neutral | P4 | Nein |
| CENSUS OTHER11.json | Door 1 | 292f5073 | door | neutral | P4 | Nein |
| CENSUS OTHER11.json | Door 2 | 102c5c26 | door | neutral | P4 | Nein |
| CENSUS OTHER11.json | Door 3 | 047d4f81 | door | neutral | P4 | Nein |
| CENSUS OTHER11.json | Door 4 | 9441a013 | door | neutral | P4 | Nein |
| CENSUS OTHER11.json | Refugee Tassi | f1f5c450 | token | neutral | P4 | Ja |
| CENSUS OTHER11.json | Refugee Camsir | d027b5d7 | token | neutral | P4 | Ja |
| CENSUS OTHER11.json | Refugee Dumudu | 8a14e512 | token | neutral | P4 | Ja |
| CENSUS OTHER11.json | Refugee Sheedro | e127b561 | token | neutral | P4 | Ja |
| CENSUS OTHER21.json | Door 1 | a23558e4 | door | neutral | P4 | Nein |
| CENSUS OTHER21.json | Door 2 | 08a90865 | door | neutral | P4 | Nein |
| CENSUS OTHER21.json | Door 3 | 7a21649b | door | neutral | P4 | Nein |
| CENSUS OTHER3.json | Warship | d39dbb1c | highlight | jagt | P1 | Nein |
| CENSUS OTHER37.json | Security Station 1 | f8454455 | token | neutral | P4 | Nein |
| CENSUS OTHER37.json | Security Station 2 | 2429899a | token | neutral | P4 | Nein |
| CENSUS OTHER37.json | Security Station 3 | 8d3cf270 | token | neutral | P4 | Nein |
| CENSUS TWIN2.json | Safe Door | fa70937f | door | neutral | P4 | Nein |
| CENSUS TWIN4.json | Cantina Door 1 | b64be74e | door | neutral | P4 | Nein |
| CENSUS TWIN4.json | Cantina Door 2 | 8dbb677d | door | neutral | P4 | Nein |
| CENSUS TWIN4.json | Junk Shop Door 1 | 40aad4da | door | neutral | P4 | Nein |
| CENSUS TWIN4.json | Junk Shop Door 2 | 0f269c05 | door | neutral | P4 | Nein |

_Zeilenzahl: 117. Reihenfolge: Mission-Dateiname ordinal, danach Entity-Reihenfolge der Datei — deterministisch._
