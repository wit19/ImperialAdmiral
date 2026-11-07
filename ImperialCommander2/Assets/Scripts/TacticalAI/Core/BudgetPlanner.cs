// P33-H5 (Thomas 08.11, CONCEPT_drohungsbudget Sec.2 Stufe 2, card
// t_94d361bb): the Beschaffungs-Ranking. Instead of the strict sequence
// (reinforce-then-buy, always the most expensive card, never any saving)
// the tactical round-end popup ranks ALL legal actions in ONE list and
// lets public board facts decide the order.
//
// Laws encoded here (all deterministic, zero RNG, AppView facts only -
// the caller hands NOTHING the human at the table could not see):
//  * Null-Law (concept Sec.0/Sec.6): without ANY battle-zone fact Rank
//    answers null and the caller keeps today's strict sequence VERBATIM.
//    "fehlendes Fakt => kein Takt-Anspruch" is machine-pinned here, not
//    just by the popup gate - budget_rank.json case null_law_no_zones.
//  * Proximity: SectionGraph hop lower bound from the action's sections
//    to the nearest zone. The Loch-Gesetz holds (P33-H4): an unrecorded
//    route is NO CLAIM (never "far", never a penalty) - an unknown fact
//    contributes 0, it cannot push a rival behind.
//  * Achsen-Vorrang (controller order 07.11): the mission-axis zone
//    (H4b: rebel escape zone / axis actor section from the REAL victory
//    condition) is worth MORE than the feed zones - "nutze die
//    Achsen-Kampfzone als hohes Ziel-Sek-Gewicht".
//  * Urgency: losses x URG only for a reinforcement NEAR the fight
//    (<= 1 hop) - "verlustreichste Gruppe in Kampfnaehe".
//  * Posture weights (planPosture truth, concept Sec.3 "EINE Wahrheit"):
//    hunt (jagd)   -> buy bonus      (Neukauf-Waffenprioritaet)
//    defend (halten) -> reinforce + size (Verstaerkungs-/Groessenprioritaet)
//    Feed posture stays descriptive (H4 naming law) - the caller maps it
//    to hunt/defend only as the documented pre-plan fallback.
//  * Leash: a purchase landing on a section that holds own located groups
//    gets the support bonus (Stufe 3 leash law, lifted to the action
//    layer as the concept's "Friendly-Sektionen" input).
//  * Spar-Regel (concept Stufe 2, Player-33 deficit 2 "kein Horizont"):
//    a HEAVY purchase (>= 3/5 of the account) that would leave the rest
//    BELOW the cheapest still-buyable card (naechste-Kaufstufe) AND that
//    does not block reaching a BIGGER card with next round's income is
//    held back - threat may not rot into an unusable remainder while a
//    bigger purchase is one round away. Sparen NIE im Dumpfenster
//    (dumpWindow true => the rule is inert) and NIE for reinforcements
//    (keeping a fighting group at strength is never "wasting").
//  * Tie law: score, then kind (reinforce before buy - the proven
//    sequence - before hold), then name Ordinal, then id Ordinal. Never
//    a coin flip; the hold action (score 0) can only outrank actions the
//    Spar-Regel pushed negative.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// <summary>One candidate procurement action (the caller fills the
    /// facts; Rank fills AxisDist/ZoneDist/Leash/Score/Spared). Sections =
    /// where the action happens: a reinforcement's ENTRY sections (deploy
    /// ledger - the honest last-seen law), a purchase's fresh valid DP
    /// sections (where the landing could go). Empty list = no location
    /// fact, the proximity stages stay inert for this action.</summary>
    public sealed class BudgetAction
    {
        public string Kind;           // "buy" | "reinforce"
        public string Id;             // card id (unique among the actions)
        public string Name;
        public int Cost;              // the affordability law's cost
                                      // (buy: DeployCost, reinforce: Max(1,rcost))
        public int Losses;            // reinforce: dead figures (public signals,
                                      // caller takes the max - never a sum)
        public int Size;              // reinforce: currentSize; buy: card.size
        public int Firepower;         // printed attack dice (row fact; the
                                      // score itself does NOT weigh it)
        public readonly List<string> Sections = new List<string>();

        // ---- filled by Rank ------------------------------------------------
        public int AxisDist = -1;     // -1 = no claim (Loch-Gesetz)
        public int ZoneDist = -1;
        public bool Leash;
        public bool Spared;
        public int Score;

        public string Dump()
        {
            return Kind + " " + (Id ?? "")
                   + " cost=" + Cost
                   + " axis=" + (AxisDist < 0 ? "-" : AxisDist.ToString())
                   + " zone=" + (ZoneDist < 0 ? "-" : ZoneDist.ToString())
                   + " leash=" + (Leash ? "1" : "0")
                   + " losses=" + Losses
                   + " size=" + Size
                   + " spare=" + (Spared ? "1" : "0")
                   + " score=" + Score;
        }
    }

    /// <summary>The deterministic action ranking (concept Sec.2 Stufe 2).
    /// Pure function: same inputs, same order, no RNG.</summary>
    public static class BudgetPlanner
    {
        // Documented weight table - every value is pinned by a vector in
        // budget_rank.json, so a retune must re-read the playtest, not
        // silently drift.
        public const int AxisNear = 40, AxisRing = 28, AxisFar = 12;
        public const int ZoneNear = 30, ZoneRing = 21, ZoneFar = 9;
        public const int UrgencyPerLoss = 5;
        public const int HuntBuyBonus = 15;
        public const int DefendReinforceBonus = 15;
        public const int DefendSizeWeight = 2;
        public const int LeashBonus = 8;
        // Heavy purchase: 5*COST >= 3*THREAT (integer math, no rounding).
        public const int HeavyNumerator = 5, HeavyDenominator = 3;
        // Big enough to drop any real score below the hold action (0).
        public const int SparePenalty = 400;

        public const string KindBuy = "buy";
        public const string KindReinforce = "reinforce";
        public const string KindHold = "hold";
        public const string PostureHunt = "hunt";
        public const string PostureDefend = "defend";

        // kind rank for the tie law: reinforce before buy before hold.
        static int KindRank(string kind)
        {
            if (kind == KindReinforce) return 0;
            if (kind == KindBuy) return 1;
            return 2;   // hold / anything unrecognized
        }

        /// <summary>Rank all legal actions, best first. The hold action
        /// (score 0, Kind "hold", empty ids) is appended and part of the
        /// returned order - the caller stops consuming AT the hold row.
        ///
        /// graph null OR both zone lists empty => NULL: no battle-zone
        /// fact exists, the Null-Law sends the caller back to the strict
        /// sequence (bit-identical legacy order).
        /// posture: PostureHunt / PostureDefend / "" (inert); any other
        /// token is treated like "" (honest: unknown posture claims nothing).
        /// threat: the CURRENT account (before the popup's gain? no - the
        /// caller passes the value AFTER its threat gain, the same number
        /// the affordability laws read).
        /// projectedGain: the documented next-round income estimate
        /// (threatLevel (+1 landing)) - only ever used by the Spar-Regel's
        /// reachability test, never as a spendable value.
        /// futureHandCosts: DeployCost law costs of hand cards that are
        /// NOT affordable now - the bigger card the saving would reach.
        /// friendlySections: sections holding own located groups (entry
        /// ledger). null/empty => leash stage inert.
        /// actions: junk rows fail CLOSED (null entries, unknown kinds,
        /// empty ids are skipped).</summary>
        public static List<BudgetAction> Rank(SectionGraph graph,
                                              IList<string> battleZones,
                                              IList<string> axisZones,
                                              IList<string> friendlySections,
                                              string posture,
                                              long threat, long projectedGain,
                                              bool dumpWindow,
                                              IList<BudgetAction> actions,
                                              IList<long> futureHandCosts)
        {
            // ------------------------------------------------- Null-Law gate
            if (graph == null) return null;
            bool anyFact = false;
            if (battleZones != null)
                foreach (var z in battleZones)
                    if (!string.IsNullOrEmpty(z)) { anyFact = true; break; }
            if (!anyFact && axisZones != null)
                foreach (var z in axisZones)
                    if (!string.IsNullOrEmpty(z)) { anyFact = true; break; }
            if (!anyFact) return null;

            if (actions == null) return null;
            var list = new List<BudgetAction>();
            foreach (var a in actions)
            {
                if (a == null || string.IsNullOrEmpty(a.Id)) continue;
                if (a.Kind != KindBuy && a.Kind != KindReinforce) continue;
                list.Add(a);
            }

            // --------------------------------------------------- score parts
            foreach (var a in list)
            {
                a.AxisDist = Distance(graph, axisZones, a.Sections);
                a.ZoneDist = Distance(graph, battleZones, a.Sections);
                a.Leash = a.Kind == KindBuy && SectionHit(a.Sections, friendlySections);

                int s = ProximityWeight(a.AxisDist, AxisNear, AxisRing, AxisFar)
                      + ProximityWeight(a.ZoneDist, ZoneNear, ZoneRing, ZoneFar);

                bool near = (a.AxisDist >= 0 && a.AxisDist <= 1)
                         || (a.ZoneDist >= 0 && a.ZoneDist <= 1);
                if (a.Kind == KindReinforce && a.Losses > 0 && near)
                    s += a.Losses * UrgencyPerLoss;

                if (posture == PostureHunt && a.Kind == KindBuy)
                    s += HuntBuyBonus;
                if (posture == PostureDefend && a.Kind == KindReinforce)
                    s += DefendReinforceBonus + a.Size * DefendSizeWeight;

                if (a.Leash) s += LeashBonus;
                a.Score = s;
            }

            // ---------------------------------------------------- Spar-Regel
            // Cheapest other buy cost = the "naechste Kaufstufe" the rest
            // must not fall below. Bigger-card reachability uses the SAME
            // affordability law the caller built the candidates with.
            if (!dumpWindow && threat > 0)
            {
                foreach (var a in list)
                {
                    if (a.Kind != KindBuy) continue;
                    if ((long)HeavyNumerator * a.Cost < (long)HeavyDenominator * threat)
                        continue;                               // not heavy
                    long rest = threat - a.Cost;
                    long restCost = CheapestOtherBuy(list, a);
                    if (restCost < 0 || rest >= restCost) continue;
                    if (!BiggerReachable(list, futureHandCosts, a, threat, projectedGain))
                        continue;
                    a.Spared = true;
                    a.Score -= SparePenalty;
                }
            }

            // ------------------------------------------------------ total law
            // The hold action JOINS the sort at score 0: "sparen und nichts
            // tun" is a real option, and only Spar-penalised actions fall
            // BEHIND it. Appending it after the sort would make the penalty
            // cosmetically invisible (own-harness caught this, run #2).
            list.Add(new BudgetAction { Kind = KindHold, Id = "", Name = "" });
            list.Sort((x, y) =>
            {
                if (x.Score != y.Score) return x.Score > y.Score ? -1 : 1;
                int kr = KindRank(x.Kind) - KindRank(y.Kind);
                if (kr != 0) return kr;
                int n = string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.Ordinal);
                if (n != 0) return n;
                return string.Compare(x.Id ?? "", y.Id ?? "", StringComparison.Ordinal);
            });
            return list;
        }

        // Nearest recorded hop distance from any of the action's sections
        // to any zone. -1 = no claim (no sections, no graph route). The
        // Loch-Gesetz: unmeasured routes are skipped, NEVER counted as far.
        static int Distance(SectionGraph graph, IList<string> zones, IList<string> sections)
        {
            if (graph == null || zones == null || sections == null) return -1;
            int best = -1;
            foreach (var s in sections)
            {
                if (string.IsNullOrEmpty(s)) continue;
                foreach (var z in zones)
                {
                    if (string.IsNullOrEmpty(z)) continue;
                    int d = graph.DistanceLowerBound(s, z);
                    if (d < 0) continue;
                    if (best < 0 || d < best) best = d;
                }
            }
            return best;
        }

        static int ProximityWeight(int dist, int near, int ring, int far)
        {
            if (dist < 0) return 0;             // no claim contributes 0
            if (dist == 0) return near;
            if (dist == 1) return ring;
            return far;
        }

        static bool SectionHit(IList<string> sections, IList<string> set)
        {
            if (sections == null || set == null) return false;
            foreach (var s in sections)
                if (!string.IsNullOrEmpty(s))
                    foreach (var f in set)
                        if (string.Equals(s, f, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Cheapest buy cost among the OTHER candidates (the cheapest thing
        // still buyable from the leftover). None => -1 => no saving stage.
        static long CheapestOtherBuy(List<BudgetAction> list, BudgetAction self)
        {
            long best = -1;
            foreach (var b in list)
            {
                if (b.Kind != KindBuy || ReferenceEquals(b, self)) continue;
                if (best < 0 || b.Cost < best) best = b.Cost;
            }
            return best;
        }

        // Saving must aim at something: a card BIGGER than the deferred
        // purchase that the NEXT round's income could pay for. Candidates
        // already affordable now trivially qualify (income only grows);
        // not-yet-affordable hand costs qualify up to threat + projectedGain.
        static bool BiggerReachable(List<BudgetAction> list, IList<long> futureHandCosts,
                                    BudgetAction self, long threat, long projectedGain)
        {
            foreach (var b in list)
            {
                if (b.Kind != KindBuy || ReferenceEquals(b, self)) continue;
                if (b.Cost > self.Cost) return true;
            }
            if (futureHandCosts != null)
                foreach (var c in futureHandCosts)
                    if (c > self.Cost && c <= threat + projectedGain) return true;
            return false;
        }
    }
}
