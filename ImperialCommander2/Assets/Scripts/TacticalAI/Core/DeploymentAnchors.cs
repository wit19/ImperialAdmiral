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

        /// <summary>P33-H4 (Thomas 08.11, CONCEPT_drohungsbudget Sec.2 Stufe 1,
        /// the DYNAMIC ANKER-LAW): a section-activated anchor (feed fact
        /// "MapManagement activated this section, and it is STILL live")
        /// claims its WHOLE section - it has no entity and therefore NO
        /// position claim. HasPosition=false joins the tier law (a DP in that
        /// section is tier 0) but never contributes a distance: candidates in
        /// the section tie at this stage and the next law stage decides.
        /// A section that also carries positioned anchors keeps its
        /// positional precision (positioned anchors are measured first; a
        /// section-only anchor is the fallback claim only).</summary>
        public bool HasPosition = true;
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

        /// <summary>P33-H4: the winner's scored facts (tier/zone/leash/diversity
        /// parts) for the EXPLAIN line and the vectors; null only when the
        /// caller used a path that does not score (never today).</summary>
        public DpRow Score;
    }

    /// <summary>P33-H4 (Thomas 08.11, CONCEPT_drohungsbudget Sec.2 Stufe 3):
    /// one scored DP candidate - every public fact the ranked choice reads,
    /// kept SEPARATE so the explain line can name the Score-Bestandteile and
    /// the vectors can pin each stage without exposing the seed chain.
    /// Dump format (harness row pin, input order):
    /// "name|tier=N|anchor=(d | sec | -)|zone=(n | -)|own=0/1|fresh=0/1" -
    /// "sec" = a section-only dynamic anchor claimed the section without a
    /// position fact; "-" under zone = the recorded graph has no route to any
    /// battle-zone section (NO claim, never "far").</summary>
    public sealed class DpRow
    {
        public DeployCandidate Candidate;
        public int Tier = 1;                 // 0 = DP sits in an anchor section
        public bool HasAnchor;
        public int AnchorIndex = -1;         // index into the anchor list
        public double SecDistSq = double.PositiveInfinity; // intra-section, section-only => 0
        public double AnchorDist = double.NaN;             // shown distance; NaN = section-only claim
        public bool ZoneKnown;
        public int ZoneDist = int.MaxValue;  // graph hops to the nearest battle zone
        public bool Own;                     // friendly-leash fact (own group's section)
        public bool Fresh = true;            // DP not used by the deploy ledger
        public ulong Seed;                   // legacy tie key (last resort)

        public string Dump()
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return (Candidate != null ? Candidate.Name : "?")
                + "|tier=" + Tier
                + "|anchor=" + (!HasAnchor ? "-"
                    : (double.IsNaN(AnchorDist) ? "sec" : AnchorDist.ToString("0.##", ci)))
                + "|zone=" + (ZoneKnown ? ZoneDist.ToString(ci) : "-")
                + "|own=" + (Own ? "1" : "0")
                + "|fresh=" + (Fresh ? "1" : "0");
        }
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
        // P33-H4: POSITIONED anchors are measured first (unchanged law); a
        // section-only anchor (HasPosition=false, the dynamic ANKER-LAW)
        // never enters the distance measurement - it is the fallback claim
        // when the section carries no positioned anchor, reported at
        // distance 0 so every candidate in the section ties at this stage
        // (a whole-section claim has no positional fact to prefer).
        static int SectionAnchorIndex(DeployCandidate c, IList<DeployAnchor> anchors, out double distSq)
        {
            int best = -1;
            distSq = double.PositiveInfinity;
            int secOnly = -1;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null || !SameGuid(a.SectionGuid, c.SectionGuid)) continue;
                if (!a.HasPosition)
                {
                    if (secOnly < 0 ||
                        string.Compare(a.EntityGuid ?? "", anchors[secOnly].EntityGuid ?? "",
                                       StringComparison.Ordinal) < 0)
                        secOnly = i;
                    continue;
                }
                double d = DistSq(c, a);
                if (d < distSq - 1e-9 ||
                    (Math.Abs(d - distSq) <= 1e-9 && best >= 0 &&
                     string.Compare(a.EntityGuid ?? "", anchors[best].EntityGuid ?? "",
                                    StringComparison.Ordinal) < 0))
                { distSq = d; best = i; }
            }
            if (best < 0 && secOnly >= 0) { best = secOnly; distSq = 0.0; }
            return best;
        }

        // Nearest anchor anywhere (log diagnostics for a section-foreign
        // winner; never an ordering input across sections). P33-H4: only
        // POSITIONED anchors are measurable - a section-only dynamic anchor
        // has no coordinates, so it never shows up as a "nearest anchor"
        // for a foreign winner (its claim is a tier, printed by the row).
        static int NearestAnyIndex(DeployCandidate c, IList<DeployAnchor> anchors, out double distSq)
        {
            int best = -1;
            distSq = double.PositiveInfinity;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (a == null || !a.HasPosition) continue;
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
            if (string.IsNullOrEmpty(sec) || ownSections == null) return false;
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
        /// friendly figures (entry-DP sections from the deploy ledger).
        /// P33-H4 EVOLUTION (Thomas 08.11): the leash is now a VOLLRECHT -
        /// it decides after ALL spatial stages (tier, intra-section distance,
        /// battle-zone graph distance) and before the DP-diversity tie and
        /// the seed chain, so it can pull a candidate across sections when
        /// the spatial facts say "equal". It can still never out-rank a
        /// spatial claim. null/empty => bit-identical pre-M14e behaviour.
        /// P33-H4 is bit-compatible on every pre-P33 input: with graph and
        /// zoneSections null (no battle-zone fact) and usedDps null the
        /// stage ladder collapses to exactly the pre-P33 Choose law - the
        /// canon dp_anchors.json and deploy_ledger.json vectors pin that.</summary>
        public static DeployChoice Choose(IList<DeployCandidate> cands,
                                          IList<DeployAnchor> anchors,
                                          ulong sessionSeed, int round,
                                          IList<string> ownSections = null)
        {
            List<DpRow> ignored;
            return ChooseScored(cands, anchors, null, null,
                                sessionSeed, round, ownSections, null, out ignored);
        }

        /// <summary>P33-H4 (Thomas 08.11, CONCEPT_drohungsbudget Sec.2 Stufe 3):
        /// the DP-Score over the (Karte, DP)-Paar - the full stage ladder,
        /// each stage a total pre-order over the still-tied candidates,
        /// decided SEQUENTIALLY (first differing stage wins):
        ///   1. ANKER-TIER: a DP in an anchor section (mined live anchor or
        ///      dynamic section anchor, see DeployAnchor.HasPosition) beats
        ///      every section-foreign DP. Null-Law: no anchor, no tier.
        ///   2. intra-section anchor distance (same section only, positioned
        ///      anchors only - the pre-P33 law, unchanged).
        ///   3. BATTLE-ZONE graph distance: DpRow.ZoneDist = the SectionGraph
        ///      hop lower bound from the DP's section to the nearest
        ///      Kampfzone section (anchor sections + focus/reaction zones the
        ///      caller passes). This is the honest CROSS-SECTION distance the
        ///      world coordinates could never offer (Loch-Gesetz: an
        ///      unrecorded route is NO CLAIM, never "far" - a candidate with
        ///      a recorded route beats a claimless one, two claimless ones
        ///      stay tied). graph null / zones empty => stage inert.
        ///   4. FRIEND LEASH (ownSections): now a Vollrecht (see Choose doc).
        ///   5. DP-DIVERSITAET (usedDps = the deploy-ledger points + the
        ///      points this mission's bridge picks already chose): an UNUSED
        ///      DP beats a used one (letzte Welle nicht auf denselben Punkt),
        ///      but only among candidates every earlier stage called equal -
        ///      it never out-ranks a spatial or leash claim. All used => all
        ///      tied => the law never walls the choice. null/empty => inert.
        ///   6. the LEGACY seed chain TieKey -> Name -> Guid, bit-identical.
        /// Determinism: no RNG anywhere; the same inputs yield the same pick.
        /// rows: every scored candidate in CALLER ORDER for the EXPLAIN line
        /// and the vectors (may be null when the caller needs no parts).</summary>
        public static DeployChoice ChooseScored(IList<DeployCandidate> cands,
                                                IList<DeployAnchor> anchors,
                                                SectionGraph graph,
                                                IList<string> zoneSections,
                                                ulong sessionSeed, int round,
                                                IList<string> ownSections,
                                                IList<string> usedDps,
                                                out List<DpRow> rows)
        {
            rows = null;
            if (cands == null || cands.Count == 0) return null;
            if (anchors == null) anchors = new DeployAnchor[0];

            var scored = new List<DpRow>();
            foreach (var c in cands)
            {
                if (c == null) continue;
                double secDistSq;
                int aIdx = SectionAnchorIndex(c, anchors, out secDistSq);
                var row = new DpRow
                {
                    Candidate = c,
                    AnchorIndex = aIdx,
                    SecDistSq = secDistSq,
                    Tier = aIdx < 0 ? 1 : 0,
                    HasAnchor = aIdx >= 0,
                    AnchorDist = aIdx >= 0 && anchors[aIdx].HasPosition
                                 ? Math.Sqrt(secDistSq) : double.NaN,
                    Own = OwnSectionHit(c.SectionGuid, ownSections),
                    Fresh = !UsedHit(c.Guid, usedDps),
                    Seed = TieKey(c.Name, sessionSeed, round),
                };
                bool zk; int zd;
                ZoneDistance(graph, zoneSections, c.SectionGuid, out zk, out zd);
                row.ZoneKnown = zk; row.ZoneDist = zd;
                scored.Add(row);
            }
            if (scored.Count == 0) return null;
            rows = scored;

            // RESOLUTION LAW (unchanged): sequential challenge against the
            // current best IN CALLER ORDER.
            DpRow best = scored[0];
            for (int i = 1; i < scored.Count; i++)
                if (CompareScored(scored[i], best, ownSections) < 0) best = scored[i];

            var choice = new DeployChoice { Candidate = best.Candidate, Score = best };
            if (best.AnchorIndex >= 0)
            {
                choice.Anchor = anchors[best.AnchorIndex];
                choice.Dist = double.IsNaN(best.AnchorDist) ? 0.0 : best.AnchorDist;
            }
            else
            {
                double anyDistSq;
                int any = NearestAnyIndex(best.Candidate, anchors, out anyDistSq);
                if (any >= 0) { choice.Anchor = anchors[any]; choice.Dist = Math.Sqrt(anyDistSq); }
            }
            return choice;
        }

        // The stage ladder as a pairwise comparator (sequential challenge,
        // caller order - same determinism law as the pre-P33 Choose).
        static int CompareScored(DpRow c, DpRow b, IList<string> ownSections)
        {
            if (c.Tier != b.Tier) return c.Tier - b.Tier;
            if (c.Tier == 0 && SameGuid(c.Candidate.SectionGuid, b.Candidate.SectionGuid))
            {
                // same section, both anchored: intra-section distance decides
                if (c.SecDistSq < b.SecDistSq - 1e-9) return -1;
                if (c.SecDistSq > b.SecDistSq + 1e-9) return 1;
            }
            // stage 3: battle-zone hop distance - a claim beats no claim
            // (facts only RAISE a candidate), smaller beats larger, equal or
            // both claimless falls through.
            if (c.ZoneKnown || b.ZoneKnown)
            {
                if (c.ZoneKnown != b.ZoneKnown) return c.ZoneKnown ? -1 : 1;
                if (c.ZoneDist != b.ZoneDist) return c.ZoneDist < b.ZoneDist ? -1 : 1;
            }
            // stage 4: friend leash as Vollrecht (0 when both/neither/no data)
            int ownCmp = OwnSectionCompare(c.Candidate.SectionGuid, b.Candidate.SectionGuid, ownSections);
            if (ownCmp != 0) return ownCmp;
            // stage 5: DP diversity - unused beats used (both fresh or both
            // used, or no ledger data: inert, falls through).
            if (c.Fresh != b.Fresh) return c.Fresh ? -1 : 1;
            // stage 6: the legacy seed chain, bit-identical
            if (c.Seed != b.Seed) return c.Seed < b.Seed ? -1 : 1;
            int cmp = string.Compare(c.Candidate.Name ?? "", b.Candidate.Name ?? "",
                                     StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(c.Candidate.Guid ?? "", b.Candidate.Guid ?? "",
                                  StringComparison.Ordinal);
        }

        // Hop lower bound from ONE section to the nearest battle zone over
        // the recorded graph. DistanceLowerBound's NoKnowledge (-1) means
        // "no recorded route" - per the Loch-Gesetz that is NO CLAIM
        // (known=false), never distance infinity-as-penalty: an unmeasured
        // candidate is not pushed behind other unmeasured ones and not
        // rewarded either; it simply cannot win this stage.
        static void ZoneDistance(SectionGraph graph, IList<string> zones,
                                 string section, out bool known, out int dist)
        {
            known = false; dist = int.MaxValue;
            if (graph == null || zones == null || zones.Count == 0) return;
            if (string.IsNullOrEmpty(section)) return;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (string.IsNullOrEmpty(z)) continue;
                int d = graph.DistanceLowerBound(section, z);
                if (d < 0) continue;                    // no recorded route: no claim
                if (!known || d < dist) { known = true; dist = d; }
            }
        }

        // DP-diversity helper: true when this DP entity is recorded as used
        // (deploy ledger + this mission's bridge picks). Null-Law: no ledger
        // data => nothing is "used", the stage is inert.
        static bool UsedHit(string guid, IList<string> usedDps)
        {
            if (string.IsNullOrEmpty(guid) || usedDps == null) return false;
            for (int i = 0; i < usedDps.Count; i++)
                if (SameGuid(guid, usedDps[i])) return true;
            return false;
        }
    }
}
