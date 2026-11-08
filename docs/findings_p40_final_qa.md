# P40 — Abschluss-QA über die GESAMTE Strecke 8b9cb9f..HEAD (P37 + P38 + P39) — t_8ef69800

Geprüft: Commits 8b9cb9f..1c2e118 (HEAD = P39-Commit des Controllers, Tree clean).
Review-only; kein Produkt-Edit, kein Commit. Einziges Schreibrecht: diese Datei
(lokal via .git/info/exclude — Linksvorbehalt wie P37-QA; der Controller committet bei Wunsch).

## Schwere-Matrix

| Schwere | Fund | Datei:Zeile / Ort | Beweis / Bewertung |
|---|---|---|---|
| BLOCKER | — keine — | | |
| MAJOR | — keine — | | |
| MINOR-A | CJK-Fund im Gesamt-Diff: ein korruptes Wort („Re“ + U+8F93 U+5165 statt „Ein“→„eingabe“) steht im P37-QA-Befundtext selbst | docs/findings_p37_qa.md:25 | Python-Bereichsscan über alle 9399 Plus-Zeilen des Range-Diffs: 1 Treffer (U+8F93 U+5165; Bytes hier bewusst als Flucht geschrieben, damit dieses Protokoll den Scan selbst nicht mehr rot macht — die Lehre von MINOR-A in eigener Anwendung). Ironie: die Datei, deren PASS-9 „CJK-Scan 0 Treffer“ attestiert, brachte ihr eigenes CJK mit dem QA-Commit 963fa27 in den Baum (der Scan damals lief VOR dem Commit der Datei). Doku-only, kein Produkt-/Runtime-Touch; Ein-Wort-Fix im Doku-Hop. |
| MINOR-B | P38-Handoff-Text beschreibt sein eigenes Bedrohungs-Gatter falsch (Klassenfehler, nicht Codefehler) | Handoff-Kommentar t_11ebb843 vs Core/PreyQuota.cs:124-152 | Handoff behauptet: „Bedrohung>=15 => vollen Würfel, <15 => kappt auf k=1; an EMPIRE3: threat 1..14 => k=1..5, >=15 => k=1..6“. Gemessen: das Schiff-Gesetz ist ein BOOLESCHES Sektions-Mitlocations-Gatter (PreyThreat.TreatSectionOf, Bridge:3226; Popup-Zeile apPreyThreatLine/Rel) — numerischer Klemmer existiert nirgends; k-Band am Twin gemessen [1..cap=ceil(n/2)]=[1..5] für n=9 (Twin 20/20 MATCH), **k=6 für die EMPIRE3-Kohorte unerreichbar**. Code + 7 benannte threat_vectors + Gate: PASS; nur das Persistat Handoff-Text ist irreführend (Klasse des P37-QA-MINOR-1-Mislabels). Massstab ist der Code — der ist gut. |
| MINOR-C | Thomas-Entscheid aus P37c-Handoff (Offener Punkt 3: Duty-Ast gedruckt-VOR-Musterbaum statt Bewegungsbaum-ERSETZUNG im TreeFor-Schlüssel, „bitte auf Brett-QA entscheiden“) ist durch KEINE Folgekarte aufgegriffen worden | Handoff-Kommentar t_e80246ed, Punkt 3 | Nachgemessen: findings_p37_qa.md erwähnt ihn nicht; P38/P39-Handoffs erwähnen ihn nicht. shipped ist die gedruckte Form (G1-interpretiert, Popup-Pins duty_popup.json). Entscheide-Tracking-Lücke: bewusst offen ODER vergessen — hier als „in keiner QA-Liste aufgetaucht“ gemeldet. Kein Spielerschaden unter der aktuellen Form. |
| MINOR-D | Doku-Zwei-Zeiler (Build-Stempel P35 + ACHSE::-Zeile) nach Controller-Aufschiebung „nach Kettenende als Einzeiler“ weiterhin nicht geschrieben | Controller-Kommentar t_b3eaef5c; docs/ grep | grep über docs/*.md nach Build-Stempel-Doku: 0 Treffer (TacticalAiBuildStamp.cs undoku­mentiert). ACHSE:: hat immerhin einen WIKI-Absatz (docs/WIKI_tactical_ai.md:1083), der Build-Stempel keinen. Kette ist zu Ende (P39) — Zeilen sind jetzt fällig im nächsten Doku-Hop. |

## PASS-Liste (11 Prüfpunkte, je eigene Messung am HEAD 1c2e118)

1. **Trio komplett selbst gefahren:** Harness `ALL PASS | checks=46883 fails=0` rc=0 (Summe
   der pro-Datei-Zeilen = exakt 46883, arithmetischer Beweis). Gate rc=0, **6260** `^PASS`-
   Zeilen, 0 FAIL-Zeilen, letzte Zeile ALL PASS (DutyMineGate 7/7/124/28, DutyScrubGate,
   NeutralPreyGate 17, UiCompleteness missing=0). build.sh `APP: rc=0 errors=0 files=293` /
   `TESTS: rc=0 errors=0 files=10`. taboo_lint GREEN, self-tests **12/12**, rc=0.
   **Keine Abweichung** zu den Karten-Handoffs (P39: 46883/6260/293/10/12; P38-Basis 46836+47
   = 46883; P37-QA 6243+17 Prey-Sektion = 6260 — alles zahlgleich).
2. **Canon-Bilanz GESAMTE Strecke:** `git diff 8b9cb9f..HEAD -- Assets/Tests/Fixtures/` =
   **minus-Zeilen 0**, plus 1667 (numstat: duty_core 849/0, duty_loader 691/0, duty_nameless
   19/0, duty_popup 44/0, prey_core 64/0 — alle rein additiv). Zero undisclosed Flips.
   Eingetretene disclosed Verhaltensänderung der Strecke (P39): CORE23-Escort-Popup-Kopfzeile
   benannt-dangling → namenlos — im P39-Handoff + Commit 1c2e118 disclosed, Seam nachgemessen
   (Bridge:1568: Namenlos-Key nur bei TargetName „“, benannter Pfad bit-identisch).
3. **P37-Referenz am neuen HEAD nachgemessen:** DutyMineGate `duty=7 borderline=7 ruhe=124
   anchors=28` (exakt P37-Golden); 28 Anker ohne Feed-Bleed (NeutralPreyGate-Zeile); marker_object
   121 checks (P36-Referenzzahl); 11/11 Sprachen für alle 6 neuen Keys; Scrub-Inventar: nur
   HOTH10 (13) + OTHER9 (14) Inventartreffer, REF-clean-Guards für HOTH12/OTHER31,
   Fremd-Key-never-scrubs 5×. **Dead-Key-Hunt NEU:** ForMission hat einen Runtime-Consumer —
   ComputeDutyRound (Bridge:2024, definiert; aufgerufen :789 in EnsureRound hinter ActiveNow) —
   der P37-QA-MINOR-1-Tabellentext („Bridge ruft ForMission nur für Popup/Scrub“) war ab
   963fa27 ein Mislabel, hier mit eigenem grep am HEAD belegt. Was nach P39 tot ist, ist
   ausdrücklich quittiert: SelectDoctrine (KEPT+DECLARED Doctrine.cs:324-339 + MECHANICS:1637-1649,
   null Runtime-Aufrufer nachgemessen), DutyAnchor.Threshold/.Law (EVIDENZFELD-Deklaration
   DutyAnchors.cs:49-62, Loader-Pins jetzt unbedingt), DutyRound.Combat() (ENTFERNT — Definition
   weg, Rest nur Kommentar; CombatOf-Local in SectionsDutyCore:235/SectionsDutyPopup/DutyCoreGate:37).
   PreyRound.IsChosen/InCohort sind NICHT tot (Bridge:1304/3205/3206). Kein unquittierter Leichnam.
4. **P38-Scope-Härte:** Zensus idempotent NACHGEPINNT: `gate -- census` + p38_census_doc.py
   neu gefahren → sha256 **3ed1dc75d317…** byte-identisch, Tree clean. Extensionale Vorgabe
   gegen Tabelle gemessen (117 Zeilen): Protester 9/9 DRIN (Ja), Refugee 4/4 DRIN (Ja; HOTH2-
   Refugee ehrlich NICHT im Feed), RAUS-Liste (Checkpoint/Wall/Weak Point/Support Point/Beacon/
   Cell/Engine Core/Data Core/Barricade/Window/Security Station/alle Türen/alle Terminals) —
   **0 Verletzungen** (Data Core CORE24 = jagt P1, also Beute-frei). EMPIRE3: 9 Protester-Zeilen,
   k-Band [1..5] (cap=ceil(9/2), Twin **20/20 MATCH**) ⇒ nicht mehr „alle auf Demonstrant“.
   Seed pro Runde konstant: ComputePreyRound einmal pro EnsureRound (Bridge:799), Aktivierungs-
   reset nur am Marker (:2817/:294), golden-rerun-Pins im Fixture. Bedrohungs-Gatter-Fall: 7
   benannte threat_vectors (unit_sec/prey_sec/none/multi_gap/multi_back/two_heroes_prey/no_hero)
   + Konsument Bridge:3226 mit Quota-vor-Gatter-Reihenfolge (:3222 Kommentar + Code).
   Nicht-neutrale POLAR-Missionen: CORE27 im Zensus nur Türen (kein Bystander ⇒ Gesetz inaktiv),
   alle Canon-Vektoren minus-0 (Fixtures), Gate/Harness grün ⇒ bit-identisch gegen P37-Endstand.
5. **P39-Abrechnung, je eigener Beweis am Code (nicht am QA-Text):** MINOR-1a SelectDoctrine:
   null Runtime-Aufrufer per grep über Assets/Scripts (nur Doku-Kommentare + Core-Definition),
   Deklaration + MECHANICS-Abschnitt vorhanden — Entscheidung FESTGELEGT ist belegt. MINOR-1b:
   apDutyEscortTreeNameless in 11/11 ui.json; DE echte Übersetzung (gemäß/ä), 9 EN-Kopien
   byte-identisch zum En-Original; Seam nach TargetName==„“ (Bridge:1568); Prämisse am Artefakt
   gepinnt: Sektion duty_nameless.json **47 checks** grün. MINOR-2: Combat()-Definition aus
   DutyRound entfernt (grep: keine Definition, keine Konsumenten — nur Historie-Kommentare),
   Single Source plan.Duty/CombatOf lokal in Harness+Gate; Sektionszahlen 107/103/83 zahlgleich.
   MINOR-3: EVIDENZFELD-Deklaration gelesen; Loader-Checks laufen unbedingt (SectionsDutyLoader
   :104-110 „d0 = duties.Count>0 ? duties[0] : null“ + benannte duty_shipped[*]-Checks).
   MINOR-4: **eigene Mutationsprobe gefahren** — HOTH10 class „duty“→„none“ (Zeile 10939,
   count-asserted): Harness FAILURES **fails=21 BENANNT** (u.a. duty_shipped[HOTH10.json].duties/
   .anchors/.variable/.threshold/.law/.tfNames, hoth10.duties, hoth10.target …) bei **konstanter
   Gesamtzahl checks=46883** (Genau der Härtungs-Sinn: kein lautloses Sinken). Restore
   cmp+sha256-identisch, Harness danach wieder ALL PASS.
6. **Modus aus / Legacy:** alle neuen Call-Sites gate-geprüft: ComputeDutyRound/ComputePreyRound
   nur in EnsureRound hinter ActiveNow() (:374: Settings.Enabled + mission-frozen aiMode!=legacy);
   Popup-Zeilen hinter Popup-Guard; ScrubDutyBriefingText mit `if (!TacticalMissionActive())
   return text;` (:511) — Legacy-Pfad ruft denselben Code, bekommt ihn aber nie; entityType
   producer-seitig nur im Tactical-Feed (data-existence, Hand-Fixtures ohne Key ⇒ Null-Law);
   PreyRound-Marker null outside tactical. Canon-Beweis: Fixtures minus-0 (alle Legacy-Vektoren
   inkl. aller Nicht-Duty-/Nicht-Prey-Missionen erhalten), 46883 zahlgleich über die ganze
   Strecke, 138/138-Missionen-Corpus-Analyse (P37-QA PASS-8) gilt weiter (kein Produkt-Diff
   an Missionsdateien außer dem disclosed P39-Popup-Fall). Legacy-Fixtures: keine Änderung
   (Diff-Name-Status der Fixtures: nur 5 neue duty/prey-Dateien).
7. **Mutation-Proben (P38-Gatter):** (a) Fixture-Pin gebrochen (prey_core.json: golden k=5→7
   outside Band): Harness benannt FAIL `quota.em3_n9.r1.explain: expected …k=7…, got …k=5…`,
   rc=1, checks=46883. (b) Neutral-Klassifikation im ARTEFAKT gebrochen (EMPIRE3.json Protester 1
   entityType token→door): Harness bleibt ALL PASS — **ehrliche Feststellung**: die (A)-Sektion
   pinnt die im Gen-Modus eingebetteten Mine-Vektoren, live-artifact-Bindung schützt das GATE:
   rc=1 mit benannten FAILs „critical row drift b76939b9… in EMPIRE3.json“, „census: Protester
   x9 token+jagt+BYSTANDER“, „doc classification matches the live derivation exactly
   (drifts=1)“. Beide Restore: cmp + sha256 byte-identisch, git status clean, Trio danach grün.
8. **CJK/Codepage-Scan über den Gesamt-Diff (Python-Bereichsscan, maßgeblich):** 9399
   Plus-Zeilen, **1 Treffer**: U+8F93 U+5165 in docs/findings_p37_qa.md:25 (Wort „Re“+Zeichen) → MINOR-A.
   Produkt-/Asset-seitig 0 Treffer. (Hinweis wie P37-QA: grep -P \p{Han} false-positived auf
   U+00B7, deshalb Python-Scan.)
9. **Fairness gesamt:** keine neue Informationsquelle außer Feed/Karten/Seed. Neue Flächen
   einzeln gelesen: entityType = authoretes Missionsdatei-Feld (öffentlich wie Buttontexte;
   Producer liefert nur, CORE richtet); PreyThreat liest ausschließlich die bestehenden
   HeroInSection-Feed-Fakten („nothing new surveyed“-Kommentar bestätigt im Code); PreyQuota
   rein über sessionKey/Runden- salts/ids/gewürfelte Muster; Gate-Literal-Scan „no law string-
   literal names a mission or an authored person“ PASS; entityPosition steht nur als Spalte der
   Data-Review-Tabelle (P25-DP-Konvention „authored review copy, readers ignore unknown
   columns“) — grep: DutyAnchors.cs liest kein Position-Feld.
10. **Rückstände-Liste (Status je Zeile):**
    - Unity-.meta-Bündel: **bewusst offen** (Thomas' Unity-Maschine). Gemessen: 45 getrackte
      Assets-Dateien ohne .meta — Familie ab M14e inkl. Chain-Dateien DutyAnchors.cs,
      DutyQuota.cs, DutyScrub.cs, NeutralPrey.cs, PreyQuota.cs + 5 Fixture-JSONs
      (duty_core/loader/nameless/popup, prey_core). Repo-Präzedenz dokumentiert (P37b OP-3).
    - Doku-Zwei-Zeiler (Build-Stempel, ACHSE::): **offen, jetzt fällig** → MINOR-D.
    - Thomas-Entscheid M14f-Jäger-Text: **behoben vor der Kette** (d629e61 „M14g: Thomas'
      Entscheidung — Jaeger-Text bleibt M14d-Fassung“). Nicht offen.
    - Thomas-Entscheid B5: **behoben vor der Kette** (4c60408 M6s-B5, Wachzone im Standard-
      Wachschutz-Baum). Kein pending B5 in den Konzept-Dokumenten (grep 0).
    - Thomas-Entscheid P37c-OP-3 (Duty-Ast-Form): **offen und spurlos** → MINOR-C.
    - P37a OP 1-5: Golden-28-Pin intentional (nachgemessen 28); CORE23 9× endEvents ehrliche
      Rohabbildung; dp_anchors nicht duty-erweitert (Abdeckung DutyMineGate, beabsichtigt);
      buttonText-Exportform ok; Repo-Root-Hartpfad-Trick beim Controller als Migrationsnotiz
      aufgenommen.
    - P37b OP-1 Data-Pfad ohne C#-Leser: **bewusst offen** (Languages-Kanal ist der Leser,
      dokumentiert). OP-2 knownEntityGuids-Policy: **behoben** — Bridge füttert live
      mapEntities-Wahrheit (DutyMissionLive :487-495, ComputeDutyRound :2033-2037).
      OP-4 Popup/Scrub: **behoben** durch P37d.
    - P37c OP-1 Hop-D-Werte: **behoben** (P37d). OP-4 Quota-Zeile auch im Gefechts-Popup:
      **behoben** (P37d Escort-only, duty_popup pinnt „Gefecht 0 Zeilen“).
    - P37d OP-2/3 (9 kaputte Missions-Injektionen JABBA5_DE/_ES, LOTHAL*_NL, OTHER1x_NL;
      HOTH10 Es/Nl missionInfo„“): **vorbestehend, bewusst offen**, Kette ausser Scope,
      Gate-Rohscan deckt Leaks ab (0).
    - P37-QA a)-f): a/b/c MINORs **behoben durch P39**; d) Worktree-Methode = dokumentierte
      Methodengrenze; e) Findings-Commit **behoben** (Controller 963fa27); f) .meta → Bündel.
    - P38 OP: .meta-Bündel; Playtest-Blicke EMPIRE3-Popups ** offen** (Board-Maschine);
      Commit **behoben** (f771152). P38-Risiko-MINOR-1..3 = disclosed Design, keine Wunde.
    - P39 OP: .meta duty_nameless.json → Bündel; Playtest-Blick CORE23-Popup **offen**;
      Commit **behoben** (1c2e118).
11. **Urteil:** siehe Freigabe unten.

## Zusätzliche Zahlen (Eigene Läufe, alles rc wie angegeben)

- Harness 46883/0 rc=0 · Gate rc=0 6260 ^PASS · build APP 293 / TESTS 10 rc=0 · taboo 12/12.
- Twin p38_prey_twin.py: TWIN OK vectors=20 matches=20 fails=0; k-Span {1,2,3,4,5}, caps {1,5}.
- Census-Idempotenz: sha256 3ed1dc75d317… vor==nach Generator-Neuauflage, Tree clean.
- Mutationen (3): p1 21 benannte Fails bei 46883 konstant · p2a 1 benannter Fail (quota.em3_n9.
  r1.explain) · p2b Gate rc=1 3 benannte Drift-Fails (Harness blind per Design — Mine-Embedding).
  Alle Restores cmp+sha256-identisch; Abschluss git status LEER.

## Freigabe-Empfehlung

**Freigabe EMPFOHLEN (0 BLOCKER, 0 MAJOR, 4 MINOR).**
Die Strecke ist Canon-additiv (minus-0 über die gesamte Strecke), fail-closed gemessen
(7/7/124/28, Zensus drift=0, Summen-Alarm benannt), modus-gated (Legacy bit-identisch),
fairness-sauber und sprachvollständig (11/11, DE echt, 9 EN-Kopien byte-exakt).
Die 4 MINORs sind Text-/Tracking-Natur (Doku-CJK-Artefakt, Handoff-Prosa-Mislabel,
zwei offene Doku-/Entscheide-Punkte) — keiner blockiert ein Release. Vor dem nächsten
Doku-Hop: MINOR-A beheben, MINOR-D nachziehen, MINOR-C auf Thomas' Liste setzen.

## Nachbau-Rezept (exakte Befehle)

```bash
cd /home/open/work/ia-ai/ImperialCommander2
dotnet run --project tools/TacticalAiHarness -- ImperialCommander2/Assets/Tests/Fixtures/tacai
dotnet run --project tools/TacticalAiGate/gate.csproj          # rc=0, 6260 ^PASS
bash ~/work/ia-ai/compile_check/build.sh                       # APP 293 / TESTS 10
python3 tools/taboo_lint.py                                    # 12/12 GRUEN
python3 tools/p38_prey_twin.py                                 # 20/20
git diff 8b9cb9f..HEAD -- ImperialCommander2/Assets/Tests/Fixtures/ | grep -cE '^-[^-]'   # 0
# Census-Idempotenz:
dotnet run --project tools/TacticalAiGate/gate.csproj -- census > /tmp/p38_gate_census.log
python3 tools/p38_census_doc.py && sha256sum docs/CENSUS_beute_objekte.md   # 3ed1dc75d317…
# CJK-Bereichsscan ueber den Gesamt-Diff: Python-Scan ueber '^\+'-Zeilen (grep -P ist unzuverlaessig)
# Mutationen (backup + cmp/sha256-Restore, wie in PASS-5/7 beschrieben):
#  En-Artefakt :10939 class duty->none  | prey_core.json golden k=5->k=7 | EMPIRE3 Protester-1 entityType 4->1
```
