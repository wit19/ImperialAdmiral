// O-A (concept offizier_supporter v1 §2, Thomas-Gesetz 04.11., card
// t_4acaa220): officers (printed Order/Executive Order token, id exclusion
// DG075/DG078) are SUPPORTERS - they keep commanding per doctrine, but their
// SELF-COMBAT runs only while an emergency is active. This file is the pure
// decision function of that codex; NO popup/ActionBuilder consumes it yet
// (O-B/O-C hops). Until S2 the codex is deliberately silent: S1 shows the
// beginner mistake on purpose, so skill < 2 ALWAYS returns None (bit-identical
// old path, pinned in the harness officer_doctrine.json section).
//
// The four emergency stages, FIRST MATCH WINS (E1 before E2 before E3
// before E4 - the why-line names exactly one reason):
//   E1 "steht unter Feuer"   - the R-A shooter attribution (GroupView
//       LastShot*) says a hero hit THIS group inside the LIVE round. The
//       same-consumption law as OverlordFocus/ReactionOrder: a shot from a
//       finished round is history, not causation.
//   E2 "direkt bedroht"      - a hero menaces the officer in melee. The sim
//       has the adjacency fact; the app-side popup does NOT (fairness law).
//       So the channel is the explicit signal directThreatSeen handed in by
//       the EXECUTING site (the human sees the table). No signal = unknown =
//       no E2. Never guessed from distances the Core cannot see.
//   E3 "Missionsziel akut,   - the executing site names the hero threatening
//        nur er kann"         the mission objective (threatHeroOrNull, the
//       public designation channel; null = unknown = no E3) AND the view
//       carries an objective designation AND no other living imperial group
//       can attack that hero. "Can attack" here is the PUBLIC catalog fact
//       ONLY - life + printed ranged capability (DeploymentCard.AttackType),
//       NEVER a position. Unknown prints (cards without a catalog entry) and
//       a missing catalog count as "could attack" -> no E3 (fail-closed,
//       unsicher => KEIN E3, Thomas' Nie-raten-Gesetz).
//   E4 "letzte lebende       - the roster holds no other living imperial
//        Gruppe"              group (NAlive > 0) besides the supporter.
//
// Inputs are public table knowledge only (concepts §4.1-§4.5): printed card
// data, public feed facts, human-sighted signals handed in from outside.
// Pure: same inputs -> bit-identical stage + why-line. No wall-clock, no
// own Random, no hidden state.
using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// Self-combat emergency stages of the supporter codex (concept §2).
    public enum OfficerEmergency
    {
        None = 0,
        E1   = 1,   // steht unter Feuer
        E2   = 2,   // direkt bedroht (Nahkampf-Signal der ausfuehrenden Stelle)
        E3   = 3,   // Missionsziel akut, nur er kann
        E4   = 4,   // letzte lebende imperiale Gruppe
    }

    /// Stage + one German public-safe why-line (OrderSlot.Why class: the
    /// popup wording itself is ui.json data in O-C; this line is the
    /// planner-side account, pinned byte-exact by the harness).
    public sealed class OfficerEmergencyResult
    {
        public readonly OfficerEmergency Stage;
        public readonly string Why;

        public OfficerEmergencyResult(OfficerEmergency stage, string why)
        {
            Stage = stage;
            Why = why ?? "";
        }

        public static readonly OfficerEmergencyResult None =
            new OfficerEmergencyResult(OfficerEmergency.None, "");
    }

    public static class OfficerDoctrine
    {
        /// Codex skill floor (Thomas 04.11.): ab S2. S1 keeps the old path
        /// bit-identical on purpose ("der Spieler sieht den Fehler").
        public const int MinSkill = 2;

        /// The supporter's self-combat codex. Pure over its inputs:
        ///  - view:   the R-A/B projection (E1 fact, hero labels). May be
        ///            null: then the E1/E3 stages simply cannot fire.
        ///  - self:   the activating supporter's facts; a non-supporter or a
        ///            dead group is None by definition (the flag itself is
        ///            the DeploymentCard.Supporter law, see there).
        ///  - roster: ALL imperial group facts the caller knows (the E3
        ///            "only he can" and E4 "last one" counts read it).
        ///  - threatHeroOrNull: the hero the EXECUTING site designates as the
        ///            objective threatener (E3) / melee threatener label
        ///            (E2). null = unknown - the stage stays silent.
        ///  - catalog: printed card data for the E3 capability facts
        ///            (same catalog the planner runs on). null = no printed
        ///            knowledge = no E3 (fail-closed).
        ///  - directThreatSeen: E2 signal (app: the human sees melee; sim:
        ///            the adjacency fact). false = not seen / unknown.
        public static OfficerEmergencyResult EmergencyFor(
            int skill, FeedWorldView view, GroupFacts self,
            IReadOnlyList<GroupFacts> roster, string threatHeroOrNull,
            Dictionary<string, DeploymentCard> catalog = null,
            bool directThreatSeen = false)
        {
            // S1 law (fixture-pinned): the codex never speaks below S2.
            if (skill < MinSkill) return OfficerEmergencyResult.None;
            // Nothing is inferred from missing facts (Nie-raten-Gesetz).
            if (self == null || roster == null) return OfficerEmergencyResult.None;
            // Codex of the SUPPORTERS only - plain troops fight as before;
            // field commanders DG075/DG078 never carry the flag (the id
            // exclusion lives in DeploymentCard.FieldCommanderExceptions).
            if (!self.Supporter) return OfficerEmergencyResult.None;
            // A wiped group has no emergencies.
            if (self.NAlive <= 0) return OfficerEmergencyResult.None;

            // E1 "steht unter Feuer": the shot must be from the LIVE round
            // (LastShotRound == view.Round, R-A attribution with the hero
            // credited - same consumption law as OverlordFocus.HeroThreat and
            // ReactionOrder R1).
            if (view != null && view.Round > 0)
            {
                var gv = view.FindGroup(self.GroupId);
                if (gv != null && gv.LastShotHero != null && gv.LastShotRound == view.Round)
                    return new OfficerEmergencyResult(OfficerEmergency.E1,
                        "Er steht unter Feuer" + HeroLabel(view, gv.LastShotHero) + ".");
            }

            // E2 "direkt bedroht": the EXECUTING site's signal only (the
            // human sees the melee; the sim passes its adjacency fact). No
            // signal = unknown = stay silent - never guessed.
            if (directThreatSeen)
                return new OfficerEmergencyResult(OfficerEmergency.E2,
                    "Ein Held bedroht ihn direkt" + HeroLabel(view, threatHeroOrNull) + ".");

            // E3 "Missionsziel akut, nur er kann": designation + objective +
            // public capability count, every unknown blocks (fail-closed).
            if (view != null && catalog != null
                && !string.IsNullOrEmpty(threatHeroOrNull)
                && !string.IsNullOrEmpty(view.Objective))
            {
                var hv = FindHero(view, threatHeroOrNull);
                if (hv != null && !hv.Defeated && PrintedRanged(catalog, self.CardId))
                {
                    bool otherCouldAttack = false;
                    for (int i = 0; i < roster.Count; i++)
                    {
                        var g = roster[i];
                        if (g == null || g.NAlive <= 0) continue;
                        if (g.GroupId == self.GroupId) continue;
                        DeploymentCard card;
                        // Unknown print (fan import without a catalog entry):
                        // capability unknowable -> could attack -> no E3.
                        if (!catalog.TryGetValue(g.CardId ?? "", out card) || card == null)
                        { otherCouldAttack = true; break; }
                        // Printed melee = cannot reach by this fact set
                        // (NO position is consulted - concept §2 E3 law).
                        if (string.Equals(card.AttackType, "Ranged",
                                          StringComparison.OrdinalIgnoreCase))
                        { otherCouldAttack = true; break; }
                    }
                    if (!otherCouldAttack)
                        return new OfficerEmergencyResult(OfficerEmergency.E3,
                            "Das Missionsziel ist akut bedroht" +
                            HeroLabel(view, threatHeroOrNull) +
                            " - nur er kann eingreifen.");
                }
            }

            // E4 "letzte lebende Gruppe": nobody else in the roster lives.
            int living = 0;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].NAlive > 0) living++;
            if (living == 1)
                return new OfficerEmergencyResult(OfficerEmergency.E4,
                    "Er ist die letzte lebende imperiale Gruppe.");

            return OfficerEmergencyResult.None;
        }

        // Printed ranged capability from the catalog (public card fact).
        // Missing card / empty lane = NOT proven ranged (self-blocks E3,
        // fail-closed both sides).
        static bool PrintedRanged(Dictionary<string, DeploymentCard> catalog, string cardId)
        {
            DeploymentCard card;
            if (cardId == null || !catalog.TryGetValue(cardId, out card) || card == null)
                return false;
            return string.Equals(card.AttackType, "Ranged", StringComparison.OrdinalIgnoreCase);
        }

        static FeedWorldView.HeroView FindHero(FeedWorldView view, string heroId)
        {
            if (view.Heroes == null || heroId == null) return null;
            for (int i = 0; i < view.Heroes.Count; i++)
                if (view.Heroes[i] != null && view.Heroes[i].HeroId == heroId)
                    return view.Heroes[i];
            return null;
        }

        // Public-safe label " (Name)" or "" when the id resolves to nothing -
        // an unresolvable hero is named by its id, never dropped silently and
        // never invented.
        static string HeroLabel(FeedWorldView view, string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return "";
            var h = FindHero(view, heroId);
            var label = h != null && !string.IsNullOrEmpty(h.Name) ? h.Name : heroId;
            return " (" + label + ")";
        }
    }
}
