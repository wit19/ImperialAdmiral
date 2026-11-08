# P37-QA — Abschluss-QA (review-only) — t_131a135b

Geprüft: Commits 8b9cb9f..HEAD (HEAD = a2c553b, main, tree clean).
Keine Produkt-Edits; einziges Schreibrecht: diese Datei (lokal via .git/info/exclude).

## Schwere-Matrix

| Schwere | Fund | Datei:Zeile | Beweis / Bewertung |
|---|---|---|---|
| BLOCKER | — keine — | | |
| MAJOR | — keine — | | |
| MINOR-1 | W4-Runtime-Aufrufer fehlt: `dutyAnchors`-Parameter hat keinen Runtime-Consumer | Doctrine.cs:325,388-390 | einziger Aufrufer mit Anker-Argument = Harness (SectionsDutyCore.cs:161-176); `SelectDoctrine` selbst hat Null Runtime-Aufrufer in Assets/Scripts (grep: nur VectorRowTests.cs:78 + Harness); Bridge ruft ForMission nur für Popup/Scrub (1939, 471). Officer-Texte nennen den Anker im Spiel noch nicht. **Offengelegt** in P37c-Handoff („Runtime-Aufrufer mit dutyAnchors existiert noch nicht") und MECHANICS:1591ff. Player-facing WIKI verspricht nichts davon → kein Release-Schaden, Follow-Up-Hop nötig. |
| MINOR-2 | `DutyRound.Combat()` ohne Runtime-Consumer | DutyQuota.cs (Combat()) | grep: nur SectionsDutyCore.cs:70,145 / SectionsDutyPopup.cs:106,131 / DutyCoreGate.cs:158. Runtime leitet Kampf invers über plan.Duty ab. Tot, aber test-zweckdienlich; CS0414-frei (öffentliche API). |
| MINOR-3 | `DutyAnchor.Threshold` / `.Law` ohne Runtime-Consumer | DutyAnchors.cs:49,51 | Loader-Evidenzfelder; Reader liefert Namen (+ Target-Feldnamen fürs Popup), Law/Threshold nur Harness-Beweise (SectionsDutyLoader.cs:105-106,206-207). Design-konform (Popup zeigt Gesetzestext, nicht Rohfeld), aber tot fürs Runtime. |
| MINOR-4 | Harness fail-soft bei fehlender duty-Zeile: checks-Gesamtzahl sinkt still (46591→46588, duty_loader 107→104) | SectionsDutyLoader.cs (Summen-Check 97-101) | In der Mutation probe gemessen. Der duties-Summen-Pin fing es („hoth10.duties: expected 1, got 0"), aber die Gesamtsummen-Delta wäre als Alarmzeichen besser. Gate compensiert fail-closed (counts == golden). |

## PASS-Liste (je ein Beweis)

1. **Canon-Additivität maschinell:** `git diff 8b9cb9f..HEAD -- Assets/Tests/Fixtures/ | grep -cE '^-[^-]'` = **0** (erwartet 0, keine disclosed flips). Numstat: duty_core 849/0, duty_loader 691/0, duty_popup 44/0. Docs additiv: WIKI 76/0, MECHANICS 129/0, CONCEPT 570/0.
   Gegenbeweis im frischen Worktree auf 8b9cb9f: **methodisch eingeschränkt** — `path.json`, `risk.json` und alle `*_record.json` sind git-ignored (check-ignore rc=0, 0 getrackt), fehlen im Clean-Checkout ⇒ Worktree-Harness misst nur 12467 checks (nicht 46298). Canon-Zahlgleichheit daher arithmetisch bewiesen: sum(PASS-zeilen HEAD) = 46591 exakt, duty-Sektionen 107+103+83 = 293, **46591 − 293 = 46298 = P37a-Endstand**, und kein Canon-Fixture wurde getoucht (minus-Zeilen 0).
2. **124-Ruhe + EMPIRE3-Gegenvektor:** Gate selbst nachgemessen: `DutyMineGate: ALL PASS | duty=7 borderline=7 ruhe=124 anchors=28`. EMPIRE3-Gegenvektor = six_groups-Golden (sessionKey `dutycore-six|EMPIRE3.json`), 12 Runden, im Gate + im Python-Twin MATCH. objactor-Check unverändert: `PASS marker_object.json (121 checks)` = P36-Referenzwert. Popups bit-identisch: alle P36-Canon-Vektorfixtureszahlgleich + duty_popup pins HOTH10-DE/EN-Popup auf realer Duty-Fact (83 PASS).
3. **Meta-Law / Mode-Gate taktisch-AUS:** alle neuen Call-Sites gelesen, nicht Prosa: `EnsureRound` nur hinter `if (!ActiveNow()) return;` (TacticalAIBridge.cs:681→691, ComputeDutyRound :752); Popup-Zeilen :1472/:1486 nur hinter Popup-Guard :1112; `ScrubDutyBriefingText` explizit gated :489 + DutyMissionLive fail-closed :461-478; MissionEventHandler.cs:31 / HeroDashboard.cs:33 / InputBox.cs:42 / PromptBox.cs:57 rufen die gated Seam. SelectDoctrine null-default ⇒ refs-Kette bit-identisch.
4. **Fairness:** kein neuer Reader toucht damage/health/hidden data: DutyAnchors liest Artefakt (Name+Typ, nie Koordinaten) + mapEntities-GUID-Wahrheit; DutyQuota.Compute rein (sessionKey, gerollte Patterns, Stativ-Gesetz, öffentliche duty-Facts, patternWeights); DutyScrub nur Text. SessionKey = campaignGUID|missionID. Duty-Daten == öffentliche Missionsdaten: EN-Artefakt ist öffentlich-reviewable Deklaration (`_meta` note), Reader-Kette Artefakt→ForMission→Popup belegt. Gate: DutyCoreGate Fairness-Scan PASS.
5. **Null-Law-Mutationsprobe:** Backup → /tmp, HOTH10 `dutyAnchors`→class none (count-asserted, kein git checkout): Harness ROT (duty_loader 5, duty_core 9, duty_popup 4 fails; FAIL-Zeilen zitieren Gesetz: „hoth10.duties: expected 1, got 0", escort_head De/En false), Gate **rc=1** mit FAIL-Zeilen „duty count == 7 (found 6)", „ruhe count == 124 (found 125)", Pfad-Doppelpfad 27/28, duty-7-Goldenliste ohne HOTH10. Chirurgisch zurück: `cmp` identisch BEIDER Artefakte, sha256 -c OK, Harness wieder **46591 ALL PASS**, git status clean.
6. **Quota-Grenzen + Seed-Stabilität (Python-Zweit-Twin, unabhängig implementiert):** fnv1a64/fmix64/SessionSeed-0-Guard/Quoten-Ziehung/Ranking-Ordinal-Tiebreak/Demotion/Floor nachgebaut — **15/15 Golden-Vektoren MATCH** (hoth10 r1-r3 + six r1-r12 inkl. vollständiger Explain-Zeichenketten); Band: cap=ceil(n/2) ≤ n−1 für n≥2 (Verletzungsliste leer n=2..99), nie 0 wenn mobil + duty; seed-stabil (identische Eingabe ⇒ identisch).
7. **Dead-Key-Hunt:** Runtime-Consumer gefunden für: `objectiveCounter` (Board.cs:346 + VictoryCounter Valid-Set Victory.cs:351), `dutyModifiers`/`DutyMods` (DutyQuota.cs:163-167 Matrix-Pfad), `apDutyEscortTree`/`apDutyQuotaLine` (Bridge :1474/:1486), `plan.Duty/DutyTarget/DutyQuotaLine` (:1256-1263 → :1472), `DutyAnchors.ForMission` (:471, :1939), `DutyQuota.Compute/DutyOf` (:752, :1256), `Scrub` (3 UI-Seiten + HeroDashboard). Tote Kandidaten → MINOR-1..3 (alle offengelegt oder test-only).
8. **Sprachen 11/11 + De-Umlaute + Scrub-Inventar:** apDutyEscortTree/apDutyQuotaLine in allen 11 ui.json (diff +2/0 je Datei, append-only); De echte Übersetzung inkl. §9.4-Reichweiten-Definition; übrige 10 = EN-Kopien gemäß M6c-Flotten-Gesetz (duty_popup pinnt 9 byte-exakte EN-Kopien); WIKI enthält 17 Zeilen mit echten Umlauten. Scrub-Inventar: DutyScrub.cs ByMission = genau HOTH10+OTHER9 (Rest der 7 duty-Missionen REF-clean, Gate guard), DutyScrubGate 98 PASS + globaler Leak-Scan leer; Nicht-Duty-Missionen byte-identisch (Artefakt-Analyse: 138/138 EN-Missionen ändern NUR den dutyAnchors-Key; DE rows+columns byte-identisch).
9. **CJK-Scan:** Python-Bereichsscan über ALLE + Zeilen des Range-Diffs: 0 Treffer (Han/Kana/Halbfett-Blöcke). Hinweis: `grep -P '\p{Han}'` false-positived auf U+00B7 — Python-Scan ist maßgeblich (Skill bereits aktualisiert).

## Zusätzlich gemessen (QA-Trio, eigene Läufe)

- Harness: `ALL PASS | checks=46591 fails=0`, rc=0 (61 Fixture-Dateien, Summe exakt 46591).
- Gate: rc=0, **6243** `^PASS`-Zeilen, DutyMineGate/DutyCoreGate/DutyScrubGate ALL PASS.
- build.sh: `APP: rc=0 errors=0 files=291` / `TESTS: rc=0 errors=0 files=10`.
- taboo_lint: GREEN, self-tests 12/12.
- Tree vor/nach QA clean (a2c553b, main); keine Produkt-Commits, keine Edits.

## Release-Empfehlung

**Freigabeempfohlen (0 BLOCKER, 0 MAJOR, 4 MINOR).** MINOR-1 (W4-Runtime-Feeder)
ist redlich dokumentiert und player-seitig nichts-versprechend; als Folgehop klinken
(Anker-Namen in die Officer-Refs einspeisen, Popup-Canon-Vektoren gegenchecken).
MINOR-2/3 sind bewusste Test-/Evidenz-APIs; MINOR-4 wäre ein Harness-Härtungs-
Follow-Up (Summen-Delta-Alarm). TODOs Thomas' Unity-Maschine bleiben wie in den
Handoffs benannt: .meta-Dateien für DutyAnchors.cs / DutyQuota.cs / DutyScrub.cs.
