# CONCEPT Missionsdienstplicht — Missionsobjekt-Interaktion im taktischen Modus

*Thomas-Auftrag 08.11.2026: Hoth-Nebenmission „Immer wachsam" (HOTH10, Loku) —
der taktische Modus ignoriert die Gefangenen. Gefragt: allgemeines,
missionsübergreifendes Konzept, das ohne Bruch der bestehenden Logik passt.
Reiner Konzept-Stand — kein Code, kein GO.*

---

## 0. Befund (verifiziert, mit Fundstellen)

### 0.1 Was „Befehl" im taktischen Modus heute bedeutet

Der Popup-Kanal ist die einzige Entscheidungsoberfläche:
`EnemyActivationPopup.ShowSaga` → `TacticalAIBridge.TryBuildActivationPlan`
(„Activation popup hook", Bridge Z. 985). Der Popup-Inhalt im Taktik-Modus:

```
Fokus-Ziel-Zeile (focusPrefix, immer sichtbar, M6p-Gesetz)
Doktrin-Zeile (doctrineHeader + doc*, M6p)
AKTIONSPUNKTE-Block (apHead, apRule „Lies von oben: Der erste Zweig mit JA
   wird ausgeführt", Doktrin-Baum apTree*, apEnd)
[Info-Block, Erklär-Block — gated]
```

Das **Sprachmaterial** dieser Befehle liegt in `Languages/<11>/ui.json →
uiTacticalAI` (164 Keys, 11/11 konstruiert: Br De En Es Fr Hu It Nl No Pl Ru;
echt übersetzt sind De+En, Rest hält EN-Fallback — geltendes Gesetz).
Bemerkenswert: ein **`interactLine`-Key existiert bereits in allen 11
Sprachen** — „Interaktion: eine von der Mission erlaubte Interaktion
ausführen (z. B. eine angrenzende Tür öffnen …)" — und wird gegenwärtig von
NIEMANDEM gerendert (nur Header-Label-Schutz, Popup Z. 417). Das
Interaktions-Vokabular ist reserviert, aber unbenutzt.

### 0.2 Der strukturierte Missionskanal (das, was der taktische Modus übersieht)

Missionsbefehle sind **kein Text-Fund** — sie sind eine eigene Event-Action:

* `eventActionType 11` = **„Change Group Instructions"**
  (`ChangeInstructions : EventAction` — `theText`, `instructionType`,
  `groupsToAdd` mit expliziten DG-IDs, leer = ALLE).
* `instructionType` = `CustomInstructionType { Top, Bottom, Replace }`.
* **Korpus: 26 Missionen, 37 Aktionen** (Top 19 / Bottom 7 / Replace 11),
  über Core, Empire, Hoth, Lothal, Jabba, Twin Suns, Other — also
  definitionsgemäß allgemein.
* Zur Laufzeit liegt der Override in `gameVars.GetDeploymentOverride(dgId)
  .changeInstructions` — **lokalisiert** (Sprachdatei-Injektion!) und
  **mit der Quell-Action-GUID** (`EventAction.GUID`). Die GUID ist
  sprachunabhängig → Klassifikation offline, Lookup live, null
  Sprach-Parsing.

### 0.3 Was der taktische Popup heute damit tut (Genauigkeit statt Verdacht)

`GetModifiedInstructions(cd.id, tacInstructions, true)` (Popup Z. 682):

* **Top/Bottom**: Missionszeile wird **wörtlich vor-/angestellt**. Sie ist
  also sichtbar — sie **schwebt aber über allem** (sogar über der
  Fokus-Ziel-Zeile, gegen die Leseordnung) und ist **nicht in den
  Entscheidungsbaum eingebunden**. Der Baum sagt derweil
  „Bewegung Richtung Fokusziel" und weiß nicht, dass in dieser Mission die
  Bewegung selbst der Missionsbefehl ist („MOVE, PRISONER: Push the closest
  prisoner … 4 spaces towards the cell").
* **Replace**: `TacticalMerge` (Z. 725) behält nur Nicht-Glyphen-Regel-
  bullets — **Glyphen-Aktionszeilen werden verworfen**. In HOTH9
  („{A} Move 4 toward the closest terminal. {A} Move 2 to discard an
  adjacent terminal."), CORE3 (Weiss 1-1), CORE27 (Courier zum Entrance)
  und EMPIRE5 (Vader-Shard) verschwindet damit **der imperiale
  Missionsdienst aus dem taktischen Popup komplett**.
* **Entscheidungsebene**: Fokus-Ranking (`OverlordFocus`) permutiert
  ausschließlich Helden (fairness-rechtlich so gewollt: „No positions, no
  hidden state"); Markers existieren dort nicht. Posture = `defend` nur bei
  Priority-Ally/Terminal-Entity → HOTH10 läuft als `hunt`. Counter-Gesetz
  (`DeriveCounter`) kennt nur `woundAll`/`incapacitateAll` aus den vier
  missionProperties-Textfeldern — HOTH10 hat sie leer → `counter=null` →
  Legacy-Finisher-Doktrin. Die Marker+Zelle sind als kritische Entities
  zwar **schon gemined** (`tactical_mission_critical.json`: Prisoner+Cell →
  Start Section) und fließen in die Wachzonen-Glaubensbildung
  (`LastCriticalSections` → `PlanZones`) — aber nirgends in einen
  Handlungsast.

**Korrektes Fazit:** Auf der Anzeige-Ebene ist der HOTH10-Befehl sichtbar
(Top), auf der Entscheidungs-Ebene wird er von Fokus, Doktrin, Baum und
Counter ignoriert; bei Replace-Missionen fehlt er auch sichtbar. Thomas'
Eindruck „ignoriert die Gefangenen komplett" ist entscheiderisch richtig.

---

## 1. Korpus-Klassifikation (Grundlage der Allgemeinheit)

Die 37 Aktionen sind mechanisch klassierbar (EN-kanonischer Quelltext,
wie alle Mining-Tabellen: regenerierbar, öffentlich, reviewable, Goldene
Vektoren gegen Drift):

| Klasse | Merkmal (mechanisch) | Beispiele | Behandlung heute | Behandlung Konzept |
|---|---|---|---|---|
| **Duty** (Dienstanweisung) | Imperativ + Bewegungs-/Interaktionsverb + Missionsobjekt-Referenz oder Glyphen-Aktionszeile ({A}/{Q}+Move/Interact) | HOTH10 Push 4/6, HOTH9 Terminal discard, CORE3 Terminal-Interact + AT-ST, CORE16, CORE27 Flucht zum Entrance, TWIN3, EMPIRE4 Arrest Lorn, EMPIRE5, EMPIRE7 Push Lorn, CORE10, CORE28 Supply-Caches, OTHER9 Wookiee-Bind | Top/Bottom: schwebt; Replace: **verloren** | **Duty-Zweig im Baum** (§2) |
| **Law** (Ordnungsregel) | Konditional ohne imperativen Aktionsträger („attack doors if quickest way", „becomes the target", Speeder-Buff, Insight-Gesetze) | CORE11-Door, CORE6, HOTH2/8/9-Door, JABBA2/4, LOTHAL3/6, OTHER10/11, HOTH11-Token | Info-Block/Ordnungsrecht deckt das ab | **unverändert** (kein neuer Ast) |
| **Effect** (einmalig) | Kein Instructionsbefehl (Heilung, Zustände) | CORE3 Invulnerable, CORE14 | Regel-Zeile bleibt | unverändert |

Damit ist die Antwort auf „allgemein gültig für alle Missionen mit solchen
Missionszielen?" **Ja, beweisbar am Korpus**: dieselbe Action-Klasse,
dieselbe GUID-Verdrahtung, 26 Missionen, eine Klassifikation.

---

## 1a. REV 2 — Duty-Ableitung aus dem Missionsziel (Thomas-Korrektur 08.11., maßgeblich für Ableitung/Scope; Parameter-Form seit Rev 3: §1b)

**Befund:** Der Duty-Kanal aus §1/§2 („Change Group Instructions") ist die
Überschreibung der **Legacy-Anweisungskarten** — gruppen-selektiv, mit
Legacy-Dosierung (HOTH10: Sturmtruppen 4 Felder, Offiziere 6, HK-Assassine
DG026: gar nicht). Diese Skopierung ist Legacy-Anpassung; die
Referenzregeln kennen sie so nicht (HOTH10: **jede** imperiale Figur darf
die Gefangenen stoßen). Ein Duty aus dem Legacy-Kanal wäre ein
„legacy cheat-Erbe" und ist verworfen. Auch die Briefing-Texte der App
tragen die legacy Splitting („Imperiale Offiziere und Sturmtruppen werden
die Gefangenen in die Zelle treiben") — MedPac-Präzedenz: wo App-Text der
Referenzregel widerspricht, darf REF/ORD ihn nicht ungeprüft weiterreichen.

**Stattdessen: Ableitung aus der Missionsziel-Struktur (rein mechanisch):**
* Duty-EXISTENZ: Zielfeld-Entity mit Button → Event mit ModifyVariable(V)
  → Schwellen-Trigger V==N auf ein End-Event der imperiale-Gewinn-Seite.
  (Mining-Grund `objective-variable` existiert bereits.)
* Duty-SCOPE: Die Ziel-Struktur enthält KEINEN Gruppenskopus ⇒
  Scope-Gesetz: **jede imperiale Figur**, außer gedruckte Missionsdaten
  nennen Ausnahmen. DG-Listen sind Anweisungskarten-Artefakte.
* Duty-OBJEKT: Marker = per ModifyMapEntity spawnende Token-Entities;
  Zielfeld = Ziel-Entity. **Die Interaktions-OBERFLÄCHE ist
  missionsindividuell und KEIN Bestandteil der Anweisung** (Thomas 08.11.
  III): HOTH10 hat überhaupt KEINE Buttons in der ganzen Mission — der
  Marker wird unsichtbar am Brett geführt, „Prisoner transferred" ist ein
  wiederholbares globales Buchhaltungs-Event; andere Missionen mögen
  Buttons am Marker oder am Zielfeld haben. Darum sagt die Duty-Zeile
  niemals „klicke X", sondern immer nur „gemäß Missionsregel". Wo der
  Mensch am Brett klickt (oder nicht), ist App-Buchhaltung, nicht
  Taktikanweisung. Der Duty-Fakt steht allein auf Zähler + Gewinn-Seite;
  Setter mit oder ohne Button ist dafür gleichwertig.
* Duty-PARAMETER (Distanz/Aktionstyp): **KEINE Kuratur, keine Tabelle, kein
  Pflegen pro Mission** (Thomas 08.11. II: „Ich möchte KEINE spezielle
  Lösung für HOTH10"). Die Duty-Zeile verweist generisch auf die
  **gedruckte Missionsregel** — die Zahl/den Aktionstyp liest der
  Imperiumsspieler von seiner Missionskarte (HOTH10-REF als EXAMPLE, nie als
  Regelwerk-Eintrag: jede imperiale Figur darf mit einem Marker
  interagieren, um ihn 6 Felder zu bewegen). Kein Legacy-Wert-Import,
  kein Fail-Closed nötig: es gibt nichts zu kuratieren.

**Legacy-Overrides (§0.2/§0.3) bleiben, aber degradiert:** Top/Bottom/
Replace-Merge verhält sich exakt wie heute (Regel-Zeilen im Popup,
EMPIRE13-Fix unberührt) — sie speisen KEINEN Duty mehr. Der Duty-Block
steht ausschließlich aus der Missionsziel-Ableitung.
§1-Klassifikation überlebt für law/effect unverändert (die brauchen keinen
Duty); die duty-Klasse meint ab jetzt den abgeleiteten Duty, nicht die
Legacy-Zeile.

**Consequenz für alle Interaktions-Missionen:** CORE16-Terminal-discard,
CORE28-Cache, OTHER9-Bind, CORE27-Courier — dieselbe Bauart wie HOTH10:
Duty aus Ziel-Struktur, Parameter aus der gedruckten Missionsregel. Eine
Speziallösung pro Mission existiert nicht und ist verboten.

## 1b. REV 3 — Mischungsgesetz (Thomas-Korrektur 08.11. II, maßgeblich)

**Befund zur Rev-2-Form:** „Duty geht dem Bewegungsast vor" wäre
spielzerstörend: die imperialen Figuren tun dann nichts als Marker
schubsen, Bedrohung und Vergeltung greifen entscheiderisch nie, die Rebellen
eliminieren in Ruhe — oder die AI spielt reinen Whack-a-Mole-Reaktionär.
Beides kein Spielspaß. **Absoluter Duty-Vorrang ist verworfen** (§6-
Entscheidung 1 und §2-Schicht-3-Block gelten nur noch als Historie).

**Gesetz 1 — Der Duty reitet die Aktionspunkte, nie den Angriff.**
Die Angriffs-/Zielauswahl-Schicht (Fokus-Ziel, Vergeltung, Counter-Gesetz,
Reaktionsrecht) bleibt in JEDEM Fall unangetastet: eine geschossene
Escort-Sturmtruppe schießt auf ihren Schützen wie jede andere Figur.
Pflichttrennung: der Duty konkurriert ausschließlich mit dem
BEWEGungs-Aktionspunkt.

**Gesetz 2 — Mischung über die vorhandene Doktrin-Würfel-Ökonomie.**
In Duty-Missionen erhalten die Muster (M13u-Mechanik: gedruckte
Flavour-Gewichte × Modifikator-Zeile, Seed Mission+Karte+Runde) eine
**Duty-Modifikator-Zeile** als DATENSATZ in `tactical_pattern_weights`:
ein Teil der Muster reitet den Duty-Ast im Bewegungs-Zweig (z. B.
guard/blocker → Escort), der andere Teil behält volles Gefechtsverhalten
(aggressive/hunter/ambusher/support). Escort-Anteil = Zahl in der Tabelle,
QA-stimmbar; pro Runde seed-stabil; im Explain offen gelegt. Kein neuer
Zufall, kein Code-Zweig pro Mission — dieselbe Mechanik, die in M13u schon
die Posture verschiebt. „Eine gesunde Mischung" ist damit kein Text,
sondern die Würfel-Ökonomie selbst.

**Gesetz 3 — Bedrohung schlägt Duty (innerhalb der Duty-Gruppe).**
Der Duty-Ast im Bewegungs-Zweig ist ein gedruckter Wenn-Dann-Zweig wie
alle Äste: „? kein Rebell in Drohungsreichweite von dir oder deinem
Missionsmarker? → interagiere mit dem Missionsmarker gemäß Missionsregel.
? Marker nicht erreichbar? → bewege dich zum nächsten Missionsmarker.
? Bedrohung? → regulärer Gefechtsast." Schubsen passiert nur, wenn niemand
auf sie schießt oder sie bedroht — das Kanonenfutter-Szenario ist
strukturell ausgeschlossen, die Rebellen erzwingen mit Näherung selbst den
Ausstieg ihrer Gegner aus dem Duty.

**Gesetz 4 — Nicht-Duty-Gruppen behalten volle Doktrin inkl.
Objektbedrohung.** Die Gefechts-Gruppen intercepten nach den bestehenden
gedruckten Zielregeln `objektbedroher`/`gewinntraeger` („zuerst jeder
Rebell, der {schutz}/ein Auftragsziel berührt") — Speisung des {schutz}
aus dem Duty-Objekt-Anker (minierter Name, keine Koordinaten, kein neues
Ranking). Wer die Marker anfasst, zieht Gefechts-Gruppen an; die Escort
selbst wehrt sich (Gesetz 1+3).

**Gesetz 5 — Abgrenzung gegen die MissionObjekt-Mechanik (EMPIRE3-
Demonstranten; Thomas 08.11. III).** Die beiden Mechaniken sind
klassen-, schicht- und datenmäßig getrennt, Beweise aus den echten Dateien
(08.11. gelaufen):

* **Klassen-Fence:** Die MissionObjekt-Feed-Klasse (P33-H4c) speist nur
  Objekte mit **gedrucktem Angriffssatz** in eigenen Kanon-Texten
  („can attack … (Health: N…)"/„angreifen"), Null-Law. Keiner der 8
  Duty-Kandidaten besitzt eine Angriffssatz-Entity — der Feed kann dort
  per Bauart keinen Fakt erzeugen. Umgekehrt verlangt der Duty-Detektor
  Zähler + imperiale Gewinn-Seite; EMPIRE3s End-Events tragen keine
  Imperiumsgewinn-Phrase (Hideaway-Ereignisse, Side-Check = False) — der
  Detektor ist dort stumm. **Keine Mission gehört aktuell zu beiden
  Klassen.**
* **Schicht-Fence:** objactor/TryObjectTarget wirkt ausschließlich in der
  Angriffs-/Zielschicht; der Duty-Ast ausschließlich im Bewegungs-Zweig
  (Gesetz 1). Zwei Mechaniken, zwei Slots, kein doppelt gedruckter Satz.
* **Dedup-Fürsorge (Zukunfts-Missionen):** Sollte je dasselbe Objekt
  (GUID) in beiden Klassen erschiene, gilt: **objactor behält die
  Ziel-Schicht, Gesetz-4-Speisung des Duty wird für diese GUID
  unterdrückt** — nie zwei Popup-Zeilen über dasselbe Objekt. QA-Assertion
  pro Duty-Mission: keine Duplikat-Zeile; EMPIRE3-Popup **bit-identisch**
  zum P36-Stand (wird als Vektor gegengeprüft).

Für den Menschen heißt das: in EMPIRE3 sieht er exakt die ihm vertrauten
objactor-Zeilen, nie eine Duty-Zeile; in Duty-Missionen sieht er den
Duty-Ast, nie objactor-Zeilen; Missionen, die der Detektor nicht meldet,
bleiben komplett unberührt.

**Spaß-Kriterium als Abnahmegesetz (QA, §7):** Es darf in keiner Runde
einer Duty-Mission ein Popup geben, in dem ALLE Gruppen nur schubsen.
Erwartetes Bild: einige Gruppen eskortieren (ungefährdet), einige jagen,
beschossene Escort schießt zurück, Rebell am Marker zieht Interception.

## 2. Konzept: der Duty-Zweig (drei Schichten, alle mit vorhandenen Slots)

> **REV-3-Stand:** Schichten 1/2 bleiben (Mining + GUID-Fakt), aber der
> Mining-Fakt kommt jetzt aus der **Ziel-Struktur-Ableitung** (§1a/§7),
> nicht aus den Anweisungskarten; Schicht 3 lautet nicht mehr
> „Duty-Block geht vor", sondern **Duty-Ast im Bewegungs-Zweig mit
> Bedrohungs-Gatter, gemischt über Gesetz 2**. Der Block unten ist
> Rev-2-Historie.

### Schicht 1 — Mining (offline,_like schon immer_)
Verlängerung des `mission_critical_mine.py`-Outputs (neues Feld
`dutyDirectives` pro Mission): `{ actionGuid, eventGuid, eventName,
groupIds (leer=ALLE), instructionType, class ∈ {duty,law,effect},
hasGlyphLines }`. Klassifikation = feste EN-Verbliste (push, interact,
move … toward, arrest, bind, discard, place) + Glyphen-Erkennung.
37 Zeilen als Goldener Vektor fixiert. „Regenerate, never hand-edit"
bleibt das Gesetz.

### Schicht 2 — Runtime-Fakt (live, strukturiert, sprachneutral)
Bei Popup-Bau für Gruppe X: live-Override (gruppe-spezifisch + ALL) lesen;
**Klassifikations-Lookup über die Action-GUID** (übersteht die
Sprach-Injektion, kein Regex gegen 11 Sprachen, keine Text-Auslegung).
Kein Tabellen-Treffer ⇒ Verhalten **bit-identisch zu heute**
(Fail-Open/Null-Law wie bei allen Mining-Kanälen).

### Schicht 3 — Der Befehl (was in die Sprachdateien kommt)
Ein **Duty-Block** direkt nach `apRule` und **vor** dem Doktrin-Baum, mit
zwei neuen `uiTacticalAI`-Keys (11×; De/En echt übersetzt, Rest
EN-Fallback wie bisher):

```
MISSIONSDIENSTANWEISUNG — gedruckt, geht dem Bewegungsast des Baums vor:
{die Missionszeile selbst, in der aktiven Sprache, wörtlich}
? Anweisung nicht ausführbar? → regulärer Bewegungsast des Baums.
```

* Der **Inhalt** wird nie von uns übersetzt — die Missions-Injektion liefert
  ihn bereits in allen Sprachen („BEWEGUNG, GEFANGENER: Stoße den
  nächstgelegenen Gefangenen …" / „MOVE, PRISONER: …"). **Deshalb braucht
  es KEINEN 11-fach zu übersetzenden neuen Inhalts-Befehl, sondern nur die
  Wrapper-Keys.** Das beantwortet Thomas' Befehl-in-allen-Sprachdateien-
  Punkt: nötig ja (2 Wrapper-Keys), Übersetzungsarbeit nein.
* **Replace+Glyphen**: `TacticalMerge` behält DUTY-Klassen-Glyphenzeilen
  künftig (sie rendern im Duty-Block); Law/Effect-Replace bleiben
  unverändert → der EMPIRE13-Fix (Regel-Bullets bleiben, Würfelbaum bleibt)
  bleibt bit-identisch erhalten.
* **Top-Ordnung**: Duty-Zeilen schweben nicht mehr über der Fokus-Zeile —
  sie landen im Duty-Block. Leseordnung (Fokus zuerst, Thomas-Gesetz
  29.10.) bleibt heil. Law-Zeilen bleiben, wo sie sind (minimale
  Änderungsbreite).
* `interactLine` (dormant, 11/11) wird **bewusst nicht umgenutzt**: es
  beschreibt eine *erlaubte* Interaktion; der Duty-Block ist ein
  *Befehl*. Semantik sauber halten, neuer Key.

---

## 3. Die Abstands-Frage (das Kernargument)

Thomas' Sorge: „wie willst du den Abstand zu den verschiedenen, parallel im
Spiel befindlichen Objekten bestimmen?" — oder: Befehl vage halten und
Bewegung clever ausrichten.

**Antwort: Vage ist nicht der Notnagel, vage ist das System.**

1. Die Entscheidungs-Schicht enthält **per Fairness-Grundgesetz keine
   Einheiten-Positionen** (`OverlordFocus`-Kanon: „No positions, no hidden
   state, no dice"; `Doctrine.SelectDoctrine`: AppView only, „never rebel
   positions"). Ein zellgenaues „Distanz zu Marker 1..3, Zelle, Helden"
   erforderte einen zweiten Live-Pathfinder gegen dieses Grundgesetz — er
   würde Behauptungen erzeugen, die kein Imperialspieler an diesem Punkt
   seiner Runde wissen darf, und wäre gegen alles, was die bisherigen
   QA-Runden (Maskengesetz, öffentliche Daten) getragen haben.
2. Die Missionen **selbst drucken den vagen Selektor** — und zwar schon
   perfekt: „the closest prisoner **that is not on the Cell**", „the
   nearest capture site", „the quickest way". Der Duty-Block zitiert ihn
   wörtlich. Die Runden-Dynamik (jede Runde ein neuer Marker, max 3 auf der
   Karte, 4 nötig) wird vom gedruckten Selektor **automatisch absorbiert**
   — die AI-Schicht muss über Marker-Anzahl nichts wissen und nichts pro
   Runde neu berechnen. Genau das war Thomas' Vermutung — sie stimmt.
3. **Gleichstände lösen nach gedrucktem Recht**: Die GH-Kaskade
   (S. 39-40: wenigste Bewegungspunkte → kürzeste Distanz nach
   Geländeregeln → Missionsrollen-Gewicht → Aktivierungsreihenfolge →
   geringere Vitalität) ist bereits heute Fußzeile jeder Doktrin
   (`Doctrine.GhCascadeFootnote`). „Nächstgelegenes Missionsobjekt" hat
   damit eine regelkonforme, am Tisch lösbare Definition — dieselbe, die
   das Imperium bei jeder anderen Zielwahl benutzt.
4. **Klickbares Zielfeld** (Zelle-Button „Gefangener überstellt"): bleibt
   Menschen-Buchhaltung, exakt wie „Bind" in OTHER9 — Button-Klicks sind
   Aktionen des Imperialspielers, nicht der AI. Fortschritt sieht der
   Mensch im Missionslog („überstellte Gefangene: X/4" wird vom App-Log
   mit der Variable live gezogen). Ein KI-seitiger Fortschritts-Counter
   (Variable+Zielwert gemined) ist als **spätere Option** notierbar, für
   den Duty-Zweig aber **nicht nötig** — und ohne Not führen wir ihn nicht
   ein (Counter-Gesetz bleibt woundAll/incapacitateAll, Null-Law).
5. Was die Schicht sehr wohl steuern *kann* (weil öffentliche Fakten):
   Der Duty-Zweig ist **Geografie, kein Ziel**. Die Zielwahl bleibt
   unangetastet; die vorhandene Doktrin-Regel `gewinntraeger` („zuerst
   jeder Rebell, der ein Auftragsziel berührt") und `objektbedroher`
   („Zuerst jeder Rebell, der {schutz} bedroht") können in Duty-Missionen
   den Dienstraum als {schutz} bekommen — existierendes Vokabular, neue
   Speisung, kein neues Ranking. (Als Ausbaustufe P5 markieren, nicht
   Schicht-1-Pflicht.)

---

## 4. Probleme mit der bestehenden Logik (Prüfung, Punkt für Punkt)

1. **TacticalMerge-Regression (EMPIRE13)** — chirurgisch: nur Duty-Klasse
   behält Glyphenzeilen; Regel-Bullets und Law/Replace unverändert.
   Absicherung: alle bestehenden Popup-/Doktrin-Fixtures müssen
   bit-identisch bleiben (Missions-Ohne-Duty = 137 Missionen unverändert).
2. **Leseordnung** — Top-Zeilen oberhalb der Fokus-Zeile war schon heute
   ein stiller Gesetzesverstoß; der Duty-Block behebt ihn. Sichtbare
   Popup-Änderung nur in Duty-Missionen.
3. **Fokus-Maschinerie** — keine neue Kandidatenklasse, kein neuer Ranking-
   Key, keine Fairness-Drift. (Marker sind keine Ziele — sie werden
   weder angegriffen noch „besiegt"; das Null-Gesetz der OBJEKT-Rechts-
   lage lässt sie neutral, und das ist korrekt so: das Imperium muss sie
   lebend haben.)
4. **Muster-Würfel & Posture** — bleiben unverändert (HOTH10 bleibt hunt;
   kein Terminal ⇒ kein defend). Der Duty-Block erscheint in JEDEM Muster
   — gedruckter Befehl ist Pflicht, Doktrin ist Farbe. Tripod/Officer-
   Bäume unverändert; Missionstexte weisen die Duty ohnehin nur mobilen
   Gruppen zu (groupIds, gedruckt).
5. **Wachzone-Text** — apTreeGuard definiert Wachzone rebellen-zentriert
   („Bereich, in dem Rebellen ihre Ziele erfüllen"). In Duty-Missionen
   entscheidet dieselbe Zone (Prisoner+Cell sind bereits als kritische
   Sections gemined und versorgt `PlanZones` heute schon). Empfehlung:
   Definitionssatz zu „Bereich, in dem diese Mission entschieden wird"
   generalisieren (1 Key, 11 Sprachen) statt Duty-Variante — offene
   Entscheidung, §6.
6. **Persona-/Overlord-Ebene** — orthogonal (Ziel-Boni vs. Bewegungs-
   pflicht), keine Wechselwirkung mit Fokuszone/ORD-Fokus.
7. **Legacy/off/Import-Gruppen** — Katalog-Gatter upstream, unverändert;
   durchgehende Fail-Open-Regel: kein Tabellen-Treffer ⇒ exakt heutiges
   Popup.
8. **MedPac-Scrub, Info-Block, Reaktions-/Vergeltungsrecht** — unangetastet.
9. **SPECIAL-Slot-Duties** (HOTH11 Token-Placement, ORDER-Zusatzbewegung
   CORE11-Action2) — bleiben Regel-Zeilen (Klasse law/effect). Ein
   SPECIAL-Slot-Pendant zum Duty-Zweig ist eine ehrliche, aber separate
   Ausbaustufe — hier nicht mitversprochen.
10. **Counter-Gesetz** — bleibt für HOTH10 `null` (Legacy-Finisher). Das
    ist korrekt und redlich: „Prisoners claimed/4" ist kein woundAll-Satz;
    der Duty-Block braucht keinen Counter. Wer den Fortschritts-Counter
    will, bestellt ihn als eigenes Projekt (§3.4).

## 5. Machbarkeits-Urteil

**Ja — integrierbar, ohne die bestehende Logik zu biegen.** Jeder Teil des
Konzeps fällt in einen Slot, den es schon gibt: Mining-Tabelle (Schema
tacai/*/1, regenerierbar), GUID-gekeyter Live-Fakt (Override trägt Quell-
GUID), Popup-Block (Block-Mechanik + 2 neue Keys × 11 Sprachen),
TacticalMerge (eine Klassen-Weiche — GUID bleibt Bindeglied). Kein
Engine-Eingriff, keine neue RNG,
keine Positions-Lesung, 137/138 Missionen bit-identisch.

Arbeitspakete bei GO: P1 Mining+Fixture · P2 Core-Lookup+Vektoren ·
P3 Bridge-Einbau + Merge-Weiche + 11×3 Keys · P4 Popup-Vektoren
(HOTH10/HOTH9/CORE27) + Bit-Identitäts-Regression. Grobe Ordnung: ein
Arbeits-Sitzungs-Block; Details im GO-Gespräch.

## 6. Entscheidungen — ENT SCHIEDEN (Thomas, 08.11.2026 via Rückfrage-Runde)

1. ~~**Prioritäts-Wortlaut:** „geht dem Bewegungsast des Baums vor"~~
   **AUßER KRAFT durch §1b (Thomas 08.11. II): kein absoluter Vorrang —
   Mischungsgesetz.** Die Entscheidungen 2 (Wachzone `apDutyGuardDef`) und
   3 (Wrapper-Keys) gelten weiter, angepasst an Baum-Ast statt Vorrang-
   Block; Key-Zahl wird bei GO final geschnürt (Wrapper + Ast-Zeilen).
2. **Wachzone-Definition:** KEINE Generalisierung des Bestandskeys —
   stattdessen **eigener Duty-Varianten-Satz**: neuer Key
   `apTreeGuardDutyDef` („Bereich, in dem diese Mission entschieden wird"
   als Wachschutz-Definition), gerendert **nur** wenn Muster=guard UND
   Duty-Fakt vorliegt; der rebellen-zentrierte Originalsatz bleibt in
   allen Nicht-Duty-Missionen bit-identisch.
3. **Wrapper-Keys:** neue Keys **`apDutyHeader` + `apDutyFallback`**;
   `interactLine` bleibt unangetastet für seine dormante Bedeutung
   („erlaubte Interaktion").

Damit: **3 neue uiTacticalAI-Keys × 11 Sprachen** (De/En echt übersetzt,
Rest EN-Fallback wie gehabt). §4-Punkt 5 gilt in der Duty-Varianten-Form.
Mockup der Entscheidungsrunde: `~/.hermes/cache/scratch/duty_mockup.html`
(HOTH10 heute/vorgeschlagen + HOTH9 Replace-Fall, Original-Strings).
**Hinweis: Mockup zeigt Rev-2-Form (Vorrang) — nach §1b überholt; wird bei
GO auf Mischungsdarstellung (Escort-Gruppe + Gefechts-Gruppe parallel)
neu gebaut.**

## 7. QA-Population — der Detektor über ALLE 138 Missionen (08.11., real gelaufen)

Sprachneutral, rein strukturell (Kanon aller Mining-Kanäle):
(a) Variable, deren Schwellen-Trigger (`additionalTriggers`) auf einem
End-Event der **Imperiumsgewinn-Seite** („rebels lose") liegt;
(b) Setter-Event ist **nicht** das End-Event selbst (Selbst-Buchhaltung)
und **kein** reiner Kampf-/Runden-Blocker (dead/defeat/wound/round);
(c) Schwellenwert als Ziel-Zähler.

**Ergebnis (echt ausgeführt über `Resources/SagaMissions/*/*.json`):**
18 Roh-Treffer, nach (b)/(c) gefiltert **8 saubere Duty-Kandidaten + 6
Grenzfälle zur Text-QA**:

| Mission | Variable@Ziel | Setter (Beleg) |
|---|---|---|
| CORE16 | Terminals disabled @3 | Discard Terminal 1-3 |
| CORE23 | Imperial Recruits @16 | Empire 1-8 recruit (+Rebel Wounded) |
| CORE27 | Imperials escape @1 | extern (Flucht-Feld) |
| CORE28 | Caches destroyed @5 | Destroy Cache 1-8 |
| HOTH10 | Prisoners claimed @4 | Prisoner transferred |
| HOTH12 | Draylen executed @1 | Draylen executed |
| OTHER9 | Wookiees Bound @6 | Wookiee Bound/Freed ×16 |
| OTHER31 | Rebels Lose @1 | Cargo Position 16 (Bewegungsziel) |

Grenzträge (Buchhaltungs-Verdacht, Text-QA bei Umsetzung): CORE10 (Officer
freed/Door), EMPIRE2 (Recording Active), EMPIRE4 (Story-Flag), HOTH15,
OTHER6, OTHER7 (Defeat-Flags — vermutlich KEIN Duty). **Nicht erfasst und
ehrlich so benannt:** Duty-ohne-Zähler-Bauart (EMPIRE7 Push Lorn,
EMPIRE4 Arrest) — die bleiben vorerst Regel-Zeilen; Ausbaustufe
„Conditional-Duty aus Law-Zeilen" ist separierbar.

**QA-Gesetz bei Umsetzung:** Diese Tabelle ist der Goldene Vektor.
Pro Duty-Mission: Popup-Vektoren für EINE Escort- UND EINE Gefechts-
Aktivierung, gemischt-Vektor über 3 Runden (Seed-Stabilität der
Muster-Zeile), Bedrohungs-Override-Vektor (Escort unter Feuer →
Vergeltungsast), und die 130 Nicht-Duty-Missionen **bit-identisch**.
Keine Mission, die der Detektor nicht meldet, bekommt je einen Duty-Block;
kein Duty-Block ohne QA-Vektor. **EMPIRE3 ist Pflicht-Gegenvektor**
(Popup bit-identisch zu P36 — objactor-Zeilen unverändert, keine
Duty-Zeile) und in jeder Duty-Mission die Duplikat-Assertion aus §1b
Gesetz 5 (nie zwei Zeilen über dasselbe Objekt).
**Spaß-Kriterium (§1b) ist Teil der
Abnahme, nicht Wunschbild.**

## 8. Whitelist-Karte (P36-Lehre: jede neue Sache ist ein Whitelist-Fall)

Real geprüft in Board.cs/Doctrine.cs/TacticalAIBridge.cs/ActionBuilder.cs
(08.11.). **Erweiterung nötig:**

| # | Stelle | Befund | Folge für Duty |
|---|---|---|---|
| W1 | `Board.CounterTypes` = {woundAll, incapacitateAll, surviveRounds, capture} | HOTH10-Bauart („N Marker ans Ziel") hat KEINEN Typen — Loader wirft `bad victoryCounter` | Typ ergänzen (z. B. `transfer`) ODER auf `capture` mappen — GO-Entscheidung, sonst stirbt der Duty im Loader wie objactor in P36 |
| W2 | `Board.PostureNames` = {eliminate, stall, protect, deny} | vier Typen, alle belegt | KEIN neuer Posture-Typ — Duty-Modifikator schreibt nur in bestehende Zeilen |
| W3 | `protectedEntities`-Kanal (Board.cs:313) | existiert, validiert (begehbar, vitality≥1, keine Duplikate) — in **allen** ausgelieferten Missionen leer (nur Fixture `m2a1_protect_npc.json`) | Duty-Mining liefert den ERSTEN echten Füller; wirft die Validierung (Zielfeld nicht begehbar), statt nachzubiegen: eigener Kanal `dutyAnchors` mit eigener Validierung |
| W4 | `{schutz}`-Speisung (Doctrine.cs:157) | rendert aus `Contest[posture].targets`, bisher nur protect-Posture | Duty-Namen MUSS auch Escort-/Gefechts-Muster-Zeilen speisen (mustergeschützte Speisung, §1b Gesetz 4) |
| W5 | `FObjectThreatener` liest nur `Board.ProtectedEntities` (Figuren-zentriert, `Victory.cs:584` = `AliveOf("npc")`) | Marker/Tokens sind KEINE Figuren | Gesetz-4-Speisung läuft über die ZELLEN des Ankers (`VictoryHelpers.EntityCells`), nicht über Figuren-IDs — sonst toter Zweig |
| W6 | `UILanguage`-Schlüssel (~1591 `switch (basis)`) | Basis-Labels sind fix | KEIN neuer Basis-Wert geplant (Ziel-Schicht unberührt) → nothing to do; falls Explain je „duty" als Basis zeigt: Fall + Label-Key in ALLEN Sprachdateien (QA-Lektion 07.11.) |
| W7 | Mining-Artefakt-Doppelpfad (`Languages/En/tactical_mission_critical` + `Data/taktische_mission_critical`) | zwei Loader lesen denselben Satz | `duty`-Feld muss in BEIDEN regeneriert werden |

**Bewusst NICHT erweitert (Gesetz-5-Umkehrung der P36-Lehre):**
TryObjectTarget-Basis-Whitelist {card, economy, basis, proxy, objactor}
bleibt duty-frei (OBJEKT-Slot gehört der objactor-Klasse — kein Doppel,
keine Irritation); `ActionBuilder.allowed`
{target_id, states, mp, purpose, focus_cost, door, special} bleibt
duty-frei, weil der Duty-Zweig KEINE neuen Aktionsparameter emittiert
(gedruckte Anleitung; HOTH10-Marker ist offline-Buchhaltung, hat gar kein
AI-Ziel); `TacticalShippedIds` (M11) bleibt, Duty gilt nur für ausgelieferte
taktische Missionen; `PatternWeights`-Loader ist freier Key-Value-Raum
(Duty-Modifikator ist DATEN-Zeile, kein Code).
