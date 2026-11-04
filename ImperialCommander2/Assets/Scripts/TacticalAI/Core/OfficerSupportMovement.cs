// O-B supporter movement codex (concept offizier_supporter §2 Schicht C,
// Thomas-Gesetz 04.11.: "Offiziere werfen keine Vorstoss-Doktrin mehr").
// Two pure candidate-set laws for groups carrying the O-A Supporter flag:
//
// 1) VORSTOSS-SPERRE (AdvanceRemoval): Move end states that CLOSE the
//    distance to the nearest known hero are removed as long as the figure
//    could also hold (stand) or withdraw - and holding is always legal, so
//    the law is stated on the set: every advance candidate is dropped, the
//    remaining legal options are bit-identical (fairness law like every
//    skill law: the codex only NARROWS, never invents). Exceptions (law
//    fully INERT, both laws): OfficerEmergency != None (E1..E4 self-combat,
//    OfficerDoctrine.EmergencyFor - the O-A cascade) and the order-receiver
//    exception: when the receiver of the officer's own order needs the move
//    for its range, the movement is HIS JOB, not a push (the execution
//    layer passes orderRangeNeeded and the why-line).
//
// 2) GRUPPEN-LEINE (group leash, Konzept Bewegungsgesetz 4, refinement
//    04.11.): only for Supporter flag + pattern == "support" + emergency
//    None. Anker = the nearest OTHER living imperial figure (same-team,
//    deterministic first-min over world order, like the Patterns.cs wounded
//    pick). Distance measure = exactly the support distance measure of
//    Patterns.cs: Geometry.ChebyshevGap over the footprint cells. Every
//    candidate whose anchor distance EXCEEDS the anchor distance at the
//    CURRENT position is removed; the LEERE-SET-GESETZ undoes the leash
//    narrowing when it would empty the set (the leash never produces
//    "kein Zug" - same anti-starvation law as the FilterDP fallback).
//    No anchor (single figure on the board) = leash INACTIVE. Emergency
//    lifts the leash; order-range movement is leash-conform by definition.
//
// Composition law: the advance removal runs FIRST, the leash applies to the
// REMAINING set (Gesetze komponieren). Tiebreaks (wounded-ally nudge,
// cover, GH cascade) live downstream in Patterns.EndpointScore / the graph
// and are untouched by this module.
//
// The O-A identity inputs stay explicit injections (same discipline as
// OfficerDoctrine.EmergencyFor: the caller computes the emergency, the
// harness injects it; a missing signal can never fabricate a push).
// Golden vectors: Assets/Tests/Fixtures/tacai/officer_support.json.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// Result of one SupporterCodex narrowing (every stage surfaced - the
    /// harness pins the counts, not just the survivor set).
    public sealed class SupportNarrowResult
    {
        public Dictionary<(Cell, int), int> Kept;
        public bool AdvanceLaw;      // Vorstoss-Sperre was ACTIVE (gate fired)
        public bool LeashLaw;        // Gruppenleine was ACTIVE (anchor exists)
        public long RemovedAdvance;
        public long RemovedLeash;
        public bool LeashRestored;   // Leere-Set-Gesetz undid the leash
        public int CurHeroGap;       // nearest-hero gap at the current position
        public int CurAnchorGap = -1; // anchor gap at current; -1 = no anchor
        public string Why = "";

        public SupportNarrowResult(Dictionary<(Cell, int), int> kept)
        { Kept = kept; }
    }

    public static class OfficerSupportMovement
    {
        /// Gate of the Vorstoss-Sperre (concept §2 Schicht C). True only for a
        /// card carrying the printed-supporter identity (DeploymentCard.
        /// Supporter, O-A) WITHOUT an emergency and WITHOUT the order-range
        /// exception. Every other caller is bit-identical to pre-O-B.
        public static bool AdvanceBarred(Figure fig, OfficerEmergency emergency,
                                         bool orderRangeNeeded)
        {
            if (fig == null || fig.Card == null || !fig.Card.Supporter) return false;
            if (emergency != OfficerEmergency.None) return false;   // E1..E4 self-combat
            if (orderRangeNeeded) return false;                    // his job, not a push
            return true;
        }

        /// Single-end-state test for the sequential planner branches (a move
        /// candidate is an ADVANCE when it closes the nearest-hero gap, and a
        /// LEASH break when the pattern is support and it exceeds the current
        /// anchor gap). No empty-set restoration here: a removed single
        /// candidate always leaves at least the hold option (cost-0 state is
        /// never an advance and never leashed), so "kein Zug" cannot arise.
        public static bool BlockedEnd(World world, Figure fig, Cell anchor, int orient,
                                      OfficerEmergency emergency = OfficerEmergency.None,
                                      bool orderRangeNeeded = false)
        {
            if (!AdvanceBarred(fig, emergency, orderRangeNeeded)) return false;
            if (Patterns.HeroGap(world, Geometry.FootprintCells(anchor, fig.Card.MiniSize, orient), fig, null)
                < Patterns.HeroGap(world, SpaceLegality.FigureCells(fig), fig, null))
                return true;
            if (fig.Pattern == "support")
            {
                var ally = AnchorAlly(world, fig);
                if (ally != null &&
                    StateGap(world, fig, anchor, orient, SpaceLegality.FigureCells(ally))
                    > AnchorGapAt(world, fig, fig.Anchor, fig.Orientation))
                    return true;
            }
            return false;
        }

        /// The nearest other living same-team figure (deterministic first-min
        /// over world order - python min() semantics, like the EndpointScore
        /// wounded pick). null = no anchor -> leash inactive.
        public static Figure AnchorAlly(World world, Figure fig)
        {
            Figure best = null;
            int bestD = int.MaxValue;
            foreach (var a in world.Figures)
            {
                if (!a.Alive || ReferenceEquals(a, fig) || a.Team != fig.Team) continue;
                var ac = SpaceLegality.FigureCells(a);
                if (ac.Count == 0) continue;
                int d = GapCells(fig.Anchor, fig.Orientation, fig, ac);
                if (d < bestD) { bestD = d; best = a; }
            }
            return best;
        }

        /// Anchor distance at a hypothetical end state. -1 = no anchor
        /// (leash inactive; the harness pins "no anchor -> unchanged set").
        public static int AnchorGapAt(World world, Figure fig, Cell anchor, int orient)
        {
            var ally = AnchorAlly(world, fig);
            if (ally == null) return -1;
            return StateGap(world, fig, anchor, orient, SpaceLegality.FigureCells(ally));
        }

        /// Composed narrowing of a candidate end-state set (Exhaust output or
        /// any caller-built set): advance removal first, leash second with
        /// the Leere-Set-Gesetz. Inert callers (no supporter flag / emergency
        /// / order exception) get the SAME dictionary instance back.
        public static SupportNarrowResult Narrow(World world, Figure fig,
                                                 Dictionary<(Cell, int), int> candidates,
                                                 OfficerEmergency emergency = OfficerEmergency.None,
                                                 bool orderRangeNeeded = false)
        {
            var res = new SupportNarrowResult(candidates);
            bool barred = AdvanceBarred(fig, emergency, orderRangeNeeded);
            res.AdvanceLaw = barred;
            if (!barred || candidates == null || candidates.Count == 0) return res;

            res.CurHeroGap = Patterns.HeroGap(world, SpaceLegality.FigureCells(fig), fig, null);

            // -- 1) Vorstoss-Sperre: drop every end state closer to the
            //       nearest known hero than the current position (the hold
            //       state is never closer, so the set can never empty here).
            var kept = new Dictionary<(Cell, int), int>();
            foreach (var kv in candidates)
                if (Patterns.HeroGap(world,
                        Geometry.FootprintCells(kv.Key.Item1, fig.Card.MiniSize, kv.Key.Item2),
                        fig, null)
                    >= res.CurHeroGap)
                    kept[kv.Key] = kv.Value;
            res.RemovedAdvance = candidates.Count - kept.Count;
            res.Kept = kept;
            var why = new List<string>();
            if (res.RemovedAdvance > 0)
                why.Add("Vorstoss-Sperre: " + res.RemovedAdvance + " Vorstoss-Kandidat(en) entfernt");

            // -- 2) GRUPPEN-LEINE (nur Muster support; Notfall hebt sie auf -
            //       bei Emergency != None ist das Gate oben schon inert).
            if (fig.Pattern == "support")
            {
                var ally = AnchorAlly(world, fig);
                if (ally != null)
                {
                    res.LeashLaw = true;
                    var allyCells = SpaceLegality.FigureCells(ally);
                    res.CurAnchorGap = GapCells(fig.Anchor, fig.Orientation, fig, allyCells);
                    var kept2 = new Dictionary<(Cell, int), int>();
                    foreach (var kv in kept)
                        if (StateGap(world, fig, kv.Key.Item1, kv.Key.Item2, allyCells)
                            <= res.CurAnchorGap)
                            kept2[kv.Key] = kv.Value;
                    res.RemovedLeash = kept.Count - kept2.Count;
                    if (kept2.Count == 0 && kept.Count > 0)
                    {
                        // Leere-Set-Gesetz: die Leine erzeugt nie "kein Zug" -
                        // nur DIESE Verengung wird zurueckgenommen.
                        res.LeashRestored = true;
                        why.Add("Gruppenleine: Leere-Set, Verengung zurueckgenommen ("
                                + res.RemovedLeash + ")");
                    }
                    else
                    {
                        res.Kept = kept2;
                        if (res.RemovedLeash > 0)
                            why.Add("Gruppenleine: " + res.RemovedLeash
                                    + " Kandidat(en) ueber Anker-Distanz entfernt");
                    }
                }
            }
            res.Why = string.Join("; ", why);
            return res;
        }

        // ------------------------------------------------------------- metric
        // Support distance measure of Patterns.cs (HeroGap): ChebyshevGap
        // between footprint cells of the state and the target cells.

        static int StateGap(World world, Figure fig, Cell anchor, int orient, List<Cell> targetCells)
            => GapCells(anchor, orient, fig, targetCells);

        static int GapCells(Cell anchor, int orient, Figure fig, List<Cell> targetCells)
        {
            var cells = Geometry.FootprintCells(anchor, fig.Card.MiniSize, orient);
            int best = int.MaxValue;
            foreach (var c in cells)
                foreach (var t in targetCells)
                {
                    // identical per-cell Chebyshev metric as
                    // Geometry.ChebyshevGap over the two cell sets.
                    int g = Math.Max(Math.Abs(c.X - t.X), Math.Abs(c.Y - t.Y));
                    if (g < best) best = g;
                }
            return best == int.MaxValue ? 0 : best;
        }
    }
}
