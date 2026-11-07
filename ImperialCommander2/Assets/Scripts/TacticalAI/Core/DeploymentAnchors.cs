// M14c P25-C2 (card t_15ea0fba, fixplan_Player-25_v2 C2): the PURE ranked
// DP choice with mission-objective anchors. Player-25 finding C: the
// deterministic DP chooser ignored how near a deployment point was to the
// mission objective (Empire 11: the search droid spawned far from the
// Informant contact path). The ranker below orders the already-filtered
// DP candidates by (a) DP section == anchor section first, (b) distance to
// the nearest anchor INSIDE that same section (world coordinates are only
// compared intra-section - section borders make a cross-section world
// distance worthless; section-foreign DPs simply rank behind every (a)
// candidate), (c) ties fall to the EXISTING seed-hash chain
// (SessionSeed^round over the DP name, then name, then GUID) - the very
// key the legacy single OrderBy in TacticalAIBridge uses today, so an
// empty anchor list reproduces today's choice bit-for-bit.
//
// Data-exists law: anchors only come from the shipped mined table
// (tools/dp_anchor_mine.py -> Resources/Data/taktische_mission_critical.json)
// re-resolved against the LIVE map entities by GUID; no table, no row, no
// live entity => empty anchor list => the legacy line runs verbatim. No
// invented anchors, no RNG, reproducible per round.
//
// Language neutrality: GUIDs and coordinates only. Mission rows match the
// loaded mission FILE NAME OrdinalIgnoreCase - never a localized title.
// Hand-pinned fixture: Assets/Tests/Fixtures/tacai/dp_anchors.json (same
// documented route as optional_deploy.json - no Python sim twin, Canon
// vectors untouched).
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// <summary>One mission-objective anchor: the entity's GUID (for the
    /// log line), its section and its position. Position/section are the
    /// LIVE values when the Unity adapter built the anchor (row values are
    /// the authored review copy).</summary>
    public sealed class DeployAnchor
    {
        public string EntityGuid;
        public string SectionGuid;
        public double X;
        public double Y;
    }

    /// <summary>One already-filtered DP candidate. Name participates ONLY
    /// in the tie chain (never in display here); Guid is the identity the
    /// caller resolves back to the map entity.</summary>
    public sealed class DeployCandidate
    {
        public string Guid;
        public string Name;
        public string SectionGuid;
        public double X;
        public double Y;
    }

    /// <summary>Winner + the diagnostics the play-log line prints: the
    /// anchor that drove the choice and the distance it was measured at
    /// (intra-section distance; when no DP sits in an anchor section the
    /// winner is seed-chosen and Dist is the nearest anchor distance,
    /// printed for review only).</summary>
    public sealed class DeployChoice
    {
        public DeployCandidate Candidate;
        public DeployAnchor Anchor;
        public double Dist;
    }

    /// <summary>M14e: one hero-entry field of the mined hero-entry table -
    /// the blue "Deploy the heroes here" highlight: entity GUID + its
    /// section (the spatial claim the app is allowed to make) + authored
    /// name (log sugar only).</summary>
    public sealed class HeroEntry
    {
        public string Guid;
        public string SectionGuid;
        public string Name;
    }

    public static class DeploymentAnchors
    {
        /// <summary>FNV-1a string ordinal -> ulong. Byte-identical law to
        /// TacticalAIBridge.OrdinalOf (bridge keeps its copy for the legacy
        /// line; the harness pins both against the same vectors).</summary>
        public static ulong OrdinalOf(string text)
        {
            ulong h = 1469598103934665603UL;
            foreach (var c in text ?? "") { h ^= c; h *= 1099511628211UL; }
            return h == 0 ? 0x9E3779B97F4A7C15UL : h;
        }

        /// <summary>The EXISTING seed-hash tie key, same constants as the
        /// legacy OrderBy line in TacticalAIBridge.TryChooseDeploymentPoint:
        /// OrdinalOf(name) ^ (sessionSeed ^ max(1,round)*GOLDEN).</summary>
        public static ulong TieKey(string name, ulong sessionSeed, int round)
        {
            return OrdinalOf(name ?? "") ^ (sessionSeed ^
                (ulong)Math.Max(1, round) * 0x2545F4914F6CDD1DUL);
        }

        static bool SameGuid(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Header-table adapter (pure): the shipped-shaped table
        /// { columns:[...], rows:[[...],...] } -> anchor rows for ONE
        /// mission, matched on the mission JSON FILE NAME OrdinalIgnoreCase
        /// (file ids, never a string title). Required columns: missionFile,
        /// entityGuid, entityPosition ("x,y"), sectionGuid. Unknown columns
        /// are ignored (positions-subtask ids are not anchor data); a row
        /// missing or failing a required column does not exist
        /// (fail-closed, data-exists). No table / no match => empty list.</summary>
        public static List<DeployAnchor> RowsForMission(object tableRoot, string missionFile)
        {
            var list = new List<DeployAnchor>();
            var doc = tableRoot as Dictionary<string, object>;
            if (doc == null || string.IsNullOrEmpty(missionFile)) return list;
            object colsRaw, rowsRaw;
            if (!doc.TryGetValue("columns", out colsRaw) ||
                !doc.TryGetValue("rows", out rowsRaw)) return list;
            var cols = colsRaw as List<object>;
            var rows = rowsRaw as List<object>;
            if (cols == null || rows == null) return list;

            int iFile = -1, iGuid = -1, iPos = -1, iSec = -1;
            for (int i = 0; i < cols.Count; i++)
            {
                var c = Json.AsStr(cols[i]) ?? "";
                if (c == "missionFile") iFile = i;
                else if (c == "entityGuid") iGuid = i;
                else if (c == "entityPosition") iPos = i;
                else if (c == "sectionGuid") iSec = i;
            }
            if (iFile < 0 || iGuid < 0 || iPos < 0 || iSec < 0) return list;

            foreach (var raw in rows)
            {
                var cells = raw as List<object>;
                if (cells == null) continue;                      // junk row: absent
                int max = Math.Max(Math.Max(iFile, iGuid), Math.Max(iPos, iSec));
                if (cells.Count <= max) continue;                 // ragged row: absent
                var file = Json.AsStr(cells[iFile]) ?? "";
                if (!string.Equals(file, missionFile, StringComparison.OrdinalIgnoreCase))
                    continue;
                var guid = (Json.AsStr(cells[iGuid]) ?? "").Trim();
                var sec = (Json.AsStr(cells[iSec]) ?? "").Trim();
                var pos = (Json.AsStr(cells[iPos]) ?? "").Trim();
                if (guid.Length == 0 || sec.Length == 0) continue;
                double x, y;
                if (!TryPosition(pos, out x, out y)) continue;    // unreadable position: absent
                list.Add(new DeployAnchor { EntityGuid = guid, SectionGuid = sec, X = x, Y = y });
            }
            return list;
        }

        // "x,y" in invariant culture (mine law); extra components ignored,
        // Z stays 0. Any other shape => the row does not exist.
        static bool TryPosition(string pos, out double x, out double y)
        {
            x = 0; y = 0;
            if (string.IsNullOrEmpty(pos)) return false;
            var parts = pos.Split(',');
            if (parts.Length < 2) return false;
            double dx, dy;
            if (!double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out dx)) return false;
            if (!double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out dy)) return false;
            x = dx; y = dy;
            return true;
        }

        /// <summary>M14e (CONCEPT_standort_ledger_v2): the hero-entry table
        /// row for ONE mission, matched on the mission JSON FILE NAME
        /// OrdinalIgnoreCase (same law as RowsForMission). Table shape
        /// { rows: [ { mission, entries:[ { guid, section, name } ] } ] }
        /// (mine: tools/hero_entry_mine.py). Fail-closed everywhere: a
        /// missing/blank guid or section drops the entry, a junk row drops
        /// the row, no table/no match/no reader => empty list => every hero
        /// consumer stays inert (data-exists, Null-Law). The name is audit
        /// sugar for the log only - never a match key.</summary>
        public static List<HeroEntry> HeroEntriesForMission(object tableRoot, string missionFile)
        {
            var list = new List<HeroEntry>();
            var doc = tableRoot as Dictionary<string, object>;
            if (doc == null || string.IsNullOrEmpty(missionFile)) return list;
            object rowsRaw;
            if (!doc.TryGetValue("rows", out rowsRaw)) return list;
            var rows = rowsRaw as List<object>;
            if (rows == null) return list;
            foreach (var raw in rows)
            {
                var row = raw as Dictionary<string, object>;
                if (row == null) continue;                        // junk row: absent
                var file = Json.AsStr(Get(row, "mission")) ?? "";
                if (!string.Equals(file, missionFile, StringComparison.OrdinalIgnoreCase))
                    continue;
                var entries = Get(row, "entries") as List<object>;
                if (entries == null) continue;
                foreach (var eraw in entries)
                {
                    var e = eraw as Dictionary<string, object>;
                    if (e == null) continue;
                    var guid = (Json.AsStr(Get(e, "guid")) ?? "").Trim();
                    var sec = (Json.AsStr(Get(e, "section")) ?? "").Trim();
                    if (guid.Length == 0 || sec.Length == 0) continue;   // fail-closed
                    list.Add(new HeroEntry
                    {
                        Guid = guid,
                        SectionGuid = sec,
                        Name = Json.AsStr(Get(e, "name")) ?? "",
                    });
                }
            }
            return list;
        }

        static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        static double DistSq(DeployCandidate c, DeployAnchor a)
        {
            double dx = c.X - a.X, dy = c.Y - a.Y;
            return dx * dx + dy * dy;
        }

        // Nearest anchor in the candidate's OWN section; -1 when that
        // section carries no anchor. Tie between equal-distance anchors ->
        // lower entity GUID (Ordinal), so the log line stays deterministic.
        static int SectionAnchorIndex(DeployCandidate c, IList<DeployAnchor> anchors, out double distSq)
        {
            int best = -1;
            distSq = double.PositiveInfinity;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null || !SameGuid(a.SectionGuid, c.SectionGuid)) continue;
                double d = DistSq(c, a);
                if (d < distSq - 1e-9 ||
                    (Math.Abs(d - distSq) <= 1e-9 && best >= 0 &&
                     string.Compare(a.EntityGuid ?? "", anchors[best].EntityGuid ?? "",
                                    StringComparison.Ordinal) < 0))
                { distSq = d; best = i; }
            }
            return best;
        }

        // Nearest anchor anywhere (log diagnostics for a section-foreign
        // winner; never an ordering input across sections).
        static int NearestAnyIndex(DeployCandidate c, IList<DeployAnchor> anchors, out double distSq)
        {
            int best = -1;
            distSq = double.PositiveInfinity;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null) continue;
                double d = DistSq(c, a);
                if (d < distSq - 1e-9 ||
                    (Math.Abs(d - distSq) <= 1e-9 && best >= 0 &&
                     string.Compare(a.EntityGuid ?? "", anchors[best].EntityGuid ?? "",
                                    StringComparison.Ordinal) < 0))
                { distSq = d; best = i; }
            }
            return best;
        }

        // M14e friend-leash helper: -1 when c is own-section and best is
        // not, +1 vice versa, 0 when both or neither (or no leash data -
        // then the legacy seed chain decides, bit-identical pre-M14e).
        static int OwnSectionCompare(string cSec, string bestSec, IList<string> ownSections)
        {
            if (ownSections == null || ownSections.Count == 0) return 0;
            bool cOwn = OwnSectionHit(cSec, ownSections);
            bool bOwn = OwnSectionHit(bestSec, ownSections);
            if (cOwn == bOwn) return 0;
            return cOwn ? -1 : 1;
        }

        static bool OwnSectionHit(string sec, IList<string> ownSections)
        {
            if (string.IsNullOrEmpty(sec)) return false;
            for (int i = 0; i < ownSections.Count; i++)
                if (SameGuid(sec, ownSections[i])) return true;
            return false;
        }

        /// <summary>Ranked choice over non-empty candidates. Ordering law
        /// (fixplan C2): (a) DP in an anchor section beats every DP outside;
        /// (b) inside one and the same section, the smaller distance to
        /// that section's anchors wins - distances are NEVER compared
        /// across sections; (c) every remaining tie falls to the existing
        /// seed chain TieKey -> Name (Ordinal) -> Guid (Ordinal), i.e.
        /// exactly the legacy order. Empty anchor list => every candidate
        /// ranks section-foreign => pure legacy chain (bit-compat with
        /// today's single OrderBy; the bridge still keeps the legacy line
        /// verbatim for that case - this path only exists so the harness
        /// can pin the old result against the ranker).
        /// RESOLUTION LAW: candidates are challenged against the current
        /// best IN CALLER ORDER (sequential challenge), which is exactly
        /// deterministic even though the cross-section fallback (different
        /// anchor sections never compare distances) makes the pairwise
        /// relation non-transitive; the caller passes the app's DP list,
        /// whose order is the map-entity order.
        /// M14e FRIEND LEASH (Thomas 07.11, CONCEPT_standort_ledger_v2 C4):
        /// ownSections = the sections carrying the group's OWN located
        /// friendly figures (entry-DP sections from the deploy ledger). It
        /// rides ONLY INSIDE the existing tie - anchor tier and intra-
        /// section distance keep absolute Primat (the leash can never pull
        /// a DP across sections or away from an anchor); when the anchor
        /// law says equal, the section that already holds friendlies wins
        /// (concentration instead of splitting the landing), then the
        /// legacy seed chain decides as before. null/empty => bit-identical
        /// pre-M14e behaviour (canon vectors unchanged).</summary>
        public static DeployChoice Choose(IList<DeployCandidate> cands,
                                          IList<DeployAnchor> anchors,
                                          ulong sessionSeed, int round,
                                          IList<string> ownSections = null)
        {
            if (cands == null || cands.Count == 0) return null;
            if (anchors == null) anchors = new DeployAnchor[0];

            DeployChoice best = null;
            int bestAnchorIdx = -1;
            double bestSecDistSq = double.PositiveInfinity;   // cached per candidate below
            foreach (var c in cands)
            {
                if (c == null) continue;
                double secDistSq;
                int aIdx = SectionAnchorIndex(c, anchors, out secDistSq);
                int tier = aIdx < 0 ? 1 : 0;
                ulong key = TieKey(c.Name, sessionSeed, round);

                if (best == null)
                { best = new DeployChoice { Candidate = c }; bestAnchorIdx = aIdx; bestSecDistSq = secDistSq; continue; }

                int cmp;
                int bestTier = bestAnchorIdx < 0 ? 1 : 0;
                if (tier != bestTier) cmp = tier - bestTier;
                else if (tier == 0 && SameGuid(c.SectionGuid, best.Candidate.SectionGuid))
                {
                    // same section, both anchored: intra-section distance decides
                    if (secDistSq < bestSecDistSq - 1e-9) cmp = -1;
                    else if (secDistSq > bestSecDistSq + 1e-9) cmp = 1;
                    else cmp = 0;
                }
                else cmp = 0;    // different sections: world distance worthless
                if (cmp == 0)
                {
                    // M14e friend leash: inside the anchor-law tie, the
                    // own-section candidate wins before the seed chain.
                    int ownCmp = OwnSectionCompare(c.SectionGuid, best.Candidate.SectionGuid, ownSections);
                    if (ownCmp != 0) cmp = ownCmp;
                    else
                    {
                        ulong bestKey = TieKey(best.Candidate.Name, sessionSeed, round);
                        if (key != bestKey) cmp = key < bestKey ? -1 : 1;
                        else
                        {
                            cmp = string.Compare(c.Name ?? "", best.Candidate.Name ?? "",
                                                 StringComparison.Ordinal);
                            if (cmp == 0)
                                cmp = string.Compare(c.Guid ?? "", best.Candidate.Guid ?? "",
                                                     StringComparison.Ordinal);
                        }
                    }
                }
                if (cmp < 0)
                { best = new DeployChoice { Candidate = c }; bestAnchorIdx = aIdx; bestSecDistSq = secDistSq; }
            }
            if (best == null) return null;

            if (bestAnchorIdx >= 0)
            {
                best.Anchor = anchors[bestAnchorIdx];
                best.Dist = Math.Sqrt(bestSecDistSq);
            }
            else
            {
                double anyDistSq;
                int any = NearestAnyIndex(best.Candidate, anchors, out anyDistSq);
                if (any >= 0) { best.Anchor = anchors[any]; best.Dist = Math.Sqrt(anyDistSq); }
            }
            return best;
        }
    }
}
