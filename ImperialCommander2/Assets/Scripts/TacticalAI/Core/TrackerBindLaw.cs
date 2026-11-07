using System;
using System.Collections.Generic;

namespace TacticalAI.Core
{
    /// <summary>
    /// P33-H3 (findings_Player-33 §2c, Player-33.log Z743-749/Z799-801): the
    /// wound tracker's instance-binding and close-commit laws as pure logic.
    /// Lives in Core so the SAME functions HPTrackerContainer (Unity side)
    /// calls are what tools/TacticalAiHarness/SectionsTrackerBind.cs pins -
    /// no mirror, no second implementation.
    ///
    /// Law 1 BOARD-INSTANCE (ResolveBoard): a damage wheel may only ever be
    /// bound to the DeploymentCard instance sitting in the deployed list
    /// ("the board instance"). Two objects carrying the SAME card id exist in
    /// the wild: SagaDGPrefab.Init swaps a token to ovrd.customCard, and
    /// CreateCustomCard inherits the id of the pool card it represents - all
    /// id-keyed list logic stays blind to the difference. Committing to a
    /// non-board twin makes committed damage oscillate between the full and
    /// the empty array mid-fight. Reference identity decides first; a same-id
    /// board card replaces any other instance and the wheels re-init FROM the
    /// board object - never the other way around (a popup copy is never
    /// authoritative). Null-Law: no list, no id, or the id is not on the
    /// board (setup previews, hero cards, legacy pool views) -> bind exactly
    /// as before, canon path unchanged.
    ///
    /// Law 2 SINGLE-COMMIT PER OPEN (TryBeginCommit): a container commits at
    /// most once per open session, and an open session is armed ONLY by
    /// ResetTracker (a fresh read from a card) - not by plain re-enablement.
    /// Player-33 root cause: UpdateAndHide() committed unconditionally, so a
    /// SECOND close (OK double-click, or Space during the 0.5 s fade-out -
    /// CardViewPopup.Update has no close guard) wrote the already-reset zero
    /// wheels back onto the card: [2,5,0] -> [0,0,0]. The test+flip is one
    /// transaction, so the stray close can neither wipe nor double-log.
    /// </summary>
    public static class TrackerBindLaw
    {
        public sealed class BindDecision<T>
        {
            public T Bound;       // instance the tracker must bind to
            public bool Rebound;  // true when `given` was replaced by the board card
            public string Reason; // "as-is" | "board-rebound" | "null-law"
        }

        public static BindDecision<T> ResolveBoard<T>(IList<T> deployed, T given, Func<T, string> idOf)
        {
            if (given == null)
                return new BindDecision<T> { Bound = given, Rebound = false, Reason = "null-law" };
            var id = idOf(given);
            if (string.IsNullOrEmpty(id) || deployed == null)
                return new BindDecision<T> { Bound = given, Rebound = false, Reason = "null-law" };

            // reference identity wins first: the given card IS on the board.
            for (int i = 0; i < deployed.Count; i++)
                if (ReferenceEquals(deployed[i], given))
                    return new BindDecision<T> { Bound = given, Rebound = false, Reason = "as-is" };

            // a same-id board card exists and `given` is not it: bind to the
            // board instance (first match, OrdinalIgnoreCase - same id law as
            // Extensions.GetDeploymentCard) and re-init the wheels from it.
            for (int i = 0; i < deployed.Count; i++)
            {
                var d = deployed[i];
                if (d == null) continue;
                var did = idOf(d);
                if (!string.IsNullOrEmpty(did) &&
                    string.Equals(did, id, StringComparison.OrdinalIgnoreCase))
                    return new BindDecision<T> { Bound = d, Rebound = true, Reason = "board-rebound" };
            }

            // id not deployed at all (preview / hero card / pool view): old path.
            return new BindDecision<T> { Bound = given, Rebound = false, Reason = "null-law" };
        }

        /// True = this close commits (wheels -> card); the open flag is
        /// consumed atomically. False = already committed (or never opened):
        /// NOTHING may be written to the card.
        public static bool TryBeginCommit(ref bool wasVisible)
        {
            if (!wasVisible) return false;
            wasVisible = false;
            return true;
        }
    }
}
