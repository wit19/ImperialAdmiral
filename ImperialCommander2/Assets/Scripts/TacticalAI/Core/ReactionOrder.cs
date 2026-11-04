// R-C (concept eventreaktionen v1.2 par.2.2/par.4/par.5; Thomas decision
// 04.11., "so und nicht anders"): at EVERY press of "Imperiale aktivieren"
// the activation order is re-scored from the REMAINING figures - no switch,
// no interruption feature, ALWAYS. The App hands exactly the pending groups
// (SagaController.OnActivateImperial passes dgManager.GetNonExhaustedGroups()
// per click; ExhaustGroup runs per activation), so the permutation
// invariant is enforced by App state: SelectNext chooses ONLY from the list
// it is given (fail-loud otherwise). No interruption, no skipping: every
// pending figure stays pickable until it has acted.
//
// Score of one pending figure = reaction push overlay on top of the round
// plan basis:
//   BASIS    PlanRound over EXACTLY the pending figures, with a
//            DeterministicRandom seeded by the bridge's round formula
//            (sessionSeed ^ round*GOLDEN), so mid-round re-computation
//            reproduces the very same seeded draws as EnsureRound (M13u
//            contract: one pattern/plan roll per figure per ROUND).
//   R2 Schutzpflicht (skill >= S3): an ObjectiveChange credit OR a strike
//            against the mission-named figure landed THIS round, AND the
//            candidate is a guard anchor (pattern guard / the mission
//            objective names this figure - Guardhold law) OR the PlanRound
//            finisher with symbolic target proximity (the hero ledger shows
//            an "at:<figure>" anchor this round, or the objective names the
//            finisher itself). This is the Palpatine case: the S4/S5
//            finisher_last slot is automatically overruled by the overlay -
//            finisher_last stays PlanRound law, no points-fudging against it.
//   R1 Vergeltung (skill >= S2): this figure was shot within an open hero
//            window THIS round (R-A LastShot* attribution).
//   R3 Eindringling (skill >= S2): this figure arrived THIS round and its
//            Spawn fell inside an open hero window (R-B marker).
// Punktklassen (Thomas 04.11. #4 - collision order, NEVER sums): class
//         descending R2 > R1 > R3, within a class the event seq descends,
//         the PlanRound position is the tertiary key. A push ALWAYS
//         outclasses the bare basis (class*1000 dominates any basis rank),
//         and the highest push of a figure is the one shown - stacking
//         would let R1+R3 imitate R2 and dissolve the class order.
//
// S1 reads the basis only (honest handicap, disclosed). Unknown cards
// without a catalog entry (fan imports, M6s-B8 law) keep the legacy popup
// but participate in the order: basis behind every known figure, priority==1
// (public deployment knowledge) leads inside the unknown block. Every point
// comes from a PUBLIC feed event (concept 2.4 fairness invariants); nothing
// in here ever reads hidden hero positions - the ledger anchors are the
// symbolic identities R-B pinned.
//
// Pure: same pending list + same event history + same seed -> bit-identical
// pick and why-line. No wall-clock, no own Random stream beyond the seeded
// basis reproduction.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    public static class ReactionOrder
    {
        /// Round-seed formula of TacticalAIBridge.EnsureRound - reproduced
        /// here verbatim so BasisPlan's seeded draws (S3/S4 missers) are the
        /// same numbers EnsureRound rolled for this round. One constant,
        /// two call sites, harness pattern_stability pins the equality.
        public static ulong RoundSeed(ulong sessionSeed, int roundNo)
            => sessionSeed ^ (ulong)roundNo * 0x9E3779B97F4A7C15UL;

        // Punktklassen (collision order Thomas 04.11. #4: Schutzpflicht >
        // Vergeltung > Eindringling; class descending).
        public const int ClassProtect = 3;    // R2
        public const int ClassRetaliate = 2;  // R1
        public const int ClassIntruder = 1;   // R3
        public const int ClassBasis = 0;      // kein Reaktions-Schub

        const int ClassBlock = 1000;          // Punkte = Klasse*1000 + Ordinal

        sealed class Cand
        {
            public string Id;
            public string Name;
            public GroupFacts Fact;
            public FeedWorldView.GroupView Gv;      // null: feed never saw it
            public int PlanPos;                     // basis rank (unique)
            public int Cls;
            public long Seq;                        // raw event seq (0 = none)
            public bool SeqKnown;
            public int Ordinal;                     // seq rank among the field
            public string Push = "Basis";
            public string Reason = "Geplante Reihenfolge";
            // S-D (card t_c52dd34b): Bedrohung key of a Wachzone candidate
            // (printed damage EV x alive, ThreatKnown=false = no catalog
            // record - unknown is NEVER better). Only meaningful together
            // with Push=="WZ"; untouched by every pre-S-D path.
            public Rational Threat;
            public bool ThreatKnown;
        }

        /// <summary>The basis the overlay ranks on: PlanRound over the
        /// pending KNOWN figures, seeded exactly like the bridge's round.
        /// Exposed for the harness (pattern_stability pins it against a
        /// direct OrderPlanner.PlanRound call with the same inputs).
        /// S-D (card t_c52dd34b): the two proximity channels ride the
        /// documented PlanRound parameters - both null-defaulted, so every
        /// pre-S-D call stays bit-identical; SelectNext fills them from
        /// SectionConsumers.PlanZones only when a section space is handed.</summary>
        public static OrderPlan BasisPlan(int roundNo, List<GroupFacts> knownFacts,
                                          Dictionary<string, DeploymentCard> catalog,
                                          string posture, SkillProfile skill,
                                          ulong sessionSeed, string counter,
                                          IEnumerable<string> ringZones = null,
                                          string contestZone = null)
        {
            var rng = new DeterministicRandom(RoundSeed(sessionSeed, roundNo));
            return OrderPlanner.PlanRound(roundNo, knownFacts, catalog, posture,
                                          skill, rng, roundNo, ringZones, contestZone, counter, null);
        }

        /// <summary>Pick the next group to activate. Returns a member of
        /// pendingIds or throws (fail-loud); the bridge keeps its fail-open
        /// catch so any exception degrades to the legacy random draw.</summary>
        /// <param name="skill">skill rung 1..5 (S1 reads basis only)</param>
        /// <param name="view">fresh FeedWorldView of THIS mission's public
        /// feed (null: no feed yet - pure basis order, no reaction points)</param>
        /// <param name="why">debug account line (Thomas reads logs as the
        /// explanation UI): only publicly visible facts, no internal jargon.</param>
        public static string SelectNext(int roundNo, IList<string> pendingIds,
                                        List<GroupFacts> factsPending,
                                        Dictionary<string, DeploymentCard> catalog,
                                        string posture, int skill, ulong sessionSeed,
                                        FeedWorldView view, string counter,
                                        out string why,
                                        SectionSpace sectionSpace = null,
                                        IList<string> criticalSections = null)
        {
            if (catalog == null)
                throw new ArgumentException("ReactionOrder.SelectNext: catalog is null");
            var pending = new List<string>();
            var pendingSet = new HashSet<string>(StringComparer.Ordinal);
            if (pendingIds != null)
                foreach (var id in pendingIds)
                    if (id != null && pendingSet.Add(id)) pending.Add(id);
            if (pending.Count == 0)
                throw new ArgumentException(
                    "ReactionOrder.SelectNext: empty pending list - the caller must " +
                    "never ask without candidates (permutation law: the App's " +
                    "GetNonExhaustedGroups() is the only source)");

            var factById = new Dictionary<string, GroupFacts>(StringComparer.Ordinal);
            if (factsPending != null)
                foreach (var f in factsPending)
                    if (f != null && f.GroupId != null && !factById.ContainsKey(f.GroupId))
                        factById[f.GroupId] = f;

            int rung = skill < 1 ? 1 : (skill > 5 ? 5 : skill);
            var prof = SkillProfile.For("S" + rung);
            var postureE = string.IsNullOrEmpty(posture) ? "eliminate" : posture;

            // -- split: known (PlanRound-capable) vs unknown (fan imports) ----
            var knownFacts = new List<GroupFacts>();
            var unknownFacts = new List<GroupFacts>();
            foreach (var id in pending)
            {
                GroupFacts f;
                if (!factById.TryGetValue(id, out f))
                    throw new ArgumentException(
                        "ReactionOrder.SelectNext: no GroupFacts for pending id '" + id +
                        "' - the bridge contract requires a fact per candidate " +
                        "(synthesized minimal fact for unknown/wiped cards)");
                if (f.CardId != null && catalog.ContainsKey(f.CardId)) knownFacts.Add(f);
                else unknownFacts.Add(f);
            }

            // -- S-D section space: exception maps + proximity feed (card
            //    t_c52dd34b). NULL LAW: no space handed => all three stay
            //    null and every pre-S-D path runs bit-identical. Wachzone
            //    and 3c carry NO skill floor (Thomas-Gesetz 04.11.: the
            //    event-driven exceptions fire on ALL rungs - fact reaction
            //    is fairness, not an intelligence bonus); the zones inside
            //    PlanZones are trust-gated (fail-closed without a row).
            //    GroupFacts.Zone is the documented belief slot Efficiency
            //    reads - filled ONLY here, ONLY from a trust-gated belief.
            Dictionary<string, WachzoneDecision> wachMap = null;
            Dictionary<string, ThreeCFact> threeMap = null;
            ZoneBelief zones = null;
            if (sectionSpace != null)
            {
                var factList = new List<GroupFacts>(factById.Values);
                wachMap = SectionConsumers.WachzoneDecisions(
                    sectionSpace, criticalSections, factList, catalog);
                threeMap = SectionConsumers.ThreeCPushes(sectionSpace, pending, roundNo);
                zones = SectionConsumers.PlanZones(sectionSpace, view, prof, criticalSections);
                if (zones != null)
                    foreach (var f in factList)
                        if (f.Zone == null)
                        {
                            var z = zones.ZoneOf(f.GroupId);
                            if (z != null) f.Zone = z;
                        }
            }

            // -- basis: PlanRound over the known pending figures ---------------
            var planPos = new Dictionary<string, int>(StringComparer.Ordinal);
            string finisherId = null;
            if (knownFacts.Count > 0)
            {
                var plan = BasisPlan(roundNo, knownFacts, catalog, postureE, prof,
                                     sessionSeed, counter,
                                     zones != null ? zones.RingZones : null,
                                     zones != null ? zones.ContestZone : null);
                var knownIds = new List<string>();
                foreach (var f in knownFacts) knownIds.Add(f.GroupId);
                OrderPlanner.AssertPermutation(plan, knownIds);
                for (int i = 0; i < plan.Slots.Count; i++)
                {
                    var s = plan.Slots[i];
                    planPos[s.GroupId] = i;
                    if (s.Tags != null && s.Tags.Contains("finisher_last"))
                        finisherId = s.GroupId;
                }
            }
            // Unknown block: priority==1 leads, then index order, then id -
            // the WHOLE block sits behind every known group (M6s-B8 law:
            // no popup for them, but no random-draw fallback either).
            unknownFacts.Sort((a, b) =>
            {
                int c = (b.PriorityOne ? 1 : 0).CompareTo(a.PriorityOne ? 1 : 0);
                if (c != 0) return c;
                c = a.Index.CompareTo(b.Index);
                if (c != 0) return c;
                return string.CompareOrdinal(a.GroupId, b.GroupId);
            });
            int posNext = knownFacts.Count;
            foreach (var f in unknownFacts) planPos[f.GroupId] = posNext++;

            // -- this round's reaction events (public feed facts, once) --------
            bool live = view != null && view.Round > 0;
            int vround = view != null ? view.Round : 0;

            bool objEvent = false; long objSeq = 0; string objHero = null;
            bool strikeEvent = false; long strikeSeq = 0; string strikeHero = null;
            if (live)
            {
                // ObjectiveChange credit terminal on THIS round (M10b/Player-19
                // law already resolved in the projection). The seq rides the
                // open-window ledger anchor ("obj:<text>", kind fact); the
                // legacy retro channel has no anchor -> seq 0.
                if (view.ObjectiveActorRound == vround && !string.IsNullOrEmpty(view.ObjectiveActor))
                {
                    objEvent = true; objHero = view.ObjectiveActor;
                    foreach (var h in view.Heroes)
                        if (h.HeroId == view.ObjectiveActor && h.LastSeenRound == vround
                            && h.LastSeenZone == "obj:" + view.Objective)
                            objSeq = h.LastSeenSeq;
                }
                // Strike against the mission-named figure THIS round. The
                // window channel names shooter AND exact event seq on the
                // named group's LastShot* (R-A); the pending-closer channel
                // (strikePending) credits a hero but leaves no shot seq -
                // honest fallback: hero known, seq 0. Never from absence.
                if (!string.IsNullOrEmpty(view.Objective))
                {
                    foreach (var g in view.Groups)
                    {
                        if (string.IsNullOrEmpty(g.DisplayName)) continue;
                        if (!WorldEventVector.TextNames(view.Objective, g.DisplayName)) continue;
                        if (g.LastShotRound == vround && g.LastShotHero != null)
                        {
                            if (!strikeEvent || g.LastShotSeq > strikeSeq)
                            { strikeEvent = true; strikeSeq = g.LastShotSeq; strikeHero = g.LastShotHero; }
                        }
                    }
                    if (!strikeEvent)
                        foreach (var h in view.Heroes)
                            if (h.Strikes > 0 && h.StrikeRound == vround)
                            { strikeEvent = true; strikeHero = h.HeroId; break; }
                }
            }

            // -- per-candidate push evaluation ---------------------------------
            var cands = new List<Cand>(pending.Count);
            foreach (var id in pending)
            {
                var f = factById[id];
                var c = new Cand
                {
                    Id = id,
                    Name = string.IsNullOrEmpty(f.Name) ? id : f.Name,
                    Fact = f,
                    Gv = view != null ? view.FindGroup(id) : null,
                    PlanPos = planPos[id],
                };
                bool named = live && c.Gv != null && !string.IsNullOrEmpty(c.Gv.DisplayName)
                             && !string.IsNullOrEmpty(view.Objective)
                             && WorldEventVector.TextNames(view.Objective, c.Gv.DisplayName);

                // R2 Schutzpflicht (>= S3): event AND anchor.
                if (rung >= 3 && live && (objEvent || strikeEvent))
                {
                    bool guardAnchor = f.Pattern == "guard" || named;
                    bool finisherNear = finisherId != null && finisherId == id
                        && (named || LedgerAtThisRound(view, id, vround));
                    if (guardAnchor || finisherNear)
                    {
                        c.Cls = ClassProtect; c.Push = "R2";
                        if (strikeEvent && (!objEvent || !seqVisible(objSeq) || strikeSeq >= objSeq))
                        { c.Seq = strikeSeq; c.SeqKnown = seqVisible(strikeSeq); c.Reason = "Ziel-Interaktion durch " + strikeHero; }
                        else
                        { c.Seq = objSeq; c.SeqKnown = seqVisible(objSeq); c.Reason = "Missionsziel-Interaktion durch " + objHero; }
                    }
                }
                // R1 Vergeltung (>= S2): shot inside an open window THIS round.
                if (c.Cls == 0 && rung >= 2 && live && c.Gv != null
                    && c.Gv.LastShotRound == vround && c.Gv.LastShotHero != null)
                {
                    c.Cls = ClassRetaliate; c.Push = "R1";
                    c.Seq = c.Gv.LastShotSeq; c.SeqKnown = true;
                    c.Reason = "Reaktion auf Beschuss durch " + c.Gv.LastShotHero;
                }
                // R3 Eindringling (>= S2): arrived this round inside a window.
                // No spawn seq is projected (R-B pinned the marker only) - the
                // honest reading is "seq not visible to the overlay", tie goes
                // to the basis position.
                if (c.Cls == 0 && rung >= 2 && live && c.Gv != null
                    && c.Gv.SpawnedInHeroWindow && c.Gv.DeployedRound == vround)
                {
                    c.Cls = ClassIntruder; c.Push = "R3";
                    c.Seq = 0; c.SeqKnown = false;
                    c.Reason = "Neue Ankunft in dieser Runde";
                }
                // S-D section-space exceptions (card t_c52dd34b, concept
                // positionierung v1 10+13.3). NO skill floor - IMMER
                // (Thomas-Gesetz 04.11.). They ride the classes of their
                // fact family INSTEAD of renumbering Thomas' pinned
                // collision order (Schutzpflicht > Vergeltung > Eindringling
                // stays 3>2>1, the class CONSTANTS are untouched law):
                //  * WZ rides ClassProtect - it is the section-space
                //    generalization of the guard duty (Baustein 1 IS R2's
                //    ground: mask overlap replaces the symbolic ledger).
                //    No event seq is carried (the fact keys on rounds) -
                //    an R2 with a visible seq therefore stays ahead, and
                //    several WZ candidates queue by BEDROHUNG (comparator
                //    branch, concept 13.3 "Bedrohungsordnung").
                //  * 3C rides ClassIntruder - the section-grounded sibling
                //    of R3 (activation + intruder SAME round, strictly the
                //    stronger Eindringling fact); ties with R3 fall to the
                //    basis position, exactly R3's honest no-seq law.
                // Only where no R push fired (c.Cls == 0): highest push
                // shown, no stacking (Thomas 04.11. #4 untouched).
                if (c.Cls == 0 && sectionSpace != null)
                {
                    WachzoneDecision wz;
                    if (wachMap != null && wachMap.TryGetValue(id, out wz))
                    {
                        c.Cls = ClassProtect; c.Push = "WZ";
                        c.Seq = 0; c.SeqKnown = false;
                        c.Threat = wz.Threat; c.ThreatKnown = wz.ThreatKnown;
                        c.Reason = wz.Reason;
                    }
                    else
                    {
                        ThreeCFact tc;
                        if (threeMap != null && threeMap.TryGetValue(id, out tc))
                        {
                            c.Cls = ClassIntruder; c.Push = "3C";
                            c.Seq = 0; c.SeqKnown = false;
                            c.Reason = SectionConsumers.ThreeCReason(sectionSpace, tc);
                        }
                    }
                }
                cands.Add(c);
            }

            // -- event-seq ordinal: the raw feed seq may exceed one class's
            //    1000-point block, so the score rides the RANK of the distinct
            //    known seqs (newest event -> highest ordinal). The raw seq
            //    stays visible in the account line.
            var seqs = new List<long>();
            foreach (var c in cands)
                if (c.Cls > 0 && c.SeqKnown && !seqs.Contains(c.Seq)) seqs.Add(c.Seq);
            seqs.Sort();
            foreach (var c in cands)
                c.Ordinal = c.Cls > 0 && c.SeqKnown ? seqs.IndexOf(c.Seq) : 0;

            // -- collision: class desc, seq desc, basis position asc ----------
            //    planPos is unique (plan slots + unknown block ranks), so the
            //    comparison is a total order - fully deterministic.
            cands.Sort((a, b) =>
            {
                int c = b.Cls.CompareTo(a.Cls);
                if (c != 0) return c;
                // S-D Bedrohungsordnung (concept 13.3, card t_c52dd34b):
                // among Wachzone candidates only - higher printed damage EV
                // first, no-catalog candidates last (unknown is never
                // better). Pre-S-D vectors carry no "WZ" push, so this
                // branch is inert there by construction.
                if (a.Push == "WZ" && b.Push == "WZ")
                {
                    if (a.ThreatKnown != b.ThreatKnown) return a.ThreatKnown ? -1 : 1;
                    if (a.ThreatKnown && b.ThreatKnown)
                    {
                        int t = b.Threat.CompareTo(a.Threat);
                        if (t != 0) return t;
                    }
                }
                c = b.Seq.CompareTo(a.Seq);
                if (c != 0) return c;
                return a.PlanPos.CompareTo(b.PlanPos);
            });

            var win = cands[0];
            if (!pendingSet.Contains(win.Id))
                throw new AssertionError(
                    "ReactionOrder.SelectNext: pick '" + win.Id + "' left the pending " +
                    "list - permutation invariant broken (never activate what the App " +
                    "did not hand as non-exhausted)");

            var sb = new System.Text.StringBuilder();
            sb.Append("TACTICAL next r").Append(roundNo).Append(" -> ")
              .Append(win.Name).Append(' ').Append(Account(win));
            if (cands.Count > 1)
                sb.Append(" ; vor ").Append(cands[1].Name).Append(' ').Append(Account(cands[1]));
            sb.Append(" \u2014 ").Append(win.Reason);
            why = sb.ToString();
            return win.Id;
        }

        static bool seqVisible(long seq) { return seq > 0; }

        /// Symbolic proximity per the Last-Seen-Ledger (R-B): the hero's
        /// latest anchor THIS round is "at:<group>" - a public presence fact
        /// (hypothesis kind for hits; consumers of proximity read exactly
        /// this symbol, never a coordinate).
        static bool LedgerAtThisRound(FeedWorldView view, string groupId, int vround)
        {
            foreach (var h in view.Heroes)
                if (h.LastSeenRound == vround && h.LastSeenZone == "at:" + groupId)
                    return true;
            return false;
        }

        // Account segment of one candidate for the debug why-line. Shape per
        // card spec:  |R2:3001(seq 41)|basis:2|  (seq omitted when the event
        // carries no visible seq; push "Basis" for an un-pushed figure).
        static string Account(Cand c)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('|');
            if (c.Cls > 0)
            {
                sb.Append(c.Push).Append(':')
                  .Append((long)c.Cls * ClassBlock + Math.Min(c.Ordinal, ClassBlock - 1));
                if (c.SeqKnown) sb.Append("(seq ").Append(c.Seq).Append(')');
            }
            else sb.Append("Basis:0");
            sb.Append("|basis:").Append(c.PlanPos).Append('|');
            return sb.ToString();
        }
    }
}
