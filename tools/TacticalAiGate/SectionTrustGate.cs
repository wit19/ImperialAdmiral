// S-C live gate (card t_865d5997): the SHIPPED data contracts of the
// Eigen-Glaube hop, zero fixtures (pipeline law):
//   1. tactical_section_trust.json: schema tacai.section_trust/1, rows are
//      sane rationals (1 <= num <= den), ids unique, exactly ONE default
//      row, and the default is the concept-10 Zielwert 2/3 (moving it is a
//      concept+gate+data decision in one commit - this line goes red first).
//   2. The skill curve (Thomas-Gesetz 04.11.): refinement/clamp/mask-trust open at
//      S3+ only; the exception classes fire on a COARSE projection too
//      (Fakt-Reaktion ist Fairness) - run live, not read from a table.
//   3. tactical_mission_critical mining: DoorOwnersOf and MissionDoorEdges
//      are cross-checked against an INDEPENDENT re-read of the shipped
//      table (not the miner's own output): completeness over unswapped door
//      rows, runtimeSwapped doors never owners, no edge endpoint outside
//      the mission's own sections list, no invented edge, dedup, and the
//      Bespin 1 Corridor<->Catwalk door pair must actually come out of the
//      real table (a zero-edge corpus would silently dead-wire the transit
//      law - the ignored-lane failure class).
//   4. Calibration audit-channel law: SectionCalibration and its report
//      tokens appear in NO runtime decision file (grep over Assets/Scripts;
//      the harness is its only caller by construction of this check).
using System;
using System.Collections.Generic;
using System.IO;
using TacticalAI.Core;

public static class SectionTrustGate
{
    static int fails = 0;
    static void Ok(bool cond, string what)
    {
        Console.WriteLine((cond ? "PASS " : "FAIL ") + what);
        if (!cond) fails++;
    }

    static readonly string App =
        "/home/open/work/ia-ai/ImperialCommander2/ImperialCommander2";

    public static void Run()
    {
        TrustAsset();
        SkillCurveLive();
        MiningAgainstShippedTable();
        AuditChannelOnly();
        Console.WriteLine("SectionTrustGate: " + (fails == 0 ? "ALL PASS" : fails + " FAIL"));
        if (fails > 0) Environment.ExitCode = 1;
    }

    // ---- 1) shipped trust table ------------------------------------------
    static void TrustAsset()
    {
        var path = Path.Combine(App, "Assets", "Resources", "Languages", "En",
                                "tactical_section_trust.json");
        if (!File.Exists(path))
        {
            Ok(false, "tactical_section_trust.json exists");
            return;
        }
        var doc = Json.AsDict(Json.Parse(File.ReadAllText(path)));
        var meta = Json.AsDict(Json.Get(doc, "_meta", null) ?? new Dictionary<string, object>());
        Ok(Json.AsStr(Json.Get(meta, "schema", "")) == "tacai.section_trust/1",
           "schema == tacai.section_trust/1");

        var rows = SectionTrust.RowsOf(doc);
        Ok(rows.Count >= 1, "at least one row (rows: " + rows.Count + ")");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        bool idsUnique = true, sane = true;
        int defaults = 0;
        foreach (var r in rows)
        {
            if (!ids.Add(r.Id)) idsUnique = false;
            if (!(r.Threshold.Num >= 1 && r.Threshold.Num <= r.Threshold.Den)) sane = false;
            if (r.Skill.Length == 0) defaults++;
        }
        Ok(idsUnique, "row ids unique");
        Ok(sane, "every row a sane rational 1 <= num <= den (no doubles, no zero, nothing above certainty)");
        Ok(defaults == 1, "exactly one default (no-skill) row");

        // The pin: the default row is the concept-10 Zielwert 2/3.
        SectionTrust.PrimeDoc(doc);
        Rational thr;
        bool have = SectionTrust.TryThresholdFor(null, out thr);
        Ok(have && thr.ToString() == "2/3", "default threshold == Zielwert 2/3 (got " +
           (have ? thr.ToString() : "no row") + ")");
        // Fail-closed read: with the shipped table a sharp mask is trusted,
        // a 2-way mask is NOT (1/2 < 2/3): trust is earned by the Eichung.
        var g = new SectionGraph();
        g.AddSection("s-a"); g.AddSection("s-b");
        var sharp = new SectionMask();
        sharp.AddObservation(1, "deploy", new[] { "s-a" }, "fact");
        Ok(SectionTrust.Trusted(sharp, g, 1, 0, SkillProfile.For("S3")),
           "sharp mask trusted under the shipped table");
        var wide = new SectionMask();
        wide.AddObservation(1, "deploy", new[] { "s-a", "s-b" }, "fact");
        Ok(!SectionTrust.Trusted(wide, g, 1, 0, SkillProfile.For("S3")),
           "2-way mask NOT trusted under 2/3 (deliberately strict)");
        SectionTrust.Clear();
    }

