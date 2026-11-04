# Sektions-Zensus: wie grob ist der Sektionsraum wirklich?

Vom Harness-Hop S-B generiert (`tools/section_census.py`; nach jeder
Mission-Korpus-Aenderung neu laufen lassen). Gezaehlt wird NUR das, was in den ausgelieferten `SagaMissions/*.json` steht - keine
Erinnerung, keine Schaetzung.

## Verteilung der Sektionsanzahl ueber alle 138 Missionen

| Sektionen | Missionen | Beispiele |
|---:|---:|---|
| 1 | 33 | CORE11, CORE13, CORE14 |
| 2 | 46 | BESPIN4, BESPIN6, CORE16 |
| 3 | 31 | BESPIN1, BESPIN3, BESPIN5 |
| 4 | 19 | CORE1, CORE10, CORE31 |
| 5 | 2 | JABBA2, OTHER30 |
| 6 | 2 | BESPIN2, LOTHAL3 |
| 7 | 1 | HOTH15 |
| 15 | 1 | OTHER24 |
| 21 | 2 | EMPIRE6, EMPIRE9 |
| 23 | 1 | EMPIRE3 |

- Kleinste Mission: 1 Sektionen; groesste: 23; Median: 2.
- Im Konzept-Maßstab 2-6 Knoten liegen 100 von 138 Missionen (72%).
- Beispiel CORE1: 4 Sektionen (Start + 3 unsichtbare).
- Unsichtbar-bis-aktivierte Sektionen pro Mission: min 0, max 22 - genau diese Sektionen ueberwacht SectionActivated im Feed.
- Platzhalter-Sektionen mit NULL-GUID im Korpus: 0 - die Diff- maschine des Feeds behandelt jede GUID gleich, auch diese.

## Warum das die Modul-Groessenordnung klaert

Der Sektionsraum hat pro Mission so viele Knoten wie die Mission Sektionen traegt: Hauptmasse 1-6 Knoten (133/138), zwei Ausreisser-Autoren gehen bis 23 (EMPIRE3) und 15 (OTHER24). Der SectionGraph bleibt damit ein Graph unter 25 Knoten - BFS-Distanz und Maske sind Billigoperationen, die Grundgesetz-Pruefung (durchlaessig, Untergrenze, kein closed-Status) laeuft in jedem Fall durch. Die Ein-Sektions-Missionen sind legitim: dort ist der Sektionsraum bewusst still (eine Sektion = keine Distinktion), Masken bleiben bei der Wahrheit von einer Kandidatin.
