using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    // ==================================================================
    //  Player-24 BEFEHLSZEITPTUNKT-Gesetz (Thomas 04.11 Go) — "Nutzlast
    //  vor Ort" replaces the blind "buff_before_receiver".
    //
    //  The printed facts (De/DeploymentGroups/enemies.json, DG004/DG005/
    //  DG006, verified 04.11): a command is an INTERRUPT, not a stat buff.
    //    - plain Befehl (DG004/DG005): campaign = receiver may interrupt
    //      to perform A TURN (a turn is a turn - the command re-orders it,
    //      it does not multiply it; skirmish = +2 MP). It therefore never
    //      earns extra actions -> stays OUT of this law entirely (the
    //      plain group keeps its Player-21 head-slot demotion and is not
    //      an actor here).
    //    - Vollstreckungsbefehl (DG006, elite): the receiver interrupts
    //      for ONE movement OR attack action - that is the only command
    //      that CREATES an action, so only the elite's slot is a timing
    //      question at all.
    //
    //  The law, per living elite supporter, on ZONE facts only (the app
    //  never sees board cells - Nie-raten-Gesetz; the exact 2-field
    //  command ring is TABLE knowledge and the why-line says so):
    //    receiver = ENGAGED FIRST (Player-24 log review, Thomas 04.11):
    //      attack-pattern groups whose zone carries a fresh same-round
    //      hero anchor compete first - the table commander orders the
    //      troop in contact, where the interrupt pays. Nobody engaged ->
    //      the living attack-pattern group with the highest
    //      PrintedEvTotal x VitalityFrac (the finisher key; lower Index
    //      breaks ties) whose card is known (unknown EV = never chosen).
    //    engaged (a hero carries a FRESH same-round fact anchor inside the
    //      receiver's zone) -> the supporter acts AFTER the receiver:
    //      the troop activates (moves, shoots), the interrupt pays the
    //      SECOND attack and picks its target after the move.
    //    not engaged -> the supporter acts BEFORE the receiver: the
    //      command's movement leg doubles the troop's reach this round
    //      (two movement phases before its own shot).
    //  Ring honesty: supporter zone == receiver zone (both known) is a
    //  zone-fact claim ("beide in <Sektion>"); anything else is silently
    //  unknowable here and the why-line hands the 2-field check to the
    //  table - the same E2 discipline OfficerDoctrine keeps.
    //
    //  Consumption: OrderPlanner.PlanRound (ApplySlotBuff) only, and the
    //  bridge feeds it only on the SupporterMaskTrustEnabled rung (S4/S5).
    //  null/absent decision = the old blind chain, bit-identical canon.
    // ==================================================================

    /// One supporter's timing decision for this round.
    public sealed class CommandTimingDecision
    {
        public string SupporterGroup;   // the elite command group
        public string ReceiverGroup;    // the chosen receiver group
        public bool Before;             // true = supporter BEFORE receiver
        public bool RingShared;         // zone fact: both in the same zone
        public string SectionName;      // shared zone name (or null)
        public string Why;              // public-safe German line (pinned)
    }

    public static class CommandTiming
    {
        /// Per-round timing table: supporter group id -> decision. Groups
        /// without an entry keep the blind chain untouched.
        public static Dictionary<string, CommandTimingDecision> ForRound(
            int roundNo, IReadOnlyList<GroupFacts> roster,
            SectionSpace space, FeedWorldView view,
            Dictionary<string, DeploymentCard> cards)
        {
            var map = new Dictionary<string, CommandTimingDecision>(StringComparer.Ordinal);
            if (roster == null || roster.Count == 0 || cards == null)
                return map;

            // receiver pool: living attack-pattern groups (OrderPlanner's
            // own definition - aggressive + hunter).
            var receivers = new List<GroupFacts>();
            foreach (var g in roster)
                if (g != null && g.NAlive > 0 && IsAttackPattern(g.Pattern))
                    receivers.Add(g);
            if (receivers.Count == 0)
                return map;

            foreach (var s in roster)
            {
                if (s == null || s.NAlive <= 0) continue;
                if (!s.Supporter || s.MovementBuffOnly) continue;   // elites only
                GroupFacts rec;
                Rational key;
                PickReceiver(s, receivers, cards, space, view, roundNo,
                             out rec, out key);
                if (rec == null)
                    continue;
                bool engaged = HeroInZone(space, view, rec.Zone, roundNo);
                bool shared = !string.IsNullOrEmpty(s.Zone) &&
                              string.Equals(s.Zone, rec.Zone, StringComparison.Ordinal);
                var d = new CommandTimingDecision
                {
                    SupporterGroup = s.GroupId,
                    ReceiverGroup = rec.GroupId,
                    Before = !engaged,
                    RingShared = shared,
                };
                // Public-safe German (Thomas-Gesetz: what, not how). The
                // receiver is named - friendly table knowledge, zero
                // guessing. The command's printed reach fact rides along.
                string line = engaged
                    ? "Befehlszeitpunkt: im Bereich von " + Name(rec) +
                      " steht ein frischer Helden-Anker - erst rückt die Truppe " +
                      "ein und schießt, dann zahlt der Vollstreckungsbefehl den " +
                      "zweiten Angriff."
                    : "Befehlszeitpunkt: kein Held im Bereich von " + Name(rec) +
                      " - der Vollstreckungsbefehl geht vor, die Bewegungshilfe " +
                      "verdoppelt die Reichweite der Truppe.";
                if (shared)
                {
                    string sec = space != null && space.Graph != null
                        ? space.Graph.SectionName(s.Zone) : null;
                    d.SectionName = string.IsNullOrEmpty(sec) ? s.Zone : sec;
                    line += " || Befehlsring: beide in " + d.SectionName + ".";
                }
                else
                    line += " || Befehlsring am Tisch prüfen: der Befehl erreicht nur Figuren in 2 Feldern.";
                d.Why = line;
                map[s.GroupId] = d;
            }
            return map;
        }

        /// Highest PrintedEvTotal x VitalityFrac among the receiver pool
        /// (the finisher key; the card must be known - unknown EV is never
        /// bet on). Ties keep the LOWER Index (Python max keeps first).
        ///
        /// Player-24 log review (Thomas 04.11, Player-24.log R2): ENGAGED
        /// FIRST. A raw-EV pick sent the Vollstreckungsbefehl's plan to the
        /// strongest troop ACROSS the map while the heroes stood right next
        /// to the garrison - the table commander orders the troop HE SEES IN
        /// CONTACT, so the pool is tiered: groups whose zone carries a fresh
        /// same-round hero anchor compete first (there the interrupt pays
        /// the second attack); only when NO attack zone shows an anchor does
        /// the global strongest win (movement-leg branch, unchanged).
        static void PickReceiver(GroupFacts supporter, List<GroupFacts> receivers,
                                 Dictionary<string, DeploymentCard> cards,
                                 SectionSpace space, FeedWorldView view, int roundNo,
                                 out GroupFacts best, out Rational bestKey)
        {
            best = null; bestKey = Rational.Zero;
            // tier 1: engaged candidates (fresh hero anchor in their zone)
            foreach (var r in receivers)
            {
                DeploymentCard t;
                if (!ReceiverCandidate(r, supporter, cards, out t)) continue;
                if (!HeroInZone(space, view, r.Zone, roundNo)) continue;
                var k = OrderPlanner.WithEv(r, t).PrintedEvTotal * r.VitalityFrac;
                if (best == null || k > bestKey ||
                    (k == bestKey && r.Index < best.Index))
                { best = r; bestKey = k; }
            }
            if (best != null) return;
            // tier 2: nobody is in contact - the global strongest gets the
            // movement help (the pre-review law, bit-identical fallback).
            foreach (var r in receivers)
            {
                DeploymentCard t;
                if (!ReceiverCandidate(r, supporter, cards, out t)) continue;
                var k = OrderPlanner.WithEv(r, t).PrintedEvTotal * r.VitalityFrac;
                if (best == null || k > bestKey ||
                    (k == bestKey && r.Index < best.Index))
                { best = r; bestKey = k; }
            }
        }

        /// Shared candidate gate for both receiver tiers: not the supporter
        /// itself, and the printed EV must be KNOWN (unknown EV never chosen).
        static bool ReceiverCandidate(GroupFacts r, GroupFacts supporter,
                                      Dictionary<string, DeploymentCard> cards,
                                      out DeploymentCard card)
        {
            card = null;
            if (r == null || r.NAlive <= 0) return false;
            if (string.Equals(r.GroupId, supporter.GroupId, StringComparison.Ordinal))
                return false;
            if (r.CardId == null || !cards.TryGetValue(r.CardId, out card) || card == null)
                return false;
            return true;
        }

        /// A hero with a FRESH (this-round) fact anchor inside the zone.
        /// Stale anchors are yesterday's news (R-A live-round discipline);
        /// missing space/view/zone = no knowledge = not engaged.
        static bool HeroInZone(SectionSpace space, FeedWorldView view,
                               string zone, int roundNo)
        {
            if (space == null || view == null || view.Heroes == null ||
                string.IsNullOrEmpty(zone))
                return false;
            foreach (var h in view.Heroes)
            {
                if (h == null || h.Defeated || string.IsNullOrEmpty(h.HeroId))
                    continue;
                if (!string.Equals(space.HeroInSection(h.HeroId), zone, StringComparison.Ordinal))
                    continue;
                if (space.HeroSectionRound(h.HeroId) == roundNo)
                    return true;
            }
            return false;
        }

        static bool IsAttackPattern(string pattern)
        {
            return pattern == "aggressive" || pattern == "hunter";
        }

        static string Name(GroupFacts g)
        {
            return string.IsNullOrEmpty(g.Name) ? g.GroupId : g.Name;
        }
    }
}