    // ---- 2) skill curve, run live ----------------------------------------
    static void SkillCurveLive()
    {
        Ok(!SectionSkill.MaskRefinementEnabled(null) &&
           !SectionSkill.PostureClampBasisEnabled(null) &&
           !SectionSkill.SupporterMaskTrustEnabled(null),
           "no profile (reference path) = every gated channel off (chain-entry state)");
        bool lowOff = true, highOn = true;
        foreach (var level in new[] { "S1", "S2" })
        {
            var p = SkillProfile.For(level);
            if (SectionSkill.MaskRefinementEnabled(p) || SectionSkill.PostureClampBasisEnabled(p)
                || SectionSkill.SupporterMaskTrustEnabled(p)) lowOff = false;
        }
        foreach (var level in new[] { "S3", "S4", "S5" })
        {
            var p = SkillProfile.For(level);
            if (!SectionSkill.MaskRefinementEnabled(p) || !SectionSkill.PostureClampBasisEnabled(p)
                || !SectionSkill.SupporterMaskTrustEnabled(p)) highOn = false;
        }
        Ok(lowOff, "S1/S2: refinement, posture-clamp basis and supporter mask trust all OFF");
        Ok(highOn, "S3/S4/S5: all three intelligence channels ON");

        // Exceptions fire at EVERY rung: a Wachzone scenario through the
        // REAL feed, read on a Refine=false projection (low rung).
        var feed = new WorldEventFeed("gate|wachzone_low_rung");
        feed.Append("MissionStart", 1, DictN(
            "objective", "Guard the array.", "priorityTarget", "REBEL",
            "roundLimit", 6L, "mission", "GATE"));
        feed.Append("Spawn", 1, DictN("group", "GUARD", "size", 1L, "section", "s-array"));
        feed.Append("SectionActivated", 1, DictN("section", "s-array", "name", "Array Room"));
        feed.Append("HeroActivationStart", 2, DictN("hero", "LEIA"));
        feed.Append("EntityInteract", 2, DictN("entity", "t-array", "name", "Comms Array",
                                               "section", "s-array"));
        feed.Append("HeroActivated", 2, DictN("hero", "LEIA"));
        var coarse = SectionSpace.Project(feed.Events, null, null, null,
            new SectionOptions { Refine = false });
        var facts = coarse.WachzoneFacts(new[] { "s-array" }, new[] { "GUARD" });
        Ok(facts.Count == 1, "Wachzone exception fires on the LOW rung (coarse) - ungated by law");
        var ring = coarse.VictimRingOf("GUARD", 0);
        Ok(ring != null && ring.Basis == "argmax", "victim ring computes on the LOW rung too (fact reaction)");
    }

