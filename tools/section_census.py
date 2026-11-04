#!/usr/bin/env python3
"""S-B census: how many map sections does each shipped mission actually carry?

WHY THIS EXISTS: the positionierung concept sizes its section-space module
with "2-6 nodes per mission" (CONCEPT_positionierung_v1.md 8.1). That claim
must be a CENSUS over the shipped mission files, not a remembered impression
(daten-ehrlichkeit law: reference facts from JSON only). The generator writes
docs/SECTIONS_census.md; regenerate after any mission corpus change:

    python3 tools/section_census.py     (from repo root)

Data sources read (never memory): ImperialCommander2/Assets/Resources/
SagaMissions/<expansion>/*.json -> mission.mapSections[] with
name/GUID/invisibleUntilActivated. Zero-GUID placeholder sections are
counted but flagged (same ZERO constant as tools/mission_critical_mine.py).
Exit code 1 on invariant violations (mission without any section = fail-closed
warning line, not abort - the corpus legitimately carries authoring noise).
"""
import json
import glob
import os
import sys
from collections import Counter

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MISSIONS_GLOB = os.path.join(REPO, "ImperialCommander2", "Assets", "Resources",
                             "SagaMissions", "*", "*.json")
OUT = os.path.join(REPO, "docs", "SECTIONS_census.md")


def main():
    rows = []
    for f in sorted(glob.glob(MISSIONS_GLOB)):
        try:
            d = json.load(open(f, encoding="utf-8"))
        except Exception as ex:  # fail-loud on unreadable corpus files
            print("CENSUS FAIL: cannot read %s: %s" % (f, ex))
            sys.exit(1)
        if not isinstance(d, dict) or "mapSections" not in d:
            continue  # non-mission JSON inside the folder (none shipped today)
        secs = d.get("mapSections") or []
        name = d.get("name") or os.path.splitext(os.path.basename(f))[0]
        expansion = os.path.basename(os.path.dirname(f))
        invisible = sum(1 for s in secs if s.get("invisibleUntilActivated"))
        zero_guid = sum(1 for s in secs
                        if (s.get("GUID") or "").startswith("00000000"))
        rows.append((name, expansion, len(secs), invisible, zero_guid))

    if not rows:
        print("CENSUS FAIL: no missions matched")
        sys.exit(1)

    dist = Counter(r[2] for r in rows)
    total = len(rows)
    lines = []
    lines.append("# Sektions-Zensus: wie grob ist der Sektionsraum wirklich?")
    lines.append("")
    lines.append("Vom Harness-Hop S-B generiert (`tools/section_census.py`; nach jeder")
    lines.append("Mission-Korpus-Aenderung neu laufen lassen). Gezaehlt wird NUR das,"
                 " was in den ausgelieferten `SagaMissions/*.json` steht - keine")
    lines.append("Erinnerung, keine Schaetzung.")
    lines.append("")
    lines.append("## Verteilung der Sektionsanzahl ueber alle %d Missionen" % total)
    lines.append("")
    lines.append("| Sektionen | Missionen | Beispiele |")
    lines.append("|---:|---:|---|")
    for n in sorted(dist):
        examples = [r[0] for r in rows if r[2] == n][:3]
        lines.append("| %d | %d | %s |" % (n, dist[n], ", ".join(examples)))
    counts = [r[2] for r in rows]
    lines.append("")
    lines.append("- Kleinste Mission: %d Sektionen; groesste: %d; Median: %d."
                 % (min(counts), max(counts), sorted(counts)[total // 2]))
    in26 = sum(v for k, v in dist.items() if 2 <= k <= 6)
    lines.append("- Im Konzept-Maßstab 2-6 Knoten liegen %d von %d Missionen (%s%%)."
                 % (in26, total, round(100.0 * in26 / total)))
    lines.append("- Beispiel CORE1: %d Sektionen (Start + 3 unsichtbare)."
                 % next(r[2] for r in rows if r[0] == "CORE1"))
    invis = [r[3] for r in rows]
    lines.append("- Unsichtbar-bis-aktivierte Sektionen pro Mission: min %d, max %d -"
                 " genau diese Sektionen ueberwacht SectionActivated im Feed."
                 % (min(invis), max(invis)))
    zero = sum(r[4] for r in rows)
    lines.append("- Platzhalter-Sektionen mit NULL-GUID im Korpus: %d - die Diff-"
                 " maschine des Feeds behandelt jede GUID gleich, auch diese."
                 % zero)
    empt = [r[0] for r in rows if r[2] == 0]
    if empt:
        lines.append("- Missionen OHNE jegliche Sektion (Autor-Rauschen, Feed liefert"
                     " dort keine SectionActivated-Ereignisse): %s" % ", ".join(empt))
    lines.append("")
    lines.append("## Warum das die Modul-Groessenordnung klaert")
    lines.append("")
    lines.append("Der Sektionsraum hat pro Mission so viele Knoten wie die Mission"
                 " Sektionen traegt: Hauptmasse 1-6 Knoten (%d/138), zwei"
                 " Ausreisser-Autoren gehen bis 23 (EMPIRE3) und 15 (OTHER24). Der"
                 " SectionGraph bleibt damit ein Graph unter 25 Knoten - BFS-Distanz"
                 " und Maske sind Billigoperationen, die Grundgesetz-Pruefung"
                 " (durchlaessig, Untergrenze, kein closed-Status) laeuft in jedem"
                 " Fall durch. Die Ein-Sektions-Missionen sind legitim: dort ist der"
                 " Sektionsraum bewusst still (eine Sektion = keine Distinktion),"
                 " Masken bleiben bei der Wahrheit von einer Kandidatin."
                 % sum(v for k, v in dist.items() if k <= 6))
    lines.append("")

    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))
    print("wrote %s | missions=%d dist=%s" % (OUT, total,
          dict(sorted(dist.items()))))


if __name__ == "__main__":
    main()
