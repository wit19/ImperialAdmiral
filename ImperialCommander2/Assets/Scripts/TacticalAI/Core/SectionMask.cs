// S-B (Konzept positionierung v1 0'+8.1 L3'+C): the group's OWN belief over
// the SECTION vector - one SectionMask per (group, figure index), bundled in
// a GroupSectionBelief. This is the Eigen-Glaube rail: the command chain
// reads it, the player never sees it, nothing of it rides the public vector.
//
// STRUCTURE. A mask is a list of OBSERVATIONS. The sections of ONE
// observation are ALTERNATIVES (the group is in at most one of them: the
// belief unions within an observation - deploy candidate sets, the
// disjunctive "one of the living" re-grounding). DIFFERENT observations are
// CONSTRAINTS on the same figure: "it was compatible with A at round 3 AND
// compatible with B at round 5" - the belief INTERSECTS across observations
// (concept C law "Mehrere Anker schneiden: C = geschnittene, aufgeweitete
// Anker-Mengen"). An empty intersection degrades to the union: with
// lower-bound distances, distant anchors do not actually contradict, so the
// honest reading is a DIFFUSE mask, never a fabricated narrowing - and the
// consumer key stays off on diffuse masks (fallback ladder 10).
//
// Observation sources (the Eichung channels, grades as in the vector ledger):
//   * deploy        - the S-A result: deploy marker section => EXACT seed
//                     (one alternative) or CANDIDATE SET (concept 5.6 law:
//                     never a lie of precision, never a random pick).
//   * hit           - GroupDamage dial event: victim figure (index >= 0)
//                     re-grounds ONLY its own mask; the anonymous index -1
//                     re-grounds the GROUP as a disjunctive over the living
//                     figures and NEVER a named figure (card law).
//   * wounded-group - HeroWounded inside the group's imperial window: one
//                     HYPOTHESIS-grade group disjunctive observation
//                     ("struggle came out of this region, one of the living
//                     did it"), never fact, never per figure (concept 0' b).
//   * death         - alive count drops; masks STAY (no positional fact was
//                     produced - the "one of them fell" disjunctive).
//
// DILATION: each alternative's ball grows by SPEED x rounds since the
// observation; speed is the OBSERVED hero crossings per window (SectionSpace
// speed track), never intent-derived (concept 0': otherwise self-confirmation).
//
// FULL-INTEGER PATH (card law): weights are Rational (BigInteger), uniform
// 1/|candidates| - set semantics, no invented probability, no double anywhere.
//
// GRUNDGESETZ mirror (8 law): a section outside the mask has status UNKNOWN.
// There is structurally NO "closed" status in this module: the mask answers
// "the group could plausibly be here", never "it is not there".
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// One alternative of one observation: section, the observation's round,
    /// the epistemic grade (fact | hypothesis - the ledger's grading).
    public struct SectionAnchor
    {
        public string Section;
        public int Round;
        public string Kind; // "fact" | "hypothesis"

        public SectionAnchor(string section, int round, string kind)
        {
            Section = section; Round = round; Kind = kind;
        }
    }

    /// Belief of ONE figure over the section vector.
    public sealed class SectionMask
    {
        public const string StatusInside = "inside";
        public const string StatusUnknown = "unknown";
        // Deliberately NO StatusClosed - Grundgesetz as type design.

        private sealed class Observation
        {
            public int Round;
            public string Source;
            public readonly List<SectionAnchor> Alternatives = new List<SectionAnchor>();
        }

        private readonly List<Observation> _observations = new List<Observation>();

        /// Reflects the LAST Candidates() call (harness/EXPLAIN read it right
        /// after computing): true when the intersection degraded to the union.
        public bool DegradedToUnion { get; private set; }
        public bool HasObservation { get { return _observations.Count > 0; } }
        /// How many observations the belief carries (deploy/hit/wounded legs).
        public int ObservationCount { get { return _observations.Count; } }

        /// Add one observation (its sections are ALTERNATIVES of one another).
        /// Empty/blank sections are dropped (data-exists: no section, no anchor).
        public void AddObservation(int round, string source, IEnumerable<string> sections, string kind)
        {
            if (sections == null) return;
            var obs = new Observation { Round = round, Source = source };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in sections)
            {
                if (string.IsNullOrEmpty(s) || !seen.Add(s)) continue;
                obs.Alternatives.Add(new SectionAnchor(s, round, kind));
            }
            if (obs.Alternatives.Count > 0) _observations.Add(obs);
        }

        /// Dilated ball of one observation at roundNow: union of the balls of
        /// its alternatives, sorted. Internal helper (Candidates/Dump).
        List<string> BallOf(SectionGraph graph, Observation obs, int roundNow, int sectionsPerWindow)
        {
            var u = new HashSet<string>(StringComparer.Ordinal);
            int t = Math.Max(0, sectionsPerWindow) * Math.Max(0, roundNow - obs.Round);
            foreach (var a in obs.Alternatives)
                foreach (var s in graph.BallWithin(a.Section, t)) u.Add(s);
            var l = new List<string>(u);
            l.Sort(StringComparer.Ordinal);
            return l;
        }

        /// Candidate SET at roundNow: intersection across observations of the
        /// dilated union of each observation's alternatives. Empty
        /// intersection -> union (honest degradation, DegradedToUnion=true).
        /// Deterministic and sorted.
        public IReadOnlyList<string> Candidates(SectionGraph graph, int roundNow, int sectionsPerWindow)
        {
            DegradedToUnion = false;
            if (graph == null || _observations.Count == 0) return new List<string>();
            List<string> acc = null;
            var union = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obs in _observations)
            {
                var ball = BallOf(graph, obs, roundNow, sectionsPerWindow);
                foreach (var s in ball) union.Add(s);
                acc = acc == null ? ball : Intersect(acc, ball);
            }
            if (acc != null && acc.Count > 0)
            {
                acc.Sort(StringComparer.Ordinal);
                return acc;
            }
            DegradedToUnion = true;
            var u = new List<string>(union);
            u.Sort(StringComparer.Ordinal);
            return u;
        }

        /// Grade of one candidate: "fact" if ANY observation supports it with
        /// a fact-grade alternative inside its ball; "hypothesis" only when
        /// every supporter is a hypothesis. Hypotheses never upgrade to fact;
        /// one fact support is enough (positive evidence wins over inference).
        public string CandidateGrade(SectionGraph graph, string section, int roundNow, int sectionsPerWindow)
        {
            if (graph == null || string.IsNullOrEmpty(section)) return "hypothesis";
            bool any = false;
            foreach (var obs in _observations)
            {
                int t = Math.Max(0, sectionsPerWindow) * Math.Max(0, roundNow - obs.Round);
                foreach (var a in obs.Alternatives)
                {
                    foreach (var s in graph.BallWithin(a.Section, t))
                    {
                        if (!string.Equals(s, section, StringComparison.Ordinal)) continue;
                        any = true;
                        if (a.Kind == "fact") return "fact";
                    }
                }
            }
            return any ? "hypothesis" : "unknown";
        }

        /// Uniform candidate weights: 1/|candidates| each (exact Rational).
        public Dictionary<string, Rational> CandidateWeights(SectionGraph graph, int roundNow, int sectionsPerWindow)
        {
            var c = Candidates(graph, roundNow, sectionsPerWindow);
            var w = new Dictionary<string, Rational>(StringComparer.Ordinal);
            if (c.Count == 0) return w;
            foreach (var s in c) w[s] = new Rational(1, c.Count);
            return w;
        }

        /// Dominance = weight of the strongest section (uniform set
        /// semantics: 1/|candidates|; exact seed => 1). The S-C consumer keys
        /// only above its threshold; a diffuse mask keeps the key OFF.
        public Rational Dominance(SectionGraph graph, int roundNow, int sectionsPerWindow)
        {
            var c = Candidates(graph, roundNow, sectionsPerWindow);
            return c.Count == 0 ? Rational.Zero : new Rational(1, c.Count);
        }

        /// inside | unknown. NO closed status - a section outside the mask
        /// carries zero information (Grundgesetz as code).
        public string Status(SectionGraph graph, string section, int roundNow, int sectionsPerWindow)
        {
            foreach (var s in Candidates(graph, roundNow, sectionsPerWindow))
                if (string.Equals(s, section, StringComparison.Ordinal)) return StatusInside;
            return StatusUnknown;
        }

        /// Re-anchor onto the current candidate region: the hit confirms
        /// presence where the belief already pointed (Eichung). ONE new
        /// observation whose alternatives are the current candidates - growth
        /// restarts from the evidence, no position is invented.
        public void Reground(SectionGraph graph, int roundNow, int sectionsPerWindow, string kind, string source)
        {
            var c = Candidates(graph, roundNow, sectionsPerWindow);
            if (c.Count == 0) return; // no belief, nothing to re-ground on
            AddObservation(roundNow, source, c, kind);
        }

        /// Deterministic dump (harness pin + EXPLAIN trail).
        public string Dump(SectionGraph graph, int roundNow, int sectionsPerWindow)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var obs in _observations)
            {
                sb.Append("obs round=").Append(obs.Round)
                  .Append(" src=").Append(obs.Source)
                  .Append(" alts=");
                var secs = new List<string>();
                foreach (var a in obs.Alternatives) secs.Add(a.Section + ":" + a.Kind);
                secs.Sort(StringComparer.Ordinal);
                sb.Append(string.Join(",", secs.ToArray())).Append('\n');
            }
            var cands = Candidates(graph, roundNow, sectionsPerWindow);
            var candList = new List<string>(cands);
            sb.Append("cands ").Append(candList.Count == 0 ? "-" : string.Join(",", candList.ToArray()));
            if (DegradedToUnion) sb.Append(" degraded=union");
            sb.Append('\n');
            if (candList.Count > 0)
                sb.Append("dom ").Append(Dominance(graph, roundNow, sectionsPerWindow).ToString()).Append('\n');
            return sb.ToString();
        }

        public SectionMask Clone()
        {
            var m = new SectionMask();
            m.DegradedToUnion = DegradedToUnion;
            foreach (var obs in _observations)
            {
                var o = new Observation { Round = obs.Round, Source = obs.Source };
                o.Alternatives.AddRange(obs.Alternatives);
                m._observations.Add(o);
            }
            return m;
        }

        private static List<string> Intersect(List<string> a, List<string> b)
        {
            var sb = new HashSet<string>(b, StringComparer.Ordinal);
            var r = new List<string>();
            foreach (var s in a) if (sb.Contains(s)) r.Add(s);
            r.Sort(StringComparer.Ordinal);
            return r;
        }
    }

    /// The GROUP container: per-figure masks (concept 0' key = (group, index))
    /// plus the disjunctive bookkeeping the card law demands:
    ///   * anonymous victim (figure index -1): the hit re-grounds the GROUP as
    ///     a disjunctive over the living figures - it must NEVER name one.
    ///   * HeroWounded attributed to this group (window law decides WHICH):
    ///     one HYPOTHESIS group disjunctive, never per figure.
    ///   * Death: alive drops, every mask stays.
    public sealed class GroupSectionBelief
    {
        // The disjunctive victim convention (card law): producers that cannot
        // name the hit figure pass -1; the mask then acts group-wide only.
        public const int AnonymousVictim = -1;

        private readonly Dictionary<int, SectionMask> _figures = new Dictionary<int, SectionMask>();
        private int _alive;
        private int _fallen;
        private bool _anonymousVictim;
        private bool _woundedDisjunctive;

        public string Group { get; private set; }
        public int Alive { get { return _alive; } }
        public int Fallen { get { return _fallen; } }
        /// A figure-index-less hit was observed (disjunctive victim).
        public bool AnonymousVictimObserved { get { return _anonymousVictim; } }
        /// A HeroWounded was attributed to this group's window (hypothesis).
        public bool WoundedDisjunctiveObserved { get { return _woundedDisjunctive; } }

        public GroupSectionBelief(string group, int alive)
        {
            Group = group; _alive = Math.Max(0, alive);
            EnsureFigureMasks();
        }

        /// Spawn: alive = size, fresh per-figure masks (a re-rolled group is a
        /// new belief: the old masks described figures that no longer exist).
        public static GroupSectionBelief FromSpawn(string group, int size)
        {
            return new GroupSectionBelief(group, size);
        }

        /// Deploy seed for EVERY figure: one section = EXACT, a set =
        /// CANDIDATES (one observation of alternatives, concept 5.6). This is
        /// the S-A result channel; the S-C bridge wires the producer, the
        /// fact shape is law here.
        public void SeedDeploy(IReadOnlyList<string> sections, int round)
        {
            if (sections == null || sections.Count == 0) return; // data-exists
            foreach (var idx in FigureIndexes())
                _figures[idx].AddObservation(round, "deploy", sections, "fact");
        }

        public SectionMask MaskOf(int figureIndex)
        {
            SectionMask m;
            return _figures.TryGetValue(figureIndex, out m) ? m : null;
        }

        public IReadOnlyList<int> FigureIndexes()
        {
            var l = new List<int>(_figures.Keys);
            l.Sort();
            return l;
        }

        private void EnsureFigureMasks()
        {
            for (int i = 0; i < _alive; i++)
                if (!_figures.ContainsKey(i)) _figures[i] = new SectionMask();
        }

        /// GroupDamage calibration. index >= 0: ONLY that figure's mask
        /// re-grounds (the dial names the victim - truth-conditional use of
        /// the index law). index == -1 (anonymous): the hit re-grounds the
        /// GROUP as a disjunctive - every figure gets the same group-grade
        /// observation; no single figure is singled out. Card law, pinned.
        public void NoteHit(SectionGraph graph, int figureIndex, int roundNow, int sectionsPerWindow)
        {
            if (figureIndex >= 0)
            {
                var m = MaskOf(figureIndex);
                if (m != null) m.Reground(graph, roundNow, sectionsPerWindow, "fact", "hit");
                return;
            }
            _anonymousVictim = true;
            foreach (var idx in FigureIndexes())
                _figures[idx].Reground(graph, roundNow, sectionsPerWindow, "fact", "hit-group");
        }

        /// HeroWounded attributed to this group (concept 0' b): the current
        /// candidate region rides on as ONE hypothesis-grade observation per
        /// figure - "struggle came out of this region, ONE OF THE LIVING did
        /// it". Never fact-grade, never per-figure attribution.
        public void NoteHeroWounded(SectionGraph graph, int roundNow, int sectionsPerWindow)
        {
            _woundedDisjunctive = true;
            foreach (var idx in FigureIndexes())
            {
                var m = _figures[idx];
                var c = m.Candidates(graph, roundNow, sectionsPerWindow);
                if (c.Count > 0) m.AddObservation(roundNow, "wounded-group", c, "hypothesis");
            }
        }

        /// Death: alive drops, masks STAY (the "one of them fell"
        /// disjunctive: nothing positional was learned).
        public void NoteDeath()
        {
            if (_alive > 0) _alive--;
            _fallen++;
        }

        /// Dump every figure (deterministic order).
        public string Dump(SectionGraph graph, int roundNow, int sectionsPerWindow)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("group ").Append(Group)
              .Append(" alive=").Append(_alive)
              .Append(" fallen=").Append(_fallen)
              .Append(" anonVictim=").Append(_anonymousVictim ? "yes" : "no")
              .Append(" woundedDisjunctive=").Append(_woundedDisjunctive ? "yes" : "no")
              .Append('\n');
            foreach (var idx in FigureIndexes())
            {
                sb.Append("fig ").Append(idx).Append('\n');
                sb.Append(_figures[idx].Dump(graph, roundNow, sectionsPerWindow));
            }
            return sb.ToString();
        }

        public GroupSectionBelief Clone()
        {
            var b = new GroupSectionBelief(Group, _alive);
            b._fallen = _fallen;
            b._anonymousVictim = _anonymousVictim;
            b._woundedDisjunctive = _woundedDisjunctive;
            b._figures.Clear();
            foreach (var kv in _figures) b._figures[kv.Key] = kv.Value.Clone();
            return b;
        }
    }
}