    // ---- 3) mining against the shipped mission-critical table ------------
    static void MiningAgainstShippedTable()
    {
        var path = Path.Combine(App, "Assets", "Resources", "Languages", "En",
                                "tactical_mission_critical.json");
        if (!File.Exists(path)) { Ok(false, "tactical_mission_critical.json exists"); return; }
        var doc = Json.AsDict(Json.Parse(File.ReadAllText(path)));
        var missions = Json.AsList(Json.Get(doc, "missions", new List<object>()));
        Ok(missions.Count > 0, "table carries " + missions.Count + " mission rows");

        int totalEdges = 0, totalOwners = 0, bespinEdges = 0;
        bool bespinPairFound = false;
        foreach (var mraw in missions)
        {
            var md = Json.AsDict(mraw);
            var file = Json.AsStr(Json.Get(md, "file", "")) ?? "";
            var missionId = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? file.Substring(0, file.Length - 5) : file;

            // Sections the mission itself declares (edge-endpoint universe).
            var ownSections = new HashSet<string>(StringComparer.Ordinal);
            var sections = Json.AsList(Json.Get(md, "sections", new List<object>()));
            string corridorGuid = "", catwalkGuid = "";
            foreach (var s in sections)
            {
                var sd = Json.AsDict(s);
                var g = Json.AsStr(Json.Get(sd, "section", "")) ?? "";
                ownSections.Add(g);
                var n = Json.AsStr(Json.Get(sd, "name", "")) ?? "";
                if (string.Equals(missionId, "BESPIN1", StringComparison.OrdinalIgnoreCase))
                {
                    if (n == "Section 2 - Corridor") corridorGuid = g;
                    if (n == "Section 3 - Catwalk") catwalkGuid = g;
                }
            }

            // INDEPENDENT re-read (not the miner): owner map + expected edges.
            var wantOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in Json.AsList(Json.Get(md, "criticalEntities", new List<object>())))
            {
                var ed = Json.AsDict(e);
                var swapped = Json.Get(ed, "runtimeSwapped", null) as bool?;
                if (swapped.HasValue && swapped.Value) continue;
                if (Json.AsStr(Json.Get(ed, "entityType", "")) != "Door") continue;
                var ent = Json.AsStr(Json.Get(ed, "entity", "")) ?? "";
                var sec = Json.AsStr(Json.Get(ed, "section", "")) ?? "";
                if (ent.Length > 0 && sec.Length > 0) wantOwners[ent] = sec;
            }
            var gotOwners = SectionTrust.DoorOwnersOf(doc, missionId);
            bool ownersEqual = gotOwners.Count == wantOwners.Count;
            if (ownersEqual)
                foreach (var kv in wantOwners)
                {
                    string has;
                    if (!gotOwners.TryGetValue(kv.Key, out has) || has != kv.Value) { ownersEqual = false; break; }
                }
            if (!ownersEqual)
            {
                Ok(false, "DoorOwnersOf matches the independent re-read for " + file);
                continue;
            }
            totalOwners += gotOwners.Count;

            var wantEdges = new List<string[]>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in Json.AsList(Json.Get(md, "sectionHypotheses", new List<object>())))
            {
                var hd = Json.AsDict(h);
                var secB = Json.AsStr(Json.Get(hd, "section", "")) ?? "";
                if (secB.Length == 0) continue;
                foreach (var e in Json.AsList(Json.Get(hd, "entities", new List<object>())))
                {
                    var ed = Json.AsDict(e);
                    if (Json.AsStr(Json.Get(ed, "entityType", "")) != "Door") continue;
                    var door = Json.AsStr(Json.Get(ed, "entity", "")) ?? "";
                    string secA;
                    if (!wantOwners.TryGetValue(door, out secA)) continue;
                    if (secA == secB) continue;
                    var key = secA + "|" + secB + "|" + door;
                    if (!seen.Add(key)) continue;
                    wantEdges.Add(new[] { secA, secB, door });
                }
            }
            var gotEdges = SectionTrust.MissionDoorEdges(doc, missionId);
            if (gotEdges.Count != wantEdges.Count)
            {
                Ok(false, "MissionDoorEdges count matches for " + file +
                          " (miner " + gotEdges.Count + ", re-read " + wantEdges.Count + ")");
                continue;
            }
            bool same = true;
            for (int i = 0; i < wantEdges.Count; i++)
                if (gotEdges[i][0] != wantEdges[i][0] || gotEdges[i][1] != wantEdges[i][1]
                    || gotEdges[i][2] != wantEdges[i][2]) { same = false; break; }
            if (!same) { Ok(false, "MissionDoorEdges rows match for " + file); continue; }

            // Endpoints must be sections the mission itself declares.
            bool endpointsReal = true;
            foreach (var row in gotEdges)
                if (!ownSections.Contains(row[0]) || !ownSections.Contains(row[1])) endpointsReal = false;
            if (!endpointsReal) { Ok(false, "edge endpoints inside the mission's sections for " + file); continue; }
            totalEdges += gotEdges.Count;

            if (string.Equals(missionId, "BESPIN1", StringComparison.OrdinalIgnoreCase))
            {
                bespinEdges = gotEdges.Count;
                foreach (var row in gotEdges)
                    if (row[0] == corridorGuid && row[1] == catwalkGuid) bespinPairFound = true;
            }
        }
        Ok(true, "DoorOwners + MissionDoorEdges verified per mission (independent re-read, " +
           missions.Count + " missions, " + totalOwners + " owners, " + totalEdges + " edges)");
        Ok(totalEdges > 0, "corpus is NOT zero-edge (transit law has real data behind it)");
        Ok(bespinEdges >= 1 && bespinPairFound,
           "Bespin 1: Corridor<->Catwalk door edge mined from the real table (hand-verified pair)");
    }

    // ---- 4) calibration stays on the audit channel -----------------------
    static void AuditChannelOnly()
    {
        var root = Path.Combine(App, "Assets", "Scripts");
        var offenders = new List<string>();
        foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            if (!text.Contains("SectionCalibration") && !text.Contains("hitrate")) continue;
            var norm = f.Replace('\\', '/');
            if (norm.EndsWith("/Core/SectionCalibration.cs", StringComparison.Ordinal)) continue;
            offenders.Add(Path.GetFileName(norm));
        }
        Ok(offenders.Count == 0,
           "no runtime file references SectionCalibration/hitrate (audit channel only" +
           (offenders.Count == 0 ? ")" : "; offenders: " + string.Join(", ", offenders.ToArray())) + ")");
    }

    static Dictionary<string, object> DictN(params object[] kv)
    {
        var d = new Dictionary<string, object>();
        for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
        return d;
    }
}
