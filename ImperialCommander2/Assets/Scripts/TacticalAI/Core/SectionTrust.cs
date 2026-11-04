// S-C (Konzept positionierung v1 10 + card t_865d5997 Scope 2): the mask
// TRUST threshold as PUBLIC DATA, never a code literal. Concept 10 makes the
// threshold "der einzige neue Tuning-Parameter der ganzen Idee" - so it
// lives in tactical_section_trust.json (schema tacai.section_trust/1) the
// same way the doctrine dice live in tactical_pattern_weights.json: plain
// reviewable text, rational integers, first-match row order (Thomas law
// 01.11. "Regeln als Tabelle, nicht als Code"), fail-closed.
//
// Semantics (concept 10 "MASKEN-VERTRAUEN"): a (group, figure) mask is
// TRUSTED when the dominant section's weight >= num/den AND the last
// Candidates() evaluation did NOT degrade to the union. Below threshold or
// diffuse => key OFF => today's canon (fallback ladder 10, step 1: no
// behavior change, no new penalty). NO table, NO row, unreadable table =>
// key OFF too: the absence of data is never a hidden default (data-exists).
//
// FULL-INTEGER PATH (card law): Rational (BigInteger), no doubles - the
// Zielwert 2/3 is the EXACT rational 2/3, boundary behaviour is exact.
//
// The second half of this file mines the SHIPPED tactical_mission_critical
// table (S-A artifact) into the two coordinate-free data feeds the S-B
// projector accepts but had no producer for: DoorOwners (door GUID -> its
// OWN section, criticalEntities rows) and MissionDoorEdges ([A, B, door]
// rows: a door whose own section is A, flipped by an event body that
// activates section B, connects A and B - a HYPOTHESIS-grade door edge,
// AddDoorEdge semantics; a missing row keeps the pair unknown-PERMEABLE,
// never a wall (Grundgesetz)). runtimeSwapped rows are skipped: the table's
// own note says the authored section is not runtime truth there.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public sealed class SectionTrustRow
    {
        public string Id;
        public string Skill;      // "" = default row; else exact "S1".."S5"
        public Rational Threshold;
        public string Note;

        /// A row is usable only when it states a sane rational threshold:
        /// 1 <= num <= den (a threshold above certainty can never fire; a
        /// zero/negative or sub-unit numerator is authoring noise). Bad row
        /// => the row does not exist (fail-closed, never a guess).
        public bool Valid { get { return Threshold.Num >= 1 && Threshold.Num <= Threshold.Den; } }
    }

    public static class SectionTrust
    {
        static object _doc;

        /// Prime from an already-parsed doc (harness + the Unity asset seam).
        public static void PrimeDoc(object doc) { _doc = doc; }
        public static object DocOrNull() { return _doc; }
        public static void Clear() { _doc = null; }

        // MiniJson number law: integers arrive as long, floats as double. A
        // threshold must be stated as INTEGER num/den (full-integer path); a
        // double row is authoring noise => the row does not exist.
        static bool IsIntLike(object v) { return v is long || v is int; }

        static long AsIntValue(object v) { return v is int ? (int)v : (long)v; }

        static bool IsTrue(object v) { var b = v as bool?; return b.HasValue && b.Value; }

        /// Parse the shipped-shaped table. Null doc / no "rows" => empty list
        /// (key OFF). Row order is priority order (first match wins).
        public static List<SectionTrustRow> RowsOf(object doc)
        {
            var rows = new List<SectionTrustRow>();
            var d = doc as Dictionary<string, object>;
            if (d == null) return rows;
            object raw;
            if (!d.TryGetValue("rows", out raw)) return rows;
            var list = raw as List<object>;
            if (list == null) return rows;
            foreach (var r in list)
            {
                var rd = r as Dictionary<string, object>;
                if (rd == null) continue;                 // junk row: absent
                var oNum = Json.Get(rd, "num", null);
                var oDen = Json.Get(rd, "den", null);
                if (!IsIntLike(oNum) || !IsIntLike(oDen)) continue;
                long num = AsIntValue(oNum), den = AsIntValue(oDen);
                if (num <= 0 || den <= 0) continue;       // invalid: row absent
                var row = new SectionTrustRow
                {
                    Id = Json.AsStr(Json.Get(rd, "id", "")) ?? "",
                    Skill = Json.AsStr(Json.Get(rd, "skill", "")) ?? "",
                    Threshold = new Rational(num, den),
                    Note = Json.AsStr(Json.Get(rd, "note", "")) ?? "",
                };
                rows.Add(row);                            // kept even invalid: RowsOf is the raw table view
            }
            return rows;
        }

        /// First VALID row whose skill matches the profile, else the first
        /// valid default row (Skill ""). profile null reads the default row
        /// only (reference path has no rung). No usable row => false (OFF).
        public static bool TryThresholdFor(SkillProfile profile, out Rational threshold)
        {
            threshold = Rational.Zero;
            SectionTrustRow fallback = null;
            foreach (var r in RowsOf(_doc))
            {
                if (!r.Valid) continue;
                if (r.Skill.Length == 0)
                {
                    if (fallback == null) fallback = r;
                    continue;
                }
                if (profile != null && string.Equals(r.Skill, profile.Level, StringComparison.Ordinal))
                {
                    threshold = r.Threshold;
                    return true;
                }
            }
            if (fallback != null) { threshold = fallback.Threshold; return true; }
            return false;
        }

        /// THE consumer key (concept 10). Trust requires ALL of:
        ///  * a usable threshold row (data-exists; no table => OFF),
        ///  * a non-empty candidate set (no belief => nothing to trust),
        ///  * the last evaluation NOT degraded to the union (a diffuse mask
        ///    is honest diffuseness, never trustable - fallback ladder 10),
        ///  * dominance >= threshold (exact Rational compare).
        /// Read DegradedToUnion right after the Dominance() call: it reflects
        /// that evaluation (SectionMask contract).
        public static bool Trusted(SectionMask mask, SectionGraph graph,
                                   int roundNow, int sectionsPerWindow,
                                   SkillProfile profile)
        {
            Rational thr;
            if (!TryThresholdFor(profile, out thr)) return false;
            if (mask == null) return false;
            var dom = mask.Dominance(graph, roundNow, sectionsPerWindow);
            if (mask.DegradedToUnion) return false;
            if (dom.IsZero) return false;
            return dom >= thr;
        }

        // ---- tactical_mission_critical mining (coordinate-free) ----------
        // Mission join key: the live missionID (SessionKey uses it); table
        // rows carry "file" = missionID + ".json" (mine-tool law). Match is
        // ordinal-ignore-case over that identity; "missionGuid" is carried by
        // the table too for human review but the live model has no field for
        // it, so it is never the join key (data-exists).
        static Dictionary<string, object> MissionRow(object doc, string missionId)
        {
            if (doc == null || string.IsNullOrEmpty(missionId)) return null;
            var d = doc as Dictionary<string, object>;
            if (d == null) return null;
            object missionsRaw;
            if (!d.TryGetValue("missions", out missionsRaw)) return null;
            var missions = missionsRaw as List<object>;
            if (missions == null) return null;
            string want = missionId + ".json";
            foreach (var m in missions)
            {
                var md = m as Dictionary<string, object>;
                if (md == null) continue;
                var file = Json.AsStr(Json.Get(md, "file", "")) ?? "";
                if (string.Equals(file, want, StringComparison.OrdinalIgnoreCase)) return md;
            }
            return null;
        }

        /// door GUID -> owning section GUID for the mission (criticalEntities
        //  rows whose section is authored and whose runtimeSwapped is false).
        /// runtimeSwapped doors are SKIPPED: the table says the live feed is
        /// the truth there, and a wrong owner would poison the transit law.
        public static Dictionary<string, string> DoorOwnersOf(object doc, string missionId)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var md = MissionRow(doc, missionId);
            if (md == null) return map;
            return CriticalDoorSections(md);
        }

        // Shared source of both products: entity GUID -> section, doors only,
        // unswapped, authored section non-empty (data-exists).
        static Dictionary<string, string> CriticalDoorSections(Dictionary<string, object> md)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            object entsRaw;
            if (!md.TryGetValue("criticalEntities", out entsRaw)) return map;
            var ents = entsRaw as List<object>;
            if (ents == null) return map;
            foreach (var e in ents)
            {
                var ed = e as Dictionary<string, object>;
                if (ed == null) continue;
                if (IsTrue(Json.Get(ed, "runtimeSwapped", null))) continue;
                if (!string.Equals(Json.AsStr(Json.Get(ed, "entityType", "")), "Door", StringComparison.Ordinal)) continue;
                var guid = Json.AsStr(Json.Get(ed, "entity", "")) ?? "";
                var sec = Json.AsStr(Json.Get(ed, "section", "")) ?? "";
                if (guid.Length == 0 || sec.Length == 0) continue;
                map[guid] = sec;
            }
            return map;
        }

        /// [sectionA, sectionB, doorGuid] hypothesis rows for the mission:
        /// a door owned by section A, modified by an event body whose
        /// MapManagement activates section B (sectionHypotheses rows), gives
        /// the pair A-B a door edge. A == B or unknown owner => no row (the
        /// pair stays unknown-PERMEABLE, never a wall). Deterministic order:
        /// hypothesis-row order, entity order within the row, deduped.
        public static List<string[]> MissionDoorEdges(object doc, string missionId)
        {
            var rows = new List<string[]>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var md = MissionRow(doc, missionId);
            if (md == null) return rows;
            var owners = CriticalDoorSections(md);
            object hypsRaw;
            if (!md.TryGetValue("sectionHypotheses", out hypsRaw)) return rows;
            var hyps = hypsRaw as List<object>;
            if (hyps == null) return rows;
            foreach (var h in hyps)
            {
                var hd = h as Dictionary<string, object>;
                if (hd == null) continue;
                var secB = Json.AsStr(Json.Get(hd, "section", "")) ?? "";
                if (secB.Length == 0) continue;           // unmined section: no edge
                object entsRaw;
                if (!hd.TryGetValue("entities", out entsRaw)) continue;
                var ents = entsRaw as List<object>;
                if (ents == null) continue;
                foreach (var e in ents)
                {
                    var ed = e as Dictionary<string, object>;
                    if (ed == null) continue;
                    if (!string.Equals(Json.AsStr(Json.Get(ed, "entityType", "")), "Door", StringComparison.Ordinal)) continue;
                    var door = Json.AsStr(Json.Get(ed, "entity", "")) ?? "";
                    if (door.Length == 0) continue;
                    string secA;
                    // The door's OWN section must come from the mission's
                    // unswapped critical rows (runtimeSwapped never enters
                    // the owner map). No owner -> no edge, permeable stays.
                    if (!owners.TryGetValue(door, out secA)) continue;
                    if (string.Equals(secA, secB, StringComparison.Ordinal)) continue;
                    var dedupe = secA + "|" + secB + "|" + door;
                    if (!seen.Add(dedupe)) continue;
                    rows.Add(new[] { secA, secB, door });
                }
            }
            return rows;
        }
    }
}
