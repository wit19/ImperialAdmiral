// S-C (card Scope 5, concept 11 "Guete-Meter"): the CALIBRATION metric -
// section hit-rate and tightness of the belief against a fixture oracle.
//
// AUDIT CHANNEL ONLY, NEVER THE DECISION LEVEL. These lines are a gauge
// for QA (which skill rung sees how closely): a hit-rate or a tightness
// number must never feed a decision, a clamp or an ordering - decisions
// ride facts (SectionSpace probes) and the trust table (SectionTrust),
// never on how well the belief has been doing. The SectionTrustGate pins
// that no runtime decision file references this class or its report
// tokens; the harness is its only caller.
//
// Metric (per labelled batch of oracle rows, each row = one hand-pinned
// ground-truth fact "group/figure was in section S at round R" - in the
// synthetic fixture worlds this is true BY CONSTRUCTION):
//  * hitrate  = fraction of rows whose section is inside the mask's
//    candidate set evaluated AT the row's round (fenster-um-die-runden).
//  * tightness = mean over rows of 1/|candidates| (exact Rational, S-B
//    uniform-weight set semantics; empty candidate set contributes 0 -
//    no knowledge is neither a hit nor a tight belief).
// Full-integer path: no doubles anywhere.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public sealed class CalibrationRow
    {
        public string Group;
        public int Figure;
        public int Round;
        public string Section;
    }

    public static class SectionCalibration
    {
        /// Deterministic report lines for one labelled batch (harness-pinned).
        public static List<string> Report(string label, SectionSpace space,
                                          IEnumerable<CalibrationRow> rows)
        {
            var lines = new List<string>();
            if (space == null || rows == null) return lines;
            var graph = space.Graph;
            int t = space.SectionsPerWindow;
            int n = 0, hits = 0;
            Rational tight = Rational.Zero;
            foreach (var r in rows)
            {
                if (r == null) continue;
                var b = space.BeliefOf(r.Group);
                var m = b != null ? b.MaskOf(r.Figure) : null;
                IReadOnlyList<string> c = m != null
                    ? m.Candidates(graph, r.Round, t)
                    : new List<string>();
                bool hit = false;
                foreach (var s in c)
                    if (string.Equals(s, r.Section, StringComparison.Ordinal)) { hit = true; break; }
                n++;
                if (hit) hits++;
                if (c.Count > 0) tight += new Rational(1, c.Count);
                lines.Add("calib " + label + " row group=" + r.Group + " fig=" + r.Figure +
                          " round=" + r.Round + " cands=" + c.Count + " hit=" + (hit ? "yes" : "no"));
            }
            var hitrate = n == 0 ? "0" : new Rational(hits, n).ToString();
            var tightAvg = n == 0 ? "0" : (tight / n).ToString();
            lines.Add("calib " + label + " summary n=" + n + " hits=" + hits +
                      " hitrate=" + hitrate + " tightness=" + tightAvg);
            return lines;
        }
    }
}
